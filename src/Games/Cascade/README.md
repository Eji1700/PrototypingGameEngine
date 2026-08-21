# Cascade

A board of sixteen by sixteen, and every cell on it an **elbow**: two arms at a right angle,
pointing up and right, right and down, down and left, or left and up. They are dealt at random
and nothing whatever is checked about them until you touch one.

```powershell
dotnet run -- cascade play 1     # here at this keyboard
dotnet run -- cascade serve 1    # the same board, in a browser
```

## The rule

Touch a cell and it begins turning. Half a second later it lands, a quarter turn to the right —
up-and-right becomes right-and-down. Then it reaches out along **the two arms it now has**, and
whatever it finds has to be reaching back:

```
        ┌───►   east:  is the cell to the right reaching west - ┐ or ┘ ?
        │
        ▼       south: is the cell below reaching north - └ or ┘ ?
```

Two of the four facings have an arm pointing any given way, so each of those two questions is
a coin. Every cell that answers yes begins turning, and half a second later the same thing
happens again from each of them.

That is the whole game. Two arms, a coin apiece, is a branching factor of very nearly one —
which is what a cascade here is: a chain that mostly dies in a handful of turns and now and then
does not. Over a hundred and twenty touches on random boards the median came to **ten turns**,
the mean to ninety-seven, one in nine passed two hundred, and the largest was **1,694 turns over
ninety-five waves** — three quarters of a minute of board, off a single cell.

Three things follow from the rule and are worth saying out loud:

- **A wave lands all at once.** Everything turning lands on the same beat, and only then is the
  board read for what comes next. So two cells that set each other off turn *together* rather
  than one after the other, and a cascade is a sequence of waves rather than a walk over the
  board in some order.
- **A cell that has already turned is not spared.** Only a cell *in the middle of* a turn is
  passed over, and by the time the board is read there are none. So a cascade may come back over
  its own ground — and the good ones do. It is why the count is of turns rather than of cells.
- **Nothing may be touched while anything is turning.** You get one decision, and then you
  watch. Space holds the clock if you want to read the board while it is going.

Because a cascade can in principle loop for ever, one is held to **4,096 turns over 200 waves**.
Nothing ordinary comes near it. It is there so that "nothing may be touched while anything is
turning" cannot quietly become "nothing may be touched".

## What is counted

You are given **twelve touches**, and then the board is over. Four numbers are kept, for the
cascade you are watching and for all of them together:

| | |
| --- | --- |
| **Turns** | every time any cell entered the turning state |
| **Rows and columns** | a row or column all sixteen of whose cells turned during this one cascade |
| **Squares** | a two-by-two all four of whose cells did. They overlap, and each is worth its own |
| **Waves** | how long the cascade took, which is not worth anything |

A shape counts once a cascade. Rows and columns are the rare thing by a long way: over the same
hundred and twenty touches, sixty-four took at least one square and only **six** took a whole
row or column. A cascade big enough to take a line has taken a great many squares first.

## How it is drawn

A cell is drawn **a step heavier for every five turns it has made**, and the four steps differ in
the weight of the line rather than only in colour:

```
└┌┐┘     as dealt
╰╭╮╯     five turns
┗┏┓┛     ten
╚╔╗╝     fifteen and up
```

That is deliberate. `plain` has no colour at all, and a player reading a board there still has to
be able to see the ground a cascade keeps coming back over. In `rich` and in a browser the four
steps take colours as well, and every one of them is a slot a player can recolour.

A cell in the middle of a turn is drawn three ways across the beat — where it was, its corner
half way round, and where it is going:

```
└   ►   >   ►   ┌
```

There is no box-drawing character for an elbow at forty-five degrees, so what stands for one is
the way its corner is pointing. **A browser does not use those pictures**: it is sent the board
once a beat and rotates the glyph itself, out of this game's own stylesheet, over exactly the
half second the rules are written in. Wind the clock and the animation winds with it.

A cell that lands flashes, and a row, column or square that comes up is lit by a light that runs
along it — a browser inverts them outright, a terminal brightens them.

The count and what to type next are **beside** the board rather than under it, and while the
clock is running the log is cut to its last three lines. Both for the same reason: a board
sixteen deep with three boxes stacked under it is taller than a terminal, and a clock redrawing a
screen taller than its window walks the board off the top of it. Hold it with space and the whole
log comes back, along with the notes and the box of commands — a held board is one somebody is
*reading* rather than watching.

Five sounds, and they are chosen by how *often* the thing happens as much as by what it is worth:

| | | at a terminal |
| --- | --- | --- |
| **tap** | a wave landed | silent |
| **chime** | a square came up | silent |
| **fanfare** | a whole row or column came up | rings |
| **ready** | the cascade came to rest — the board is yours again | rings |
| **knell** | the touches are spent, or a cascade was stopped short | rings |

A browser makes all five out of oscillators and nothing it had to fetch. A terminal has **one
bell** — that is its entire vocabulary, measured rather than assumed — so it keeps it for the
three that come rarely enough to be worth interrupting somebody with. Taps come twice a second
and squares several times a cascade; ringing for those is what makes a game sound like a smoke
alarm. `mute` silences the lot, for that console alone, and `sound` turns them back on.

A wave can say two things at once and they are different kinds of thing: a wave that completed a
whole column *and* left the board at rest says **fanfare** and **ready**, and a browser plays
them a moment apart. What it never says is three noises on top of one another for what was one
moment.

### The bell you can see

Whatever a terminal would ring its one bell for, the board is also **struck**: a band of light
four rows deep runs down the whole of it, crossing in about three beats. That is what `plain`
has instead of hearing anything, so the band is marked on the **row labels** as well as
colouring the cells — a `*` running down the edge where there is no colour at all.

```
   1 ┘└┘└┘┐┌└┘┐┌┐┘┘┌┘
 * 2 ┌┐└└┘┌┐└┌┘└└┐┐┘┌
 * 3 └┌┘┘┘┌┘┌┐┐└┘└┘┌┌
 * 4 ┌┐┌┐┘┌└┌┌└┐┘┘└┌┘
   5 ┐┘┐┌└└└┘└┐┌┘└┘┌┐
```

Which occasions strike the board is decided in the rules and which sounds ring the bell is
decided at the table, and neither can see the other — so the two lists are held up against each
other in `Faults`, and a board where they disagreed would say so before anybody sat down.

A board that has come to rest is still *showing* something, which is not the same as having
something left to do, so the clock goes on beating until the lit shapes and the strike have
finished. Those beats are three or four more lines in the record and they are honest ones: the
board really is still doing something. Once there is nothing moving and nothing showing, a beat
takes nothing, says nothing, and leaves no line behind.

## The hand

The board is steered with the arrow keys or `wasd`, and the space bar presses whatever the hand
is resting on. Naming a cell outright still works and is often quicker across a wide board, and
both go through the same move: a key here stands for a line the game already reads, so nothing
can be pressed that could not have been typed.

The hand is marked on the **edges** of the board rather than in it â the row down the side, the
column in capitals across the top:

```
     abcdeFghijklmnop
   1 ┘└┘└┘┐┌└┘┐┌┐┘┘┌┘
 ...
 > 6 └└└┌┌┘┐┐┌└┌┘┌┘┘┐
```

Every cell is one character wide and every one of them already says something â which way it
faces and how worn it is â so a cursor drawn *in* the grid would be a cell that had stopped
saying it. Marking the edges costs nothing and is legible in `plain`, which has no colour to
fall back on. A browser, which can ring a cell without taking anything away from it, is told
where the hand is instead and draws an outline.

Because the space bar presses, it is `h` that holds the clock here rather than space.

Moving the hand is an ordinary move: it is in the record, it undoes, and a board taken up from a
record comes back with the hand where it was left. It says nothing in the log, though â a line
every time somebody nudged the cursor a square would bury what the board was actually doing.

## Commands


| | |
| --- | --- |
| arrows, `wasd` | move the hand about the board |
| space, `press` | set the cell the hand is on turning |
| `f7` | set that cell turning, wherever the hand is |
| `why f7` | what it would reach when it lands, and whether anything is reaching back |
| `faster`, `slower`, `speed 7` | how long a quarter turn is given to take, from 900ms down to 100ms |
| `up`, `down`, `left`, `right` | move the hand a cell (the arrows and `wasd` send these) |
| `press` | set the cell the hand is on turning (the space bar sends this) |
| `sound`, `mute` | whether this board is heard as well as read |
| `log` | whether what the game has been saying is drawn under the board |
| `undo`, `redo` | walk the cascade back and forward, a wave at a time |
| `restart`, `restart 42` | another board, or that one |
| `resign` | put it down with the touches you have left unspent |

The clock is a notch a player sets and it is an ordinary move — in the record, replayed off it,
and undoable like any other. What it changes is how long you spend watching; the board does the
same thing at every notch.

## What this game asked the machinery for

Cascade is the first game here whose board **moves between two beats** rather than only on them,
and the engine grew three things for it. All of them are general, none of them is about elbows,
and every other game got them for free and does nothing with them.

- **A frame is not a move.** A beat is a move, folded by the ordinary `update` and written into
  the record; a *frame* is a redrawing and nothing else, and the only thing that differs from one
  to the next is `Margins.Phase`, running from 0 at a beat towards 1 before the next one. Nothing
  a frame draws can reach the timeline, the record or the rules. `Pulse.Frames` is how a game
  asks for them, from where it stands — this one asks for six while something is turning and
  none at all while nothing is.
- **A field.** `Walled` puts a wall round every cell, which is right for nine squares and
  unreadable at two hundred and fifty-six. A `Field` is a glyph a cell, with a legend across the
  top and labels down the side, and each cell carries a **mood** — a bare word saying what it is
  *doing* rather than what it is. A terminal ignores the moods and draws the glyph; a page turns
  them into classes, which is where `turning`, `landed`, `lit` and `pace-5` come from.
- **A table that can be heard.** `Playable.Rings` reads what the board is sounding off the state
  after a move, rather than out of the notices — which is what makes a game taken up from a
  record sound exactly like the game it was saved from.

And one thing it found rather than asked for: **a move that changes nothing is no longer written
down**. The clock here beats over a board at rest for as long as nobody touches anything, and
those beats used to leave a line in the record apiece. Now a game nobody has touched has an empty
record, a board at rest is not sent down every wire in the house twice a second, and Snake stops
writing down a steer that turned a snake the way it was already going.
