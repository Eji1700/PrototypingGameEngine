# TCModel — two games, one engine

**Turncoats** is the game this was built for. Two to five players draw bags of coloured
stones and place them into fourteen regions. Nobody commands a faction - the stones are
red, blue and green whoever holds them - so the game is settled twice over: the faction
ruling the most land carries the board, and the player left holding most of that colour
carries the faction. A bag played out to nothing wins nothing.

TCModel is the *program* it is played in, and there are two games in it now. The other is
noughts and crosses, and the reason for it is below.

It is a stone-placement game built as a Model-View-Update loop in F#. The core is pure:
`Setup.deal` deals a game from a seed and `Update.update` folds a `Msg` into the
next `Model`. Nothing below the table layer knows a screen exists, and every
screen a player reads goes through a `View` - so how the game looks is swappable,
and it is: `plain` writes blocks of text, `rich` builds panels and charts, and two
players at one networked table can each pick their own.

Seats can be played by the program rather than by somebody in the room. A machine
at a seat is held to what a player is held to: it asks the rules what they will
take rather than keeping its own opinion, it reads the map and its own bag and
nothing else, and it picks a `Move` - the very thing a typed line turns into.

**Why a second game.** Noughts and crosses is not a feature - it is the only honest test of
the claim the machinery makes about itself. About four fifths of this program is not about
stones: a history to walk back through, a record that replays, seats and tokens, a machine
to play one of them, three ways of drawing a board, a table over a wire. All of it was
extracted on the grounds that it is generic, and a claim like that cannot be tested by the
game it was extracted from. So there is a second one, at a fraction of the size, going
through the same two seams and getting the same machinery for nothing - and [what that
turned up](#what-a-second-game-found) is the most useful thing in this file.

```powershell
dotnet run                      # asks which game, then that game's own menu
dotnet run -- tictactoe play 2  # or say which, and everything after it is read by that game
```

**Playing** — [Running](#running) · [Rules as implemented](#rules-as-implemented) ·
[The map](#the-map) · [The four actions](#the-four-actions) · [Ruling](#ruling) ·
[Winning](#winning) · [Ending the game](#ending-the-game)

**At the table** — [Taking it back, and writing it down](#taking-it-back-and-writing-it-down) ·
[Who knows what](#who-knows-what) ·
[How the board is shown](#how-the-board-is-shown) ·
[Playing from different machines](#playing-from-different-machines) ·
[Playing against the program](#playing-against-the-program)

**The code** — [How it is put together](#how-it-is-put-together) ·
[Tests](#tests) · [One file](#one-file) · [Tooling](#tooling)

## Running

```powershell
dotnet run                     # which game, then that game's own menu: how many are playing,
                               #   then who is in each seat
                               #   (arrows or wasd to move, a number to pick)
dotnet run -- tictactoe        # or say which, and go straight to that game's menu
dotnet run -- tictactoe play 2 # everything after the name is read by that game
dotnet run -- tictactoe serve 2 --rival hard   # nine buttons in a browser, against a machine
                               #   that cannot be beaten

dotnet run -- turncoats play 3 # or name it; a line that names none means the first game

dotnet run -- play 3 --seed 42 # the same game again, from a seed
dotnet run -- play 2 --view rich --colour blue=teal

dotnet run -- play 2 --rival medium              # the seat after yours, played by the program
dotnet run -- play 4 --rival easy --rival hard   # once per seat you are giving away

dotnet run -- serve 3          # the same game, played in a browser on this machine
dotnet run -- serve 2 --rival hard

dotnet run -- replay logs/2026-08-02-215823-2p-seed42.log

dotnet run -- host 3                        # open a table at their own machines
dotnet run -- host 3 --open                 # ...with no word at the door, for a room you trust
dotnet run -- join greg-pc --code <word>    # sit down at one someone else opened
dotnet run -- join greg-pc --token <token>  # come back to the seat you were in
                                            # or open the address it prints in a browser

dotnet run -- host 3 --behind --at stones.example.org   # https ends at a tunnel or proxy
dotnet run -- host 3 --cert stones.pfx --cert-password <pw>  # ...or is held here

dotnet run -- --help           # every command; --help works on each of them too
```

Everything below this line is about **Turncoats**, the game of stones. Noughts and crosses
takes the same commands - `play`, `serve`, `host`, `join`, `replay`, `--rival`, `--view`,
`--colour`, `undo`, `save` - because none of them are this game's, and it has a `help` of
its own for the four words that are.

Once a game is dealt, everything else is typed at the prompt:

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
map that lies. [actions.fsx](tests/actions.fsx) checks the same list is empty.

The Flag and the Axe are drawn below the map in the same hand, standing clear of it
and of each other — sharing no side with anything, they border nothing.

No two homes border each other, and every home is three steps from every other,
whether or not the dead region is passable.

Rules that use adjacency can be written against `Board.areAdjacent`,
`Board.neighbours` and `Board.reachableFrom` (which takes a set of blocked regions,
ready for the dead region to obstruct movement).

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

**The timeline** ([Timeline.fs](src/Engine/Timeline.fs)) is a zipper: the deal, the
moves made since, and the moves taken back. The present is wherever the finger
points. `undo` and `redo` only move the finger, so no state is ever rebuilt and
none is ever lost.

Because a session is a value and the generator travels inside it, undoing carries
the generator back too. A negotiation taken back and made again **draws the same
stone**. Undo is going back in time, not rolling again, and there is no way to
fish for a better draw.

**The journal** ([Journal.fs](src/Engine/Journal.fs)) is append-only and never
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
([Knowledge.fs](src/Games/Turncoats/Rules/Knowledge.fs)) is what they are shown:

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
worded around in [Words.fs](src/Games/Turncoats/Rules/Words.fs):

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

## How the board is shown

A **view** ([View.fs](src/Table/Parts/View.fs)) is every screen a player ever reads.
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
one block of text, `rich` ([Rich.fs](src/Games/Turncoats/Reading/Rich.fs)) builds it out of Spectre's
panels, tables and charts - every region a panel bordered in the colour of whoever
rules it, bags drawn stone by stone, a closed bag drawn as the row of stones nobody
can name, land ruled as a bar chart, what is out of sight as a breakdown - and `html`
([Html.fs](src/Games/Turncoats/Reading/Html.fs)) builds the same things out of elements.

**The three factions are Red, Blue and Green**, written `R`, `B` and `G`. Each is
drawn a good deal brighter than the flat version of its own colour, because a stone
in plain blue is barely there on a dark screen. Because a colour's name and its glyph
now begin with the same letter, one lookup serves both: `Gx4`, a lone `G` on the map,
the word "Green" in a sentence, a bar in a chart and a region's border all follow from
[Tint.fs](src/Table/Parts/Tint.fs) and `Words.glyph`. The reader's own seat is marked in
gold rather than a fourth hue that could be mistaken for a stone.

### Colours a player chooses

Which colour is drawn for what is a **palette** ([Palette.fs](src/Table/Parts/Palette.fs)),
and `colours` at the menu opens the screen that changes one:

```
     1  red     Red   R R   >R          crimson   Red stones, and the regions Red rules
  -> 2  blue    Blue   B B   >B         teal      Blue stones, and the regions Blue rules
     3  green   Green   G G   >G        moss      Green stones, and the regions Green rules
     4  yours   (you)   ->              gold      your own seat, and whose turn it is
     5  hidden  dead                    slate     what is held back from you, and ground nobody may enter
     6  reset   put them all back
     0  done    back to the menu

  Left and right walk the one marked -> through the colours, or say 'blue teal' to
  name one outright.
```

Five things take a colour and nineteen colours are on offer, each with a short word of
its own rather than Spectre's `mediumpurple1`. The five live in one list, so the screen
that offers them, the line that changes one and the two halves of sending a palette down
a wire cannot come to disagree about what there is.

The samples in the middle column are not written out for the screen: they are the board's
own words - a stone's glyph, `>R`, `(you)`, `dead` - and the screen is shown through the
very view built in the palette it is offering. So `Tint` colours them exactly as it will
colour the board, and choosing is looking.

Left and right walk the marked slot through the nineteen, and the sample beside it changes
under the cursor as they do. Nothing is remembered between presses: what a slot is drawn in
now is in the palette the screen was built from, so the step is read off that and the line
the press stands for - `red ember` - says the whole of the change. It goes through the same
`Options.choose` a person typing those two words would have reached, the screen comes
straight back in the new palette, and walking right round the list arrives back at the
colour it set out from.

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

**What a view may not do is decide anything.** Three rules keep that honest:

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
- **What a screen *says* comes from `Render` and `Words`**, and what is left for a view
  is where to put it. The notes that explain the board are `Render.Notes`, one paragraph
  each; what the blocks are called is `Render.Blocks`; the table still filling up is
  `Render.Filling`; the supply's three labels are `Render.Supply`; marking the reader's
  own seat is `Words.seated`; saying who rules a region in words is `Words.rule`. Each
  view decides only how it is drawn - `plain` shouts a block name and wraps a note to
  the width of its map, `rich` writes the name into a panel's top wall, `html` hands the
  paragraph to a browser and lets it wrap.

  This is the seam that goes wrong quietly, and it had: the same border was a "side" in
  one view and a "wall" in another, one view called the dead region wild, `rich` had two
  sentences for an empty record and used the wrong one on the board, and of the four
  notes two were shown by one view and silently missing from the others. Nothing failed.
  Every board still drew.

  So [view.fsx](tests/view.fsx) sweeps `View.all` and holds every view to the same
  words: each note shown and each one gone when the notes are off, every block present,
  and the waiting screen saying where each seat stands, how many are still to come and
  which seat is the reader's own. That sweep is what found `rich` drawing the Flag and
  the Axe without ever naming the block they are in.

**The map is drawn three times, and all three say the same thing.** `plain` draws
the honeycomb by counting characters into columns; `rich` gives every region a panel
of its own, bordered in the colour of whoever rules it; `html` gives it a box, bordered
the same way.

What matters is that none of them loses adjacency. The lattice itself is described
under [The map](#the-map); what each view has to keep is the half-region offset,
because that is what makes a region touch six others rather than four, and the map is
the only part of the screen that says where a player may march. A honeycomb shows the
offset with cut corners, brickwork shows it with the offset alone, and either is
faithful. A tidy grid with the offset dropped is not, which is why `rich` and `html`
both keep it - in `html` it is a `margin-left` of so many half-regions and nothing else.

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
dotnet run -- host 3                        # opens a table for three and waits
dotnet run -- join greg-pc --code <word>    # each of the others, from their own machine
```

At the menu it is a seating with anybody joining in it — `play you hard joins` opens a
table for three, plays the middle seat here, takes the first seat from this very process,
and waits for the last one. The word at its door, the port and everything else about how
far it reaches are settled on [the screen behind the seat
list](#the-other-question-asked-the-same-way-how-far-it-reaches).

The host prints the addresses it can be reached at, the word at its door, and the whole
line each of the others types — that line is written by `Launch.write`, so what somebody is
told to type is something the program is certain to accept. A player says a machine name,
an address, or a whole URL; the port and the path are filled in
([Reach.fs](src/Table/Parts/Reach.fs)). Nobody plays until every seat a person has is taken - a
game dealt for three hands out three bags whether or not three people have arrived, so
starting early would mean somebody playing a bag that is not theirs. A seat the program
plays was never empty and is not waited for.

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

### Further than a network

A table on a network everybody in the room is on is guarded by the room: whoever can reach
the address was invited. Hosted where players are in different places, it is guarded by
nothing, and three things that were fair assumptions on a LAN stop being true.

**Anybody who finds the port can take a seat, and a seat once taken is kept.** That is not a
bug to fix at the far end — it is the rule that lets a player close their laptop and come
back to their own stones, and there is deliberately no move for standing somebody up again.
So a stranger has to be stopped at the door, and the door is one word:

```
  The word at this table's door:  82xv-33yd-cx7w

  2 are somebody else's, from their own machines. Each of them runs:

    dotnet run -- join https://stones.example.org --code 82xv-33yd-cx7w

  or open https://stones.example.org/?code=82xv-33yd-cx7w in a browser.
```

Twelve letters from the machine's own source of randomness — not from `Rng`, which is
random the way a deal is random and would tell anybody holding the seed what came next.
No `o` or `0`, no `l` or `1`, because it gets read out loud as often as it gets copied, and
the case and the dashes are forgiven when it comes back. Words are held up against each other
a letter at a time whatever they are, so how long the answer took says nothing about how
much of it was right.

**A table gets one whether or not anybody asked**, and `--open` is how you say a room you
trust. That way round because the failure is silent and one-way: nobody notices the open
door until somebody is sitting in their seat.

It travels the one way each kind of console can manage. A browser carries it in the address
the first time and is handed a cookie for everything after — its client, its stream, every
line it types. A console at a terminal has no address bar and no cookie jar, so it sets a
header on every request it makes. One piece of middleware in front of the whole table reads
all three, because the ways in are not all pages: a terminal arrives at the SignalR hub,
which no page routing would have covered.

Somebody who turns up at the front door without it gets a page with one box on it rather
than a number — they are a player who was sent an address, and `403` is not an instruction
anybody can act on.

**And a stranger who keeps guessing is slowed down.** Fifty-nine bits is past guessing in
any case, but "past guessing" is a weaker sentence than a bucket, and the bucket is five
lines: `System.Threading.RateLimiting`, which is already in the shared framework. Ten wrong
answers, then one back every five seconds — a person who mistypes a word twice, or a browser
that fetches three things before it has been handed a cookie, never notices.

Two buckets, because one of them can be got round. The first is per caller, and past a
tunnel that is the address the tunnel *says* it came from — which anybody who can reach the
machine directly is free to make up, and by making up a new one each try would have a fresh
bucket every time. So the second counts the door itself, however many addresses the tries
arrive from.

**Only wrong answers are counted, which is what makes counting them safe.** A player who has
the word never touches either bucket however fast they play, so nothing here can come between
somebody and a game they were invited to. What it costs when a bucket is spent is that
somebody arriving with the *wrong* word is told to wait rather than shown the box to type it
into. Somebody arriving with the right one is let in regardless — [smoke.ps1](tools/smoke.ps1)
gets that wrong fourteen times in a row and then gets it right from the very same address, to
say so.

**Everything crosses in the clear over http**, which at this game means the boards going
past are somebody's stones. Two ways out, and the second is the one most people hosting one
of these actually have:

| | what it means |
| --- | --- |
| `--cert <file.pfx> [--cert-password <pw>]` | https ends here; Kestrel is bound with the certificate |
| `--behind` | https ends at a tunnel or proxy in front, which forwards plain http to this |
| `--at <address>` | what to tell players, when it is not this machine's own name |

`--behind` is not `--cert` with the checking off. It changes what this process believes
about the player at the far end: the forwarded headers are read, so `Request.IsHttps` is the
browser's answer rather than this socket's, and the cookies a table hands out are marked
`secure` exactly when the browser is on a connection that will send them back. Guessing
either way breaks something — marked secure over plain http the cookie is dropped and the
player loses their seat on every reload.

The two schemes are kept apart on purpose. What a player types is `https://stones.example.org`;
what is listening *here* is `http://localhost:5000`, because the encryption ended at the
machine next door. Both are printed, each labelled with who it is for.

**Give the table a hostname of its own, not a path under one.** A page fetches its client,
its stream and every line it types from the root — `/stream`, not `../stream` — so
`https://example.org/stones/` behind a proxy serves the first page and then nothing else,
while `join https://example.org/stones/table` from a terminal works fine, because a console
is given the whole path and keeps it. A subdomain, or the hostname a tunnel hands you, and
the split does not arise. Fixing it properly means `UsePathBase`, `X-Forwarded-Prefix` and
relative URLs in [Html.fs](src/Games/Turncoats/Reading/Html.fs); it is written down rather than done because
nothing yet needs this table to share a hostname with anything.

**A wire between houses goes quiet, and then goes away.** Three defaults were written for
requests that take a moment, and this is a socket held open across a game that takes an
evening:

- **A stream with nothing on it looks idle**, and a tunnel or proxy will close an idle
  connection inside a minute — while a turn-based game between people who are thinking is
  idle by nature. So the table says something harmless down each page's stream every fifteen
  seconds. It is a script rather than an SSE comment for the second half of the reason: a
  comment keeps the wire warm but the page cannot *see* one, and a connection that has
  silently stopped arriving is indistinguishable from a game where nobody has moved.
- **So the page counts the silence.** Four missed beats and it says so, then tries the
  address every few seconds until something answers and reloads. A reload loses nothing —
  the board is drawn at the table, the seat is remembered by the cookie — which is why it is
  the right answer rather than a crude one. The stream is also asked to stay open while the
  tab is hidden, without which being told your turn came round while you were elsewhere
  could not work at all: being elsewhere is the state this game is played in.
- **A console at a terminal waits longer and tries forever.** Sixty seconds before deciding
  the table has gone, and a retry policy that backs off to half a minute and then stays
  there — because the seat is *kept*, nobody else may take it, and the game will wait all
  evening. A line typed while the wire is down says so and stays typeable, rather than
  taking the process down with it.

**What this is not.** There are no accounts and no per-player secrets: one word lets you
into the table, and which seat you get is the table's business. The thing that identifies a
player afterwards is the seat token, which is minted per seat and is what `--token` and the
browser's cookie carry. A game is worth exactly this much ceremony and not more.

Where it is checked: [reach.fsx](tests/reach.fsx) holds the door and the address filling as
values, without opening a port — the deciding is `Reach.admits`, which takes a list of
whatever was presented and answers yes or no. [cli.fsx](tests/cli.fsx) writes every one of
these options out and reads it back through the real command surface, which is how
`--cert-password` was found to be spelt two ways by the two libraries. And
[smoke.ps1](tools/smoke.ps1) drives a real browser at a real locked table: turned away
without the word, slowed down for guessing at it, seated with it, and still hearing the
table's heartbeat a whole interval later. The buckets are the one part of this that is not a
value — a limit is a thing about time, not about a table — so a socket is the only place it
can honestly be asked about.

### Two kinds of table

There are two, and they are the same shape: hand one a typed line and it gives back the
next table and a list of things to show, each addressed to somebody.

| | [Solo.fs](src/Table/Playing/Solo.fs) | [Lobby.fs](src/Net/Lobby.fs) |
| --- | --- | --- |
| who is at it | one pair of hands, however many are watching | one seat each |
| seats the program plays | any of them | any of them, and they are not waited for |
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
down at that table. Both take the same flags for how far they can be reached and what it
takes to sit down — a port, a door, a certificate — because a page on this machine and a
table people join are the same problem the moment either is further away than a room.

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
| the word at the door | a header on everything it sends | the address once, then a cookie |
| when the wire goes | SignalR reconnects, and it re-takes its seat by token | the table's heartbeat stops, and the page tries the address until it answers, then reloads |

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

They do not agree everywhere, and the stream is where it shows. The page asks for one
request option — that its stream stays open while the tab is hidden, without which the turn
arriving could not reach anybody who was elsewhere — and that one crosses intact. The knobs
for how hard the client should *retry* a broken stream do not: the library drops `retry`
entirely and spells the wait `retryMaxWaitMs` where the carried client reads `retryMaxWait`.
So the page does not lean on them. It keeps its own watch instead, which it would have
wanted anyway: neither knob covers a stream that ended tidily, or one that stopped arriving
without saying so.

Two small deviations from what the library emits, both in [Html.fs](src/Games/Turncoats/Reading/Html.fs)
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

**All four actions are on the map.** A region offers a recruit in each colour, and where
it holds stones a second row offers what can be done with them: `×R` battles with a Red
one and drives out as many as it may, `R→8` marches a Red one into 8. Both are whole
lines, which is what let them off the prompt: a battle with no casualties named drives
out all it can, and a march with no count moves one - so each is a colour and one or two
region numbers, and a region knows all of that about itself.

That second row is filtered by what is actually standing there and the first is not,
which is the same rule read twice rather than an inconsistency. Recruiting a colour you
do not hold is a fair thing to try and the table answers it in words; a battle or a march
of a colour that is not in the region is not a move at all, and a button refused every
time it is pressed is not a button.

It also needed a check the parser cannot give. `march r 5 12` is a perfectly well-formed
line - `Parse.line` takes it happily - and it is the *rules* that refuse it, 5 not
bordering 12. So a button offering it would pass every check above. `html.fsx` asks a
different question of these two: not "does it parse" but "is this the exact set the
position allows", worked out from the board rather than from the markup.

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
dropping every one of these. So [smoke.ps1](tools/smoke.ps1) sits a second
console at the served game — a cookie and a held-open stream, no browser involved — says one
line there, and asks the page whether it was knocked on.

## Playing against the program

Any seat can be played by the program. At the menu that is the seat list above — walk a
seat along to `easy`, `medium` or `hard`, or say the whole table at once:

```
play you hard          # you and one machine
play medium you        # and the machine may have the first seat
play easy you hard     # a person between two of them
play you hard joins    # you, a machine, and a friend at their own machine
```

The command line names the machines rather than the seats, which is a seating said shorter
— `--rival` gives away the seat after yours, once per seat:

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
asks [Actions.fs](src/Games/Turncoats/Rules/Actions.fs) - the same functions `Update` asks - and
takes the answer. A machine that worked legality out for itself would be a second
opinion about the rules, free to drift from the ones being played, and the way that
shows up at a table is a machine asking for something, being told no, and asking
again with the turn never passing. Over two whole machine-played games,
[rival.fsx](tests/rival.fsx) insists not one thing it asked for was refused.

**It reads the map and one bag.** The function that weighs a position takes a
`Position` and a `Pile`, which is the whole of what somebody at that seat can see.
There is nothing in the arguments to cheat with. Working out how the land stands from
a position alone is what [Ruling.fs](src/Games/Turncoats/Rules/Ruling.fs) does, so this is the game's
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
two knobs, all at the foot of [Rival.fs](src/Games/Turncoats/Rules/Rival.fs):

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

There is one rule, and it lives in `Rival.answering`: after a person has spoken, the
machines answer for as long as the seat to act is one of theirs. What stops them is a move
that left the game exactly as it found it - nothing they pick should ever be refused, but a
machine that had somehow found one the rules would not take would otherwise be asked for it
again, and again.

The same rule read backwards is what `undo` does. Taking a move back takes the
machine's answer back with it, and stops where a person has to decide something;
anything else and one `undo` would hand the turn straight back and nothing would ever
be undone.

Everything else follows from a machine's move being an ordinary move. It goes through
`Update.update`, lands in the record against its own seat, is drawn to everybody
watching, and reaches a browser down the same stream a person's does. There is no
second table and no second protocol.

Which is why a hosted table ([Lobby.fs](src/Net/Lobby.fs)) can have machines at it too,
without a second answer to when they play: it calls the same loop. What it adds is the one
thing a table with seats has to say for itself — such a seat holds the occupant `Played`,
which was never empty, so nobody waits for it and nobody can sit down in it. The machines
play as the last person arrives rather than before, because a game dealt for four is not
begun until four are sitting at it.

## How it is put together

Everything above is what the game does. What follows is how it is arranged so that it
keeps doing it: where each piece lives, what the types refuse to let anyone write, and
the one place a command line is read and written by the same declaration.

### A game-shaped hole

Most of this program is not about stones. A history to walk back through, a record that
replays, seats at a table, a machine to play one of them, a screen per player, a wire
between them — none of that is a fact about this game, and all of it took longer to get
right than the rules did.

So it is compiled *before* the games and knows nothing about them. `src/Engine` mentions no
type in `src/Games` and never will: the whole of what it knows is one record.

```fsharp
type Rules<'Move, 'State, 'Notice> =
    { Deal:   int -> uint64 -> Result<'State, string>
      Play:   'Move -> 'State -> 'State option * 'Notice list
      Active: 'State -> PlayerId
      Turn:   'State -> int
      Over:   'State -> bool
      Seats:  'State -> int
      Reseed: 'State -> uint64 }
```

Seven questions. Answer them and you get the timeline, the journal, undo and redo, the
record on disk, the table at one keyboard, the table over a network with seats and tokens
and reconnection, three views, a browser, a machine opponent, and a command line — without
writing any of it.

**`Play` cannot fail, and that is the load-bearing line.** A refusal is something the game
*says* — `None` for the state, and notices saying why — rather than an error the machinery
has to handle. That is why `Update.update` is total, and why every table above it is a fold
that [lobby.fsx](tests/lobby.fsx) can check without a socket. A game that returned
`Result` here would push a failure case into every screen in the program.

**Things moved both ways to make the seam honest.** Down into the game went everything the
old middle layer had quietly accumulated about *this* game: the phase a turn is in, the run
of negotiations that ends it, what a turn even is. A turn that stays open until a stone goes
back is nobody else's rule, and it now sits in [Turn.fs](src/Games/Turncoats/Rules/Turn.fs) beside
the rest of them. Up into the engine went `PlayerId` — which seat, said without saying who
is in it — because the record, the tables, the wire and the screens all speak it and none of
that is about stones.

**And the game names its own shapes once**, so nothing above carries three type arguments
about:

```fsharp
type Msg   = Msg<Move>
type Told  = Told<Move, Notice>
type Model = Model<Move, Session, Notice>
```

[Playing.fs](src/Games/Turncoats/Rules/Playing.fs) is the other half of plugging in: the engine's
own machinery with these rules already bound into it.

### The second seam

`Rules` answers how a game is *played*. It held. What did not exist was a seam for how a
game is *read* — seventeen files above the game reached into it by direct module reference,
so every screen, menu and command line in the program was bound to stones. `Playable` is
that second seam, and it lives at the top of [src/Table](src/Table/Playable.fs):

```fsharp
type Playable<'Move, 'State, 'Notice> =
    { Rules:   Rules<'Move, 'State, 'Notice>
      Name:    string;  Title: string;  Blurb: string
      Fewest:  int;     Most:   int
      Read:    string -> Result<Command<'Move>, string>   // this game's own words
      Write:   Msg<'Move> -> string                       // what a record is made of
      Seat:    PlayerId -> string
      Says:    'Notice -> string
      SeenBy:  PlayerId -> 'Notice -> string
      Resign:  'Move option
      Faults:  string list
      Slots:   Slot list                                  // what takes a colour
      Skills:  (string * string) list
      Seating: uint64 -> string option list -> 'State -> (PlayerId * Seated<'Move,'State>) list
      Page:    Shell
      Views:   Palette -> View<'Move, 'State, 'Notice> list }
```

Fill both records in and everything above is already written: the timeline, the record on
disk, the seats and their tokens, the menu and the seat list, the colour screen, the command
line, the wire, the browser, and the machine loop.

Three things had to change for it to fit. **`View` lost its game types** — `Board` took this
game's `Player` and `Ruling` took this game's `RegionId`, which is what kept every screen
bound to one game; a seat is a `PlayerId` now, and anything else a game can be asked about
arrives at `Answer` as the words that were typed. **`Command` split off** — `help`, `quit`,
`undo`, `save`, `view rich`, `restart` mean the same thing whatever is on the board, so they
are read once in [Commands.fs](src/Table/Parts/Commands.fs) for every game and a game's own reader
never sees them. **`Palette` became keyed** — the game says what it colours, so the colour
screen, the line that changes one, the form on the page and both halves of sending a palette
down a wire stopped knowing there are three factions.

There is one more seam, at the far end. Two games have different moves, states and notices,
so no list holds both — [Play.fs](src/Play.fs) ends in a plain interface with no type
parameters at all, implemented by closing over a game. That is where the types stop, which
is what makes [Games.fs](src/Games.fs) a two-line list.

### What a second game found

Noughts and crosses is nine files and about 450 lines, of which the rules are 60. It
compiled the first time it was asked to and needed no change to `Engine`, `Table` or `Net`.
What it turned up along the way was worth more than the game:

- **`Solo.walking` compared two game states for equality** to decide whether a move landed.
  Generic code cannot require that — nothing about a game says it has to be comparable. It
  asks the history now, which is what `Machines.answering` had been doing all along for the
  same reason. The engine had solved it; the table had not noticed.
- **Six sentences were the engine talking in the game's voice.** "There is nothing left to
  take back", "the game is over, so there is nothing left to play" — a game with an opinion
  about undo. They live in `Told.inWords` now, and both games' renderers reach the same
  copy. It took writing the second renderer to make copying them a third time obviously
  absurd.
- **`Solo` hard-coded `Make Resign`.** What putting a game down *plays* is the game's;
  that a person may put one down is not.
- **A machine as a recursive closure erases a type parameter.** `Machine<'Move,'State> =
  Choosing of ('State -> ('Move * Machine<...>) option)` means whatever a machine remembers
  between turns — a generator, a plan, nothing at all — stays its own business instead of
  appearing in every table, screen and seating signature above it.
- **The wire broke, and nothing caught it.** Making the table generic turned the SignalR hub
  into a generic type, and a generic type named in a route cannot be tied to the game being
  played: F# infers `MapHub<TableHub<_,_,_>>` as `obj`, the container is asked for a hub it
  was never given, and every console that tries to sit down is dropped without a word. No
  test read that, because every test either folds a value or drives a browser. It was found
  by joining a table, and [wire.ps1](tools/wire.ps1) is that, written down.
- **The line a table reads out was one word short.** `dotnet run -- join <address>` finds
  the table and draws the right board whichever game it is — and then reads every colour
  asked for against the wrong list of them. A second game is what makes a printed
  instruction's missing word visible.
- **`--help` was one game's help, printed for both.** The parser was a module-level value
  with the blurb, the examples and the program name written into it, so
  `dotnet run -- tictactoe --help` said "stones on a map" and offered `--rival hard`
  without saying what the other two were. Half of it is `IArgParserTemplate.Usage` being a
  *static* member — no option can name its own game's views — and the fix is the same shape
  as `Chosen`: one parser per game, with the lists the options point at written by the one
  function that has a game to ask. One of those options had been left naming them anyway,
  and came out as a sentence ending in a colon and nothing at all.

And two places where the seam is wider than it needs to be, left as they are and said out
loud rather than papered over. `View.Answer` exists for Turncoats' `rule 8`; a game whose
whole position is nine squares in plain sight fills it with a line explaining there is
nothing to explain, and nothing can ever reach it. And `Update.start` takes a seed from
every game, including one with no dice at all — so a tic-tac-toe record says `deal 2` and a
number off the clock, and two identical games land in differently-named files. Both cost one
line. Fixing either properly costs a field on the seam, which is worse.

**What is still not done.** A view is handed the whole model and trusted to ask `Knowledge`
what its reader may see — a discipline three renderers keep and [view.fsx](tests/view.fsx)
patrols, where an engine ought to hand a view only what that seat may know and make a leak
unwriteable. The second game is the wrong shape to force it: it hides nothing, which is
itself the useful half of the answer — `Knowledge` is a game's own idea and not something
the screens require.

### Layout

Seven layers, each depending only on the ones above it. **The order in
[TCModel.fsproj](TCModel.fsproj) is the architecture, and the only thing enforcing it**:
F# compiles a project in the order its files are listed, so a layer cannot reach into one
beneath it even by accident. Read it top to bottom — nothing generic mentions a game, and
the games never mention each other.

**`src/Common`** — generic, and knows nothing about the game.

| File | Role |
| --- | --- |
| [Result.fs](src/Common/Result.fs) | The `result` computation expression used to chain an action's checks |
| [Cascade.fs](src/Common/Cascade.fs) | Settling a contest by measures applied in order, shared by ruling and winning |
| [Random.fs](src/Common/Random.fs) | An immutable SplitMix64 generator, passed along as a value |

**`src/Engine`** — what every turn-based game wants and none of them should write again.
It is compiled *before* the game and mentions nothing in it; see [A game-shaped
hole](#a-game-shaped-hole).

| File | Role |
| --- | --- |
| [Seats.fs](src/Engine/Seats.fs) | `PlayerId`: which seat, and nothing about who is in it |
| [Messages.fs](src/Engine/Messages.fs) | `Msg<'Move>`: the game's own move, and the three the engine answers itself |
| [Told.fs](src/Engine/Told.fs) | `Told<'Move,'Notice>`: what the game said, and what the engine said |
| [Rules.fs](src/Engine/Rules.fs) | The seam: the seven questions the machinery asks a game |
| [Timeline.fs](src/Engine/Timeline.fs) | Every state a game has stood in, with a finger on the present |
| [Journal.fs](src/Engine/Journal.fs) | The record of play: what was asked, by whom, and what came of it |
| [Model.fs](src/Engine/Model.fs) | The timeline, the journal, and the last few lines on screen |
| [Update.fs](src/Engine/Update.fs) | `Rules -> Msg -> Model -> Model`, and nothing in it can fail |
| [Machines.fs](src/Engine/Machines.fs) | Seats played by something that is not a person, and when they take their turns |

**`src/Table`** — how a game is *read*, which is the second seam. Nothing here names a
game, a piece, or a board. Two folders and one file between them, and the file is the
point: `Parts` does not know there is a seam, and `Playing` is written against it.

| File | Role |
| --- | --- |
| [Showing.fs](src/Table/Parts/Showing.fs) | What a table shows one console, and which console it is for |
| [Waiting.fs](src/Table/Parts/Waiting.fs) | A seat at a table that has not filled up yet, as the person waiting sees it |
| [Palette.fs](src/Table/Parts/Palette.fs) | Which colour is drawn for what, keyed by the words a game says it colours |
| [Reach.fs](src/Table/Parts/Reach.fs) | How far a table can be reached and what it takes to sit down at one: the port, the word at the door, what it is all wrapped in, and an address as somebody says it |
| [Keys.fs](src/Table/Parts/Keys.fs) | Screens picked from rather than typed at: the rows, where a person has got to, and what a key press comes to |
| [Commands.fs](src/Table/Parts/Commands.fs) | The words a person can type at *any* game, read once for all of them |
| [View.fs](src/Table/Parts/View.fs) | Every screen a player reads, generic in the game |
| [Seating.fs](src/Table/Parts/Seating.fs) | Who is in each seat before a game is dealt, and everything that falls out of it |
| [Tint.fs](src/Table/Parts/Tint.fs) | Colour laid over writing already laid out, and Spectre's output as a string |
| [Page.fs](src/Table/Parts/Page.fs) | The browser's shell: the stream, the prompt, the colour form, the door, and the two places a fragment can land. A game brings a stylesheet and a name for the tab |
| [Screens.fs](src/Table/Parts/Screens.fs) | Driving a screen at a real terminal: clear it, draw it, read a key, hand back a line |
| [Options.fs](src/Table/Parts/Options.fs) | The colour screen: what is drawn in what, and how a person changes it |
| [Playable.fs](src/Table/Playable.fs) | **The seam**: everything a game has to say about itself to be read and played here |
| [Solo.fs](src/Table/Playing/Solo.fs) | The game at one keyboard, as a value: what a typed line does, who answers it, and what it asks written down |
| [Transcript.fs](src/Table/Playing/Transcript.fs) | A journal as a file, and a file back into a journal |
| [Menu.fs](src/Table/Playing/Menu.fs) | The start menu and the seat list: what there is to open, and what a typed line asks for |
| [Launch.fs](src/Table/Playing/Launch.fs) | The command line, both ways round: the commands and their options, what a typed line comes to, what is refused at the door, and the line the program writes when it has to tell somebody what to type |

**`src/Net`** — the same table with the players at different keyboards. Only the last
three files here touch a socket.

| File | Role |
| --- | --- |
| [Protocol.fs](src/Net/Protocol.fs) | Where the table listens, and what each end calls the other |
| [Browser.fs](src/Net/Browser.fs) | A page as a console: the streams held open to them, and what is served. Knows of no table in particular |
| [Lobby.fs](src/Net/Lobby.fs) | Seats, tokens, and the three rules a table adds to the game |
| [Server.fs](src/Net/Server.fs) | The host, and the local game served to a browser: a table behind a lock, with pages and sockets over it, and the door everybody arrives at |
| [Client.fs](src/Net/Client.fs) | A console at somebody else's table |

**`src/Games/Turncoats`** — one game, and inside it the two seams are two folders. `Rules` is
how it is played: no English a player reads is laid out there, no screen, and nothing from
`src/Table` - which is not a house rule but a fact you can check, because not one file in
there opens it. `Reading` is how it is read. `Offer.fs` joins them, and is the only file
either layer above ever sees.

| File | Role |
| --- | --- |
| [Stones.fs](src/Games/Turncoats/Rules/Stones.fs) | `StoneColor` and `Pile`, a multiset of stones |
| [Board.fs](src/Games/Turncoats/Rules/Board.fs) | The fixed map: `RegionId`, the regions, the borders, and the checks that it hangs together |
| [Players.fs](src/Games/Turncoats/Rules/Players.fs) | `Player` and `Table`, a seating of 2-5 with one of them active |
| [Position.fs](src/Games/Turncoats/Rules/Position.fs) | Which stones stand where |
| [Ruling.fs](src/Games/Turncoats/Rules/Ruling.fs) | Who rules a region, and how the land stands - both read off a position alone |
| [Game.fs](src/Games/Turncoats/Rules/Game.fs) | The game in progress, and what can be asked of it |
| [Knowledge.fs](src/Games/Turncoats/Rules/Knowledge.fs) | What one player can see of a game, and what they cannot |
| [Events.fs](src/Games/Turncoats/Rules/Events.fs) | What happened, and why an action was refused |
| [Actions.fs](src/Games/Turncoats/Rules/Actions.fs) | The four actions, each a `Game -> Result<Game * Event, Rejection>` |
| [Outcome.fs](src/Games/Turncoats/Rules/Outcome.fs) | Which faction carries the board, and which player carries the faction |
| [Setup.fs](src/Games/Turncoats/Rules/Setup.fs) | Dealing a fresh game |
| [Turn.fs](src/Games/Turncoats/Rules/Turn.fs) | `Move`, and where a game stands: the phase, the turn, the run of negotiations, and how a turn ends |
| [Playing.fs](src/Games/Turncoats/Rules/Playing.fs) | This game as the engine takes one, and the engine with it already in |
| [Words.fs](src/Games/Turncoats/Rules/Words.fs) | Every string a player reads, including how events and rejections are worded |
| [Rival.fs](src/Games/Turncoats/Rules/Rival.fs) | A seat played by the program: how a position is weighed, and how well |
| [Ink.fs](src/Games/Turncoats/Reading/Ink.fs) | What this game colours, and its alphabet for laying colour over a drawn board |
| [Parse.fs](src/Games/Turncoats/Reading/Parse.fs) | This game's own words as a `Move` - and only those, the rest having been read already |
| [Render.fs](src/Games/Turncoats/Reading/Render.fs) | The `plain` view: every screen as blocks of text |
| [Rich.fs](src/Games/Turncoats/Reading/Rich.fs) | The `rich` view: every screen built from Spectre's panels, tables and charts |
| [Html.fs](src/Games/Turncoats/Reading/Html.fs) | The `html` view: every screen as a fragment of a page |
| [Offer.fs](src/Games/Turncoats/Offer.fs) | Both seams filled in: this game as the engine takes one, and as a table reads one |

**`src/Games/TicTacToe`** — the other one, in the same shape and a fifth of the size.
Worth reading beside the folder above, because that is the whole argument for the seams
being where they are.

| File | Role |
| --- | --- |
| [Marks.fs](src/Games/TicTacToe/Rules/Marks.fs) | `Mark`, and the squares - the runs that win worked out from the side rather than written down |
| [Board.fs](src/Games/TicTacToe/Rules/Board.fs) | What is on the board, and the line somebody holds all of |
| [Session.fs](src/Games/TicTacToe/Rules/Session.fs) | Where the game stands, and which seat plays which mark |
| [Turn.fs](src/Games/TicTacToe/Rules/Turn.fs) | `Move`, and how a turn goes: the square has to exist and be free |
| [Words.fs](src/Games/TicTacToe/Rules/Words.fs) | Every string a player reads |
| [Rival.fs](src/Games/TicTacToe/Rules/Rival.fs) | A seat played by the program: the game walked to its end, with alpha-beta so it answers |
| [Ink.fs](src/Games/TicTacToe/Reading/Ink.fs) | Two colours, against the other game's four |
| [Parse.fs](src/Games/TicTacToe/Reading/Parse.fs) | A number, which on this board is the whole move |
| [Render.fs](src/Games/TicTacToe/Reading/Render.fs) | The `plain` view, and the words the other two borrow |
| [Rich.fs](src/Games/TicTacToe/Reading/Rich.fs) | The `rich` view: walled squares, in colour |
| [Html.fs](src/Games/TicTacToe/Reading/Html.fs) | The `html` view: nine buttons, each typing its own number |
| [Offer.fs](src/Games/TicTacToe/Offer.fs) | Both seams filled in |

**And the way in**, which needs every layer above it — F# compiles in order and a file sees
only what came before it, so the door has to be the last thing built.

| File | Role |
| --- | --- |
| [Play.fs](src/Play.fs) | What opening a game *involves*: dealing one, keeping its record, the menu loops, both tables and the browser - still generic - ending in the interface that seals a game's types off |
| [Games.fs](src/Games.fs) | The games there are, and the only file in the program that names more than one |
| [Program.fs](src/Program.fs) | Which game a line is about, the screen that asks when nothing says, and nothing else |

### Keeping invalid states out

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
  `NothingToDriveOut(region, colour)` and [Words.fs](src/Games/Turncoats/Rules/Words.fs) decides
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

### A command line, both ways round

The command line has to work in two directions, and that is the whole of what shapes it.
Reading one is the ordinary direction: five commands, the options that go with each,
`--help`, and the checking. A count the table would refuse, a view nobody has, a colour
nobody has, an option nobody has - each is answered at the door, in the same words the
game would have used, before anything is dealt:

```
> dotnet run -- play 9
9 players? The game takes 2 to 5.
```

Writing one is the other direction, and it is why [Launch.fs](src/Table/Playing/Launch.fs)
exists. A player whose console drops off a networked table is shown the line that brings
them back to their seat; the host of a locked table is shown the line each of the others
types. Both are command lines the program will later be handed. Written by hand they are
a second spelling of the command surface, free to drift from what the door accepts;
written from the declaration that parses them, they cannot be.

**It was two libraries, and now it is one.** [Argu](https://fsprojects.github.io/Argu/)
does both halves — five nested subcommands, each holding its own options, each with its
own `--help`. It used to write while Spectre.Console.Cli read, which is exactly the
arrangement that lets a name drift: `--cert-password` on one side was `--certpassword` on
the other, and the program printed an instruction its own front door refused. The round
trip in [cli.fsx](tests/cli.fsx) caught it, which is the system working — but a bug that
cannot be written down beats a bug that gets caught, and one declaration is that.

What went with it: a hundred lines of container plumbing that existed only to satisfy the
other library's idea of how a command is built, and the sharp edge that made options
silently vanish from a settings type it could not reflect into. What stayed: the round
trip, which is worth as much as ever. Every line `Launch` can write goes through
`Launch.run` — what `main` calls, not a copy — and has to come out the far end as the
launch that went in.

It is the same bargain the record keeps, one level out. A move is written in the words
the prompt takes, so a record always replays; a session is written in the words the
command line takes, so an instruction the program prints is always one it will accept.

Everything that reads a command line stops at a `Launch` and hands it on, so there is
one place that knows what opening a game involves rather than a road through `main` per
entry point. With no arguments at all, the menu ([Menu.fs](src/Table/Playing/Menu.fs)) asks:

```
=== Turncoats ===

  Stones on a map, and a seat each.

  -> 1  New game         how many are playing, and who each of them is
     2  Join a table     sit down at one somebody else is hosting
     3  Replay a record  a saved game, played through again
     4  How it is drawn  now plain - plain text, and nothing this terminal has to understand
     5  Colours          which colour is drawn for what
     6  Rules            the rules and the commands, at length
     7  Quit

  Move with the arrows or w and s. Enter takes the one marked ->, and so does its number.

  Or type it: a seat each, from you, easy, medium, hard, joins - 'play you hard joins' to deal
  one, 'serve you medium' to read it in a browser, 'seats you you' to lay it out
  first. The short ways still hold: '3' for a game of three, '3 42' for that same
  game again, 'serve 3', 'host 3', 'vs <skill>...' for easy, medium, hard,
  'join <address>', 'replay <file>', 'view <plain, rich>', 'colours',
  'rules', 'quit'.
```

### One question, asked once: who is in each seat

There used to be three ways in — play here, against the program, host a table — and each
asked how many were playing in its own words. That is three doors onto one question, and
between them they could not describe a table with a machine in the *middle* of it, or one
where a friend joins two people already at this keyboard. So there is one door now. It asks
how many first, because the list of seats is exactly as long as the answer:

```
=== Who is playing ===

  Each seat is somebody here, the machine, or somebody at their own machine.

  -> 1  Seat 1             you     somebody at this keyboard
     2  Seat 2             hard    counts the tie-breakers too, and what you could do about it
     3  Deal               and play it here at this keyboard
     4  In a browser       the same game, read as a page
     5  How it is reached  a word at the door, in the clear, port 5000

  Left and right walk the one marked -> through you, easy, medium, hard, joins.
  Enter takes the next one along, and so does the seat's own number.
```

A **seating** ([Seating.fs](src/Table/Parts/Seating.fs)) is one `Sitter` to a seat, in the order
the game deals them: `Here`, `Machine of Skill`, or `Elsewhere`. That is the whole value,
and how many are playing is how long it is — so the count and the seats cannot disagree,
which is the one sum the old menu could get wrong and did: a game dealt for two against
three machines had an empty chair at it.

**Where a game is played is the seating's own answer rather than another question.** Nobody
joining is a game to deal here; anybody joining is a table to open and wait at. So `Deal`
becomes `Open the table` on a seating with a `joins` in it, and `In a browser` stops being
offered — a page on this machine is one hot seat, and there is nobody for a seat at it to be
at the far end of.

Everything shorter is built out of a whole seating rather than beside one, so a shorthand
cannot come to mean something the long way round does not:

| said short | the seating it is |
| --- | --- |
| `3` | `you you you` |
| `vs easy hard` | `you easy hard` |
| `host 3` | `joins joins joins` |
| `serve 2` | `you you`, read as a page |
| `--rival easy` on the command line | `you easy`, at whatever size was asked for |

`Rival.seating` takes one entry per seat too, so nothing between the menu and the table
still believes the first seat belongs to a person. A machine may have seat 1, and the run of
them between one person's move and their next is played by `Rival.answering` — one loop,
used by the keyboard's table and the networked one alike, so a machine at a seat nobody
drove to is the same machine.

A networked table can therefore have a machine in it. `Lobby` gives such a seat the
occupant `Played`: it was never empty, so nobody waits for it and nobody can sit down in
it, and the machines play as the table fills rather than after somebody has read a board.
The host's screen reads the seating back — which seats are the machine's, which are for
somebody at this machine, and which are for somebody at theirs.

**And a seat here at a table you are hosting is taken by the process hosting it.** It used
to be that you opened the table and then joined your own table from a second terminal,
which is a strange thing to have to be told. What happens now is exactly what you would
have done by hand: once the port is answering, a console sits down over the same wire
everybody else arrives on, presents the same word at the same door, and is handed a seat
and a token like anybody. The table cannot tell that the process it is inside is its own —
which is the point, because a seat played by a shortcut would be a second way of sitting
down and there is no room here for two. When that player leaves their seat the table goes
on standing: getting up is not the same as closing the room.

### The other question, asked the same way: how far it reaches

A seating settles who is at the table. The screen behind it settles where the table is and
what it takes to sit down — which the menu had no way of saying at all until it did, so a
table opened from a menu was always in the clear, on the usual port, and told nobody
anything:

```
=== How far it reaches ===

  What it takes to sit down at this table, and what carries what it says.

  -> 1  The door      a word: kbd4-9mtx-7rfp
     2  Carried       in the clear - right on a network you trust, and nowhere else
     3  The port      5000
     4  Tell players  this machine's own addresses
```

It is the seat list's own trick again. A **reach** ([Reach.fs](src/Table/Parts/Reach.fs)) is one
value, and every row on either screen stands for the *whole* of what is being opened after
its own change — a seating and a reach, in one line:

```
seats you joins via port:5000 word:kbd4-9mtx-7rfp clear
play  you joins via port:8443 open behind at:stones.example.org
```

So neither screen remembers anything between presses, and the two halves cannot come apart
between them. Each word says which part of the reach it is, which means their order is
nobody's to get wrong and the last word about a part wins — and *that* is what lets the two
rows that want typing write the line exactly as it stands, put `port:` or `at:` on the end,
and wait. Nothing has to be taken out of a line to put something else in.

The one thing here that could not be read off a screen is the word at the door, because it
has to be made up and nothing in the menu makes anything up. So `Program` makes one when the
seat list opens and passes it along: it is what the door starts out holding, and what
walking the door shut again puts back, so somebody who opens it to look and changes their
mind gets the table they were reading a moment ago rather than a different one.

### Picking, and typing, are the same thing

The arrows move the mark, `w`/`a`/`s`/`d` do the same, a number takes that row outright,
and Enter takes whatever is marked. What makes that cheap rather than a second front door
is that **a row is not a second way of meaning something — it stands for a line.** Picking
one hands that line to `Menu.choose`, which is the very function a person typing the words
would have reached. There is one grammar, and the keys are a way into it.

So `New game` opens a list of the counts, and picking `3` there sends the line
`seats you you you`; the row that joins a table writes `join ` into the prompt and waits,
because no list holds every address; and `Backs`, the line that escape stands for, is a
line too. The counts are read off `Table.MinPlayers` and `Table.MaxPlayers`, and each is
picked by *its own* digit rather than by its place on the list, so the key that says three
is the three.

A seat's row is the same idea one turn further on: it stands for **the whole seating with
that one seat walked along**, so left and right need nothing remembered between presses —
the line says the whole of the change, the screen is built again from what came back, and
the seat under the cursor changes under it as it is walked. The colour screen has kept that
bargain since it was written; the seats keep it for the same reason. Once a game is dealt,
`players <n>` and `restart` do the same job from the prompt.

The one place the two readings of a key meet is the steering letters. With nothing typed
they steer; with a line underway every letter belongs to it, or an address with an `a` in
it could not be spelt out at all.

[Keys.fs](src/Table/Parts/Keys.fs) holds the shape of such a screen and what a press comes to,
including where a person has got to — which list they have opened into, where the mark is,
what they have typed. All of it is a value, so walking about can be checked without a
keyboard, and `Program` is left with drawing what it says and asking for the next key. That
is also why none of this reaches the views: the mark is written `->`, the arrow `Tint`
already draws in the reader's own colour to say *whose turn it is*, so `rich` picks out the
marked row without having been told there are menus.

A console reading a piped line has no arrow to press — `Console.ReadKey` throws at one —
so when the input is redirected the screen is shown whole with nothing marked and read a
line at a time, exactly as it was before any of this. Every script that drives the program
by feeding it lines is untouched.

Like the rest of the console layer `Menu` and `Options` are pure: they say what a screen
reads like and what a line means, and `Program` does the reading and the writing.

## Tests

```powershell
pwsh tools/tests.ps1            # all of them, together
pwsh tools/tests.ps1 -Only solo,lobby
```

Or one at a time, which is what the runner does for you:

```powershell
dotnet fsi tests/ruling.fsx     # the ruling cascade, including elimination
dotnet fsi tests/outcome.fsx    # both winning cascades
dotnet fsi tests/actions.fsx    # what each action does and refuses, and stone conservation
dotnet fsi tests/history.fsx    # undo, redo, and a record that survives the round trip
dotnet fsi tests/knowledge.fsx  # what a player sees, and that what they cannot still adds up
dotnet fsi tests/lobby.fsx      # seats, tokens, whose turn it is, what a table refuses, and
                                #   the seats at one that the program plays
dotnet fsi tests/solo.fsx       # the game at one keyboard: what a line does, and what it
                                #   asks written down
dotnet fsi tests/view.fsx       # that no view shows a player anything they should not see,
                                #   that every view says the same things in the same words, and
                                #   that changing the colours changes nothing else
dotnet fsi tests/html.fsx       # that the page is well-formed, lands where it is aimed,
                                #   and has no control on it the game would not take
dotnet fsi tests/reach.fsx      # the word at a table's door, and an address as somebody says
                                #   it filled out into one a console can reach
dotnet fsi tests/cli.fsx        # the command surface, that both halves of it agree, and that
                                #   every row of every menu - including every seat list there
                                #   could be - stands for a line the menu can read
dotnet fsi tests/properties.fsx # the invariants, over games FsCheck thinks up itself
dotnet fsi tests/rival.fsx      # the seat the program plays: that it plays legally, that it
                                #   plays fairly, and that the skills mean something
dotnet fsi tests/tictactoe.fsx  # the second game - its rules, its three views, its machine,
                                #   and everything above it that it gets for nothing
```

**Two harnesses, and the reason is `dotnet fsi` rather than the program.** A script names a
loaded file by its basename, and both games have a `Board.fs`, a `Turn.fs`, a `Words.fs` and
four more - which compile happily side by side in one project and cannot be loaded into one
script. So [Checks.fsx](tests/Checks.fsx) keeps score and loads nothing at all, and
[Whole.fsx](tests/Whole.fsx) and [Noughts.fsx](tests/Noughts.fsx) each load the same stack in
the project's own order with one game on the end of it. The thirteen scripts above the last
one used to keep a hand-ordered `#load` list apiece; they had already drifted, which is the
same disease the seams are for.

Two more, which want a process rather than a value:

```powershell
pwsh tools/smoke.ps1                  # a real browser, driven: the stream, the box, the
pwsh tools/smoke.ps1 -Game tictactoe  #   buttons, the knock, the door
pwsh tools/wire.ps1                   # a hosted table with two consoles at it, over SignalR
pwsh tools/wire.ps1 -Game tictactoe
```

And [publish.ps1](#one-file), which builds the program into one file and then plays it.

`wire.ps1` is in CI; `smoke.ps1` is not, because it wants a Chromium-based browser and the
runner may not have one. `wire.ps1` wants nothing but dotnet, and it is there because a
regression got past everything else - see [what a second game found](#what-a-second-game-found).

**Together, because nearly all of it is compiling.** Each script is its own `dotnet fsi`,
and each of those recompiles the same sources from scratch: `lobby.fsx` takes five seconds,
and two tenths of one of them are spent on its checks. Fourteen of those in a row is most of
a minute of one core while the rest of the machine watches. They share nothing — separate
processes folding values from fixed seeds — so there is no order between them to get wrong,
and running them at once cannot change what any of them decides. Started together they take
fourteen seconds, and the runner caps how many at once by the number of cores, so a two-core
build machine still gets half its time back rather than thrashing.

The obvious next step is not taken: the scripts could `#r` the built `TCModel.dll` instead
of `#load`ing the sources, which would cut each one from five seconds to under two. It is
declined because it changes what is being tested. `#load` cannot go stale, needs no build,
and tests the source in the working tree; a reference tests whatever was last compiled, and
a forgotten build turns a red run green. Fourteen seconds is not worth that.

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

It then sits a **second console** at the same served game and says one line there — not a
second browser, just a cookie and a held-open stream, because a console at this table is
whatever holds one and posts a line. That is what makes the last check possible: everything
the stream has carried until then has been a piece of the page, landing where the element of
its id already was, and a nudge is not a piece of anything. See [When the turn comes
round](#when-the-turn-comes-round).

It also arrives at a table with a **word at its door** — which is what a table gets when
nobody says otherwise, so what is driven is the way this is really served rather than a way
round it. A stranger with no word is turned away and shown the door page; the browser is sent
the address with the word in it, and everything it fetches afterwards rides the cookie the
table hands back. And it waits out a whole interval of quiet at the end to see the table's
**heartbeat** arrive: a page can take every board perfectly and never hear one of those, and
over a long wire that page goes silent within the minute.

**Nothing else in it waits a fixed length of time.** Every wait is for the thing being waited
on — the board arriving, a heading changing, the knock landing — which is both quicker and the
only version that is honest: a pause long enough to be safe on a loaded machine is wasted on
every run that did not need it, and a run that did need it fails looking exactly like a page
with a dead button on it. It went from thirty-three seconds to eight, and the second of those
is the point. The heartbeat is the one exception, and it is a real one: what is being checked
there is that something arrives when *nothing* has happened, so there is nothing to wait on
but the clock.

`-Rival` serves the same game with the program in the second seat, and checks the two things
about that which only a browser can show: that the page is told whose seat it is, and that
the machine's own move arrives down the stream without the page going and asking for it.

It wants Edge or Chrome on the machine, which is why it is not in CI. Run it after touching
anything the browser reads.

Each script exits non-zero on failure. They load the source directly, so they run
without building the console app. `history.fsx` reaches up through the engine to
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

## One file

```powershell
pwsh tools/publish.ps1                     # both shapes, for this machine
pwsh tools/publish.ps1 -Shape portable     # just the small one
pwsh tools/publish.ps1 -Runtime linux-x64  # for somebody else's machine
```

| | size | wants |
| --- | --- | --- |
| `portable` | 6.3 MB | the ASP.NET Core 10 runtime installed |
| `standalone` | 104.7 MB | nothing at all |

Guests need neither. They join in a browser, and the browser is served by whoever is
hosting — which is what embedding [datastar.js](assets/datastar.js) rather than fetching it
buys. So `standalone` is for exactly one case, somebody hosting a table on a machine with
no .NET on it, and a hundred megabytes is a fair price for that and a poor one for anything
else. `portable` is what goes in a release.

**Never trimmed.** `Launch` builds its command line by reflecting over the argument types,
SignalR finds a hub's methods by name, and `Page.Signals` is read off a request by a
serialiser that reflects. A trimmed build is 24 MB, emits **no IL warning whatsoever**, and
throws on the first line it is given:

```
The type initializer for '<StartupCode$TCModel>.$Launch' threw an exception.
```

So [publish.ps1](tools/publish.ps1) does not just build the file, it runs it: `--help`, a
table served to a browser with the client carried inside the file, and a table hosted over a
socket with a console sitting down at it. Between them those are every part of this program
that only works because something was found by reflection.

### What a program calls itself

Everything printed here for somebody to type is an instruction, and an instruction that will
not run is worse than none. There are two ways this game gets passed around and they are
called two different things: a clone, run with `dotnet run --`, and one file somebody was
sent, called by its own name.

```
    dotnet run -- turncoats join greg-pc --code kbd4-9mtx-7rfp     # from a clone
    TCModel turncoats join greg-pc --code kbd4-9mtx-7rfp           # from a published file
```

[Invoked.fs](src/Table/Parts/Invoked.fs) answers that once, and everything that prints a
line asks it: the usage and the examples, the address a table reads out to the room, the
line a dropped player is handed for getting back to their seat, the header on a record. What
it asks is not how this process was started but **where the reader is standing** — `dotnet
run --` works when there is a project in the current directory to run, and nowhere else.

Until there was a file to hand anybody, every one of those said `dotnet run --` and nobody
noticed, because everybody testing it had the repository.

## Tooling

```powershell
dotnet tool restore                     # once
dotnet fantomas src tests tools          # format
dotnet fantomas --check src tests tools  # or just say what is unformatted
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
`-warnaserror`, the format check, every test script, a hosted table with two consoles at it
over a real socket, and it replays the two committed records. One is the oldest thing in the repository that still has to work; the
other is a game of noughts and crosses, holding the same promise for the game that was
written to find out whose promise it was. The two scripts that want a browser or a port -
[smoke.ps1](tools/smoke.ps1) and [wire.ps1](tools/wire.ps1) - are not in there, and are run
by hand after touching anything a browser or a socket reads.

### What it depends on

| | |
| --- | --- |
| [Spectre.Console](https://spectreconsole.net) | the `rich` view's panels, tables and charts |
| [Argu](https://fsprojects.github.io/Argu/) | the command surface both ways round: `play`, `serve`, `host`, `join`, `replay`, and a command line as a value the program can write |
| [Falco.Markup](https://github.com/FalcoFramework/Falco.Markup) | the `html` view's elements |
| [Falco.Datastar](https://github.com/FalcoFramework/Falco.Datastar) | the client's attributes, its stream frames and its signals, so none of those spellings are this repo's to remember |
| [FsCheck](https://fscheck.github.io/FsCheck/) | the generated games in `properties.fsx` (a test-time reference) |
| ASP.NET Core + SignalR | the host, its hub, the streams held open to browsers, and the buckets that slow down guessing at the door |
| `assets/datastar.js` | [Datastar](https://data-star.dev) 1.0.2, committed and embedded rather than fetched |

`Spectre.Console.Cli` used to sit beside `Spectre.Console` here, owning the command
surface while Argu wrote lines for it. It is gone: one library reads and writes now, and
what went with it was a container the commands had to be built through and a pinning
problem — its version had to be held at `0.51.1` to match the rendering library, because
taking its newest would have brought a newer one of those along under `Rich`.

Nothing here needs npm, node, or a build step. The one piece of client-side anything is
that committed `.js`, and it is served by the same process that hosts the game.

`Falco.Datastar` brings `Falco` itself along, and its routing is not used — the endpoints
are mapped onto ASP.NET directly in [Server.fs](src/Net/Server.fs), because a hosted table
needs SignalR's `MapHub` beside them regardless. That is a known cost, paid for everything
else the package carries: the attribute vocabulary, the stream's frames and the signals
are all spellings this repo would otherwise be remembering by hand, and it had already got
one of them wrong.
