# Noughts and crosses

Nine squares, three in a row, and nothing hidden. The second of the three games here, and the
engine it runs on is [one directory up](../../../README.md).

**It is not a feature.** It exists because about four fifths of this program was extracted on
the claim that it is generic - a history to walk back through, a record that replays, seats
and tokens, a machine to play one of them, three ways of drawing a board, a table over a wire -
and a claim like that cannot be tested by the game it was extracted from. This is a second one
going through the same two seams at a fraction of the size, and [what that turned
up](../../../README.md#what-a-second-game-found) is worth more than the game.

```powershell
dotnet run -- tictactoe play 2
dotnet run -- tictactoe play 2 --rival hard    # a machine that cannot be beaten
dotnet run -- tictactoe serve 2 --rival hard   # nine buttons in a browser

dotnet run -- tictactoe replay logs/...-tictactoe-2p-seed<n>.log   # one you put down
```

A game of nine squares is not one anybody puts down for the evening, but it is taken up the
same way every game here is: `quit` writes the record, `Continue a game` at the menu lists
them, and the machine comes back to the seat it was playing at the strength it was playing
it. All of that is the engine's rather than this game's, and is [documented
there](../../../README.md#taking-it-back-and-writing-it-down) — which is the point of this
game being here at all.

## Playing

Crosses go first. Every command that is not about squares - `undo`, `redo`, `history`, `save`,
`notes`, `commands`, `log`, `view`, `resign`, `restart`, `help`, `quit` - belongs to the engine and is
[documented there](../../../README.md). What this game adds is a number.

| command | action |
| --- | --- |
| `5` | take square 5 |
| `place 5` (`mark`, `p`) | the same, the long way round |

Squares are numbered the way a keypad is: 1 to 9, left to right and top to bottom, so the
number a player types is the square they are looking at.

```
 1 | 2 | 3
---+---+---
 4 | 5 | 6
---+---+---
 7 | 8 | 9
```

A bare number is a move, because on a board where there is only one thing to do, naming the
square *is* saying what to do - and it is what everybody types anyway. The long way round is
kept because it is what a record is written in, and a record that read as a column of bare
digits would be a record nobody could skim.

## Rules as implemented

- Two players, and exactly two. Seat one plays the crosses, because the crosses go first.
- Three in a row wins: along a row, down a column, or corner to corner.
- A full board with no such line is a draw.
- `resign` gives the game up, and writes it down.

Nothing is dealt, nothing is shuffled, and nothing is hidden - both players are looking at the
whole game, which is the only kind of game this is. So there is no `Knowledge` here, no seed
that means anything, and no answer to this game's own question: the board is nine squares in
plain sight, and asking it anything gets a line saying there is nothing to work out.

**The board is worked out rather than written down.** `Squares.Side` is three, and the rows,
the columns, the diagonals and the count of winning lines all follow from it. What could be
wrong with that is arithmetic rather than a typo - but arithmetic goes wrong too, which is why
this game still fills in the seam's `Faults` and says so before anybody sits down.

## The machine

Three ways of playing, and none of them is a strategy. This game is small enough to be solved
outright - nine squares, and every line of play can be walked to its end - so what `easy`,
`medium` and `hard` name is how *far* a machine looks and how often it does not play what it
saw.

| | `easy` | `medium` | `hard` |
| --- | --- | --- | --- |
| moves it looks ahead | 1 | 3 | 9 |
| how often it plays something else anyway | 40% | 15% | never |

Nine from an empty board is the whole game, which at this size is perfect play. The slip is
what makes a beatable opponent out of a solved one: a machine that never slipped could not be
beaten, only drawn with, and losing every game is nobody's idea of an easy one.

Which is worth reading beside [the other game's machine](../Turncoats/README.md#three-sets-of-numbers-not-three-machines),
where a machine has weights on five things and still cannot see the end of a game. Both are
machines; only one of them could ever be perfect, and the reason is the game rather than the
writing.

## The files

Ten files, against [Turncoats'](../Turncoats/README.md#the-files) twenty-one, in the same
shape and a fifth of the size. Worth reading beside that folder, because the two of them
together are the whole argument for the seams being where they are.

| File | Role |
| --- | --- |
| [Marks.fs](Rules/Marks.fs) | `Mark`, and the squares - the runs that win worked out from the side rather than written down |
| [Board.fs](Rules/Board.fs) | What is on the board, and the line somebody holds all of |
| [Session.fs](Rules/Session.fs) | Where the game stands, and which seat plays which mark |
| [Turn.fs](Rules/Turn.fs) | `Move`, and how a turn goes: the square has to exist and be free |
| [Words.fs](Rules/Words.fs) | Every string a player reads |
| [Rival.fs](Rules/Rival.fs) | A seat played by the program: the game walked to its end, with alpha-beta so it answers |
| [Ink.fs](Reading/Ink.fs) | Two colours, against the other game's four |
| [Parse.fs](Reading/Parse.fs) | A number, which on this board is the whole move |
| [Render.fs](Reading/Render.fs) | Every screen described once as a [`Scene`](../../../README.md#a-screen-described-once), which `Readers` then draws three ways |
| [Offer.fs](Offer.fs) | Both seams filled in |

Three files where the other game has five, and the two that are missing are the point.
`Rich.fs` and `Html.fs` were the same board written out a second and a third time; this game
says what a screen is *made of* and the readers in the table layer do the drawing. See
[A screen described once](../../../README.md#a-screen-described-once).

