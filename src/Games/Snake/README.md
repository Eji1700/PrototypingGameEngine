# Snake

The arcade game: a board of 24 by 14 with a wall round it, a snake each for 1 to 4 players, and
one piece of food at a time. The snakes move on their own and quicken as they eat, and you only
steer — or, played the other way, the board waits for you and a direction is a step. The two are
one set of rules at two paces, and the engine they run on is [one directory up](../../../README.md).

```powershell
dotnet run -- snake play 1                      # on the clock: the arrows steer, space holds
dotnet run -- snake play 2                      # two snakes at one keyboard, the arrows and wasd
dotnet run -- snake serve 1                     # the same, in a browser
dotnet run -- snake-turns play 2 --rival hard   # a step at a time, against the machine
```

## The rules

Each snake is dealt 3 segments long, spread evenly down the board and facing in from the left and
right edges by turns, with a piece of food on a square nothing is standing on, drawn from the
deal's seed. A snake moves one square at a time, the way its head faces, and may go any way but
back into its own neck — the square behind its head, wherever that is. Eating adds a segment,
which arrives on the step after: the head goes on and the tail stays put once. The next piece
lands at once, somewhere nothing is standing.

A snake stops when its head meets the wall, another snake, or itself, and what is left of it lies
where it fell, for everybody else to go round. The square a tail is leaving is not in the way, its
own or anybody's, since the tail has gone by the time a head arrives — unless that snake is
growing, when its tail is staying where it is.

At a table of one the game is over when the snake stops, and the score is what it ate. At a table
of more it is over when one snake is left moving, and that one has won, or when none is.

**A step at a time** (`snake-turns`): the snakes go in seat order, a direction is one square that
way, and a turn is one round of whoever is still moving; a snake that has stopped is passed over.
`resign` stops your own snake, and the rest play on.

**On the clock** (`snake`): every snake still moving steps at once, each beat, the way it faces,
and a direction only turns a head — turning it where it already points does nothing. Every head is
judged against the board as it stood when the beat began, so two heads that pick the same square
both stop, a snake following another nose to tail is safe, and the head that lands on the food
eats it. Two turns between one beat and the next are both taken, each checked against where the
neck is and not against the way the head now points. `resign` stops every snake, because a resign
carries no seat and on the clock nobody is to play.

The clock has nine notches, and a fresh board starts at 5. A beat is 420ms less 40ms a notch,
less 8ms for every piece the best-fed snake has eaten, and never under 50ms — so notch 5 opens
at 220ms, notch 9 at 60ms, and a board that has been going a while tightens on its own. Winding
the clock is a move: it is in the record, `undo` takes it back, and `restart` deals at 5 again.
Winding past either end, or to the notch you are at, does nothing and says nothing; `speed 12`
is refused.

Each way refuses the other's moves and says so: a direction on the clock is not a step, and `go`
at a game of turns is not a beat.

## The words

The compass is `north`, `east`, `south` and `west`, each also by its initial or by where it is on
the screen — `n` or `up`, `e` or `right`, `s` or `down`, `w` or `left`. Typed, `w` is west; the
keys, under [The board](#the-board), are another matter.

A step at a time:

| | |
| --- | --- |
| `north` | one square that way |
| `go` (`on`, `ahead`) | straight on, the way you are already facing |
| `why north` (`look north`) | what is one square that way, before you commit to it |

On the clock:

| | |
| --- | --- |
| `north` | turn Snake A that way |
| `b north` | turn Snake B — the snakes are lettered from `a` |
| `go` (`beat`, `tick`) | one beat, said out loud |
| `faster` (`quicker`, `+`), `slower` (`-`) | wind the clock a notch |
| `speed 7` | straight to a notch, from 1 to 9 |
| `why north` (`look north`) | as above |

`resign`, `restart`, `undo` and the rest are the table's, read the same at every game and listed
[there](../../../README.md#at-the-prompt); what `resign` stops is this game's, above. A record
keeps a move as it is typed here — `north`, `go`, `a north`, `faster`, `speed 7` — so a record
of the clock way is beats and steers, and replays beat for beat with no clock involved.

## The board

A table of four dealt from seed 7, drawn plain with the notes off:

```
=== Beat 1 - Snake A (you), 3 segments, ate nothing yet ===

THE BOARD
  +------------------------+
  |........................|
  |.aaA....................|
  |........................|
  |......*.................|
  |....................Bbb.|
  |........................|
  |........................|
  |.ccC....................|
  |........................|
  |........................|
  |....................Ddd.|
  |........................|
  |........................|
  |........................|
  +------------------------+

THE SNAKES
    Snake A (you)  3 segments  ate nothing yet  facing east
    Snake B        3 segments  ate nothing yet  facing west
    Snake C        3 segments  ate nothing yet  facing east
    Snake D        3 segments  ate nothing yet  facing west
  clock at speed 5 of 9 - 'faster' and 'slower', or + and -
```

A snake is its own letter, small along the body and capital at the head, so a board with no colour
in it still says which way everything points; the food is `*`, and a snake that has stopped is
`#` in the quiet colour, because what it is now is an obstacle. The heading is your snake's — at a
game of turns, whoever is to play — and once the game is over it is the score. `The snakes` has a
row each, with `->` at the one to play. `Which way` is the lines you could type next: your own
snake's four turns, `slower`, `faster` and `restart` on the clock; the four directions, `go` and
`restart` at a game of turns. On a page each is a button, drawn for the seat looking at it. Two
notes explain the board and the pace, and go with `notes`. `why north` answers with what is one
square that way — open board, the wall, the food, its own body, Snake B, or what is left of Snake
B — how many steps the food is from there, and a warning when that way is its neck.

The four snakes and the food are the colour slots, `a` to `d` and `food`: moss, crimson, azure,
violet and gold unless [set otherwise](../../../README.md#colours), and a snake's name is painted
in its colour wherever it is written. `plain`, `rich` and `html` are drawn from the same
[scenes](../../../README.md#screens) and show the same things. Nothing is hidden — every seat is
told the same — and nothing rings.

On the clock the keys are hands rather than seats, at a terminal and on a page alike:

| | |
| --- | --- |
| the arrows | turn Snake A |
| `w` `a` `s` `d` | turn Snake B |
| `i` `j` `k` `l` | turn Snake C |
| `8` `4` `5` `6` on the number pad | turn Snake D — on a page, any key that types those |
| `+`, `-` | `faster`, `slower`; a page takes `=` and `_` as well |

Each sends a line the game reads, and the arrows send `a north` wherever they are pressed, so a
key can never turn somebody else's snake by mistake; at a table with the players at different
machines a page's arrows are still Snake A's, and Snake B's player steers with `wasd` or the
buttons. The rest of the keys are [the table's](../../../README.md#a-game-on-a-clock): at a
terminal space holds the clock and brings back the notes and the box of commands, which go while
the board is moving; `r` deals another board once the clock is held or the game is over, and not
before; Enter opens the prompt and Esc puts the game down. A game of turns has no keys and no
clock, and is played at the prompt.

## The machine

Three skills, at `snake-turns` only. Each looks at every way its snake can go that is not back into
its neck and does not stop it, and rates a step by four things compared in order: whether the room
it leaves is at least the snake's own length, whether it eats, how much nearer the food it lands,
and the room itself. The room is a flood fill out from the square the head would land on, over
everything no body is standing on, and only `hard` counts it; the other two take the whole board
for the room, so the first and last never tell them anything. A slip is a step taken at random
from the ones that do not stop it, rather than the best it can see; if every way stops it, it
takes one anyway.

| | `easy` | `medium` | `hard` |
| --- | --- | --- | --- |
| counts the room a step leaves | no | no | yes |
| slips | 35 in 100 | 5 in 100 | never |

There is no machine on the clock: the engine plays a machine's turns between one person's move
and the next, and on the clock there is no such turn. How a machine is seated and held is
[the engine's](../../../README.md#against-the-machine).

## Settings

`snake` and `snake-turns` are one game offered two ways, each a `Playable` of its own with the
same rules behind it. Which one a new game is dealt as is settled on the game's
[settings page](../../../README.md#settings) — `plays snake-turns` under `[snake]` in
`settings.txt` — or by naming the way on the command line, as `dotnet run -- snake-turns play 2`
does; a record replays the way it was played. The video page sets the five colours above.

## The files

| | |
| --- | --- |
| [Rules/Board.fs](Rules/Board.fs) | the 24 by 14 grid, the four directions, and a step along one — which may leave the board, which is what a wall is |
| [Rules/Snakes.fs](Rules/Snakes.fs) | one snake: its body head first, the way it faces, the growth it is owed, what it has eaten, and how it stopped |
| [Rules/Session.fs](Rules/Session.fs) | the table: the snakes by seat, the food and the generator it comes from, whose turn, the pace and the notch; the deal and the endings |
| [Rules/Turn.fs](Rules/Turn.fs) | the moves and what they do — a step, a steer, a winding, a resign — and the beat that moves everybody at once |
| [Rules/Words.fs](Rules/Words.fs) | every word a player reads, and how a move is written in the record |
| [Rules/Rival.fs](Rules/Rival.fs) | the three skills: what a step is worth, and how much room it leaves |
| [Reading/Ink.fs](Reading/Ink.fs) | the glyphs, the five colour slots, and the marking that paints a snake's name |
| [Reading/Parse.fs](Reading/Parse.fs) | two readers, one a way: a direction as a step, or as a turn of a lettered snake |
| [Reading/Render.fs](Reading/Render.fs) | every screen as a scene — the board, the record, the answer to `why`, the rules, the waiting room — and the page's keys |
| [Offer.fs](Offer.fs) | the seam filled in twice, one `Playable` a way: the deal and its faults, the clock, the keys, and the machines at one pace |
| [Program.fs](Program.fs) | the door: this game as a program of its own |

## Checks

[snake.fsx](../../../tests/snake.fsx) loads [Slither.fsx](../../../tests/Slither.fsx), the harness
that compiles the files above, and holds the game to: the deal at every size and its faults; a
step, the tail following, and no turning back; eating, the segment arriving a step later, the next
piece drawn where the same deal draws it, and `undo` taking the draw back; the three stops and the
tail rule, growing or not; four seats in order, a resign passed over, and each ending in its words;
on the clock, a beat and a steer, two quick turns, two heads on one square, following a tail,
eating on the beat, the notches and the floor, and winding as a move; every key a line the game
reads, and the page's keys the same list; a record written in beats and steers that replays with
no clock; the three views and the page, block by block; and the machines played out, `hard`
eating more than three times what `easy` does and the same seed playing the same game. It ends
with [Conforms.against](../../../README.md#tests) for both ways — `beat`, `faster`, `slower` and
`b north` on the clock, `go`, `north` and `go` a step at a time.

[counting.fsx](../../../tests/counting.fsx) holds the counts of segments, steps and pieces eaten to
their nouns at nought and one, and [records.ps1](../../../tools/records.ps1) takes the Snake
records in `logs/` back up on every CI run.
