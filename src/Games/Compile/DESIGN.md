# Compile — how the rules are held and resolved

[README.md](README.md) is how Compile is played and read: [the rules](README.md#the-rules),
[the words](README.md#the-words), [the board](README.md#the-board), [the machine](README.md#the-machine),
[the settings](README.md#settings), [the files](README.md#the-files) and [the checks](README.md#checks).
This file is for whoever opens `Rules/`: how a turn is represented, what a card's text is as data,
how that data is resolved, and where the few things the game has to *remember* live.

## The loop

A game is a `Session` ([Rules/Session.fs](Rules/Session.fs)) in one of four `Stage`s — `Drafting`,
`Arranging`, `Playing`, `Done` — over a `Field`, which is a private `Map<PlayerId, Side>`
([Rules/Field.fs](Rules/Field.fs)). A `Side` is everything one player has: `Drafted`, `Order` (the
protocol facing each line, by index), `Deck`, `Discard`, `Hand`, `Stacks: Map<int, Placed list>`
and `Compiled: Set<Protocol>`. The draft and the laying-out are plain state changes in
[Rules/Turn.fs](Rules/Turn.fs); once `Session.dealHands` has shuffled each deck and drawn five, every
move goes through the pile.

In `Playing` a move is `Play of Card * line * Face` or `Refresh`, and `Turn.play` and
`Turn.refresh` do the same thing with it: put steps on `session.Pile` and let
`Resolving.settle` run them down. `Resolving.ending` puts `Trimming; Closing; EndTurn` on the
*bottom*, so everything the move sets off runs before the turn is closed out; `laying` puts the
card on the top. From one move to the next player's first free choice the pile runs:

| step | what it does |
| --- | --- |
| `Placing` or `Refreshing` | the move itself — a card lands, or the hand goes down and five come up |
| `Trimming` | the check cache phase: a hand over `Deck.HandSize` is asked down to it, one discard at a time |
| `Closing` | the `AtEnd` commands of the uncovered face-up card in each of the mover's lines |
| `EndTurn` | `ToPlay` passes, `Turn` counts up, and `Opening :: BeginTurn` go on |
| `Opening` | the `AtStart` commands of the uncovered face-up card in each of the new player's lines |
| `BeginTurn` | the control component, then `Escaping` and `Compiling` for every won line |

Compiling is never a move: `beginning` finds `Field.winning` and pushes one `Compiling lines` step
for all of them, so nobody is asked and nobody can decline. `Session.NoCompile` is spent here — set
to a seat by `StopTheirCompile`, it skips that seat's check once, whether or not anything would have compiled.

**Values.** `Field.valueOn seat line field` is the only place a line is counted, and it needs both
sides: your stack summed — face up at its printed value, face down at `Placed.FaceDownValue` (2)
unless a `FaceDownWorth n` of yours is in force on that line — plus your `LinePlus n` and
`LinePlusPerFaceDown n` for each face-down card, less the other side's `TheirLineMinus n`, and
never below nought. There is no `Side.valueOn`, because a side cannot answer alone.

**Compiling.** `Field.won` is `valueOn >= Stack.ToCompile` (10) and strictly more than
`Field.opposing`. `compileLine` reads the protocol off `Side.protocolOn line` *at that moment*,
takes the top card of the other deck if it is one already compiled (`taking`, restocking their deck
from their discard first), sweeps both sides' stacks in that line into their own discards
(`Side.swept`), and marks the protocol compiled if it was not. `Side.hasCompiledAll` ends the game
as `Done(Won seat)`.

**Whose card it is.** A `Card` is `{ Protocol; Value }` and nothing else ([Rules/Cards.fs](Rules/Cards.fs)).
Who holds it is where it sits: the side whose `Hand`, `Deck`, `Discard` or `Stacks` it is in, so a
delete, a return or a sweep always goes to the side the card was lying in and there is nothing to
look up. A card changes sides only by a second compile, `TakeTheirTop`, `TakeAtRandom` or `Give`.
Where it may go face up is a question about the *line*, not the card: `Field.allows` asks whether
the card's protocol is one of the two facing that line (`Field.protocolsOn`, both sides) or whether
you have `YouMayPlayAnywhere` in force. Nothing checks a hand against `Order`, which is what lets a
card of theirs in your hand go face up where their protocol sits.

**Face up and face down.** A card on the table is a `Placed = { Card; Face; Seen }`. `Seen` is
whether the other player has ever had it face up in front of them — set by `Placed.laid` for a
face-up play and by `Placed.turned` when a card turns face up — and is what `Placed.readableBy`
and `peek` read. A face-down card is worth 2 and says nothing: `Ruling.saying` returns nothing for
it, the look-again does not see it, and `laying`, `listening` and `Escaping` all filter on
`Placed.isFaceUp`. `Turn.play` refuses in this order — `MustRefresh` for an empty hand,
`NoSuchLine`, `NotInHand`, `Forbidden` from `Field.barred`, then `NotFacingThere` carrying
`Field.facingLines` — and takes the card out of the hand before the pile starts, so a card is
briefly in the air between hand and table.

**Refresh.** `Side.refreshed` puts the hand on the discard and draws `Deck.HandSize` through
`Side.drawing`, which shuffles the discard back into an empty deck (`restocked`) and stops short
when there is nothing to shuffle. `Deck.HandSize` is what a refresh draws to and what `Trimming`
trims to, one number for both. A card can call for the same thing with `RefreshHand`.

**Covered and uncovered.** A stack is newest first, so `Stack.uncovered` is `List.tryHead`, and
`Side.rulesOn` hands `depth = 0` to `Ruling.saying` as *uncovered*. `Printed.ongoing uncovered card`
is `Top @ Bottom` for an uncovered card and `Top` alone for a covered one — the top box holds for
as long as the card is face up, the bottom only while nothing is on it. `UnderThis` is the one way
a card arrives at the bottom of a stack, covering nothing.

## What a card says

A card's text is a `Text` record ([Rules/Effects.fs](Rules/Effects.fs)), nine lists, each read
from one place:

| field | holds | read by |
| --- | --- | --- |
| `Top` | `Ongoing` rules | `Side.rulesOn`, while face up, covered or not |
| `After` | `(Trigger * Command list)` | `listening`, while face up, covered or not |
| `WhenFlipped` | commands | `carriedOut` for a `Flip`, pushed ahead of the `Turning` — read off the card whichever way it lies |
| `WhenCompiled` | commands | `Escaping`, for every face-up card in a line about to be compiled, either side |
| `Shown` | commands | the look-again, the moment the card becomes the face-up uncovered card of its stack |
| `Bottom` | `Ongoing` rules | `Side.rulesOn`, while face up *and* uncovered |
| `AtStart`, `AtEnd` | commands | `Opening` and `Closing`, for the uncovered face-up card of each line on the side whose turn it is |
| `WhenCovered` | commands | `laying`, pushed ahead of the `Placing` that would cover it, if the card is face up and uncovered |

`Words.boxes` ([Rules/Words.fs](Rules/Words.fs)) prints `Top`, `After`, `WhenFlipped` and
`WhenCompiled` in the top box, `Shown` in the middle and the rest in the bottom, which is why the box
a line is printed in says how long it holds.

### The vocabulary

`Command` is closed, and every card is spelt out of it. The words are what `Words.printing` writes
for each case:

| command | printed as | how it resolves |
| --- | --- | --- |
| `Delete`, `Flip`, `Return`, `Show` of `Selector` | delete … / flip … / return … to hand / reveal … | every card on the table the selector names: none fizzles, one is done, several are asked |
| `Shift of Selector * Where` | shift … to this line / to another line / either to or from this line | the card, then `ALine` for where; a `Where` that leaves one line does not ask |
| `Discard`, `Give`, `Reveal` | discard a card / give a card from your hand… / reveal a card from your hand | the same, over the actor's hand |
| `Draw of Count` | draw a card, draw 2 cards, draw cards equal to that card's value, draw that many cards plus 1 | `Side.drawing`; `Done` is how many came |
| `RefreshHand`, `TakeTheirTop`, `TakeAtRandom`, `RevealTheirHand`, `StopTheirCompile` | refresh / draw the top card of your opponent's deck / take a card at random… / your opponent reveals their hand / your opponent cannot compile next turn | done outright; `TakeAtRandom` is the only command that asks the generator |
| `PlayFromHand of Face * Where` | play a card from your hand face down in another line | the line first (`ALineFor`), then the card, landing through `laying` |
| `FromDeck of Face * Where` | play the top card of your deck face down in this line | the line first when the card does not say, then the top of the deck into it |
| `UnderThis of Face` | play the top card of your deck face down under this card | to the bottom of the card's own stack |
| `Rearrange of Whose`, `Swap` | rearrange your / their protocols; swap the positions of two of your protocols | an `AnOrder` question — every order, or the three one swap reaches |
| `Opposing` | your opponent: … | the same command with the other seat as actor |
| `May` | you may … | a `Whether` question, or a fizzle if there is nothing to do |
| `Either` | either … or … | a `OneOf` question if both halves could happen; the live half alone if only one could |
| `IfYouDo of Command * Command list` | …. If you do, …, then … | `Run first :: Gate rest` |
| `IfCovering` | if this card is covering a card, … | read off the stack |
| `Every` | flip *every* … | `carriedOut` for each target, first found first done, with no question about which |
| `Times of Count * Command` | …, 3 times over / once for every 2 cards in this line | that many `Run`s |
| `OneOrMore` | …, one or more times | `Run inner :: Repeating` |
| `InAChosenLine`, `InAChosenLineOf of atLeast` | …, in a line of your choosing / with 8 or more cards | an `ALineFor` question, then the inner command with its `Source.Line` moved |
| `InEachOtherLine`, `InEachLineHolding` | …, in each other line / in each line where you have a card | one `Run` per line, each with its source moved there |

A `Selector` is a record built on `Select.any` by one combinator per field — `yours`, `theirs`,
`here`, `elsewhere`, `faceUp`, `faceDown`, `uncovered`, `covered`, `worth [ 0; 1 ]`, `other`,
`thisCard`, `thatCard`, `highest`, `lowest` — read by `onTable` as one long *and*: `Worth` is what a
card is worth on the table, `thatCard` is `Session.Chose`, and `Pick` narrows to the extreme with
ties surviving. A `Count` is `Just n`, `WorthOfChosen`, `HowManyPlus n` (from `Done`) or `PerCards`.

The ten `Ongoing` rules are each asked from the one place they bite, and that place has to be
found for anything added: the four value rules in `Field.valueOn`; `TheyCannotPlayHere`,
`TheyCannotPlayFaceDownHere` and `TheyMustPlayFaceDown` in `Field.barred`; `YouMayPlayAnywhere` in
`Field.allows`; `SkipsCacheCheck` in `Field.skipsCache`, read by `Trimming`; `Silence` in
`Field.silenced`, read by the look-again. The four `Trigger`s — `YouDraw`, `YouDelete`,
`TheyDiscard`, `YouClearCache` — are listened for in one place, `heard`, below.

## Resolving rules text

[Rules/Resolving.fs](Rules/Resolving.fs) is the interpreter, and `session.Pile` is a stack of
`Pending` work, head first:

```fsharp
type Pending =
    | Run of Command * Source            // a command, and which card at which seat and line said it
    | Ask of Question                    // stopped, waiting on somebody
    | EndTurn | BeginTurn | Opening | Closing
    | Compiling of lines: int list       // the won lines, all at once
    | Escaping of lines: int list        // WhenCompiled, ahead of the Compiling
    | Refreshing | Trimming
    | Repeating of Command * Source * tally: int
    | Placing of PlayerId * Placed * line: int * from: Origin
    | Turning of PlayerId * Placed * line: int
    | Gate of Command list * Source
```

A `Source` is `{ Owner; Saying: Card; Line }` — whose command, which card, and the line that card
is in, which is what `ThisLine` means. A `Question` is `{ Chooser; Because: Asker; Wanting }`, where
`Because` is `ACardSaying source`, `TheControlComponent` or `TheCacheCheck` and `Wanting` is one of
six shapes — `ACard of Command * Target list`, `AnOrder of whose * Protocol list list`,
`ALine of Target * int list`, `ALineFor of Command * int list`, `Whether of Command`,
`OneOf of Command * Command`. A `Target` is `OnTable(seat, line, placed)` or `InHand(seat, card)`.

**The walk.** `walk` does one thing per step, up to `Runaway` (500) steps, and then stops where it
is rather than hang the table. Before taking the head it looks again — unless the head is a `Gate`,
which must read the `Done` of the command it was pushed under before anything uncovered can
overwrite it. `lookAgain` finds the face-up uncovered card of every stack on both sides
(`shownNow`), compares it with `session.Revealed`, the set of cards that were showing at the last
look, and pushes the `Shown` text of every newcomer, in seat then line order, skipping any in a
`Field.silenced` line. So a card speaks when it is played face up, when it is flipped face up, and
when whatever covered it leaves — three moments and one mechanism — and cannot speak twice without
going out of view in between. Fixtures that lay cards down by hand end with `Resolving.asRead`,
which sets `Revealed` to what is showing so a hand-built position is not read as a history.

Then the head: an empty pile or an `Ask` stops the walk; a `Run` is popped and handed to `resolve`,
so whatever it pushes goes ahead of what was already waiting — newest first — and the notices it
returned are read by `heard`, which pushes the `After` listeners they woke: `Drew` by the actor for
`YouDraw`, any `Deleted` for the actor's `YouDelete`, and `Discarded` by a seat for the *other*
seat's `TheyDiscard`. A compile's sweep is not a `Deleted`, so nothing hears it.

**Validity is checked when a command resolves, not when it was written.** `resolve` works out the
actor (the other seat under `Opposing`), then for anything with a selector or a hand asks
`targets`: none is a fizzle — `Fizzled` is said, `Done` goes to 0 and the pile carries on — one is
carried out with `Done = 1` and `Chose = Some card`, several become an `Ask`. `May` refuses to
offer the impossible and `Either` offers only the halves that could happen, which is the same rule
at the other end. `Gate` runs its tail if `Done > 0` and drops it with a `Fizzled` if not;
`Repeating` asks *another?* for as long as the last one did something and there is something left
to do it to, and puts the running tally into `Done` when it stops, which is what `HowManyPlus`
reads.

**A card leaves and lands in one step.** A play, a shift, a `PlayFromHand` and a `FromDeck` all go
through `laying`, which pushes the uncovered card's `WhenCovered` ahead of a
`Placing(seat, placed, line, from)`; `Placing` lifts the card off its old line (`from = FromLine`)
and puts it on the new one together, so the table is never missing a card between two looks. A
`Flip` pushes the card's `WhenFlipped` ahead of a `Turning`, which checks the card is still where it
was before turning it. An interrupt never stops the move — it changes the board and lets the card
through.

**Where a pause lands.** An `Ask` at the head of the pile is the whole of "the game is mid-effect".
`Session.asking` reads it, `Session.active` answers the `Chooser` ahead of the stage — which can be
the player whose turn it is not — and `Session.doing` says `AChoice`, so a refusal can say what is
wanted. With a question pending, `Turn.asked` sends `Choose` to `Resolving.choosing` and `Arrange`
to `Resolving.ordering`, lets `Resign` end the game, and refuses anything else with `AnswerFirst`.

**How an answer resumes it.** `choosing` matches the `Wanting` against the `Chosen` and refuses
anything not on offer with `NotOnOffer`. On a match it pops the `Ask`, does what the answer chose —
`carriedOut` with the card's source, `resolve` of the half a `Whether` or `OneOf` picked, `moving`
for an `ALine`, a `Run` with its `Source.Line` set for an `ALineFor`; the cache check, which has no
source, discards outright — sets `Done` and `Chose`, and calls `carryOn`: `heard`, then `settle`,
so the walk continues from exactly the step below the question. `No` sets `Done = 0` and says
`Declined`. `ordering` sets `Side.arranged` on the side the `AnOrder` named, which is not always
the chooser. An answer is a `Move` — `Choose of Chosen` or `Arrange` — so it lands in the record
in the same words as anything else ([records](../../../README.md#records)), and `pile` at the prompt
prints the pile as `Words.waiting` reads it:

```
  1.  waiting on you - say one of: Water-2, Water-3.
  2.  Fire-4: discard a card, again if you want it.
  3.  Fire-4: if that did anything, draw that many cards plus 1.
  4.  any hand over 5 comes back down to it.
  5.  the end commands of everything face up and uncovered.
  6.  the turn is handed on.
```

**What is remembered.** Almost everything is read off the board: a standing rule is a card lying
face up, and stops when that card is covered, flipped or deleted. Four fields on `Session` are the
exceptions — `Revealed`, `Done` and `Chose` (the last command's tally and pick, read by the next),
and `NoCompile`, the only rule that outlives the card that said it.

What a move says back is a `Notice` ([Rules/Events.fs](Rules/Events.fs)): `Happened of Happening`
— `Played`, `Flipped`, `Deleted`, `Drew`, `Fizzled`, `Asked`, `Compiled` and the rest — or
`Refused of Refusal`. `Words.said` puts each into a sentence and `Words.saidTo seat` keeps back what
a seat may not know: the other order while it lies face down, and the name of a card played face
down or taken off your deck.

## The control component

`Control` is `NotInPlay`, `InTheMiddle` or `HeldBy seat`, held on the session and set at the deal.
`takingControl` runs first in `BeginTurn`: nothing if the rule is not in play, if `Field.leading`
is under `Field.LanesForControl` (2), or if this seat holds it already; otherwise `HeldBy seat` and
a `TookControl(seat, from)`. `Field.leads` is `valueOn > opposing` — no ten needed, and a tie is no
lead.

Paying for it is an `Ask` no card wrote: `rearranging seat` is
`{ Chooser = seat; Because = TheControlComponent; Wanting = AnOrder(seat, every order but the current) }`
— five of the six. `Resolving.refreshing` pushes it ahead of `Refreshing`, and `beginning` pushes it
ahead of `Escaping :: Compiling`, each with a `MustRearrange`. The answer is the same `Arrange`
move that laid the protocols out. That is what makes compiling non-atomic: the won lines are settled
by values, the question is asked, and `compileLine` reads which protocol faces each line
*afterwards*. The stacks do not move, so a line built for one protocol can compile another, and a
line whose protocol has moved onto one already compiled compiles it again.

**How it ships.** `Rules.Deal` ([Engine/Rules.fs](../../Engine/Rules.fs)) takes a count and a seed
and nothing else, so an option cannot be a parameter of it. It is a second value instead:
`Offer.offering control` ([Offer.fs](Offer.fs)) builds a whole `Playable` round `deal control`, and

```fsharp
let playable = offering NotInPlay        // "compile"
let withControl = offering InTheMiddle   // "compile-control"
let ways = [ playable; withControl ]
```

[Program.fs](Program.fs) hands `Offer.ways` to `Play.only`, and [Games.fs](../../Games.fs) hands
them to `Play.chosen`, which names both (`Names`) and lets `settings.txt` pick with
`plays compile-control` under `[compile]` ([settings](../../../README.md#settings)). The two differ
in what `Deal` seeds `Control` with and in `Name`, `Title` and `Blurb`, and in nothing else —
`Render` draws the component only when `Session.withControl` — and [compile.fsx](../../../tests/compile.fsx)
holds their `Faults` equal and runs `Conforms.against` on both.

## A card is one line

[Rules/Printed.fs](Rules/Printed.fs) is the ninety cards, and each is one `let`:

```fsharp
let private loveOne =
    { shown [ TakeTheirTop ] with
        AtEnd = [ IfYouDo(May Give, [ Draw(Just 2) ]) ] }
```

which `Words.printed` turns into *Draw the top card of your opponent's deck.* and *At the end of
your turn: You may give a card from your hand to your opponent. If you do, draw 2 cards.* — the
English is generated from the data, so a card cannot say one thing and do another, and every view
gets the wording at once. The helpers — `standing`, `shown`, `whileClear`, `atStart`, `atEnd`,
`after`, `whenCovered` — each fill one field of `blank`, and a card that uses two starts from one
and sets the other. `theFive` is `shown [ Discard ]` and stands for all fifteen fives.

Text is a lookup, not a field on the card: `listed` pairs each `Card` with its `Text`, `texts` is
the `Map`, and `Printed.on card` answers `blank` for anything not in it. That keeps `Card` two
fields, cheap to compare and to name (`Card.byName "fire-3"`), and a hand a plain `Card list`.
Adding a card is one `let` and one row in `listed`; `Printed.unwritten` and `Printed.twice` are
what `Faults` reads to catch a card left off or listed twice. A private `without` in `Card` is
the one value each protocol lacks — the 6 for twelve of them, and Gravity's 3, Love's 0 and
Metal's 4 — so `Card.inProtocol` is six cards and `Card.exists` refuses the seventh.

Data rather than closures is what lets four readers share one source — `Words` prints it,
`Resolving` runs it, `Rival.weighing` prices it, `Offer.faults` checks it — and why two sessions
that are the same position compare equal, which half of the suite depends on.

## What Faults holds

`Playable.Faults` ([Table/Playable.fs](../../Table/Playable.fs)) is what is wrong with the game as
described; the table refuses to open one that has any and prints them. `Offer.faults` is a list
comprehension over the constants and the cards, and it yields for:

- a protocol listed twice in `Protocol.all`, or fewer of them than `Draft.Picks`
- a number repeated in `Card.values`, or a protocol without exactly `Card.PerProtocol` cards
- `Lines.Count` not equal to `Protocol.Each`; `Deck.Size` not `Each × PerProtocol`; an opening hand bigger than the deck
- `Placed.FaceDownValue` above the best printed value or below the worst
- `Stack.ToCompile` that one card could reach alone, or that a whole protocol could not reach
- a `Draft.order` that is not `Picks` long, not `Each × Seats`, not `Each` picks per seat, or naming a seat nobody is in
- `Field.LanesForControl` above the number of lines or under 1, or protocols that cannot be put in another order
- a card with nothing printed on it, or printed twice over
- on any card: a `Draw(Just n)` under 1 (inside an `Opposing` too), an `Opposing` of an `Opposing`, the same standing rule twice, or one rule in both the top and the bottom box

## The machine's reading of the state

`Rival.plays session rival` ([Rules/Rival.fs](Rules/Rival.fs)) is asked whenever `Session.active` is
a seat a machine holds — including in the middle of the other player's turn, when a card has
stopped on it. It reads the same things a person can: its own `Hand`, both sides' `Field.valueOn`,
`Side.protocolOn`, `Side.hasCompiled`, `Stack.uncovered` for what a play would cover, and
`Printed.on` for what a card says. It never reads the other hand: the `Target`s of a question put
to it are on the table or in the chooser's own hand, and nothing else of theirs reaches it.

With a question pending it answers by the `Wanting`'s shape — a `Target` from an `ACard`, a line
from an `ALine` or `ALineFor`, an order from an `AnOrder`, `Yes`/`No` or `TheFirst`/`TheSecond` —
and gives back the same `Choose` or `Arrange` a person would type. Otherwise it goes by stage: a
`Take` from the pool, an `Arrange` of what it drafted, or in play a `Play` chosen from every
legal way — each card in hand on each of `Field.facingLines` where `Field.barred` allows it face
up, and on every line it allows face down — with `Refresh` when there is none. The legality test is
the one `Turn.play` uses, so a machine cannot pick a move the rules would refuse and stall the
table. The `deep` skill plays each candidate out on a copy through `Turn.asked`, answering its own
questions along the way, and scores the board left behind. What the four skills weigh is in
[the README](README.md#the-machine); how a machine is seated and held to what a player is held to is
the engine's ([against the machine](../../../README.md#against-the-machine),
[Engine/Machines.fs](../../Engine/Machines.fs)).

## Holding it to this

[compile.fsx](../../../tests/compile.fsx) ([checks](README.md#checks)) loads
[Compiled.fsx](../../../tests/Compiled.fsx) — the engine, the table and the game's files in the
order [Compile.fsproj](Compile.fsproj) compiles them — and checks each mechanism above against real
cards: the walk's order, the fizzle, the question and its answer, every interrupt, the gate, the
tally, the cache check, control, the conservation of thirty-six cards, and that each card prints
what it does. Its fixtures lay cards down with `quiet`, which refuses a card with a standing rule,
an end command or an interrupt, or `mute`, which refuses one with any text at all, so scenery
cannot start talking. Both `Playable`s are then held to [Conforms.fsx](../../../tests/Conforms.fsx),
and the seam itself is unchanged by this game ([SEAM.md](../../../SEAM.md)).
