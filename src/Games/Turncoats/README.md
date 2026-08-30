# Turncoats

Two to five players draw bags of coloured stones and place them into fourteen regions. Nobody
commands a faction — the stones are red, blue and green whoever holds them — so the game is
settled twice over: the faction ruling the most land carries the board, and the player left
holding most of that colour carries the faction. Every bag is held closed, and so is the
reserve, so nobody sees the whole game.

```powershell
dotnet run -- turncoats play 3               # or `dotnet run -- play 3`: a line naming no game means this one
dotnet run -- turncoats play 2 --rival hard
dotnet run -- turncoats serve 3              # the same game, in a browser
```

Everything that is not this game — the command line, the menu, the record and taking a game up
again, the three views, tables and houses — is the engine's, [one directory up](../../../README.md).

## The rules

**The deal.** There are 21 stones of each colour, 63 in all. Each home takes two stones of its
own colour, each of the eight wild regions draws two at random from what is left, and then each
player draws a bag of eight. What remains is the reserve: 25 stones with two players, 1 with
five. The Flag, the Axe and the dead region start empty.

**The map.** Fourteen regions, numbered 1 to 14. Twelve are land: the three homes — Nightfen
(green), Emberfall (red) and Tidewatch (blue) — eight wild regions, and The Hollow Waste, dead
ground that no stone may ever enter though it borders six regions. The Flag and the Axe stand
apart from the map and border nothing. The borders are drawn under [The board](#the-board).

**A turn** is one of four actions. There is no passing.

| | what happens | refused when |
| --- | --- | --- |
| **Recruit** | A stone from the bag goes into any region but the dead one — the Flag and the Axe included. | the stone is not in the bag; the region is dead ground |
| **Battle** | A stone from the bag goes into the Axe. In the region named, up to as many stones of *other* colours as it already holds of that colour are driven out, back to the reserve. | the region is dead, the Flag or the Axe; it holds no stone of that colour, or nothing of another; a colour named is the battle's own, is not standing there, or more are named than may go; the stone is not in the bag |
| **March** | A stone from the bag goes into the Flag. One or more stones of that colour then move from the region named into one region bordering it. | the region is dead, the Flag or the Axe; the destination is dead ground, or shares no border with it; fewer stones of that colour stand there than are to move; the stone is not in the bag |
| **Negotiate** | A stone is drawn from the reserve into the bag. Then one stone from the bag goes back to the reserve — the one just drawn, if you like. | the bag is empty; the reserve is empty |

A battle told no colours drives out all it may. Where that would take a real choice — more
stones on offer than may go, of more than one colour — the game says what stands there and asks
which, rather than guessing. A battle must drive out at least one stone. Since the Flag and the
Axe border nothing, nothing can ever march into them.

A negotiation is two steps, because the draw is random and the player should see it before
choosing what to hand back. Between the two nothing is accepted but the return and `resign`. A
stone always goes back, so a negotiation never changes the size of a bag; every other action
plays one, so a bag only ever shrinks.

**Ruling a region.** A region is ruled by the colour with most stones in it; failing that, most
stones in the Axe; failing that, most in the Flag; failing that, it is tied and has no ruler.
Only colours standing in the region contend, so an empty region is unclaimed whatever the Axe
holds, and each measure only narrows the field the one before it left, so a colour behind on
stones cannot win the region on the Axe. The cascade is
[`Tiebreak.run`](../../../src/Common/Tiebreak.fs), which the two below share.

**The end.** The game ends the moment every player in turn has taken a turn without playing a
stone. A negotiation adds one to the run; a recruit, a battle or a march sets it back to nought;
a player whose bag is empty has their turn skipped, and the skip counts as a negotiation — so
once every bag is empty the run fills in one lap of the table. The board says which it was:
*every player has negotiated in turn*, or *every player has played out their bag*. `resign` ends
it at once, and is written down as *the players walked away*.

**The faction that carries the board** rules the most land, the Flag and the Axe not being
land; failing that, holds most stones in the Axe; failing that, most in the Flag; failing that,
the game is a draw. All three colours contend, ruling something or not.

**The player who carries the faction** holds the most stones of its colour still in the bag;
failing that, the fewest stones of the other two colours; failing that, would take the next turn
— which no two players are equally close to, so it always settles. But if every bag has been
played out nobody wins, and the game is drawn even though a faction carried the board.

## The words

Once a game is dealt, everything is typed at the prompt. These are the game's own words; `undo`,
`history`, `save`, `view`, `restart`, `players`, `resign`, `help`, `quit` and the rest are the
table's, and are [listed there](../../../README.md#at-the-prompt).

| | |
| --- | --- |
| `recruit <colour> <region>` | or `r` |
| `battle <colour> <region> [colours...]` | or `b`; name no colours to drive out all you may, or the colours to drive out, one stone each. `none` is read, and refused |
| `march <colour> <from> <to> [count]` | or `m`; the count is 1 unless said |
| `negotiate`, then `return <colour>` | or `n`; the return is asked for until it comes |
| `rule <region>` | not a move: shows who rules the region and why |

Colours are `r`/`red`, `b`/`blue` and `g`/`green`; regions are their numbers on the map. So
`battle green 2 blue` puts a green stone in the Axe and drives one blue stone out of Saltmarsh,
and `march blue 8 5 2` puts a blue stone in the Flag and moves two blue stones from The
Crossroads into Emberfall. `k` and `black` still read as green, because the earliest records
wrote the third colour that way and a record is meant to replay for good. A line that is none of
these is answered with the four in short — `r b 5`, `b r 8`, `m g 8 5 2`, `n` — and `help` has
them at length.

The record keeps each move as it would be typed: `battle r 8` for a battle that drove out all it
could and `battle r 8 b g` for one that named its casualties, `march g 8 5 2` with the count
always written, `return r`, `resign`.

## The board

Every screen is drawn for one seat and headed by where the game stands — *Turn 4 - Player 1 to
play*; *Turn 4 - Player 1 drew a Green stone and must hand one back*; *Game over after 12 turns -
every player has negotiated in turn* — and shows the same blocks in every view:

| | |
| --- | --- |
| The map | the twelve regions of land, with what stands in each and who rules it |
| Standing apart | the Flag and the Axe, the same way |
| Players | every seat, its bag as you may see it, and the run of negotiations against the number that ends the game |
| Land ruled | how many of the eleven regions that can be held each colour rules, how many are tied, how many unclaimed |
| Supply | what is on the board, in the reserve, and out of sight |
| Result | once the game is over, both cascades with their working |
| Commands, Log | the box of what can be typed, and what the game has been saying; `commands` and `log` turn them |

The map is the border table laid out. Each region is a hex, each row sits half a region across
from the one above, and **a shared side is a border**: two regions that meet only at a point
share none. Seed 42, three players, after `battle b 4 g`, `march r 3 4` and `recruit g 9`:

```
THE MAP
                      _/________/ \________\_/________/ \________\_
                      | [ 2] Saltmarsh      | [ 1] Nightfen (G)   |
                      | B B              >B | G G              >G |
           _/________/ \________\_/________/ \________\_/________/
           | [ 3] Greymarket     | [ 4] Thornwood      |
           | R                >R | R B              >B |
_/________/ \________\_/________/ \________\_/________/ \________\_
| [ 5] Emberfall (R)  | [ 6] Hollow Waste   | [ 7] Stonecradle    |
| R R              >R | dead                | R B              >B |
 \________\_/________/ \________\_/________/ \________\_/________/ \________\_
           | [ 8] The Crossroads | [ 9] Windgap        | [10] Tidewatch (B)  |
           | R G              >R | B G G            >G | B B              >B |
            \________\_/________/ \________\_/________/ \________\_/________/
                      | [11] Ironford       | [12] Dunmoor        |
                      | B B              >B | B G              >B |
                       \________\_/________/ \________\_/________/

STANDING APART
__________/ \__________   __________/ \__________
| [13] The Flag       |   | [14] The Axe        |
| R                >R |   | B                >B |
__________\_/__________   __________\_/__________
```

A home has its colour after its name, `>B` marks the colour ruling a region, `=BG` the colours
level in it, and the dead region says so where its stones would be. The layout is
`Board.layout` and the borders `Board.declaredBorders`, and `Board.problems` walks one against
the other before a game is dealt — every land region laid out once, the Flag and the Axe not at
all, no shared side without a border and no border without a shared side — so a map that lies
stops the game rather than being drawn. It is what the seam's `Faults` reports.

`rule <region>` shows the working behind a `>`:

```
> rule 4
Thornwood holds 1 Red and 1 Blue.
  stones in the region: Red 1, Blue 1 -> Red, Blue still level
  stones in the Axe: Red 0, Blue 1 -> Blue leads
  Blue rules the region.
```

The three views are written by hand — [Render.fs](Reading/Render.fs) is `plain`,
[Rich.fs](Reading/Rich.fs) is `rich` and [Html.fs](Reading/Html.fs) is `html` — rather than
drawn from a `Scene`, for the honeycomb's sake ([screens](../../../README.md#screens)). They say
the same things in the same words and differ in what each medium allows. `rich` draws each region
as a panel bordered in its ruler's colour, land ruled as a bar chart and what is out of sight as
a breakdown. The page puts the moves under the regions: `R` `B` `G` recruit a stone of that
colour into it and `?` asks `rule`; where stones stand, `×R` battles with a Red one and drives
out all it may, and `R→8` marches one Red stone into 8. A *This turn* row offers `negotiate`, or
`return Red`, `return Blue` and `return Green` while a stone is owed. Anything more — a battle
naming its casualties, a march of more than one stone — is typed.

`notes` turns the explanations under the blocks: the map, the Flag and the Axe, what counts as
land, what is closed to you, and on the page what its controls do. There is no clock and no key
to press — every move is a typed line — and the game makes no sound of its own. Its four colour
slots on the video page are `red`, `blue`, `green` and `hidden`, the last for closed bags and
dead ground, standing crimson, azure, moss and slate ([colours](../../../README.md#colours)).

**What a seat may see** is decided in `Knowledge.seenBy` ([Knowledge.fs](Rules/Knowledge.fs))
and in the seam's `SeenBy`, never in a view:

| | |
| --- | --- |
| the map, the Flag and the Axe | every stone |
| your own bag | every stone |
| every other bag | how many stones, never which — `closed (7)`, or a `?` a stone |
| the reserve | how many stones, never which |
| out of sight | which colours are out there, exactly, and never where |

Every stone is somewhere, so whatever is neither on the map nor in your bag is in the reserve or
in somebody else's, and its colours follow: a player can count what is still to come without
being told where it is. Two things said at the table would give a closed bag away, and both are
worded round: the stone drawn in a negotiation is named to the player who drew it and is *a
stone* to everyone else — handing one back is public, since it lands in the reserve in front of
everybody — and *Settle the negotiation first* leaves the colour out, because it stays on the
screen after the turn has moved on. The journal and the record keep the whole truth; the masking
is the view's, and once the game is over `Knowledge.laidBare` opens every bag and the reserve.

`history` is the record so far: each line as it was typed, what the game said back, and how far
from the deal the game stands.

## The machine

There is one machine, and `easy`, `medium` and `hard` are three sets of numbers at the foot of
[Rival.fs](Rules/Rival.fs). How a machine is seated, when it plays and how `undo` takes its
answers back is the engine's ([against the machine](../../../README.md#against-the-machine));
what it weighs is this game's.

| | `easy` | `medium` | `hard` |
| --- | --- | --- | --- |
| | plays anything the rules allow | plays the best move it can see, and now and again does not | counts the tie-breakers too, and what you could do about it |
| land ruled | 10 | 10 | 10 |
| standing in a region, short of ruling it | 1 | 1 | 1 |
| stones in the Axe, in the Flag | — | — | 4, 3 |
| its own colour still in the bag | 12 | 12 | 12 |
| every other stone in the bag | −1 | −1 | −1 |
| turns in 100 it plays anything legal instead | 100 | 15 | 0 |
| moves it looks a reply ahead on | — | — | 5 |

It backs the colour its bag holds most of. Land, standing, the Axe and the Flag are each a lead
— its colour's count less the better of the other two — so a colour is only winning relative to
its rivals; the two bag lines are plain counts. *Standing in a region* is that lead in stones, region by region across
the land, clamped to two either way, so that piling stones into a region already ruled is worth
nothing more. *Its own colour in the bag* is set above a region because the game is settled a
second time by who is left holding the winning colour: weighed low, a machine empties its bag
onto the map and draws.

Every move the rules would take is tried and the position it leaves weighed: every recruit,
every battle with every set of casualties it could name, every march of every count. A
negotiation is weighed as what it hopes for — the bag with one stone of another colour swapped
for one of its own — and no reply is looked for against it. A stone kept weighs more than a
region ruled, so it plays a stone only where a battle or a march is worth more than holding it
and negotiates otherwise: two `hard` machines left to each other play a battle each and
negotiate the game shut in 3 or 4 turns. When it owes a stone it weighs each colour it could
hand back the same way.

`hard` keeps its five best and rates each by the worst the next player could leave it. It
cannot see that player's bag, so it takes it to be everything out of sight — `Knowledge.Unseen`,
the same pile a person can count — which is pessimistic and honest: it never plays better for
knowing something it was not told. Among moves that weigh the same it draws lots from its own
generator, so the same deal against the same machines plays the same game twice; `easy` draws
lots among everything legal every turn, and `medium` does so 15 turns in 100.

## The files

`Rules/` is how it is played and knows nothing of screens or English; `Reading/` is how it is
read; [Offer.fs](Offer.fs) joins them as the one `Playable` the table sees
([the seam](../../../README.md#the-seam)). In the order they compile:

| | |
| --- | --- |
| [Rules/Stones.fs](Rules/Stones.fs) | `StoneColour`, and `Pile`, stones counted by colour |
| [Rules/Board.fs](Rules/Board.fs) | the fourteen regions, the borders, the layout, and the checks that hold them to each other |
| [Rules/Players.fs](Rules/Players.fs) | a `Player` with a bag, and the `Table` of 2 to 5 with one of them to act |
| [Rules/Position.fs](Rules/Position.fs) | which stones stand in which region |
| [Rules/Ruling.fs](Rules/Ruling.fs) | who rules a region, and how the land stands |
| [Rules/Game.fs](Rules/Game.fs) | the position, the table, the reserve and the generator, and what can be asked of them |
| [Rules/Knowledge.fs](Rules/Knowledge.fs) | what one seat sees of a game: `Open` piles, `Closed` counts, and what is out of sight |
| [Rules/Events.fs](Rules/Events.fs) | what happened, how a game ends, and why a move was refused |
| [Rules/Actions.fs](Rules/Actions.fs) | the four actions on a `Game`, each the game after and what happened, or a `Rejection` |
| [Rules/Outcome.fs](Rules/Outcome.fs) | the two cascades: which faction carries the board, and which player the faction |
| [Rules/Setup.fs](Rules/Setup.fs) | the deal, from a seed |
| [Rules/Turn.fs](Rules/Turn.fs) | `Move`, the phase, the run of negotiations, and how a turn is handed on and a game ends |
| [Rules/Playing.fs](Rules/Playing.fs) | the seven answers the engine asks for, as `Rules` |
| [Rules/Words.fs](Rules/Words.fs) | every string a player reads, and what each seat is told |
| [Rules/Rival.fs](Rules/Rival.fs) | the machine: how a position is weighed, and the three skills |
| [Reading/Ink.fs](Reading/Ink.fs) | the colour slots, and how a drawn board is painted |
| [Reading/Parse.fs](Reading/Parse.fs) | a typed line as a `Move`, or `rule` as a question |
| [Reading/Render.fs](Reading/Render.fs) | the `plain` view: the honeycomb, the notes, the help, and the words the other two build on |
| [Reading/Rich.fs](Reading/Rich.fs) | the `rich` view, in Spectre's panels, tables and charts |
| [Reading/Html.fs](Reading/Html.fs) | the `html` view, with the moves as controls under the regions, and the page's stylesheet |
| [Offer.fs](Offer.fs) | the `Playable`: both halves, the three views, and the one way this game is played |
| [Program.fs](Program.fs) | the door: this game as a program of its own |

## Checks

[turncoats.fsx](../../../tests/turncoats.fsx) is the contract, and the rest hold the game
itself. Each loads [Harness.fsx](../../../tests/Harness.fsx), the rules alone, or
[Whole.fsx](../../../tests/Whole.fsx), the game on the whole stack.

| | |
| --- | --- |
| [turncoats.fsx](../../../tests/turncoats.fsx) | `Conforms.against`, everything the table expects of a `Playable`, over a game of two resigned on its first turn — where a turn count built by hand reads wrong |
| [actions.fsx](../../../tests/actions.fsx) | the map hangs together; each of the four actions does what it says, refuses what it must, and keeps all 63 stones |
| [ruling.fsx](../../../tests/ruling.fsx) | who rules a region, tie by tie: the Axe, the Flag, level throughout, and a colour out early never coming back |
| [outcome.fsx](../../../tests/outcome.fsx) | both cascades: land, the Axe, the Flag, a draw; the winning stones, the losing ones, who acts next, every bag played out |
| [knowledge.fsx](../../../tests/knowledge.fsx) | a seat sees its own bag, the sizes of the others and the reserve, and an out-of-sight pile that comes to exactly what is held back |
| [history.fsx](../../../tests/history.fsx) | undo and redo exact, and time travel rather than a re-roll; a refusal written down and changing nothing; a record that replays state for state, survives the file, seats the machines back down and still reads `k` |
| [view.fsx](../../../tests/view.fsx) | no view shows a seat another's bag or names the drawn stone to anyone but the drawer; the three have the same blocks and notes; colours change by the words a person types, and `plain` stays plain |
| [html.fsx](../../../tests/html.fsx) | the page has its places and carries its client, and the board offers exactly the recruits, battles, marches and returns the position allows, and nothing for what is not there |
| [solo.fsx](../../../tests/solo.fsx) | the table at one keyboard, played on this game: the margins, a machine answering before the prompt returns, undo taking its answer back, and the record written on `save`, `quit`, `restart` and the end |
| [properties.fsx](../../../tests/properties.fsx) | over games FsCheck deals and plays itself: every colour keeps its 21 stones, a refused move changes nothing, a ruler holds as many stones as anyone there, a seat sees no more than it should, a record read back is the same game |
| [rival.fsx](../../../tests/rival.fsx) | machines play a game out with nothing refused, in lines the prompt reads back, the same twice from one seed; a machine plays the same move however the bags it cannot see are shuffled; over twelve deals each way `hard` beats `easy`, `medium`, and a machine that sits on its stones |
| [counting.fsx](../../../tests/counting.fsx) | every count in the game agreeing with its noun |
