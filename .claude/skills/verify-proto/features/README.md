# Proto verification map

This directory is the maintained source for verifying what a player meets when Proto runs: the
program this repository builds, with eight games in it. Read this index before driving the
program, then use the matching feature file as the recipe. A proof that drives one convenient
entry point is incomplete when the feature file lists others.

## Baseline preconditions

- `dotnet build PrototypingGameEngine.slnx` has said `Build succeeded.` since the last source
  change, so `bin\Debug\net10.0\Proto.exe` is what the sources say.
- `pwsh .claude/skills/verify-proto/drive.ps1 doctor` ends `worth driving`.
- Every command of a run carries the same `-Run <name>`; the name is new for this run.
- Every instance runs from the run's box under `%TEMP%\proto-verify\<run>`, never from the
  repository root: the program reads `settings.txt` and writes `logs\` wherever it is started,
  and the repository's `logs\` is committed fixtures.
- Ports are chosen per run, from 5120 upwards. 5000, 5001 and 5100 to 5102 belong to the
  repository's own harnesses.
- Never drive, and never stop, a `Proto` process this run did not start.

## Driving conventions

- Start every recipe from the baseline unless its preconditions say otherwise.
- A console is driven by typed lines: `drive.ps1 console -Words <program args> -Lines <lines>`.
  With its input redirected the program has no keys and no clock, reads whole lines in order, and
  the lines are typed all at once.
- A page is driven over HTTP: `drive.ps1 serve` starts it, `drive.ps1 browse -Lines <lines>`
  opens the stream and posts the lines to `/say`.
- Seed every deal with `--seed 42` so the same drive meets the same board every time.
- Prefer the handles the program gives a reader: the `=== heading ===` over the board, the lines
  of the log, `Record saved to logs\<name>.log`, the exit code, the HTTP status, the page's
  `<h1>`. Do not count rows of the board.
- Treat every command as literal. Keep quoted lines unchanged.
- `drive.ps1 stop -Run <run>` after every recipe, pass or fail.

## Proof and skip reporting

- Capture the action and the state after it: the heading before a line, the line, the heading
  and log after it. The console transcript redraws the whole screen after every line, so quote
  the heading sequence the helper prints rather than diffing screens.
- Console proof is the evidence file the helper writes: command, lines typed, exit code, the
  records left, the transcript.
- Page proof is the helper's `browse.txt` (statuses, headings, the log's last lines), the saved
  page and the saved stream.
- Mutation proof is the record under `evidence\<run>\records\`: quote its non-comment lines. A
  game undone back to the deal writes no record, and that is the behaviour, not a failure.
- Name the feature ID and the entry point used with every artifact.
- Report an unreachable path with the attempted command and the unmet precondition.
- Do not report a skipped entry point as verified through a different path.

## Feature entry contract

Each feature file starts with an H1 title and one paragraph describing the user-visible behaviour.
It then uses exactly four H2 sections in this order.

1. `Sub-features` lists short IDs with one line for each behaviour.
2. `How to get to it (user POV)` lists every user entry point.
3. `Driving it with drive.ps1` starts with `Preconditions:` and uses labelled bullets that pair
   each user action with an exact command and an observable result.
4. `Gotchas` lists traps that can waste or invalidate a verification run.

Keep implementation details out of the map. Name only user paths, stable handles, required
state, commands, and observable proof.

## Features

- [Play a game at the keyboard](./play-at-keyboard.md) covers dealing, moving, a refusal, undo and
  redo, history, and the record `quit` writes.
- [Against the machine](./against-the-machine.md) covers a machine seat answering on its own, and
  the record naming its skill.
- [Records and replay](./records-and-replay.md) covers `save`, taking a record up with `replay`,
  and a replay that adds nothing writing nothing.
- [In a browser](./in-a-browser.md) covers the served page: the word at the door, the board over
  the stream, lines sent to the table, and the record it writes.
- [A hosted table](./hosted-table.md) covers two consoles at one table over a real socket, and a
  house dealing tables, through the repository's own harness.
- [A game on a clock](./on-a-clock.md) covers Life and Cascade driven a beat at a time.

## Not yet mapped

The menu (`Proto <game>` with no command), the four settings pages and `settings.txt`, `--colour`
and the `rich` view, `--from <file>` at `serve` and `host`, `--fill` at a house, https with
`--cert` and `--behind`, and the specific moves of Turncoats, Diplomacy, Compile, Snake and Warband.
Each game's own `src/Games/<Name>/README.md` lists the words it takes.
