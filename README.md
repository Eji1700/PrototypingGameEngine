# TCModel

A stone-placement game built as a Model-View-Update loop in F#. The core is pure:
`Setup.init` deals a game from a seed, `Update.update` folds a `Msg` into the next
`Model`, and `View.render` projects a `Model` to text. Only `Program.fs` touches the
console, so the view can be swapped for anything later.

## Layout

| File | Role |
| --- | --- |
| [src/Prelude.fs](src/Prelude.fs) | The `result` computation expression used to chain an action's checks |
| [src/Rng.fs](src/Rng.fs) | Immutable SplitMix64 generator and the `Rand<'T>` computation expression |
| [src/Domain.fs](src/Domain.fs) | `StoneColor`, `Pile` (a stone multiset), `Region`, `Player` |
| [src/Cascade.fs](src/Cascade.fs) | Settling a contest by measures applied in order, shared by ruling and winning |
| [src/Ruling.fs](src/Ruling.fs) | Who rules a region |
| [src/Outcome.fs](src/Outcome.fs) | Which faction carries the board, and which player carries the faction |
| [src/Board.fs](src/Board.fs) | The fixed map: the region table, the borders between them, and the checks that the map hangs together |
| [src/Model.fs](src/Model.fs) | The `Model`, the `Msg` cases, and queries over the model |
| [src/Setup.fs](src/Setup.fs) | Board table and the opening deal |
| [src/Update.fs](src/Update.fs) | `Msg -> Model -> Model` |
| [src/View.fs](src/View.fs) | `Model -> string` |
| [src/Input.fs](src/Input.fs) | Console text -> `Msg` |
| [src/Program.fs](src/Program.fs) | The read/update/render loop |

## Rules as implemented

- 21 stones of each colour: red, blue, black (63 in total).
- 14 regions: one home per colour, eight wild regions, two special regions and
  one dead region.
- Each home starts with two stones of its own colour; each wild region draws two
  stones at random from what is left. The special regions (**The Flag** and
  **The Axe**) start empty and border nothing. The dead region starts empty and
  nothing may ever enter it, but it still sits on the map for adjacency.
- 2 to 5 players. Each draws a bag of eight stones at random; a player commands no
  faction, so a bag holds stones of any colour. Undealt stones sit in the reserve
  (25 with two players, 1 with five).
- On a turn a player takes one of the four actions below. There is no passing.

## Winning

Two cascades run when the game ends, both shaped exactly like ruling a region — see
`Cascade.run`, which all three share.

**The faction that carries the board:**

1. rules the most land;
2. failing that, the most stones in the Axe;
3. failing that, the most stones in the Flag;
4. failing that, the game is a draw.

The Flag and the Axe are ruled like anywhere else, but they are manoeuvres bought
with stones rather than ground held, so they count for nothing in the first
measure — only in the tie-breakers they *are*. That leaves twelve regions of land:
three homes, eight wilds and the dead region, which nobody can ever hold.
`Model.landRulings` draws that line.

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

Both cascades are printed with their working when the game ends, and
`dotnet fsi tests/outcome.fsx` checks them, including the two examples above.

One reading to confirm: when every bag is empty the whole game is a draw, following
"no one wins, the entire game is a draw" — even though a faction did carry the
board. The result names the faction anyway, so it reads *Black carried the board,
but every player has played out their bag*.

## Ending the game

The game ends once every player, in a row, has taken a turn without playing a
stone. `Model.Negotiations` counts that run: negotiating adds to it, recruiting,
battling or marching resets it to zero, and the game is over the moment it reaches
the number of players.

A player whose bag is empty has their turn skipped, and the skip counts towards the
run exactly as a negotiation does. So in a two-player game, one player being
skipped and the other negotiating ends it. If every bag is empty, every turn is a
skip and the run fills in one lap of the table — which is the "all players have
played out their stones" ending, arrived at by the same counter rather than a
separate check. The end is reported either way: *every player negotiated in turn*,
or *every player has played out their bag*.

One reading to confirm: an empty-handed player is **skipped rather than allowed to
negotiate**, following "a player can play their last stone, but then their turn is
skipped". That makes an empty bag final — a player cannot draw their way back into
the game. Letting them negotiate instead would still count towards the run, but
would let them refill from the reserve.

## The four actions

**Recruit** — place any stone from the bag into any region but the dead one. The
Flag and the Axe are legal targets, since the rule excludes only the dead region.

**Battle** — place any stone from the bag into the Axe and name another region
(not dead, not the Flag or the Axe). Count the stones there matching the colour
just placed; up to that many stones *of other colours* may be driven out of that
region and back to the reserve. The player chooses which. Driving out none is
legal, since the rule says "may".

**March** — place any stone from the bag into the Flag and name another region
(not dead, not the Flag or the Axe). One or more stones there of the matching
colour then move into a single region bordering it, which must not be the dead
region. Since the Flag and the Axe border nothing, they can never be marched into.

**Negotiate** — only open to a player holding at least one stone. Draw a stone from
the reserve at random into the bag; the player may then hand any one stone from the
bag back to the reserve, including the stone just drawn.

Readings the rules left open, all easy to change:

- **"the main bag"** in Battle is taken to be the reserve, the same pool Negotiate
  draws from.
- **Battle counts matching stones in the named region**, not in the Axe. The stone
  placed in the Axe stays there and does not count towards the total.
- **A march moves its whole group into one destination**, rather than splitting it
  across several neighbours.
- **Negotiate is two steps.** The draw is random, so the player cannot sensibly
  choose what to hand back before seeing it. `Negotiate` draws and leaves the turn
  open with `Model.Pending` set to `AwaitingReturn`; `Settle` then ends the turn.
  No other action is accepted in between.

Points the rules did not settle, decided here and easy to change:

- **Setup exclusions** — "every other region gets 2 stones at random" is read as
  covering the wild regions only: the dead region is excluded because nothing may
  enter it, and the special regions are excluded because they start empty by rule.
- **Region count** — the Flag and the Axe are additions to the original twelve, so
  the board holds fourteen regions: 3 home + 8 wild + 1 dead makes the twelve, plus
  the two specials (`Setup.board`).

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
of step with the board. It carries no consequence yet. The Flag and the Axe are
ruled like any other region, which makes the Axe's own tie-breaker self-referential
but well defined.

`rule <region>` shows the working:

```
> rule 6
Saltmarsh holds 1 Blue and 1 Black.
  stones in the region: Blue 1, Black 1 -> Blue, Black still level
  stones in the Axe: Blue 0, Black 1 -> Black leads
  Black rules the region.
```

`dotnet fsi tests/ruling.fsx` checks the cascade, including the elimination rule.

## The map

Borders are declared once in `Board.declaredBorders` and symmetrised, so a border
only has to be named from one end. The resulting graph has 23 edges, is connected
across all twelve mainland regions, and leaves the Flag and the Axe bordering
nothing.

| region | borders |
| --- | --- |
| 1 Emberfall (Red home) | 4, 5, 14 |
| 2 Tidewatch (Blue home) | 9, 10, 11 |
| 3 Nightfen (Black home) | 6, 7 |
| 4 The Crossroads | 1, 8, 9, 14 |
| 5 Greymarket | 1, 6, 7, 14 |
| 6 Saltmarsh | 3, 5, 7 |
| 7 Thornwood | 3, 5, 6, 10, 14 |
| 8 Ironford | 4, 9, 11 |
| 9 Windgap | 2, 4, 8, 10, 11, 14 |
| 10 Stonecradle | 2, 7, 9, 14 |
| 11 Dunmoor | 2, 8, 9 |
| 12 The Flag, 13 The Axe | none |
| 14 The Hollow Waste (dead) | 1, 4, 5, 7, 9, 10 |

No two homes border each other, and every home is three steps from every other,
whether or not the dead region is passable. `Board.problems` checks the table at
startup — ids on the board, no self-borders, isolated regions bordering nothing,
every other region reachable — and the game refuses to start if any check fails.

Rules that use adjacency can be written against `Model.neighbours`,
`Model.areAdjacent` and `Board.reachableFrom` (which takes a set of blocked
regions, ready for the dead region to obstruct movement).

## Running

```powershell
dotnet run                # 2 players, random seed from the clock
dotnet run -- 3           # 3 players, random seed
dotnet run -- 3 42        # 3 players, reproducible game from seed 42
```

| command | action |
| --- | --- |
| `recruit <colour> <region>` (`r`) | Recruit |
| `battle <colour> <region> [colours...]` (`b`) | Battle; the trailing colours are the stones driven out |
| `march <colour> <from> <to> [count]` (`m`) | March; count defaults to 1 |
| `negotiate` (`n`), then `return <colour>` or `keep` | Negotiate |
| `rule <region>` | not an action; shows who rules a region and why |
| `restart [seed]`, `players <n> [seed]`, `help`, `quit` | — |

Colours are `r`/`red`, `b`/`blue`, `k`/`black`; regions are numbered as shown on
the board. So `battle black 6 blue` places a black stone in the Axe and drives one
blue stone out of Saltmarsh, and `march blue 4 1 2` places a blue stone in the Flag
and moves two blue stones from the Crossroads into Emberfall.

Every random decision comes from the seed, so a seed plus a list of messages
reproduces a game exactly.
