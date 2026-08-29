# The seam, and what moved it

Eight games sit on this engine, and about four fifths of the program is not about any of them.
That claim is only worth something if there is a record of what it cost to keep — so this file is
the ledger: every change to the seam, which game demanded it, and what would break without it.

The seam is three types and thirty-three members between them:
[`Rules`](src/Engine/Rules.fs) (7), [`Playable`](src/Table/Playable.fs) (22) and `Pulse` (4).
Everything else a game touches is vocabulary it may use and need not.

**The headline is what is missing from the table below.** Four of the eight games in this
repository — Diplomacy, Compile, Life and Warband — changed the seam in no way at all. Neither
`Rules` nor `Playable` has a commit against it from any of them.

EndTimes is the ninth and the first from *outside* the repository, built against the packages rather
than beside the sources. That it could ask for something is the point of packaging them; that it
asked for two fields is the answer worth keeping.

| | Game | What it added to the seam |
| --- | --- | --- |
| 2026-08-06 | **Turncoats** | `Rules` — all seven members. The extraction itself |
| 2026-08-07 | **Noughts and crosses** | `Playable` — all twenty. The whole reading half |
| 2026-08-12 | **Diplomacy** | *nothing* |
| 2026-08-14 | **Compile** | *nothing* |
| 2026-08-16 | **Life** | *nothing* |
| 2026-08-17 | **Snake** | `Pulse` — `Every`, `Beat`, `Pressed`; and `Playable.Pulse` |
| 2026-08-20 | **Cascade** | `Pulse.Frames`; `Playable.Rings` |
| 2026-08-23 | **Warband** | *nothing* |
| 2026-08-24 | **EndTimes** | `Playable.Aside`; and `Menu.Choice.Working` |
| 2026-08-24 | **EndTimes** | `Playable.Steering` |
| 2026-08-24 | **EndTimes** | `Margins.Showing`; `Command.Showing`; `Screens.askingOver` |
| 2026-08-27 | **EndTimes** | `Keys.path` for `Keys.started`; `Keys.draw` in columns |

## What each one was for

**Turncoats — `Rules`, at [`fce2f1a`](src/Engine/Rules.fs).** The engine was extracted from this
game, so nothing here was a demand: it is what one game looks like with the game taken out. That
is exactly why it could not be trusted, and why there are six others.

**Noughts and crosses — `Playable`, at `d26c683`.** A second game turned the extraction into a
seam. Everything about how a game is *read* arrived at once: `Read` and `Write` as the two ends of
a typed line, `Says` and `SeenBy` as the two audiences for a notice, `Seat`, `Slots`, `Views`,
`Page`, `Faults`, `Resign`, `Skills` and `Seating`. Written against one game these would have been
Turncoats with the names changed; written against two, they are a seam.

**Snake — `Pulse`, at `fb41c5b`.** The first game that does not wait for anybody, and the one that
looked like it needed something other than a fold. It did not. **A beat is a move**: the game says
what its beat is and how long a table should leave between them, and the tables keep the time.
Nothing about real time reaches the rules, so a record replays beat for beat with no clock
involved and every rule is checked by folding beats by hand. `Pressed` is the other half — a key
stands for a line the game already reads, so nothing can be pressed that could not have been
typed, and a board driven by a keypress is the same game and the same record as one driven by
hand. `Conforms.against` checks that for every game that has a clock.

**Cascade — `Pulse.Frames` and `Playable.Rings`, at `1f5c74a`.** The first board that moves
*between* two beats rather than only on them.

- `Frames` asks for redrawings that are not moves. All that differs between two frames is
  `Margins.Phase`, running from 0 at a beat towards 1 before the next; nothing a frame draws can
  reach the timeline, the record or the rules. A game with nothing moving between beats asks for
  none, which is every game of turns and Snake as well.
- `Rings` reads what the board is sounding off the state after a move rather than out of the
  notices — which is what makes a game taken up from a record sound like the game it was saved
  from, and lets a game say it once for every table rather than once per endpoint.

**EndTimes — `Playable.Aside`, 2026-08-24.** The first game with something to offer that is not a
board. Its players build a summoner — an archetype and six things it brings — before any game is
dealt, keep it between games, and pick it up at the start of one. Every screen the table had was a
screen *of a game in progress*, and there was nowhere for that to live.

The demand is narrow and the answer is deliberately narrower. An `Aside` is a word, two lines of
label, a screen and a line-reader; it holds **no state at all**. Everything the bench remembers,
the game remembers — which is what keeps this one field instead of a type parameter on `Playable`
that seven games with no bench would have had to carry. `Screen` is a function rather than a value
because the screen is redrawn after every line, and a bench that could not show what the last line
did to it would be a bench nobody could work at.

Two things are worth knowing about where it sits.
[`Menu.screen`](src/Table/Playing/Menu.fs) now numbers its rows by where they end up rather than by
hand, because a bench puts a row in the middle of that list and every number after it was otherwise
one out. And [`Play.working`](src/Play/Play.fs) does **not** consult `Menu.choose`: at a bench the
game's words come first, so a bench with a row called `3` opens that row instead of dealing a game
of three. The four words that navigate — `back`, `menu`, `quit`, `exit` — are answered there by
name, so nothing a game does can take them away from a player. `Conforms.against` holds a bench to
that: it may not be opened by a word the menu already answers to, and it may not swallow a line it
did not understand.

**EndTimes — `Playable.Steering`, 2026-08-24.** A second ask from the same game, and the one that
finally joined the two halves of the program that had been steered differently since Snake.

A game on a clock has been steerable since `Pulse.Pressed`: keys do things, Enter opens the prompt.
A game of turns had a board and a `ReadLine`, and the arrow keys that walk every *menu* in the
program did nothing at a board. `Steering` closes that: a game hands back a `Keys.Screen` for where
it stands and the table steers it with the machinery the menus already use, so there is one way of
walking a list in this program rather than two.

It makes the same bargain `Pressed` does, and `Conforms.against` holds it to it at every state the
suite walks through: **a row stands for a line the game already reads**. Nothing can be picked that
could not have been typed, a board driven by the arrow keys writes the same record as one driven by
hand, and `Escape` backs out by sending a line that reads too. Enter with nothing typed takes the
marked row; Enter with a line underway sends the line, so no game can take the prompt away from
anybody.

The board as the table drew it is handed in with the seat it was drawn for, so a game may put it
above its rows, replace it, or ignore it — the table has already chosen the view and the margins,
and the seam does not second-guess either. The one thing `Play.loop` now carries across a move is
where the mark had got to: every line rebuilds the screen from the state it left behind, and a mark
that went back to the top each time would make walking one row through its choices impossible.

**EndTimes — `Margins.Showing`, 2026-08-24.** Not the seam, but next to it: a game with eight
screens and a tab bar needed somewhere to keep *which one this console is looking at*.

It could not go in the game's state - it is not part of the game, would ride into the record, and
would be the same for everybody at a shared keyboard. It could not go in a mutable of the game's
own either, for the last of those reasons. Where it belongs is beside the three switches the table
has carried per console since there were margins: `Notes`, `Commands`, `Logged` - and now one word
that the table neither knows nor asks the meaning of. `Command.Showing` is how a game asks for it,
and it is never read from a typed line here: `Playable.Read` decides which words mean it, because
the table has no idea what screens a game has or what they are called.

`Screens.askingOver` came with it. A board that is drawn and then steered has to go up **as its
view drew it** - handing an already-painted board back to `Says` throws it away, because what a
rich board is made of is escapes rather than markup and the second pass eats them. Menus have
nothing drawn for them and go through `asking`, which is the same function passing "".

**EndTimes — `Keys.path`, 2026-08-27.** Not the seam either, and no member moved: this is a bug in
how the table remembered where somebody was standing, found by a game with lists two deep.

A place used to be a row on the *first* screen. Walk into a list a row opens, take something on it,
and the answer handed back was the outermost row - so the next frame put whoever took it back at the
top of the first list. For a list you take one thing off and leave, that reads as finishing. For a
list of things to tick it is unusable: EndTimes builds a summoner off a catalogue of nine, and every
tick threw the player out of the catalogue. A place is now the way down to it - which row of the
first screen, which row of the screen that opens, and so on - and `standing` walks that path down
whatever screens the game built this time. It has to be a path rather than the screens themselves,
because a game builds its screens afresh every time it is asked and the ones somebody walked down
are stale by the moment they are wanted again. Where the screens no longer go that deep, it stops
where they do.

`Keys.draw` learned columns in the same breath, and takes the width to draw into. A row with
something to say beside it is a line of prose and keeps its own line; a screen of nothing but names
is a list of names, and a list of names is read in columns, down one and then down the next. A
dozen short rows drawn one to a line is a dozen lines of a window that had a board to show.

`Conforms` grew with it: the contract now walks every row *anywhere* under a steered screen rather
than the first list only, and accepts any line the game reads rather than only a move - a row that
opens one of the game's own screens is still a row standing for something somebody could have typed.

## What the four quiet games did instead

They are the evidence, so what they found is worth as much as what they changed.

**Diplomacy** — seven seats, no chance at all, orders written in secret by everybody at once, a
year of three kinds of phase, and a board no picture can show all of. It needed no new seam
member. What it did find is that the engine's idea of *what one move can say* was looser than two
games suggested: one phase resolving produces twenty notices where the others produce one or two.
Nothing broke — `Model.LogDepth` truncates and the journal keeps the rest — but it is worth
knowing. See [what a third game found](README.md#what-a-third-game-found).

**Compile** — a deck rather than a board, and three games in a row: a draft, a laying-out, then
play, with different moves and three senses of whose turn it is. It went in a piece at a time over
many commits, which makes it the honest test of the other claim this repository makes: that a game
can be *added to* rather than only added, without anything above it moving. Nothing did.

**Life** — one seat, no opponent, nothing to win, no ending, and a position that changes because a
rule says so rather than because anybody chose it. It got the timeline, the record, the replay,
the menu, the command line, the wire and all three screens for nothing. What it turned up was
three places where code right for four games had never been handed a *one*: "1 players" in the
list of games, a clause about machines at a game with none, and a block that loses its name. See
[what it turned up](src/Games/Life/README.md#what-it-turned-up).

**Warband** — the first game that is **hidden and on a clock at once**, which was the only thing
about it worth doubting. Turncoats and Diplomacy keep things back but wait for everybody; Life,
Snake and Cascade beat on their own but hide nothing. Putting the two together asks a question
neither half had been asked: a table with a `Pulse` lets any console speak whenever it likes
(`Lobby` only enforces whose turn it is at a game *without* one), so a game that hides something
cannot lean on the turn order to keep it hidden. It does not have to — `SeenBy` and the per-seat
board were already the whole of the curtain, and the lobby draws every console its own seat's board
either way. Nothing moved.

Two things it found that are worth writing down and are not seam:

- **A game's state may not have a field called `Phase`.** `Margins` has one, and F# resolves a
  field on an un-annotated value by name alone, so half of `Render` silently retyped itself. It is
  the same trap `Margins.Logged` was named around, and the second time it has been sprung —
  `Model.Log`, and now this.
- **`Rules.Active` is read by more things than "whose turn is it".** One keyboard draws the board
  for whoever is active, so a game that names a different seat every beat turns the board over
  under the person reading it. Naming the side about to swing was tried and taken out again; the
  cost of the plain answer is that nothing can be given up mid-battle, which for this game turns
  out to be right anyway. [`Rules/Session.fs`](src/Games/Warband/Rules/Session.fs) says so where
  somebody would go looking.

## Things that changed near the seam without changing it

- **`Field` and `Speck`** (Cascade). `Walled` puts a wall round every cell, which is right for
  nine squares and unreadable at two hundred and fifty-six. A field is a glyph a cell, and each
  cell carries a **mood** — a bare word for what it is *doing* rather than what it is. A terminal
  ignores moods; a page turns them into classes. Reading vocabulary, not seam.
- **A move that changes nothing is not written down** (Cascade, in `Update.make`). A clock beating
  over a board at rest used to leave a line in the record every beat. Now a game nobody has
  touched has an empty record, and a board at rest is not sent down every wire in the house twice
  a second.
- **`Playable.opening`** (`ed699a6`). A helper, not a member: the menu and the command line both
  have to open a game the way it was left, and two answers to that would be two defaults.
- **The record's `format` line** (2026-08-23). Not asked for by a game — asked for by the engine
  being packaged and versioned apart from the games built on it, so a record written by one
  version will be read by another.

## Keeping this honest

A change to `Rules`, `Playable` or `Pulse` gets a row in the table above and a paragraph saying
which game demanded it and what breaks without it. If no game demanded it, that is worth writing
down too — and worth a second look, because a seam that grows without a game asking is a seam
growing towards one game's idea of what a game is.

The other half of holding the line is [`tests/Conforms.fsx`](tests/Conforms.fsx), which is the
contract all thirty-three members are checked against, for every game, on every run.
