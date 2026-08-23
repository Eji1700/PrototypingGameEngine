# MyGame

A row of 15 tokens. Take one, two or three of them; whoever takes the last one wins. Two to four
players.

It is here to be deleted. Everything in it is the smallest honest answer to one of the questions
the seam asks, so the way to write your own game is to work through the files below replacing
answers, rather than to remember what a `Playable` wants.

```
dotnet run -- mygame play 2
```

## What is where

| | |
| --- | --- |
| [Rules/Row.fs](Rules/Row.fs) | The two numbers the whole game is about |
| [Rules/Round.fs](Rules/Round.fs) | The state: a game in play, or a game finished with an ending |
| [Rules/Turn.fs](Rules/Turn.fs) | The moves, the notices, and the fold. This is the game |
| [Rules/Words.fs](Rules/Words.fs) | Every word a player reads, and a move written down as a line |
| [Reading/Ink.fs](Reading/Ink.fs) | What may be recoloured, and where the colour goes back into plain text |
| [Reading/Parse.fs](Reading/Parse.fs) | A typed line read as a move |
| [Reading/Render.fs](Reading/Render.fs) | The board, the history, the rules and the page, described once |
| [Offer.fs](Offer.fs) | The seam: both halves handed over as one `Playable` |
| [Program.fs](Program.fs) | One line, so the game is its own executable |

`Rules/` is how it is played and `Reading/` is how it is read, and nothing in `Rules/` knows a
screen exists. Files compile in the order [the `.fsproj`](MyGame.fsproj) lists them, not
alphabetically — a new file means a new `<Compile Include=...>` line in the right place.

## Three things the template could not do

**1. Put it in the solution.** Add it to `TCModel.slnx` under the `/src/Games/` folder:

```xml
<Project Path="src/Games/MyGame/MyGame.fsproj" />
```

**2. Register it.** `src/Games.fs` is the only file in the program that names more than one game:

```fsharp
Play.chosen TCModel.MyGame.Offer.ways TCModel.MyGame.Offer.playable
```

and add a `<ProjectReference>` to it in the root `TCModel.fsproj`.

**3. Give it a suite.** `tests/mygame.fsx`, with a harness that `#load`s this game's sources over
the engine's — copy `tests/Living.fsx`, which is the shortest — and then:

```fsharp
#load "MyGaming.fsx"
#load "Conforms.fsx"

Conforms.against mygame 2 [ "2"; "1"; "3" ]

Checks.finish ()
```

`Conforms.against` is the contract every game here is held to: the deal, the seats, reading and
writing a line, the timeline, the record, the notices, every view at every state, the machines,
the clock and the page. Add `"mygame"` to the list in `tools/tests.ps1` or the suite never runs.

## Where to go next, when this is not enough

- **A machine at a seat** — write `Rules/Rival.fs` and fill in `Skills` and `Seating`.
  [`src/Games/TicTacToe/Rules/Rival.fs`](../../src/Games/TicTacToe/Rules/Rival.fs) is the small one.
- **A board that moves on its own** — fill in `Pulse`, and a beat becomes a move.
  [`src/Games/Life/Offer.fs`](../../src/Games/Life/Offer.fs) is the plain case;
  [`src/Games/Snake/`](../../src/Games/Snake/) is the one that also draws between beats.
- **Chance** — `Deal` is handed a seed, and `TCModel.Common.Rng` is a generator you pass along
  rather than one that mutates, so a seed and a list of moves still reproduce the game exactly.
- **Something hidden** — `SeenBy` is where a seat is told less than the table is, and it is the
  only place that difference lives. [`src/Games/Turncoats/`](../../src/Games/Turncoats/) is the
  game that has one.
- **A screen no general reader would think of** — `Views` takes a `View` written by hand instead
  of one built from `Scenes`. Turncoats is the only game here that needed to.

## Counting

Never build a count by hand — `TCModel.Common.Counting` has the three shapes, and nought and one
are where counts read wrong. If this game starts counting something the rest do not, add the noun
to the lists in `tests/counting.fsx` and `tests/Conforms.fsx`.
