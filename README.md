# TCModel

A stone-placement game built as a Model-View-Update loop in F#. The core is pure:
`Setup.deal` deals a game from a seed, `Update.update` folds a `Msg` into the next
`Model`, and `Render.model` projects a `Model` to text. Only `Program.fs` touches
the console, so the view can be swapped for anything later.

## Layout

Four layers, each depending only on the ones above it.

**`src/Common`** — generic, and knows nothing about the game.

| File | Role |
| --- | --- |
| [Result.fs](src/Common/Result.fs) | The `result` computation expression used to chain an action's checks |
| [Cascade.fs](src/Common/Cascade.fs) | Settling a contest by measures applied in order, shared by ruling and winning |
| [Random.fs](src/Common/Random.fs) | An immutable SplitMix64 generator, passed along as a value |

**`src/Domain`** — the language and the rules. No English a player would read.

| File | Role |
| --- | --- |
| [Stones.fs](src/Domain/Stones.fs) | `StoneColor` and `Pile`, a multiset of stones |
| [Board.fs](src/Domain/Board.fs) | The fixed map: `RegionId`, the regions, the borders, and the checks that it hangs together |
| [Players.fs](src/Domain/Players.fs) | `Player` and `Table`, a seating of 2-5 with one of them active |
| [Position.fs](src/Domain/Position.fs) | Which stones stand where |
| [Ruling.fs](src/Domain/Ruling.fs) | Who rules a region |
| [Game.fs](src/Domain/Game.fs) | The game in progress, and what can be asked of it |
| [Events.fs](src/Domain/Events.fs) | What happened, and why an action was refused |
| [Actions.fs](src/Domain/Actions.fs) | The four actions, each a `Game -> Result<Game * Event, Rejection>` |
| [Outcome.fs](src/Domain/Outcome.fs) | Which faction carries the board, and which player carries the faction |
| [Setup.fs](src/Domain/Setup.fs) | Dealing a fresh game |

**`src/App`** — the MVU loop: whose turn it is, when it ends, and when the game does.

| File | Role |
| --- | --- |
| [Model.fs](src/App/Model.fs) | `Session`, `Play`, `Over`, and the log |
| [Messages.fs](src/App/Messages.fs) | `Action` and `Msg` |
| [Update.fs](src/App/Update.fs) | `Msg -> Model -> Model` |

**`src/Console`** — the only part that talks to a person.

| File | Role |
| --- | --- |
| [Words.fs](src/Console/Words.fs) | Every string a player reads, including how events and rejections are worded |
| [Render.fs](src/Console/Render.fs) | `Model -> string` |
| [Parse.fs](src/Console/Parse.fs) | Console text to `Msg`, checking region numbers against the board |
| [Program.fs](src/Console/Program.fs) | The read/update/render loop |

## Keeping invalid states out

The types are shaped so that a good deal of what could go wrong cannot be written
down in the first place.

- **`RegionId` has a private constructor** and only `Board.tryId` mints one, from a
  number someone typed. So a `RegionId` always names a region that exists, looking
  one up is total, and "no such region" is a parsing concern that never reaches the
  rules.
- **`Position` holds an entry for every region**, so asking what stands somewhere
  always answers rather than returning an option.
- **`Table` keeps the active player as a seat, not an id**, and can only be built
  with 2 to 5 players. There is always exactly one active player, and they are
  always at the table.
- **`Pile` never holds a zero or negative count** — a colour that is absent is
  simply not in the map.
- **`Session` is `InPlay` or `Finished`**, and only `InPlay` carries a phase and a
  turn. Nothing has to ask whether the game it holds is still running.
- **`Phase` is `AwaitingAction` or `AwaitingReturn`**, so a turn cannot be both open
  to any action and waiting on a stone to go back.
- **Events and rejections are data**, not sentences. The domain says
  `NothingToDriveOut(region, colour)` and [Words.fs](src/Console/Words.fs) decides
  how that reads, so wording can change without touching a rule.

What is not enforced by types: that the 63 stones are conserved. Actions only ever
move stones between piles, and `tests/actions.fsx` checks the total after each one.

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

Points the rules did not settle, decided here and easy to change:

- **Setup exclusions** — "every other region gets 2 stones at random" is read as
  covering the wild regions only: the dead region is excluded because nothing may
  enter it, and the special regions are excluded because they start empty by rule.
- **Region count** — the Flag and the Axe are additions to the original twelve, so
  the board holds fourteen regions: 3 home + 8 wild + 1 dead makes the twelve, plus
  the two specials.

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
> rule 6
Saltmarsh holds 1 Blue and 1 Black.
  stones in the region: Blue 1, Black 1 -> Blue, Black still level
  stones in the Axe: Blue 0, Black 1 -> Black leads
  Black rules the region.
```

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
separate check. The end is reported either way: *every player negotiated in turn*,
or *every player has played out their bag*.

Since a negotiation never grows a bag and only a stone in hand allows one, every
bag shrinks monotonically, so a game always winds down.

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

Rules that use adjacency can be written against `Board.areAdjacent`,
`Board.neighbours` and `Board.reachableFrom` (which takes a set of blocked regions,
ready for the dead region to obstruct movement).

## Running

```powershell
dotnet run                # 2 players, random seed from the clock
dotnet run -- 3           # 3 players, random seed
dotnet run -- 3 42        # 3 players, reproducible game from seed 42
```

| command | action |
| --- | --- |
| `recruit <colour> <region>` (`r`) | Recruit |
| `battle <colour> <region> [colours...]` (`b`) | Battle; name no colours to drive out all you may |
| `march <colour> <from> <to> [count]` (`m`) | March; count defaults to 1 |
| `negotiate` (`n`), then `return <colour>` | Negotiate |
| `rule <region>` | not an action; shows who rules a region and why |
| `restart [seed]`, `players <n> [seed]`, `help`, `quit` | — |

Colours are `r`/`red`, `b`/`blue`, `k`/`black`; regions are numbered as shown on
the board. So `battle black 6 blue` places a black stone in the Axe and drives one
blue stone out of Saltmarsh, and `march blue 4 1 2` places a blue stone in the Flag
and moves two blue stones from the Crossroads into Emberfall.

Every random decision comes from the seed, so a seed plus a list of messages
reproduces a game exactly. That is why the generator in [Random.fs](src/Common/Random.fs)
is a value handed back with each result rather than `System.Random`: a generator that
advanced by mutation would make `update` impure and stop the model being a value, and
`restart` draws its next seed from the generator already in play.

## Tests

```powershell
dotnet fsi tests/ruling.fsx     # the ruling cascade, including elimination
dotnet fsi tests/outcome.fsx    # both winning cascades
dotnet fsi tests/actions.fsx    # what each action does and refuses, and stone conservation
```

Each script exits non-zero on failure. They load the domain directly, so they run
without building the console app.
