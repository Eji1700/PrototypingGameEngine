# Compile

Two players sitting opposite each other, fifteen protocols between them, and three lines running
across the table. Each drafts three protocols, sets them against the lines face down, and plays a
deck made of nothing but those three: a card face up where its protocol is, for the number printed
on it, or face down on any line, for 2. A line at ten and ahead compiles when its owner's turn
comes round, and the third protocol compiled wins. Ninety cards, and every one of them says
something. The engine it runs on is [three directories up](../../../README.md).

```powershell
dotnet run -- compile play 2
dotnet run -- compile play 2 --rival hard     # the seat opposite, played by the program
dotnet run -- compile serve 2                 # the same, in a browser
dotnet run -- compile-control play 2          # with the optional rule in it
```

It takes 2 and only 2; any other count is turned away at the door. Putting a game down at any
stage and taking it up again is the engine's, and is [documented there](../../../README.md#records).

## The rules

**The draft.** Fifteen protocols - Apathy, Darkness, Death, Fire, Gravity, Hate, Life, Light, Love,
Metal, Plague, Psychic, Speed, Spirit and Water - and six picks, going 1-2-2-1: one to Player 1,
two to Player 2, two back to Player 1, one back to Player 2. A protocol taken is taken from both,
and the nine nobody took are out of the game.

**The protocols.** Each player sets their three against lines 1, 2 and 3, Player 1 first, and an
order is laid face down: the other player is told it was laid and nothing more, until both are in
and both are turned over at once. Line 2 is then your second protocol meeting theirs in the middle
of the table.

**The deal.** A deck is the eighteen cards of your three protocols, shuffled. A protocol is six
cards numbered out of 0 to 6 with one number missing: twelve go without the 6, and the three that
carry a 6 go without something lower - Gravity a 3, Love a 0, Metal a 4. Five are drawn to open,
and nothing is drawn at the end of a turn.

**A turn.** When your turn begins, the start-of-turn text of your uncovered cards runs, the control
component changes hands if it is in play and you have earned it, and every line you have won
compiles. Then one action: play a card, or refresh. Then the check cache phase, the end-of-turn
text of your uncovered cards, and the turn is handed on.

**Playing a card**, from hand onto one of your three stacks:

| | goes | is worth | says |
| --- | --- | --- | --- |
| face up | only on a line its protocol sits on - either player's | the number printed on it | all of it |
| face down | on any line | 2 | nothing |

No protocol is drafted twice, so a card has exactly one line it can go face up on, and it may be
the line their protocol made. A card of theirs can shut a line to you, or the face-down half of it,
or leave you nothing but face down anywhere; a card of yours can open every line. A refusal says
where the card could have gone instead.

**Refreshing.** Your whole hand into the discard, and five up. It is the turn's action rather than
something done as well as one, and with an empty hand it is the only move the rules take. A deck
that runs out is shuffled from its own discard; a player with nothing anywhere draws nothing.

**What a line is worth** is the sum of your stack, a face-down card counting 2, plus anything of
yours that adds to it and less anything of theirs that takes from it, and never below 0. Both
totals are on the board.

**Compiling.** At the start of your turn every line where you have 10 or more and strictly more
than they do compiles, whether you would have it or not: the protocol facing it is turned over,
and every card in the line - theirs as well as yours - goes to its owner's discard. A line that
will compile reads `ready` the turn before, which is the one turn they have to answer it; a tie is
no win, so 2 face down can be enough. Compile a protocol already compiled and the line is wiped
just the same, and the top card of their deck comes into your hand instead - their discard
shuffled back in first if the deck is empty, nothing if they have nothing anywhere. Compile all
three of yours and you win.

**What a card says.** A card played face up does what is printed on it: a list of commands,
carried out one at a time with the table looked at again between any two, written in three boxes.

| | holds | carries |
| --- | --- | --- |
| top | while the card is face up, covered or not | a standing rule; what the card listens for (*after you draw cards*, *after your opponent discards cards*, *after you clear cache*); what it does first when it would be flipped, or deleted by compiling |
| middle | the moment the card is shown - played face up, turned face up, or uncovered again | the one-off: delete, flip, draw, shift, discard, and the rest |
| bottom | while the card is face up and uncovered | a standing rule; the start and the end of your turn; what it does first when it would be covered |

Playing anything on a line, face up or face down, covers the middle and the bottom of the card
that was on top. A card lying face down says nothing at all, and one line can be silenced so that
no middle box in it speaks.

A command with one thing to point at does it. With nothing, it finds nothing to do and the next
command still runs - except behind *if you do*, which lets nothing through unless the command
before it did something. With several, the game stops and asks: which card, which line, whether
(*you may*), or which of two (*either ... or*). It may ask the player whose turn it is not, and
then nothing moves until they answer. *Every* takes all of them and asks nobody, and a command
handed across (*your opponent: discard a card*) is carried out by them.

**The check cache phase.** At the end of your turn a hand over five comes back down to it, one
discard at a time, asked by the rules rather than by any card. One card calls the phase off, and
one listens for it.

**The control component** is the optional rule, and `compile-control` is the game with it in. It
starts in the middle. At the start of your turn, leading two lanes - strictly; a tie is no lead,
and no ten is needed - takes it, out of the middle or off the other player, and nobody loses it
except to somebody earning it away. Holding it, every time you compile or refresh your three
protocols have to move first, into a different order: five of the six are offered. Only the
protocols move. The stacks stay where they are, so a line built for Water can end up compiling
Darkness, and compiling a protocol you already own is something that happens.

`resign` gives the game up at any of the three stages, and is written down.

## The words

| typed | or | which |
| --- | --- | --- |
| `fire` | `draft fire`, `take fire` | takes Fire at the draft |
| `water darkness fire` | `arrange water darkness fire`, `order ...` | sets yours against lines 1, 2 and 3 |
| `fire-3 2` | `play fire-3 2` | plays Fire-3 face up to line 2 |
| `fire-3 2 down` | `play fire-3 2 down`, `face-down` for `down` | plays it face down, for 2 |
| `refresh` | `r` | puts the hand down and takes five up - the whole turn |
| `fire-3` | `choose fire-3`, `pick fire-3` | answers a card offering cards |
| `2` | `choose line 2`, `pick line 2` | answers one offering lines |
| `yes`, `no` | `y`, `n` | answers *you may* |
| `first`, `second` | `1st`, `2nd` | answers *either ... or* |
| `water darkness fire` | `arrange ...` | answers a card, or the component, asking for an order |

A card is its protocol, a dash and its number, upper or lower case; a number the protocol lacks
is refused as not a card, and a protocol is its whole name. The record keeps the long forms -
`draft fire`, `arrange water darkness fire`, `play fire-3 2 down`, `choose fire-3`, `choose line 2`
- so that it reads as sentences rather than a column of bare words.

Three more ask rather than move, so they cost no turn and reach no record: `what fire-3` (or
`says fire-3`) reads a card's three boxes wherever the card is, `peek` reads your own cards lying
face down and `peek all` every face-down card you know the face of, and `pile` lists what the game
still has to do in the order it will do it. Any other line is answered with what could be typed
now - the protocols left at the draft, the six orders, the cards on offer, or your own hand and
the line each card could go face up on. `undo`, `resign`, `help`, `quit` and the rest are the
table's: [at the prompt](../../../README.md#at-the-prompt).

## The board

The heading says whose turn it is, or which card, the control component or the check cache phase
is waiting on whom; the turn count runs from the first pick of the draft. Under it, **The field**:

```
THE FIELD
            -           |         [2]          |         [2]
                        |       Metal-2        |       Light-3
                        | Your opponent cannot |
                        | play cards face down |
                        |    in this line.     |
  ----------------------+----------------------+----------------------
          Line 1        |        Line 2        |        Line 3
        Gravity  0      |       Metal  4       |       Light  5
      Water  0  done    |     Darkness  0      |       Fire  6
  ----------------------+----------------------+----------------------
            -           |          -           |        Fire-4
                        |                      |        Fire-2
                        |                      |         top
                        |                      |
                        |                      |        middle
                        |                      |  Discard a card. If
                        |                      |  you do, return any
                        |                      |    card to hand.
                        |                      |        bottom
                        |                      |
```

Their three stacks are above the lines and yours below, each growing away from the line, so the
card most recently played is the one nearest whoever played it. The middle names the two
protocols meeting on each line with what each side's stack is worth, `ready` beside a stack that
will compile when its owner's turn next begins and `done` beside a protocol already compiled. The
uncovered card of a stack is drawn with its three boxes, empty ones and all; a covered card is
drawn with its top box only, which is all of it that still holds; a card face down is `[2]`.

Below the field is whatever the stage asks for: **The draft**, the protocols still on the table;
**Your protocols**, the six orders yours could go in; **Your hand**, each card as its three boxes
with a `play face up` for every line it may go to, and a refresh tile; or **Waiting on an
answer**, what is being asked and the choices. **Players** counts each side's compiled protocols,
deck, discard and hand, and marks who holds the control component when it is in play. The notes,
the box of commands and the log are the engine's [margins](../../../README.md#how-the-board-is-drawn).

**What a hand hides.** Your hand is drawn to you and to nobody else; the other player sees how
many you hold. A card you play face down reads `[2] Darkness-1` on your board and `[2]` on theirs,
and the log names it to you alone. An order laid face down reads `hidden` in the field and
*sets their 3 protocols against the lines, face down* in the log and the history, until both are
in. A card taken off the top of your deck by a second compile is named to the taker only, a card
played face down off a deck is named to nobody, and a question that offers cards out of a hand
shows the other player *a card in their hand* and how many. `peek all` shows a card of theirs
only if it has been face up on the table and turned over since, and the knowing goes with the
card: a shift carries it, and a card back in hand and played down again is a secret again.

There is no clock, no key and no sound. `plain`, `rich` and `html` draw the same scenes; in a
browser every tile is a button for the line it stands for - a protocol, an order, `play face up`,
`refresh`, `choose`, a line, `yes` and `no`, `first` and `second` - and a face-down play is typed.
Two [colour slots](../../../README.md#colours), one per player, crimson and azure unless settled
otherwise.

## The machine

| | |
| --- | --- |
| `easy` | drafts, arranges and plays at random - a seat filled, not an opponent |
| `medium` | counts: drafts the richest protocols, plays for the line nearest compiling, and will not spend a five as a two |
| `hard` | counts, and reads the cards - it plays for what a card says as well as for what it is worth |
| `deep` | plays every move out on a copy of the game and keeps the one that leaves the best board |

Each keeps what the one below it does. Every one of them arranges its protocols at random and
answers a question of lines or orders at random; ties are broken by the generator, so the same
skill does not draft the same three every game.

**`medium` counts.** At the draft, the protocol whose six cards add to most - Love, then Gravity,
then Metal, then the twelve at fifteen. At a play, every card in hand against every place it may
go: twice what it adds to the line, 20 more if that takes the line to ten and ahead, 4 more if the
line's own protocol is still to compile, and less the card's printed number if it goes face down.
Nothing it may play, and it refreshes. Offered cards, it takes the one worth most to it - theirs
to delete, return or shift, yours to play, and the smallest of yours to discard, give or reveal; a
flip by what it does to the stack. It says yes to *you may* and first to *either ... or*.

**`hard` reads.** To that it adds a price on the card's text, walked off the same commands the
board prints: a draw 3 a card, a discard -3, deleting theirs 5 and yours -4, returning 4 and -2, a
flip 2 and 1, a shift 1, stopping their compile 8, playing from hand 4, taking off their deck or
at random 3, refreshing or laying off the deck 2, giving -2, revealing a card -1 and their whole
hand 2, showing 1, a swap 1, rearranging 1 and theirs 2. A command handed to the opponent is its
own negative, *you may* is never below nought, *n times* multiplies, *every* and
*in each line* double, *if you do* adds half of what follows, *either* takes the better half. A
standing rule is priced too: a shut line or nothing but face down 6, playing anywhere 5, no face
down here 4, skipping the cache 3, silence 2, and a total moved by 2 a point. The top, middle and
bottom boxes count in full; what a card listens for and its interrupts count half; and the
uncovered text of the card it would cover is taken off. A face-down play reads nothing. It says
no to a *you may* priced at nought or less and second to an *either* whose second half is worth
more. It still drafts on totals.

**`deep` looks.** It weighs no card at all: for every legal move it plays that move out on a copy
of the game, answering its own questions as it would answer them for real, through the end of its
turn and the beginning of the opponent's - the component taken, every line they have won
compiled - and stops at the first thing the other seat has to answer. The board left is scored:
100 a protocol compiled, for and against; each line's total up to ten, twice; 30 a line won,
either way; 3 a card in hand over theirs. Four times that, plus `medium`'s count of the play.

How a machine is seated, seeded and held to what a player sees is the engine's:
[against the machine](../../../README.md#against-the-machine). [compile.fsx](../../../tests/compile.fsx)
plays them against each other and holds each to beating the one below it.

## Settings

Two ways of playing, each a game in its own right: `compile`, and `compile-control` with the
component in. `dotnet run -- compile-control play 2` deals the second outright; the game page
under `settings` lists both, `plays compile-control` settles which a new game is dealt as, and
`save` keeps it as

```
[compile]
plays compile-control
```

in `settings.txt`. A record says on its deal line which of the two it is, and taking one up plays
it that way whatever is settled here. The rest of the page is the engine's:
[settings](../../../README.md#settings).

## The files

`Rules/` knows nothing of screens or English; `Reading/` is how it is read; `Offer.fs` joins them
as the [seam](../../../README.md#the-seam). How card text is represented and resolved - the
command language, the pile, what writing a card involves - is [DESIGN.md](DESIGN.md).

| | |
| --- | --- |
| [Rules/Protocols.fs](Rules/Protocols.fs) | the fifteen, their names, and the six orders three can be laid in |
| [Rules/Cards.fs](Rules/Cards.fs) | a card, the number each protocol goes without, a card as placed - which way up, and whether the other side has seen it - and a deck of three protocols |
| [Rules/Effects.fs](Rules/Effects.fs) | the command language card text is written in, the standing rules, the questions a card can ask, and what the pile is a list of |
| [Rules/Printed.fs](Rules/Printed.fs) | what is printed on each of the ninety, and nowhere else |
| [Rules/Field.fs](Rules/Field.fs) | the lines; one side's deck, discard, hand, stacks and compiled protocols; what a line is worth, where a card may go, and which lanes are led |
| [Rules/Drafting.fs](Rules/Drafting.fs) | the 1-2-2-1 order of picks |
| [Rules/Session.fs](Rules/Session.fs) | the stages, the pile, the control component, and whose turn it is - which a question answers first |
| [Rules/Events.fs](Rules/Events.fs) | everything that can happen, and every refusal |
| [Rules/Resolving.fs](Rules/Resolving.fs) | carrying out what the cards say: the pile, the questions, compiling, and the beginning and end of a turn |
| [Rules/Turn.fs](Rules/Turn.fs) | `Move`, and which moves the rules take when |
| [Rules/Words.fs](Rules/Words.fs) | every string a player reads: card text written out of what it does, the notices, the refusals, the record's words, and what each seat is told |
| [Rules/Rival.fs](Rules/Rival.fs) | the four skills |
| [Reading/Ink.fs](Reading/Ink.fs) | two colour slots, one per player |
| [Reading/Parse.fs](Reading/Parse.fs) | a typed line as a move, or as a question for the board |
| [Reading/Render.fs](Reading/Render.fs) | every screen as a [`Scene`](../../../README.md#screens): the board, the history, the notes, the help, the answers to `what`, `peek` and `pile`, and the page |
| [Offer.fs](Offer.fs) | both games out of one function, and `Faults`, which checks the numbers against each other and every one of the ninety texts before a table opens |
| [Program.fs](Program.fs) | the door |

## Checks

[compile.fsx](../../../tests/compile.fsx) loads [Compiled.fsx](../../../tests/Compiled.fsx), the
harness, and holds the game to:

- the numbers - fifteen protocols, six cards each with one number missing, ninety in all, eighteen
  to a deck, and nothing for `Faults` to say;
- the draft, the orders and the deal - 1-2-2-1, every refusal, an order kept from the other
  player's words, board and record until both are in, the same seed dealing the same hands;
- playing, refreshing and compiling - face up on the one line and face down anywhere for 2, an
  empty hand refused everything but `refresh`, ten and ahead compiling as the turn comes round,
  both sides swept, the second compile taking a card across the table;
- every shape of command a card can carry, each with a card that prints it: questions on either
  seat, fizzles, gates, interrupts, listeners, silence, the check cache phase, and what a line is
  worth under every modifier;
- the control component in both games - lanes, the five orders, the stacks staying put;
- thirty-six cards each in exactly one place, resigning at any stage, every move written and
  read back, an unknown word asked about, undo and replay;
- the machines - whole games from the draft to a win, both seats winning some, `medium` beating
  `easy`, `hard` beating `medium`, `deep` beating `hard`, and none ever left without a move;
- what each seat sees - hands, face-down cards, `peek`, the pile, a question over a hand - the
  same in all three views, and a page with no control the game would refuse;

and, first of all, `Conforms.against` for both ways, the
[contract](../../../README.md#tests) every game answers to.
[counting.fsx](../../../tests/counting.fsx) sweeps its counts. CI also carries it over a real
socket with [wire.ps1](../../../tools/wire.ps1), into a real browser with
[smoke.ps1](../../../tools/smoke.ps1), and takes the Compile records in [logs/](../../../logs)
back up with [records.ps1](../../../tools/records.ps1).
