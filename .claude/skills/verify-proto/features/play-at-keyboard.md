# Play a game at the keyboard

`play <n>` deals a game for that many and plays it at this terminal: a heading saying whose turn
it is, the board, the players, a box of commands, a log of what the game has said, and a prompt
that reads the game's own moves and the table's commands. A refused line is said back and the
turn does not pass; `quit` writes the record and leaves.

## Sub-features

- `play-deal` a count the game takes is dealt; one it does not is refused at the door.
- `play-move` a move typed at the prompt changes the heading and is said in the log.
- `play-refusal` a move the rules refuse is said back and the heading stays.
- `play-undo-redo` `undo` and `redo` walk the game back and forward and are written down.
- `play-history` `history` prints the record so far, one numbered line a move.
- `play-quit` `quit` writes the record under `logs\` and the program exits 0.

## How to get to it (user POV)

- Run `dotnet run -- <game> play <n>` from a clone, or `Proto <game> play <n>` from a build.
- Open the menu with `dotnet run -- <game>` and choose `1  New game`.
- Add `--seed <n>` for the same deal again, `--view plain` or `--view rich` for how it is drawn.

## Driving it with drive.ps1

Preconditions:

- `drive.ps1 doctor` ends `worth driving`.
- `<run>` is a run name not used before, for example `play-1130`.

- **Deal, move, refuse, undo, redo, read, quit.** Type six lines at a game of noughts and
  crosses. Run `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,play,2,--seed,42,--view,plain -Lines 5,5,undo,redo,history,quit`.
  It prints `exit code: 0` and the headings, in order: `Turn 1 - X (you) to play`,
  `Turn 2 - O (you) to play`, `Turn 2 - O (you) to play`, `Turn 1 - X (you) to play`,
  `Turn 2 - O (you) to play`, `Turn 2 - O (you) to play`.
- **The move was said.** Open the evidence file it names. After the first `5` the log reads
  `X takes square 5.` and the board shows `X` where `5` was.
- **The refusal was said.** After the second `5` the log reads `Square 5 already has X in it.`
  and the heading is still `Turn 2 - O (you) to play`.
- **Undo and redo.** After `undo` the log reads `Taken back: place 5.` under `Turn 1`; after
  `redo` it reads `Made again: place 5.` under `Turn 2`.
- **History.** After `history` the screen lists `1  turn 1  X: place 5  X takes square 5.` and a
  line for each of the undo and the redo.
- **Quit writes the record.** The transcript ends `Record saved to logs\<stamp>-tictactoe-2p-seed42.log.`
  and the helper prints `record kept: evidence\<run>\records\<that file>`. Its non-comment lines
  are `format 1`, `deal 2 42 you you`, `place 5`, `place 5`, `undo`, `redo`.
- **Refused at the door.** Ask for a count the game does not take. Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,play,3 -Lines quit`.
  It prints `exit code: 1` and no headings; the evidence file's stdout is empty and its stderr
  is the one line `3 players? The game takes 2.`
- **Proof.** `evidence\<run>\01-console-tictactoe-play-2.txt`, `evidence\<run>\02-console-tictactoe-play-3.txt`
  and `evidence\<run>\records\<stamp>-tictactoe-2p-seed42.log`. Then run
  `pwsh .claude/skills/verify-proto/drive.ps1 stop -Run <run>`; it must not warn of records in
  the repository's `logs\`.

## Gotchas

- With its input redirected the program reads whole lines: there are no steering letters, no
  arrow keys and no clock. A game's key is typed as its word (`up`, `step`, `beat`).
- The lines are typed all at once. A line that opens a question of its own (Turncoats'
  `negotiate` asks for a `return <colour>`) must be followed by its answer, or the console sits
  on that question until the helper's timeout kills it and reports `killed after 60 seconds`.
- Every line redraws the whole screen, so the transcript is long. Read the helper's heading
  list first and go to the transcript for the log lines.
- A game undone back to the deal and then quit writes no record. That is the program's rule ("a
  sitting that added nothing writes nothing"), not a missing side effect.
- The second `5` is refused by the rules, not at the prompt; `seven` would be refused at the
  prompt (`'seven' is not a square`) and never reach the rules. Both leave the heading alone.
- Each game writes its own words. Turncoats opens on `Turn 1`, Compile on `The draft`, Warband
  on `The muster`, Life on `Generation 0`; check the game's README before asserting a heading.
