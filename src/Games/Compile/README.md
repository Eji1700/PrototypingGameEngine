# Compile

Two players sitting opposite each other. Fifteen protocols on the table, six of them drafted
1-2-2-1, three lines running across between the players, and a deck built out of whatever each
of them took. The fourth of the games here, and the engine it runs on is
[three directories up](../../../README.md).

```powershell
dotnet run -- compile play 2
dotnet run -- compile play 2 --rival deep   # the seat opposite, played by the program
dotnet run -- compile play 2 --rival easy   # ...or by one that only plays legally
dotnet run -- compile serve 2               # the same table in a browser, with buttons

dotnet run -- compile-control play 2        # the same game, with the optional rule in it

dotnet run -- compile replay logs/...-compile-2p-seed<n>.log   # one you put down
```

**Put it down and come back.** `quit` writes the record and leaves the table exactly as it
stands — mid-draft, mid-laying-out or mid-play, the stage being part of the game like
anything else — and `Continue a game` at the menu lists what there is to take up, with the
same decks in the same order and the machine back opposite at the strength it was playing.
Conceding is `resign`, which is a different thing said on purpose. The two games are two
games here as everywhere else: a `compile` record and a `compile-control` record are filed
under their own names and neither will open the other. All of that is the engine's rather
than this game's, and is [documented
there](../../../README.md#taking-it-back-and-writing-it-down).

**The game is whole.** The draft, the protocols against the lines, the decks and the hands; the
values and the ten, compiling, refreshing, the check cache phase, and winning; the optional control
component; and **all ninety cards, every one saying the whole of what the real card says**. What is
left is [one thing](#what-is-not-here), and it is not a rule.

**[DESIGN.md](DESIGN.md) is how it got here** - the resolution pile, the fourteen shapes the ninety
cards asked the command language for, and the order it was built in. It is worth reading for the
rulings rather than for the plan: every one of them is now a line of code, and the document says
which line and why.

## Playing

Every command that is not about protocols or cards - `undo`, `redo`, `history`, `save`,
`notes`, `commands`, `log`, `view`, `resign`, `restart`, `help`, `quit` - belongs to the engine and is
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
| `what fire-3` | read a card at length, wherever it is |
| `peek` (`peek all`) | read the face-down cards you are entitled to know |
| `pile` | what the game still has to do, in the order it will do it |

The last three ask rather than move, so they cost no turn and go nowhere near the record.

The short forms exist because the stage already says which of the three a bare line could be,
and which one it is, is settled by how many words are in it: one word is a protocol, three are
an order, and a card and a number are a card going onto a line. The long forms are kept
because a record is written in them, and a record that read as a column of bare words would be
a record nobody could skim.

A card is written as a protocol, a dash and the number on it - `fire-3`. A protocol's six
cards carry six different numbers, so no two cards in a deck are the same card, and naming one
is naming a card rather than a place in a hand. That matters: `fire-3` is still `fire-3` after
the hand beside it has changed.

**A card you can still play on is drawn as its three boxes**, empty ones and all - because which
box a rule is in is the rule that matters most here. Playing anything on a line, face up or face
down, covers the middle and the bottom of whatever was there, so a covered card is drawn without
its boxes: what is left under its name is its top box, and the whole of what it still says. A card
lying face down says nothing at all, whatever is printed on it.

That grouping is [the same one the rules make](Rules/Words.fs), off the same `Text` that makes
them, rather than the board's own opinion about what is worth showing - so **the top box is
exactly what a cover cannot silence**, which is a sentence the tests hold the game to. `what
fire-3` draws the same three boxes, and is still the place to read a card that is nowhere near
the table.

**A lone word the parser cannot place is answered rather than refused.** Which of the three sets of
verbs above was wanted is a fact about where the game stands, and the parser is handed a line and
nothing else - so a stray word used to come back *"that is not a protocol"*, which is true on the
first six moves and beside the point for the rest of the game. It asks the game instead, and the
answer is what can be typed **now**: the protocols still on the table at the draft, the orders
your three could go in, the cards a waiting card is offering, or your own hand with the line each
of them could go face up on. Your own hand and nobody else's - it is the same rule the board
keeps.

**`peek` is memory, not a new right.** Two face-down cards are not secrets: your own, because you
put them there, and one of theirs that has been face up on that line and has been turned over
since, because you read it when it was. `peek` shows the first, `peek all` shows both, and neither
tells you anything you were not told once already. A card of theirs that has only ever been face
down stays face down. The knowing rides on [the card where it lies](Rules/Cards.fs) rather than in
a list somewhere, which is exactly how long it should last: **a card returned to hand, discarded,
deleted or swept away by a compile leaves the knowing behind with its place on the table**, and
one *shifted* to another line takes it along, because everybody watched it move.

**`pile` reads what the game has still to do.** It is the one part of this game that is entirely
real and entirely invisible: a card that says *"delete a card. If you do, draw a card"* stops on
the deletion, and the question you are staring at means something different depending on whether
the draw is still behind it. The board is the position; the pile is what is about to happen to it.

Both are read per seat, and so is the block that offers you a question - because **a question can
*be* a hand**. *"Your opponent discards 1 card"* stops on the other player and offers them
everything they are holding, so the list of what is on offer is the list of their cards. That
block is drawn to both players, and it used to print the whole of the other hand on the board of
whoever played the card. What a reader is owed is that there is a question and how big it is,
which is what somebody at a table would see: a hand held up, and no faces.

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
  Fire-0 asks you to pick a card to turn over.
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
| **top** | while it is face up, **covered or not** | a standing rule, or a trigger that listens |
| **middle** | the moment it becomes *shown* - played face up, flipped face up, or uncovered again | a one-off: delete, flip, draw, shift |
| **bottom** | while it is face up and **uncovered** | a standing rule, and the start and end of your turn |

Which box a line sits in is the whole of what covering decides: the same sentence in the top box
survives being built on and in the bottom box does not.

**A card's text is written out of what the card does**, rather than typed beside it - so a card
cannot say one thing and do another, and ninety of them cannot drift one at a time. `what fire-0`
reads it at length.

```
> what fire-0
FIRE-0
  Flip any other card. Draw 2 cards.
  When this card would be covered, first: Draw a card. Flip any other card.

> what speed-2
SPEED-2
  When this card would be deleted by compiling, first: Shift this card to another line.
```

**All ninety carry text**, which is why the board marks none of them: a star beside the cards with
something to read would be a star beside all of them.

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
- A deck is 18 cards: three protocols of six. Card values run 0 to 6, and every protocol goes
  without exactly one of the seven. Five are drawn to open.
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

## The seat opposite

Four machines, and each differs from the last by something a player can see. Three is what the
other games here carry; the fourth is only here because it can show what it is worth.

| | what it does | against the one below |
| --- | --- | --- |
| `easy` | drafts, arranges and plays at random - a seat filled, not an opponent | |
| `medium` | counts | 99 games in 100 |
| `hard` | counts, and reads the cards | about 3 in 5 |
| `deep` | plays the move out and looks at the board it leaves | about 7 in 10 |

**What `medium` counts** is the arithmetic the board already shows you, and nothing else:

- **at the draft**, the protocol worth the most - the six cards it brings add up to something, and
  that is the one thing about a protocol you can judge without reading ninety cards. Which is why
  Love, Gravity and Metal go first: [they carry a 6](#the-files);
- **at a play**, every card in hand against every legal place for it, at once - a machine that
  picked a card first and a place for it second could only ever find the best place for an
  arbitrary card;
- **and what it is spending**: a five face down is three thrown away, so the 0s go down and the 5s
  go up. A line it can take to ten and ahead outweighs everything else, because that line compiles
  the moment its turn comes round.

**What `hard` adds is reading**, and it reads *structurally* rather than out of a table of ninety
cards. Card text is data, so the machine walks the same `Command` tree that the screen prints and
`Faults` checks: a draw is worth about three, a card of theirs off the table about five, a turn
where they cannot compile about eight, and a command handed to the opponent is the same number with
its sign turned over. **A card written tomorrow is weighed tomorrow**, without anybody editing the
machine.

Two things it learned by measurement rather than by argument, and both are in the code:

- **it does not draft on text.** Adding the weights at the draft made it markedly *worse* - value
  totals are already a sharp signal, and the weights are too coarse to improve on them without
  drowning them. They earn their keep at a play, where the choice is between cards already in hand.
- **weighing the yes-or-no questions changed nothing measurable**, because saying yes was already
  right nearly every time. It is kept because it is the *reason* rather than the guess.

**What `deep` does is stop guessing.** It weighs no card at all: for every legal move it plays that
move out on a copy of the game - answering its own questions along the way exactly as it would
answer them for real - and scores the board it is left with. A draw shows up as cards in hand, a
delete as their line dropping, and a card whose text fizzles as nothing, which is what it was.

**How far it looks is further than it sounds.** Resolving a play runs the whole of the rest of the
turn - the cache check, the end commands, the turn passing - and then the opponent's turn *begins*:
the component is taken and every line they have won is compiled. So the board `deep` scores is one
on which the answer to a careless move has already been paid for.

It stops at the first thing the **other** seat has to answer, and at the card they choose to play,
which is the honest place to stop: what they will do is their business, and a machine that guessed
for them would be reading a hand it cannot see.

**One thing that stopping costs it was measured and then not fixed.** A card like *"your opponent
discards 1 card"* halts the search on them, so the board `deep` scores is one on which they have not
discarded — the best half of the card goes unpaid. Paying for it with `hard`'s estimate at exactly
that boundary is a natural idea and about fifteen lines; it was written, measured over eight hundred
deals, and moved the record by six games. On a posed board it did not change so much as which card
was chosen. So it is not in the tree, and this paragraph is where the next person to have the idea
will find the measurement.

It keeps `medium`'s arithmetic as a tie-break. Two moves that leave the same board are still not
equal: one of them may have spent a five to get there.

Ties are broken by the generator rather than by list order, which matters more than it sounds: a
machine that always took the first of an equal set would draft the same three protocols every game
and play the same line every turn.

## What is not here

Written down rather than left to be discovered:

- **A machine that weighs the reply.** `deep` plays its own move out and reads the board that
  leaves - which already includes the opponent taking the control component and compiling anything
  they had won, because the simulation runs past the turn boundary. What it does not see is the
  card they *play* in answer: it can tell that a line of theirs was going to compile, but not that
  the five in their hand is about to make one that was not. Weighing the reply means guessing at a
  hidden hand, a face-down table and a shuffled deck, which is a different kind of machine and a
  real piece of work.

  **Nothing else is missing.** Every rule is in, all ninety cards say the whole of what they are
  printed to say, and every machine here is measurably better than the one below it.

**A game is playable and winnable as it stands**, cards and all. Two machines finish every deal
they are given - **two thousand four hundred of them without one unfinished** - and the record
replays to the same position. And every rung of the machine is measurably above the last, from
either seat, which is the only honest way to ask whether words like that mean anything.

That first figure is a scar as much as a boast. Playing the machines against each other is what
turned up **the one endless game this codebase has had**: a shift used to lift a card off its line
in one step and lay it down in the next, and the pile looks at the table between every two steps -
so for one look the card was nowhere. It came back a card the game had never seen, its middle box
fired, and Gravity-1, which can point its own shift at itself, did that for ever. The fix is that
a card leaves and lands in the **same step**, and [the regression is
checked](../../../tests/compile.fsx) by name.

**Two machines of the same skill are not even, though**, and it is worth writing down: the first
seat takes about five games in eight, and it makes no difference whether they count or read. A
machine that plays this as a race to ten arrives first if it moves first - so the figure is at
least as much a fact about *these machines* as about the game. The 1-2-2-1 draft balances the
**draft**; nothing in the rules balances the turn order, and a person redresses it with card text.

**The invariant the tests hold to had to be restated, and this is where.** It used to be
*eighteen cards each, wherever they are* - deck plus hand plus discard plus everything on the
table. That was the strongest thing true, and the second compile made it false: a card can cross
the table now, so a player can be holding nineteen while the other is down to seventeen. What
survives is **thirty-six in total, each in exactly one place**, and the per-player count is a
thing that drifts on purpose. Both are checked - the weaker one over a game where nothing
crossed - so that the day something makes cards appear from nowhere, one of them says so.

## The files

Sixteen, and the four-line `Program.fs` that is every game's door, in the shape every game here
has: `Rules` is how it is played and contains no English and nothing from the table layer;
`Reading` is how it is read.

| File | Role |
| --- | --- |
| [Protocols.fs](Rules/Protocols.fs) | The fifteen, and the words for them |
| [Cards.fs](Rules/Cards.fs) | A card, which way up it lies and what that makes it worth, and a deck shuffled out of three protocols |
| [Effects.fs](Rules/Effects.fs) | What rules text is made of - as data rather than functions - and what the pile is a list of |
| [Printed.fs](Rules/Printed.fs) | What is on each of the ninety - all of them, and this file is the only copy |
| [Field.fs](Rules/Field.fs) | The lines, what a stack is worth, one player's half of the table, and the question no one half can answer: where a card may go face up |
| [Drafting.fs](Rules/Drafting.fs) | Whose pick it is, which is a list of six and nothing else |
| [Session.fs](Rules/Session.fs) | The stages, the pile, and whose turn it is - which the pile answers first |
| [Events.fs](Rules/Events.fs) | Everything the game has to say, above both things that say it |
| [Resolving.fs](Rules/Resolving.fs) | The pile: one command at a time, with a look at the table between every two |
| [Turn.fs](Rules/Turn.fs) | `Move`, and which moves the rules will take, when |
| [Words.fs](Rules/Words.fs) | Every string a player reads |
| [Rival.fs](Rules/Rival.fs) | Four seats the program can play, each measurably above the last: random, counting, reading, and playing the move out |
| [Ink.fs](Reading/Ink.fs) | Two colours: one per side of the table |
| [Parse.fs](Reading/Parse.fs) | Three verbs, each with a short form |
| [Render.fs](Reading/Render.fs) | Every screen described once as a [`Scene`](../../../README.md#a-screen-described-once), which `Readers` then draws three ways |
| [Offer.fs](Offer.fs) | Both seams filled in - **twice**, out of one function: the game, and the game with the control component |

**There used to be one file more**, and its going is worth a line. `Cards.js` was the ninety
cards as they are really printed - not F#, not built, and the source everything in
[Printed.fs](Rules/Printed.fs) was transcribed from. It stayed while it was the only copy of cards
that had not been typed in yet, and it went in the commit that took the count to ninety. Two copies
of the same ninety cards are two things that can disagree, and only one of them is the one the game
plays.

**Six cards apiece, and not the same six.** There are **seven** numbers a card can carry, and every
protocol goes without exactly one of them. Twelve go without the 6 and run 0 to 5. Three carry a 6
and give up a number lower down to pay for it:

| | has | goes without | and the six says |
| --- | --- | --- | --- |
| **Gravity** | 0, 1, 2, 4, 5, **6** | 3 | *"Your opponent plays the top card of their deck face-down in this line."* |
| **Love** | 1, 2, 3, 4, 5, **6** | 0 | *"Your opponent draws 2 cards."* |
| **Metal** | 0, 1, 2, 3, 5, **6** | 4 | *"When this card would be covered or flipped: First, delete this card."* |

So [Cards.fs](Rules/Cards.fs) states the rule as **which number a protocol is missing** rather than
as a list of what it has - `Card.without`, one line and three exceptions. That is worth more than a
card list, because `gravity-3` is then not a card: a player who types it is told so rather than
handed something nobody's deck contains, and `Faults` can say whether every protocol still comes to
six.

A six is most of a compile on its own, which is why all three of them hand something to the
opponent or refuse to be built on.

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
