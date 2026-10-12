# Records and replay

A game writes itself to `logs\` as `<stamp>-<game>-<n>p-seed<seed>.log` when it ends, on
`restart`, on the way out, and whenever `save` is typed. `replay <file>` takes it up where it was
left, against the same players, and a replay that adds nothing writes nothing.

## Sub-features

- `record-save` `save` writes the record now, and says where.
- `record-name` the file is named by stamp, game, seats and seed, and the name is fixed at the deal.
- `record-shape` the file opens `format 1` then `deal <n> <seed> <seats>`, and every move is a
  line exactly as typed, with everything else a comment.
- `replay-takes-up` `replay <file>` lands on the position the record was saved from.
- `replay-unchanged` a replay that adds nothing leaves the file byte for byte as it was.
- `replay-continues` a move made at a replay is written to the same file.

## How to get to it (user POV)

- Type `save` or `quit` at the prompt of any game.
- Run `dotnet run -- <game> replay logs\<file>.log`.
- Choose `3  Continue a game` at the menu, which lists what there is.
- Run `play`, `serve` or `host` with `--from logs\<file>.log` in place of a count.

## Driving it with drive.ps1

Preconditions:

- `drive.ps1 doctor` ends `worth driving`.
- `<run>` is a run name not used before, and `stop` has not been run on it yet: a replay reads
  the record from the run's box, and `stop` removes the box.

- **Write a record with save.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,play,2,--seed,42,--view,plain -Lines 5,save,quit`.
  The transcript says `Record saved to logs\<stamp>-tictactoe-2p-seed42.log.` after `save` and
  again after `quit`, both naming the same file. The helper prints `record kept:` with that name.
  Note the name as `<file>`.
- **Its shape.** `evidence\<run>\records\<file>` opens with comment lines, then `format 1`,
  `deal 2 42 you you`, a comment `#   1  turn 1, X`, `place 5`, and the comment
  `#      X takes square 5.`. Nothing else is uncommented.
- **Take it up.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,replay,logs\<file>,--view,plain -Lines history,quit`.
  The first heading is `Turn 2 - O (you) to play`, the screen says
  `The moves taken up can be taken back with 'undo', or read with 'history'.`, and `history`
  lists `1  turn 1  X: place 5  X takes square 5.`
- **Nothing added, nothing written.** The transcript of that replay does not say
  `Record saved`, and `evidence\<run>\records\<file>` is the same bytes as after the first
  console (compare the two copies the helper kept, or the box's file against the evidence copy
  before `stop`).
- **Carry on from it.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,replay,logs\<file>,--view,plain -Lines 1,quit`.
  The log reads `O takes square 1.`, the transcript says `Record saved to logs\<file>.` with the
  same name, and the kept record now has `place 1` after `place 5`.
- **Proof.** The three console files and `evidence\<run>\records\<file>`. Then
  `pwsh .claude/skills/verify-proto/drive.ps1 stop -Run <run>`.

## Gotchas

- Records are relative to where the program runs, and every console of a run runs from the run's
  box, so `logs\<file>` in `-Words` is the box's file. The copy under `evidence\` is for reading,
  not for replaying.
- The helper copies records to `evidence\<run>\records\` after every console, overwriting the
  copy, so the copy is the record as it stood after the latest console.
- A path with a comma in it cannot be passed in `-Words`; the stamped names never have one.
- `replay` of a file the program cannot read is refused at the door with a line saying what was
  wrong with it, and exit code 1; the test records under the repository's `logs\` are all readable.
- The stamp is the clock at the deal, so two deals in the same second with the same game, seats
  and seed share a name. Do not deal twice in one second and expect two records.
