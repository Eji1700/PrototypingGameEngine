# PrototypingGameEngine — an engine for turn-based games, and eight games in it

Answer seven questions about how a game is *played* and fifteen about how it is *read*, and
you are handed the rest: a history to walk back through, a record that replays, seats and
tokens, a machine to play one of them, three ways of drawing a board, a command line, a menu,
a browser, and a table with the players at different keyboards. None of that is written twice
and none of it knows what game it is carrying.

This file is about that machinery. **The rules of the games are not in it** — each has a
README of its own:

| | | |
| --- | --- | --- |
| [**Turncoats**](src/Games/Turncoats/README.md) | 2 to 5 players | Stones on a map, hidden bags, and a game settled twice over. The game this was built for |
| [**Noughts and crosses**](src/Games/TicTacToe/README.md) | 2 | Nine squares, three in a row, and nothing hidden |
| [**Diplomacy**](src/Games/Diplomacy/README.md) | 7 | Seven powers, thirty-four centres, no dice — and everybody writes at once |
| [**Compile**](src/Games/Compile/README.md) | 2 | Fifteen protocols drafted 1-2-2-1, three lines across the table, and a deck each. All ninety cards, and an optional rule you turn on from its own settings page |
| [**Life**](src/Games/Life/README.md) | 1 | Conway's, on a board with its edges joined - a soup, a rule, and nobody to play against. Runs on a clock you start and stop |
| [**Snake**](src/Games/Snake/README.md) | 1 to 4 | The arcade game, on a clock: the snakes move on their own and quicken as they eat, and you only steer |
| [**Cascade**](src/Games/Cascade/README.md) | 1 | Two hundred and fifty-six elbows. Touch one and it turns a quarter, and whatever it is now reaching that is reaching back turns too |
| [**Warband**](src/Games/Warband/README.md) | 2 | Two squads of five on ten hexes apiece, mustered out of each other's sight - and then a battle neither of you plays, settled the moment it is joined |

```powershell
dotnet run                      # asks which game, then that game's own menu
dotnet run -- tictactoe play 2  # or say which, and everything after it is read by that game
dotnet run -- diplomacy play 7  # seven powers, and no dice anywhere in it
dotnet run -- life play 1       # one seat, no opponent, and nothing to win
dotnet run -- cascade play 1    # touch a cell, and then you only watch
```

Written in F# as a Model-View-Update loop. The core is pure: a game deals itself from a seed
and `Update.update` folds a `Msg` into the next `Model`, and nothing in it can fail — a refused
move is a model with a line in it. That is what lets every table above be a fold, and every one
of them be checked without a keyboard, a screen or a socket.

**A seed and a list of messages reproduce a game exactly**, which is what makes a record on
disk a thing you can replay rather than a thing you can read. It is also why the generator in
[Random.fs](src/Common/Random.fs) is a value handed back with each result rather than
`System.Random`: one that advanced by mutation would make `update` impure and stop the model
being a value. A game with no chance in it at all says so — `Reseed` answers zero, and nothing
above has to care either way.

Nothing below the table layer knows a screen exists, and every screen a player reads goes
through a `View` — so how a game looks is swappable, and it is: `plain` writes blocks of text,
`rich` builds panels and charts, `html` builds a page, and two players at one networked table
can each pick their own.

Seats can be played by the program rather than by somebody in the room. A machine at a seat is
held to what a player is held to: it asks the rules what they will take rather than keeping its
own opinion, it reads only what that seat can see, and it picks a `Move` — the very thing a
typed line turns into.

**Why three games.** About four fifths of this program is not about any of them, and all of it
was extracted on the grounds that it is generic. A claim like that cannot be tested by the game
it was extracted from. Noughts and crosses tests it by being *small* — two seats, nothing
hidden, no chance — and Diplomacy by being unlike everything else the machinery had ever been
asked for: seven seats, no chance at all, orders written in secret by everybody at once, a year
made of three kinds of phase, and a board no picture can show all of. What those two turned
up along the way is the most useful thing in this file: [what a second game
found](#what-a-second-game-found), and [what a third game found](#what-a-third-game-found).

**And a fourth.** [Compile](src/Games/Compile/README.md) is the first here that is a deck rather
than a board, and the first whose game is three games in a row: a draft, a laying-out of protocols
against lines, and then play — with different moves and three different senses of whose turn it is.
It went in a piece at a time over many commits — the table, then the numbers game, then the pile
that resolves rules text, then ninety cards one shape at a time — which makes it the one honest
test of the other claim this file makes: that a game can be **added to**, rather than only added,
without anything above it moving. Nothing did, once.

**And a fifth, which is not a game of turns at all.** [Life](src/Games/Life/README.md) has one
seat, no opponent, nothing to win, no ending, and a position that changes because a rule says so
rather than because anybody chose it. Four games of two or more people taking turns against each
other do not test "generic in the game" very hard; this one fills in the same two records,
unchanged, and gets the timeline, the record, the replay, the shared verbs, the menu, the command
line, the wire and all three screens for nothing. What it turned up is three places where code
that had been right for four games had never been handed a one: ["1 players", a clause about
machines at a game with none, and a block that loses its
name](src/Games/Life/README.md#what-it-turned-up).

**And a sixth, which does not wait for anybody.** [Snake](src/Games/Snake/README.md) is the
arcade game: the snakes move on their own, quicker as they eat, and what you press only steers.
It is the one that has no business working on a pure fold, and the point of it is that it does —
[a beat is a move](#a-game-that-does-not-wait), the game says how long a table should leave
between them, and the tables keep the time. Everything a game of turns gets, it gets: a record
that replays beat for beat with no clock involved, `undo` that walks back through beats, and
every one of its rules checked by folding beats by hand. It is also the first game here whose
generator is used *through* a game rather than only at the deal — a piece of food is drawn every
time one is eaten — so it is the one that says whether "a seed and a list of messages reproduce
a game exactly" survives a game that keeps drawing.

It offers the same board a step at a time as well (`snake-turns`), which is where its machines
live and where four people can play it round one keyboard.

**Using it** — [Running](#running) ·
[Taking it back, and writing it down](#taking-it-back-and-writing-it-down) ·
[How the board is shown](#how-the-board-is-shown) ·
[Kept for next time](#kept-for-next-time) ·
[Colours of your own](#colours-of-your-own) ·
[A screen described once](#a-screen-described-once) ·
[Playing from different machines](#playing-from-different-machines) ·
[Playing against the program](#playing-against-the-program)

**The seams** — [A game-shaped hole](#a-game-shaped-hole) · [The second seam](#the-second-seam) ·
[A game that does not wait](#a-game-that-does-not-wait) ·
[A frame is not a move](#a-frame-is-not-a-move) ·
[Adding a game](#adding-a-game) ·
[What a second game found](#what-a-second-game-found) ·
[What a third game found](#what-a-third-game-found)

**The code** — [How it is put together](#how-it-is-put-together) · [Layout](#layout) ·
[A house of them](#a-house-of-them) · [In a container](#in-a-container) · [Tests](#tests) · [One file each](#one-file-each) · [Tooling](#tooling)

## Running

```powershell
dotnet run                     # which game, then that game's own menu: how many are playing,
                               #   then who is in each seat
                               #   (arrows or wasd to move, a number to pick)
dotnet run -- tictactoe        # or say which, and go straight to that game's menu
dotnet run -- tictactoe play 2 # everything after the name is read by that game
dotnet run -- tictactoe serve 2 --rival hard   # nine buttons in a browser, against a machine
                               #   that cannot be beaten

dotnet run -- diplomacy play 7 # seven powers, all of them at this keyboard
dotnet run -- diplomacy play 7 --rival hard --rival hard --rival hard --rival hard \
                               --rival hard --rival hard    # ...or six of them played for you

dotnet run -- turncoats play 3 # or name it; a line that names none means the first game

dotnet run -- play 3 --seed 42 # the same game again, from a seed
dotnet run -- play 2 --view rich --colour blue=teal

dotnet run -- play 2 --rival medium              # the seat after yours, played by the program
dotnet run -- play 4 --rival easy --rival hard   # once per seat you are giving away

dotnet run -- serve 3          # the same game, played in a browser on this machine
dotnet run -- serve 2 --rival hard

dotnet run -- replay logs/...-2p-seed42.log  # take a saved game up again, machines and all
dotnet run -- serve --from logs/...-2p-seed42.log   # ...the same game, taken up in a browser

dotnet run -- host 3                        # open a table at their own machines
dotnet run -- host 3 --open                 # ...with no word at the door, for a room you trust
dotnet run -- join greg-pc --code <word>    # sit down at one someone else opened
dotnet run -- join greg-pc --token <token>  # come back to the seat you were in
                                            # or open the address it prints in a browser

dotnet run -- host 3 --behind --at stones.example.org   # https ends at a tunnel or proxy
dotnet run -- host 3 --cert stones.pfx --cert-password <pw>  # ...or is held here

dotnet run -- house            # several games at once, listed on a page, dealt on demand
dotnet run -- house --fill     # ...taking up the games in logs/ on the way up

dotnet run -- --help           # every command; --help works on each of them too
```

Each game is also its own program, which is what goes in a container and what a release
hands you. There the game's name is already said, so it is not said twice:

```powershell
dotnet run --project src/Games/TicTacToe -- play 2   # from a clone
Turncoats host 3 --open                              # from a published file
```

**Every command above belongs to the engine and not to any game**, which is why they are the
same three lines whichever one is being played: `play`, `serve`, `host`, `house`, `join`,
`replay`,
`--seed`, `--rival`, `--view`, `--colour`, and at the prompt `undo`, `redo`, `history`,
`save`, `notes`, `commands`, `log`, `sound`, `view`, `restart`, `help` and `quit`. Each game adds the words for its own
moves and nothing else, and each has a `help` of its own for them.

Those words, and the rules they are for, are in each game's own README:

| | | |
| --- | --- | --- |
| [**Turncoats**](src/Games/Turncoats/README.md) | 2 to 5 | Stones on a map, hidden bags, and a game settled twice over |
| [**Noughts and crosses**](src/Games/TicTacToe/README.md) | 2 | Nine squares, three in a row, and nothing hidden |
| [**Diplomacy**](src/Games/Diplomacy/README.md) | 7 | Seven powers, thirty-four centres, no dice — and everybody writes at once |
| [**Compile**](src/Games/Compile/README.md) | 2 | Fifteen protocols drafted 1-2-2-1, three lines across the table, and a deck each. All ninety cards, and an optional rule you turn on from its own settings page |
| [**Life**](src/Games/Life/README.md) | 1 | Conway's, on a board with its edges joined - a soup, a rule, and nobody to play against. Runs on a clock you start and stop |
| [**Snake**](src/Games/Snake/README.md) | 1 to 4 | The arcade game, on a clock: the snakes move on their own and quicken as they eat, and you only steer |

Everything below this line is about the program rather than about any of them.

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

The one line that is not a move is the deal, and it carries the whole of what a
game cannot recover from its own moves: how many were playing, what they were
dealt from, and **who was in each seat** — `you` for a person at the keyboard, a
skill for the program. Which seat the machine played and how well it played are
decided before the first move and are written down nowhere else, so a record that
left them out would replay into a table where every seat was suddenly yours.

```
deal 2 42 you hard

#   1  turn 1, Player 1
recruit r 3
#      Player 1 recruits a Red stone into Greymarket.

#   2  turn 2, Player 2
undo
#      Taken back: recruit r 3.
```

```powershell
dotnet run -- replay logs/2026-08-02-215823-turncoats-2p-seed42.log
```

...or `Continue a game` at the menu, which lists what there is to say that about and
hands back that very line.

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

**Which makes it a resume rather than a viewing.** Four things follow from a
record being a game you can carry on with:

- **It is offered rather than remembered.** `Continue a game` at every game's menu
  lists the records there are — when each was put down, how many seats, how many
  moves — and every row on it stands for the `replay <file>` a person would have
  typed. This worked at every game in the program long before there was a screen
  for it, and almost nobody found it: it wanted a path typed at a menu that never
  offered one. A record's name says which game it is for the same reason, so the
  list can be drawn without opening every file in the folder — and so that handing
  one to the wrong game is refused in a sentence that names the right one, rather
  than getting as far as the first move and being told to say a square's number.
  The name goes in the *stamp*, which is whatever comes before the seats and the
  seed and which `Transcript.path` never looked inside, so every record written
  before this still reads back and still goes on being written to.
- **The machines come back.** The seating is on the deal line, so they take the
  seats they were playing at the strength they were playing them.
- **It can be taken up any of the three ways in.** `play`, `serve` and `host` all
  ask where the game comes from in the same words — a count and a seed, or
  `--from <record>` — because it is the same question, and a record answers it
  more completely than a count does. So a game put down at one keyboard can be
  taken up in a browser or as a table others join. The machines keep their seats
  and only the people move: whether they are in this room or at their own machines
  is what the three verbs *mean*, not something the game remembers. (`replay <file>`
  is the short way of saying `play --from <file>`, and reads to the same value, so
  the two cannot drift apart.) Saying a count, a seed or a `--rival` alongside
  `--from` is refused rather than half-heard — that is two games asked for at once.
- **It is one game and one record.** A resumed game goes on writing to the file it
  came out of rather than forking a second beside it. It knows which file that is
  from the name — built in one place and taken apart in the same one — and a record
  that has been renamed is filed afresh instead, on the principle that a file you
  cannot prove is this game's is a file not to write over.
- **Reading one does not change it.** A record is written on the way out whether or
  not anybody asked, so once the way out leads back to the file it came from, taking
  a game up and putting it straight down would rewrite it — which is how the oldest
  records in `logs/` got quietly restated in a newer form the first time this was
  tried. A sitting that added nothing to the game writes nothing.
- **`quit` leaves the game standing.** It used to resign first, so a record said
  how it ended rather than simply stopping. That was right while a record was a
  finished thing to read back; it is wrong once one is a game to take up, because
  it made putting a game down for the evening the same act as losing it. Conceding
  is `resign`, which is a move a player makes on purpose and always was.

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
      Board:   Margins -> Player -> Model -> string // the whole board, for one player
      History: Player -> Model -> string           // the record of play so far
      Answer:  Player -> string -> Model -> string // this game's own question, in the words asked
      Rules:   string                              // the rules and the commands
      Says:    string -> string                    // one line, with no board to go with it
      Waiting: Waiting list -> string }            // a table still filling up
```

`Margins` is what the person reading wants round the game itself - the writing that explains
the board, the box that lists what can be typed, and the box of what the game has been saying -
and it is a record rather than three booleans in a row because three booleans in a row get
passed the wrong way round exactly once and then never look wrong again. Not one of them is
part of the game, so none of them reaches the model or the record, and at a table over a
network each person has their own. `notes`, `commands` and `log` are the three words that turn
them.

The third arrived last and is the one that shows what the record is for. A log twelve lines
deep under a board somebody has stopped moving is twelve lines in the way of the thing they are
trying to read, and `log off` puts it away without losing anything: `history` still writes out
the whole record, because the box on the screen and the record on disk were never the same
thing. That is also why `log` had to *stop* being a second word for `history` to become the
word for the box it names - `notes` turns the notes and `commands` turns the commands, and a
switch named after something else would have been the odd one out.

`sound` is a fourth of the same kind, and sits beside the margins rather than in them: a
console that has said `mute` is not sent the sounds a board makes, and one at the next keyboard
still is. Nothing about it reaches the game either - a game says what its board *is sounding*
and never who is listening.

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

### Settings, and the three kinds of question

`settings` at the menu opens a short menu of its own, and a page behind each row of it
([Options.fs](src/Table/Parts/Options.fs)):

```
     1  audio  whether the table rings when your turn comes round
     2  video  how the board is drawn, and what is drawn in what colour
  -> 3  game   2 ways this game can be played
     0  done   back to the menu

  Nothing on any of these pages is kept unless you say 'save', which keeps all three
  at once - so a page you only looked at is a page that changed nothing.
```

Three pages because there are three kinds of question. **Video** is how a board reaches your
eyes. **Audio** is how it reaches your ears, which at a terminal is one bell and a question about
whether you want it. **Game** is whatever this particular game lets you settle about itself,
which for seven of these eight games is nothing at all.

The split is not decoration. Before it there was one screen with a view row and a colour row
on it, and every new kind of question would have gone on the end of the same list until it was
a list nobody could read. What a menu with pages behind it buys is not room so much as a
*settled place to put the next thing*.

One `save` writes all three from whichever page it was said on, which is the only honest
reading of the word: nothing on any page is a draft, and a save that kept the page you
happened to be standing on would quietly drop the answer you gave two screens ago.

#### Audio

One row, and a real one rather than a place kept for later. The table already rang when the
turn came round and nothing you did brought it round — that is what `Nudged` is, and it is
how a game you are not watching tells you it is waiting. Until this page there was no way to
say you would rather it did not.

It is the one setting that is not a game's own. A bell is a fact about the room you are
sitting in, so it is asked once, kept above all the games, and every game picks it up.

#### Game

The page a game answers for itself, and the reason `Games.all` is seven entries rather than
eight. Compile can be played with an optional rule in it, and that used to be a second game in
the picker — so the front door asked which game and then asked the same question again in
different words. It is one game with two ways of being played now:

```
     1  compile          in play   Draft three protocols, set them against three lines, ...
  -> 2  compile-control            The same, with the optional rule: lead 2 lanes and take ...
     0  done             back to the settings
```

**Each way keeps a name of its own, and that is load-bearing.** A game with the optional rule
in it is a different game — different deal, different reckoning — and its record says so on
the deal line. So a saved game is taken back up exactly as it was played whatever this page
was last left saying, and `compile-control` on a command line still opens that game outright,
because a word somebody typed is not a question they are asking. What is settled here is what
a *new* game is dealt as, and nothing about an old one.

That constraint is what the page is *for*, and it is worth saying which way round it runs: a
setting that could quietly change what a record replayed into would not be a setting, it would
be a bug with a screen in front of it. Which is also why a game's other choices — how it draws
a board, how much of it to show — are not on this page and never will be. Those are views, and
a game that wants another one adds a view.

### Colours a player chooses

Which colour is drawn for what is a **palette** ([Palette.fs](src/Table/Parts/Palette.fs)),
and the Video page is the screen that changes one:

```
     1  drawn                           rich      panels, charts and colour
  -> 2  red     Red   R R   >R          crimson   Red stones, and the regions Red rules
     3  blue    Blue   B B   >B         teal      Blue stones, and the regions Blue rules
     4  green   Green   G G   >G        moss      Green stones, and the regions Green rules
     5  yours   (you)   ->              gold      your own seat, and whose turn it is
     6  hidden  dead                    slate     what is held back from you, and ground nobody may enter
     7  reset   put the colours all back
     8  save    keep all this, and open that way next time
     0  done    back to the menu

  Left and right walk the one marked -> through what it can be, or say 'blue teal' to
  name a colour outright, or 'view rich' to name a way of drawing.
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
`Options.chooseVideo` a person typing those two words would have reached, the screen comes
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

### Kept for next time

`save` on any of the three pages writes [settings.txt](src/Table/Parts/Settings.fs) beside the
records, and every way into a game reads it — the menu, and the command line alike:

```
view rich
bell off

[turncoats]
view rich
red lemon
blue azure
green moss
hidden slate
yours gold

[compile]
plays compile-control
```

**Every line in it is a line you could type at the screen it belongs to.** `view rich` is what
the menu takes; `bell off` is what the Audio page takes; `plays compile-control` is what the
Game page takes; `red lemon` is what the Video page takes; and reading the file back is
handing those lines to those very readers. So the file cannot come to hold something no screen
can express, no screen can come to offer something the file cannot hold, and what a line
*means* is settled in one place rather than two.

Which half of the file a line belongs in is part of what it means, and the two new lines
belong in opposite halves. `bell` is nobody's game in particular, so it goes above the first
`[game]` and is complained about under one; `plays` is a game's own answer — there is no way
of playing that every game has — so it goes under a game and is complained about above them.
A `plays` line is kept under the name of the game that *offers* the ways rather than the name
of the way settled on, because the second of those is what the line says, and a file that kept
it under itself would be answering a question with itself. It is the bargain the record already
keeps — a record is the moves a player typed — applied one level out, to the answers they
gave rather than the moves they made. Editing it by hand is reading it back, and a line that
has gone stale is said out loud at the menu rather than passed over in silence.

The colours are per game and have to be: a game of stones colours three factions and a game
of nine squares colours two marks, and there is no one list to keep them in. The **view** is
written twice — once above all the games and once for the game it was saved at — which is
what makes it a *default* rather than a note about one game. Settle on `rich` at Turncoats
and the games you have not opened yet open in `rich` too; settle differently at one of them
and that one keeps its own answer, its own line being read first. A view a game does not
have falls back to the plainest rather than refusing, because a game you cannot open on
account of how you once read a different one is not a setting but a trap.

The command line reads the same file through the same function
([`Playable.opening`](src/Table/Playable.fs)), and that is deliberate: a default that people
who pick from a list got and people who type did not would be two defaults. What is said on
the line is the later word — `--colour red=teal` changes that one slot and leaves every other
as it was left, and `--view plain` overrides for that game without writing anything down.

### Colours of your own

Nineteen colours is what this program was *built* with, not what it can offer. A
`colours.txt` beside the records says otherwise:

```
rust     #b7410e     # a new one, on the end of the list
crimson  #ff2d2d     # brighter than the built-in, and in its place
no slate             # one I never use
```

A name already known is restated **in place**, so saying what crimson should have been does
not shuffle a list somebody has learnt the shape of; a new name goes on the end; `no <name>`
drops one. Hex rather than a colour name, because the point of the file is the colours this
program does not have. Names are letters only — a name with a space in it could not be said
in the two words the Video page reads.

What a *game* draws itself in is looked up in the built-in catalogue rather than in this
list, and that separation is load-bearing: Diplomacy says Russia is `violet`, and a player
who never wants to see violet again has said nothing whatever about Russia. So dropping a
colour removes it from what you are **offered** and cannot leave a game unable to draw
itself. Every line the file gets wrong is a sentence at the menu, not a silent shrug — it is
a file a person writes by hand, and a line of it that quietly did nothing is an evening.

### A screen described once

The trade above is paid per game as well as per endpoint. Three views with six screens apiece
is eighteen answers before a new game is playable, and noughts and crosses paid all of it for
nine squares. Worse, the compiler only checks that the eighteen *exist*. It has
nothing to say about whether they agree, and they did not: the same border was a "side" in
one view and a "wall" in another, one view called the dead region wild, and two of the four
notes were shown by one view and silently missing from the others. Nothing failed. Every
board still drew. [view.fsx](tests/view.fsx) is what catches that, and it catches it
afterwards.

So there is now a middle ground, and it sits *under* the view seam rather than replacing it.
A **scene** ([Scene.fs](src/Table/Parts/Scene.fs)) is what a screen is made of, said once and
with nothing in it about what it looks like:

```fsharp
type Scene =
    | Blank
    | Heading of string
    | Say of Line                                       // one line of the game talking
    | Note of string                                    // writing that goes when the notes do
    | Written of string                                 // already laid out; keep it as it is
    | Block of title: string * Scene list
    | Stack of Scene list
    | Beside of Scene list                              // side by side where there is room
    | Aligned of Line list list                         // columns lined up, no walls
    | Walled of across: int * Course list               // cells in rows, with walls
    | Tile of title: string option * tone: Tone * body: Scene list
    | Patch of shape: string * tone: Tone * body: Scene list  // a cell of a bigger shape
    | Big of Span                                       // one glyph, drawn to be seen
    | Does of caption: string * line: string * tone: Tone   // a control that types a line
    | Field of legend: string * rows: (string * Speck list) list  // many small cells, a glyph each
```

[Readers.fs](src/Table/Parts/Readers.fs) draws one three ways - `Plain` counts characters into
columns, `Panels` builds Spectre's widgets, `Pages` builds elements - and a game that
describes its screens says `Views = Readers.views scenes` and has all three. Noughts and
crosses does, and `Rich.fs` and `Html.fs` went with it: 396 lines deleted, and the board,
the record, the rules, the waiting room and nine buttons all still there.

**Three things stop being tests and become facts.**

- **A control cannot offer a line the parser would refuse**, because `Does` carries the line
  and every reader draws *that*. A page makes it a button and a terminal prints it as the
  thing to type, so what you are told to type and what a click posts are not two strings
  kept in step - they are one string read twice. [tictactoe.fsx](tests/tictactoe.fsx) walks
  the description and holds the page's buttons up against it.
- **A block cannot be shown by two readers and missing from the third**, there being one
  description and all three walking it.
- **A note cannot be worded one way here and another way there**, for the same reason. The
  table still filling up had been written out once per game, and the two wordings had
  already drifted - one said a console had "gone" and the other that it had "dropped". It is
  `Scene.waiting` now, and a game that wants it says so in one line.

**Colour is described, not applied.** A `Tone` is `Plainly`, `Quiet`, `Yours`, or `Slot` of
one of the words the game says it colours - so a scene names the same slots the colour screen
offers, and each reader spends them its own way: `Panels` in Spectre's markup, `Pages` in the
custom properties the page carries in its head, and `Plain` not at all, because it lays out by
counting characters and an escape code inserted mid-layout pushes everything after it
sideways.

**And a reader may refuse a hint.** `Walled` carries how much room a cell *wants*; a reader
honours it if it can afford to. The one counting characters into columns cannot, and gives a
cell what is in it. That is the difference between a description and a layout, and it is why
the half-cell `Shift` on a `Course` is not a hint: the offset is what makes a region on a
lattice touch six others rather than four, so it is a fact about the board, and a reader that
dropped it would be drawing a different map.

**A grid of tiles is a table; a grid of patches is a map; a grid of specks is a field.** The
first two put a wall round every cell, which is right for nine squares and unreadable at two
hundred and fifty-six - so `Field` is a glyph a cell, laid out in rows with a legend across the
top and labels down the side, and Cascade's board is one. Each cell of it is a `Speck`, and a
speck carries a **mood** as well as a glyph and a tone: a bare word saying what the cell is
*doing* rather than what it is. A terminal has no way to be part-way through anything and
ignores them; a page turns them into classes, and the game's own stylesheet says what turning,
landing and lit look like. It is the same bargain a `Tone` makes about colour, made about
movement.

**A grid of tiles is a table; a grid of patches is a map.** A `Tile` is a cell and its own four
walls, which is what a board of nine squares is made of. A `Patch` says which *shape* it is a
piece of - so cells carrying the same shape that touch are drawn as one region, with no walls
inside it and the wall round it in the region's own tone. That is what lets Diplomacy lay a
province over as many hexes as its borders need and still have it read as one province, outlined
in the colour of whoever holds its centre. The shape has to be said rather than guessed: two
provinces held by the same power are two provinces, and the tone cannot tell you what the shape
does. Both terminal readers hand the same layout the same rows and differ only in the alphabet
they draw it with - box characters for one, dashes and bars for the other - so the two maps are
the same map, wall for wall. A browser's cells do not touch, being laid out with a gap and
rounded corners, so a page keeps the colour and drops the merging.

**None of this is compulsory.** The `View` seam is untouched and Turncoats still fills it in
by hand, which is the right answer for a honeycomb laid out by counting characters into
columns - the screen no general reader would have thought of. What is on offer is that a game
which does not want to should not have to.

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
under [The map](src/Games/Turncoats/README.md#the-map); what each view has to keep is the half-region offset,
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
something the program is certain to accept. See [A command line, both ways round](#a-command-line-both-ways-round).

**Getting up.** `quit` is the same thing to the table as dropping off - the seat is kept,
the game waits, and nobody else may take it - and it is the one thing a player may do at
every table there is: one still filling up, one whose game has finished, one whose seat
the game no longer has. What is different is that this one was *meant*, so the table says
so. `GotUp` carries the sentence, and a console at a terminal ends on it: print the words,
hang up, stop.

Which is the whole reason it is a thing the table says rather than a word the console
knows. A client that read `quit` for itself would be a second opinion about what it means,
free to differ from the one the game keeps - so it types the word like any other line and
what comes back is what stops it. For a while nothing came back: the seat was kept,
everybody else was drawn again, and the console that had typed it was sent nothing at all.
At a terminal that is a prompt that has stopped answering, which is exactly what a table
that has *gone* looks like. The table it got up from goes on standing until whoever opened
it presses Ctrl+C, because one player leaving their seat is not the same as closing the
room on everybody still in it.

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

### A house of them

`host` opens one table and waits at it. A **house** opens none and deals them on demand:

```powershell
dotnet run -- house           # a house of this game, dealing tables as people ask for them
dotnet run -- house --open    # ...with no word at the door, for a room you trust
dotnet run -- house --fill    # ...taking up the games in logs/ on the way up
```

What answers is a page listing what is being played — a link to open a table at each size
the game takes, and a row for every table there is, the ones with a seat going spare first.
Open one and you are sent to a table of your own, at an address you can read out to whoever
is playing.

This is what goes in a container: one program, one port, one game, several games of it at
once. It is what the per-game executables were split apart *for*.

**A terminal joins one by name**, which the list on that page shows beside each table:

```powershell
dotnet run -- join greg-pc --table kbd4-9mtx-7rfp
```

The name is a word on the line rather than part of the address, because an address is a thing
this program puts its *own* path onto — a name typed in there would replace the hub's path
instead of hanging off it. The hub is the very same hub a single hosted table uses; all that
differs is that it works out **which** table a connection is for, off the route it negotiated
at, and turns away a name it does not know rather than dropping that console without a word.

A browser and a terminal sit at one of a house's tables exactly as they do at a hosted one,
and neither can tell which it found.

**One door, not two.** Everything in a house is behind the same word: the list, opening a
table, and every board. A table with a second word of its own was considered and is the wrong
shape here — a house exists so people can see what is being played and sit down at it, and a
list of games you are not allowed to join is a list with no purpose. Whoever is in the house
was let in at the front.

**Tables are forgotten, and games are not.** A table nobody ever sat at goes after an hour,
a finished one after a day, and one with anybody at it never — however long a turn takes,
because a game of Diplomacy between two time zones can sit untouched for a day and is not
abandoned. Sweeping touches nothing on disk: every table writes the same replayable record
every other game writes, so a game swept off the list is still one you can take up, and
`--fill` is a house reading them back. A restart is a pause rather than a loss.

The rules behind all of that are values and are checked as values —
[house.fsx](tests/house.fsx) asks them about tables that do not exist at ages that have not
happened. [DESIGN.md](src/Net/DESIGN.md) is how it was built and what it cost.

### In a container

```powershell
docker build -t turncoats .
docker build -t tictactoe --build-arg GAME=TicTacToe .
docker run -p 5000:5000 -v proto-logs:/data/logs turncoats
docker run -p 5000:5000 turncoats house --code hunter2     # a word at the door
docker run -p 5000:5000 turncoats host 3 --open            # one table, not a house
```

**The command line is the configuration, and there is deliberately no second way to say any
of it.** This program has one language for what to open and how far it reaches — the one a
person types, and the one it writes back out. A set of `PROTO_*` environment variables
would be a second, to be kept in step with the first for ever. So the entry point is the game
and the command is a default anybody may replace, which is what the lines above are doing.

`/data/logs` is the one directory worth keeping. Records go there, and a house takes its
games back up from exactly those files — so the volume is the difference between a restart
being a pause and being a loss. Nothing else in the image is worth a byte: there is no
database, no state directory, and no settings file. A container comes up having settled
nothing, which is what `Settings.none` has always meant and is now something a house relies
on.

**The build machine builds it and then plays a game in it**, which is where that is worth
doing rather than on anybody's own machine: what ships is a Linux image and the runner is
Linux, so what CI builds is the thing rather than a Windows box's idea of it. It runs
[image.ps1](tools/image.ps1) — the same checks a person runs, not a second set written in
shell — against two of the five games, because one would build an image and prove nothing
about `--build-arg GAME`, which is the only thing that differs between them and therefore the
only thing that can be wrong.

What that catches is the set of things that do not fail at build: a `WORKDIR` the image's own
user cannot write to, an entry point that does not pass its arguments on, and a port bound to
the container's loopback and therefore to nothing at all.

It has been built and run since, and two of the three things it was written blind about were
right: `/data` is writable by the image's own user, and the entry point passes its arguments
on. The third was not — not in the image, but in the script checking it.

**Waiting for a container is not waiting for a port.** Every other script here waits by
opening a socket, which is right when the thing being waited for is the thing that binds.
`-p` puts a proxy on the host port the moment the container starts, and that proxy accepts a
connection immediately and holds it until something inside is listening — so a socket check
passes instantly against a container whose runtime has not finished starting, every request
after it fails, the log reads empty, and eight checks call a perfectly good image broken. It
waits for the house to *answer* now.

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

**What the skills are called is the game's own**, and so is what they mean. All three games
here happen to offer `easy`, `medium` and `hard`, and nothing above the game knows that or
requires it — a game says what it has and the command line reads the list off it, which is
why `--help` prints a different sentence for each. What each one actually weighs is in that
game's own README: [Turncoats](src/Games/Turncoats/README.md#the-machine),
[noughts and crosses](src/Games/TicTacToe/README.md#the-machine),
[Diplomacy](src/Games/Diplomacy/README.md#the-machine).

Nothing on the board says which seats are the machine's - its pieces look like anybody's, and
they are - so the table says so once, to whoever sits down to watch, in the words their own
view speaks.

### It plays no better than it is allowed to

A machine at a table is the one thing here that can be wrong quietly. Every other
part of the program either draws something a person looks at or refuses something a
person typed; a machine that has picked a move nobody would has picked a legal move,
drawn a perfectly good board, and said nothing at all. So it is fenced in three ways,
and each of them is a thing the compiler or a check can hold it to.

The examples below are Turncoats', because it is the game with something to hide and therefore
the one where the fences are worth the most. The discipline is not its own: every game's
machine is held to the same three things.

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

There is one more seam, at the far end. Three games have different moves, states and notices,
so no list holds all of them — [Play.fs](src/Play/Play.fs) ends in a plain interface with no type
parameters at all, implemented by closing over a game. That is where the types stop, which
is what makes [Games.fs](src/Games.fs) a three-line list.

### A game that does not wait

Everything above assumes a game of turns: nothing happens until somebody types, and a table left
alone for an hour is a table where nothing has happened for an hour. An arcade game is the one
shape that looks like it needs something else - and it does not, which is what
[Snake](src/Games/Snake/README.md) is here to say. [Life](src/Games/Life/README.md) takes the
same clock and puts a switch on it: the rule runs at about three generations a second until you
stop it, and stops without the clock stopping.

**A beat is a move.** The game says what its beat is and how long a table should leave between
them; the tables keep the time. One field on the second seam, `None` at every game of turns:

```fsharp
type Pulse<'Move, 'State> =
    { Every: 'State -> TimeSpan          // from where the game stands, so it may quicken
      Beat: 'Move                        // an ordinary move, folded by the ordinary update
      Pressed: ConsoleKeyInfo -> string option }   // a key, as the line it stands for
```

Nothing in the timeline, the record, the seats or the screens changed for it. `Every` takes the
state rather than being a number, which is what lets a game quicken as it goes *and* lets a
player wind the clock themselves: Snake keeps a notch from one to nine in its own position, so
`faster` is an ordinary move — in the record, replayed off it, and undoable like any other.

**A game may switch it off.** The beat is asked for whatever the game is doing, so a game with
nothing to do answers with nothing at all - and a move that neither moved the position nor said
a word is left out of the record by the engine. That is the whole of Life's run-and-stop: `p`
turns a flag in its own state, the clock goes on beating three times a second either way, and a
stopped board costs one step of the rule and not a line anywhere. The same answer serves a board
that has settled or died, which is why a clock left running over a still life does not fill a
record with refusals.

What that buys:

- **The record is every beat**, so a real-time game replays to the square it was saved on. There
  is no timing in the file and none is needed — the clock only ever decided *when* a move was
  asked for.
- **`undo` walks back through beats** like any other move.
- **Every rule of it is checked without a clock.** [snake.fsx](tests/snake.fsx) folds beats by
  hand; if any of it needed a timer, the seam would be in the wrong place.
- **A key is a line.** The same bargain [a control on a page](#a-screen-described-once) makes:
  a key press stands for a line the game already reads, so nothing can be pressed that could not
  have been typed, and the four hands at one keyboard — arrows, `wasd`, `ijkl`, the number pad —
  send `a north` and `b north` rather than a private language.

  **The game is asked first.** The loop keeps two keys it cannot give up — Enter opens the line
  prompt, Escape puts the game down — and offers every other key to `Pressed` before falling
  back to its own. That is what lets Snake hold the clock with the space bar and Cascade press a
  cell with it, out of the same loop and with neither game knowing the other exists. `h` holds as
  well as the space bar always does, so a game that takes the space bar does not cost a player
  the one key that stops the clock — and the line at the foot of the screen says which key that
  is, because it asks the game rather than assuming.

Three tables drive it, and each is a handful of lines because a beat is a move:

| where | what keeps the time | what it calls |
| --- | --- | --- |
| a terminal | a loop that draws, watches the clock and polls for keys ([Play.fs](src/Play/Play.fs)) | `Solo.beaten` |
| | *space holds it, `r` deals another, Enter types a line, Esc leaves - and a game that is over is read as a held one, so the clock comes back the moment a fresh board is dealt* | |
| a browser | a timer beside the table it serves ([Server.fs](src/Net/Server.fs)) | `Aside.Beats` |
| a house of tables | one timer, each table on its own beat ([House.fs](src/Net/House.fs)) | `Table.Beats` |

**A terminal draws less of it while it is moving.** Every screen here is drawn with
[margins](#how-the-board-is-shown) - the writing that explains the board, and the box listing
what can be typed - and at three beats a second those are two blocks being repainted under a
board that has moved by one square, which is a screen nobody can follow. So a running clock is
the same thing as those two being off, and holding it puts back whatever that player had
chosen. The third margin does *not* go with them: what the game has just said is the one thing
somebody watching a board move actually wants, so `log` is obeyed at a running board and a
held one alike. Nor is the terminal cleared per beat: [Screens.redrawn](src/Table/Parts/Screens.fs)
writes the new screen over the old one and blanks whatever is left below it, because clearing
three times a second is three flashes a second of an empty window. It also declines to write a
screen identical to the one already there, which is what keeps a board at rest under a running
clock from repainting as fast as the loop can poll — and *that* is why what is on the terminal is
a value rather than a line count, and why it lives next to `cleared`: a terminal that has just
been wiped has nothing on it to be identical to, and the two halves of that fact have to be
within reach of each other. Kept apart for one afternoon, they were not: the loop wiped the
screen before reading a typed line, then compared the new screen against what had been there
before the wipe, matched, wrote nothing, and left the player looking at the blank.

Two rules the tables keep that the game does not know about: **a game nobody is watching does
not beat** — a browser table is dealt when the process starts and read when somebody opens the
page, and a clock running in between plays the whole game into an empty room — and at a hosted
table **nothing beats until every seat is taken**. And whose turn it is stops being a question:
a table whose game has a pulse lets anybody move at any time, because there is no turn to be out
of.

### A frame is not a move

Snake settled that a clock needs nothing but a move. [Cascade](src/Games/Cascade/README.md)
asked the harder half of the same question: what about a board that is *between* two of them?

A cell there takes half a second to turn a quarter, and half a second is a long time to show
nothing. But a picture of a cell part-way round cannot be a move — there is nothing to record,
nothing to undo, and a record with thirty of them a second in it would be a video rather than a
game. So the answer is the exact complement of the one above:

```fsharp
type Pulse<'Move, 'State> =
    { Every: 'State -> TimeSpan
      Beat: 'Move
      Frames: 'State -> int              // redrawings between two beats. Nought at every
      Pressed: ConsoleKeyInfo -> string option }   // game that has nothing moving between them
```

A frame folds nothing. The single thing that differs between one and the next is a number on
the record every screen is already drawn with:

```fsharp
type Margins =
    { Notes: bool                        // which of the three boxes round the game
      Commands: bool                     // a player wants drawn
      Logged: bool
      Phase: float }                     // and how far through a beat this drawing is: 0 at
                                         // the beat, towards 1 before the next
```

`Margins.frame 3 margins` answers which of three pictures the phase falls in, which is what a
terminal picking one of a few wants rather than the fraction. Cascade draws a turning cell as
where it was, its corner half way round, and where it is going. **A view that never asked for
frames is only ever handed 0**, so every screen in the program that was drawn one way before is
drawn exactly that way now.

Three things this buys, and one it does not:

- **The record is unchanged.** A cascade of ninety-five waves is ninety-five lines, not two
  thousand. Frames cannot reach the timeline, the journal or the rules; there is no path from
  one to any of them, which is a stronger claim than a convention about not doing it.
- **A browser does not use them at all.** It is sent the board once a beat and animates the half
  second itself, out of the game's own stylesheet, off the [moods](#a-screen-described-once) the
  cells carry. That is the right split: a terminal cannot interpolate and a browser should not be
  asked to hold a connection open thirty times a second to be told what it could work out.
- **A clock may rest.** Cascade's board is still most of the time, and it beats over a still
  board at whatever notch it is wound to. That used to be a line in the record a beat, because
  `Update` wrote down a move whether or not anything came of it. Now a move the game **neither
  took nor said anything about did not happen** — the model comes back untouched. A game nobody
  has touched has an empty record, a board at rest is not sent down every wire in the house twice
  a second, and Snake quietly stopped writing down a steer that turned a snake the way it was
  already going.
- **What it does not buy is a way to cheat.** A frame cannot show the board something the model
  does not hold. Which cells are turning, what they are turning from, which shapes have come up
  and on which beat are all in the state; the phase only says how far through. So the same board
  read by a terminal three times a beat and by a browser once are the same board, and neither can
  be shown a cell the other cannot.

**And a table can be heard.** The sound a board makes was the other thing half a second of
turning wanted, and it goes on the same seam rather than into a view:

```fsharp
Rings: 'State -> Sound list              // named for what happened, not for what it sounds like

type Sound =
    | Tap        // something small, and there will be a great many of them
    | Chime      // something came out well
    | Fanfare    // something came out well and rarely
    | Ready      // the table is waiting on somebody again
    | Knell      // something ended, or was stopped short
```

Read off where the game stands *after* a move rather than out of the notices, which is what makes
a game taken up from a record sound exactly like the game it was saved from. Named for the
occasion rather than for the noise, which is the bargain a `Tone` makes about colour: a browser
builds all three out of three oscillators and nothing it had to fetch, and a table with nothing
to make a sound with drops them without any of it knowing.

A terminal has *one* bell, and what it does with five sounds is the interesting half. That is
not a limitation anybody chose - it was measured. `printf ""` costs nothing and works
everywhere; `Console.Beep()` blocks for two hundred milliseconds and is still the same bell;
`Console.Beep(freq, dur)` gives a real pitch but is `[SupportedOSPlatform("windows")]`, and this
program publishes `linux-x64` containers. So: one bell.

Which means `Sound.worthABell` is a judgement about *frequency* rather than about worth. It
rings once however many sounds a move produced - three bells in a beat is a noise rather than a
sound - and it does not ring at all for the two that come often. A bell twice a second is what a
game sounds like when it was written by somebody who had several sounds and one bell and did not
notice. `mute` at the table turns off even the rare ones, for that console alone.

**And what a terminal cannot hear, it can see.** Cascade strikes the whole board - a band of
light run down it over about three beats - for exactly the sounds a terminal would have rung
for, and marks the band on the row labels so that it shows in `plain`, which has no colour at
all. That is the only channel that genuinely reaches all three views, and it is worth saying
that the rules decide what strikes the board while the table decides what rings the bell, and
neither can see the other: the two lists are held up against each other in the game's `Faults`,
so a board where they had drifted apart would say so before anybody sat down.

### Adding a game

A folder, two records, and a line. Nothing above the game is touched, and nothing above the
game can be: it is all compiled before the folder exists.

**1. `Rules/`** — how it is played, in its own terms. No English a player reads, no screen, and
nothing from `src/Table`. Name the three shapes once so nothing above carries type arguments
about:

```fsharp
type Msg   = Msg<Move>
type Told  = Told<Move, Notice>
type Model = Model<Move, State, Notice>
```

Then fill in `Rules` — the seven questions above. The load-bearing one is `Play`: it **cannot
fail**. A refusal is something the game *says* — `None` for the state, and notices saying why.
A game that returned `Result` there would push a failure case into every screen in the program.

**2. `Reading/`** — how it is read. Three files is the usual shape: `Ink` says what the game
colours, `Parse` reads the words it invented (and only those — `undo`, `save`, `view rich`,
`resign`, `quit` and `restart` have already been read for every game there is), and `Render`
describes each screen once as a [`Scene`](#a-screen-described-once). A game that wants a screen
no general reader would think of writes its own `View` instead, and Turncoats does.

**3. `Offer.fs`** — both seams joined into one `Playable`, which is the only file either layer
above ever sees.

**4. A project of its own** — a `.fsproj` listing the game's files in order, referencing
[Prototyping.Play.fsproj](src/Play/Prototyping.Play.fsproj), and a `Program.fs` that is one line:

```fsharp
[<EntryPoint>]
let main argv = Prototyping.Play.only Offer.ways argv
```

That line is the same at all eight games, and its being the same is the fair test of the two
seams: by the time a game is a `Playable` there is nothing left for a way in to decide.

**5. And one line in [Games.fs](src/Games.fs)**, if it is also to appear in the program that
has all of them in it and asks which.

What you get for it, without writing any of it: the timeline, undo and redo, the record on
disk and the replay off it, the seats and their tokens, the menu, the seat list, the settings
and their three pages, the command line and its `--help`, three ways of drawing a board, the
browser, the wire between machines, and a loop that lets a machine play any seat.

Two things are worth filling in that are easy to leave empty. **`Faults`** is what the game
says is wrong with *itself* before anybody sits down — a game built out of data can be built
wrong and nothing above it could know what well-formed would look like; it is empty at nine
squares and it is fifty lines at a map of seventy-five provinces. And **`SeenBy`** is the same
notice as much of it as one seat may know, which is the whole of what a game with anything
hidden needs from the machinery.

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

### What a third game found

Diplomacy is about fourteen hundred lines, of which four hundred are the map typed out and two
hundred are the adjudicator. It compiled the first time it was asked to and needed no change
to `Engine`, `Table` or `Net` — the same result the second game got, which is worth saying
because this one was chosen to be as unlike both as the machinery would take.

**The thing it was supposed to break, and did not.** `Rules.Active` says *one* seat is to
play, and Diplomacy is the game where everybody plays at once. That looked like the seam's
first real failure. It is not, and the reason is worth having: a phase is seven powers each
writing orders and then saying they have finished, and *writing* is a move like any other. The
seats come round one at a time; nobody may read anybody else's orders until the last power
seals; and when it does, the whole season resolves inside one `Play`. What the seam asks for
is a total function from a move and a position to the next position, and "everybody moves at
once" turns out to be a fact about what a *phase* is, not about what a move is.

The half of that which does need something is secrecy, and the seam already had it. `SeenBy`
was put there for a game with a closed bag of stones; it is what stops France reading Austria's
orders, and what makes `press france ...` a line only France reads.

- **A move may legitimately change nothing.** `press` puts a line in the record, tells two
  people something and leaves every province exactly where it was. Everything above held —
  the timeline takes it, the journal writes it, undo walks back over it — because nothing in
  the engine ever asked a move to *do* anything. It was worth checking, because the whole
  machinery is built on watching whether the timeline grew.
- **`Machines.answering` stops when the timeline stops growing, and that is load-bearing in a
  way it had not been.** At the other two games a machine takes one turn. Here it writes five
  orders and then commits, one call at a time, and the run only ends because a machine that has
  written everything says `commit` — and the phase resolving is what moves the seat on. A
  machine that answered `Some` forever would spin. It cannot: the seat stops being its own.
- **Two graphs over one board, and neither is derivable from the other.** Armies walk the land,
  fleets sail the water and the coast, and a fleet may not cross a land border — Rome to Venice
  is a border and a fleet cannot use it. Nothing above the game had an opinion about this, which
  is the seam working; what it cost the *game* is that `Faults` had to grow up. Three hundred
  borders typed out by hand will have a mistake in them, so both ends of every border are
  written down and checked against each other. A table mirrored from one end agrees with itself
  however wrong it is, and Turncoats' twenty-three borders are small enough that mirroring is
  the right call. At three hundred it stops being one.
- **"A picture of a board must be the board" was the right rule; "one region, one cell" was the
  wrong reason it seemed impossible.** Turncoats prints a honeycomb, and can, because its borders
  are a patch of a triangular lattice: the picture *is* the border table, and `Board.problems`
  demands every one of the twenty-three be drawn. This map is not a lattice. A province here has
  up to eleven neighbours where a hexagon has six sides, so no arrangement of them could show all
  two hundred and six borders — and the first answer here was that a board which cannot be drawn
  faithfully is better not drawn at all.

  That was the wrong half of the choice, and it took four goes to find out why.

  **First**, a map under a weaker rule. What Turncoats' check really buys, the argument went, is
  not completeness but *truthfulness*, and on a board this size those come apart — so the
  provinces went on the honeycomb one hex apiece under a one-directional promise: every side
  drawn is a real border, a border the picture cannot reach is left undrawn rather than faked.
  Half the borders were drawn. Honest, and not much use.

  **Second**, the thing that actually mattered: **a province takes as many hexes as it needs.**
  Six sides is a limit on a cell, not on a country. A region three or four hexes across has sides
  to spare; its name repeats over all of them, and a side between two of its own hexes is the
  inside of a country rather than a border. That took it from a little over half to nine in ten.
  The shapes were grown rather than drawn — seeded a hex apiece and spread outward, always into
  space that met a neighbour they had not met, never into space beside a province they do not
  border.

  **Third**, one level down, because the drawing could not keep up. `Scene` could describe a grid
  of *cells* but not a grid of *regions*, so a province came out as four boxes that happened to
  share a name — and Spectre's tables, which have one wall in one colour and no way to leave a
  wall out, could not have drawn it any other way. A cell of a map is a
  [`Patch`](src/Table/Parts/Scene.fs) now and says which shape it belongs to. Both terminal
  readers hand the same layout the same rows and differ only in the alphabet they draw it with,
  so a province is one outline in the colour of whoever holds its centre, and the two maps are
  the same map wall for wall. That is the seam paying for itself in the direction it was meant
  to: what a third game needed was general, and the two games before it got it untouched.

  **Fourth**, the last tenth, one border at a time. The grower could only ever add a hex where
  one was free and safe — it could never take a hex off a neighbour or move a province across, so
  wherever that was the answer it stopped, and stopped somewhere that broke no rule and made no
  sense. The Eastern Mediterranean sat in the middle of the Ionian touching none of the three
  provinces it exists to touch; Burgundy was penned in by a row of Piedmont whose hexes touched
  only Piedmont and empty space. Undoing those by hand cost nothing anybody was using.

  So the rule this board ended up under is the one it was thought unable to keep. **Every side the
  picture draws is a real border, and every one of the two hundred and six borders is drawn** —
  checked both ways by `Atlas.problems` before a game is dealt, so an edit that quietly drops one
  fails in the words of the border it dropped. The generalisation is not that a picture may be
  incomplete. It is that **"one region, one cell" was never part of the rule** — and until a board
  turned up that broke it, there was no way to tell the two apart.
- **`View.Answer` stopped being a field with one caller.** It was put there for `rule 8`, and
  the second game filled it with a line saying there was nothing to explain. Here it is what
  makes an incomplete map safe to draw — `borders vie` is the whole truth for one province,
  which is exactly what the picture cannot be for all of them.
- **The browser check had one game's vocabulary written into it.** `tools/smoke.ps1` waited for
  a board whose heading `startsWith('Turn')`. Two games counted their turns, so it read as
  machinery; a game whose seasons are called Spring and Autumn is what made it a fact about
  those two games. It is a substituted string now, like everything else in that script that is
  about the game rather than about the browser.
- **And it found a check that had been quietly dead.** Running that script against a third game
  meant running it at all, and `-Game tictactoe` had been failing since that game's three
  renderers were folded into one `Scene`: a square stopped being a `.cell` and became a `.tile`,
  and the selector was never updated. Nothing said so, because a selector matching nothing
  throws inside the page and the page's own error was the first thing reported. Two values in a
  table. The check has been fixed and is passing; it had not run since the refactor that broke
  it.

**Where the seam was widened, said out loud.** `Play` cannot fail, and a phase resolving inside
it does a great deal — adjudication, retreats, centres changing hands, builds, elimination, and
walking on through however many phases have nobody in them. All of that comes back as notices,
which is right, but it means one move can produce twenty of them where the other two games
produce one or two. Nothing broke; `Model.LogDepth` truncates and the journal keeps the rest.
It is worth knowing that the engine's idea of "what one move can say" is looser than either of
the other games would suggest.

**And one place it is honestly worse than a real game of this.** Diplomacy is played in the
negotiation, and the negotiation happens in a room. `press` carries a line to one power with
the right secrecy, and that is all it is: there is no side channel, no simultaneous private
conversation, no way to lie to two people differently at the same moment except by sending two
messages. The machine at a seat does not read its press at all, and says so in `help`. This is
a program that adjudicates Diplomacy correctly and lets people talk through it; it is not a
program that plays Diplomacy for you.

### Layout

Seven layers, each depending only on the ones above it — and now twelve projects, because
every seam between them that can be a project boundary is one rather than a place in a list.

**Within a project, the order in its `.fsproj` is the architecture and the only thing
enforcing it**: F# compiles a project in the order its files are listed, so a layer cannot
reach into one beneath it even by accident. Read
[Prototyping.Engine.fsproj](src/Prototyping.Engine.fsproj) top to bottom — nothing in it mentions a
game.

**Between projects, the compiler enforces it outright.** A game references `Play`, `Play`
references `Net`, `Net` references `Table`, `Table` references `Engine`, and `Engine` references
nothing but `FSharp.Core`. "A game may reach the engine and the engine may not reach a game" used
to be a convention that happened to hold and could be checked by reading; it is now a thing that
will not build — and so is every other step of the layering. `Engine` cannot see a screen because
it cannot see `Table`; `Table` cannot open a socket because it cannot see `Net`. One source file
moved to get that — `Play.fs`, into a directory of its own, because a project wants one — and
every other file is where it always was, which is why the test scripts that `#load` them by path
did not change.

| Project | What it is |
| --- | --- |
| [src/Prototyping.Engine.fsproj](src/Prototyping.Engine.fsproj) | `Common` and `Engine`: the fold, the timeline, the record and the generator. Depends on `FSharp.Core` and nothing else |
| [src/Table/Prototyping.Table.fsproj](src/Table/Prototyping.Table.fsproj) | The seam a game fills in, the screens, the parsing of a typed line, the record on disk and the menu. A terminal and a page, and no server |
| [src/Net/Prototyping.Net.fsproj](src/Net/Prototyping.Net.fsproj) | The same table with the players at different keyboards. The only project with a web server in it |
| [src/Play/Prototyping.Play.fsproj](src/Play/Prototyping.Play.fsproj) | Opening one. What a game's own `Program.fs` calls, and the only one of the four a game names |
| [src/Games/Turncoats/Turncoats.fsproj](src/Games/Turncoats/Turncoats.fsproj) | One game, and its own executable |
| [src/Games/TicTacToe/TicTacToe.fsproj](src/Games/TicTacToe/TicTacToe.fsproj) | " |
| [src/Games/Diplomacy/Diplomacy.fsproj](src/Games/Diplomacy/Diplomacy.fsproj) | " |
| [src/Games/Compile/Compile.fsproj](src/Games/Compile/Compile.fsproj) | " |
| [src/Games/Life/Life.fsproj](src/Games/Life/Life.fsproj) | " |
| [src/Games/Snake/Snake.fsproj](src/Games/Snake/Snake.fsproj) | " |
| [src/Games/Cascade/Cascade.fsproj](src/Games/Cascade/Cascade.fsproj) | " |
| [src/Games/Warband/Warband.fsproj](src/Games/Warband/Warband.fsproj) | " |
| [Proto.fsproj](Proto.fsproj) | All eight in one program, which asks which. What a clone runs |

A game's own executable is one game, one port and nothing else in the image, which is what
goes in a container. `Proto` is what somebody who wants all eight downloads once, and what
every `dotnet run` line in this README is.

**`src/Common`** — generic, and knows nothing about the game.

| File | Role |
| --- | --- |
| [Result.fs](src/Common/Result.fs) | The `result` computation expression used to chain an action's checks |
| [Cascade.fs](src/Common/Cascade.fs) | Settling a contest by measures applied in order, shared by ruling and winning. It is older than the game of the same name and has nothing to do with it |
| [Random.fs](src/Common/Random.fs) | An immutable SplitMix64 generator, passed along as a value |

**`src/Engine`** — what every turn-based game wants and none of them should write again.
It is compiled *before* the game and mentions nothing in it; see [A game-shaped
hole](#a-game-shaped-hole).

| File | Role |
| --- | --- |
| [Seats.fs](src/Engine/Seats.fs) | `PlayerId`: which seat, and nothing about who is in it |
| [Messages.fs](src/Engine/Messages.fs) | `Msg<'Move>`: the game's own move, and the three the engine answers itself - and the words a record writes those three in |
| [Told.fs](src/Engine/Told.fs) | `Told<'Move,'Notice>`: what the game said, and what the engine said |
| [Rules.fs](src/Engine/Rules.fs) | The seam: the seven questions the machinery asks a game. `Play` answering `None` with nothing to say is a move that did not happen, and leaves no line behind |
| [Timeline.fs](src/Engine/Timeline.fs) | Every state a game has stood in, with a finger on the present |
| [Journal.fs](src/Engine/Journal.fs) | The record of play: what was asked, by whom, and what came of it |
| [Model.fs](src/Engine/Model.fs) | The timeline, the journal, and the last few lines on screen |
| [Update.fs](src/Engine/Update.fs) | `Rules -> Msg -> Model -> Model`, and nothing in it can fail |
| [Machines.fs](src/Engine/Machines.fs) | Seats played by something that is not a person: which machines a game offers, which seats they take and what each draws its choices out of, and when they take their turns |

**`src/Table`** — how a game is *read*, which is the second seam. Nothing here names a
game, a piece, or a board. Two folders and one file between them, and the file is the
point: `Parts` does not know there is a seam, and `Playing` is written against it.

| File | Role |
| --- | --- |
| [Showing.fs](src/Table/Parts/Showing.fs) | What a table shows one console, and which console it is for - and the three sounds a table may be heard making |
| [Waiting.fs](src/Table/Parts/Waiting.fs) | A seat at a table that has not filled up yet, as the person waiting sees it |
| [Scene.fs](src/Table/Parts/Scene.fs) | What a screen is made of, before anybody has decided what it looks like - and `Margins`, which carries how much of it to draw and how far through a beat it is being drawn |
| [Palette.fs](src/Table/Parts/Palette.fs) | Which colour is drawn for what, keyed by the words a game says it colours - and what there is to choose from, which a `colours.txt` may add to |
| [Settings.fs](src/Table/Parts/Settings.fs) | What somebody settled on last time and gets again this time, written as the lines they would have typed |
| [Reach.fs](src/Table/Parts/Reach.fs) | How far a table can be reached and what it takes to sit down at one: the port, the word at the door, what it is all wrapped in, and an address as somebody says it |
| [Keys.fs](src/Table/Parts/Keys.fs) | Screens picked from rather than typed at: the rows, where a person has got to, and what a key press comes to |
| [Commands.fs](src/Table/Parts/Commands.fs) | The words a person can type at *any* game, read once for all of them |
| [View.fs](src/Table/Parts/View.fs) | Every screen a player reads, generic in the game |
| [Seating.fs](src/Table/Parts/Seating.fs) | Who is in each seat before a game is dealt, and everything that falls out of it |
| [Tint.fs](src/Table/Parts/Tint.fs) | Colour laid over writing already laid out, and Spectre's output as a string |
| [Page.fs](src/Table/Parts/Page.fs) | The browser's shell: the stream, the prompt, the colour form, the door, and the two places a fragment can land. A game brings a stylesheet and a name for the tab |
| [Screens.fs](src/Table/Parts/Screens.fs) | Driving a screen at a real terminal: clear it, draw it, read a key, hand back a line |
| [Options.fs](src/Table/Parts/Options.fs) | The settings: a short menu, and a page behind each row of it - Audio, Video, and whatever this game lets you settle |
| [Readers.fs](src/Table/Parts/Readers.fs) | The three ways of reading a `Scene` - as text, as Spectre's widgets, as a page - written once for every game there will ever be |
| [Playable.fs](src/Table/Playable.fs) | **The seam**: everything a game has to say about itself to be read and played here |
| [Solo.fs](src/Table/Playing/Solo.fs) | The game at one keyboard, as a value: what a typed line does, who answers it, and what it asks written down |
| [Transcript.fs](src/Table/Playing/Transcript.fs) | A journal as a file, and a file back into a journal - what a record is called, taken apart in the same place it is put together, and every record there is to take up |
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

**`src/Games`** — a game is a folder, and inside it the two seams are two folders. `Rules` is
how it is played: no English a player reads is laid out there, no screen, and nothing from
`src/Table` — which is not a house rule but a fact you can check, because not one file in any
of them opens it. `Reading` is how it is read. `Offer.fs` joins the two, and is the only file
either layer above ever sees.

The games never mention each other, and nothing above them names any of them until
[Games.fs](src/Games.fs). What is in each folder is listed in that game's own README:

| | files | |
| --- | --- | --- |
| [Turncoats](src/Games/Turncoats/README.md#the-files) | 21 | a map, hidden bags, a generator, four kinds of move, three ways of being won, and three views written out by hand |
| [Noughts and crosses](src/Games/TicTacToe/README.md#the-files) | 10 | nine squares, nothing hidden, and its screens described once |
| [Diplomacy](src/Games/Diplomacy/README.md#the-files) | 13 | seven seats, no chance at all, three kinds of phase, and a map of three hundred borders |
| [Compile](src/Games/Compile/README.md#the-files) | 17 | a deck each, ninety cards of rules text, a draft, and a game that is three games in a row |
| [Life](src/Games/Life/README.md#the-files) | 8 | one seat, no opponent, no ending, and a board of four hundred cells drawn as rows rather than as cells |
| [Snake](src/Games/Snake/README.md#the-files) | 10 | a clock, one to four seats on one board, a generator that keeps drawing, and a machine that cannot see the end of the game |
| [Warband](src/Games/Warband/README.md#what-is-where) | 13 | ten hexes to a squad, six kinds of unit that are three answers apiece, a muster kept from the other seat, and a battle on a clock that nobody plays |

**And the way in**, which needs every layer above it — F# compiles in order and a file sees
only what came before it, so the door has to be the last thing built.

[Play.fs](src/Play/Play.fs) is the last file in the engine rather than the first outside it, and
that is the point of it: what opening a game *involves* — dealing one, keeping its record, the
menu loops, the settings pages, both tables and the browser — is generic in the game, and ends
in the interface that seals a game's types off. It is the whole of what a game's own door has
to call.

| File | Role |
| --- | --- |
| [Play.fs](src/Play/Play.fs) | The above, ending in `Chosen` — and `Play.only`, which is what `main` is at a game's own executable |
| [Games/*/Program.fs](src/Games/Turncoats/Program.fs) | One line each, and the same line at all eight: `Play.only Offer.ways argv` |
| [Games.fs](src/Games.fs) | The games there are, and the only file in the program that names more than one |
| [Program.fs](src/Program.fs) | Which game a line is about, the screen that asks when nothing says, and nothing else |

A game's door being one line is the fair test of the two seams. By the time a game is a
`Playable` there is nothing left for a way in to decide: what a line means, what the menu
offers, where a record goes and how far a table reaches are all settled above it, generically,
and none of it is written six times.

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
     3  Continue a game  one you put down, taken up where it was left
     4  How it is drawn  now plain - plain text, and nothing this terminal has to understand
     5  Settings         how it is drawn, in what colours, and kept for next time
     6  Rules            the rules and the commands, at length
     7  Quit

  Move with the arrows or w and s. Enter takes the one marked ->, and so does its number.

  Or type it: a seat each, from you, easy, medium, hard, joins - 'play you hard joins' to deal
  one, 'serve you medium' to read it in a browser, 'seats you you' to lay it out
  first. The short ways still hold: '3' for a game of three, '3 42' for that same
  game again, 'serve 3', 'host 3', 'vs <skill>...' for easy, medium, hard,
  'join <address>', 'continue', 'replay <file>', 'view <plain, rich>',
  'settings', 'rules', 'quit'.
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

### A bench, for what is not a board

Everything above is a screen of a game — being dealt, being joined, being taken up. The ninth
game asked for one that is not. EndTimes has its players build a summoner before anything is
dealt, keep it between games and pick it up at the start of one, and there was nowhere for that
to live.

`Playable.Aside` is the answer, and it is a word, two labels, a screen and a reader:

```fsharp
type Aside =
    { Word: string                       // what opens it, typed or picked
      Says: string
      Does: string
      Screen: unit -> Keys.Screen        // drawn again after every line
      Read: string -> Result<string, string> }
```

**It holds no state**, which is the whole of why it is one field rather than a type parameter on
`Playable` that seven games with no bench would have carried anyway. Whatever the bench remembers,
the game remembers — on disk, in a `lazy`, wherever it likes; the table neither knows nor asks.
`Screen` is a function for the same reason `Rings` reads off the state: it is drawn again after
every line, and a bench that could not show what the last line did to it would be a bench nobody
could work at.

Two things about where it sits. `Menu.screen` now numbers its rows **by where they end up** rather
than by hand, because a bench puts a row in the middle of that list and every number after it was
otherwise one out — right until the day somebody adds a row. And the loop that drives a bench does
*not* go through `Menu.choose`:

```fsharp
| [ "back" ] | [ "menu" ] -> None
| [ "quit" ] | [ "exit" ] | [ "q" ] -> Some(Done 0)
| _ -> aside.Read line
```

At a bench the game's words come first, so a bench with a row called `3` opens that row instead of
dealing a game of three, and a bench may have any word it likes without first checking what the
menu had left. The four that navigate are answered there by name, so nothing a game does can take
them away from a player — and `Conforms.against` holds every bench to the other half of that
bargain: it may not be opened by a word the menu already answers to, and it may not swallow a line
it did not understand.

### And the same keys at a board

Every menu in this program has been steerable since there were menus. A game on a clock has been
steerable since Snake — `Pulse.Pressed`, keys doing things, Enter opening the prompt. A game of
*turns* had a board and a `ReadLine`, and the arrow keys that walk every list in the program did
nothing at one. `Playable.Steering` closes that gap:

```fsharp
Steering: string -> PlayerId -> Model<'Move, 'State, 'Notice> -> Keys.Screen option
```

A game hands back a screen for where it stands and the table steers it with the machinery the menus
already use, so there is **one** way of walking a list here rather than two. `None` means what it
always meant: draw the board and read a line.

It makes the same bargain `Pressed` does, and `Conforms.against` holds it to it at every state the
suite walks through: **a row stands for a line the game already reads.** Nothing can be picked that
could not have been typed, a board driven by the arrow keys writes the same record as one driven by
hand, and `Backs` — what Escape sends — has to read as a line too. Enter with nothing typed takes
the marked row; Enter with a line underway sends the line, so no game can take the prompt away from
anybody, and `w`/`a`/`s`/`d` are letters again the moment a line is started.

The board as the table drew it is handed in with the seat it was drawn for, so a game may put it
above its rows, replace it, or ignore it — the table has already chosen the view and the margins,
and the seam does not second-guess either.

The one thing [`Play.loop`](src/Play/Play.fs) now carries across a move is **where the mark had got
to**. Every line rebuilds the screen from the state it left behind, and a mark that sprang back to
the top each time would make walking one row through its choices impossible — press right, the line
goes, the screen comes back, and the mark is still on the row you were walking.

That place is a **path** — which row of the first screen, which row of the screen that one opens, and
so on — rather than a row on the first screen alone. It has to be a path rather than the screens
themselves, because a game builds its screens afresh every time it is asked and the ones somebody
walked down are stale by the moment they are wanted again; `Keys.standing` walks the path down
whatever came back this time, and stops early where the screens no longer go that deep. Keeping only
the first row is what this used to do, and it made a list one deep somewhere you pass through rather
than somewhere you live: take anything off it and you were dropped back out at the top. A list of
things to *tick* was unusable — every tick threw you off the list.

**A screen of bare names is drawn in columns.** A row with something to say beside it is a line of
prose and keeps its own line; a screen of nothing but names is a list of names, read down one column
and then down the next, so that walking off the foot of one arrives at the head of the following one
— which is what up and down already do, and there is nothing that walks sideways between columns. A
dozen short rows drawn one to a line is a dozen lines of a window that had a board to show.

A console reading piped lines has no arrow to press, so it takes the other branch and is exactly the
loop it always was. Every script that drives a game by feeding it lines is untouched.

**A board is as long as it likes and a window is not.** Clearing the terminal and then writing more
lines than it holds is what made a tall screen arrive in pieces - the terminal scrolled what had
just been cleared, and the eye caught the halves. So a steered screen is *drawn over* rather than
cleared and written again, the way a board on a clock always was; and `Screens.fitting` trims the
board to what is left after the rows, the note and the prompt, which are how somebody gets off the
screen and are therefore never what gets cut. What was cut says so, because a board that quietly
stopped early would be a board somebody read the wrong answer off.

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
dotnet fsi tests/diplomacy.fsx  # the third game - the adjudication cases every set of these
                                #   rules is judged on, the year and its skipped phases, what
                                #   one seat may read of another's orders, and a few thousand
                                #   machine moves played out and replayed from the record
dotnet fsi tests/compile.fsx    # the fourth game - the draft, the lines, the deal, that a
                                #   hand is on nobody else's screen, and that eighteen cards
                                #   stay eighteen wherever they are
dotnet fsi tests/life.fsx       # the fifth game - Conway's rule against the shapes that test
                                #   it, and one seat, no opponent and no ending going through
                                #   the same two seams as the rest
dotnet fsi tests/snake.fsx      # the sixth game - the three ways a snake stops, the square its
                                #   own tail is leaving, a table of one and a table of four, and
                                #   a machine that counts the room a step leaves it
dotnet fsi tests/cascade.fsx    # the seventh game - the rule read off a board laid out by hand,
                                #   a wave landing all at once, a cell set off a second time, and
                                #   a board drawn three ways across one beat
```

**Seven harnesses, and the reason is `dotnet fsi` rather than the program.** A script names a
loaded file by its basename, and every game has a `Turn.fs` and a `Words.fs` - which
compile happily side by side in one project and cannot be loaded into one script. So
[Checks.fsx](tests/Checks.fsx) keeps score and loads nothing at all, and
[Whole.fsx](tests/Whole.fsx), [Noughts.fsx](tests/Noughts.fsx),
[Europe.fsx](tests/Europe.fsx), [Compiled.fsx](tests/Compiled.fsx),
[Living.fsx](tests/Living.fsx), [Slither.fsx](tests/Slither.fsx) and
[Cascading.fsx](tests/Cascading.fsx) each load the same stack in the project's own order with one
game on the end of it. The thirteen scripts above the last six used to keep a hand-ordered
`#load` list apiece; they had already drifted, which is the same disease the seams are for.

Two more, which want a process rather than a value:

```powershell
pwsh tools/smoke.ps1                  # a real browser, driven: the stream, the box, the
pwsh tools/smoke.ps1 -Game tictactoe  #   buttons, the knock, the door
pwsh tools/smoke.ps1 -Game diplomacy
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

The obvious next step is not taken: the scripts could `#r` the built assemblies instead
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

`-Game` says which. What differs between one game and the next is a row of a table at the top
of the script — what a board is made of, what a player types at one, what the table says back —
and everything under it is between this program and a browser. **Two things had been written
into that machinery rather than into the table, and a third game is what found them.** The
script waited for a board whose heading `startsWith('Turn')`, which was true of two games that
count their turns and is not true of one whose seasons are called Spring and Autumn. And the
number of seats was the literal `2`, because there had never been a game that was not — a
browser can only sit in one of seven seats, so every other seat has to be filled or the table waits
forever for people who are not coming.

**And it found a check that had stopped running.** `-Game tictactoe` had been failing since
that game's three renderers were folded into one `Scene`: a square stopped being a `.cell` and
became a `.tile`, and the selector in the table was never updated. Nothing said so, because a
selector matching nothing throws inside the page and the page's own error is the first thing
reported — so the failure read as "the page is broken", which is exactly the alarm this script
exists to raise honestly. It is two values, they are fixed, and all three games pass.

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

## One file each

```powershell
pwsh tools/publish.ps1                     # every program, both shapes, for this machine
pwsh tools/publish.ps1 -Shape portable     # just the small one
pwsh tools/publish.ps1 -Program Turncoats  # just the one game
pwsh tools/publish.ps1 -Runtime linux-x64  # for somebody else's machine
```

Nine programs come out: one per game, and `Proto`, which has all eight in it and asks
which. A game's own file is one game, one port and nothing else — which is what goes in a
container — and `Proto` is what somebody who wants all eight downloads once.

| | size | wants |
| --- | --- | --- |
| `portable` | 6.1 – 7.2 MB | the ASP.NET Core 10 runtime installed |
| `standalone` | ~105 MB | nothing at all |

The checks below are run against each of the eight, and they are not the same lines at each: a
game's own file takes `serve 2` and the one with eight games in it takes `tictactoe serve 2`.
That difference is the whole of what publishing separately changed, so it is checked rather
than assumed — `Turncoats.exe` printing `Turncoats turncoats play 2` is a line that runs and
then refuses, which is worse than one that does not run at all.

**Publishing passes `-p:SelfContained` rather than `--self-contained`, and the difference is
load-bearing.** The second sets the property on the project named and on nothing else, so
publishing `Proto` portable left the games it references self-contained and the SDK
refused the mixture outright (NETSDK1151). A `-p:` on the command line is a global property:
it reaches every project in the graph and overrides what each says for itself, which is
exactly what "this whole publish is one shape" means.

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
The type initializer for '<StartupCode$Proto>.$Launch' threw an exception.
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
    Proto turncoats join greg-pc --code kbd4-9mtx-7rfp             # from a published file
    Turncoats join greg-pc --code kbd4-9mtx-7rfp                   # from that game's own file
```

[Invoked.fs](src/Table/Parts/Invoked.fs) answers that once, and everything that prints a
line asks it: the usage and the examples, the address a table reads out to the room, the
line a dropped player is handed for getting back to their seat, the header on a record. What
it asks is not how this process was started but **where the reader is standing**.

Splitting the games into their own executables put a second question beside the first, and
the third line above is it: `Turncoats` has already said which game, so nothing it prints
should say it again. That is one flag, set by `Play.only` and by nothing else — a program
cannot see how many games were compiled into it, so the door that knows says so once, before
it opens anything. The same flag is why a game's own file, handed a record belonging to a
different game, says which game that is rather than inventing a command for an executable it
has never seen.

It also broke the first question, quietly, and in the way these things usually break: "is
there a project here to `dotnet run`" was enough while there was one project, and there are
six now. A clone's root holds a project that `dotnet run` would run and it is not the game
whose executable somebody just started from that folder — so `Turncoats.exe` printing `dotnet
run -- play 5` there was an instruction that runs, and runs something else, which is worse
than one that does not run. The project has to be *ours*, so every project here sets
`AssemblyName` to its own file's name and the question is one lookup.

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
| [Argu](https://fsprojects.github.io/Argu/) | the command surface both ways round: `play`, `serve`, `host`, `house`, `join`, `replay`, and a command line as a value the program can write |
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
