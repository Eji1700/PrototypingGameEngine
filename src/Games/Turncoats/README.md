# Turncoats

The game this program was built for, and the first of those in it. The engine it runs
on, and everything that is not this game — the record, the seats, the wire, the screens, the
command line — is [one directory up](../../../README.md).

Two to five players draw bags of coloured stones and place them into fourteen regions. Nobody
commands a faction - the stones are red, blue and green whoever holds them - so the game is
settled twice over: the faction ruling the most land carries the board, and the player left
holding most of that colour carries the faction. A bag played out to nothing wins nothing.

```powershell
dotnet run -- turncoats play 3        # or just `dotnet run -- play 3`: a line that
                                      #   names no game means this one
dotnet run -- turncoats play 3 --seed 42
dotnet run -- turncoats play 2 --rival medium
dotnet run -- turncoats serve 3       # the same game, in a browser

dotnet run -- turncoats replay logs/...-turncoats-3p-seed<n>.log    # one you put down
dotnet run -- turncoats host --from logs/...-turncoats-3p-seed<n>.log
```

**Put it down and come back.** `quit` writes the record and leaves the board exactly as it
stands, and `Continue a game` at the menu lists what there is to take up — with the same
factions in the same bags and the machine back at the seats it was playing, at the strength
it was playing them. Conceding is `resign`, which is a different thing said on purpose. It
need not be taken up the way it was put down either: `--from` works on `play`, `serve` and
`host` alike, so a game started against two machines here can be reopened as a table your
friends join. All of that is the engine's rather than this game's, and is [documented
there](../../../README.md#taking-it-back-and-writing-it-down).

[Playing](#playing) · [Rules as implemented](#rules-as-implemented) · [The map](#the-map) ·
[The four actions](#the-four-actions) · [Ruling](#ruling) · [Winning](#winning) ·
[Ending the game](#ending-the-game) · [Who knows what](#who-knows-what) ·
[The machine](#the-machine) · [The files](#the-files)

## Playing

Every command that is not about stones - `undo`, `redo`, `history`, `save`, `notes`,
`commands`, `log`, `view`, `restart`, `players`, `help`, `quit` - belongs to the engine and is
[documented there](../../../README.md). What follows is what this game adds.

Once a game is dealt, everything else is typed at the prompt:

| command | action |
| --- | --- |
| `recruit <colour> <region>` (`r`) | [Recruit](#the-four-actions) |
| `battle <colour> <region> [colours...]` (`b`) | [Battle](#the-four-actions); name no colours to drive out all you may |
| `march <colour> <from> <to> [count]` (`m`) | [March](#the-four-actions); count defaults to 1 |
| `negotiate` (`n`), then `return <colour>` | [Negotiate](#the-four-actions) |
| `rule <region>` | not an action; shows [who rules a region](#ruling) and why |

Colours are `r`/`red`, `b`/`blue`, `g`/`green`; regions are numbered as shown on
the board. So `battle green 2 blue` places a green stone in the Axe and drives one
blue stone out of Saltmarsh, and `march blue 8 5 2` places a blue stone in the Flag
and moves two blue stones from the Crossroads into Emberfall.

Every random decision at this game comes from the seed — the deal, the bags, the stone a
negotiation draws — so a seed and a list of messages reproduce a game exactly, and `restart`
draws its next seed from the generator already in play rather than off the clock. Why the
generator is [a value rather than `System.Random`](../../../README.md) is the engine's story
and is told there.

## Rules as implemented

- 21 stones of each colour: red, blue, green (63 in total).
- 14 regions: one home per colour, eight wild regions, two special regions and
  one dead region.
- Each home starts with two stones of its own colour; each wild region draws two
  stones at random from what is left. The special regions (**The Flag** and
  **The Axe**) start empty and border nothing. The dead region starts empty and
  nothing may ever enter it, but it still sits on the map for adjacency.
- 2 to 5 players. Each draws a bag of eight stones at random; a player commands no
  faction, so a bag holds stones of any colour. Undealt stones sit in the reserve
  (25 with two players, 1 with five).
- On a turn a player takes one of [the four actions](#the-four-actions). There is no
  passing.

Points the rules did not settle, decided here and easy to change:

- **Setup exclusions** — "every other region gets 2 stones at random" is read as
  covering the wild regions only: the dead region is excluded because nothing may
  enter it, and the special regions are excluded because they start empty by rule.
- **Region count** — the Flag and the Axe are additions to the original twelve, so
  the board holds fourteen regions: 3 home + 8 wild + 1 dead makes the twelve, plus
  the two specials.

## The map

Borders are declared once in `Board.declaredBorders` and symmetrised, so a border
only has to be named from one end. The resulting graph has 23 edges, is connected
across all twelve mainland regions, and leaves the Flag and the Axe bordering
nothing.

| region | borders |
| --- | --- |
| 1 Nightfen (Green home) | 2, 4 |
| 2 Saltmarsh | 1, 3, 4 |
| 3 Greymarket | 2, 4, 5, 6 |
| 4 Thornwood | 1, 2, 3, 6, 7 |
| 5 Emberfall (Red home) | 3, 6, 8 |
| 6 The Hollow Waste (dead) | 3, 4, 5, 7, 8, 9 |
| 7 Stonecradle | 4, 6, 9, 10 |
| 8 The Crossroads | 5, 6, 9, 11 |
| 9 Windgap | 6, 7, 8, 10, 11, 12 |
| 10 Tidewatch (Blue home) | 7, 9, 12 |
| 11 Ironford | 8, 9, 12 |
| 12 Dunmoor | 9, 10, 11 |
| 13 The Flag, 14 The Axe | none |

Regions are numbered across the map rather than by kind, so that neighbours read as
neighbours: no border joins regions more than three apart, and all but three of the
numbers border the one after them. The mainland takes 1 to 12, with the dead region
at its centre; the Flag and the Axe, which are no part of the map, come last.

### Drawn as a map

That border graph is a patch of a triangular lattice, so it can be drawn as the map
it is rather than listed. `Board.layout` says where each region lies — rows north to
south, each row half a region across from the one above — and the board is drawn as a
honeycomb:

```
                      _/________/ \________\_/________/ \________\_
                      | [ 2] Saltmarsh      | [ 1] Nightfen (G)   |
                      | B B              >B | G G              >G |
           _/________/ \________\_/________/ \________\_/________/
           | [ 3] Greymarket     | [ 4] Thornwood      |
           | R R              >R | B G             =BG |
_/________/ \________\_/________/ \________\_/________/ \________\_
| [ 5] Emberfall (R)  | [ 6] Hollow Waste   | [ 7] Stonecradle    |
| R R              >R | dead                | R B             =RB |
 \________\_/________/ \________\_/________/ \________\_/________/ \________\_
           | [ 8] The Crossroads | [ 9] Windgap        | [10] Tidewatch (B)  |
           | R G             =RG | B G             =BG | B B              >B |
            \________\_/________/ \________\_/________/ \________\_/________/
                      | [11] Ironford       | [12] Dunmoor        |
                      | B B              >B | B G             =BG |
                       \________\_/________/ \________\_/________/
```

Every region is a hex two half-columns wide, upright either side and coming to a
point above and below, and each row is laid half a region across from the one above.
So a region has six neighbours — two beside it and two along each of its sloping
sides — which is exactly the most any region on this map has. **A shared side is a
border, and regions that meet only at a point share none.** No border is drawn as a
line into open ground, and none can be drawn wrong: the picture is the border table,
laid out.

`Board.problems` checks that it is, before a game is ever dealt. Alongside the
older checks — ids on the board, no self-borders, isolated regions bordering
nothing, every other region reachable — it now walks the layout and the borders
against each other: every mainland region laid out exactly once, the Flag and the
Axe laid out nowhere, no border without a shared side, and no shared side without a
border. A layout that drifts from the table stops the game rather than drawing a
map that lies. [actions.fsx](../../../tests/actions.fsx) checks the same list is empty.

The Flag and the Axe are drawn below the map in the same hand, standing clear of it
and of each other — sharing no side with anything, they border nothing.

No two homes border each other, and every home is three steps from every other,
whether or not the dead region is passable.

Rules that use adjacency can be written against `Board.areAdjacent` and
`Board.neighbours`.

## The four actions

**Recruit** — place any stone from the bag into any region but the dead one. The
Flag and the Axe are legal targets, since the rule excludes only the dead region.

**Battle** — place any stone from the bag into the Axe and name another region
(not dead, not the Flag or the Axe). Count the stones there matching the colour
just placed; up to that many stones *of other colours* are driven out of that
region and back to the reserve.

A battle must be a real fight, so the target must hold at least one stone of the
attacking colour and at least one stone of another colour, and at least one stone
must be driven out. Naming no colours drives out everything the rule allows. Where
that would take a genuine choice — more stones on offer than removals, spread
across more than one colour — the game asks which instead of guessing.

**March** — place any stone from the bag into the Flag and name another region
(not dead, not the Flag or the Axe). One or more stones there of the matching
colour then move into a single region bordering it, which must not be the dead
region. The source must hold at least one stone of that colour and at least one
must move. Since the Flag and the Axe border nothing, they can never be marched
into.

**Negotiate** — only open to a player holding at least one stone. Draw a stone from
the reserve at random into the bag, then hand any one stone from the bag back to
the reserve, which may be the stone just drawn. A stone always goes back, so a
negotiation trades one stone for another and never changes the size of a bag.

Readings the rules left open, all easy to change:

- **"the main bag"** in Battle is taken to be the reserve, the same pool Negotiate
  draws from.
- **Battle counts matching stones in the named region**, not in the Axe. The stone
  placed in the Axe stays there and does not count towards its own total.
- **A march moves its whole group into one destination**, rather than splitting it
  across several neighbours.
- **Negotiate is two steps.** The draw is random, so the player cannot sensibly
  choose what to hand back before seeing it. `Actions.negotiate` draws and leaves
  the turn in `AwaitingReturn`; `Actions.settle` then ends it. No other action is
  accepted in between, and the turn cannot end without a stone going back.

## Ruling

A region is ruled by the colour holding the most stones in it. Ties cascade through
two further measures, and each one only narrows the field left by the one before —
a colour knocked out never comes back:

1. most stones in the region;
2. failing that, most stones in the Axe;
3. failing that, most stones in the Flag;
4. failing that, the region is tied and has no ruler.

So a colour trailing on stones cannot win a region on the strength of the Axe, even
if it holds the Axe outright. `Ruling.decide` returns `RuledBy`, `Contested` (level
after every measure) or `Unclaimed`.

An empty region is `Unclaimed`: only colours actually present contend, so a loaded
Axe does not hand out the regions nobody has entered. That is a reading, not a
stated rule — counting absent colours as tied on zero would instead give every
empty region to whoever leads the Axe.

Ruling is computed on demand from the position, never stored, so it cannot fall out
of step with the board. `rule <region>` shows the working:

```
> rule 2
Saltmarsh holds 1 Blue and 1 Green.
  stones in the region: Blue 1, Green 1 -> Blue, Green still level
  stones in the Axe: Blue 0, Green 1 -> Green leads
  Green rules the region.
```

## Winning

Two cascades run when the game ends, both shaped exactly like ruling a region — see
`Tiebreak.run`, which all three share.

**The faction that carries the board:**

1. rules the most land;
2. failing that, the most stones in the Axe;
3. failing that, the most stones in the Flag;
4. failing that, the game is a draw.

The Flag and the Axe are ruled like anywhere else, but they are manoeuvres bought
with stones rather than ground held, so they count for nothing in the first
measure — only in the tie-breakers they *are*. That leaves twelve regions of land:
three homes, eight wilds and the dead region, which nobody can ever hold.
`RegionKind.isLand` draws that line.

Every faction contends, including one ruling nothing. If no faction rules a single
region they are level on nought and the Axe decides, rather than the game being
drawn out of hand.

**The player who carries that faction:**

1. if every player has played out their bag, nobody wins and the game is a draw;
2. otherwise, the most stones of the winning faction's colour still in the bag;
3. failing that, the fewest stones of the losing factions;
4. failing that, whoever would take the next turn.

The last measure always separates them, since no two players sit the same distance
from the next turn — so a game with a winning faction and any stones left always
has exactly one winning player.

One reading to confirm: when every bag is empty the whole game is a draw, following
"no one wins, the entire game is a draw" — even though a faction did carry the
board.

## Ending the game

The game ends once every player, in a row, has taken a turn without playing a
stone. `Play.Negotiations` counts that run: negotiating adds to it, recruiting,
battling or marching resets it to zero, and the game is over the moment it reaches
the number of players.

A player whose bag is empty has their turn skipped, and the skip counts towards the
run exactly as a negotiation does. So in a two-player game, one player being
skipped and the other negotiating ends it. If every bag is empty, every turn is a
skip and the run fills in one lap of the table — which is the "all players have
played out their stones" ending, arrived at by the same counter rather than a
separate check. The end is reported either way: *every player has negotiated in turn*,
or *every player has played out their bag*.

Since a negotiation never grows a bag and only a stone in hand allows one, every
bag shrinks monotonically, so a game always winds down.

## Who knows what

A bag is held closed and so is the reserve, so nobody sees the whole game. Every
screen is drawn for one seat, and `Knowledge.seenBy`
([Knowledge.fs](Rules/Knowledge.fs)) is what that seat is shown:

| | what they see |
| --- | --- |
| their own bag | every stone, colour by colour |
| the map | every stone, colour by colour — it is open to everyone |
| every other bag | how many stones, never which |
| the reserve | how many stones, never which |
| out of sight | which colours are out there, never where |

That last line is the one worth having. Every stone is somewhere, so whatever is
neither on the map nor in the beholder's own bag must be in the reserve or in
somebody's bag, and its colours follow exactly: `Unseen` is the whole game less
the map less what the beholder holds. A player can count what is still to come
without being told where any of it is.

A `Sight` is either `Open` of a pile or `Closed` of a count, so a closed bag has
no colours to leak by accident — there is nothing in the value to read. Once the
game is over `Knowledge.laidBare` opens everything, because there is no longer
anything to hold back.

Two things a player is told would otherwise give a closed bag away, and both are
worded around in [Words.fs](Rules/Words.fs):

- **A stone drawn from the reserve** goes straight into a closed bag. The player
  who drew it is told its colour; everyone else is told only that a stone was
  drawn. Handing one back *is* public — the stone lands in the reserve in front
  of everybody — so between the two, the table learns what left a bag and never
  what entered it.
- **"Settle the negotiation first"** names the stone just drawn, and it stays on
  screen after the turn has moved on, so the colour is left out of it. Only the
  player who drew can be refused this way, and the heading tells them the colour
  regardless.

Every other refusal stays public and unabridged. Asking is part of what happened
at the table: if a player calls for a red stone and cannot produce one, the table
saw that, and the record says so.

The journal keeps the whole truth, and so does the saved record — masking is a
property of the view, not of what is stored. `Render` reads the journal through
`Words.noticeSeenBy`; `Transcript` writes it through `Words.notice`. So typing
`save` mid-game does put a full account on disk, where the file is out of the
game's hands anyway.

## The machine

The engine's half of this - when a machine plays, what stops it, how it is fenced in, and how
`undo` walks its answers back - is
[in the main README](../../../README.md#playing-against-the-program). What follows is this
game's half: what its machine actually weighs.

### Three sets of numbers, not three machines

There is one machine. What `easy`, `medium` and `hard` name is a set of weights and
two knobs, all at the foot of [Rival.fs](Rules/Rival.fs):

| | `easy` | `medium` | `hard` |
| --- | --- | --- | --- |
| land ruled by the faction it is backing | 10 | 10 | 10 |
| standing inside a region, short of ruling it | 1 | 1 | 1 |
| the Axe, and the Flag | — | — | 4, 3 |
| its own faction's stones still in the bag | 12 | 12 | 12 |
| the other two's, which count against it | −1 | −1 | −1 |
| how often it plays anything legal instead | always | 15% | never |
| how many of its own moves it checks a reply to | — | — | 5 |

`easy` throws its judgement away every turn, so its column is there for the shape of the
thing rather than because it is ever read.

The last line is the only one that involves looking past its own turn, and it runs into the
same wall as everything else: it cannot see the next player's bag, so it does not guess at
one. It assumes the worst instead - that the seat about to act holds every stone that is
neither on the map nor in its own bag, which is exactly what `Knowledge.Unseen` says is out
there somewhere. Pessimistic, and honest. It never plays better for knowing something it was
not told, only more carefully.

The weights are the game's own winning conditions with a number against each, so
there is no strategy written down anywhere: there is a statement of what winning is,
which the rules already say, and how much a machine cares about each part of it.
Adding a fourth way of playing is a fourth entry in the list; changing how `hard`
plays is changing a number. Nothing else in the program knows what any of these
words mean.

Two of those lines are worth explaining, because they are what the numbers had to be
tuned to get right.

**Standing inside a region** is the slope up to the step. Ruling a region is a step,
and most moves do not take one - so weighed on land alone nearly every move is worth
exactly what every other one is, and the machine picks between them by drawing lots.
Tuned without it, `hard` beat `easy` about as often as a coin would.

**Its own stones still in the bag**, set high against land, is what stops it emptying
its bag onto the map. This game is settled in two cascades: which faction carried the
board, and then which *player* carried the faction - and that second one is decided by
who is left holding most of the winning colour. A bag played out to nothing wins
nothing, and a game where every bag is empty is drawn outright. Weighted low, two
machines play every stone they have and draw every time.

That has a corollary that the checks had to be taught: **a machine that never plays a
stone at all beats a random one handsomely**, because the rules reward being left
holding things. It also makes a dreadful opponent. So `hard` is held to beating that
as well, and not by imitating it - [rival.fsx](../../../tests/rival.fsx) plays it against a
weights-set built to sit still, and separately insists that a machine facing somebody
who plays plays back rather than negotiating the game away.

The ordering the checks hold, over twelve deals played twice each with the seats
swapped so that going first is not what is being measured:

```
hard vs easy     net +10   won 16  lost  6  drawn  2
medium vs easy   net  +3   won 11  lost  8  drawn  5
hard vs medium   net  +7   won 13  lost  6  drawn  5
hard vs hoarder  net +10   won 16  lost  6  drawn  2
```

Fixed seeds, so there is nothing flaky in that: a run that came out differently would
mean the machine had changed and not the dice.

What it does not do is model the clock. The game ends when everybody negotiates in a
row, and knowing whether you want that to happen yet is a real part of playing well
that none of these three understand. Two machines of the same skill will often close a
game out at once for that reason; against somebody playing stones, they play back.


## The files

A game is a folder, and inside it the two seams are two folders. `Rules` is how it is played:
no English a player reads is laid out there, no screen, and nothing from `src/Table` - which
is not a house rule but a fact you can check, because not one file in there opens it.
`Reading` is how it is read. `Offer.fs` joins them, and is the only file either layer above
ever sees.

| File | Role |
| --- | --- |
| [Stones.fs](Rules/Stones.fs) | `StoneColour` and `Pile`, a multiset of stones |
| [Board.fs](Rules/Board.fs) | The fixed map: `RegionId`, the regions, the borders, and the checks that it hangs together |
| [Players.fs](Rules/Players.fs) | `Player` and `Table`, a seating of 2-5 with one of them active |
| [Position.fs](Rules/Position.fs) | Which stones stand where |
| [Ruling.fs](Rules/Ruling.fs) | Who rules a region, and how the land stands - both read off a position alone |
| [Game.fs](Rules/Game.fs) | The game in progress, and what can be asked of it |
| [Knowledge.fs](Rules/Knowledge.fs) | What one player can see of a game, and what they cannot |
| [Events.fs](Rules/Events.fs) | What happened, and why an action was refused |
| [Actions.fs](Rules/Actions.fs) | The four actions, each a `Game -> Result<Game * Event, Rejection>` |
| [Outcome.fs](Rules/Outcome.fs) | Which faction carries the board, and which player carries the faction |
| [Setup.fs](Rules/Setup.fs) | Dealing a fresh game |
| [Turn.fs](Rules/Turn.fs) | `Move`, and where a game stands: the phase, the turn, the run of negotiations, and how a turn ends |
| [Playing.fs](Rules/Playing.fs) | This game as the engine takes one, and the engine with it already in |
| [Words.fs](Rules/Words.fs) | Every string a player reads, including how events and rejections are worded |
| [Rival.fs](Rules/Rival.fs) | A seat played by the program: how a position is weighed, and how well |
| [Ink.fs](Reading/Ink.fs) | What this game colours, and its alphabet for laying colour over a drawn board |
| [Parse.fs](Reading/Parse.fs) | This game's own words as a `Move` - and only those, the rest having been read already |
| [Render.fs](Reading/Render.fs) | The `plain` view: every screen as blocks of text |
| [Rich.fs](Reading/Rich.fs) | The `rich` view: every screen built from Spectre's panels, tables and charts |
| [Html.fs](Reading/Html.fs) | The `html` view: every screen as a fragment of a page |
| [Offer.fs](Offer.fs) | Both seams filled in: this game as the engine takes one, and as a table reads one |

