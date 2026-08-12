# Diplomacy

The standard board, played by the standard rules. Seven powers, seventy-five provinces,
thirty-four supply centres, and no chance in it anywhere — not a die, not a shuffle, not a
card. Hold eighteen centres and you have won.

```powershell
dotnet run -- diplomacy play 7      # seven seats at this keyboard
dotnet run -- diplomacy serve 7 --rival hard --rival hard --rival hard \
              --rival hard --rival hard --rival hard    # one of you, in a browser
dotnet run -- diplomacy host 7      # seven of you, at your own machines
```

[A year](#a-year) · [The orders](#the-orders) ·
[The map, and why there is no picture of one](#the-map-and-why-there-is-no-picture-of-one) ·
[Points the rules leave to the adjudicator](#points-the-rules-leave-to-the-adjudicator-decided-here) ·
[The machine](#the-machine) · [The files](#the-files)

The third of the three games here, and the engine it runs on is
[one directory up](../../../README.md). Every command that is not about this board - `undo`,
`redo`, `history`, `save`, `notes`, `view`, `restart`, `help`, `quit` - belongs to the engine
and is documented there.

**Seven, and exactly seven.** There is no variant here for a table of five, and there should
not be: the balance of the whole thing is built on all seven home countries being played. A
seat with nobody in it is given to the machine, which is what `--rival` is for — once per seat
you are giving away. Seat one is Austria and seat seven is Turkey, at every game there will
ever be, so a record reads the way anybody who plays this would write it.

**It is here to lean on the seams.** Noughts and crosses tests them by being small; this one
tests them by being unlike everything else the machinery had been asked for. Seven seats. No
chance at all. Orders written in secret by every power at once. A year made of three kinds of
phase, two of them skipped most years. A move that changes nothing on the board and is still
the most important thing anybody does. And a map that cannot honestly be drawn. [What that
turned up](../../../README.md#what-a-third-game-found) is in the engine's README.

## A year

| Phase | What is asked for |
| --- | --- |
| Spring, Autumn | An order for each of your units, then `commit` |
| Retreats | Where each beaten unit goes, or `disband` |
| Winter | Builds or removals, to match your centres |

The centres are counted once a year, after the autumn's retreats. **A phase with nobody in it
is skipped without anybody being stopped to be asked** — most seasons dislodge nobody and most
winters owe nothing, so a quiet year is two phases and not five.

**Everybody writes at once.** The seats come round one at a time, and what any of them has
written stays that power's own business until the last of them has committed. Then the whole
season resolves and everybody sees all of it together. That is not a concession to the
machinery — it is exactly the guarantee that writing orders at one table in one room is
supposed to give.

## The orders

| Order | Written |
| --- | --- |
| Hold | `vie hold` |
| Move | `vie - tri`, and `mao - spa/nc` where a province has two coasts |
| Support a unit where it stands | `bud s vie` |
| Support a move | `bud s vie - tri` |
| Convoy an army over water | `nth c lon - bel` |
| Retreat | `vie - tri`, the same as a move |
| Disband, build | `disband vie`, `build a vie`, `build f stp/sc` |
| Take one back | `cancel vie` |
| Finish | `commit` |

Provinces are named by their first three letters, or by their name with the spaces taken out —
`stp` and `stpetersburg` are the same place. `A vie - tri` works too; the piece is named in
every printed set of these rules and the order does not need it.

**Talking** is `press france <anything>`, read by France and by nobody else, or `press all
<anything>` for the table. Everybody is told that a message went and nobody else is told what
was in it. Nothing in the rules makes anybody keep a promise, and that is the game. The machine
does not read its press.

**Walking away** is `resign`, and it does not end a game of seven because one power left: its
units stand where they are and are taken off the board as they are pushed out, which is what
these rules call civil disorder. Units it owes and does not name are taken furthest from home
first, so a table never waits forever on somebody who has gone.

## The map, and why there is no picture of one

Armies walk the land; fleets sail the water and hug the coast. **They do not travel the same
map**, and neither graph can be worked out from the other: Rome and Venice border, and a fleet
cannot use it. Spain, Bulgaria and St Petersburg have two coastlines each that face different
waters, so a fleet standing there is standing on one of them and a fleet sent there has to say
which — unless only one is reachable from where it is, in which case being asked would be
pedantry rather than a rule.

Turncoats prints [a honeycomb](../Turncoats/README.md#drawn-as-a-map) because its borders are a patch of a triangular
lattice, so the picture *is* the border table and cannot lie. This map is not a lattice. Any
grid of seventy-five provinces would put pairs side by side that share no border and pull apart
pairs that do, and a player would read the picture — there is no other reason to draw one. So
the board shows what is actually true: every supply centre and who holds it, every unit and
where it stands, grouped the way people who play this talk about the map. The borders are one
question away.

```
borders vie      what a piece in Vienna could reach, by land and by sea
where mun        what is standing there, and who holds the centre
```

That answer comes out of the same table the adjudicator walks, so it cannot be out of date and
cannot be wrong. In a browser every unit carries the question as a button beside its orders.

## Points the rules leave to the adjudicator, decided here

- **Convoy or march.** An army ordered somewhere it can walk to is walking, whatever fleets are
  sitting in the water beside it; an army ordered somewhere it cannot is asking to be carried.
  Decided by the map rather than by reading intent into the order, which is what saves this
  from needing a rule about intent.
- **Paradoxes.** A convoy that holds only if it is not attacked and is attacked only if it
  holds is broken by disrupting the convoy — Szykman's rule, which is the one most sets of
  these rules end up at. A ring of units all moving is not a paradox and all of it gets through.
- **Support to a province, not to a coast.** A support names provinces; which coast the
  supported unit lands on is its own business.
- **Retreat as a move.** `vie - tri` in a retreat phase is a retreat, because the parser reads a
  line without being told which phase the game is in and one line should not mean two things.


## The machine

The engine's half of this - when a machine plays, what stops it, and how `undo` walks its
answers back - is [in the main README](../../../README.md#playing-against-the-program). What
follows is this game's half.

**There is nothing to search, and saying so plainly is better than a name that promises
more.** Noughts and crosses can be walked to its end; Turncoats can at least be scored. This
is seven powers writing in secret and a resolution nobody can see coming, and the part of it
that actually decides games happens in conversation between the people playing - which a
machine at this table is not having.

So what is here plays the board and nothing else: it wants centres, it wants them near, it
will hold what it has, and the better ones will put a second unit behind a push rather than
send it somewhere on its own.

| | `easy` | `medium` | `hard` |
| --- | --- | --- | --- |
| how far out it will look for something worth taking | 1 | 3 | 6 |
| will spend a unit backing another unit's move | — | yes | yes |
| will stand a unit in front of a centre it holds | — | — | yes |
| how often it plays something other than its own advice | 35% | 12% | never |

A centre somebody else holds is worth most, a neutral one nearly as much, one of its own is
worth holding on to, and everywhere else on the map is worth nothing at all and is only ever a
road to somewhere. That is the whole valuation, and it is in
[Rival.fs](Rules/Rival.fs).

**It does not read its press.** A machine at a seat is sent messages like anybody else and
does nothing with them, which is worth knowing before you spend a season promising it things.
## The files

Thirteen files and about fourteen hundred lines, in the same two folders the other two games
use — [Turncoats](../Turncoats/README.md#the-files) has twenty-one and
[noughts and crosses](../TicTacToe/README.md#the-files) has ten.

| File | Role |
| --- | --- |
| [Powers.fs](Rules/Powers.fs) | The seven, army and fleet, and which coastline of a province that has two |
| [Atlas.fs](Rules/Atlas.fs) | The board: seventy-five provinces and the two graphs over them, both ends of every border written out, and the checks that they agree |
| [Position.fs](Rules/Position.fs) | What is standing where, who owns which centres, and the opening |
| [Orders.fs](Rules/Orders.fs) | The eight orders, and what each phase will take |
| [Adjudicate.fs](Rules/Adjudicate.fs) | What happened when everybody moved at once: strengths, cut supports, dislodgement, convoys, and the two rules for a cycle that answers differently depending on what it is told about itself |
| [Session.fs](Rules/Session.fs) | The year: three kinds of phase, the ones with nobody in them walked straight through, and the centres counted once |
| [Turn.fs](Rules/Turn.fs) | `Move` - write an order, take one back, commit, send a word, walk away - and the total `Play` |
| [Words.fs](Rules/Words.fs) | Every string a player reads, including which sentence a seat gets for an order it did not write |
| [Rival.fs](Rules/Rival.fs) | A seat played by the program: it wants centres, it wants them near, and the better ones put a second unit behind a push |
| [Ink.fs](Reading/Ink.fs) | Seven colours, against the other games' four and two |
| [Parse.fs](Reading/Parse.fs) | The words every printed set of these rules uses, and the other half of the bargain `Words.order` writes |
| [Render.fs](Reading/Render.fs) | Every screen described once as a [`Scene`](../../../README.md#a-screen-described-once), and the argument for drawing no map |
| [Offer.fs](Offer.fs) | Both seams filled in |
