# PrototypingGameEngine

An engine for turn-based games, in F#, with eight games on it. A game answers seven questions
about how it is *played* and twenty-two about how it is *read*, and is handed the rest: a
timeline to walk back through, a record that replays, a menu, a command line, three ways of
drawing a board, seats the machine can play, a browser, a table with the players at different
machines, and a house of such tables. None of that is written twice and none of it knows which
game it is carrying.

| | seats | |
| --- | --- | --- |
| [**Turncoats**](src/Games/Turncoats/README.md) | 2 to 5 | Stones on a map, hidden bags, and a game settled twice over |
| [**Noughts and crosses**](src/Games/TicTacToe/README.md) | 2 | Nine squares, three in a row, and nothing hidden |
| [**Diplomacy**](src/Games/Diplomacy/README.md) | 7 | Seven powers, thirty-four centres, no dice, and everybody writes at once |
| [**Compile**](src/Games/Compile/README.md) | 2 | A deck each, fifteen protocols drafted 1-2-2-1, and an optional rule of its own |
| [**Life**](src/Games/Life/README.md) | 1 | Conway's, on a board with its edges joined, on a clock you start and stop |
| [**Snake**](src/Games/Snake/README.md) | 1 to 4 | The arcade game: the snakes move on their own, and you only steer |
| [**Cascade**](src/Games/Cascade/README.md) | 1 | Two hundred and fifty-six elbows; touch one and watch the turning spread |
| [**Warband**](src/Games/Warband/README.md) | 2 | Two squads mustered out of each other's sight, then a battle nobody plays |

The rules of each game, the words it takes and what its machines weigh are in that game's own
README. This file is the engine: how to play any game on it, how to write one, and how the code
is arranged.

## Getting started

It needs the .NET 10 SDK and nothing else — no npm, and no build step besides `dotnet`.

```powershell
dotnet build PrototypingGameEngine.slnx
dotnet run                          # asks which game, then opens that game's menu
dotnet run -- tictactoe             # straight to one game's menu
dotnet run -- tictactoe play 2      # everything after the game's name is read by that game
dotnet run -- play 3                # a line that names no game means the first, Turncoats
dotnet run -- --help                # every command, and --help works on each of them too
```

Every game is also a program of its own, which is what a release hands you and what goes in a
container. There the game is already said, so its name is left off:

```powershell
dotnet run --project src/Games/TicTacToe -- play 2   # from a clone
TicTacToe play 2                                     # from a published file
```

Every line the program prints for somebody to type — the usage, the address a table reads out,
the line that gets a dropped player back to their seat — says `dotnet run --` from a clone and
the program's own name from a published file, so it runs wherever the reader is standing.

## Playing

### The command line

Six commands, the same at every game:

| | |
| --- | --- |
| `play <n>` | deal a game for that many and play it at this keyboard |
| `serve <n>` | the same, played in a browser on this machine |
| `host <n>` | open a table for that many and wait for them to arrive |
| `join <address>` | sit down at a table someone else is hosting |
| `house` | several games at once, listed on a page, dealt as people ask for them |
| `replay <file>` | take a saved game up where it was left, against the same players |

and the options that go with them:

| | goes with | |
| --- | --- | --- |
| `--seed <n>` | play, serve, host | deal from this seed rather than from the clock, for the same game again |
| `--rival <skill>` | play, serve | the machine plays the next seat; once per seat you give away |
| `--from <file>` | play, serve, host | take up this saved game instead of dealing one |
| `--view <name>` | play, host, join, replay | how the board is drawn at this terminal: `plain` or `rich` |
| `--colour <slot>=<colour>` | all but house | what to draw something in, as `blue=teal`; once per slot |
| `--port <n>` | serve, host, house | listen on this port rather than 5000 |
| `--code <word>` | serve, host, house, join | the word at the door, rather than one made up here |
| `--open` | serve, host, house | no word at the door: whoever can reach the address may sit down |
| `--cert <file.pfx>` `--cert-password <pw>` | serve, host, house | hold this certificate and speak https |
| `--behind` | serve, host, house | https ends at a tunnel or proxy in front, which forwards to this |
| `--at <address>` | serve, host, house | the address to tell players, when it is not this machine's own name |
| `--token <token>` | join | come back to the seat this token claimed |
| `--table <name>` | join | which table, at a house holding several |
| `--fill` | house | take up the games in `logs/` on the way up |

```powershell
dotnet run -- play 3 --seed 42 --view rich
dotnet run -- play 4 --rival easy --rival hard      # you, two machines, and a person at this keyboard
dotnet run -- serve 2 --rival hard                  # in a browser, against the machine
dotnet run -- replay logs/2026-08-02-215823-turncoats-2p-seed42.log
dotnet run -- host 3 --open                         # a table for three, no word at the door
dotnet run -- join greg-pc --code kbd4-9mtx-7rfp    # from each of the other machines
dotnet run -- house --fill                          # a house, with last night's games taken up
```

Whatever is wrong with a line is said at the door, before anything is dealt — a count the game
does not take, a view or a colour nobody has, a skill the game has no machine for.

### The menu

With no command, a game opens its menu:

```
=== Turncoats ===

  Stones on a map, and a seat each.

  -> 1  New game         how many are playing, and who each of them is
     2  Join a table     sit down at one somebody else is hosting
     3  Continue a game  one you put down, taken up where it was left
     4  How it is drawn  now plain - plain text, and nothing this terminal has to understand
     5  Settings         sound, how it is drawn, and what this game lets you settle
     6  Rules            the rules and the commands, at length
     7  Quit
```

Every screen in the program is walked the same way: the arrows or `w` and `s` move the mark, a
row's number takes it outright, Enter takes the one marked, Escape backs out, and `0` is the way
out of a settings page. On a row that turns through choices — a seat, a view, a colour — left and
right, or `a` and `d`, walk it. The space bar, or any letter that is not one of those four, starts
a line at the prompt instead, and from then on every key is a letter until the line is sent.

Every row stands for a line that could have been typed, so the two are one grammar. `New game`
asks how many, then lists the seats:

```
=== Who is playing ===

  -> 1  Seat 1             you     somebody at this keyboard
     2  Seat 2             hard    counts the tie-breakers too, and what you could do about it
     3  Deal               and play it here at this keyboard
     4  In a browser       the same game, read as a page
     5  How it is reached  a word at the door, in the clear, port 5000
```

Each seat is `you`, one of the game's machines by skill, or `joins` — somebody at their own
machine. A seating with a `joins` in it is a table to open and wait at, so `Deal` becomes `Open
the table`, and the screen behind `How it is reached` settles the port, the word at the door,
https and the address to tell people. The short ways of saying all this hold at the prompt too:
`3` for a game of three, `3 42` for that same game again, `play you hard joins`, `serve you
medium`, `vs easy hard`, `host 3`, `join <address>`, `continue`, `replay <file>`, `view rich`,
`settings`, `rules`, `quit`.

### At the prompt

Once a game is dealt the prompt reads the game's own moves — each game's README lists them, and
`help` prints them — and these, which are the table's and the same at every game:

| | |
| --- | --- |
| `undo`, `redo` | walk the game back and forward |
| `history` | the record so far |
| `save` | write the record now |
| `notes`, `commands`, `log` | hide or show the writing that explains the board, the box of commands, and what the game has been saying; `notes off` and `notes on` say which way |
| `sound`, `mute` | whether this table is heard as well as read |
| `view <name>` | draw the board another way |
| `restart`, `restart 42` | deal a fresh game to the same players, or that particular one |
| `players <n>` | deal afresh for a different number |
| `resign` | give the game up, but write it down |
| `help` | every command, at length |
| `quit` | leave; the record is written, and the game can be taken up again |

`quit` leaves the game standing rather than losing it: conceding is `resign`, which is a move made
on purpose. A finished game stays open at the prompt, and `undo` and `redo` walk it backwards and
forwards for reading.

### A game on a clock

Life, Snake, Cascade and Warband's battle move on their own. The board beats at a rate the game
sets, `+` and `-` wind it faster and slower, the keys the game names steer it — the arrows for
Snake, `p` to run and `.` to step for Life — and Enter opens the prompt for anything longer. A
beat is a move: nothing about real time reaches the rules, so a record of such a game replays
beat for beat with no clock involved, and `undo` walks back through beats like any other move.

## Records

A game writes itself to `logs/` when it ends, on `restart`, on the way out, and whenever you type
`save`; a hosted table writes after every move, since it can lose its host without warning. The
name is fixed when the game is dealt — `<stamp>-<game>-<n>p-seed<seed>.log` — so saving again
writes over the same file, and a sitting that added nothing to the game writes nothing.

There is one format. Every move is written exactly as it was typed, one to a line, and everything
else is a comment, so reading a record back is the job of the same parser that reads the prompt.
After a header saying as much, a record runs:

```
format 1
deal 2 42 you hard

#   1  turn 1, Player 1
recruit r 3
#      Player 1 recruits a Red stone into Greymarket.

#   2  turn 2, Player 2
undo
#      Taken back: recruit r 3.
```

The deal line carries what the moves cannot: how many were playing, the seed, and who was in
each seat — `you` for a person, a skill for a machine. Undo and redo are moves like any other, so
replaying passes through every state the original did, in order, and lands on the one it was
saved from. A replayed game writes a byte-identical record.

A record is a game to carry on with rather than a thing to look at. `Continue a game` at the menu
lists what there is — when each was put down, how many seats, how many moves — and `replay
<file>` names one outright. `play`, `serve` and `host` all take `--from <file>` in place of a
count, so a game put down at one keyboard can be taken up in a browser or as a table others join:
the machines keep their seats at the strength they were playing, and only the people move. A
resumed game goes on writing to the file it came from.

The generator ([Random.fs](src/Common/Random.fs)) is a value passed along with the state rather
than something that mutates, which is what makes a seed and a list of moves reproduce a game
exactly — including one that keeps drawing, as Snake does — and why undoing a draw and making it
again draws the same thing.

The records in `logs/` are committed on purpose: they are replay fixtures, and
[records.ps1](tools/records.ps1) takes every one of them back up on every CI run.

## How the board is drawn

Three views, and a reader is only ever offered the ones it can show:

| | shown | |
| --- | --- | --- |
| `plain` | at a terminal | blocks of text, and nothing the terminal has to understand |
| `rich` | at a terminal | panels, tables and charts in colour, via [Spectre.Console](https://spectreconsole.net) |
| `html` | in a browser | a page, updated in place |

`--view` on the command line, `How it is drawn` at the menu and `view <name>` at the prompt all
pick one. Round the board are three margins — the notes that explain it, the box of commands, and
the log of what the game has been saying — turned by `notes`, `commands` and `log`; with both
boxes off the log shrinks to its last three lines. `sound` and `mute` are a fourth switch of the
same kind. None of the four is part of the game: they reach neither the record nor the other
players, and at a table over a network each console has its own.

### Settings

`settings` at the menu opens three pages — **audio**, whether the table rings when your turn comes
round; **video**, which view and what colour each of the game's slots is drawn in; and **game**,
whatever this game lets you settle about itself. Compile is played with or without its optional
rule, and Snake on a clock or a step at a time; each way keeps a name of its own, so what is
settled here is what a *new* game is dealt as, and a record still replays as it was played.

`save` on any page writes `settings.txt` beside the records, and every way into a game reads it:

```
view rich
bell off

[turncoats]
view rich
red lemon
blue azure

[compile]
plays compile-control
```

Every line is one you could have typed at the screen it belongs to. The view above the games is
the default; a game's own line wins for that game, and a view a game does not have falls back to
its plainest. The command line reads the same file, and `--view` and `--colour` are the later
word, for that run only.

### Colours

A game names the things it draws in colour — Turncoats its three factions, Diplomacy its seven
powers — and the video page sets each one, by row or by line: `blue teal`. Nineteen colours are
built in, each with a short word of its own. A `colours.txt` beside the records adds to them:

```
rust     #b7410e     # a new one, on the end of the list
crimson  #ff2d2d     # brighter than the built-in, and in its place
no slate             # one I never use
```

What a game draws itself in comes from the built-in list, so dropping a colour changes what you
are offered and can never leave a game unable to draw itself. A line the file gets wrong is said
at the menu rather than passed over.

## Against the machine

Any seat can be the machine's. At the menu, walk a seat along to a skill; on the command line,
`--rival <skill>` once per seat, the first taking the seat after yours. Turncoats, noughts and
crosses, Diplomacy, Compile, Warband and Snake a step at a time each bring machines of their own,
named and described by the game — `--help` lists them, and the game's README says what each one
weighs.

A machine is held to what a player is held to. It asks the rules what they will take rather than
keeping a copy of them, it reads only what its own seat can see, and it picks a `Move` — the very
thing a typed line turns into — so its moves land in the record in the same words as yours. Its
generator is drawn from the deal's seed and its seat, so the same deal against the same machines
plays the same game twice. After a person moves the machines answer for as long as the seat to act
is one of theirs; `undo` takes their answers back along with yours and stops where a person has to
decide.

Nothing on the board says which seats are the machine's, so the table says so once, when you sit
down.

## From other machines

### A hosted table

```powershell
dotnet run -- host 3                        # opens a table for three and waits
dotnet run -- join greg-pc --code <word>    # from each of the other machines
```

The host prints the addresses it can be reached at, the word at its door, and the whole line each
of the others types — or the address to open in a browser, which sits down at the same table. A
`join` takes a machine name, an address or a whole URL; a bare name is given the usual port. The
host's own seat is taken by the hosting process over the same wire as everybody else's. Nobody
plays until every seat a person has is taken; a seat the machine plays was never empty.

A seat once taken keeps its token, and a console that drops leaves the seat taken but empty, so
its player can come back to it — the client prints the `join --token` line that does so, and
reconnects with it by itself. `quit` at a table is the same: the seat is kept and the game waits.
The table stands until whoever opened it presses Ctrl+C.

The table is the only thing that holds the game. A client sends the line that was typed and prints
the board it is sent back, drawn for that seat and no other, so a client cannot show anybody
something they should not see. Three things a table refuses that one keyboard allows: a move out
of turn, `undo` and `redo` — walking a game back and forward is a way of reading what is hidden —
and `restart`. A game of turns takes a move from whoever is to play; a board on a clock takes a
steer from anybody.

When your turn comes round and nothing you did brought it, a terminal rings and a page marks its
tab, and raises a notification if you allowed one with the `notify` button.

### In a browser

`serve <n>` is `play` with a page instead of a terminal: one hot seat, with the screen belonging
to whoever is to play, so it starts the moment it is opened and `undo` works. A browser and a
terminal can sit at one hosted table, and each is drawn its own board in its own view. The one
piece of JavaScript is [Datastar](https://data-star.dev), committed under `assets/` and embedded
in the program, so a table needs nothing fetched from anywhere.

### A house

`house` opens no table and deals them on demand. The page it serves lists what is being played,
with a link to open a table at each size the game takes, and a row for every table there is.
Open one and you are sent to a table of your own, at an address to read out to whoever is
playing; a terminal joins one by the name shown beside it, `join <address> --table <name>`.

A table nobody sat at goes after an hour, a finished one after a day, and one with anybody at it
never. Sweeping touches nothing on disk — every table writes the same record every game writes —
and `--fill` reads them all back on the way up, so a restart is a pause rather than a loss.

### Further than a room

A table gets a word at its door unless `--open` says the room is trusted: twelve letters, read out
loud as easily as copied, with the case and the dashes forgiven when it comes back. Somebody who
turns up without it gets a page with one box on it; somebody who keeps guessing is slowed to one
try every few seconds, counted per caller and for the door as a whole, and only wrong answers
count — a player with the word is never held up.

Everything crosses in the clear over http unless one of two things is said. `--cert <file.pfx>`
ends https here, with the certificate in Kestrel; `--behind` says a tunnel or proxy in front ends
it and forwards plain http, which changes what this process believes about the far end — the
forwarded headers are read, and cookies are marked secure exactly when the browser will send them
back. `--at` is the address to print for players when it is not this machine's own name. Give a
table a hostname of its own rather than a path under one: a page fetches its stream from the
root.

A wire between houses goes quiet, so the table sends a heartbeat down each page's stream; a page
that stops hearing it says so, tries the address until something answers, and reloads, losing
nothing. A terminal waits longer and retries for as long as it takes, since its seat is kept.

There are no accounts: one word lets you into the table, and the seat token is what says who you
are afterwards. [reach.fsx](tests/reach.fsx) holds the door and the addresses as values,
[cli.fsx](tests/cli.fsx) writes every option out and reads it back, and
[smoke.ps1](tools/smoke.ps1) drives a real browser at a real locked table.

### In a container

```powershell
docker build -t turncoats .
docker build -t tictactoe --build-arg GAME=TicTacToe .
docker run -p 5000:5000 -v proto-logs:/data/logs turncoats     # a house, with a word at the door
docker run -p 5000:5000 turncoats house --code hunter2          # ...with this word
docker run -p 5000:5000 turncoats host 3 --open                 # one table, not a house
```

The entry point is the game and the command is its configuration: anything the game takes on a
command line it takes here, and there is no second way of saying any of it. Run bare, a container
opens a house on `PORT` with `--fill`, mints a word for the door and says it in `docker logs`.
`/data/logs` is the one directory worth keeping, since it is what `--fill` reads; there is no
other state. [image.ps1](tools/image.ps1) builds the image and plays a game in it, and CI runs
it for all eight games.

## Writing a game

Start from the template rather than by copying a game. It generates the whole shape — a project,
`Rules/` and `Reading/`, an `Offer.fs`, a README — already playing and already passing the
contract:

```powershell
dotnet new install templates/game
dotnet new proto-game -n Foo -o src/Games/Foo
```

Three things it cannot do for you, which its README writes out: add the project to
`PrototypingGameEngine.slnx` and a reference to it in `Proto.fsproj`; register it in
[Games.fs](src/Games.fs), the only file that names more than one game; and give it a suite,
`tests/foo.fsx`, that loads a harness of its own and [Conforms.fsx](tests/Conforms.fsx) and says

```fsharp
Conforms.against foo 2 [ "a line"; "another" ]
```

which holds the game to everything the table expects of one. [template.ps1](tools/template.ps1)
generates a game, builds it, plays it and takes it away again, and CI runs it.

### The seam

A game is a folder with the two halves in it — `Rules/`, how it is played, which knows nothing of
screens or English; `Reading/`, how it is read — joined in `Offer.fs` as one `Playable`, the only
value the table ever sees. The seam is three records and thirty-four members, and
[SEAM.md](SEAM.md) is the ledger of every change to it and which game asked.

**`Rules`** ([Rules.fs](src/Engine/Rules.fs)) is how it is played:

```fsharp
type Rules<'Move, 'State, 'Notice> =
    { Deal: int -> uint64 -> Result<'State, string>       // how many, and a seed
      Play: 'Move -> 'State -> 'State option * 'Notice list
      Active: 'State -> PlayerId
      Turn: 'State -> int
      Over: 'State -> bool
      Seats: 'State -> int
      Reseed: 'State -> uint64 }                           // what a restart deals from
```

`Play` cannot fail. A refusal is `None` and notices saying why; `None` with nothing to say is a
move that did not happen and leaves no line in the record. The engine folds a `Msg` — `Make` of
a move, `Undo`, `Redo`, `Restart` — into a `Model` holding the timeline, the journal and the last
few lines said, and nothing in `Update.update` can throw.

**`Playable`** ([Playable.fs](src/Table/Playable.fs)) is how it is read, and everything else:

| | |
| --- | --- |
| `Rules` | the above |
| `Name`, `Title`, `Blurb` | the word on a command line, the heading, and the sentence under it |
| `Fewest`, `Most` | how many seats it takes |
| `Read`, `Write` | a typed line as a `Command`, and a `Msg` back into the words a record keeps |
| `Seat` | what a seat is called |
| `Says`, `SeenBy` | a notice in words, for the table and for one seat — where anything hidden lives |
| `Rings` | what the board is sounding, read off the state |
| `Resign` | the move `resign` stands for, if there is one |
| `Faults` | what is wrong with the game as described; the table refuses to open one that has any |
| `Slots` | what the game draws in colour, for the video page |
| `Skills`, `Seating` | the machines it offers, and which seats they take; `Playable.seating` builds the second from the parts |
| `Pulse` | the clock, for a board that moves on its own |
| `Aside` | a section of the menu the game owns, for something that is not a board |
| `Steering` | rows to walk with the arrows at the board, each standing for a line |
| `Page` | a browser's title, stylesheet, keys and prompt |
| `Views` | every way of drawing it: `Readers.views scenes`, or a `View` written by hand |

`Read` reads only the words the game invented; `undo`, `save`, `view`, `restart`, `resign`,
`quit` and the rest are read once for every game in [Commands.fs](src/Table/Parts/Commands.fs).

**`Pulse`** is filled in by a game on a clock: `Every`, how long a table leaves between beats;
`Beat`, the move a beat is; `Frames`, how many times to draw between two beats for a board that
moves between them; `Pressed`, what a key stands for, as a line the game already reads; and
`Free`, whether the clock frees every seat to speak or the game is still taking turns.

### Screens

A game describes each screen once as a [`Scene`](src/Table/Parts/Scene.fs) — headings, lines,
notes, blocks, tiles in rows, patches of a map, a field of glyphs, a control that types a line —
with colour as a `Tone` naming one of the game's slots and nothing in it about what it looks like.
[Readers.fs](src/Table/Parts/Readers.fs) draws one three ways, so

```fsharp
Views = Readers.views scenes
```

is the whole of a game's part in `plain`, `rich` and `html`, and a block cannot be shown by one
reader and missing from another. `Scenes` asks for the board, the history, the answer to the
game's own question, the rules, the waiting room, what the game marks, and how wide it draws.
A game that wants a screen no general reader would think of writes a `View` instead; Turncoats
does, for its honeycomb.

### What the rest expects

- **Counting** goes through `Prototyping.Common.Counting`, never by hand: `Counting.several
  "turn" "turns"` is "1 turn" and "3 turns". [counting.fsx](tests/counting.fsx) sweeps every
  game for a count that does not agree with its noun.
- **Chance** comes from the seed `Deal` is handed, through `Rng`, a generator passed along as a
  value. A game with none says so with `Reseed = fun _ -> 0UL`.
- **What a seat may know** is decided in `SeenBy` and in what a machine is handed, never in a
  view. [view.fsx](tests/view.fsx) holds Turncoats' three views to that.
- **A change to `Rules`, `Playable` or `Pulse`** belongs in `Conforms.fsx`, so all eight games
  answer for it at once, and gets a row in `SEAM.md`.

The rest of the house rules are in [CLAUDE.md](CLAUDE.md).

## How it is put together

`Common` → `Engine` → `Table` → `Net` → `Play` → a game, and nothing lower reaches up. Each step
is a project boundary, so reaching up is a build error rather than a rule to remember; within a
project, files compile in the order the `.fsproj` lists them, and a file sees only what came
before it.

| | |
| --- | --- |
| [src/Prototyping.Engine.fsproj](src/Prototyping.Engine.fsproj) | `Common` and `Engine`: the fold, the timeline, the record and the generator. Depends on `FSharp.Core` and nothing else |
| [src/Table](src/Table/Prototyping.Table.fsproj) | The seam a game fills in, the screens, the prompt, the menu, the settings, the record on disk. A terminal and a page, and no server |
| [src/Net](src/Net/Prototyping.Net.fsproj) | The same table with the players at different machines: seats and tokens, the host, the client, the house. The only project with a web server in it |
| [src/Play](src/Play/Prototyping.Play.fsproj) | Opening a game — the menu loops, both tables, the browser — ending in the one call a game's `Program.fs` makes |
| [src/Games/*](src/Games) | One project per game, each its own executable |
| [Proto.fsproj](Proto.fsproj) | All eight in one program, which asks which. What a clone runs |

Each folder below lists its files in the order they compile:

```
src/Common      Result, Tiebreak, Random, Counting, Grid, Notch - generic, and shared by the games
src/Engine      Seats, Messages, Told, Rules, Timeline, Journal, Model, Update, Machines
src/Table
  Parts/        Invoked, Posts, Waiting, Scene, Palette, Settings, Reach, Keys, Commands, View,
                Seating, Tint, Page, Screens, Options, Readers - none of which knows there is a seam
  Playable.fs   the seam
  Playing/      Solo (the game at one keyboard, as a value), Transcript (a journal as a file),
                Menu, Launch (the command line, read and written from one declaration)
src/Net         Protocol, Pages, Browser, Lobby (seats, tokens, whose turn), Tables, House,
                Announce, Server, Client
src/Play        Play.fs - what opening a game involves, generic in the game
src/Games       <Name>/Rules, <Name>/Reading, <Name>/Offer.fs, <Name>/Program.fs
src/Games.fs    the games there are
src/Program.fs  which game a line is about, and the screen that asks when nothing says
```

Both tables are values. [Solo.fs](src/Table/Playing/Solo.fs) and [Lobby.fs](src/Net/Lobby.fs)
each take a typed line and give back the next table and a list of things to show, addressed to
somebody; the terminal, the hub and the page only carry lines in and screens out. That is what
lets the rules of a networked table — whose turn, who may sit, what is refused — be checked
without a socket.

The four engine projects pack as `Prototyping.Engine`, `Prototyping.Table`, `Prototyping.Net` and
`Prototyping.Play`, versioned together in [Directory.Build.props](Directory.Build.props); a game
outside this repository references `Prototyping.Play` and is otherwise the template.

## Tests

```powershell
pwsh tools/tests.ps1                 # every suite, in parallel
pwsh tools/tests.ps1 -Only solo,lobby
dotnet fsi tests/solo.fsx            # one, by hand
```

Suites are `tests/*.fsx` scripts run by `dotnet fsi`, found rather than listed: a lower-case name
is a suite and runs, a capitalised one is a harness other files load. Each game's harness
(`Living.fsx`, `Slither.fsx`, …) loads [Stack.fsx](tests/Stack.fsx) — the engine, the table and
the wire — and then the game's own files in the order its project compiles them, and a game's
suite calls `Conforms.against` before anything else.

| | |
| --- | --- |
| `tictactoe`, `diplomacy`, `compile`, `life`, `snake`, `cascade`, `warband`, `turncoats` | one per game: its rules, its machines, and the contract |
| `actions`, `ruling`, `outcome`, `knowledge`, `history` | Turncoats' rules, and a record that survives the round trip |
| `properties` | invariants over games [FsCheck](https://fscheck.github.io/FsCheck/) deals and plays itself, shrunk to the shortest game that fails |
| `rival` | machines sat opposite each other and played out: nothing refused, every turn passing, `hard` beating `easy` |
| `view`, `html` | that no view shows a seat what it may not see, that the three say the same things, and that the page is well-formed with no control the game would refuse |
| `solo`, `lobby`, `house` | the three tables, as values: what a line does, who may sit, whose turn it is, what is swept |
| `cli`, `reach` | the command line written and read back, and the door and addresses as values |
| `counting` | every count in every game agreeing with its noun |

[Conforms.fsx](tests/Conforms.fsx) is the contract: the deal, the seats, reading and writing a
line, the timeline, the record, the notices, every view at every state, the machines, the clock,
the page, the bench, the steering, and the same table with the players at different keyboards.
A check spanning several games needs a load list of its own, as `counting.fsx` has, since each
harness compiles its own copy of the engine.

## Tools

| | |
| --- | --- |
| [tests.ps1](tools/tests.ps1) | every suite, in parallel, with a cap per suite |
| [template.ps1](tools/template.ps1) | generate a game from the template, build it, play it, take it away |
| [records.ps1](tools/records.ps1) | take every record in `logs/` back up |
| [wire.ps1](tools/wire.ps1) | a table over a real socket, with consoles sitting down at it |
| [smoke.ps1](tools/smoke.ps1) | a table in a real browser: turned away, slowed down, seated, still hearing the heartbeat (Windows) |
| [package.ps1](tools/package.ps1) | pack the four, and build a game outside the repository on them |
| [publish.ps1](tools/publish.ps1) | one file per program, `portable` (needs the ASP.NET Core 10 runtime) or `standalone`, and run |
| [image.ps1](tools/image.ps1) | build the container image and play a game in it |

A change is finished when `dotnet build PrototypingGameEngine.slnx` is clean — warnings are
errors — `dotnet fantomas src tests tools templates --check` is clean, and every suite passes.
[Fantomas](https://fsprojects.github.io/fantomas/) is pinned in
[.config/dotnet-tools.json](.config/dotnet-tools.json) (`dotnet tool restore` once) and set up in
[.editorconfig](.editorconfig). [CI](.github/workflows/build.yml) runs the build, the format
check, every suite, the template, four games over the wire, every record, the packages, two games
in a browser on Windows, and the image for all eight games.

Published files are never trimmed: the command line, the hub and the page's signals are all found
by reflection, and a trimmed build fails on its first line.

### What it depends on

| | |
| --- | --- |
| [Argu](https://fsprojects.github.io/Argu/) | the command line, read and written from one declaration |
| [Spectre.Console](https://spectreconsole.net) | the `rich` view's panels, tables and charts |
| [Falco.Markup](https://github.com/FalcoFramework/Falco.Markup), [Falco.Datastar](https://github.com/FalcoFramework/Falco.Datastar) | the `html` view's elements, and the page's attributes, stream and signals |
| ASP.NET Core and SignalR | the host, its hub, the streams held open to browsers, and the buckets at the door |
| [FsCheck](https://fscheck.github.io/FsCheck/) | the generated games in `properties.fsx`; test-time only |
| [assets/datastar.js](assets/datastar.js) | [Datastar](https://data-star.dev), committed and embedded rather than fetched |

Versions are pinned once, in [Directory.Build.props](Directory.Build.props) for the projects and
[tests/Packages.fsx](tests/Packages.fsx) for the scripts.
