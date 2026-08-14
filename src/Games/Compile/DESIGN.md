# Compile — the rest of the design

What is [in the tree](README.md) is the table: the draft, the protocols against the lines, the
decks, the hands, and a card going onto a stack. This is the design for everything after that —
what a card is worth, what ends a turn, how a protocol is compiled, how a game is won, what the
control component does, and how the ninety cards' text actually resolves.

**It was a design before it was a diff, and all of it is built now.** It has been kept in the tense
it was written in rather than tidied into the past, because what is worth reading here is the
*reasoning* — the rulings that turned out to matter, the shapes the ninety cards asked for, and the
things this document had wrong until the cards arrived. Each row and each section says where it
landed.

The two that were hardest to get right were the [resolution pile](#3-resolving-rules-text), which is
the whole technical core and is easy to get subtly wrong, and [control](#4-control), which makes
compiling non-atomic and is the reason this document exists in this shape rather than the last one.

**The order it was built in** is [at the bottom](#8-seven-steps). The first three steps give a
complete, winnable game with no card text at all — which was not a staging convenience, because the
numbers game underneath Compile is a real game, and having it playable is what made the effects
testable.

---

## 1. The loop

A turn is four beats, and only one of them is a free choice:

```
   CONTROL    if you lead two lanes, take the control component
   COMPILE    compile any line you have won  (holding control? rearrange first, and it is forced)
   ACTION     play one card, or refresh      (holding control? refreshing rearranges first, too)
   END        the end commands of everything uncovered, then the turn passes
```

### Values, and the ten

Every card has a value, 0 to 6, and a protocol has six of the seven. A stack's value is the sum of
the cards in it. A line is a contest between two stacks facing each other across the table.

**You compile a line when, at the start of your turn, your stack in it is 10 or more *and* strictly
more than theirs.** Compiling flips the protocol facing that line to its compiled side and **deletes
every card in that line — both players'** — to their owners' discard piles. The line starts again
from nothing.

**Compiling is mandatory.** A line that qualifies is compiled, whether or not you wanted it — which
is what stops control from being dodged by simply declining, and what makes the next rule reachable
rather than theoretical.

**You win when all three of your protocols are compiled.**

The 10 is the clock. Two cards can reach it and three comfortably do, which is what stops a line
becoming a pile nobody can move.

### Compiling one that is already compiled

Because compiling is mandatory and [control](#4-control) can move a protocol you did not want moved,
you will end up qualifying on a line whose protocol is already compiled. When that happens:

> **Take the top card of your opponent's deck into your hand.** If their deck is empty, shuffle
> their discard into it first. That card may be played face-down like any other, or **face-up on the
> line where its protocol sits — even though that protocol is on their side of the board.**

The line is still wiped, both players' cards and all. So a second compile is not a wasted turn and
it is not only a consolation prize — it is a **board weapon**: it deletes whatever the opponent had
built in that lane, and it takes a card out of their deck and puts it in your hand. It is
attrition. Do it often enough and they are reshuffling a thinner deck while you are holding cards
they were counting on.

**This is the rule that broke the conservation invariant**, and it was worth saying loudly because
[the tests asserted the opposite](../../../tests/compile.fsx): *eighteen cards each, wherever they
are*. Once a card can cross the table it becomes **thirty-six in total, each in exactly one place**,
and the per-player count is a thing that drifts on purpose. That test was restated in the same
commit as this rule and not after it, which is the only way a restated invariant is worth anything.

It also means **a hand can hold a card of a protocol nobody at this table drafted for you** — so
nothing may check a card in hand against `Side.Order`.

### Whose card is it

A stolen card is **yours until a card effect gives it back**, and there are cards that do. Two
different questions hide in that sentence, and the useful thing is that **neither of them needs a
field**:

| | | |
| --- | --- | --- |
| **holder** | whose zones it is sitting in right now | *positional* — it is in your deck, hand, discard or stack, so it is yours |
| **home** | whose protocol it belongs to | *derivable* — the card names its protocol, and the draft settled who took that |

Holder is positional because every zone in this game already hangs off a `Side`. A card in my stack
is in `(Field.side me).Stacks`, so "delete it to its owner's discard" is *this* side's discard and
there is nothing to look up. Home is derivable because protocols are fixed at the draft and never
move — `Field.homeOf card` is a search of two `Order`s and it is total, because a card can only be
in play if somebody drafted its protocol. That last sentence is a `Faults`-shaped invariant and
should be checked as one.

So "give it back" needs no bookkeeping to have been kept along the way. **And in the end it needed
no command either** — reading the ninety turned up no card that says *return this to whoever owns
it*, and two that say *give 1 card from your hand to your opponent*:

```fsharp
| Give        // Love-1 and Love-3, and the only way a card crosses back
```

Worth writing down because the obvious modelling — an `Owner` field on every card — is wrong twice
over: it duplicates what the zone already says, and it would make two states that are the same
position compare unequal.

### Face-up, face-down

The action is *play one card*, and there are two ways to do it:

| | where it may go | value | text |
| --- | --- | --- | --- |
| **face-up** | only a line where the card's protocol is one of the two facing each other | printed | all of it |
| **face-down** | any line | **2** | none |

Everything else leans on this. A card you cannot use is still worth 2 anywhere, so a hand is never
dead — but spending a 5 as a 2 to win a race is a real cost.

**"One of the two" is the whole rule, for every card.** Their Fire on line 2 lets you play your Fire
cards face-up there. So an opponent's protocol order is a standing gift, and choosing where to put
your own three is choosing what to hand them — which is why the legality test is a question about
*the line* and never about whose card it is:

```fsharp
let mayPlayFaceUp card line field =
    Session.seats
    |> List.exists (fun seat -> Side.protocolOn line (Field.side seat field) = Some card.Protocol)
```

**Which raises the arranging.** [The code lays the protocols out in seat order and in the
open](README.md#how-a-game-goes) — Player 1 sets theirs, then Player 2 sets theirs having seen them.
With this rule that is a real advantage to whoever goes second, and it undoes what the 1-2-2-1 draft
was shaped to balance. It should almost certainly be **simultaneous and hidden, then revealed
together**. The state is already a `Map<PlayerId, Protocol list>` filled one seat at a time, so what
changes is `active` no longer stopping at the first empty entry and `SeenBy` learning to keep one —
which is the first thing at this game that is genuinely secret, and the first use it has for the
half of the seam built for exactly that.

### Refresh

The other action: **discard your hand and draw five**. It costs your whole turn, and **you must
refresh if you begin your action with an empty hand.** There is no end-of-turn draw. Five cards is
five turns of tempo, and the turn you spend getting five more is a turn your opponent spends
getting closer to ten.

A deck that runs out is shuffled from its discard and drawn again.

### Covered and uncovered

Only the **top card of a stack is uncovered**. A covered card contributes its value and nothing
else — its text is out of play until whatever is on top of it leaves. That is what makes stacking
onto your own good card a real decision, and what makes *delete* worth playing.

---

## 2. What a card says

Three commands, by where they sit on the card:

| | when | what it is |
| --- | --- | --- |
| **top** | the moment it becomes face-up | a one-off: delete, draw, flip, shift |
| **middle** | continuously, while uncovered | a rule change: this counts as 0, this cannot be deleted |
| **bottom** | at the end of your turn, while uncovered | a repeating one-off |

**Each of those is a *list* of commands, not one command.** "Flip a card. Draw a card." is two
things, and the whole of the next section is about why that distinction is load-bearing rather than
punctuation.

**The only trigger in this game is a card becoming face-up.** That is worth stating as a rule
because it collapses two cases into one: playing a card face-up and flipping a face-down card
face-up both put that card's top commands onto the pile, by the same mechanism, and nothing else in
the game triggers anything. Being *uncovered* reveals middle and bottom commands, but those are
asked rather than fired — a middle command is a query and a bottom command is checked at the end of
a turn.

### The keywords

| word | what it does |
| --- | --- |
| **delete** | a card in a line → its owner's discard |
| **discard** | a card in hand → its owner's discard |
| **draw** | deck → hand (shuffle the discard in if the deck is out) |
| **flip** | a card in a line turns over where it lies — and if it lands face-up, its text fires |
| **return** | a card in a line → its owner's *hand* |
| **shift** | a card moves to another line, same owner's side |
| **play** | play a card as part of an effect, not as your action |
| **refresh** | discard your hand and draw five |
| **compile** | compile a line now, outside the start-of-turn check |
| **rearrange** | put your three protocols in a different order |

---

## 3. Resolving rules text

This is the core, and it is a **pile** — commands waiting to happen, newest first — with a check
between every two of them.

Take the worked example. A card reads *"Flip a card. Draw a card."*

```
   pile: [ Flip ; Draw ]

   1. pop Flip, resolve it. A face-down card turns face-up.
   2. LOOK AGAIN. That card is now face-up, so its own text goes on the pile — in front of
      the Draw that was already waiting.

   pile: [ Delete ; Draw ]        (say the flipped card read "Delete a card")

   3. pop Delete, resolve it.
   4. LOOK AGAIN. Nothing new became face-up.
   5. pop Draw — if it is still a thing that can be done — and resolve it.
```

Three properties fall out of that, and all three have to be in the types rather than in anybody's
discipline:

- **Newest first.** What a command causes resolves before what was already waiting. A pile, not a
  queue.
- **Look again between every two commands**, never only at the end. The Draw happens after the
  flipped card's whole text, not alongside it.
- **Still valid, checked when it resolves and not when it was written.** A command whose target has
  left the table since it was put on the pile fizzles, says so, and the pile carries on. That is not
  an error condition — it is the ordinary case, and it is why "Delete a card. Draw a card." draws a
  card even when there was nothing to delete.

### The types

```fsharp
/// One command. A card's text is a list of these; so is what a command causes.
type Command =
    | Draw of int
    | Discard of int
    | Delete of Selector
    | Return of Selector
    | Flip of Selector
    | Shift of Selector
    | PlayFromHand of Face option
    | Refresh
    | CompileLine of Selector
    | Rearrange
    /// "Your opponent discards 1" - the same command, done by the other player.
    | Opposing of Command
    /// The player picks a branch. Asks, so it goes through the pile like anything else.
    | OneOf of Command list list
    | Onlyif of Condition * Command list

/// Where a command came from: whose it is, which card said it, and which line that card is in.
/// Carried for three reasons - the log says which card is talking, `ThisLine` selectors need to
/// know which line that is, and a command can ask whether its own source is still on the table.
type Source =
    { Owner: PlayerId
      Card: Card
      Line: int }

/// What is waiting to happen, newest first.
type Step =
    | Run of Command * Source
    /// Stopped, waiting on somebody. Nothing moves until it is answered.
    | Ask of Question

type Question =
    { Chooser: PlayerId
      Wanted: Wanted
      Because: Source option }
```

And on the session:

```fsharp
type Play =
    { ...
      /// Newest first. Empty almost always, and the whole of what "the game is mid-effect" means.
      Pile: Step list

      /// Every spot on the table that was face-up when the pile was last looked at. Anything that
      /// is face-up now and is not in here has just been revealed, and its top commands go on.
      /// A set rather than a flag per card, because it is the *difference* that triggers.
      Revealed: Set<Spot> }
```

### The interpreter

One function, and everything above it is unchanged:

```fsharp
/// Run the pile down until it is empty or the top of it is a question.
///
/// The look-again is the first thing rather than the last, so a command that revealed something
/// cannot hand over to the next command before that something has had its say.
let rec settle session =
    match newlyRevealed session with
    | [] ->
        match session.Pile with
        | [] -> session
        | Ask _ :: _ -> session                                   // waiting on somebody
        | Run(command, source) :: rest ->
            let session = { session with Pile = rest }

            match validate command source session with
            | Error fizzle -> settle (session |> saying (Fizzled(command, fizzle)))
            | Ok target -> settle (resolve command source target session)
    | revealed -> settle (session |> pushing (revealed |> List.collect topCommands) |> marking revealed)
```

`Rules.Play` calls `settle` after every move and hands back whatever it stops at, so the engine sees
one move producing eight or ten notices — which the journal already keeps and the log already shows.
**Nothing above the game changes**, and that is the test of whether this design is right.

### Where the pause lands

`Session.active` gains a fourth answer, ahead of the other three:

```fsharp
let active session =
    match session.Pile with
    | Ask question :: _ -> question.Chooser
    | _ ->
        match session.Stage with
        | Drafting _ -> ...
```

Which is the same shape the file already has — `active` already answers one question three ways
because a draft, a laying-out and play have different senses of whose turn it is. A pending choice
is a fourth, and it can belong to the player whose turn it is *not*: "your opponent discards 1"
stops on them. `Doing` gains `AChoice` so a refusal can still say what the game is asking for.

---

## 4. Control

An **optional rule**, and the more interesting half of this document.

**Gaining it.** At the start of your turn, if you lead in **two or more lanes** — strictly, ties do
not count — you take the control component, from the middle or from your opponent. Nobody loses it
except by somebody else taking it, so it sits where it last landed until a start-of-turn earns it
away.

**Paying for it.** If you hold the control component and you **refresh** or **compile**, you must
first **rearrange your protocols into a different order.** Not may — must, and it has to be a *new*
order, so five of the six permutations are legal and standing pat is not.

### Which makes compiling non-atomic

This is the part that changes the code rather than a number. **The protocols move; the stacks do
not.** So the line that qualifies to compile is settled by values, and *which protocol that line
compiles* is settled afterwards, by an order the player is forced to change first:

```
   start of turn
     1. control check           - you lead two lanes, so you take the component
     2. which lines qualify?    - line 2, where you are at 11 against their 6
     3. you hold control        - so: rearrange, and it must be a different order
                                  (this is an Ask on the pile - the game stops here)
     4. you answer              - Spirit / Metal / Death, where it was Metal / Spirit / Death
     5. compile line 2          - which now faces Spirit. Metal is still uncompiled.
```

A stack built patiently for Metal compiles Spirit instead, because holding control meant you could
not leave the protocols where they were. That is a genuinely nasty, genuinely good rule, and it
means **`Compile` cannot be written as one step that reads the protocol and deletes the line.** It
is: find the lines, ask for the rearrange, *then* read the protocol off the line, then delete.

And because compiling is mandatory, step 5 has a second door. If the rearrange lands **Death** on
line 2 and Death is already compiled, you compile it again: no progress towards the win, the line is
wiped anyway, and you take the top card of their deck. **Control is the engine that makes second
compiles happen** — without it you would have to walk into one on purpose, and with it the game
hands you one every time you are forced to shuffle a protocol you wanted left alone. That is the
whole cost of holding the component, made concrete.

Both the forced rearrange and the refresh version go on the pile as an `Ask`, exactly like a card's
choice — which is a good sign about the pile, because it was designed for card text and the first
non-card thing to need it fitted without changing it.

### How an optional rule ships

`Rules.Deal` takes a player count and a seed and nothing else, and it should stay that way — a
game's options are not the engine's business and adding a third parameter would touch every table
in the program.

**So it ships as two `Playable`s.** [Rules.fs](../../Engine/Rules.fs) already says why this works:
*"a game is a value here. Two of them can sit side by side in one process."* One flag, baked in at
the point the value is built:

```fsharp
let private offering withControl =
    { Rules = { Deal = deal withControl; ... }
      Name = if withControl then "compile-control" else "compile"
      Title = if withControl then "Compile, with control" else "Compile"
      ... }

let playable = offering false
let withControl = offering true
```

Two lines in [Games.fs](../../Games.fs), everything else shared, and `--help`, the menu, the seat
list, the record and the wire all get the second one for nothing. It also means the *tests* get
both: every check in [compile.fsx](../../../tests/compile.fsx) that does not mention control can be
run over the pair of them in a fold, which is the cheapest possible way to be sure the flag does not
leak.

---

## 5. Building a card must be easy

Stated as a requirement, so here is the test it has to pass:

> **Adding a card touches one file, is one readable line, needs no change to the interpreter, and
> `Faults` and `Words` pick it up without being told.**

Three things make that true, and each is a decision that has to be made now rather than later.

### Card text is data, not functions

A card could carry `Session -> Session`. It must not:

- **`Faults` can read data.** Seventy-two cards typed in by hand is exactly the "game built out of
  data that can be built wrong" the seam exists for.
- **`Words` can read data.** The text on screen is *generated* from the commands, so a card cannot
  say one thing and do another, and all three views get it at once.
- **The machine can read data.** A rival that can see `Delete` costs the opponent a card can weigh
  it; one handed a closure can only try it and see.
- **A closure is not a value the model can hold.** Half the tests here compare whole states.

### The command set is closed early

Adding a card should never mean adding a `Command` case. That is the expensive kind of card, and the
way to have few of them is to close the list at about fifteen cases while the first protocol is
being written, then treat every later case as a design question rather than a chore. Write the two
or three *hardest* cards first — whichever ones you already know bend the rules — because a command
set derived from the easy cards will not survive them.

### A card is one line

Combinators over the selector, so the text reads aloud. All six of Fire, as they came out:

```fsharp
let private fireZero =                                    // "Flip 1 other card. Draw 2 cards."
    { shown [ Flip(Select.any |> Select.other); Draw(Just 2) ] with
        WhenCovered = [ Draw(Just 1); Flip(Select.any |> Select.other) ] }

let private fireOne = shown [ IfYouDo(Discard, [ Delete Select.any ]) ]
let private fireTwo = shown [ IfYouDo(Discard, [ Return Select.any ]) ]
let private fireThree = atEnd [ IfYouDo(May Discard, [ Flip Select.any ]) ]
let private fireFour = shown [ IfYouDo(OneOrMore Discard, [ Draw(HowManyPlus 1) ]) ]
let private theFive = shown [ Discard ]                   // and the 5 of every protocol
```

where the selector is a record with a default and one combinator per field:

```fsharp
let any = { Whose = Anyone; Where = AnyLine; Showing = None; Uncovered = false; Covered = false
            Worth = []; NotThis = false; JustThis = false; Pick = Whichever }

let theirs selector = { selector with Whose = Theirs }
let uncovered selector = { selector with Uncovered = true }
let here selector = { selector with Where = ThisLine }
let faceDown selector = { selector with Showing = Some FaceDown }
```

so `any |> theirs |> uncovered |> here` is a phrase rather than a record literal. The plan was one
file per protocol, six lines each — and one file with a section per protocol is what it turned out
to want, because half the sections are four lines and the sixth of every protocol is the same card.

### What `Faults` should hold the pen on

- every protocol has exactly six cards, none of them twice, going without exactly one of the seven numbers
- no selector asks for more cards than a line can hold
- every `OneOf` has at least two branches, and no branch is empty
- a card of value 0 has some text (a 0 with nothing on it is a card nobody would ever play)
- no card carries a middle command contradicting its own top command
- the total printed value of each protocol is within a point or two of the others

The last is a balance check rather than a correctness one, and it is exactly the sort of thing that
is invisible in a spreadsheet and obvious in a `Faults` line before anybody sits down.

---

## 6. The machine

A win condition makes a real rival possible for the first time. It cannot be solved the way noughts
and crosses is — hands are hidden and the branching is wide — so it is Turncoats' shape: weights on
a handful of things, and a shallow look.

| | why |
| --- | --- |
| protocols compiled | overwhelming — two is nearly the game |
| per line, `min(yours, 10) - min(theirs, 10)` | progress that counts, without rewarding a stack of 19 |
| a line at 10+ and ahead | about to compile: worth nearly a compile |
| cards in hand | tempo — an empty hand is a turn gone |
| uncovered cards with live middle commands | the board working for free |
| holding control | positive under the optional rule, but *less* so the closer you are to compiling |

That last row is the one worth writing a test for: control is a genuine liability when you are one
turn from compiling the protocol you want, and a rival that cannot see that will throw games away in
a way a person would notice immediately.

---

## 7. What the screens gain

Almost all of it is `Render.fs`, and none of it is new machinery:

- **Line totals** at both ends of every line, and a mark on any line at 10 and ahead.
- **Compiled protocols** shown compiled, in the middle of the line where the protocol already sits.
- **The control component**, drawn where it is: the middle, or one player's side.
- **Face-down cards** as a back with a 2 on it, and never as what they are — including on their
  owner's screen, which costs nothing to get right and is easy to get wrong.
- **Card text** for uncovered cards, generated by `Words` from the commands.
- **The pile, when it is not empty**: what is waiting, what is being asked, and a control per card
  that could answer it. This is the screen the game most needs and the one it does not have yet.
- **`Asking` gets a real use.** `answer` currently explains the stage; with card text it becomes
  "what does `fire-3` do?" — which is what that endpoint is for.

---

## 8. Seven steps

Each leaves the tree building, the tests green, and the game playable.

| | | what it gets |
| --- | --- | --- |
| ~~**1**~~ | ~~face-up/face-down, values, line totals~~ — **done** | a real decision every turn |
| ~~**2**~~ | ~~the compile check, the delete, `Won`, the second compile and the reshuffle it drags forward~~ — **done** | **a game that can be won** |
| ~~**3**~~ | ~~refresh, the empty-hand rule, deck recycling~~ — **done** | the second clock, and long games |
| ~~**4**~~ | ~~the pile, `Ask`, `settle`, and `Revealed` — with one test card and nothing else~~ — **done** | the machinery, tested against almost nothing |
| ~~**5**~~ | ~~control, as a second `Playable`~~ — **done** | the first real user of the pile that is not a card |
| ~~**6a**~~ | ~~the `Command` set, the middle and bottom commands, and the text generator~~ — **done** | a card is one line, and adding one changes nothing else |
| **6b** | **the ninety cards** — *waiting on the real text* | the game |
| **7** | the machine, and its three skills | somebody to play it against |

**Steps 1 to 3 are perhaps a day and they are the whole game underneath the cards.** Two players
racing to ten in three lines, with face-down as the release valve and refresh as the tax, is worth
playing on its own — and every one of the ninety cards is then written against something that
already works.

**Step 1 is in.** `Face` and `Placed` in [Cards.fs](Rules/Cards.fs), `Stack.value` and
`Field.allows` in [Field.fs](Rules/Field.fs), `Play of Card * line * Face`, the `NotFacingThere`
refusal that names where a card could have gone instead, and a board that does the arithmetic. Two
things it turned up that were not in this document:

- **A card has exactly one face-up line**, because no protocol is drafted twice. That collapses
  what looked like six controls a card into one `up` and three `down`, and the asymmetry on screen
  is most of what makes the choice legible without reading a rule.
- **A card played face down is hidden from one player and not the other** — the first thing at this
  game that is. The one who played it had it in their hand a moment ago, so their board reads
  `[2] Darkness-1` where the opponent's reads `[2]`, and the notice splits the same way through
  `SeenBy`. What it is *worth* could never be secret, because both players are counting the same
  stack.

**Step 2 is in**, and it went in whole rather than in halves — the second compile came with it,
because compiling is mandatory and a line whose protocol is already turned over is reachable from
the first game anybody plays. `Stack.ToCompile`, `Field.won` and `Field.winning` in
[Field.fs](Rules/Field.fs), a `startOfTurn` cascade in [Turn.fs](Rules/Turn.fs) that runs as the
turn passes, `Side.swept`, `Side.drawnFrom` with the reshuffle, and `Ending.Won`. Three things
worth recording:

- **The conservation invariant had to be restated in this commit**, exactly as this document said
  it would. *Eighteen each, wherever they are* is now false, because a card can cross the table;
  what survives is *thirty-six in total, each in exactly one place*. Both are checked, the weaker
  one over a game where nothing crossed.
- **The taking happens before the sweeping.** Sweeping first would put the loser's cards into their
  discard, and a deck that had run out would shuffle those straight back in and hand one to the
  player who had just deleted it. It is the top of their deck *as it stands*, which is what a table
  would do.
- **A fixture that invents cards cannot check arithmetic.** The first conservation checks were
  written over a line loaded with hand-made cards and quietly counted nothing — the fix was a
  second fixture that loads a line out of the player's own deck. Behaviour and arithmetic want
  different fixtures, and using one for both is how a test passes by not running.

**Step 3 is in, and steps 1 to 3 together are the game.** `Side.drawing` with the restock inside
it, `Side.refreshed`, `Move.Refresh`, and a `MustRefresh` refusal that catches an empty hand
before the less useful truth that no card is in it. Three notes:

- **The forced refresh is a move, not something the game does for you.** Making it automatic was
  the first design, and it recurses: an auto-refresh passes the turn, the next player's turn
  begins, and if their hand is empty too you are one line away from a loop that never returns —
  and a player with no cards *anywhere* would spin forever. Refusing every other move says the
  same thing, ends up in the record where it belongs, and cannot recurse.
- **`drawing` has to stop when the restock brings nothing back.** Every card a player owns can be
  face up on the table, and then the deck, the hand and the discard are all empty at once. That is
  a position rather than a mistake, and the recursion has to know it.
- **The machine games now finish.** The check that used to assert a game ran out of cards now
  asserts somebody *wins*, over four deals — and over twenty deals both seats win some, which is
  the cheapest evidence there is that going first has not quietly become the game.

**Step 4 is in**, and it came out close to this document with four changes worth recording,
three of which are better than what was written here:

- **Card text is a lookup, not a field on the card.** `Card` stays a protocol and a number, and
  `Printed.on card` gives what is written on it. That keeps a card cheap to compare, keeps it the
  thing a hand is a list of, keeps `Card.byName` unchanged, and left every test written before
  this step working. Text is a fact *about* a card, and two cards with the same protocol and
  number could not have different text anyway.
- **`Revealed` is a set of cards, not of places.** A card *is* a place at this game: no protocol
  is drafted twice, so all thirty-six cards in play are distinct and each one names itself. It
  also survives a stack shifting under it, which an index would not.
- **The turn ending is a step on the pile**, at the bottom of it, rather than a flag saying "pass
  when this clears". `EndTurn` and `BeginTurn` are the same mechanism as everything else, so a
  card that stops to ask cannot accidentally hand the turn over, and compiling gets to be a step
  that a forced rearrange can be pushed in front of at step 5.
- **A command with exactly one target does not ask.** The answer could not have been anything
  else, and a prompt with one button on it wastes somebody's turn. Zero targets fizzle, one
  resolves, several ask.

Two cards carry text: Fire-3 is *flip a card, draw a card* — the worked example above — and
Water-0 is *your opponent discards*, which is the case the pile was really built for, because it
stops the game on the player whose turn it is not.

**Step 5 is in, and it was the honest test of the pile it was meant to be.** The forced
rearrangement is an `Ask` no card wrote, and carrying it needed exactly two things the pile did not
already have:

- **`Wanting` splits.** A question was a list of cards; it is now either `ACard of Command * Target
  list` or `AnOrder of Protocol list list`. That was the only change to the pile itself — the loop,
  the look-again, `active` and the refusals were untouched, and the second kind of question rides
  the same rails.
- **Two more steps, `Compiling` and `Refreshing`.** Both actions used to finish inside their own
  function; now each puts *what it was going to do* on the pile underneath the question, so the
  rearrangement can be wedged in front of it. That is the mechanical statement of "compiling is not
  atomic", and it is worth noticing that it made `Turn.refresh` shorter rather than longer.

**`arrange` answers it, and that was free.** The word already existed for laying the protocols out
at the start of a game; a forced rearrangement is the same thing said at a different moment. Same
parser, same record format, no new move. The one place it shows is `Turn.asked`, where a pending
question of the right kind now accepts `Arrange` as an answer rather than as an action.

**Control also settles open question 2 by construction:** two lines winning at once share one
rearrangement, because `BeginTurn` works out every won line, pushes a single `Ask`, and pushes one
`Compiling` step holding all of them.

**Step 6a is in — the machinery of a card, all three thirds of it.** `Ongoing` and the `Ruling`
that asks it, `Text.Middle` and `Text.Bottom`, a `Closing` step for the end of a turn, three more
commands (`Return`, `Shift`, `Refreshing'`), and `Words.printed`, which writes a card's text *out
of what the card does*. Four things worth recording:

- **The generator is the proof, not the convenience.** `Words.printed (card Fire 0)` is
  `"Flip any other card. Draw 2 cards."` — generated. A card cannot say one thing and do
  another, ninety of them cannot drift one at a time, and all three views get the wording
  free. That was the argument for data over functions and it is now load-bearing rather than
  asserted.
- **`Shift` is the command that asks twice**, and it needed a third `Wanting` — `ALine` — plus
  widening `Move.Choose` from a card to a `Chosen`. That is the shape the design should have had
  from the start; retrofitting it after seventy cards had been typed in would have been miserable.
- **Home needs no bookkeeping.** *Home* is derived — `Field.homeOf` searches the two drafted
  lists — so a card taken by a second compile is simply *held* by whoever took it, and `Give` is
  what hands it back. An `Owner` field would have been wrong twice over.
- **The middle command is the expensive third, and the expense is small but scattered.** An
  `Ongoing` has to be *asked* wherever it bites: `Field.valueOn` for `FaceDownWorth`, `Field.barred`
  for the four restrictions. Anything added to `Ongoing` has to find its own place to be asked
  from — which is the one part of this design that does not scale for free.

**And a test-design finding worth keeping**, in three parts, each found the hard way when the real
ninety arrived and the scenery started talking:

- **Scenery has to be scenery by construction.** A fixture that lays a card on the table lays a
  card that *does something*. `quiet` refuses any card with a standing rule, an end command or an
  interrupt; `mute` refuses any card with text at all, and is what a check that turns a card over
  has to use — because turning a card face up is exactly when a middle box speaks.
- **A board is not a history.** These fixtures assert a position, and the game's one trigger is
  *becoming* face up — so without saying so, the first thing the game did with any hand-built
  position was fire the middle box of every card in it. `Resolving.asRead` is the sentence
  *this table has already been read*, and every fixture that lays a card down now ends with it.
- **A card that draws needs a hand with room in it.** Five in hand and two drawn is seven, and the
  check cache phase then stops the turn to trim — the game being right, in the middle of a check
  about something else. `onlyHolding` empties the hand first rather than working around it.

---

## Still to settle

The rules above are yours; these are the edges they leave, in the order they will bite:

1. **Simultaneous reveals.** If one command flips two cards face-up at once, whose text goes on the
   pile first? Built as seating order and then line order — deterministic, and at least the same
   every time — but the right answer is probably that the acting player picks, which is one more
   `Ask`. It cannot arise until a card flips two things at once.
2. **A card with a choice of *effects*** rather than a choice of targets — "delete a card **or**
   draw two". Not built, and it wants a third `Wanting`. Whether a branch that is impossible still
   gets offered is the same question `OneOf` raised.

**Answered by building it:** two lines winning at once share **one** rearrangement, resolved in
line order. `BeginTurn` finds every won line, asks once, and hands them all to a single `Compiling`
step.

## 9. What the ninety cards turned out to need

A `Cards.js` arrived with all of it: **fifteen protocols, six cards each, ninety in total.** The
protocol list is in the tree now — `Dark` was `Darkness`, and `Apathy`, `Hate` and `Love` were
missing. That part was mechanical.

The text was not. Reading it settles two things this document had wrong, and turned up a great
deal the command language could not say. **Nothing was written in against a language that could
not express it** — so the fourteen rows below went in first, each one carrying the real card that
demanded it, and the rest of the ninety follow as transcription.

### The three boxes are two visibility zones

*Settled, and built.* The boxes are stacked on the card, and **a card played over another covers
its middle and bottom and leaves its top showing.** So which box a line is printed in decides
what can silence it:

| box | in play | holds |
| --- | --- | --- |
| **top** | whenever the card is face up — **including while it is covered** | standing rules, and triggers |
| **middle** | fires the moment that box becomes **shown** | the command, on 75 of the 90 |
| **bottom** | only while the card is face up *and* uncovered | standing rules, and timed commands |

**"Shown" is three moments, not one**: the card played face up, the card flipped face up, and the
card **uncovered again** by whatever was over it leaving — returned to a hand, deleted, shifted
away. A card can say its middle piece several times in one game.

That last clause turned out to be a one-line change and a real correction. `Revealed` was the set
of cards *face up*; it is now the set of cards *face up and on top*. Playing over a card takes it
out of the set, and taking the cover away puts it back in — so it fires again, by the same
difference the pile already computed. Nothing else moved.

`Ruling` split the same way: the top box is asked of any face-up card, the bottom box only of an
uncovered one. Which makes `Printed.ongoing` take a `uncovered` flag and is otherwise invisible.

*What follows is the reading that led there, kept because the counts are the evidence.*

Assumption 7 in the first draft of this document was that top / middle / bottom meant *on play /
while uncovered / at end of turn*. It is wrong, and not simply swapped:

| box | what is actually in it | how many |
| --- | --- | --- |
| **top** | standing rules (7) **and** triggered abilities (7) | 14 |
| **middle** | the command that fires when the card is played | 75 |
| **bottom** | triggered abilities (13) **and** standing rules (2) | 15 |

So **middle** is the on-play command — overwhelmingly, 75 of 90. But top and bottom both hold a
mix, and which box a standing rule or a trigger sits in is a fact about the printed card rather
than about when it fires. `Text = { Top; Middle; Bottom }` keyed by position is the wrong shape.

**The text carries its own timing**, and there are eleven of them in the data:

```
Start:                                   When this card would be covered:
End:                                     When this card would be covered or flipped:
After you draw cards:                    When this card would be deleted by compiling:
After you delete cards:
After your opponent discards cards:      (and: no label at all, which is either a standing
After you clear cache:                    rule or the on-play command, by whether it is a
                                          statement or an instruction)
```

Which means the model should be **a list of abilities, each carrying when it applies**:

```fsharp
type When =
    | OnPlay                      // the middle command, and 75 of the 90 have one
    | Always                      // a standing rule
    | AtStart | AtEnd
    | WhenCovered | WhenCoveredOrFlipped | WhenDeletedByCompiling
    | AfterYouDraw | AfterYouDelete | AfterTheyDiscard | AfterYouClearCache

type Text = (When * Ability) list
```

That is a bigger change than it looks, because seven of those eleven are **triggers on things the
game already does** — drawing, deleting, discarding, covering. Each one needs a place in the rules
to fire from, the way `FaceDownWorth` needed `Field.valueOn`. The pile can carry them; what has to be
built is the hooks.

**Nine of the eleven are in**, and the four `After you ...` ones went in as **one hook rather than
four**. The pile already produces an honest record of what a command did — a deletion that fizzled
reported none — so instead of a call-out at every effect, `walk` reads the notices the `Run` just
returned and asks the board who was listening. The only thing the notices cannot say is *who*
deleted, because `Deleted` names the side the card was sitting in; the actor is worked out from the
command and its source, exactly the way `resolve` works it out.

All four are printed in **top** boxes, so they go on listening with something built over them —
which is what *"even if this card is covered"* on Spirit-3 says out loud, and the one place in the
game where being covered does not shut a card up.

`AtStart` is worth a line of its own too, because *when* it fires is
not a detail. It is `AtEnd`'s mirror — one function reading a different field of `Text`, and two
steps, `Opening` and `Closing` — but it goes **first in the turn**, ahead of the control component
and ahead of compiling. A card that deletes at the top of a turn changes what the lines are worth,
which changes who is leading two of them, which changes what compiles. Written the other way round
it would have been a plausible-looking card that silently played a turn late.

### What the language could not say

Every one of these appears in the ninety, and none of it was expressible when this list was
written. **Every row is built**, and the list is kept because what it records is which real card
demanded each shape — a language grown card by card rather than guessed at up front:

| what a card says | example | what it needs |
| --- | --- | --- |
| ~~**optional**~~ **built** | *"You may flip 1 of your face-up covered cards"* | `May`, and a yes-or-no question — 13 cards |
| ~~**conditional follow-up**~~ **built** | *"Discard 1 card. **If you do**, delete 1 card"* | `IfYouDo`, `Session.Did` and a `Gate` step — 7 cards |
| ~~**back-reference**~~ **built** | *"Flip 1 card. Draw cards equal to **that card`s** value"* | `Session.Chose`, the sibling of `Did` |
| ~~**computed counts**~~ **built** | *"Draw cards equal to that card`s value"*, *"the amount discarded plus 1"* | `Count`, which reads `Chose` for one and the `Repeating` tally for the other |
| ~~**open-ended input**~~ **built** | *"Discard 1 or more cards. Draw the amount discarded plus 1."* | `OneOrMore` and a `Repeating` step, and `Did: bool` became `Done: int` |
| ~~**repetition**~~ **built** | *"For every 2 cards in this line, ..."* | `Times of Count * Command`, with `PerCards` counting off the board |
| ~~**superlatives**~~ **built** | *"Delete your **highest value** card"* | `Pick` on the selector: narrows to the extreme, ties survive |
| ~~**value predicates**~~ **built** | *"Delete a card **with a value of 0 or 1**"* | `Worth` on the selector - and it reads what a card is worth *on the table* |
| ~~**whole-line targets**~~ **built** | *"Delete all cards in 1 line"*, *"1 card from each other line"* | `InAChosenLine` and `InEachOtherLine`, which move the command`s source rather than teach the selector a trick |
| ~~**a named destination**~~ **built** | *"Shift 1 face-down card **to this line**"* | `Shift of Selector * Where` — the selector says where a card comes *from* and the `Where` says where it goes, and a shift that says where asks nothing |
| ~~**pointing at that card**~~ **built** | *"Flip 1 card. Shift **that card** to this line"* | `Select.thatCard`, the one narrowing a selector has that the table cannot answer — so `onTable` reads the game rather than the field |
| ~~**either/or**~~ **built** | *"Either discard 1 card **or** flip this card"* | `Either`, a `OneOf` question and two answers of its own. Not `May`: there is no third answer, and a half nobody could carry out is not offered rather than declined |
| ~~**a card asking about itself**~~ **built** | *"**If this card is covering a card**, draw 1 card"* | `IfCovering` — a condition on the board rather than on what a command did, which is what makes it a different thing from `IfYouDo` |
| ~~**a rule a phase asks**~~ **built** | *"Skip your check cache phase"* | `SkipsCacheCheck`, the fifth place a standing rule has to be asked from — and the first that is a *phase* rather than a value or a move |
| ~~**a shift with both ends said**~~ **built** | *"Shift 1 card **either to or from** this line"* | `ToOrFromHere` — the only `Where` that is a rule about the two ends together, so which half applies is settled by where the card you point at happens to be |
| ~~**a question about a line**~~ **built** | *"1 other line **with 8 or more cards**"*, *"each line **where you have a card**"* | `InAChosenLineOf` and `InEachLineHolding`. Every earlier line command took *all* the lines or *all but this one*; these two ask something about a line before pointing a command into it — and Metal-3 is the only card that counts what a line **holds** rather than what it is worth |
| ~~**deck plays**~~ **built** | *"play the top card of your deck face-down in another line"* | `FromDeck`, which lands through the same `laying` a play does |
| ~~**value modifiers**~~ **built** | all three of them | `Field.valueOn seat line field`, and `Side.valueOn` **deleted** so nothing can ask the half-question |
| ~~**restrictions**~~ **mostly built** | *"Your opponent cannot play cards face-down in this line"*, *"You can play cards in any line"* | `Field.barred`, asked by `Turn` and by the machine. *"Ignore all middle commands"* is not built |
| ~~**stopping a compile**~~ **built** | *"Your opponent cannot compile next turn"* | `Session.NoCompile` - the only thing in the game **remembered** rather than read off the board |
| ~~**under, not on**~~ **built** | *"...face-down **under** this card"* | `UnderThis` - the only way a card arrives at the bottom, covering nothing |
| ~~**playing out of the hand**~~ **built** | *"Play 1 card face-down in another line"*, *"Play 1 card"* | `PlayFromHand`, which asks the **line first** so the second half is the ordinary legality question. *"Play 1 card"* with no face named is `Either` of the two |
| ~~**interrupting a flip**~~ **built** | *"When this card would be covered **or flipped**"* | `Turning`, the twin of `Placing` - so covering and flipping are the pair they always were, and a card may speak before either |
| ~~**deleted by compiling**~~ **built** | *"When this card would be deleted by compiling: shift this card"* | an `Escaping` step ahead of `Compiling`, which is the one interrupt that fires on something **no card asked for** |

And four keywords that wanted a command of their own, all four now written: **reveal** (4 cards),
**give** (2), **take** (1), **swap** (1). Three more turned up in the reading and went in the same
way — **rearrange**, which needed `AnOrder` to learn *whose* protocols move because Psychic-2 makes
you reorder *theirs*; **draw the top card of their deck**, which is the second compile's steal said
by a card; and **reveal a card on the table**, which changes nothing and is worth having anyway
because it sets what the rest of the sentence points at.

The good news is what did *not* change: the pile, the look-again and `Session.active` carried every
one of these. The **questions** did grow — `ALine`, `ALineFor`, `Whether` and now `OneOf`, and two
more answers with it — but each one arrived as a case rather than as a mechanism, and the walk down
the pile never learned about any of them. What grew is the vocabulary and the
number of places a standing rule is asked from — which is the cost this document named as the
expensive one, and it came in at the price quoted.

### The cache

*Settled, and built.* **The cache is the hand.** The **check cache phase** looks at its size, and
a hand over five is discarded back down to five by the player it belongs to.

It exists because cards draw. *"Draw 3 cards"* leaves a hand of seven, and this is what takes it
back — which also makes the drawing cards a genuine cost rather than free value, and makes *"Skip
your check cache phase"* a real card.

Built as a `Trimming` step that pushes an ordinary `Discard` and then puts itself back on
underneath, so a hand of seven comes round twice and every card of it is asked for the same way
any other discard is. One number does both jobs: `Deck.HandSize` is what a refresh draws *to* and
what the cache is checked *against*, because a game where those differed would be one where
refreshing could put you over your own limit.

**Where in the turn is an assumption.** It goes after the action and before the end commands, so
that an end command which draws is not undone by the same turn's trimming. Nobody said which way
round; it is one line if it is the other.

## Where step 6b stands

**All four are answered and built** — [the cache is the hand](#the-cache), [the boxes are
visibility zones](#the-three-boxes-are-two-visibility-zones), `You may` / `If you do` below, and
[the interrupt](#the-interrupt). **All ninety are in**, and every one of them says what the real
card says.

**And `Cards.js` is gone**, in the commit that got there. It was the source all ninety were
transcribed from, and it stayed exactly as long as it held cards that existed nowhere else here.
Two copies of the same ninety cards are two things that can disagree, and only one of them is the
one the game plays.

### You may, and if you do

*Settled, and built — and the first two of the real ninety are in the tree because of it.*

> **Fire-1:** *"Discard 1 card. If you do, delete 1 card."*
> **Death-1:** *"You may draw 1 card. If you do, delete 1 other card, then delete this card."*

**`If you do` reads whether the command before it actually did something** — not whether it was
attempted. A discard on an empty hand did not happen, so nothing behind it happens either. And
**declining a `May` stops the whole of the rest**, not the first clause of it: Death-1 is an offer
its owner can sit on and take when it suits them, rather than a card that deletes itself the
moment they say no.

Which needed two things. `Session.Did` — *whether the command that last finished did anything* —
because a command that stops to ask has not done anything **yet**, and the answer may be several
moves away. And a `Gate` step, which is the tail of the *if you do* waiting underneath the command
it depends on: it runs if `Did`, and is thrown away if not. The pile doing the waiting again.

`May` also refuses to make an offer nobody could accept: a *"you may delete a card"* with nothing
to delete fizzles rather than asking somebody to decline the impossible. That is the same rule as
"one target does not ask", said at the other end.

**And "discard 1 or more" has a floor of one** — zero is not more than one, so an open-ended
question is *at least* one, and the count the next command reads is never nought.

### The interrupt

*Settled, and built.* **`When this card would be covered: First, …` resolves before the covering
card lands**, and the card then lands on whatever the interrupting left behind.

> **Apathy-2:** *"When this card would be covered: First, flip this card."*
> Play something over it and it turns itself face down first — so what lands on top lands on a
> two rather than on whatever was printed, and the card underneath has stopped saying anything.

It never *stops* the play, which is the mercy: every one of the five cards that has an interrupt
changes the board and then lets the card through. So no command needs a way to cancel a move, and
`Play` stays what it was.

**The one thing it did need is a move split in half.** A play used to remove the card from the
hand and put it on the line in one step; now it removes it from the hand at once and puts a
`Placing` step on the pile, with the interrupt on top of it. Which means a card is briefly *in the
air* — out of a hand and not yet on the table — and if the interrupt stops to ask, it stays there
until somebody answers. That is exactly what an interrupt is, and the pile carries it without
knowing that.

**And a shift lands the same way.** Covering is covering however the card got there, so
`Resolving.laying` is what both the play and the second half of a shift go through — which was one
line at each call site, and would have been a rule quietly missing from one of them otherwise.

### What a line is worth

*Built, and it was the expensive row exactly as advertised.*

`Stack.value cards` could answer what a stack came to by looking at the cards in it. It cannot any
more: a card in the stack may say what the face-down cards around it are worth, may add to the
total outright or once per face-down card beside it, and **a card in the stack facing it across
the table may take away from it.**

So the question moved up a layer to `Field.valueOn seat line field` — and **`Side.valueOn` was
deleted rather than left alone**, which is the part worth recording. Leaving it would have left a
function that answers the same question wrongly, sitting exactly where somebody would find it. The
compiler then listed every caller: the compile check, the control check, the board, and the tests.
Four places, all of them shallow, and none of them could have been found by reading.

The floor at nothing is an assumption. *"Reduced by 2"* can take a stack below zero and no card
asks what that would mean, so a total is never less than nothing.

### Cards off a deck, and cards that change hands

*Built.* `FromDeck` plays the top card of a deck onto a line — **a card neither player has seen**,
which is what makes it different from every other way a card arrives. Where it goes is a question
when the card does not say, and it lands through the same `laying` a play does, so it sets off any
interrupt waiting under it.

`Give` and `TakeAtRandom` are the pair on one card, and the joke of that card is the asymmetry:
taken at random and given by choice. `TakeAtRandom` is also the only thing in the game that asks
the generator for anything after the deal, which is exactly why it is *random* rather than chosen.

**Love-1 is the card worth looking at**, because every piece of it was built for something else:

```fsharp
{ blank with AtEnd = [ IfYouDo(May Give, [ Draw 2 ]) ] }
```

The end-of-turn box, the offer, the condition behind it, and the giving — four separate pieces of
machinery, and the card is one line. That is the test [this file exists to
pass](Rules/Printed.fs), and it is the first card that passes it without anything being added.

### Cards that shut a line

*Built.* Four cards say what the other player may not do — *"cannot play cards in this line"*,
*"cannot play face-down in this line"*, *"can only play face-down"* — and one says what its own
player **may**: *"You can play cards in any line."*

These are the first standing rules asked when somebody tries to **move** rather than when something
is counted, which is the fourth place in the rules an `Ongoing` has to be remembered. `Field.barred`
answers it, and it is asked *before* the protocol check, because being told a line is shut is more
use than being told the wrong protocol is on it.

**And the machine broke, which was the useful part.** The rival builds its move out of what it
believes is legal; it did not know about `barred`, so it picked shut lines, was refused, and the
game stalled — precisely what the comment in its own code warns about. The fix is that the rival's
list of moves is now filtered through the same `Field.barred` a person's move is checked against.

That is worth stating as a rule of this codebase: **a legality test that only `Turn` consults is
half a test.** Anything that chooses a move has to be able to ask the same question, and the answer
has to come from the same place — which is why `barred` returns a `Barred` rather than a refusal,
because a refusal is what a *player* is told and this is what the *field* answers.

### Saying where, rather than saying which

*Built, and it is the cheapest row in the table.* *"Delete all cards in 1 line"* and *"delete 1
card from each other line"* look like they want selectors that pick a line and then everything in
it. They do not. **They move the command's `Source`.**

A command already carries where it is being said from, because `ThisLine` needs it. So running a
command with its source standing in another line makes every `here` in it mean *that* line, and
nothing about the command or the selector changes at all:

```fsharp
| InEachOtherLine inner ->
    Lines.all
    |> List.filter ((<>) source.Line)
    |> List.map (fun line -> Run(inner, { source with Line = line }))
```

`InAChosenLine` is the same with a question in front of it. Water-1 got shorter as a side effect —
*"play the top card of your deck face-down in each other line"* was two `FromDeck` commands and is
now one, said once and run twice.

### The rule that subtracts, and the one that is remembered

*Both built, and they are the two odd ones out.*

**`Silence`** — *"Ignore all middle commands of cards in this line"* — is the only rule in the game
that **takes something away**. Everything else adds: a value, a restriction, a trigger. This takes
every card in the line its voice, both sides of it, while leaving them standing and counting
exactly as before. It is asked from the one place a card's text is set off, and nowhere else,
because a silence is about what a card *says* rather than what it is worth or whether it can be
deleted.

**`Session.NoCompile`** — *"Your opponent cannot compile next turn"* — is the only thing in this
game that has to be **remembered**. Every other standing rule is a card lying face up somewhere and
stops the moment that card is covered, flipped or deleted; this one outlives the card that said it,
so there is nowhere to read it from but the session. It is spent by the turn it was for, whether or
not there was anything to stop.

That contrast is worth keeping, because it is the only exception to a rule this design has held
everywhere else: **the board is the state.** Nine rules out of ten need no memory because the card
saying them is sitting there to be read.

### A question with no fixed size

*Built, and it was the last row.* *"Discard 1 or more cards"* is the only question in the game that
does not know how big it is: one discard is forced, and then it is offered again for as long as
there is a hand left and the player keeps saying yes.

**And it is why what the game remembers about the last command is a number rather than a yes.**
`Did: bool` became `Done: int` — *"if you do"* reads whether it is more than nought, and *"the
amount discarded"* reads the number itself. One field doing both jobs, because they are the same
question asked to different precision.

The tally rides on the `Repeating` step rather than in the session, so nothing that happens in
between can disturb it — a discard that asks which card sets `Done` to one and the step adds it up.
And the card wraps the whole thing in an *if you do*, so an empty hand does none of it: **nought is
not one or more.**

### What is left

**Every row is built, and all ninety cards are written.** The last two shapes the ninety asked for
were Speed-2`s trigger on being deleted by compiling — the one interrupt that fires on something no
card asked for — and playing a card out of the **hand** as an effect rather than as the turn`s
action, which Darkness-3 and Speed-0 wanted.

And the last of all of it was **Metal-6**`s *"covered **or** flipped"*, which turned covering and
flipping into the pair they always were: two things that happen to a card where it lies, each held
back on the pile so the card can speak first. `Placing` got a twin, `Turning`, and every flip in
the game now goes through it.

**And reading the real ninety cost seven cards their text.** An earlier draft of this file carried
invented ones, written to exercise machinery before the data arrived — a card that counts as
nothing, a card nothing may delete, a command that sends a card home. None of the three is printed
on any of the ninety, so all three came out along with the cards that carried them, and the real
cards that *do* say those things moved into their slots. That is the cost of writing cards before
you have the cards, and it is worth the note: **vocabulary with no card behind it is a rule this
game does not have, and it reads exactly like one it does.**

**The last of the fourteen shapes was the open-ended one:**

> **Fire-4:** *"Discard 1 or more cards. Draw the amount discarded plus 1."*
> **Plague-2:** *"Discard 1 or more cards. Your opponent discards the amount discarded plus 1."*

Two halves, and both are in. *"One or more"* is `OneOrMore`, which runs its command once and then
leaves a `Repeating` step behind to keep asking *another?* until the answer is no or there is
nothing left to answer with. *"The amount discarded"* is `HowManyPlus`, which reads the tally the
`Repeating` step leaves in `Done` — and that is the change that turned `Did: bool` into
`Done: int`, with `> 0` meaning what `Did` used to mean, across `Gate` and every `doneIt`.

Everything else goes in protocol by protocol.

**The arranging is now simultaneous and hidden, and that one is built** — the protocols go down
face down and turn over together, kept back in the log, on the board and in the record. It was the
only open item that touched a file that already existed, which is why it went in rather than into
this list. Everything else here waits for step 4.

Settled by what you have said, and written in above: **compiling is mandatory**; **compiling an
already-compiled protocol takes the top card of their deck**; **a stolen card is yours until an
effect gives it back**, which is `Give` and needs no `Owner` field; **face-up may go on either
protocol facing a line, for every card**; **rearranging moves the protocol cards only, and the
stacks stay where they are**; **flipping a card face-up fires its text**, which makes becoming
face-up the only trigger in the game; and **a card's text is a list of commands rather than one
effect**.
