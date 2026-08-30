# Life

Conway's Game of Life, on a board of 26 by 16 with its edges joined. There is one seat, and
whoever sits in it does not play: the deal is a soup, the rule runs on a clock, and what the
watcher does is start it and stop it, step it, wind it, and draw on the board. Nothing is hidden,
nothing is won and the game never ends, and [no machine](../../../README.md#against-the-machine)
sits at it — the rule is already playing.

```powershell
dotnet run -- life play 1              # a soup, running; p stops it and starts it again
dotnet run -- life play 1 --seed 42    # that soup, the same every time
dotnet run -- life serve 1             # the same board in a browser
```

The menu, the table's own words, records, views and colours are the engine's, described [one
directory up](../../../README.md); this file is what Life adds to them.

## The rules

- The board is 26 columns by 16 rows, 416 squares, and its edges are joined: the column after `z`
  is `a` again and the row below 16 is 1, so every cell has eight neighbours and what leaves one
  side arrives at the other. [Torus.fs](Rules/Torus.fs) says so, on the
  [`Grid`](../../Common/Grid.fs) three games share.
- A living cell with two or three living neighbours lives on. An empty cell with exactly three
  comes alive. Everything else is empty next generation, and corners count.
- The deal is a soup: every square is asked of the generator once and comes alive on a roll under
  30 in 100, so about three in ten are filled. A seed is a pattern, and the same seed is the same
  pattern every time. The deal is generation 0, and the generation is what the record calls the
  turn.
- A board is dealt running, at speed 5 of 9. A beat of the clock is one generation, and speed *n*
  beats every 560 − 50*n* milliseconds — 510 at 1, 310 at 5, 110 at 9, between about two and nine
  generations a second. `+` and `-` wind it a notch and `speed 7` goes straight to one; winding
  past either end does nothing, and a notch outside 1 to 9 is refused.
- `run` turns the rule the other way from wherever it is; `start` and `stop` say which, and asking
  for what already holds does nothing. A beat over a board that is stopped, settled or empty does
  nothing and writes nothing, so a stopped board costs no lines however long the clock beats over
  it; a beat that arrives at a settled or an empty board says so, once.
- `step` is one generation and `step 10` ten, at most 100 in a run. A run stops early where the
  board settles or dies, and says so. It is refused at a board with nothing on it, and at a still
  life, where the next generation would be this one again.
- Naming a cell turns it on, or off again if it was on; a cell off the board is refused, and told
  where the board ends. A board just drawn on forgets what it was beating between.
- `clear` sweeps the board, and is refused when there is nothing to sweep.
- Where the rule has got to is one of four things: still going; settled, when the next generation
  would be this one again; beating, when the board is back where it was two generations ago; or
  empty. Only a beat of two is noticed, since the world keeps two generations behind it and no more.
- Settling and dying are said plainly, and neither ends the game: `Over` is never true, so a board
  the rule has run out on is still a board to draw on. There is no resigning — `resign` is refused,
  there being nobody to resign to — and the door refuses any number of players but 1.

## The words

| | or | |
| --- | --- | --- |
| `f7` | `toggle f7`, `t f7` | turn cell f7 on, or off again |
| `step` | `s`; `.` at a terminal | one generation |
| `step 10` | `s 10`, `10` | ten generations, up to 100 |
| `run` | `p`; space or `p` in a browser | start the rule, or stop it |
| `start` | `go` | start it, saying which outright |
| `stop` | `pause`, `halt` | stop it, likewise |
| `faster`, `slower` | `+`, `-`; `quicker` | wind the clock a notch |
| `speed 7` | | straight to a notch, 1 to 9 |
| `clear` | `c` at a terminal | sweep the board |
| `why f7` | `ask f7` | what the rule will do with that cell, and why — a question, not a move |
| `beat` | | one beat of the clock, spelt out, for a console with nothing to press |

A cell is a letter for the column, `a` to `z`, and a number for the row, 1 to 16, in either case:
`F7` is `f7`. A bare cell is a move and a bare number is a run, and the two cannot be mistaken for
each other. The record keeps the long form — `toggle f7`, `step 10`, `run`, `start`, `stop`,
`faster`, `slower`, `speed 7`, `clear`, `beat` — which is why the long form is read. A word that is
neither is told what a cell looks like, and `step x` what a run looks like.

`undo`, `redo`, `history`, `save`, `notes`, `commands`, `log`, `sound`, `mute`, `view`, `restart`,
`players`, `help` and `quit` are [the table's](../../../README.md#at-the-prompt), the same at
every game. `restart` deals another soup here, and `restart 42` that one.

## The board

The heading says where the rule has got to — `Generation 4 - 5 cells alive`, `Generation 0 -
settled: 4 cells that will not change again`, `Generation 2 - 3 cells, beating between two
shapes`, `Generation 1 - nothing is left alive` — and under it are five blocks:

| | |
| --- | --- |
| The board | the grid, `#` for a living cell and `.` for an empty one, under the letters the columns are named by and beside the numbers of the rows, with a note on naming a cell |
| The run | the generation, the living out of 416 squares, the speed and how to stop or start it, which of the four things is happening, and the rule as a note |
| What next | `stop` or `run`, whichever applies, then `step`, `step 10`, `slower`, `faster`, `undo`, `clear`, `restart` — each is the line it types, and on a page each is a button |
| Commands | the box of every command |
| Log | what the game has been saying |

```
=== Generation 4 - 5 cells alive ===

THE BOARD
      abcdefghijklmnopqrstuvwxyz
  6   ..........................
  7   ........#.................
  8   ......#.#.................
  9   .......##.................
  10  ..........................
```

`notes` hides the two notes, `commands` hides What next and Commands, and `log` hides the log. All
three views draw the board as rows of text, a page included, with the What next buttons beside
The run.

At a terminal `p` runs and stops the rule, `.` steps it, `c` clears the board and `+` and `-` wind
it, on the keypad too; Enter opens the prompt for anything longer, and the clock itself is
[the engine's](../../../README.md#a-game-on-a-clock). While the clock beats — stopped or not, until
space or `h` holds it, which is the table's key rather than a move — a terminal draws only the
heading, The board and The run, and the log if it is on, which is why the line in The run saying
which speed it is at and that `p` stops it is the one that stays. On a page, space and `p` run,
`.` steps, `+` or `=` winds up and `-` or `_` winds down.

`why f7` answers with a block for the cell: alive or empty, how many living cells are round it and
which, and what the rule will do with it next. `history` lists every move with the generation it
was made at and what it was told.

Nothing is hidden: the one seat, called The watcher, is told everything the game says, and the game
sounds nothing. It draws in one colour, the slot `live` — moss unless the
[video page](../../../README.md#colours) or `--colour live=teal` says otherwise — for the living
cells and for every cell named in the log.

## The files

In the order the project compiles them:

| | |
| --- | --- |
| [Rules/Torus.fs](Rules/Torus.fs) | the board on a `Grid`, its joined edges and every cell's eight neighbours, and `step`, which is the whole of the rule |
| [Rules/World.fs](Rules/World.fs) | where the game stands: the living, the generation after them, the two behind, the count, whether it is running and how fast, and the deal |
| [Rules/Turn.fs](Rules/Turn.fs) | `Move`, what happens, what is refused, and how far a run goes before something stops it |
| [Rules/Words.fs](Rules/Words.fs) | every string a player reads, and the line each move is written as |
| [Reading/Ink.fs](Reading/Ink.fs) | the one colour slot, `#` and `.`, and the cell names painted in the log |
| [Reading/Parse.fs](Reading/Parse.fs) | a typed line as a move or a question |
| [Reading/Render.fs](Reading/Render.fs) | every screen as a `Scene`, the help, and the page's keys |
| [Offer.fs](Offer.fs) | the `Playable`: the deal, the clock, the terminal's keys, and the faults that hold the board's arithmetic before anybody sits down |
| [Program.fs](Program.fs) | the door, when Life is a program of its own |

## Checks

[life.fsx](../../../tests/life.fsx) loads [Living.fsx](../../../tests/Living.fsx) and holds the
game to its board — 416 squares, eight distinct neighbours each, a corner touching the far corner,
every name reading back — and to the rule: a lone cell dies, a block stands, a blinker turns over
and comes back, a glider moves a square diagonally in four generations and ten in forty, edges and
all. Then to its words, each refusal saying why and a refused move still written down; its one
seat and its lack of a machine; the record read back and replayed; the three views drawing the
ninth row cell for cell; the page's buttons being exactly the controls; and the clock — dealt
running at notch 5, a beat a generation that says nothing, twenty beats at a stopped board writing
nothing, winding, every key typing a line the game reads, and a record of beats replaying to the
same board. It ends with `Conforms.against life 1 [ "a1"; "b2"; "step"; "run"; "clear"; "faster" ]`,
the [contract](../../../README.md#tests) every game is held to.
[counting.fsx](../../../tests/counting.fsx) holds `cells` and `generations`, and every refusal and
happening that carries a count, to their nouns at nought, one and two.
