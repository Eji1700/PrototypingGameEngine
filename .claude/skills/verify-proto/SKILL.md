---
name: verify-proto
description: Launch and drive Proto, the terminal-and-browser game table that PrototypingGameEngine builds, to prove a feature works the way a player meets it - a game played at a piped console, a page served over HTTP, a hosted table over a socket - and keep the evidence. Use when asked to verify, demonstrate, reproduce or screenshot a change in the running program rather than in the tests/*.fsx scripts.
---

# Verify Proto

Proto is one program with eight turn-based games in it, and a player meets it on three surfaces.
The first is a **terminal**: `Proto <game> play <n>` deals a game and reads lines at a prompt,
and every game, every command and every refusal is reachable there. The second is a **page**:
`serve <n>` is the same game drawn in a browser over a stream, with lines posted back to it. The
third is a **socket**: `host <n>` and `join <address>` put the players at different machines, and
`house` deals such tables from a page. The terminal is the surface to prove on first; the page
when the change touches what a browser sees; the socket when it touches what reaches another
player.

`features/README.md` is the map of what is verified and how. Read the feature file before
driving; a proof that drives one convenient entry point is incomplete when the map lists others.

## Launch

Build once, from the repository root:

```
dotnet build PrototypingGameEngine.slnx
```

Ready when it prints `Build succeeded.` with `0 Error(s)`; the program is then
`bin\Debug\net10.0\Proto.exe`. The helper runs that file directly, not `dotnet run`, so a drive
has a handle to exactly the process it started.

The terminal surface has no server to keep alive: each drive starts its own process, in its own
box, and it exits when its lines run out. A page or a table does stay up:

```
pwsh .claude/skills/verify-proto/drive.ps1 serve -Run <run> -Port <port> -Words <game>,serve,<n>,--seed,42,--port,<port>,--code,<word>
```

Ready when it prints `pid <n> is listening on http://localhost:<port>`. The same command with
`host,<n>,--port,<port>,--open` or `house,--port,<port>,--open` in `-Words` starts the other two
kinds. Teardown is always `drive.ps1 stop -Run <run>`.

A **run** is a name you choose once (`-Run play-1130`) and give to every command of one
verification. Its box is `%TEMP%\proto-verify\<run>`: every process runs from there, so the
`settings.txt` it reads and the `logs\` it writes are the box's, never the repository's. Its
evidence is `.claude\skills\verify-proto\evidence\<run>\`.

Isolation: two runs side by side are fine on different ports with different run names. The
repository's own harnesses take 5000, 5001 and 5100 to 5102 and stop every `Proto` by name; use
ports from 5120 up, and do not start `tools/wire.ps1` or `tools/smoke.ps1` while a run of yours
is up.

## Doctor

```
pwsh .claude/skills/verify-proto/drive.ps1 doctor
pwsh .claude/skills/verify-proto/drive.ps1 doctor -Run <run>
```

Read-only. It says whether `Proto.exe` exists and is newer than every source file, that the .NET
10 SDK and `curl.exe` are there, and, given a run, that each pid the run started is alive, is
`Proto`, was told the port it is on, and is the process that owns that port. It also notes `Proto`
processes not started by the run (never drive or stop those), ports the repository's harnesses use
that are busy, and how many records the repository's `logs\` holds. It ends `worth driving` with
exit code 0, or `not worth driving until the FAIL lines are put right` with exit code 1. Run it
first whenever anything looks off.

## Drive

**A console.** The program reads whole lines when its input is redirected: there are no keys, no
steering letters and no clock, so a game on a clock only moves when `beat` or `step` is typed.
The lines are typed all at once and read in order.

```
pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,play,2,--seed,42,--view,plain -Lines 5,5,undo,redo,history,quit
```

`-Words` is the program's own command line, comma-separated; `-Lines` is what gets typed. A line
with a space is quoted (`"why f7"`); a comma cannot be typed. The helper prints the evidence
file's path, `exit code: <n>`, every `=== heading ===` in order, and `record kept:` for each
record the box holds. Stable handles: the heading over the board (`Turn 2 - O (you) to play`,
`Generation 1 - 121 cells alive`, `11 touches left - 1 cell turning`), the lines of the log
(`X takes square 5.`, `Square 5 already has X in it.`, `Taken back: place 5.`), the line
`Record saved to logs\<name>.log.`, and the exit code: 0 after `quit`, 1 for a refusal at the
door, which is one line of stderr such as `3 players? The game takes 2.` Always `--seed 42` and
`--view plain`.

**A page.** After `serve`, drive the page over HTTP the way the browser's own script does: one
session, the stream held open, lines posted to `/say`.

```
pwsh .claude/skills/verify-proto/drive.ps1 browse -Run <run> -Port <port> -Code <word> -Lines 5,5,quit
```

It fetches `/` without the word (expect 401), then with `?code=<word>` (expect 200, and the
game's `<title>`), opens `/stream` and waits for the first `<h1>`, then for each line posts
`/say?line=<line>` and waits for the next frame. It prints one line per step and keeps the page,
the whole stream and a summary. `-Path /at/<name>` drives a table a house dealt. For what only a
real browser shows (buttons clicked, the Enter key, the tab's title changing when the turn comes
round), `pwsh tools/smoke.ps1 -Game <game>` drives headless Edge or Chrome over the DevTools
protocol; it is Windows-only, uses port 5000 and stops every `Proto` first.

**A socket.** Two consoles at one hosted table, a house dealing one, and a house restarted with
`--fill` are driven by the repository's `pwsh tools/wire.ps1 -Game <game>`, which starts and stops
everything itself; `features/hosted-table.md` says how to keep its output as evidence.

Never drive a `Proto` process this run did not start, and never answer for a surface by driving
a different one.

## Evidence

Everything goes under `.claude\skills\verify-proto\evidence\<run>\`, numbered in the order it was
made, and the folder is gitignored:

- `NN-console-<game>-<command>-<n>.txt`: the command line, the box, the lines typed, the exit
  code, the records left, then the whole transcript and stderr.
- `NN-serve-<port>-started.txt` and, at `stop`, `NN-serve-<port>.out`: what a served program
  printed, including the address and the word at the door.
- `NN-browse-page.html`, `NN-browse-stream.txt`, `NN-browse.txt`: the page shell, every frame
  the stream sent, and the summary of statuses, headings and the log's last lines.
- `records\<stamp>-<game>-<n>p-seed<seed>.log`: every record the box held, copied after each
  console and at `stop`. Its uncommented lines are the deal and the moves exactly as typed.

What a proof needs. Exercise the path a player takes - lines at the prompt, a page's `/say` -
not the engine's `Update` functions and not the `tests/*.fsx` harnesses; those are tests, and this
skill exists to see the program do it. Capture the action and the state after it: the heading
before the line, the line, the heading and log after it, which the transcript holds because every
line redraws the screen. Verify the side effect beside the screen: the record under `records\`
with the move written in it, the serving program's output, and the repository's `logs\` left
alone (`stop` warns if it was not). A feature with two surfaces is proved on both. There are no
mocks to use: the socket and the HTTP are the real boundary, and `--seed` is what makes a drive
repeatable. There is no dry-run mode; a game undone back to the deal writes nothing, and that is
the program's rule, not a skipped write.

## Cleanup

```
pwsh .claude/skills/verify-proto/drive.ps1 stop -Run <run>
```

It kills each pid the run wrote down (after checking it is still `Proto` or the helper's own
`curl`), copies the served programs' output and the box's records into the evidence folder,
warns if records appeared in the repository's `logs\` during the run, and removes the box.
Evidence survives; it is removed by hand only when the person asks. Run `stop` after a failed
drive too, so no port is left held. Never `Stop-Process -Name Proto`: that is what the
repository's harnesses do for their own instances, and it would take down any other run's.

## Helpers

`drive.ps1` is the one helper, in PowerShell 7 (`pwsh` 7.6 is on this machine). Its five
commands, all run from the repository root:

```
pwsh .claude/skills/verify-proto/drive.ps1 doctor [-Run <run>]
pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words <program args> -Lines <typed lines> [-Label <word>] [-Timeout 60]
pwsh .claude/skills/verify-proto/drive.ps1 serve   -Run <run> -Port <n> -Words <program args> [-Timeout 60]
pwsh .claude/skills/verify-proto/drive.ps1 browse  -Run <run> -Port <n> [-Code <word>] [-Path /] -Lines <typed lines>
pwsh .claude/skills/verify-proto/drive.ps1 stop    -Run <run>
```

Two things to know when calling it from a shell tool. Do not pipe `serve` through anything
(`| tail`, `| grep`): the served program inherits the pipe and the call never returns; let it
print, or redirect to a file. And a console that is waiting for a line nobody typed is killed at
`-Timeout` and reported as `killed after <n> seconds`, with the transcript kept, so a hang is a
finding rather than a stall.

Keep the map honest as the program changes: `/maintain-verification-skill` re-reads it against
the code, and a change to what a game says will need the quoted lines here updated, as the
assertions in `tests/` are.
