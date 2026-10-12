# Against the machine

`--rival <skill>` seats the machine at the next seat after yours, once per seat given away. The
table says which seat it took, the machine answers in the log the moment your turn ends without
anything more being typed, and the record's deal line names the skill so a replay keeps it.

## Sub-features

- `rival-seated` the table says `Played by the machine: <seat> (<skill>).` before the first board.
- `rival-answers` the machine's move is in the log straight after yours, unasked.
- `rival-recorded` the deal line reads `you <skill>`, and the machine's move is written down.
- `rival-undo` `undo` takes back the machine's move and then yours.
- `rival-refused` a skill the game has no machine for is refused at the door.

## How to get to it (user POV)

- Run `dotnet run -- <game> play <n> --rival <skill>`; `--rival hard --rival easy` fills two seats.
- Run `dotnet run -- <game> serve <n> --rival <skill>` for the same in a browser.
- At the menu, `1  New game` asks who each seat is.
- `--help` at each game lists the skills it has: `The machine plays: easy, medium, hard`.

## Driving it with drive.ps1

Preconditions:

- `drive.ps1 doctor` ends `worth driving`.
- `<run>` is a run name not used before.

- **Seat the machine and move once.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,play,2,--rival,hard,--seed,42,--view,plain -Lines 5,quit`.
  It prints `exit code: 0` and the headings `Turn 1 - X (you) to play` then `Turn 3 - X (you) to play`.
- **The seat was announced.** The transcript has `Played by the machine: O (hard).` before the
  first heading.
- **The machine answered unasked.** After the one typed line the log reads `X takes square 5.`
  then `O takes square 9.`, and no second line was typed to get it.
- **The record names the skill.** The kept record's non-comment lines are `format 1`,
  `deal 2 42 you hard`, `place 5`, `place 9`.
- **Undo takes back both.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,play,2,--rival,hard,--seed,42,--view,plain -Lines 5,undo,quit`.
  The log reads `Taken back: place 9.` then `Taken back: place 5.` and the final heading is
  `Turn 1 - X (you) to play`. No record is kept from this console: everything was taken back.
- **A skill it has not got.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words tictactoe,play,2,--rival,grandmaster -Lines quit`.
  It prints `exit code: 1`, no headings, and the evidence file's stderr is the one line
  `'grandmaster' is not a way for the machine to play. There is easy, medium, hard.`
- **Proof.** The three console files under `evidence\<run>\` and
  `evidence\<run>\records\<stamp>-tictactoe-2p-seed42.log`. Then
  `pwsh .claude/skills/verify-proto/drive.ps1 stop -Run <run>`.

## Gotchas

- `hard` at noughts and crosses never loses; do not assert a win for the person.
- The machine plays at random among moves it rates alike, so with `--seed 42` the square it
  takes is fixed but with no seed it is not. Always seed.
- `--rival` once gives away the seat after yours; the third seat of a game for three is still a
  person, and the console will wait for that person's line.
- The machine's move is written as its own line in the record (`place 9`), so `history` and the
  record both count two moves after one typed line.
