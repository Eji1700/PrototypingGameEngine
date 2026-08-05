# TCModel

A stone-placement game built as a Model-View-Update loop in F#. The core is pure:
`Setup.deal` deals a game from a seed, `Update.update` folds a `Msg` into the next
`Model`, and `Render.model` projects a `Model` to text. Only `Program.fs` touches
the console, so the view can be swapped for anything later.

## Layout

Five layers, each depending only on the ones above it.

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
| [Knowledge.fs](src/Domain/Knowledge.fs) | What one player can see of a game, and what they cannot |
| [Events.fs](src/Domain/Events.fs) | What happened, and why an action was refused |
| [Actions.fs](src/Domain/Actions.fs) | The four actions, each a `Game -> Result<Game * Event, Rejection>` |
| [Outcome.fs](src/Domain/Outcome.fs) | Which faction carries the board, and which player carries the faction |
| [Setup.fs](src/Domain/Setup.fs) | Dealing a fresh game |

**`src/App`** — the MVU loop and the game's memory of itself: whose turn
it is, when it ends, when the game does, and everything that has happened so far.

| File | Role |
| --- | --- |
| [Messages.fs](src/App/Messages.fs) | `Move` and `Msg` |
| [Session.fs](src/App/Session.fs) | `Session`, `Play`, `Over`, and `Notice` |
| [Timeline.fs](src/App/Timeline.fs) | Every state the game has stood in, with a finger on the present |
| [Journal.fs](src/App/Journal.fs) | The record of play: what was asked, by whom, and what came of it |
| [Model.fs](src/App/Model.fs) | The timeline, the journal, and the last few lines on screen |
| [Update.fs](src/App/Update.fs) | `Msg -> Model -> Model` |

**`src/Console`** — the only part that talks to a person.

| File | Role |
| --- | --- |
| [Words.fs](src/Console/Words.fs) | Every string a player reads, including how events and rejections are worded |
| [Render.fs](src/Console/Render.fs) | `bool -> Model -> string`: the board, with or without the notes that explain it |
| [Parse.fs](src/Console/Parse.fs) | Console text to `Msg`, checking region numbers against the board |
| [Menu.fs](src/Console/Menu.fs) | The start menu: how many are playing, and what to deal |
| [Transcript.fs](src/Console/Transcript.fs) | A journal as a file, and a file back into a journal |

**`src/Net`** — the same game with the players at different keyboards. Only the
last two files here touch a socket.

| File | Role |
| --- | --- |
| [Protocol.fs](src/Net/Protocol.fs) | What crosses the wire, and what each end calls the other |
| [Lobby.fs](src/Net/Lobby.fs) | Seats, tokens, and the three rules a table adds to the game |
| [Server.fs](src/Net/Server.fs) | The host: one lobby behind a lock, and a SignalR hub over it |
| [Client.fs](src/Net/Client.fs) | A console at somebody else's table |

And [Program.fs](src/Console/Program.fs), compiled last, is the way in: the start
menu, the read/update/render loop, and the choice between playing here and playing
over a wire.

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
- **`Msg` separates a `Move` from walking the history.** Only a move can be
  attempted against a position, so `attempt` never has to answer what `Undo` does
  to a phase, and undo never has to answer what it does to a bag.
- **The model holds no current game.** The present position is whatever the
  timeline's finger is on, so a stored "current game" cannot drift out of step with
  the history behind it.
- **`Timeline` has a private constructor**, so its two lists can only be moved
  between in step: a state can be walked away from and back to, never dropped.
- **A `Sight` is `Open` of a pile or `Closed` of a count**, so a bag someone
  cannot see into carries no colours to leak by accident. What a player is shown
  is built by `Knowledge`, not filtered out of a `Game` at the point of writing.

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
> rule 2
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

## Taking it back, and writing it down

Two things remember the game, and they are deliberately different.

**The timeline** ([Timeline.fs](src/App/Timeline.fs)) is a zipper: the deal, the
moves made since, and the moves taken back. The present is wherever the finger
points. `undo` and `redo` only move the finger, so no state is ever rebuilt and
none is ever lost.

Because a session is a value and the generator travels inside it, undoing carries
the generator back too. A negotiation taken back and made again **draws the same
stone**. Undo is going back in time, not rolling again, and there is no way to
fish for a better draw.

**The journal** ([Journal.fs](src/App/Journal.fs)) is append-only and never
rewound. Undoing a move adds a line saying so rather than erasing the one before
it, so the record is the game as it was really played, second thoughts and all.
It keeps refused moves too: asking is part of what happened, and a refusal can
tell the table something — that the reserve is empty, or that a region holds no
black stone.

Every entry names the turn and the player who asked. That is the hook the account
of who-knows-what below hangs on: the record already says who was in a position to
learn each thing.

Both live in the model, and everything passes through `Model.happen`, so nothing
can reach the record without reaching the screen or the screen without the record.
Only input the shell could not parse stays out — it never became a move.

### The record as a file

A game writes itself to `logs/` when it ends, on `restart`, on the way out, and
whenever you type `save`. The name is fixed when the game is dealt, so saving the
same game again writes over the same file.

There is no second format. Every move is written exactly as it is typed at the
prompt, one to a line, and everything else is a comment — so reading a record back
is the same job as reading a player's input, done by the same parser.

```
deal 2 42

#   1  turn 1, Player 1
recruit r 3
#      Player 1 recruits a Red stone into Greymarket.

#   2  turn 2, Player 2
undo
#      Taken back: recruit r 3.
```

```powershell
dotnet run -- replay logs/2026-08-02-215823-2p-seed42.log
```

Replaying folds the recorded moves over a fresh deal. Since `update` is pure and
the generator is part of the game, this lands on exactly the state the record was
saved from — and, because undo and redo are recorded moves like any other, it
passes through every state the original game did on the way, in the same order.
A replayed game writes a byte-identical record; [history.fsx](tests/history.fsx)
checks that, state for state.

Replay stops where the game left off, which is also where `undo` starts. So the
same two commands that take a move back during play are what walk a finished game
backwards and forwards for review, and a game that ends stays open at the prompt
rather than closing the window on itself.

## Who knows what

A bag is held closed and so is the reserve, so nobody sees the whole game. The
screen belongs to whoever is to play, and `Knowledge.seenBy`
([Knowledge.fs](src/Domain/Knowledge.fs)) is what they are shown:

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
worded around in [Words.fs](src/Console/Words.fs):

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

## Playing from different machines

One player hosts a table and the rest join it:

```powershell
dotnet run -- host 3          # opens a table for three and waits
dotnet run -- join greg-pc    # each of the others, from their own machine
```

The host prints the addresses it can be reached at. A player says a machine name,
an address, or a whole URL; the port and the path are filled in
([Client.fs](src/Net/Client.fs)). Nobody plays until every seat is taken - a game
dealt for three hands out three bags whether or not three people have arrived, so
starting early would mean somebody playing a bag that is not theirs.

**The server is the only thing that holds a game.** A client holds no model and
knows no rules: it sends the line that was typed and prints what comes back, which
is already a board drawn for that player and nobody else. There is nothing in a
client that could show a player something they should not see, because there is
nothing in a client to show. This is why hidden information has to live where it
does - had `Render` filtered a shared screen at the last moment, a client would
have had to be trusted with the unfiltered one.

There is no second wire format either. The player sends the line they would have
typed at their own keyboard and `Parse.line` reads it; what comes back is what
`Render.model` would have drawn. Only strings and numbers cross the wire, so no
serialiser has to be taught the shape of the game's own types.

**What a table refuses that one keyboard allows** ([Lobby.fs](src/Net/Lobby.fs)):

- **Acting out of turn.** The one rule a single keyboard never needed, because
  there was only ever one pair of hands. A player who moves early is told whose
  turn it is and the game does not shift.
- **Undo and redo.** A game with more than one player at it only goes forward.
  Beyond the question of who would have the standing to take back somebody else's
  move, walking the timeline back and forward again is a way of *reading a bag*:
  undo a negotiation and redo it and you have watched a stone that was meant to be
  private. Undo stays a single-keyboard command.
- **Restart, and changing the number of players.** Seats are handed out against a
  dealt game, so redealing underneath the people sitting at it is not something one
  player may do to the others. A table plays the game it was opened with.

**Seats and tokens.** A seat is empty until somebody takes it, and once taken it
keeps its token for good. A console that drops leaves the seat *taken but empty*,
so the player can come back to their own stones rather than the seat being handed
to a stranger:

```powershell
dotnet run -- join greg-pc 3c3f9af9e8bc4bb88807a61de6e389af
```

The client prints that line when it sits down, and re-joins with the token by
itself when SignalR reconnects. A token that claimed no seat claims none now.

**Where the state is.** `Lobby` is a value like everything else, and every rule
above is decided by folding a typed line into it - `Lobby.said` returns the next
lobby and the list of things to say. [Server.fs](src/Net/Server.fs) holds exactly
one mutable field, behind a lock, and the hub does nothing but turn a call into a
fold and the fold's answer back into calls. So the multiplayer rules are testable
without a socket, and [lobby.fsx](tests/lobby.fsx) tests them that way.

The table writes its record after every move rather than at the end, because a
game with people at it can lose its host without warning. The file is the same
replayable transcript a local game writes.

## The map

Borders are declared once in `Board.declaredBorders` and symmetrised, so a border
only has to be named from one end. The resulting graph has 23 edges, is connected
across all twelve mainland regions, and leaves the Flag and the Axe bordering
nothing.

| region | borders |
| --- | --- |
| 1 Nightfen (Black home) | 2, 4 |
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
                      | [ 2] Saltmarsh      | [ 1] Nightfen (K)   |
                      | B B              >B | K K              >K |
           _/________/ \________\_/________/ \________\_/________/
           | [ 3] Greymarket     | [ 4] Thornwood      |
           | R R              >R | B K             =BK |
_/________/ \________\_/________/ \________\_/________/ \________\_
| [ 5] Emberfall (R)  | [ 6] Hollow Waste   | [ 7] Stonecradle    |
| R R              >R | dead                | R B             =RB |
 \________\_/________/ \________\_/________/ \________\_/________/ \________\_
           | [ 8] The Crossroads | [ 9] Windgap        | [10] Tidewatch (B)  |
           | R K             =RK | B K             =BK | B B              >B |
            \________\_/________/ \________\_/________/ \________\_/________/
                      | [11] Ironford       | [12] Dunmoor        |
                      | B B              >B | B K             =BK |
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
map that lies. [actions.fsx](tests/actions.fsx) checks the same list is empty.

The Flag and the Axe are drawn below the map in the same hand, standing clear of it
and of each other — sharing no side with anything, they border nothing.

No two homes border each other, and every home is three steps from every other,
whether or not the dead region is passable.

Rules that use adjacency can be written against `Board.areAdjacent`,
`Board.neighbours` and `Board.reachableFrom` (which takes a set of blocked regions,
ready for the dead region to obstruct movement).

## Running

```powershell
dotnet run                # the start menu, which asks how many are playing
dotnet run -- 3           # 3 players, random seed - straight to the board
dotnet run -- 3 42        # 3 players, reproducible game from seed 42

dotnet run -- replay logs/2026-08-02-215823-2p-seed42.log   # play a saved record again

dotnet run -- host 3      # open a table for three at their own machines
dotnet run -- join greg-pc            # sit down at one someone else opened
dotnet run -- join greg-pc <token>    # come back to the seat you were in
```

Arguments say what to deal and go straight to the board, so a game can still be
started from a script or a shortcut. With none, the menu
([Menu.fs](src/Console/Menu.fs)) asks:

```
=== TCModel ===

  Stones on a map, and a seat each. How many are playing?

    2  3  4  5             deal a game for that many, round this keyboard

  Or, to play from separate machines:

    host <players>         open a table and wait for them to arrive
    join <address>         sit down at a table someone else is hosting

  Or:

    <players> <seed>       the same game again, from a seed
    replay <file>          play a saved record again
    rules                  the rules and the commands, at length
    quit                   leave
```

A bare number is the answer to the question the menu asks, so it needs no command
word in front of it. The seatings on offer are read off `Table.MinPlayers` and
`Table.MaxPlayers` rather than written out, so the menu cannot come to offer a
number the table would refuse. Like the rest of the console layer `Menu` is pure -
it says what the menu reads like and what a typed line means, and `Program` does
the reading and the writing. Once a game is dealt, `players <n>` and `restart` do
the same job from the prompt.

| command | action |
| --- | --- |
| `recruit <colour> <region>` (`r`) | Recruit |
| `battle <colour> <region> [colours...]` (`b`) | Battle; name no colours to drive out all you may |
| `march <colour> <from> <to> [count]` (`m`) | March; count defaults to 1 |
| `negotiate` (`n`), then `return <colour>` | Negotiate |
| `undo` (`u`), `redo` | walk the game back and forward a move at a time |
| `history` (`log`) | the whole record so far, as it will be saved |
| `save` | write the record out now |
| `rule <region>` | not an action; shows who rules a region and why |
| `notes [on\|off]` | show or hide the writing that explains the board, and the short list of commands that sits above the log |
| `restart [seed]`, `players <n> [seed]`, `help`, `quit` | — |

Colours are `r`/`red`, `b`/`blue`, `k`/`black`; regions are numbered as shown on
the board. So `battle black 2 blue` places a black stone in the Axe and drives one
blue stone out of Saltmarsh, and `march blue 8 5 2` places a blue stone in the Flag
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
dotnet fsi tests/history.fsx    # undo, redo, and a record that survives the round trip
dotnet fsi tests/knowledge.fsx  # what a player sees, and that what they cannot still adds up
dotnet fsi tests/lobby.fsx      # seats, tokens, whose turn it is, and what a table refuses
```

Each script exits non-zero on failure. They load the source directly, so they run
without building the console app. `history.fsx` reaches up through the App layer to
the transcript reader and writer, which is what lets it check a whole game out to
text and back again.
