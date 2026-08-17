# Snake

One square a turn, eat the star, and do not run into anything. The sixth of the games here, and
the engine it runs on is [one directory up](../../../README.md).

The arcade game, turn by turn instead of tick by tick — which turns out to be the same game with
the hurry taken out of it, and leaves the one decision it was always about: which of three
squares to be standing on next.

```powershell
dotnet run -- snake play 1                # alone, and the score is what you ate
dotnet run -- snake play 2 --rival hard   # against the machine
dotnet run -- snake play 4                # four snakes, four people, one board
dotnet run -- snake serve 1               # the same board in a browser

dotnet run -- snake replay logs/...-snake-1p-seed<n>.log   # one you put down
```

**The first game here whose table is any size with the same rules either way.** Every other game
in this program is a fixed table or a range it was designed around: two for noughts and crosses,
seven for Diplomacy, one for Life. This one is the arcade game at a table of one and a race at a
table of four, and nothing above the seam is told which of those it is — the rules are identical
and the ending is the only thing that reads differently.

## Playing

Every command that is not a direction — `undo`, `redo`, `history`, `save`, `notes`, `commands`,
`view`, `resign`, `restart`, `help`, `quit` — belongs to the engine and is [documented
there](../../../README.md). What this game adds is four ways to go.

| command | action |
| --- | --- |
| `north` (`n`, `up`) | one square that way |
| `east` (`e`, `right`) | " |
| `south` (`s`, `down`) | " |
| `west` (`w`, `left`) | " |
| `go` (`on`, `ahead`) | straight on, the way you are already facing |
| `why east` (`look`) | what is one square that way, before you commit to it |

**There is no `wasd`, on purpose.** Every player's fingers want it and it cannot be had here:
`w` is west on a board with compass points on it and up on a keyboard, and `d` is down to half
the people who type it and right to the other half. A key that means two things at a game where
one wrong move ends the game is worse than one key more to reach for, so the single letters are
the compass and nothing else.

A record is written in the compass whichever word was typed, so `up` and `north` read back as
one line and a saved game replays the same either way.

## Rules as implemented

- A board of 24 by 14 with **walls** at the edges. [Life](../Life/README.md) joins its edges and
  this one does not, which is the whole difference between a board you can watch for ever and a
  board that is a game.
- One square a turn, any way but back into your own neck — the one thing this game refuses.
- A snake starts three segments long. Eating a piece of food adds one, and it arrives **on the
  next step**, because a snake grows by keeping its tail rather than by gaining a head.
- The next piece of food is placed the moment one is eaten, somewhere nothing is standing.
- A snake stops when its head meets the wall, another snake, or itself. What is left of it lies
  where it fell, and everybody else has to go round it.
- At a table of one the game is over when the snake stops, and the score is what it ate. At a
  table of more it is over when one is left moving, and that one has won.
- `resign` stops your own snake. At a table of more, the others play on.

**The square your own tail is standing on is a square you may move into**, because the tail
leaves as the head arrives — but not while you are growing, because then it is staying where it
is. That one rule is the difference between a snake that can turn tightly and a snake that
cannot, and it is the only place in this game where what is on the board and what is *about to
be* on the board are different questions.

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
as `#` in the quiet colour, because what it is now is an obstacle and the one thing anybody needs
to know about it is that it is in the way.

## The machine

Three ways of playing, and the difference between them is one question: **how much room does
this step leave me in?**

| | `easy` | `medium` | `hard` |
| --- | --- | --- | --- |
| walks into things | often | never | never |
| counts the room a step leaves | no | no | yes |
| plays something other than its best | 35% | 5% | never |

Anybody can see that the square in front of them is empty. What kills a snake is the square it
will want four moves from now, and the cheapest honest way to ask about that is to count how
many squares can still be reached from the one you are about to stand on. A machine that only
looks one square ahead eats well and then walls itself into a pocket — every time.

Which is worth reading beside [noughts and crosses'
machine](../TicTacToe/README.md#the-machine), where `hard` means *cannot be beaten* because the
game is small enough to walk to its end. A snake on an open board has no end to walk to, so
`hard` here promises something weaker and measurable instead: over a run of games it eats more
than three times what `easy` does, and [snake.fsx](../../../tests/snake.fsx) is where that is
held to.

## The files

Ten files, in the same shape as every other game here.

| File | Role |
| --- | --- |
| [Board.fs](Rules/Board.fs) | The board, the four directions, and a step - which may leave the board, because that is what makes a wall hittable |
| [Snakes.fs](Rules/Snakes.fs) | One snake: the body head-first, what it owes itself in growth, and how it stopped |
| [Session.fs](Rules/Session.fs) | The table: the snakes, the food, whose turn it is, and the generator the next piece comes from |
| [Turn.fs](Rules/Turn.fs) | `Move`, `Ahead`, and how a turn goes: one square, and everything that could be in the way of it |
| [Words.fs](Rules/Words.fs) | Every string a player reads |
| [Rival.fs](Rules/Rival.fs) | A seat played by the program: what a step is worth, and how much room it leaves |
| [Ink.fs](Reading/Ink.fs) | Five colours - one per seat, and the food |
| [Parse.fs](Reading/Parse.fs) | Four directions, three spellings each, and the word for carrying on |
| [Render.fs](Reading/Render.fs) | Every screen described once as a [`Scene`](../../../README.md#a-screen-described-once), which `Readers` then draws three ways |
| [Offer.fs](Offer.fs) | Both seams filled in |

`Ahead` — wall, snake, food or open board — is a type rather than three tests written out where
they are wanted, because three things ask that question and they must not come to disagree: the
rules, to say what a step did; the machine, to pick one; and the screen, to answer a player who
asked what is over there before committing to it.
