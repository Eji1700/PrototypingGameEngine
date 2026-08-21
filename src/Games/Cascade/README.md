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

Three sounds: a **tap** as a wave lands, a **chime** as a cascade comes to rest, and a
**fanfare** when a shape comes up. A browser makes all three; a terminal has one bell and makes
that.

## Commands

| | |
| --- | --- |
| `f7` | set that cell turning |
| `why f7` | what it would reach when it lands, and whether anything is reaching back |
| `faster`, `slower`, `speed 7` | how long a quarter turn is given to take, from 900ms down to 100ms |
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
