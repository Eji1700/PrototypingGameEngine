# Life

Conway's, on a board with its edges joined: a soup, a rule, and nobody to play against. The
fifth of the games here, and the engine it runs on is [one directory
up](../../../README.md).

**It is not a feature either.** [Noughts and crosses](../TicTacToe/README.md) is here because
a claim that four fifths of this program is generic in the game cannot be tested by the game
it was extracted from. This one is here because four games of two or more people taking turns
against each other do not test that claim very hard either. Life has:

- one seat, where every other game has two to seven;
- no opponent, and no machine to be one;
- nothing to win, and no ending at all;
- a position that changes because a rule says so rather than because anybody chose it.

It fills in [the same two records](../../../README.md#two-seams) as the rest, unchanged, and
gets the timeline, the record on disk, the replay, the shared verbs, the seat, the menu, the
command line, the wire and all three screens without a line of any of them being touched.

```powershell
dotnet run -- life play 1                 # a soup off the clock
dotnet run -- life play 1 --seed 42       # that soup, and the same one every time
dotnet run -- life serve 1                # the same board in a browser

dotnet run -- life replay logs/...-life-1p-seed<n>.log   # one you put down
```

## Playing

Every command that is not about cells or generations - `undo`, `redo`, `history`, `save`,
`notes`, `commands`, `view`, `restart`, `help`, `quit` - belongs to the engine and is
[documented there](../../../README.md). What this game adds is a cell and a count.

| command | action |
| --- | --- |
| `f7` | turn cell f7 on, or off again |
| `toggle f7` (`t`) | the same, the long way round |
| `step` (`s`, `run`) | let the rule run one generation |
| `step 10` (`run 10`, `10`) | ten of them, stopping early if there is nothing left to happen |
| `clear` | sweep the board, to draw on it from nothing |
| `why f7` (`ask`) | how many living neighbours that cell has, and what the rule will do with it |

Cells are named the way a person reads a square off a board they are looking at: a letter for
the column and a number for the row, so `f7` is six across and seven down.

```
    abcdefghijklmnopqrstuvwxyz
 1  ..........................
 2  ..........................
 ...
 7  .....#.#..................
 8  ......##..................
 9  ......#...................
```

A bare cell is a move and a bare number is a run, and the two can never be mistaken for each
other: a cell begins with a letter and a run does not. Which is worth the shortcut - `f7 f8 f9`
typed one after another is how a glider gets drawn - and the long way round is kept because it
is what a record is written in.

`resign` is refused, and that is deliberate: there is nobody to resign to. A game that answered
it with an ending would be a game inventing an opponent for the sake of a verb.

## Rules as implemented

- A board of 26 by 16, with the edges joined - the column after the last is the first again,
  and the row below the last is the top.
- A living cell with two or three neighbours lives on; an empty one with exactly three comes
  alive; everything else is empty next generation. Corners count as neighbours.
- The deal is a soup: every square asked of the generator once, filling about three in ten. A
  seed is therefore a pattern, and the same seed is the same pattern for ever.
- The deal is generation nought, and the generation is what the record calls the turn.
- A run of at most 100 generations at a time, which is a cap on waiting rather than a rule of
  the game.

**The edges are joined**, and that is a decision about the game rather than about the drawing.
A glider on a board with edges runs off it and there is nothing left in fifty generations; on
a board with none it goes round for ever, and a board small enough to read on a screen is only
worth watching if it does.

**It never ends.** What it does instead is arrive somewhere the rule has nothing more to do,
and there are two of those:

| | what the screen says | what `step` does |
| --- | --- | --- |
| a still life - a block, a beehive | *settled: 4 cells that will not change again* | refuses, and says why |
| nothing left alive | *nothing is left alive* | refuses, and says to draw something |

Neither of them takes the board away, which is the whole reason `Over` is `false` at this game
and always will be. `Over` is what stops the engine taking moves; a board the rule has run out
on is still a board to draw a glider on and let go.

**A still board and a beating one look identical in one frame**, and the difference is the only
thing anybody watching wants to know. So the world carries the two generations behind it, and
the heading says which of the three is happening: still going, settled, or beating between two
shapes.

**The board is worked out rather than written down.** `Grid.Width` and `Grid.Height` are the
only two numbers, and the cells, their names, the letters they are named by and every cell's
eight neighbours all follow. What could be wrong with that is arithmetic rather than a typo -
but arithmetic goes wrong too, which is why this game fills in the seam's `Faults` and says so
before anybody sits down: a cell that is its own neighbour, a neighbour off the board, a cell
whose name does not read back as the cell it was drawn on.

## No machine

`Skills` is empty, and the empty list is the honest answer rather than a gap.

A machine here would sit in the one seat and type `step` for ever - and it would: the engine
plays the machines between one prompt and the next for as long as the seat to act is theirs,
and a glider on a board with joined edges neither dies nor settles, so that run would never
come back. The rule already plays this game. What the person at the keyboard does is decide
when to let it, which is not a thing to hand to the program.

## The board is drawn as rows, not as cells

Every other board here is a [`Walled`](../../../README.md#a-screen-described-once) grid: a cell
with room in it, a wall round it and a button on it. This one is `Aligned` rows of spans, and
that is a decision about the game rather than a shortcut. At four hundred and sixteen cells,
every reader would draw something unreadable - a table four hundred columns of walls wide, or a
page of four hundred boxes. What a cell of this board *is*, is one character in a shape made of
its neighbours, and the shape is the whole point.

So the controls are the things a player does over and over - `step`, `step 10`, `step 50`,
`undo`, `clear`, `restart` - and each carries the line it would type, which is how the same
description gives a browser six buttons and a terminal six words to type.

## The files

Eight files, in the same shape as every other game here.

| File | Role |
| --- | --- |
| [Grid.fs](Rules/Grid.fs) | The board, its names, its joined edges - and `step`, which is the whole of Conway's rule |
| [World.fs](Rules/World.fs) | Where the game stands: the living, the two generations behind them, and the soup they were dealt from |
| [Turn.fs](Rules/Turn.fs) | `Move`, and how far a run goes before something stops it |
| [Words.fs](Rules/Words.fs) | Every string a player reads |
| [Ink.fs](Reading/Ink.fs) | One colour, which is the fewest a game can have and still have any |
| [Parse.fs](Reading/Parse.fs) | A cell, a count, and a question |
| [Render.fs](Reading/Render.fs) | Every screen described once as a [`Scene`](../../../README.md#a-screen-described-once), which `Readers` then draws three ways |
| [Offer.fs](Offer.fs) | Both seams filled in |

## What it turned up

Writing a game the two seams were not shaped around found three things in code that had been
right for four games:

- **"1 players"** in the list of games and on the screen that asks how many are sitting down.
  Both were written as a number and a word; neither had ever been handed a one.
- **`'vs <skill>...' for ,`** at the front door of a game with no machines - a clause written
  whatever the game said about having any.
- **A block that loses its name.** A block inside a `Beside`, holding nothing but short lines,
  comes out of the reader that builds panels narrower than its own header, and Spectre drops a
  header that will not fit rather than saying so. Worked around here by giving the block a line
  worth reading; the reader itself still has the hole in it, and it is the same hole the `Tile`
  comment in [Readers.fs](../../Table/Parts/Readers.fs) describes.
