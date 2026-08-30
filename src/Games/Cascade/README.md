# Cascade

A board of sixteen by sixteen, and every cell on it an elbow: two arms at a right angle,
pointing up and right, right and down, down and left, or left and up, dealt at random from the
seed. Touch one and it turns a quarter to the right; whatever it then reaches that is reaching
back turns too, and so on, in waves on a clock, until a wave sets nothing off. You have twelve
touches, and the score is what the cascades turned over. One seat, called The hand.

```powershell
dotnet run -- cascade play 1     # at this keyboard
dotnet run -- cascade serve 1    # the same board, in a browser
```

## The rules

Cells are named by column and row, `a1` at the top left to `p16` at the bottom right. The hand
starts on `h8`, in the middle.

A **touch** names a cell, or presses the one the hand is on, and sets it turning. It is refused
while anything on the board is still turning, and once the twelve touches are spent.

A **beat** of the clock lands every cell that is turning, all together: a quarter turn to the
right, so up-and-right becomes right-and-down. Only then is the board read. Each cell that landed
reaches out along the two arms it now has, and every neighbour with an arm pointing back begins
turning; an arm pointing off the edge reaches nothing. Two of the four facings have an arm any
given way, so each arm is a coin. Because a wave lands at once and is read at once, two cells
that set each other off turn together rather than one after the other. A cell that has already
turned this cascade is not spared, so a cascade may come back over its own ground, which is why
the count is of turns rather than of cells.

The cascade is at **rest** when a wave sets nothing off, and the next touch is owed. One that
will not stop is stopped: a cascade is held to 4096 turns over 200 waves, and a wave that reaches
either lands and sets nothing more off. The board is over on the beat the twelfth cascade comes
to rest; `resign` puts it down before that, with the touches left unspent.

Four numbers are kept, for the cascade just run and for every cascade so far:

| | |
| --- | --- |
| **Turns** | every cell that landed, counted each time it did |
| **Rows, columns** | a row or column all sixteen of whose cells turned during this one cascade; the two are counted together |
| **Squares** | a two by two all four of whose cells did; squares overlap, and each is worth its own |
| **Waves** | how many beats the cascade took, which is not worth anything and has no total |

A shape comes up once a cascade, and the totals are added when the cascade comes to rest.

The clock beats once a quarter turn: 500ms at notch 5, where a board is dealt, 900ms at notch 1,
100ms at notch 9. The board does the same thing at every notch. Winding it is a move, in the
record like any other, and so is a beat, so a record replays wave for wave and `undo` walks back
one at a time ([a game on a clock](../../../README.md#a-game-on-a-clock)). A board at rest goes
on being beaten for a few beats after a cascade, while the shapes it lit and the strike it took
are still showing; once nothing is moving or showing, a beat takes nothing and leaves no line.

The turn number is the touch: the beats of a cascade carry the number of the touch that set it
off, and the next number comes up once the board is at rest.

## The words

| | |
| --- | --- |
| `f7`, `touch f7`, `t f7` | set that cell turning, wherever the hand is; written down as `f7` |
| `press`, `touch` | set the cell the hand is on turning |
| `up`, `down`, `left`, `right`, or `w`, `s`, `a`, `d` | move the hand one cell that way. A push off the edge moves nothing and is not written down; a move of the hand is written down but says nothing |
| `why f7`, `ask f7` | what that cell would reach when it lands, and whether anything is reaching back: a question, not a move |
| `faster`, `quicker`, `+`; `slower`, `-` | wind the clock a notch; already at the end, nothing happens |
| `speed 7` | straight to that notch, from 1 to 9 |
| `beat`, `tick` | one beat by hand, which is the line the clock types for you |
| `resign` | put the board down with the touches left unspent |

Everything else at the prompt — `undo`, `restart`, `mute`, `log`, `save`, `quit` and the rest —
is the table's, and the same at every game ([at the prompt](../../../README.md#at-the-prompt)).
A line that is none of these is answered with "Say a cell to set it turning - 'f7'. 'why f7'
says what it would reach. 'help' has the rest." A cell off the board, `a17`, reads as a move and
is refused by the rules, so the refusal is in the record: "There is no cell a17. The columns run
a to p and the rows 1 to 16."

What the game says, in the log and the record:

```
h8 begins turning.
the square at h7 has turned over, 6 turns in.
column a has turned over, 16 turns in.
The cascade from h8 came to rest after 269 turns over 40 waves, bringing up the square at h7, ...
It was stopped there: a cascade is held to 4096 turns over 200 waves.
A quarter turn now takes 400ms. Notch 6.
A cell is still turning. Nothing may be touched until the board comes to rest.
No touches left. A board is worth 12 - 'restart' deals another.
Put down with 3 touches unspent.
269 turns in all, over 12 touches: 1 whole row or column, and 67 squares.
```

## The board

The heading says where things stand — `12 touches left - nothing has been touched yet`,
`11 touches left - 2 cells turning`, `11 touches left - the cascade from h8 ran to 269 turns` —
and the board is a field, one character a cell, the column letters across the top and the row
numbers down the side. A cell's glyph says which way it faces and how worn it is: a step heavier
for every 5 turns it has made, and the steps differ in the weight of the line rather than only
in colour, so `plain` shows the ground a cascade keeps coming back over as well as the other two.

| turns made | up and right, right and down, down and left, left and up | slot |
| --- | --- | --- |
| 0 to 4 | `└┌┐┘` | `elbow`, slate |
| 5 to 9 | `╰╭╮╯` | `worn`, teal |
| 10 to 14 | `┗┏┓┛` | `hot`, sky |
| 15 and up | `╚╔╗╝` | `bright`, bone |

Two more slots: `turning`, gold, for a cell in the middle of a turn, the one that has just
landed, and the cells named in the log; and `lit`, crimson, for the light that runs along a
shape that has come up. All six can be recoloured ([colours](../../../README.md#colours)).

The hand is marked on the edges rather than in the grid, where a cursor would cost a cell its
glyph: `>` beside its row, and its column's letter in capitals in the legend. A page outlines
the cell instead. Here the hand is on h8, and h7 and i8 are half way through a turn:

```
      abcdefgHijklmnop
    6 ┌┌└┘┘┐┐└┐┐┘┘┘└┐┌
    7 ┘└┘└┐┘└<┐┘┐└┘└┌┐
  > 8 ┐┌┌┌└┐┌└<┐┐┐┌┘└┌
    9 ┌┐┘└┌┌┌┐┐┐┌└┐┐└┌
```

A terminal is drawn six times a beat while anything is showing, and draws a turning cell as one
of three pictures: the elbow as it was, its corner half way round as an arrow — `^ > v <`, the
way the corner is pointing, there being no box-drawing elbow at forty-five degrees — and the
elbow it is landing as. A page is sent the board once a beat and does the turning itself,
rotating the glyph a quarter over the notch's own milliseconds, so winding the clock winds the
animation; a browser that asked for reduced motion is shown the board still.

A cell that has just landed flashes in the turning colour. A row, column or square that has come
up has a light run along it over the three beats it stays lit, in `rich` a crimson head three
cells wide, on a page cells blinking along it. `plain` has no colour and sees neither. What it
does see is the strike: whatever a terminal would ring its bell for also strikes the board, a
band four rows deep running down the whole of it over three beats, marked `*` on the row labels.

The count sits beside the board in `rich` and on a page, and under it in `plain`: the four
numbers in two columns, `this` cascade and `all`, then the touches spent of 12 and the notch.
Under the board and the count are the two notes, and with the box of commands comes `What next`,
lines to type — `press`, `f7`, `why f7`, `faster`, `slower`, `mute`, `log`, `undo`, `restart` —
which on a page are buttons. While the board is moving at a terminal the notes and the commands
go and the log is cut to its last three lines; hold the clock and whatever you had showing comes
back. Nothing on the board is hidden: one seat, and every notice reads the same to everybody.

### The keys

| | at a terminal | on a page |
| --- | --- | --- |
| arrows, or `w` `a` `s` `d` | move the hand | the same |
| space | press the cell the hand is on | the same |
| `+`, `-` | faster, slower | the same, and `=`, `_` |
| `h` | hold the clock, and go on again — space presses here, so it cannot hold | |
| Enter, Esc, `r` | type a line; put the board down; deal another while held or over | |

Every key stands for a line the game reads, so a board played by key writes the same record as
one played by hand.

### The sounds

| | | at a terminal |
| --- | --- | --- |
| tap | a wave landed | silent |
| chime | a square came up | silent |
| fanfare | a whole row or column came up | rings |
| ready | the cascade came to rest, and the board is yours again | rings |
| knell | the touches are spent, or a cascade was stopped short | rings |

A beat says at most two things, what the wave did and what the board now is — a wave that
completed a column and left the board at rest is a fanfare and a ready — and no other move makes
a sound. A page makes all five. A terminal has one bell and keeps it for the three that come
rarely, and those three are the ones that strike the board, so a reader who cannot hear is told
the same thing; `Faults` holds the two lists against each other. `mute` and `sound` are the
table's.

## The files

| | |
| --- | --- |
| [Rules/Board.fs](Rules/Board.fs) | the four ways and four facings, the quarter turn, the sixteen by sixteen grid, and the shapes watched for: 16 rows, 16 columns and 225 squares |
| [Rules/Session.fs](Rules/Session.fs) | the state — every cell's facing and wear, what is turning, the hand, the run, the tally, what is lit, sounding and struck — and the numbers: 12 touches, 4096 turns over 200 waves, the notch in milliseconds, 5 turns a step of wear |
| [Rules/Turn.fs](Rules/Turn.fs) | the moves, the happenings and the refusals, and what each move does: a touch, a beat landing a wave and reading the board, the hand, the clock, resigning |
| [Rules/Words.fs](Rules/Words.fs) | every count and notice in words, and a move written as the line it is typed as |
| [Reading/Ink.fs](Reading/Ink.fs) | the glyphs at each step of wear, the arrows for a turning cell, the six colour slots, and the marking of cell names in the log |
| [Reading/Parse.fs](Reading/Parse.fs) | a typed line as a move or a question |
| [Reading/Render.fs](Reading/Render.fs) | the board, the count, the answer to `why`, the record, the rules, and the page's stylesheet and keys |
| [Offer.fs](Offer.fs) | the `Playable`: a deal for one, the pulse, the sounds, the faults that hold the parts to each other, and the views |
| [Program.fs](Program.fs) | this game as a program of its own |

## Checks

[cascade.fsx](../../../tests/cascade.fsx) loads [Cascading.fsx](../../../tests/Cascading.fsx) —
the engine, the table and the wire, then these files in the order above — and
[Conforms.fsx](../../../tests/Conforms.fsx). It holds the board to its faults and its names; the
rule to a board laid out by hand, where every cell faces the same way and a touch marches
straight down its column; what may be touched and when; the clock, its notches and the ceiling;
the record, undo and replay through the engine; the sounds, and that only a beat makes one; the
three views, the page, its buttons and its keys; the hand; every move written and read back and
every notice worded; the frames a terminal draws and the phase that picks a picture; the log and
the sound switches at a table; the visual bell; that a moving board fits a terminal; the light
reaching the far end of a lit shape; and then `Conforms.against cascade 1 [ "a1"; "beat";
"faster"; "slower"; "press" ]`, the contract every game is held to
([tests](../../../README.md#tests)).

[counting.fsx](../../../tests/counting.fsx) holds its six counts, and every notice and refusal
that carries one, to their nouns at nought and one. `logs/` holds one Cascade record, taken back up on every CI run, and CI builds
and plays the container image for it. It is not in `smoke.ps1` or `wire.ps1`: one seat, and a
board that is glyphs rather than buttons.
