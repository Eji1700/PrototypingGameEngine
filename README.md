# TCModel

A stone-placement game built as a Model-View-Update loop in F#. The core is pure:
`Setup.deal` deals a game from a seed and `Update.update` folds a `Msg` into the
next `Model`. Nothing below the console layer knows a screen exists, and every
screen a player reads goes through a `View` - so how the game looks is swappable,
and it is: `plain` writes blocks of text, `rich` builds panels and charts, and two
players at one networked table can each pick their own.

Seats can be played by the program rather than by somebody in the room. A machine
at a seat is held to what a player is held to: it asks the rules what they will
take rather than keeping its own opinion, it reads the map and its own bag and
nothing else, and it picks a `Move` - the very thing a typed line turns into.

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
| [Ruling.fs](src/Domain/Ruling.fs) | Who rules a region, and how the land stands - both read off a position alone |
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
| [Rival.fs](src/App/Rival.fs) | A seat played by the program: how a position is weighed, and how well |
| [Timeline.fs](src/App/Timeline.fs) | Every state the game has stood in, with a finger on the present |
| [Journal.fs](src/App/Journal.fs) | The record of play: what was asked, by whom, and what came of it |
| [Model.fs](src/App/Model.fs) | The timeline, the journal, and the last few lines on screen |
| [Update.fs](src/App/Update.fs) | `Msg -> Model -> Model` |

**`src/Console`** — the only part that talks to a person.

| File | Role |
| --- | --- |
| [Waiting.fs](src/Console/Waiting.fs) | A seat at a table that has not filled up yet, as the person waiting sees it |
| [Showing.fs](src/Console/Showing.fs) | What a table shows one console, and which console it is for |
| [Words.fs](src/Console/Words.fs) | Every string a player reads, including how events and rejections are worded |
| [Render.fs](src/Console/Render.fs) | The `plain` view: every screen as blocks of text |
| [Parse.fs](src/Console/Parse.fs) | Console text to `Msg`, checking region numbers against the board |
| [Palette.fs](src/Console/Palette.fs) | Which colour is drawn for what, and the words a person says for them |
| [Tint.fs](src/Console/Tint.fs) | Colour laid over writing already laid out, and Spectre's output as a string |
| [Rich.fs](src/Console/Rich.fs) | The `rich` view: every screen built from Spectre's panels, tables and charts |
| [Html.fs](src/Console/Html.fs) | The `html` view: every screen as a fragment of a page, and the page they land in |
| [View.fs](src/Console/View.fs) | Every screen a player reads, and choosing which way to read them |
| [Solo.fs](src/Console/Solo.fs) | The game at one keyboard, as a value: what a typed line does, who answers it, and what it asks written down |
| [Options.fs](src/Console/Options.fs) | The colour screen: what is drawn in what, and how a person changes it |
| [Launch.fs](src/Console/Launch.fs) | What a command line asks the program to open, as a value that can be written back out as a line |
| [Shell.fs](src/Console/Shell.fs) | The command surface: the commands, their options, and what is refused at the door |
| [Menu.fs](src/Console/Menu.fs) | The start menu: how many are playing, and what to deal |
| [Transcript.fs](src/Console/Transcript.fs) | A journal as a file, and a file back into a journal |

**`src/Net`** — the same game with the players at different keyboards. Only the
last three files here touch a socket.

| File | Role |
| --- | --- |
| [Protocol.fs](src/Net/Protocol.fs) | Where the table listens, and what each end calls the other |
| [Browser.fs](src/Net/Browser.fs) | A page as a console: the streams held open to them, and what is served. Knows of no table in particular |
| [Lobby.fs](src/Net/Lobby.fs) | Seats, tokens, and the three rules a table adds to the game |
| [Server.fs](src/Net/Server.fs) | The host, and the local game served to a browser: a table behind a lock, with pages and sockets over it |
| [Client.fs](src/Net/Client.fs) | A console at somebody else's table |

And [src/Program.fs](src/Program.fs) is the way in: the start menu, the
read/update/render loop, and the choice between playing here and playing over a wire.
It sits outside all five because it needs all five - F# compiles in order and a file
sees only what came before it, so the door has to be the last thing built.

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
Saltmarsh holds 1 Blue and 1 Green.
  stones in the region: Blue 1, Green 1 -> Blue, Green still level
  stones in the Axe: Blue 0, Green 1 -> Green leads
  Green rules the region.
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
green stone.

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

## Playing against the program

A seat can be played by the program. `--rival <skill>` gives away the seat after
yours, once per seat, and the first seat is always yours:

```powershell
dotnet run -- play 2 --rival medium
dotnet run -- play 4 --rival easy --rival hard --rival hard
dotnet run -- serve 2 --rival hard      # the same, in a browser
```

There are three: `easy`, `medium` and `hard`. Nothing on the board says which seats
are the machine's - its stones look like anybody's, and they are - so the table says
so once, to whoever sits down to watch, in the words their own view speaks.

### It plays no better than it is allowed to

A machine at a table is the one thing here that can be wrong quietly. Every other
part of the program either draws something a person looks at or refuses something a
person typed; a machine that has picked a move nobody would has picked a legal move,
drawn a perfectly good board, and said nothing at all. So it is fenced in three ways,
and each of them is a thing the compiler or a check can hold it to.

**It keeps no second copy of the rules.** To find out whether a move is allowed, it
asks [Actions.fs](src/Domain/Actions.fs) - the same functions `Update` asks - and
takes the answer. A machine that worked legality out for itself would be a second
opinion about the rules, free to drift from the ones being played, and the way that
shows up at a table is a machine asking for something, being told no, and asking
again with the turn never passing. Over two whole machine-played games,
[rival.fsx](tests/rival.fsx) insists not one thing it asked for was refused.

**It reads the map and one bag.** The function that weighs a position takes a
`Position` and a `Pile`, which is the whole of what somebody at that seat can see.
There is nothing in the arguments to cheat with. Working out how the land stands from
a position alone is what [Ruling.fs](src/Domain/Ruling.fs) does, so this is the game's
own reckoning rather than a copy of it. And the same thing is said again from outside:
the stones in the bags it cannot see are poured together and dealt back out between
those players, and it has to pick the same move it picked before. Between them, not
into them - what it is entitled to work out, that every stone is *somewhere*, is
untouched, and the only thing that changed is the one thing it was never shown.

**It can only make moves a person could type.** It picks a `Move`, which is what
`Parse.line` produces from a typed line, so its moves land in the record in the same
words yours do and a game against a machine replays like any other. Every move made
over two whole games is written out with `Words.command`, fed back to `Parse.line`,
and has to come back the same move.

Its generator travels with it the way the game's own travels inside the game, so the
same deal against the same machines plays the same game twice.

### Three sets of numbers, not three machines

There is one machine. What `easy`, `medium` and `hard` name is a set of weights and
two knobs, all at the foot of [Rival.fs](src/App/Rival.fs):

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
as well, and not by imitating it - [rival.fsx](tests/rival.fsx) plays it against a
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

### At the table

`Solo` gains one rule and one only: after a person has spoken, the machines answer for
as long as the seat to act is one of theirs. What stops them is a move that left the
game exactly as it found it - nothing they pick should ever be refused, but a machine
that had somehow found one the rules would not take would otherwise be asked for it
again, and again.

The same rule read backwards is what `undo` does. Taking a move back takes the
machine's answer back with it, and stops where a person has to decide something;
anything else and one `undo` would hand the turn straight back and nothing would ever
be undone.

Everything else follows from a machine's move being an ordinary move. It goes through
`Update.update`, lands in the record against its own seat, is drawn to everybody
watching, and reaches a browser down the same stream a person's does. There is no
second table and no second protocol.

Hosted tables ([Lobby.fs](src/Net/Lobby.fs)) have no machines at them. That is a
different table with seats, tokens and people at their own keyboards, and filling one
of those seats with a machine is a separate piece of work rather than the same one.

## How the board is shown

A **view** ([View.fs](src/Console/View.fs)) is every screen a player ever reads.
Nothing else in the program prints anything a player would call part of the game,
so a new way of showing it is written once and everything picks it up - one
keyboard or five over a network.

```fsharp
type View =
    { Name: string
      Describe: string
      Shown:   Shown                               // what has to be reading it
      Palette: Palette                             // the colours it was built with
      Board:   bool -> Player -> Model -> string   // the whole board, for one player
      History: Player -> Model -> string           // the record of play so far
      Ruling:  RegionId -> Model -> string         // why a region is ruled as it is
      Rules:   string                              // the rules and the commands
      Says:    string -> string                    // one line, with no board to go with it
      Waiting: Waiting list -> string }            // a table still filling up
```

| view | shown | |
| --- | --- | --- |
| `plain` | at a terminal | blocks of text, and nothing this terminal has to understand |
| `rich` | at a terminal | panels, tables and charts in colour, via [Spectre.Console](https://spectreconsole.net) |
| `html` | in a browser | a page, updated in place - see [In a browser](#in-a-browser) |

```powershell
dotnet run -- play 3 --seed 42 --view rich
dotnet run -- join greg-pc --view rich
```

`--view <name>` goes with every command, because every command ends in somebody
reading a board - and how a board is drawn says nothing about what to deal, so it is
an option rather than an argument and can sit anywhere. From the menu, `view rich`;
from the prompt mid-game, the same. A new view is added to `View.all` and nowhere
else - the menu, the command line and the prompt all read that one list.

`Shown` is not a preference. A terminal handed a page reads angle brackets and a
browser handed the rich board reads escape codes, so **nothing is ever offered to a
reader that could not show it**: `View.byName` takes the reader as part of the
question, and `view html` at a terminal is answered with the two views a terminal has.

**The endpoints take the model, not somebody else's finished text**, so a view is
free to lay a screen out however it likes rather than only colouring what it is
handed. That is the whole difference between the three: `plain` writes the board as
one block of text, `rich` ([Rich.fs](src/Console/Rich.fs)) builds it out of Spectre's
panels, tables and charts - every region a panel bordered in the colour of whoever
rules it, bags drawn stone by stone, a closed bag drawn as the row of stones nobody
can name, land ruled as a bar chart, what is out of sight as a breakdown - and `html`
([Html.fs](src/Console/Html.fs)) builds the same things out of elements.

**The three factions are Red, Blue and Green**, written `R`, `B` and `G`. Each is
drawn a good deal brighter than the flat version of its own colour, because a stone
in plain blue is barely there on a dark screen. Because a colour's name and its glyph
now begin with the same letter, one lookup serves both: `Gx4`, a lone `G` on the map,
the word "Green" in a sentence, a bar in a chart and a region's border all follow from
[Tint.fs](src/Console/Tint.fs) and `Words.glyph`. The reader's own seat is marked in
gold rather than a fourth hue that could be mistaken for a stone.

### Colours a player chooses

Which colour is drawn for what is a **palette** ([Palette.fs](src/Console/Palette.fs)),
and `colours` at the menu opens the screen that changes one:

```
    red      Red   R R   >R          crimson   Red stones, and the regions Red rules
    blue     Blue   B B   >B         teal      Blue stones, and the regions Blue rules
    green    Green   G G   >G        moss      Green stones, and the regions Green rules
    yours    (you)   ->              gold      your own seat, and whose turn it is
    hidden   dead                    slate     what is held back from you, and ground nobody may enter

    Say 'blue teal' to change one, 'reset' to put them all back, or 'done'.
```

Five things take a colour and nineteen colours are on offer, each with a short word of
its own rather than Spectre's `mediumpurple1`. The five live in one list, so the screen
that offers them, the line that changes one and the two halves of sending a palette down
a wire cannot come to disagree about what there is.

The samples in the middle column are not written out for the screen: they are the board's
own words - a stone's glyph, `>R`, `(you)`, `dead` - and the screen is shown through the
very view built in the palette it is offering. So `Tint` colours them exactly as it will
colour the board, and choosing is looking.

A palette travels with the view, not with the model: the same position drawn twice in two
palettes is the same position, so it stays out of the record and out of the game. That is
also what lets a networked table draw one board five ways - a joining console sends its
colours along with the view it wants, in the same words a person would have typed
(`red=crimson blue=teal ...`), read back by the same `Palette.set` that reads them at the
prompt. Two people at one table really do read the same stones in different colours.

`plain` carries a palette too, though it draws in none, so a player who sets their colours
and *then* asks for `rich` gets the ones they set.

The green faction was called **Black** and written `K` earlier on, and records written
then say so. `Parse` still reads `black` and `k`, though nothing writes them any more,
because a record is meant to replay for good - the game in
[logs/2026-08-03-193909](logs/2026-08-03-193909-2p-seed639214079490240407.log) has a
`march k 3 2 1` in it and still plays back exactly as it did.

Adding to the game means adding an endpoint here and answering it in every view.
That is the trade: a wide seam, but one that cannot be half-implemented without the
compiler saying so.

**What a view may not do is decide anything.** Two rules keep that honest:

- **What a player may know comes from `Knowledge`**, never from a renderer, and what
  a notice says comes from `Render.wording`. A second renderer is a second chance to
  leak, and the way not to take it is not to write that reasoning down twice.
  [view.fsx](tests/view.fsx) sweeps every view in `View.all` and checks that each one
  shows a player their own bag, shows them nobody else's, and names a drawn stone
  only to the player who drew it.
- **What is true of the position comes from the domain.** How the land stands -
  ruled, tied, still going spare - is `Game.landStanding`, not a sum each renderer
  works out for itself, because a second view counting it again could count it
  differently.

**The map is drawn three times, and all three say the same thing.** `plain` draws
the honeycomb by counting characters into columns; `rich` gives every region a panel
of its own, bordered in the colour of whoever rules it; `html` gives it a box, bordered
the same way.

What matters is that none of them loses adjacency. `Board.layout` lies on a triangular
lattice - a region is two half-columns wide and each row stands half a region across
from the one above - so a region touches exactly six others: the two beside it and
two on each of the rows above and below. Those six are its borders, and the map is
the only part of the screen that says where a player may march. A honeycomb shows it
with cut corners; brickwork shows it with the half-region offset. Either is faithful.
A tidy grid with the offset dropped would not be, which is why `rich` and `html` both
keep it - in `html` it is a `margin-left` of so many half-regions and nothing else.

```
                          ╭─ [ 2] Saltmarsh ───────╮╭─ [ 1] Nightfen (G) ────╮
                          │ B B                 >B ││ G G                 >G │
                          ╰────────────────────────╯╰────────────────────────╯
             ╭─ [ 3] Greymarket ──────╮╭─ [ 4] Thornwood ───────╮
             │ R R                 >R ││ B G                =BG │
             ╰────────────────────────╯╰────────────────────────╯
╭─ [ 5] Emberfall (R) ───╮╭─ [ 6] Hollow Waste ────╮╭─ [ 7] Stonecradle ─────╮
│ R R                 >R ││ dead                   ││ R B                =RB │
╰────────────────────────╯╰────────────────────────╯╰────────────────────────╯
```

What a region *says* is shared: `Render.regionTitle` and `Render.standingIn` both
take the room they are given, so a name too long for it gives up its "The" the same
way in either view. Where the regions go is each view's own business.

`plain` composes its map and is then tinted, in that order, because it is laid out by
counting into columns and an escape code inserted mid-layout would push everything
after it sideways. Colour goes on once every column is settled.

**Over a network the view lives at the table.** A view needs the game to lay a screen
out, and the game is at the table, so the board is drawn per seat: the seat holds the
view and the client says which one it wants when it sits down. Two people at one table
can be sent two boards that look nothing alike from the one position, and neither the
game nor the other player is any the wiser.

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
dotnet run -- join greg-pc --token 3c3f9af9e8bc4bb88807a61de6e389af
```

The client prints that line when it sits down, and re-joins with the token by
itself when SignalR reconnects. A token that claimed no seat claims none now.

That line is not written by hand. It comes from `Launch.write`, which builds it from the
same declaration the command line is read by - so what a player is told to type is
something the program is certain to accept. See [Two halves of a command
line](#two-halves-of-a-command-line).

**Where the state is.** `Lobby` is a value like everything else, and every rule
above is decided by folding a typed line into it - `Lobby.said` returns the next
lobby and the list of things to say. [Server.fs](src/Net/Server.fs) holds one mutable
field per table, behind a lock, and the hub does nothing but turn a call into a fold
and the fold's answer back into calls. So the multiplayer rules are testable without
a socket, and [lobby.fsx](tests/lobby.fsx) tests them that way.

### Two kinds of table

There are two, and they are the same shape: hand one a typed line and it gives back the
next table and a list of things to show, each addressed to somebody.

| | [Solo.fs](src/Console/Solo.fs) | [Lobby.fs](src/Net/Lobby.fs) |
| --- | --- | --- |
| who is at it | one pair of hands, however many are watching | one seat each |
| whose board is drawn | whoever is to play — it turns over with the turn | whoever is reading it |
| out of turn | there is nobody else | refused |
| `undo` / `redo` | allowed | refused — walking a negotiation back reads a bag |
| `restart` | allowed | refused |
| seats and tokens | none | that is most of what it is |
| being told it is your turn | only when somebody else is watching too | every turn you did not cause |

`Solo` came out of the prompt's own read/act/print loop, which had been keeping these
rules since the beginning but kept them wrapped around `Console.ReadLine`. Pulling them
out was what let a browser play a local game without a second copy of what a typed line
means — and it made them checkable, which they had never been:
[solo.fsx](tests/solo.fsx) is what `lobby.fsx` is for the other one.

Writing a file is the only thing the local game ever needs of the world, so `Solo` says
what it wants written rather than writing it. That is what makes the awkward case
checkable: a `restart` asks for the record of the game it has just swept off the table,
under *that* game's name, not the fresh one's.

The table writes its record after every move rather than at the end, because a
game with people at it can lose its host without warning. The file is the same
replayable transcript a local game writes.

### In a browser

Either kind of game can be read in a browser:

```powershell
dotnet run -- serve 3         # a game for three in a browser here, hot seat, no waiting
dotnet run -- host 3          # a table for three, for terminals and browsers alike
```

`serve` is `play` with a page instead of a terminal - not `host` with the waiting taken
out. There are no seats: it is one hot seat and the screen belongs to whoever is to
play, so it starts the moment it is opened, every move is yours, and `undo` works. Two
browsers can watch the same hot seat, and both see every move.

`host` deals seats. Open the address it prints instead of running `join`, and you sit
down at that table.

**A browser and a terminal can sit at one hosted table**, take turns in order, and each
be drawn a board of their own. `Lobby` never learns there are two kinds of console: it
addresses a `Post` to a console id, and which sort that is, is written into the id
([Browser.fs](src/Net/Browser.fs)).

| | terminal | browser |
| --- | --- | --- |
| the socket | SignalR | server-sent events, held open by the page |
| what goes across | the line typed, and the screen back | the same |
| who you are | a token you are shown and can retype | a cookie, kept for you |
| what draws the board | the table, per seat | the table, per seat |
| how it says it is your turn | the bell, `\a` | the tab title, and a notification if allowed |

**Essentially no JavaScript, and none of it written here.**
[Datastar](https://data-star.dev) is one 34 KB file, committed under `assets/` and
embedded in the binary rather than fetched - a table opened on a machine with no way
out to the internet is exactly the table this game is for. There is no `package.json`,
no `node_modules`, and nothing to run before `dotnet run`. The markup is built with
[Falco.Markup](https://github.com/FalcoFramework/Falco.Markup), which is F#, not a
template language.

**The client's vocabulary is not written out by hand.** An attribute that takes a key
separates the key from the plugin's name with a **colon** — `data-on:click`, not
`data-on-click`. Get that wrong and the client looks for a plugin called `on-click`,
doesn't find one, and says nothing at all: the page renders, the stream opens, the board
draws, and not one button on it works. No error, no warning, nothing in the console.

That happened. Two things stop it happening again.

Every client attribute comes from
[Falco.Datastar](https://github.com/FalcoFramework/Falco.Datastar) — `Ds.onClick`,
`Ds.bind`, `Ds.onInit`, `Ds.signals` — so the spelling is the library's business and a
typo is a compile error rather than a silent one. That is the whole reason it is here: the
signal machinery it is really built for, this page barely uses.

And [html.fsx](tests/html.fsx) reads the vocabulary **out of the carried client** and
holds every attribute the page emits against it, split the way the client itself splits
them. Not a list anybody typed — the file in `assets/`. So the two can be checked against
each other, which matters because they version separately: Falco.Datastar's own CDN pin is
`1.0.0-RC.7` while the client carried here is `1.0.2`. That check is what says they still
agree.

Two small deviations from what the library emits, both in [Html.fs](src/Console/Html.fs)
with the reason written down: `data-bind:line` is given an empty value rather than standing
for itself, and the Enter-key expression asks a question rather than using `&&`. Both are
so that every screen stays well-formed markup, which is a thing the checks can hold it to.

**The frames are the library's too.** `Response.sseStartResponseWithHeaders` opens the
stream and `Response.sseStringElements` writes each screen into it, so the event's name and
the way a screen with newlines in it is carried without arriving in pieces are no longer
written out in [Browser.fs](src/Net/Browser.fs). They were, and they were the same kind of
string as `data-on-click`: wrong, and nothing says so.

**And the one signal is declared once.** `Html.Signals` is what the page is started with
(`Ds.signals`), what the box at the bottom is bound to, and what the table reads back
(`Request.getSignals`). It is spelled twice inside itself and deliberately — `Line` for F#
and `line` for the wire, because the wire's is the name the client binds to:

```fsharp
type Signals =
    { [<JsonPropertyName("line")>]
      Line: string }
```

What is *not* taken is Falco's routing. A hosted table serves terminals over SignalR and
browsers over SSE at the same time, so `MapHub` stays either way — adopting Falco's
endpoints would mean two routing idioms where there is currently one, to replace eight
lines that map five routes with no parameters.

**Every control types a line.** A button is `@post('/say?line=recruit%20r%205')`, and
the server hands `recruit r 5` to the same `Parse.line` a prompt would. So the page
cannot offer a move the game would not take - there is nothing else for a button to
send - and the record stays one language all the way out to the browser.
[html.fsx](tests/html.fsx) pins that down: it pulls every line out of every screen this
view draws and puts each one through `Parse.line`.

Moves needing more than a colour and a region - a battle, a march - are typed into the
box at the bottom, which is the prompt by another name.

**A screen is a fragment, not a page.** Every screen `Html` draws is one element with
an id on it, so the same text serves both for building the page the first time and for
patching it on the fiftieth move. The client puts an element where the element of that
id already is, which means nothing on the server has to work out what changed - and
`Lobby` was already redrawing whole boards per console, so there was nothing to work
out. Two ids, two places anything lands: the board, and whatever the game last said
without a board to go with it.

**The colours are the page's.** `Palette` becomes five CSS custom properties in the
document's own head, and every fragment draws in those rather than in colours of its
own - so one board is built however many people are reading it, in however many
palettes. The `colours` control at the top right is a plain `<form method="get">`,
which needs no client at all; each choice is written as the words a console would type
(`red=teal`) and read back by the same `Palette.read` that reads them off the wire.

For terminals, `Palette.ink` gives Spectre's name for a colour rather than a hex triple,
because sixteen of a terminal's colours belong to whoever owns the terminal and may have
been re-themed. A browser has no such sixteen, so `Palette.paint` gives the triple, exact.

### When the turn comes round

At a table where everybody is at their own machine, most of the game is spent waiting on
somebody you cannot see - and the sensible thing to do while waiting is something else.
So the turn arriving has to be able to reach a player who is not looking at it.

**The rule is one sentence, and it is a rule about being quiet.** A console is told the
turn has come round when it has come round *and nothing that console did brought it
round*. Nothing else qualifies. A move you made yourself does not, because you are
sitting there watching it. A move somebody else made that did not reach you does not. A
line that only changed how you read the board does not. A game that is over has come
round to nobody. The one case besides a move is the last player sitting down, which
starts the game - the player it starts with has been staring at a waiting room.

Half of that is knowable only at the table and half only at the far end, which is what
decides the shape:

| | knows | so it decides |
| --- | --- | --- |
| the table | who moved, and whose turn it now is | whether the turn arrived **unasked** |
| the console | whether anybody is sitting in front of it | whether that is worth **interrupting** for |

So the table sends `Nudged`, which carries no words at all - it is the one thing a table
ever says that is not something to read. What becomes of it is the far end's business.

**A terminal rings.** One character, `\a`, which has meant this since before there were
windows to put a terminal in. Most terminals flash their taskbar entry when they are not
the window being looked at, some make a sound, and one told to do neither does neither.
None of that is worth second-guessing from inside the game, and a console cannot see
whether anybody is watching it anyway - so it rings whenever the table says the turn
arrived unasked, which is rare by construction.

**A page marks itself, and says so out loud if it has been allowed to.** Two ways,
because they fail in opposite places: the tab title needs nobody's permission and is no
use at all to somebody whose browser is behind three other windows, and a notification is
exactly what reaches that player and no browser will show one unasked. So the page marks
its title, puts it back the moment somebody looks again, and raises a real notification if
permission has been given.

The asking is a `notify` button beside the colours, and it is **the only control on the
page that is not a line of typing**. That is not a lapse, it is the reason: a browser only
takes this question from a click it has just seen, and a line typed at the prompt has been
to the table and back before anything on the page could ask. The button takes itself off
the page in every state where it could do nothing.

**Where it never fires.** At one keyboard (`play`), and in a browser with the game to
yourself (`serve` with nobody else reading), the only console at the table is the one that
just typed something - so nothing ever rings, including when the machines answer you. Open
that same served game in a second browser and it does fire, in both directions, because now
there is somebody to interrupt.

What holds it: the rule is a fold, so [lobby.fsx](tests/lobby.fsx) and
[solo.fsx](tests/solo.fsx) check it without a socket - mostly by checking the silences. The
*delivery* to a browser is the part no value can be asked about, because a nudge is the one
thing down that stream that is not a piece of the page: it arrives as a script for the
client to run and throw away, and a page can take every board perfectly while quietly
dropping every one of these. So [smoke.ps1](tools/smoke.ps1) sits two consoles at one
served game - `localhost` and `127.0.0.1`, which are two cookies and therefore two callers
- moves in one, and asks the other whether it was knocked on.

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
dotnet run                     # the start menu, which asks how many are playing
dotnet run -- play 3           # 3 players, random seed - straight to the board
dotnet run -- play 3 --seed 42 # the same game again, from a seed
dotnet run -- play 2 --view rich --colour blue=teal

dotnet run -- play 2 --rival medium              # the seat after yours, played by the program
dotnet run -- play 4 --rival easy --rival hard   # once per seat you are giving away

dotnet run -- serve 3          # the same game, played in a browser on this machine
dotnet run -- serve 2 --rival hard

dotnet run -- replay logs/2026-08-02-215823-2p-seed42.log

dotnet run -- host 3                        # open a table at their own machines
dotnet run -- join greg-pc                  # sit down at one someone else opened
dotnet run -- join greg-pc --token <token>  # come back to the seat you were in
                                            # or open greg-pc:5000 in a browser

dotnet run -- --help           # every command; --help works on each of them too
```

### Two halves of a command line

The arguments go through [Spectre.Console.Cli](https://spectreconsole.net/cli/), which
owns the command surface: the five commands, the options that go with each, `--help`,
and the checking. A count the table would refuse, a view nobody has, a colour nobody
has - each is answered at the door, in the same words the game would have used, before
anything is dealt:

```
> dotnet run -- play 9
Error: 9 players? The game takes 2 to 5.
```

[Argu](https://fsprojects.github.io/Argu/) owns the other half: a command line as a
*value*, in [Launch.fs](src/Console/Launch.fs). That exists because the program has to
**write** one. A player whose console drops off a networked table is shown the line that
brings them back to their seat, and that line is a command line the program will later
be asked to read. Written by hand it is a second spelling of the command surface, free
to drift from what the shell accepts; written from Argu's own declaration, it is
generated by the same thing that parses it.

The two are pinned together by [cli.fsx](tests/cli.fsx): every line `Launch` can write
is fed to the real `Shell.describe` - not a copy of it - and has to come out the far end
as the launch that went in. Rename an option on one side and that check fails.

It is the same bargain the record keeps, one level out. A move is written in the words
the prompt takes, so a record always replays; a session is written in the words the
shell takes, so an instruction the program prints is always one it will accept.

Everything that reads a command line stops at a `Launch` and hands it on, so there is
one place that knows what opening a game involves rather than a road through `main` per
entry point. With no arguments at all, the menu ([Menu.fs](src/Console/Menu.fs)) asks:

```
=== TCModel ===

  Stones on a map, and a seat each. How many are playing?

    2  3  4  5             deal a game for that many, round this keyboard
    serve <players>        the same, but played in a browser on this machine

  Or, against the program:

    vs <skill>...          one seat each - the machine plays easy, medium, hard

  Or, to play from separate machines:

    host <players>         open a table and wait for them to arrive
    join <address>         sit down at a table someone else is hosting

  Or:

    <players> <seed>       the same game again, from a seed
    replay <file>          play a saved record again
    view <plain, rich>     how the board is drawn - now plain, plain text, and nothing this terminal has to understand
    colours                which colour is drawn for what
    rules                  the rules and the commands, at length
    quit                   leave
```

A bare number is the answer to the question the menu asks, so it needs no command
word in front of it. The seatings on offer are read off `Table.MinPlayers` and
`Table.MaxPlayers` rather than written out, so the menu cannot come to offer a
number the table would refuse. `vs` does not ask how many are playing, because
saying who you are playing has already said it: one seat for you and one for each
machine named, so `vs easy hard` is a table of three. Like the rest of the console layer `Menu` is pure -
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

Colours are `r`/`red`, `b`/`blue`, `g`/`green`; regions are numbered as shown on
the board. So `battle green 2 blue` places a green stone in the Axe and drives one
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
dotnet fsi tests/solo.fsx       # the game at one keyboard: what a line does, and what it
                                #   asks written down
dotnet fsi tests/view.fsx       # that no view shows a player anything they should not see,
                                #   and that changing the colours changes nothing else
dotnet fsi tests/html.fsx       # that the page is well-formed, lands where it is aimed,
                                #   and has no control on it the game would not take
dotnet fsi tests/cli.fsx        # the command surface, and that both halves of it agree
dotnet fsi tests/properties.fsx # the invariants, over games FsCheck thinks up itself
dotnet fsi tests/rival.fsx      # the seat the program plays: that it plays legally, that it
                                #   plays fairly, and that the skills mean something
```

And one that is not a script, because it needs a browser:

```powershell
pwsh tools/smoke.ps1                  # play the game in a real browser, and say whether it worked
pwsh tools/smoke.ps1 -Rival medium    # the same, with the machine in the second seat
```

Everything in `tests/` checks what the program **writes**. [smoke.ps1](tools/smoke.ps1)
checks what a browser **does** with it, which is a different question and the one that has
already been got wrong: a page can be well-formed, carry every attribute it should, draw a
board, and have not one working control on it. Nothing that reads markup can tell you that.
A click can. So it opens a headless browser, waits for the board to arrive over the stream,
then types a line and presses send, presses Enter in the box, clicks a region, and asks why
a region is ruled as it is — checking after each that the game moved. That last one earns
its place: the working behind a ruling is written text rather than elements, and a newline
is what separates one instruction from the next on the way to the browser, so it is the
screen that would arrive in pieces if the stream's framing were wrong.

It then opens a **second** console at the same served game and moves there. Two consoles in
one browser takes two cookies, and a cookie belongs to a host name rather than to a port -
so `localhost` and `127.0.0.1` reach the one table as two different callers. That is what
makes the last check possible: everything the stream has carried until then has been a
piece of the page, landing where the element of its id already was, and a nudge is not a
piece of anything. See [When the turn comes round](#when-the-turn-comes-round).

`-Rival` serves the same game with the program in the second seat, and checks the two things
about that which only a browser can show: that the page is told whose seat it is, and that
the machine's own move arrives down the stream without the page going and asking for it.

It wants Edge or Chrome on the machine, which is why it is not in CI. Run it after touching
anything the browser reads.

Each script exits non-zero on failure. They load the source directly, so they run
without building the console app. `history.fsx` reaches up through the App layer to
the transcript reader and writer, which is what lets it check a whole game out to
text and back again.

### Examples, and invariants

All but the last two play games somebody thought of.
[properties.fsx](tests/properties.fsx) is the other kind: it uses
[FsCheck](https://fscheck.github.io/FsCheck/) to deal from an arbitrary seed and throw
an arbitrary string of moves at the rules - legal, illegal, out of turn, mid-negotiation,
after the game is over - and asserts what has to be true of whatever comes out:

- every colour still has all 21 of its stones
- a move the rules refuse leaves the position untouched
- whoever rules a region is holding as many stones there as anyone
- a player sees their own bag, the size of everyone else's, and nothing more
- **a game written to a file and read back is the same game, state for state**
- taking the last move back and making it again leaves the game where it stood

The generated moves are deliberately *not* filtered for legality; roughly a quarter of
them carry, which is about what a person at a prompt manages. When one fails, FsCheck
cuts moves out until it has the shortest game that still fails.

That last property is the one to keep. It is the promise the whole design rests on, and
before this it was checked on one nine-move game.

[rival.fsx](tests/rival.fsx) is the third kind. It generates nothing and poses almost
nothing: it sits machines down opposite each other and plays whole games out, because the
things worth knowing about a machine at a seat - that it never asks for a move the rules
refuse, that its turn always passes, that it plays a game rather than negotiating one away,
and that `hard` really does beat `easy` - are things that only a game from deal to verdict
can answer. That a table of machines finishes at all is checked by the script reaching its
next line: a turn that never passed would hang rather than fail.

The suite earned its keep on the first run. It shrank a failure to two moves - `recruit`,
`undo` - and the finding was that the check itself was wrong, not the game: when `undo`
is refused at the deal, a `redo` left over from an earlier undo carries the game
*forward*. That is `redo` keeping its own promise. The property now says which case it
means rather than quietly covering both.

## Tooling

```powershell
dotnet tool restore              # once
dotnet fantomas src tests        # format
dotnet fantomas --check src tests # or just say what is unformatted
```

[Fantomas](https://fsprojects.github.io/fantomas/) is pinned in
[.config/dotnet-tools.json](.config/dotnet-tools.json), so everyone runs the same
formatter, and it is configured in [.editorconfig](.editorconfig). Three settings there
are not defaults and are worth knowing about:

- `fsharp_experimental_keep_indent_in_branch` - this codebase answers the awkward cases
  first and then carries on at the same indentation. Without it every such body is
  pushed a level right, and the files that lean on it hardest are the ones flattened
  that way on purpose.
- `fsharp_max_if_then_short_width` / `..._if_then_else_short_width` - a one-line `if` is
  one thought and stays on one line.
- `max_line_length = 130` - the comments here are prose and run to the margin.

[.github/workflows/build.yml](.github/workflows/build.yml) runs the build with
`-warnaserror`, the format check, every test script, and one more thing: it replays the
oldest committed record. That file is the oldest thing in the repository that still has
to work, so the build says so out loud.

### What it depends on

| | |
| --- | --- |
| [Spectre.Console](https://spectreconsole.net) | the `rich` view's panels, tables and charts |
| [Spectre.Console.Cli](https://spectreconsole.net/cli/) | the command surface: `play`, `serve`, `host`, `join`, `replay` |
| [Argu](https://fsprojects.github.io/Argu/) | a command line as a value, so the program can write one |
| [Falco.Markup](https://github.com/FalcoFramework/Falco.Markup) | the `html` view's elements |
| [Falco.Datastar](https://github.com/FalcoFramework/Falco.Datastar) | the client's attributes, its stream frames and its signals, so none of those spellings are this repo's to remember |
| [FsCheck](https://fscheck.github.io/FsCheck/) | the generated games in `properties.fsx` (a test-time reference) |
| ASP.NET Core + SignalR | the host, its hub, and the streams held open to browsers |
| `assets/datastar.js` | [Datastar](https://data-star.dev) 1.0.2, committed and embedded rather than fetched |

`Spectre.Console.Cli` is pinned to `0.51.1` to match `Spectre.Console`; taking its
newest would quietly bring a newer rendering library along with it, under `Rich`.

Nothing here needs npm, node, or a build step. The one piece of client-side anything is
that committed `.js`, and it is served by the same process that hosts the game.

`Falco.Datastar` brings `Falco` itself along, and its routing is not used — the endpoints
are mapped onto ASP.NET directly in [Server.fs](src/Net/Server.fs), because a hosted table
needs SignalR's `MapHub` beside them regardless. That is a known cost, paid for everything
else the package carries: the attribute vocabulary, the stream's frames and the signals
are all spellings this repo would otherwise be remembering by hand, and it had already got
one of them wrong.
