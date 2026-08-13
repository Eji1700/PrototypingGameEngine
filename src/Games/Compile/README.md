# Compile

Two players sitting opposite each other. Fifteen protocols on the table, six of them drafted
1-2-2-1, three lines running across between the players, and a deck built out of whatever each
of them took. The fourth of the games here, and the engine it runs on is
[three directories up](../../../README.md).

```powershell
dotnet run -- compile play 2
dotnet run -- compile play 2 --rival easy   # the seat opposite, played by the program
dotnet run -- compile serve 2               # the same table in a browser, with buttons

dotnet run -- compile-control play 2        # the same game, with the optional rule in it
```

**This game is unfinished, on purpose.** What is here is the table: the draft, the protocols
against the lines, the decks, the hands, and a card going onto a stack. What a card *does*, what
a player is trying to do with one, and how a game is won have not been written yet - so there
is no win condition in the code and there is no invented one either. See [What is not
here](#what-is-not-here), which is the honest list.

**The design for the rest of it is [DESIGN.md](DESIGN.md)** - the values and the ten, face-down
play, compiling, refresh, the control component, how a card's rules text resolves, and the seven
steps it should be built in. The first three of those steps give a game that can be won without a
single card effect written, which is the point of that order.

## Playing

Every command that is not about protocols or cards - `undo`, `redo`, `history`, `save`,
`notes`, `view`, `resign`, `restart`, `help`, `quit` - belongs to the engine and is
[documented there](../../../README.md). What this game adds is three verbs, one per stage.

| command | action |
| --- | --- |
| `fire` | take the Fire protocol at the draft |
| `draft fire` (`take`) | the same, the long way round |
| `water darkness fire` | set your three against lines 1, 2 and 3, in that order |
| `arrange water darkness fire` (`order`) | the same, the long way round |
| `fire-3 2` | play Fire-3 **face up** to line 2 |
| `fire-3 2 down` | play it **face down** instead, for 2, on any line |
| `play fire-3 2` (`play fire-3 2 down`) | the same, the long way round |
| `refresh` (`r`) | put your whole hand down and take five up - **instead of** playing, not as well as |
| `fire-3` | answer a card that is waiting on you to pick one (or `choose fire-3`) |

The short forms exist because the stage already says which of the three a bare line could be,
and which one it is, is settled by how many words are in it: one word is a protocol, three are
an order, and a card and a number are a card going onto a line. The long forms are kept
because a record is written in them, and a record that read as a column of bare words would be
a record nobody could skim.

A card is written as a protocol, a dash and the number on it - `fire-3`. A protocol's six
cards carry six different numbers, so no two cards in a deck are the same card, and naming one
is naming a card rather than a place in a hand. That matters: `fire-3` is still `fire-3` after
the hand beside it has changed.

## How a game goes

**The draft.** Fifteen protocols, no duplicates, and six picks: one to Player 1, two to Player
2, two back to Player 1, one back to Player 2. Three each. The shape is what pays for going
first - the player who chose first chooses again only after the other has had two - and the six
nobody took are gone for the rest of the game.

**The protocols.** Each player sets their three against the three lines, first for line one -
**face down, and both are turned over together.** The lines they make are read across the table
from each other: Player 1 might have Water, Darkness and Fire while Player 2 has Gravity, Metal and
Light, and line two is then Darkness meeting Metal in the middle of the table.

Face down because a card may be played face up against *either* protocol on a line, which makes
an order seen before you have chosen your own worth a great deal - and the 1-2-2-1 draft above
exists precisely to keep going first from being an advantage. The seats still come round one at
a time, because there is one keyboard; neither player chooses knowing the other's, so which of
them was asked first does not matter.

**The deal.** Each deck is the eighteen cards of the three protocols that player drafted,
shuffled, and five are drawn.

**Play.** A turn is two beats: **compile** anything you have already won, then take one action -
play a card, or refresh. Then the turn passes.

The action is either **one card out of the hand** onto one of your three stacks, face up or face
down, **or a refresh**: your whole hand down and five fresh ones up. Refreshing costs the whole
turn, and with an empty hand it is the only thing left to do.

**Nothing is drawn at the end of a turn**, and that is the game's second clock. Five cards is
five turns of tempo, and the turn you spend getting five more is a turn your opponent spends
getting closer to ten. A deck that runs out is shuffled from its own discard.

| | where it may go | worth | text |
| --- | --- | --- | --- |
| **face up** | only a line where the card's protocol is - **either player's** | the number printed on it | all of it, when there is any |
| **face down** | any line | **2** | none |

This is the decision the game is built on. A card you cannot use is still worth 2 anywhere, so a
hand is never dead - but spending a 5 as a 2 to win a race is a real cost. And because face up
reads *either* protocol on a line, the opponent's Fire on line 2 is a standing invitation to
play your Fire cards there.

No protocol is drafted twice, so a card has **exactly one** line it can go to face up, and three
it can go to face down. The hand on screen draws it that way round - one `up` button and three
`down` ones - which is most of what makes the choice legible without reading a rule.

```
                          Player 2
       Spirit-3   |     [2]      |      -
                  |   Plague-4   |
     -------------+--------------+-------------
        Line 1    |    Line 2    |    Line 3
       Spirit  3  |   Metal  6   |   Plague  0
     Water 11 ready|   Fire  2   |    Light  0
     -------------+--------------+-------------
       Water-5    |    Fire-2    |      -
      [2] Darkness-1  |              |
                          you
```

**The numbers beside each protocol are what that stack is worth**, which is the one piece of
arithmetic the board does so a player does not have to. `[2]` marks a card played face down: it
is worth 2 either way, but you see what your own is and your opponent sees only the 2. `ready`
means that line will compile the moment its owner's turn begins, and `done` means the protocol
facing it has been compiled already.

**What a card says.** A card played face up does what is printed on it. Its text is a **list of
commands**, not one sentence, and they resolve one at a time - with the table looked at again
between every two of them, because a command that turned a card face up has put *that* card's
text in front of whatever was still waiting.

```
A CARD IS ASKING
  Fire-3 asks you to pick a card to turn over.
            Water-3          |          Metal-0
   yours, line 1, face down  | theirs, line 2, face down
            choose           |          choose
```

A command with nothing to point at finds nothing to do, and the command after it still happens -
so *"delete a card, draw a card"* draws even when there was nothing to delete. A command with
exactly one thing to point at just does it. With several, the game stops and asks - and it may
ask the player whose turn it is **not**, in which case nothing at all moves until they answer.

A card has **three** commands, by where they sit on it:

| | when | what it is |
| --- | --- | --- |
| **top** | the moment it becomes face up | a one-off: delete, flip, draw, shift |
| **middle** | continuously, while it is face up and uncovered | a rule change: *this counts as 0*, *this cannot be deleted* |
| **bottom** | at the end of your turn, while it is face up and uncovered | a one-off that keeps happening |

**A card's text is written out of what the card does**, rather than typed beside it - so a card
cannot say one thing and do another. `what fire-3` reads it at length, and a `*` on the board
means there is something to read.

```
> what fire-3
  Flip any face-down card. Draw a card.

> what water-3
  Water-3 has nothing printed on it. It is worth 3 face up and 2 face down.
```

**Seven cards carry text so far, and they are placeholders.** [The rest are
blank](#what-is-not-here), on purpose: the machinery that resolves them went in first, and the
seven were chosen to exercise every shape it has rather than because they are right.

**Compiling.** At the start of your turn, every line where your stack is **10 or more and
strictly more than theirs** is compiled - and it is not optional. The protocol facing that line
is turned over, and **the whole line goes, both players' cards alike**, to their owners'
discards. **Compile all three of your protocols and you win.**

It is checked at the *start* of a turn rather than the instant a stack reaches ten, and that gap
is the tension in the game: a stack at ten is a stack the other player gets one whole turn to
answer. A tie is not a win, so answering it can be as cheap as 2 face down - if that is enough
to make the totals level.

## The control component

An **optional rule**, and it is a second game rather than a switch: `compile` is played without
it and `compile-control` with it. Everything else about the two is the same, which is why they
are [one function and two values](#the-files).

**Taking it.** At the start of your turn, if you **lead two lanes** - strictly; a tie is no lead,
and no ten is needed - the component is yours, out of the middle or off the other player. Nobody
loses it except by somebody else earning it away.

**Paying for it.** If you hold the component and you **compile or refresh**, your three protocols
have to move first, and into a *different* order. Not may - must, and five of the six
arrangements are on offer because standing pat is not one of them.

```
Waiting on an answer
  The control component needs you to put the 3 protocols in a different order.
   Water / Fire / Darkness | Darkness / Water / Fire | Darkness / Fire / Water
  ---------------------+---------------------+---------------------
   Fire / Water / Darkness | Fire / Darkness / Water
```

**Only the protocols move. The stacks stay exactly where they are.** So a line qualifies to
compile on its *values*, and which protocol it compiles is settled afterwards, by an order you
were forced to change - and a stack built patiently for Water can end up compiling Darkness. That is
the whole of the rule, and it is why compiling is not one step: find the lines, ask for the
rearrangement, *then* read the protocol off the line, then wipe it.

It also makes the next rule reachable rather than theoretical.

**Compiling one that is already compiled.** Because compiling is mandatory, you will win a line
whose protocol is already turned over. When that happens you take **the top card of your
opponent's deck** into your hand - shuffling their discard back in first if their deck has run
out - and the line is wiped just the same. No nearer to winning, but it is not a wasted turn: it
deletes whatever they had built in a lane you already own, and it takes a card out of their deck
and puts it in your hand. That card can be played face down like any other, or face up on the
line its protocol sits on, which is on *their* side of the board.

A stack grows away from the line it was played to, so the card most recently played is the one
nearest whoever played it - furthest up for them, furthest down for you. The two halves of the
board are therefore read in opposite directions, which is not a flourish: it is what the table
actually looks like from where the reader is sitting.

## Rules as implemented

- Two players, and exactly two, sitting opposite each other.
- Fifteen protocols, no duplicates, drafted 1-2-2-1 - three each.
- Three lines, one per protocol drafted. Each player chooses which of theirs faces which line,
  face down, and both orders are turned over at once.
- A deck is 18 cards: three protocols of six, valued 0 to 5. Five are drawn to open.
- A turn is one action: play a card, or refresh. Then the turn passes.
- A card is played from hand onto one of that player's three stacks, face up or face down.
- Refreshing puts the whole hand in the discard and draws five. It costs the turn, and with an
  empty hand every other move is refused. A deck that runs out is shuffled from its own discard;
  a player with nothing anywhere draws nothing.
- Face up, it is worth the number printed on it and may only go on a line where its protocol
  sits - either player's. Face down, it is worth 2 and goes anywhere.
- A stack is worth the sum of the cards in it. Both totals are on the board.
- At the start of your turn, every line where you have 10 or more **and** strictly more than
  they do is compiled. It is mandatory.
- Compiling turns the protocol facing that line over and deletes every card in the line, both
  players', to their owners' discards.
- Compiling a protocol already compiled takes the top card of the other player's deck into your
  hand instead - their discard is shuffled back in first if their deck is empty - and wipes the
  line just the same.
- Compile all three of your protocols and you win.
- A protocol already taken, a card not in hand, a line that is not there, a card face up where
  its protocol is not, and a move made at the wrong stage are all refused - and every one of
  those refusals says what could have been done instead, which is the thing that helps.
- `resign` gives the game up at any of the three stages, and writes it down.

**What is hidden, and what is not.** Two things, and they are hidden in two different ways.

A **hand** is hidden by the *board*: what a player holds is never in anything the game says, so
it is drawn for the seat reading and that is the whole of it. What one player may know about the
other's cards is three counts - deck, discard and hand - and those are on the screen precisely so
that what the cards *are* need not be.

A **card played face down** is hidden by both, and it is the one thing here that is hidden from
one player and not the other. The player who played it knows what it is - it was in their hand a
moment ago, and pretending otherwise would be the game keeping a secret from the person who owns
it - so their own board reads `[2] Darkness-1` where the opponent's reads `[2]`. The notice splits
the same way: *"Player 1 plays Darkness-1 face down to line 2"* to the one who played it, and *"a
card face down"* to the other. What it is **worth** is not a secret and could not be, because
both players are counting the same stack.

An **order laid face down** is hidden by the *game*, and it is the one thing here that `SeenBy`
is really for: the notice says which protocols went where, and the other player is told only
that they went. It is kept back in all three places a screen could leak it - the log, the board
and the record - and turned over for both at once the moment the second order lands. That is
what makes a table where the seats come round one at a time play the same game as two people
laying them out together.

Everything else is on the table. The draft is announced, because a protocol taken is taken from
both of them, and every stack is face up in front of everybody.

## What is not here

Written down rather than left to be discovered:

- **What a card does.** A card is a protocol and a number. [Cards.fs](Rules/Cards.fs) is where
  an effect goes, and it is one field on one record - the six numbers are the placeholder,
  and they are deliberately the whole of it.
- **Sixty-five of the ninety cards** - and the seven that are written are placeholders that
  should be assumed wrong. [Printed.fs](Rules/Printed.fs) is the lookup they hang off and says
  which seven and why: between them they exercise a command that asks its own player, one that
  stops the game on the other, one that asks twice, a standing rule, an end-of-turn command, and
  a card given back to whoever drafted it.

  **This is the one thing left that cannot be worked out** - a card's rules text is data, and
  [DESIGN.md](DESIGN.md#what-step-6b-needs-from-you) says what would make writing it quick.
- **A machine worth playing.** There is one, it is legal, and it is random - but it does now
  play games out to a win, which is what makes writing a better one possible: there is finally
  something to be better *at*, and a baseline to be better than.

**A game is playable and winnable as it stands.** Two machines at `easy` finish every deal they
are given, both seats win their share, and the record replays to the same position - which is
the whole of the game underneath the cards, and the thing all ninety of them will be
written against.

**The invariant the tests hold to had to be restated, and this is where.** It used to be
*eighteen cards each, wherever they are* - deck plus hand plus discard plus everything on the
table. That was the strongest thing true, and the second compile made it false: a card can cross
the table now, so a player can be holding nineteen while the other is down to seventeen. What
survives is **thirty-six in total, each in exactly one place**, and the per-player count is a
thing that drifts on purpose. Both are checked - the weaker one over a game where nothing
crossed - so that the day something makes cards appear from nowhere, one of them says so.

## The files

Fourteen, in the shape every game here has: `Rules` is how it is played and contains no English
and nothing from the table layer; `Reading` is how it is read.

| File | Role |
| --- | --- |
| [Protocols.fs](Rules/Protocols.fs) | The fifteen, and the words for them |
| [Cards.fs](Rules/Cards.fs) | A card, which way up it lies and what that makes it worth, and a deck shuffled out of three protocols |
| [Effects.fs](Rules/Effects.fs) | What rules text is made of - as data rather than functions - and what the pile is a list of |
| [Printed.fs](Rules/Printed.fs) | What is on each of the ninety. Two of them, so far |
| [Field.fs](Rules/Field.fs) | The lines, what a stack is worth, one player's half of the table, and the question no one half can answer: where a card may go face up |
| [Drafting.fs](Rules/Drafting.fs) | Whose pick it is, which is a list of six and nothing else |
| [Session.fs](Rules/Session.fs) | The stages, the pile, and whose turn it is - which the pile answers first |
| [Events.fs](Rules/Events.fs) | Everything the game has to say, above both things that say it |
| [Resolving.fs](Rules/Resolving.fs) | The pile: one command at a time, with a look at the table between every two |
| [Turn.fs](Rules/Turn.fs) | `Move`, and which moves the rules will take, when |
| [Words.fs](Rules/Words.fs) | Every string a player reads |
| [Rival.fs](Rules/Rival.fs) | A seat played by the program, through all three stages |
| [Ink.fs](Reading/Ink.fs) | Two colours: one per side of the table |
| [Parse.fs](Reading/Parse.fs) | Three verbs, each with a short form |
| [Render.fs](Reading/Render.fs) | Every screen described once as a [`Scene`](../../../README.md#a-screen-described-once), which `Readers` then draws three ways |
| [Offer.fs](Offer.fs) | Both seams filled in - **twice**, out of one function: the game, and the game with the control component |

**What this game leant on that the others did not** is the stage. A game of this is three games
in a row - a draft, a laying-out, then play - with different moves and three different senses
of whose turn it is: the draft has an order of its own, the arranging goes round once, and play
alternates. Then the pile added a fourth, which outranks all three: a card that has stopped to
ask somebody something, and the somebody is very often not the player whose turn it is. All of
that is `Session.active` answering one question four ways, and none of it needed a line changing
above the game. The engine asks *whose turn it is*; it has never asked why.

**An optional rule is a second `Playable`, not a parameter.** `Rules.Deal` takes a player count
and a seed, and it should keep taking exactly that - a game's options are not the engine's
business, and a third argument would touch every table in the program. So `Offer.offering` is one
function returning one of two values, and [`Rules.fs`](../../Engine/Rules.fs) says why that works:
*a game is a value here, and two of them can sit side by side in one process.* Two lines in
[Games.fs](../../Games.fs), and the menu, `--help`, the seat list, the record and the wire all
get the second game for nothing.

The other thing worth noting is that `Doing` exists. A refusal for the wrong stage has to name
the stage the game is actually in - a player who typed a card at the draft wants to be told
what is wanted now - but a refusal carrying the whole `Stage` would carry the pool and the
ending with it, which is a notice quoting the position back at itself. So the stage is said
twice: once in full for the rules, and once small enough to travel in something a player reads.
