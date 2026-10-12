# A hosted table

`host <n>` opens a table for that many and waits for them to arrive; `join <address>` sits a
console down at it from another terminal or machine. A console alone sees the table filling,
both are drawn a board once it is full, a move at one reaches the other, and a console that
types `quit` is told its seat is kept. `house` deals such tables on demand from a page.

## Sub-features

- `host-seats` consoles joining are given seat 1, then seat 2, and told so.
- `host-waiting` a console alone sees `Waiting for the table to fill`, not a board.
- `host-filled` once the table is full both consoles are drawn the opening board.
- `host-reaches` a move made at one console is said at the other.
- `host-quit` `quit` at a console says `You are up from the table` and `Your seat is kept`, and
  the console exits.
- `house-deals` `POST /open` with `players=2` at a house deals a table named under `/at/`.
- `house-joined` `join <address> --table <name>` sits a console at a house's table; a name no
  table has is answered `no table by that name`.

## How to get to it (user POV)

- Run `dotnet run -- <game> host <n>` on one machine; it prints the address and the word.
- Run `dotnet run -- <game> join <address> --code <word>` on each of the others.
- Run `dotnet run -- <game> house` and open its page; `Open a table` for a size, then read out
  the address or join with `--table <name>`.
- Choose `2  Join a table` at the menu.

## Driving it with drive.ps1

Preconditions:

- `drive.ps1 doctor` ends `worth driving` and lists no `Proto` process not started by this run:
  the repository's harness below stops every `Proto` by name before it starts.
- No run of yours is up. Ports 5100, 5101 and 5102 are free.
- `<run>` is a run name not used before, and `.claude\skills\verify-proto\evidence\<run>\` exists
  (`drive.ps1 doctor -Run <run>` after any other command of the run makes it; or `mkdir` it).

- **Two consoles at one table, a house, and a restart that takes the game up.** The helper cannot
  hold two consoles open at once, so this is driven by the repository's socket harness, which
  starts the table and both consoles itself. Run
  `pwsh tools/wire.ps1 -Game tictactoe *> .claude/skills/verify-proto/evidence/<run>/wire-tictactoe.txt`
  from the repository root, then read that file.
- **Seated in turn.** The file has `ok   a console can sit down at a hosted table` and
  `ok   and the next is given the next seat`.
- **Waiting, then a board each.** `ok   a console alone is shown the table filling up rather than a board`
  and `ok   and once it is full, both are drawn a board`.
- **The move reached the other console.** `ok   a move made at one console reaches the other`.
- **Getting up.** `ok   a console that types quit is told it is up from the table`,
  `ok   and told the seat is kept for it`, `ok   and stops, rather than sitting at a prompt nothing answers`.
- **A house.** `ok   a house deals a table when asked for one`,
  `ok   and a move made at one reaches the other, through the house`, and
  `ok   a console naming a table that is not there is told so rather than dropped`.
- **The last word.** The file ends `all checks passed`, and the harness's exit code was 0.
- **The same for a game that hides its moves.** Run
  `pwsh tools/wire.ps1 -Game warband *> .claude/skills/verify-proto/evidence/<run>/wire-warband.txt`;
  the other console is told `musters, out of your sight` and nothing of what was mustered.
- **Proof.** The two `wire-*.txt` files. There is nothing to `stop`: the harness stops what it
  started and removes the records it wrote to the repository's `logs\`. Confirm with
  `git status --short logs`, which prints nothing.

## Gotchas

- `tools/wire.ps1` starts the program with `dotnet run`, so the first table takes a build's time
  to answer, and it stops every process named `Proto` before each stage. Anything of yours that
  is up when it runs is killed, and its `stop` will then say the pid had already exited.
- It writes records into the repository's `logs\` while it runs and removes them after. A run
  interrupted part way leaves them there; `git status --short logs` shows them, and they can be
  deleted, but look at each first.
- The harness takes a game name only from `turncoats`, `tictactoe`, `compile` and `warband`:
  Life and Cascade seat one, Snake moves on a clock, and Diplomacy writes in secret.
- `join` without `--code` at a table with a word at the door is refused at the door; `host --open`
  has none, and that is what the harness uses.
- A console at a hosted table is driven over its own stdin exactly as `drive.ps1 console` drives
  one, but it has to stay up while the other joins, which is why the helper does not do it.
