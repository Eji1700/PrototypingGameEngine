# Noughts and crosses

Nine squares, three in a row, and nothing hidden. Two play, crosses first, and
`dotnet run -- tictactoe play 2` deals one at this keyboard; `--rival hard` after it seats a
machine that cannot be beaten. The engine it runs on is described [at the root](../../../README.md).

## The rules

- Two seats, and exactly two: seat 1 plays the crosses, `X`, and goes first; seat 2 plays the
  noughts, `O`. A deal for any other number is refused at the door.
- A turn is one mark on a free square, named by its number - 1 to 9, left to right and top to
  bottom. A square that is not there, or already has a mark in it, is refused and the turn does
  not pass.
- Three of a mark in a row wins - along a row, down a column, or corner to corner - on the mark
  that makes the line. There are eight lines.
- A full board with no such line is a draw.
- `resign` ends the game on whoever is to play, and the record says that mark walked away.

## The words

One move, in four spellings, and [Parse.fs](Reading/Parse.fs) reads nothing else:

| typed | move |
| --- | --- |
| `5` | take square 5 |
| `place 5`, `mark 5`, `p 5` | the same, the long way round |

A word that is not a number is refused where it was typed - `'seven' is not a square` - and never
reaches the rules. Whichever way a move was typed, the record writes it as `place 5`. `resign`,
`undo`, `history`, `view`, `quit` and the rest are [the table's](../../../README.md#at-the-prompt).

## The board

Every screen is described once as a [`Scene`](../../../README.md#screens) in
[Render.fs](Reading/Render.fs) and drawn as `plain`, `rich` and `html` by the engine's readers, so
the three show the same things: a heading with whose turn it is, or how the game ended; the board,
a taken square showing its mark and a free one its number - at a terminal the thing to type, in a
browser a button that types it; and the players, `->` at whoever is to play, `(you)` after the
seat that is reading, and the squares each holds, `2 of 9`.

A note under each block explains it, and `notes` puts both away; the commands box and the log are
the [table's margins](../../../README.md#how-the-board-is-drawn). Crosses are drawn in crimson and
noughts in azure until the [video page](../../../README.md#colours) says otherwise, under the slots
`x` and `o`. Nothing is hidden, so both seats are told the same; nothing is on a clock, so there
are no keys; and the board makes no sound of its own.

## The machine

Three skills, from [Rival.fs](Rules/Rival.fs), seated [as any machine is](../../../README.md#against-the-machine).
One search - the game walked ahead from each free square, with alpha-beta - and two numbers each:
how deep it looks, and how often the machine plays any free square instead of what it found. Among
moves it rates alike it picks one at random.

| | looks ahead | plays any free square instead | what `--help` says |
| --- | --- | --- | --- |
| `easy` | 1 move | 40 times in 100 | takes a win it can see, and often plays somewhere else anyway |
| `medium` | 3 moves | 15 times in 100 | takes a win and blocks yours, and looks no further |
| `hard` | 9 moves | never | plays the game out to the end before moving, so it cannot be beaten |

One move ahead sees a win of the machine's own; three see the win you would take next, and a
square of its own that threatens two lines at once, but not one of yours; nine is the whole game
from wherever it stands. A full search that never slips cannot lose a game that is a draw when
neither side errs, so `hard` can be drawn with, and no more.

## The files

| in the order [TicTacToe.fsproj](TicTacToe.fsproj) compiles them | |
| --- | --- |
| [Rules/Marks.fs](Rules/Marks.fs) | `Mark`, the nine squares, and the eight lines worked out from the side of the board |
| [Rules/Board.fs](Rules/Board.fs) | What is on the board, which squares are free, and the line somebody holds all of |
| [Rules/Session.fs](Rules/Session.fs) | Where the game stands, how it ended, and which seat plays which mark |
| [Rules/Turn.fs](Rules/Turn.fs) | `Move`, `Notice`, and how a turn goes: the square has to exist and be free |
| [Rules/Words.fs](Rules/Words.fs) | Every string a player reads, and the line a move is written down as |
| [Rules/Rival.fs](Rules/Rival.fs) | The three skills, and the search they share |
| [Reading/Ink.fs](Reading/Ink.fs) | The two colour slots, and which words are painted in them |
| [Reading/Parse.fs](Reading/Parse.fs) | The four spellings of a move |
| [Reading/Render.fs](Reading/Render.fs) | The board, the record, the rules and the waiting room as scenes, and the page's title and prompt |
| [Offer.fs](Offer.fs) | Both halves as one `Playable`, and the faults the eight lines are checked for before anybody sits down |
| [Program.fs](Program.fs) | The game as a program of its own |

## Checks

[tictactoe.fsx](../../../tests/tictactoe.fsx) is the one suite that loads it, through the harness
[Noughts.fsx](../../../tests/Noughts.fsx). It holds the eight lines and every square on one of them,
crosses first, both refusals leaving the game where it was, a win on each line, the draw, resigning,
undo and redo, a record that reads back and replays to the same game, a number and `place` read
alike, the three views saying the same things, a page with a button for each free square and none
for a taken one, `hard` never losing to `easy` in 24 games and winning more than 6 of them, `hard`
against itself only ever drawing, and the same seed playing the same game twice; then
`Conforms.against noughts 2` holds it to [the contract](../../../README.md#tests). Its one
[record](../../../README.md#records) in `logs/` is taken back up by CI with every other.
