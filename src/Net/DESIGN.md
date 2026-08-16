# A house of tables

*A design, not a description. Nothing here is built yet.*

One game's program should be able to hold more than one game of it at once: a page listing
what is being played, what is waiting for players, and what has finished, with a way to open a
new one and a way to sit down at somebody else's.

**It is one game's house.** `Turncoats` lists games of Turncoats and knows of no other. There
is no front door above the four games and there should not be: a container runs one program,
one port, one game, and a list of every game on the machine is a different thing at a different
level, most likely not written in F# at all. Everything below assumes that and gets simpler for
it.

## The word

`Net/Lobby.fs` already means *one dealt table with consoles at it*, and that is the right word
for what it is. The new thing is above it and needs its own: **a house**, holding **tables**.
A player enters the house, sees the tables, and sits down at one.

## What it is built out of, which is more than expected

The useful discovery is that the hard part is already done.

`Table` in [Server.fs](Server.fs) is **already a non-generic interface**:

```fsharp
type Table =
    abstract Sits: console:string * offered:string * resuming:string option * view:string * palette:string -> Post list
    abstract Said: console:string * line:string -> Post list
    abstract Left: console:string -> Post list
```

Its own comment says the types stop there "the same way they stop at `Chosen`". `Held<'Move,
'State, 'Notice>` implements it behind a lock, writes the record after every change, and hands
back a list of things to say. **A house can hold a `Table list` today**, with no new machinery
at all, because everything crossing that boundary is already a string.

So the fear that the type-erasure problem returns in full is wrong. What is missing is smaller
and in two pieces.

### Where those pieces live — **moved**

`Table`, `Held` and `Aside` were in `Server.fs`, above a wall of `Microsoft.AspNetCore` opens,
and used **none** of it. That put them out of reach of `dotnet fsi`, because `tests/Whole.fsx`
loads as far as `Lobby.fs` and stops — deliberately, since everything past it wants ASP.NET and
SignalR.

They are in [Tables.fs](Tables.fs) now, between the lobby and the wire, and nothing in that
file has met a socket. It is the difference between a seam that gets *checked* and one that
gets *smoke-tested*: an interface whose only implementation needs a web server running is an
interface that will be exercised by starting a web server.

### The one new seam: making a table — **done**

`Server.host` takes an **already-dealt model**. Everything about dealing — `game.Rules.Deal`,
`game.Seating`, the seed — is generic in the game, and a house has to do it on demand, at a
moment when it holds nothing but a `Table list`.

That is the whole of the new existential, and it is small:

```fsharp
/// Opening a table without knowing what a move is at this game.
type Hosting =
    /// Deal one, and hand back something to play it. The seating says who is a person and
    /// who is the machine, in the words `Seating` already reads.
    abstract Deals: seating:Sitter list * seed:uint64 option -> Result<Table, string>

    /// And take one up off a record, which is the same question asked of a file.
    abstract Resumes: path:string -> Result<Table, string>

    /// How many may sit down, so the house can refuse a table of the wrong size before
    /// dealing rather than after.
    abstract Fewest: int
    abstract Most: int
```

Built by closing over a `Playable`, exactly as `Play.chosen` closes over one — the same trick
for the same reason, and the fourth time this program plays it: `Rules` seals what a game *is*,
`Playable` seals how it is read, `Chosen` seals it for a list to hold, and `Hosting` seals it
for a house to deal from. It lives in `Tables.fs` rather than beside `Play.chosen` for the
reason just given — `Play.fs` is compiled after the whole of the wire and cannot be loaded into
a script without it.

`Deals` takes a `Sitter list` rather than a count. `Seating` already models who is a person and
who is a machine and at what strength, `Menu.seats` is already the screen that asks, and both
are already generic. **The "open a new table" form is a screen this program has written and
tested**, and how many are playing is the length of the seating rather than a second thing to
keep in step.

Two things `tests/house.fsx` pinned down while it was being written:

- **A way this game does not have deals the plainest one** rather than refusing — the same
  answer a settings file already gets for a way that has since been renamed. A house that
  refused to deal over a stale name would be a house you could lock yourself out of by editing
  a file.
- **A resumed table has the game and nobody at it.** The seats are the game's, the moves are
  the record's, and the players have to come back to them — so it comes back `Filling`, not
  `Underway`. That is the honest reading of a restarted house: it holds the games, not the
  people. The machines *do* come back, because which seat one played is written in the record
  and nowhere else.

### What `Table` has to learn to say — **done**

Today a `Table` can be played and cannot be *described*, and a house is mostly a list of
descriptions. `Lobby.Standing` and `Table.Standing` are built:

```fsharp
type Stage = Filling | Underway | Finished

type Standing =
    { Stage: Stage
      Places: int      // every seat
      Machines: int    // played by the program; never empty, never sat at
      Sat: int         // taken by a person, here or not
      Reading: int     // ...and with a console actually attached
      Sitters: string list }
```

`Lobby` knew every part of it already, so this is a projection rather than new state. Two
things it turned out to be worth being careful about:

- **The three states that look alike from outside.** A seat nobody has taken, a seat somebody
  took and walked away from, and a seat the machine plays are one thing to a naive count and
  three to a house. A table of two with a machine at one seat has *no* seat going spare;
  a table whose second player dropped has none either, and is not waiting for anybody — it is
  simply one console short. `tests/lobby.fsx` holds all three cases.
- **No id and no time.** Which table this is, and when it was opened, are the *house's* facts:
  it made the table and did the naming. A table answering for them would be answering a
  question it was never asked.

Taken under the same lock as everything else, so a list cannot catch a table mid-move — one
value rather than members asked one at a time, which is the whole reason it is a record.

## Routing

Everything on the wire is single-table today and each needs a table in the path:

| Now | In a house |
| --- | --- |
| `Protocol.Path = "/table"` (SignalR hub) | `/table/{id}` |
| `MapGet "/"` — the board | `/` is the house; `/table/{id}` is a board |
| `Page.Stream`, `Page.Say` | per table |
| one `Browser.Pages()` | one per table, or one keyed by table |

`Protocol.Path` being a constant that both ends already agree on is the right shape; it becomes
a function of an id. The console's `--code` and `--token` handling is untouched.

The house page itself is a page like any other and should be built the way the rest are — a
`Scene` drawn by `Readers`, so it is one description read three ways rather than hand-written
HTML. It has no game in it, so it is drawn in plain colours, like the picker in `Program.fs`.

## Two doors, which already compose

`Reach` mints a word per table and `guarded` checks it. A house wants two layers:

- **The house door** — may you see the list at all. One word, given to the host at startup, or
  none for a room you trust. This is `Reach` exactly as it is.
- **The table door** — may you sit at *this* one. Also `Reach`, per table, which is what
  `Reach.minted()` is already for.

Neither needs new machinery; what is new is only that there are two of them and the house has
to say which one refused you. A table with no word inside a house that has one is the ordinary
case and should be the default: you proved who you were at the front door.

## Lifetime, and the thing that makes it cheap

A house that never forgets a table grows until the process dies. Tables need to go when they
are finished and nobody is reading them, and when they were opened and never filled.

The pleasant part is **rehydration**. Every table already writes a replayable record after
every change, and `Transcript.saved()` and `takeUp` already read them back. So a house that
restarts can offer the games it was holding, off the same files, with no new format and no
database — and `Resumes` above is the whole of what it needs to do it. That is worth building
early rather than bolting on, because it turns "the container was restarted" from a disaster
into a pause.

## What already works and should be left alone

- **Two people at one table read it differently.** A joining console sends its view and colours
  as strings and the table reads them against the game. That is per-console already and needs
  nothing from a house.
- **Records.** One stamp per table, `Transcript.stamping` per deal. Already right.
- **Locking.** `Held` locks per table, so tables are independent by construction and a house is
  not a new concurrency problem — only a dictionary that is itself locked.

## Whose settings does a hosted table use? — **settled**

`settings.txt` is read from the current directory, so a hosted table reads the *host's*. The
answer is that this is right for two of the three things it holds and wrong for the third, and
the split is along a line already drawn everywhere else in this program.

**Video and Audio stay where they are, and stay per console.** A joining console already sends
its view and colours as strings and the table reads them against the game; the bell is answered
at whichever keyboard is hearing it. None of that is the table's business and none of it should
become so. A host's own colours have never leaked to a guest and must not start.

**The way of playing belongs to the table.** It is not how a game is read — it is what game was
dealt, it goes in the record's deal line, and a house must be able to hold a plain game and a
game with the optional rule in it at the same time. So:

- `Deals` carries the way of playing, as a name.
- The house's "new table" form asks, offering the ways the game declares.
- The host's `settings.txt` supplies the **default that form opens on**, not the answer. Which
  is exactly what that page always meant: it settles what a *new* game is dealt as.

That is a smaller change than it looked, because `Playable` already carries the ways and the
Game page already reads and writes the name.

The corollary worth stating plainly: **a house must run with no `settings.txt` at all**, since
a container has no keyboard to have made one. `Settings.none` already answers every question
with the game's own default, so this holds today — but it should be a check rather than a thing
that happens to be true.

## Order of work

1. ~~**`Standing` on `Table`**, snapshot taken under the lock.~~ **Done** — `tests/lobby.fsx`.
2. ~~**`Hosting`**, carrying the way of playing per the decision above.~~ **Done** —
   `Tables.fs` and `tests/house.fsx`, sixteen checks and not a socket among them.
3. **The house as a value** — a dictionary of tables and the rules for opening, listing and
   reaping one, pure, with no wire in it. This is why the lobby is testable at all: it is a
   value that answers with a list of things to say. It goes in `Tables.fs` or beside it, and
   its checks go in `house.fsx`, which is already there and already loading nothing heavier
   than `Lobby`.
4. **Routing and the page**, last, because by then it is a thin thing over something already
   checked.

What step 3 still has to decide, none of which needs a socket to settle:

- **Reaping.** When does a finished table go, and when does one nobody ever sat at? A house
  that never forgets grows until the process does.
- **Naming.** Tables need ids that are safe in a URL and mean nothing to guess at. `Reach`
  already mints words of exactly that shape.
- **What a house does at startup.** Nothing, or read `logs/` and offer what it finds?
  `Resumes` makes the second cheap, and it is the difference between a restart being a pause
  and being a loss.
