# Warband

Two squads of five, mustered onto ten hexes apiece out of each other's sight, a stretch of ground
between them, and then a battle neither of you plays. Two players.

```
dotnet run -- warband play 2                    # two people at one keyboard
dotnet run -- warband play 2 --rival steady     # or one, against the machine
```

Everything you decide, you decide before the fighting starts. There is no chance in the battle
anywhere: the same two musters fight the same battle every time, blow for blow. That is not an
economy, it is the game — it is why the muster is hidden, and why what a squad is worth is entirely
a question of where each of the five is standing, and of how far off the other line is.

## The formation

Ten hexes in three ranks, offset by half a hex:

```
     f1  f2  f3        the front rank, nearest them
   m1  m2  m3  m4
     b1  b2  b3        the back rank
```

That is where it differs from a three by three square, and the difference is not decoration. On
squares, every cell in the middle has the same four neighbours and every rank is three wide. Here
the middle rank is four wide, `m2` and `m3` touch six hexes apiece, and the four corners touch
three. The front rank never touches the back one. Say `why m2` and the game will tell you what any
hex touches.

Two things read that: a **mender** puts back only into hexes it touches, and a **warder** steps in
front of blows aimed at hexes it touches. Both are worth roughly twice as much at `m2` as at `f1`,
and neither cares which rank it is standing in for anything else.

## The muster

Five units each, taken from the roster, **at most two of a kind**, placed one at a time turn and
turn about. Neither squad sees the other's until both are on the field — the board draws their ten
hexes empty and the log says only that a muster happened.

```
bowman b2         a bowman on b2 (or 'muster bowman b2')
engage 3          stand the two lines three hexes apart
why bowman        what one does from each rank
why m2            what that hex is and what it touches
```

The five hexes you leave empty are as much a choice as the five you fill: a squad packed into the
front rank kills faster and is all in range of everything, and one spread over the middle rank has
its warder covering six hexes instead of three.

## The roster

Where a unit stands is what it does. This is the whole game, and it is the one thing every kind
below is an answer to.

| | front | middle | back | vigour | quick | reach |
| --- | --- | --- | --- | --- | --- | --- |
| **Footman** | strike 3 ×2 | strike 3 | strike 1 | 10 | 3 | 1 |
| **Spearman** | strike 5 | strike 3 ×2 | strike 1 | 9 | 3 | 2 |
| **Bowman** | strike 1 | shoot 2 ×2 | shoot 2 ×3 | 7 | 4 | 4 |
| **Rider** | strike 3 ×3 | strike 3 | *nothing* | 12 | 5 | 2 |
| **Mender** | strike 1 | mend 2 | mend 4 | 6 | 2 | 1 |
| **Warder** | strike 2 ×2 | strike 2 | strike 1 | 14 | 1 | 1 |

Reach is per stance rather than per kind, and the column is the furthest of the three: a rider
charges across two hexes from the front rank and only one from the middle. `why rider` reads them
out rank by rank. With the lines touching — where a game is dealt — every reach on the roster is
enough, and none of them bites.

- A **footman** is the plain answer and wants the front.
- A **spearman** reaches past the rank in front of it, and is the one kind that would rather be in
  the middle than at the front.
- A **bowman** is three shots from the back and almost nothing in front of everybody.
- A **rider** is the hardest charge there is and has *no room to ride* from the back rank — it
  stands there and does nothing, which is the plainest lesson the game teaches.
- A **mender** is the only thing that puts anything back, and only into hexes it touches.
- A **warder** takes blows meant for its neighbours wherever it stands. What the rank changes for a
  warder is how many neighbours it has.

## The ground

The two formations are not drawn up on top of each other. There are hexes between them, and how
many is the **engagement**: one hex is the lines touching, and `engage 3` while the muster is on
stands them three hexes apart instead, out to nine.

```
     b1  b2  b3          Squad Two, drawn facing you
   m1  m2  m3  m4
     f1  f2  f3

     .   .   .   .       the ground - three hexes of it, fading
   .   .   .   .
     .   .   .   .

     f1  f2  f3          yours
   m1  m2  m3  m4
     b1  b2  b3
```

The hexes in between are ground: nobody stands on one and nothing in the rules could put a unit
there, so the board draws them thin and fading rather than as a map. They are there to say *how far*
and nothing else.

What the ground changes is **who can do anything at all**. Every blow has a reach, and a reach
shorter than the ground between the lines lands nowhere.

| ground | what still works |
| --- | --- |
| 1 — touching | everything. This is where a game is dealt |
| 2 | the spear, the charge from the front rank, and the bow |
| 3 – 4 | the bow, and nothing else. An archery duel |
| 5 – 9 | nothing. Neither line can touch the other, and the game says so at once rather than standing there for twelve rounds |

Menders are the exception, and not really an exception: mending happens inside your own formation,
so no amount of ground stops it. A unit that cannot reach is left out of the round entirely rather
than handed a beat to say so in — the roster greys it out and the field box counts it, which is
where a standing fact belongs.

**A rank is who you are, not how far away you are.** Standing at the back does not add to the
crossing; it changes what you do. Counting the ranks into the distance as well would be the more
unified model and it is the obvious thing to try when a unit gets stats of its own — it is left
alone for now because it would quietly rewrite the roster above, and the roster is the part that is
going to change anyway.

Either squad may set the ground while the muster is on and the last word stands. That is a
placeholder: the day a battle is dealt from a scenario rather than agreed at the table, the ground
comes with the scenario and `engage` goes.

## The battle

Once both squads are mustered nobody is asked anything again. It runs on a clock, and every unit
still up acts once a round, quickest first.

```
run, stop         whether the battle runs on its own ('p' at a terminal, space in a browser)
step              one blow, while it is stopped ('.' at a terminal)
beat              the clock's own move, spelt out for a console that cannot press anything
```

All three are moves, so the record says when the battle was running. Space or `h` at a terminal
holds the table's own clock instead, and writes nothing down.

- A **strike** falls on the foremost rank of the other squad that still has anybody up, on whoever
  there has the most left in them. Empty your front rank and the blows walk back to your middle.
- A **shot** ignores rank and finds whoever is nearest to falling. The two are opposites on purpose:
  melee grinds down whoever is holding the line, arrows finish whoever is nearly gone.
- A **mending** goes into whichever hex the mender touches is missing the most. It cannot bring
  anybody back up.
- A **warder** steps in front of any blow aimed at a unit on a hex it touches. **A blow steps aside
  once and no further** — nothing steps in front of a blow aimed at a warder, or two warders either
  side of one hex would hand it back and forth for ever.

A tie on quickness goes to the first squad in odd rounds and the second in even ones, so neither is
always the one that swings first. A squad with nobody left up is broken and the other holds the
field. If neither breaks in twelve rounds — two squads of warders and menders will do it, and so
will an archery duel at three hexes — it is settled on what is left standing.

There is no resigning a battle. It was decided the moment it was joined, and the game says so
rather than pretending otherwise; `undo` walks back into the muster if you would rather try
something else.

## What is where

| | |
| --- | --- |
| [Rules/Formation.fs](Rules/Formation.fs) | The ten hexes, and what touches what. The only file that knows this is not a square grid |
| [Rules/Kinds.fs](Rules/Kinds.fs) | The six kinds, as three answers apiece — one for each rank — and how far each answer reaches |
| [Rules/Squads.fs](Rules/Squads.fs) | A squad, and every question a blow has to ask of one before it can be aimed |
| [Rules/Session.fs](Rules/Session.fs) | The state: a muster, a battle, or an ending — the ground between the lines, and the order a round acts in |
| [Rules/Events.fs](Rules/Events.fs) | Everything the game can say happened, and everything it can refuse |
| [Rules/Battle.fs](Rules/Battle.fs) | One blow, whether it gets there, and everything that follows from it. Nobody plays this |
| [Rules/Turn.fs](Rules/Turn.fs) | The moves and the fold |
| [Rules/Words.fs](Rules/Words.fs) | Every word a player reads — and the one place anything is hidden |
| [Rules/Rival.fs](Rules/Rival.fs) | The machine, which only ever musters |
| [Reading/Ink.fs](Reading/Ink.fs) | Two slots: the other squad, and the hexes |
| [Reading/Parse.fs](Reading/Parse.fs) | A typed line read as a move |
| [Reading/Render.fs](Reading/Render.fs) | Two honeycombs facing each other, and the boxes round them |
| [Offer.fs](Offer.fs) | The seam: both halves handed over as one `Playable` |

## The machines

| | |
| --- | --- |
| `raw` | musters a kind at random onto a hex at random, and finds out what the ranks were for |
| `steady` | musters to a plan: the heavy at the front, the reach behind it, the bow and the mender at the back |

Neither of them does anything once the battle joins, because there is nothing to do.

## What it added to the seam

Nothing. See [SEAM.md](../../../SEAM.md). It is the fourth game to need no new member of `Rules`,
`Playable` or `Pulse`, and the first one to be *hidden* and *on a clock* at once — which was the
only thing about it worth doubting, since the two had never met before.

What it did turn up is one small thing, written down where it happened: a game's state may not have
a field called `Phase`, because `Margins` has one and F# resolves a field on an un-annotated value
by name alone. It is called `Stage` here, and [Rules/Session.fs](Rules/Session.fs) says why.
