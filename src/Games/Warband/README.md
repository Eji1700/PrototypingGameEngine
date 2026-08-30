# Warband

Two squads of five, mustered onto ten hexes apiece out of each other's sight, a stretch of ground
between them, and then a battle neither of you plays. Two seats, and no chance anywhere in it: the
same two musters fight the same battle every time, blow for blow, which is why the muster is hidden
and why the battle is watched rather than played.

```
dotnet run -- warband play 2                    # two people at one keyboard
dotnet run -- warband play 2 --rival steady     # one, against the machine
```

## The rules

**The formation.** Each squad has ten hexes in three ranks: `f1` to `f3` across the front, nearest
the other squad, `m1` to `m4` across the middle, `b1` to `b3` at the back. The ranks sit half a hex
apart, so `m2` and `m3` touch six hexes each, `f2` and `b2` four, the other six three, and the
front rank never touches the back one. Touching is what a mender mends into and what a warder
shields; nothing else in the game reads it.

**The muster.** Squad One places first, then the squads take turns, one unit at a time, until each
has five, at most two of a kind; a hex already taken is refused, and so is a third of one kind.
Neither squad sees the other's until both are on the field, and the tenth placement joins the
battle. `resign` during the muster ends the game — the squad whose turn it is walked away.

**The roster.** Where a unit stands is what it does: each kind is three answers, one for each rank.

| | front | middle | back | vigour | quick | reach |
| --- | --- | --- | --- | --- | --- | --- |
| `Foot` footman | strike 3 x2 | strike 3 | strike 1 | 10 | 3 | 1 |
| `Pike` spearman | strike 5 | strike 3 x2 | strike 1 | 9 | 3 | 2 |
| `Bow` bowman | strike 1 | shoot 2 x2 | shoot 2 x3 | 7 | 4 | 4 |
| `Ride` rider | strike 3 x3 | strike 3 | nothing | 12 | 5 | 2 |
| `Mend` mender | strike 1 | mend 2 | mend 4 | 6 | 2 | 1 |
| `Ward` warder | strike 2 x2 | strike 2 | strike 1 | 14 | 1 | 1 |

Vigour is what a unit takes before it falls, quick is who acts first, and reach is how many hexes
of ground a blow will cross — the column is the furthest of the three ranks: a spear reaches 2 from
any rank, a charge 2 from the front and 1 from the middle, a bow 4 from the middle or back and 1 in
front, everything else 1. A warder, wherever it stands, steps in front of any blow aimed at a unit
on a hex it touches. A rider in the back rank has no room to ride and does nothing.

**The ground.** The lines are dealt touching, 1 hex apart, and either squad may say `engage <n>`
while the muster is on to stand them 1 to 9 hexes apart; the last word stands, and both squads are
told. Nobody stands on the ground and nothing can be mustered there; what it changes is who can act
at all, since a blow whose reach is shorter than the ground lands nowhere. At 2 hexes a spearman
from any rank, a rider from the front and a bowman from the middle or back still act; at 3 or 4
only the bowman; at 5 to 9 nobody, and the first beat says so. Mending never crosses the ground
and is never stopped by it.

**The battle.** Once both squads are mustered nobody is asked anything again. It goes in rounds:
every unit still up acts once a round, quickest first, a tie on quickness going to Squad One in odd
rounds and Squad Two in even ones, then front rank before middle before back, then left to right.
A unit that cannot reach the other line is left out of the round, and the board counts them; a
unit felled before its turn comes round never acts.

- A **strike** falls on the foremost rank of the other squad that still has anybody standing, on
  whoever there has the most left in them. Empty a front rank and the blows walk back to the
  middle. A run of blows is aimed afresh for each, so a rider's second strike finds somebody else
  once its first has felled its target.
- A **shot** ignores rank and finds whoever is nearest to falling.
- A **warder** on a hex touching the target steps in front of the blow, strike or shot, and takes
  it — the furthest forward of them if two touch. A blow steps aside once and no further: nothing
  steps in front of a blow aimed at a warder.
- A **mending** goes into whichever hex the mender touches is missing the most, up to that unit's
  vigour. It cannot bring anybody back up, and a mender with nobody hurt beside it does nothing.

A squad with nobody left standing is broken, and the other holds the field. If neither breaks in
12 rounds it is settled on what is left: the squad with more vigour left across its standing units
takes it, and equal is drawn. When a round would open and nobody on either side can reach the other
line, it ends there the same way, or with nothing to choose between them.

## The words

[Parse.fs](Reading/Parse.fs) reads these, and nothing else; case does not matter, and the record
writes each move as the first form in its row.

| | |
| --- | --- |
| `muster bowman b2`, `bowman b2` | a unit of that kind on that hex. A kind is its name or the four letters the board draws it with — `pike m2` is a spearman on m2 |
| `engage 3`, `ground 3` | stand the lines 3 hexes apart, from 1 to 9, while the muster is on |
| `run`, `p` | set the battle running if it is stopped, and stop it if it is running |
| `start`, `go` | set it running |
| `stop`, `pause`, `halt` | stop it |
| `step`, `s` | one beat by hand, whether the battle is running or stopped |
| `beat` | the clock's own move, spelt out for a console with no clock |
| `why bowman`, `why m2`, `ask m2` | what a kind does from each rank, or what a hex is and what it touches; anything else gets the roster |
| `resign` | walk away from the muster — refused once the battle is joined |

From the tenth placement to the end the battle is on the clock, and a beat is a move: the record
writes `beat` for each one that landed, and nothing for a beat during the muster or while the
battle is stopped. The game is dealt running, so the battle starts the moment the lines are formed;
`stop` during the muster says the battle will stand stopped once they are.

The rules refuse, in words, a muster on a hex already taken or a third of one kind, a muster or
`engage` once the lines are formed, `engage` outside 1 to 9, `step` before there is a battle, and
`resign` during one. `undo`, `history`, `save`, `view`, `restart`, `quit` and the rest are the
table's, the same at every game — see [at the prompt](../../../README.md#at-the-prompt).

## The board

Three blocks from the top: **Across the field**, the other squad drawn facing you with its front
rank nearest the middle of the screen; the ground, a line saying how far apart the lines stand and
a row of dots for every hex of it; and **Your squad**, front rank at the top. A hex shows the
unit's code and what is left of it, and `gone` once it has fallen.

```
     +-----+-----+-----+
     | f1  |Ride | f3  |
     |     |12/12|     |
  +--+--+--+--+--+--+--+--+
  | m1  |Ward | m3  | m4  |
  |     |14/14|     |     |
  +--+--+--+--+--+--+--+--+
     | b1  | b2  | b3  |
     |     |     |     |
     +-----+-----+-----+
```

During the muster the blocks below are **The muster** — how many still to place and what you have
so far — and **The roster**, the table above with anything that could not reach across the ground
as it stands drawn quiet. Once the battle is joined they are **The field** — both squads' standing
counts, how many of each cannot put a blow across the ground, who swings next, and whether the
clock is running — and **What next**, lines you could type. `why <kind>` and `why <hex>` answer in
a box of their own. Three notes explain the hexes, the ranks and reach, and `notes` hides them.

The three views come from one description
([how the board is drawn](../../../README.md#how-the-board-is-drawn)); the one thing a page does
that a terminal cannot is fade the ground towards the middle of the gap. Two things are drawn in
colour, set on the [video page](../../../README.md#colours): `foe`, the squad across the field,
crimson by default, and `hex`, the hexes and every hex named in what the game says, bone. Your own
squad is in the colour the table keeps for yours.

**On the clock.** A beat lands every 600 milliseconds and nothing winds it — `+` and `-` do nothing
here. At a terminal `p` runs and stops the battle and `.` takes one beat, both as moves; space or
`h` holds the table's own clock and writes nothing down; Enter opens the prompt, which is how a
muster is typed. In a browser, space and `p` run and stop it and `.` steps. The rest is the
engine's — see [a game on a clock](../../../README.md#a-game-on-a-clock).

**What it rings.** A muster placed sounds `ready` for the squad waited on, the tenth placement a
`chime`, every blow a `tap`, a battle settled a `fanfare`, and a squad walking away a `knell` — read
off the position rather than the move, so a game taken up from a record sounds like the one it was
saved from.

**What is hidden.** While the muster is on, the other squad's hexes are drawn empty under a count
of how many it has placed, the log and the history say `Squad One musters, out of your sight.` and
write the line as `muster` alone, and a placement the rules refused is reported as `asked for a
muster the rules would not take`, with no hex or kind in it. The ground is told to both. Once the
lines are formed both formations are open to both squads and the history writes every muster line
out. At one keyboard the screen belongs to whoever is to place, and to Squad One through the
battle; a [hosted table](../../../README.md#a-hosted-table) draws each console its own seat, holds
the turn during the muster, and lets either console run, stop and step the battle.

## The machine

Two skills, in [Rival.fs](Rules/Rival.fs), and both of them only muster: once the lines are formed
there is nothing left for anybody to decide.

| | |
| --- | --- |
| `raw` | musters a kind at random onto a hex at random, and finds out what the ranks were for |
| `steady` | musters to a plan: the heavy at the front, the reach behind it, the bow and the mender at the back |

`steady` draws one of three written-out squads when it first places and follows it to the end:
rider f2, footman f1, warder m2, bowman b2, mender b1; footman f1, footman f3, spearman m2, warder
m3, bowman b2; or warder f2, rider f1, spearman m3, bowman b2, bowman b3. Which one comes from the
deal's seed and its seat, so the same deal against `steady` musters the same squad. How a machine
is seated and held is the engine's — see
[against the machine](../../../README.md#against-the-machine).

## The files

In the order [Warband.fsproj](Warband.fsproj) compiles them.

| | |
| --- | --- |
| [Rules/Formation.fs](Rules/Formation.fs) | the ten hexes, their names, and what touches what |
| [Rules/Kinds.fs](Rules/Kinds.fs) | the six kinds as three stances apiece, with vigour, quickness and reach |
| [Rules/Squads.fs](Rules/Squads.fs) | a squad, and who a strike, a shot, a warder and a mending pick out of one |
| [Rules/Session.fs](Rules/Session.fs) | the state — mustering, fighting or ended — the ground, what the board is sounding, and the order a round acts in |
| [Rules/Events.fs](Rules/Events.fs) | everything the game can say happened and everything it can refuse |
| [Rules/Battle.fs](Rules/Battle.fs) | one beat: a unit's turn, whether it reaches, and how the battle ends |
| [Rules/Turn.fs](Rules/Turn.fs) | the moves, what each does at each stage, and the sound a move leaves |
| [Rules/Words.fs](Rules/Words.fs) | every sentence a player reads, the record's lines, and what each seat is told |
| [Rules/Rival.fs](Rules/Rival.fs) | the two skills and the three plans |
| [Reading/Ink.fs](Reading/Ink.fs) | the two colour slots, and hex names picked out in the log |
| [Reading/Parse.fs](Reading/Parse.fs) | a typed line as a move or a question |
| [Reading/Render.fs](Reading/Render.fs) | the two honeycombs, the ground, the roster, the field, the answers, the rules and the page |
| [Offer.fs](Offer.fs) | the seam: the `Playable`, the clock, the sounds and the faults |
| [Program.fs](Program.fs) | the game as a program of its own |

## Checks

[tests/warband.fsx](../../../tests/warband.fsx) loads [Warbands.fsx](../../../tests/Warbands.fsx)
and holds the game to: the formation's counts of what touches what; each kind's stances and reach;
the muster's turns and refusals; a beat costing nothing during the muster or while stopped; one
unit's turn on positions built by hand — strike, shot, a warder stepping in once, mending; the
ground, from what reaches across it to two lines that cannot; the three ways a battle ends, and
the same two musters fighting the same battle; what one squad is told of the other; `steady`
mustering whole squads; every sound; the keys; a hosted table refusing the console whose turn it
is not; `Conforms.against` over a muster with a refused line in it; and the game's own `Faults` in
[Offer.fs](Offer.fs) — the formation, the roster and the machine's plans — coming back empty.

[tests/counting.fsx](../../../tests/counting.fsx) holds its counts of units, hexes, rounds and
blows, and every refusal, ending and answer that carries a number, to reading right at nought and
one. The three Warband records in [logs/](../../../logs) are taken back up by
[records.ps1](../../../tools/records.ps1) on every CI run.
