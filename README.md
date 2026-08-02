# TCModel

A stone-placement game built as a Model-View-Update loop in F#. The core is pure:
`Setup.init` deals a game from a seed, `Update.update` folds a `Msg` into the next
`Model`, and `View.render` projects a `Model` to text. Only `Program.fs` touches the
console, so the view can be swapped for anything later.

## Layout

| File | Role |
| --- | --- |
| [src/Rng.fs](src/Rng.fs) | Immutable SplitMix64 generator and the `Rand<'T>` computation expression |
| [src/Domain.fs](src/Domain.fs) | `StoneColor`, `Pile` (a stone multiset), `Region`, `Player` |
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
- On a turn a player places one stone from their bag into any open region, or
  passes. The game ends when every bag is empty.

Points the rules did not settle, decided here and easy to change:

- **Setup exclusions** — "every other region gets 2 stones at random" is read as
  covering the wild regions only: the dead region is excluded because nothing may
  enter it, and the special regions are excluded because they start empty by rule.
- **Region count** — the Flag and the Axe are additions to the original twelve, so
  the board holds fourteen regions: 3 home + 8 wild + 1 dead makes the twelve, plus
  the two specials (`Setup.board`).

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

Commands: `place <colour> <region>` (alias `p`), `pass`, `restart [seed]`,
`players <n> [seed]`, `help`, `quit`. Colours are `r`/`red`, `b`/`blue`,
`k`/`black`; regions are numbered as shown on the board.

Every random decision comes from the seed, so a seed plus a list of messages
reproduces a game exactly.
