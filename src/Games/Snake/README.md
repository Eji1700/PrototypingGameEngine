# Snake

The arcade game, and the one game here that does not wait for you. The sixth of the games, and
the engine it runs on is [one directory up](../../../README.md).

It is offered two ways, which are the same rules at two paces:

| | | |
| --- | --- | --- |
| `snake` | on a clock | the snakes move on their own and quicken as they eat; you only steer |
| `snake-turns` | a step at a time | the board waits for you, four can play round one keyboard, and the machines play here |

```powershell
dotnet run -- snake play 1                # the arcade game: arrows steer, space holds
dotnet run -- snake serve 1               # the same, in a browser
dotnet run -- snake play 2                # two snakes: arrows are A's, wasd are B's

dotnet run -- snake-turns play 2 --rival hard   # the same board, a step at a time
dotnet run -- snake replay logs/...-snake-1p-seed<n>.log   # one you put down
```

## A game that does not wait, on an engine that folds

The interesting half of this game is not the snake. It is that the engine underneath is a pure
Model-View-Update fold — nothing happens until a message arrives — and an arcade game is the
one shape that seems to need something else.

It does not. **A beat is a move.** The game says what its beat is and how long a table should
leave between them, and the *tables* keep the time:

```fsharp
Pulse =
    Some
        { Every = every      // from where the game stands, so it quickens as the snakes eat
          Beat = Beat        // an ordinary move, folded by the ordinary update
          Pressed = pressed }  // and a key stands for a line this game already reads
```

What that buys is that nothing about real time leaks into the game:

- **The record is every beat.** A saved game replays to the square it was saved on, because
  what is in the file is the moves — beats included — and not a stopwatch.
- **`undo` walks back through beats** like any other move, and takes the food draw with it.
- **Every rule in this game is checked without a clock.** [snake.fsx](../../../tests/snake.fsx)
  folds beats by hand, and if any of it needed a timer the design would be wrong.
- **All three tables drive it the same way**: a loop at a terminal, a timer in the process
  serving a browser, and a timer per table in a house — each calls the same `beaten`, which is
  four lines because a beat is a move and the tables were already generic in what a move is.

## Playing

Every command that is not a direction — `undo`, `redo`, `history`, `save`, `notes`, `commands`, `log`,
`view`, `resign`, `restart`, `help`, `quit` — belongs to the engine and is [documented
there](../../../README.md). What this game adds is four ways to go, at two paces.

**On a clock**, a direction turns a head and the beat moves everything:

| | |
| --- | --- |
| arrows | turn Snake A |
| `wasd`, `ijkl`, number pad | turn B, C and D — four hands at one keyboard |
| `north` (`n`, `up`) | the same, typed. A bare direction is A's |
| `b north` | somebody else's, by the letter it is drawn with |
| `go` | one beat, said out loud — for a console that cannot press anything |
| `+` and `-` | wind the clock up or down a notch (`faster`, `slower`) |
| `speed 7` | straight to a notch, from 1 to 9 |
| space | hold the clock while you think - and read everything the running screen leaves off |
| `r` | deal another board, once the clock has stopped (`restart`, which every game here takes) |
| Enter | type a whole line, with the clock held while you do |
| Esc | put it down |

`r` only answers while the game is held or over, and that is the whole of the guard it needs: a
key meant for a snake in the middle of a run would otherwise sweep the game away, and a game
swept away by a mistyped key is a game nobody types a key at again. Typing `restart` works at any
moment, as it does at every game here.

**While the clock is running you get the board and nothing else** - the heading, the board, the
score and the last few lines of what was said. The writing that explains the board, the box
listing what can be typed and the list of ways to turn are all for somebody who has stopped to
read them, and redrawing them three times a second under a board that has moved makes a screen
nobody can follow. Space puts them back, because holding the clock is exactly the moment you
wanted them. It is the same `notes` and `commands` setting either way, and saying either of
those while held is what you get back next time you hold.

**A step at a time**, a direction *is* a step:

| | |
| --- | --- |
| `north` (`n`, `up`) | one square that way |
| `go` (`on`, `ahead`) | straight on, the way you are already facing |

Both take `why east`, which says what is one square that way before you commit to it.

**There is no `wasd` among the typed words, on purpose.** `w` is west on a board with compass
points on it and up on a keyboard, and `d` is down to half the people who type it and right to
the other half. The typed words are the compass; the *keys* are `wasd`, and every key sends a
line that names its snake — `b north` — so a key can never mean a direction for somebody else's
snake.

## Rules as implemented

- A board of 24 by 14 with **walls** at the edges. [Life](../Life/README.md) joins its edges and
  this one does not, which is the difference between a board you can watch for ever and a board
  that is a game.
- One square a beat (or a turn), any way but **back into your own neck** — which is where the
  neck actually is, not the opposite of the way the head points. On a clock those are different
  questions: turning is not moving, so a snake can be turned twice between two beats, and it is
  then facing one way while lying another. Asking about the facing let north-then-east through as
  two legal presses and killed the snake on the next beat.
- A snake starts three segments long. Eating adds one, and it arrives **on the next step**,
  because a snake grows by keeping its tail rather than by gaining a head.
- The next piece of food is placed the moment one is eaten, somewhere nothing is standing.
- A snake stops when its head meets the wall, another snake, or itself. What is left of it lies
  where it fell, and everybody else has to go round it.
- At a table of one the game is over when the snake stops, and the score is what it ate. At a
  table of more it is over when one is left moving, and that one has won.
- `resign` stops your own snake at a game of turns. On a clock — where nobody was waiting on you
  in the first place — it gives the whole game up.

**Everything moves at once on a clock**, and that costs three rules a game of turns never needed:

- every tail that was going to move counts as gone, so snakes can follow each other nose to tail
  and none of them is bitten by a square that was about to be empty;
- two heads that pick the same square **both** stop, because neither of them got there first;
- and a snake that stops on a beat still leaves its body there for whoever is still going.

**How fast it goes is yours to set**, from the first beat or in the middle of a run: `+` and `-`
wind the clock a notch, `speed 7` goes straight to one, and the board says which notch it is on
while it runs. Nine notches, 40ms apart:

| notch | 1 | 2 | 3 | 4 | **5** | 6 | 7 | 8 | 9 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| a beat | 380ms | 340ms | 300ms | 260ms | **220ms** | 180ms | 140ms | 100ms | 60ms |

On top of the notch, the game quickens by 8ms for every piece the longest snake has eaten - a
board that ran at one speed for ever would be a board with nothing to fear on it - down to a
floor of 50ms.

**The notch is in the position, not in a setting**, which is the one surprising thing about it. A
clock could hardly be more obviously "how this is being read" — except that what a game hands the
table is `Every: 'State -> TimeSpan`, so the only way the game can have an opinion is for the
state to hold one. Which turns out to be the right place anyway: winding it up is a move, so it
goes into the record, it replays, and it can be taken back. It does not outlive the game, though:
a `restart` deals a fresh board at notch 5.

```
+------------------------+
|........................|
|....aaA.................|
|........*.........Bbb...|
|........................|
+------------------------+
```

A snake is its own letter: small along the body, capital at the head, so a board tells you which
way everything is pointing at a terminal with no colour in it. A snake that has stopped is drawn
as `#` in the quiet colour, because what it is now is an obstacle.

## The machine

At the pace that has turns, where a machine has a turn to take. Three ways of playing, and the
difference between them is one question: **how much room does this step leave me in?**

| | `easy` | `medium` | `hard` |
| --- | --- | --- | --- |
| walks into things | often | never | never |
| counts the room a step leaves | no | no | yes |
| plays something other than its best | 35% | 5% | never |

Anybody can see that the square in front of them is empty. What kills a snake is the square it
will want four moves from now, and the cheapest honest way to ask about that is to count how
many squares can still be reached from the one you are about to stand on.

**There is no machine on the clock**, and the empty list is the honest answer rather than a gap:
the engine plays a machine's turns between one person's move and their next, and at a table
where nobody's turn it is there is no such gap. A rival there would have to be steered by the
beat, which is a thing to build rather than a thing to pretend.

## The files

Ten files, in the same shape as every other game here.

| File | Role |
| --- | --- |
| [Board.fs](Rules/Board.fs) | The board, the four directions, and a step - which may leave the board, because that is what makes a wall hittable |
| [Snakes.fs](Rules/Snakes.fs) | One snake: the body head-first, what it owes itself in growth, and how it stopped |
| [Session.fs](Rules/Session.fs) | The table: the snakes, the food, the pace, and the generator the next piece comes from |
| [Turn.fs](Rules/Turn.fs) | `Move`, `Ahead`, a step, and the beat that moves everything at once |
| [Words.fs](Rules/Words.fs) | Every string a player reads |
| [Rival.fs](Rules/Rival.fs) | A seat played by the program: what a step is worth, and how much room it leaves |
| [Ink.fs](Reading/Ink.fs) | Five colours - one per seat, and the food |
| [Parse.fs](Reading/Parse.fs) | Two readers, one per pace: at one a direction is a step, at the other it turns a named snake |
| [Render.fs](Reading/Render.fs) | Every screen described once as a [`Scene`](../../../README.md#a-screen-described-once), and the keys a page steers with |
| [Offer.fs](Offer.fs) | Both seams filled in, twice - one `Playable` per pace |

`Ahead` — wall, snake, food or open board — is a type rather than three tests written out where
they are wanted, because three things ask that question and they must not come to disagree: the
rules, to say what a step did; the machine, to pick one; and the screen, to answer a player who
asked what is over there before committing to it.
