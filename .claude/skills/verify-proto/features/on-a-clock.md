# A game on a clock

Life, Snake, Cascade and Warband's battle move on their own: the board beats at a rate the game
sets, `+` and `-` wind it, the keys the game names steer it, and Enter opens the prompt. A beat is
a move, so nothing about real time reaches the rules: a record of such a game replays beat for
beat, and with the terminal's input redirected there is no clock at all, only the line that
types a beat by hand.

## Sub-features

- `clock-touch` a cell named at the prompt changes the board and is said in the log.
- `clock-beat` `beat` (Cascade) or `step` (Life) advances one beat by hand and the heading counts it.
- `clock-still` with input redirected nothing moves between lines: two reads of the heading agree.
- `clock-record` the record writes the touch and each beat as its own line.
- `clock-why` `why <cell>` answers a question and moves nothing.

## How to get to it (user POV)

- Run `dotnet run -- cascade play 1`, `dotnet run -- life play 1`, `dotnet run -- snake play 1`.
- At a terminal, `p` runs Life and `.` steps it; the arrows steer Snake; `w` `a` `s` `d` move
  Cascade's hand. `settings` at the menu, page `keys`, moves the letters.
- Type `beat`, `step`, `faster`, `slower` or `speed 7` at the prompt of any of them.

## Driving it with drive.ps1

Preconditions:

- `drive.ps1 doctor` ends `worth driving`.
- `<run>` is a run name not used before.

- **Cascade: touch, then two beats by hand.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words cascade,play,1,--seed,42,--view,plain -Lines f7,beat,beat,quit`.
  It prints `exit code: 0`; the first heading is `12 touches left - nothing has been touched yet`
  and the one after `f7` is `11 touches left - 1 cell turning`.
- **The touch was said.** The log reads `f7 begins turning.` after `f7`.
- **A beat lands it.** After each `beat` the log reads `it lands.` With seed 42 the heading after
  both beats still reads `11 touches left - 1 cell turning`: what landed set the next cell
  turning. Quote the headings from the helper's list; there are four, one per line typed.
- **Nothing moved on its own.** The heading after `f7` and the heading the next line was typed
  under are the same text: no beat came between them.
- **The record.** The kept `<stamp>-cascade-1p-seed42.log` has, uncommented, `format 1`,
  `deal 1 42 you`, `f7`, `beat`, `beat`.
- **Life: toggle, then one generation.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words life,play,1,--seed,42,--view,plain -Lines f7,step,quit`.
  The headings run `Generation 0 - 108 cells alive`, `Generation 0 - 109 cells alive`,
  `Generation 1 - 121 cells alive`; the log reads `f7 comes alive.` after `f7`. The kept
  `<stamp>-life-1p-seed42.log` has `toggle f7` then `step`: the record writes the long spelling.
- **A question moves nothing.** Run
  `pwsh .claude/skills/verify-proto/drive.ps1 console -Run <run> -Words life,play,1,--seed,42,--view,plain -Lines "why f7",quit`.
  The heading stays `Generation 0 - 108 cells alive` throughout, the screen answers
  `f7 is empty, with 4 cells round it.` and what the rule will do with it, and no record is kept.
- **Proof.** The three console files and the two records under `evidence\<run>\records\`. Then
  `pwsh .claude/skills/verify-proto/drive.ps1 stop -Run <run>`.

## Gotchas

- The clock only runs at a real terminal. Nothing here proves the rate, `+`, `-` or a steering
  key; those need a person at a keyboard or `Conforms.against` in the game's test suite, which
  drives the pulse without a clock.
- A line with a space in it (`why f7`, `speed 7`) is one element of `-Lines`: quote it, and do
  not put a comma in it.
- Cascade's log says what each beat lands and what it reaches; the exact words depend on the
  seed, so quote them from the transcript rather than predicting them.
- Snake's snakes move on the clock and on nothing else, so under redirection a Snake console
  shows the deal and waits; its words are in `src/Games/Snake/README.md` and are not mapped yet.
- Warband's muster is turn-based and its battle is on the clock; `beat` at the battle is the line
  to type, and the record writes it.
