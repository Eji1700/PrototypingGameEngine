# The seam, and what moved it

Eight games sit on this engine, and about four fifths of the program is not about any of them.
That claim is only worth something if there is a record of what it cost to keep — so this file is
the ledger: every change to the seam, which game demanded it, and what would break without it.

The seam is three types and thirty-one members between them:
[`Rules`](src/Engine/Rules.fs) (7), [`Playable`](src/Table/Playable.fs) (20) and `Pulse` (4).
Everything else a game touches is vocabulary it may use and need not.

**The headline is what is missing from the table below.** Four of the eight games — Diplomacy,
Compile, Life and Warband — changed the seam in no way at all. Neither `Rules` nor `Playable` has
a commit against it from any of them.

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
contract all thirty-one members are checked against, for every game, on every run.
