# Diplomacy

The standard board, played by the standard rules: seven powers, seventy-five provinces,
thirty-four supply centres, and no chance in it anywhere - not a die, not a shuffle, not a
card. Every power writes its orders in secret and they are all carried out at once. Hold
eighteen centres and you have won.

It takes seven, and exactly seven. Seat 1 is Austria and seat 7 is Turkey at every game -
the seats run Austria, England, France, Germany, Italy, Russia, Turkey - and the seats nobody
is in are given to the machine with `--rival`, once for each.

```powershell
dotnet run -- diplomacy play 7          # seven seats at this keyboard
dotnet run -- diplomacy host 7          # seven of you, at your own machines
dotnet run -- diplomacy serve 7 --rival hard --rival hard --rival hard --rival hard --rival hard --rival hard
```

The last is one of you in a browser, as Austria, against six machines. A game of this takes a
while, so put it down and come back: `quit` keeps it, and [a record](../../../README.md#records)
is taken up against the same machines at the same strength. The rest of playing it - the menu,
the prompt, a table others join - is [the engine's](../../../README.md).

## The rules

**The board.** Seventy-five provinces: nineteen seas, fourteen landlocked and forty-two on a
coast. Thirty-four are supply centres - twenty-two home centres, three to a power and four to
Russia, and twelve neutral - and each power opens with a unit on each of its homes, twenty-two
units in all, Russia's northern fleet on St Petersburg's south coast. Armies walk the land.
Fleets sail the seas and along the coasts and cannot cross a land border - a fleet in Rome
sails to Naples and not to Venice - and the two maps are written out separately, since neither
follows from the other. Spain, Bulgaria and St Petersburg have two coasts each, facing
different waters: a fleet standing there stands on one of them, and a fleet sent there says
which.

**A year.**

| Phase | Reads | Asked of |
| --- | --- | --- |
| Spring, Autumn | `Spring 1901`, `Autumn 1901` | every power with a unit: an order for each, then `commit` |
| Retreats | `Spring 1901 retreats` | every power with a unit beaten out: where each goes, or `disband`, then `commit` |
| Winter | `Winter 1901` | every power with more centres than units, or fewer: builds or removals, then `commit` |

A phase with nobody to ask is passed straight through: a season that dislodged nobody goes on
to the next, and a winter where every power is square - or owes builds and has no free home to
build in - is passed over, so a quiet year is two phases. The record counts phases rather than
years: Spring 1901 is turn 1, Autumn 1901 turn 2 and Winter 1901 turn 3 whether or not anybody
had anything to do in it, and a season's retreats are a turn of their own only when somebody
was beaten out.

Centres change hands once a year, on the way into the winter - after the autumn's retreats, if
there were any: a centre with a unit standing on it passes to that unit's power, and one with
nobody on it keeps its owner. A power with eighteen at that count has won outright and the game
is over. It is over too when one power alone is left with anything on the board, the rest out
of the game or walked away, and when everybody has walked away.

**Everybody writes at once.** The seats come round one at a time, Austria first, and what a
power writes stays its own business until the last of them has committed: the others are told
that it wrote an order for Vienna, and no more. Then the whole phase resolves and everybody sees
all of it together. An order written again for the same unit replaces the first, `cancel vie`
takes one back, and `commit` seals a power's orders and passes the prompt to the next power
still writing, after which nothing of that power's can be changed. A unit with no order holds.
An order for an empty province, or for another power's unit, is refused.

**Orders.** A unit may hold, move to a province it borders, support another unit holding or
moving, or - a fleet at sea - convoy an army from one coast to another.

- One unit beats one unit. A move is as strong as the unit making it and every support given it
  that stands; so is a hold. A move goes through when it is stronger than what holds the
  province and stronger than every other move into it; equal strengths bounce, and nobody
  enters. A unit beaten out of its province is dislodged and must retreat.
- A support names a unit and, for a move, the province it is going to, and the supporting unit
  must itself be able to reach the province the support is aimed at. It is cut by another
  power's attack on the supporting unit from any province but that one - the unit under attack
  cannot cut the support against it, short of dislodging the supporter - and an attack that
  never arrives, a convoy with no water under it, cuts nothing. A support for something the
  named unit is not doing counts for nothing.
- A power never dislodges its own unit: a move against one is worth nothing, and support it
  gives to somebody else's attack on one does not count towards driving it out.
- Two units ordered into each other's provinces meet head-on: the stronger dislodges the
  weaker, equals bounce, and neither passes the other - unless one of them is convoyed, in
  which case they pass. A unit that gets out of its province counts as gone, so a column steps
  forward at once and a ring of units each moving into the next's province all goes round.
- Two or more moves into one province that all bounce leave it contested, and nothing may
  retreat into it that season.
- A convoy carries an army from one coast to another. The army sails if fleets at sea, each
  ordered `c <army> - <destination>` and none of them dislodged, run from a sea washing where
  the army stands to one washing where it is going; a chain broken anywhere leaves the army
  where it is.

**Retreats.** A dislodged unit may go to any province it could have moved to that is empty once
the season has settled, is not contested, and is not the one its attacker came from - unless
the attacker came by sea. Retreats are not supported: a beaten unit with no order written is
disbanded, and two retreating into the same province are both disbanded.

**Winter.** A power owes the difference between its centres and its units. Builds go on a home
centre of its own that it still holds and that stands empty - an army anywhere, a fleet only on
a coast, and on a named coast at St Petersburg - and are its to make or not. Removals are owed
in full: it names the units to give up, and any it does not name are taken for it, furthest
from any of its home centres first, walked by land and sea alike, fleets before armies at the
same distance. It cannot write more builds or removals than it owes.

**Press.** `press france ...` is read by France and by nobody else; `press all ...` by the
table. Everybody is told that a word went and nobody else what was in it, and a power cannot
send word to itself. Press is a move like any other - it is in the record, and `undo` takes it
back - and nothing in the rules makes anybody keep a promise. The machine does not read it.

**Walking away.** `resign` does not end a game of seven. The power that walks away takes back
whatever it had written this phase, and from then on its units stand where they are: beaten
out, they are disbanded, and owed as removals they are taken furthest from home first. Its
centres stay its own until somebody stands on them at an autumn count, and it is never asked
anything again. A power with neither a unit nor a centre is out of the game.

### Points the rules leave to the adjudicator

- **Convoy or march.** An army ordered somewhere it can walk to walks, whatever fleets are
  sitting in the water beside it; an army ordered to a coast it cannot walk to is asking to be
  carried. The map decides, not the wording of the order.
- **Paradoxes.** Orders that come out differently depending on what they are assumed to come out
  as are a ring. A ring of plain moves all goes through - a circle of units may rotate. A ring
  with a convoy in it is broken at the convoy: the fleets are taken as broken, which is what
  stops a fleet carrying the very attack that would dislodge it (Szykman's rule).
- **Support to a province, not to a coast.** `s mao - spa` names Spain; which coast the fleet
  lands on is its own business.
- **A coast left unsaid.** A fleet sent to a province with two coasts and told neither is put on
  the only one it can reach, and asked which when it could reach both. An army is never on a
  coast, and is refused one.
- **Retreat as a move.** `vie - tri` in a retreat phase is a retreat, because the parser reads a
  line without being told which phase the game is in and one line should not mean two things.
- **Nothing said.** A beaten unit nobody gave a retreat is disbanded, a removal nobody named is
  chosen as above, and a build nobody wrote is not made.

## The words

An order names the unit by the province it stands in and nothing else: `vie - tri`, though
`A vie - tri` is let through, `a`, `f`, `army` and `fleet` being ignored. A province is its
code, the three letters written across it on the map - `vie`, `tri`, `nth` - or its name with
the spaces taken out, `stpetersburg`, `northsea` (the Mid-Atlantic, whose name has a dash in
it, is `mao`). A coast follows a slash: `spa/nc`, `spa/sc`, `bul/ec`, `bul/sc`, `stp/nc`,
`stp/sc`. Capitals do not matter, and `-` may be `>`, with or without spaces round it.

| Order | Written | Also read |
| --- | --- | --- |
| Hold | `vie hold` | `vie h`, `vie holds` |
| Move | `vie - tri`; `mao - spa/nc` where there are two coasts | `vie > tri` |
| Support a unit where it stands | `bud s vie` | `bud support vie` |
| Support a move | `bud s vie - tri` | `bud support vie - tri` |
| Convoy | `nth c lon - bel` | `nth convoy lon - bel` |
| Retreat | `vie - tri`, as a move | `vie r tri`, `vie retreat tri` |
| Disband, a beaten unit or one given up in a winter | `disband vie` | `remove vie`, `vie disband`, `vie d` |
| Build | `build a vie`, `build f tri`, `build f stp/nc` | `build army vie`, `build vie f` |
| Take an order back | `cancel vie` | `clear vie` |
| Finish | `commit` | `done`, `ready`, `seal` |
| A word to one power | `press france leave Trieste alone` | the power by name, adjective, letter or first three letters: `press french`, `press f`, `press fra` |
| A word to the table | `press all nobody move` | `press table`, `press everyone` |

Press keeps its capitals, and is closed with a full stop unless you closed it yourself.

Three questions are answered on the spot. They are not moves, so they reach neither the record
nor the other players:

| | |
| --- | --- |
| `borders vie` | whether it is landlocked, a coast or open sea, and everywhere an army and a fleet could reach from it - from each coast, where there are two |
| `where mun` | what stands there, which region it is in, and whose centre it is |
| `orders` | what you have written this phase |

`resign` is the table's word and stands for walking away, above. `undo`, `history`, `save`,
`notes`, `view`, `quit` and the rest are the table's too, and are
[in the main README](../../../README.md#at-the-prompt).

## The board

The screen is headed by the phase and who is to write - `=== Spring 1901 - Austria (you) to
write ===` - and holds four blocks of the game's own, then the table's commands and log:

| | |
| --- | --- |
| **The powers** | one a row: `>` at the power to write, its centres, its units, and where it stands - `still writing`, `committed`, `nothing to do`, `walked away`, `out of the game` |
| **Your orders** | in a season, each of your units with what you have written for it, `-` for nothing yet, then for each unit still unwritten the lines it could type - `bud hold`, every `bud - ...`, `borders bud` - and `commit`. In a retreat phase, each beaten unit, its ways out and `disband`. In a winter, `1 to build`, `2 to give up` or `nothing owed`, what is written, and a tile for each home centre with room in it or each unit that could go |
| **The board** | the map |
| **Last time round** | every phase resolved since you were last asked, in full: each unit's order and what came of it, then who retreated where, who was disbanded, raised or given up, and every centre that changed hands - `Serbia to Austria` |

In a browser every line a unit could type is a button. No order another power has written
appears anywhere until the phase resolves.

What came of an order is one of nine words:

| | |
| --- | --- |
| `moves to tri` | the move went through |
| `held up` | it bounced |
| `stands` | a hold, or a unit with no order |
| `support given`, `support cut`, `nothing to support` | a support that stood, one that was cut, and one whose unit was not doing what it said |
| `convoy holds`, `convoy broken` | a convoying fleet that stood, and one dislodged |
| `no way across` | an army whose convoy never sailed |

**The map** is a honeycomb, 282 hexes in nineteen rows, 120 columns wide on a screen that asks
for 138. A province takes as many hexes as it needs and is drawn as one shape with its code in
every hex; a wall runs between two provinces exactly where they border, by land or by sea, and
never through the inside of one - all 206 borders are drawn, and what you can see is what a
piece can do. The one hole in it is Switzerland, four hexes between Marseilles, Burgundy,
Munich, Tyrolia and Piedmont, which nothing may enter.

```
+--+--+-----+--------------+--+--+  +--+     +--+-----+--------------+--+-----+--+--+--+
|breF | pic |bel*   bel   bel | hol | kie   kie |berG | pru   pru   pru |warR |mosR |
| F F |     |                 |     |           | A G |                 | A R | A R |
+--+  +--+  +--+           +--+  +--+           +--+  +--+           +--+     +--+  +--+
```

A sea's name is written between tildes, `~nth~`, and in the sea's own colour. On the first hex
of every province, reading along the rows: for a supply centre, the letter of the power that
holds it or `*` where nobody does, and under the name whatever stands there - `A` or `F` and
its power's letter. Here Brest is French and holds a French fleet, Belgium is nobody's, and the
army in Berlin is German. A province whose centre is held is outlined in that power's colour,
in `rich` and in a browser.

The map is written in [Atlas.fs](Rules/Atlas.fs) as nineteen strings, one a row, a code to a
hex and `.` for a hole, each row starting half a cell along from the last. `Atlas.problems`
walks that picture against the two border tables and refuses to deal on one that draws a side
which is not a border, leaves out one that is, or draws a province in two pieces.

**Colours.** Eight slots, each settable [as any colour is](../../../README.md#colours): the
seven powers - Austria crimson, England azure, France sky, Germany bone, Italy moss, Russia
violet, Turkey gold - and `sea`, teal. A power's name is painted in its colour wherever the
text says it. The three views are drawn from the same scenes, so they say the same things, and
nothing here is on a clock, so there are no keys.

**What a seat may see** is decided in `SeenBy`, the same at one keyboard and at a table over a
wire:

| | the power itself | every other power |
| --- | --- | --- |
| an order written in a season or a retreat | `Austria: vie - tri.` | `Austria writes an order for Vienna.` |
| an order written in a winter | `Austria: build a vie.` | `Austria writes an order.` |
| an order taken back | `Austria takes back the order for Vienna.` | `Austria takes back an order for Vienna.`, or in a winter `Austria takes back an order.` |
| a word to one power | `Austria to Italy: leave Trieste alone.`, read by both | `Austria sends word to Italy.` |
| a word to the table | `Austria to the table: nobody move.` | the same |

The record (`history`) keeps the same veil over the phase still open - another power's line
reads `an order for Vienna`, or in a winter `an order` and `cancel ...` - and shows the line in
full once the phase has resolved. A word between two other powers reads `press italy ...` in it
for good, and `orders` answers with your own and nobody else's.

## The machine

Three skills. `--help` describes `easy` as one that "walks at whatever is next door and worth
having, and often somewhere else instead", `medium` as one that "looks three provinces out and
will put a second unit behind a push", and `hard` as one that "looks across half the board,
supports its own attacks and stands over its centres":

| | `easy` | `medium` | `hard` |
| --- | --- | --- | --- |
| how far out it feels a centre worth taking | 1 | 3 | 6 |
| backs another of its units' moves with support | - | yes | yes |
| stands a unit over a centre with support | - | - | yes |
| how often it plays any of its choices at random instead | 35 in 100 | 12 in 100 | never |

Only a centre is worth anything: another power's 8, nobody's 6, its own 3, counted in tens, plus
how near a province lies to the nearest centre it does not hold - at most its sight, so nearness
only ever separates centres worth the same. Each unit without an order, in turn, is offered:
holding, worth where it stands; each move to a province it can reach that none of its other
units is already going to or standing in, 4 off if somebody else's unit is there; backing
another of its units' written move it could reach, worth the destination and 2 more; and
guarding, a support for one of its own units standing on a supply centre, worth that centre
less 5. The best is taken, ties by lot - or any of them at all, with the chance above - and when
every unit has an order it commits.

Beaten out, it takes the way out worth most and disbands only where there is none. In a winter
it builds at the home nearest something worth taking - an army inland; at a coast a fleet if it
has fewer fleets than a third of its units, and always for England; on the north coast at St
Petersburg - and gives up the unit furthest from anything worth taking, one on a centre of its
own last. It never convoys, never supports another power, never writes press and does not read
it. How a machine is seated, what stops it and how `undo` walks its answers back is
[the engine's](../../../README.md#against-the-machine).

## The files

Fourteen files, in the order the project compiles them - the shape
[Turncoats](../Turncoats/README.md#the-files) and
[noughts and crosses](../TicTacToe/README.md#the-files) share.

| File | |
| --- | --- |
| [Powers.fs](Rules/Powers.fs) | the seven powers in seat order, army and fleet, and the three coasts a province can have |
| [Atlas.fs](Rules/Atlas.fs) | seventy-five provinces, the army table and the fleet table with both ends of every border written out, `anyReach` for a walk by any road at all, the map as nineteen strings, and `problems`, every way the three could disagree |
| [Position.fs](Rules/Position.fs) | what stands where and who holds which centre, the opening, and the autumn count |
| [Orders.fs](Rules/Orders.fs) | the seven instructions, and what a season, a retreat and a winter will each take |
| [Adjudicate.fs](Rules/Adjudicate.fs) | a season worked out - strengths, cut supports, convoys, dislodgements, rings and where the beaten may go - and the retreats after it |
| [Session.fs](Rules/Session.fs) | the year: the stages, who is awaited, phases with nobody in them passed through, the count, removals nobody named, and the endings |
| [Turn.fs](Rules/Turn.fs) | `Move` - write, take back, commit, press, resign - and `asked`, which is the seam's `Play` |
| [Words.fs](Rules/Words.fs) | every sentence a player reads, each move as the record writes it, and what another power is told instead |
| [Rival.fs](Rules/Rival.fs) | the three skills and the valuation above |
| [Ink.fs](Reading/Ink.fs) | the eight colour slots, and a power's name painted wherever it is said |
| [Parse.fs](Reading/Parse.fs) | the table above, read |
| [Render.fs](Reading/Render.fs) | every screen as a [`Scene`](../../../README.md#screens) - the board, the record, the three answers, the rules, the waiting room - and the page |
| [Offer.fs](Offer.fs) | the `Playable`: seven seats and no other number, no chance, and the board's faults checked before a deal |
| [Program.fs](Program.fs) | the game as a program of its own |

## Checks

[diplomacy.fsx](../../../tests/diplomacy.fsx) is the suite. It loads
[Europe.fsx](../../../tests/Europe.fsx) - the engine, the table and the wire, then the files
above in the order above - and [Conforms.fsx](../../../tests/Conforms.fsx), and holds the game
to:

- the board: its counts, what an army and a fleet can reach, which provinces have coasts;
- the map: every province drawn and in one piece, every side drawn a border and every border
  drawn, Switzerland the only gap, the walls counted on the drawn board, held centres outlined
  in their holders' colours, seas between tildes in the sea's;
- the adjudicator: a bounce, a supported attack, a cut support, a power's own unit, a
  beleaguered garrison, a head-on, a ring of three, convoys by one fleet and by three, a
  swamped convoy, a paradox, a standoff closing a retreat, two retreats into one province;
- the year: spring to autumn to the next spring, a neutral taken bringing a winter and a build,
  coasts asked for and not, a power going out in a winter, removals measured by sea as well as
  land, a build written twice replacing the first, a power that resigns leaving its units
  standing, and the refusals;
- what each seat is told and shown of orders, winter orders, press, and the open phase's record;
- every kind of line written to a record and read back the same; a table of six turned away;
  seven `medium` machines playing four thousand moves through all three kinds of phase to 1905
  or beyond, every unit somewhere it may stand and nobody over strength, and that game
  replaying to the same board; every view at every kind of phase for every seat, the three
  questions, and every control on the page one the rules take;
- and `Conforms.against diplomacy 7 [ "vie hold"; "done"; "ber hold"; "done" ]`, the contract
  every game answers to.

[counting.fsx](../../../tests/counting.fsx) holds its counts - centres, units, builds and units
owed, a winner's centres - to their nouns at nought and one, and its records in `logs/` are
[taken back up on every CI run](../../../README.md#records).
