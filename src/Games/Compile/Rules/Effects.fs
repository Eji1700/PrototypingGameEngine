namespace TCModel.Compile

open TCModel.Engine

/// Whose cards a command is about, from the point of view of whoever is carrying it out - which
/// is not always the player whose card said it. "Your opponent deletes one of theirs" hands the
/// command to the other player, and `Yours` then means theirs.
type Whose =
    | Yours
    | Theirs
    | Anyone

/// Which lines a command can reach.
type Where =
    | ThisLine
    | AnyLine
    /// *"another line"*, *"each other line"* - any but the one the card saying it is in.
    | OtherLines
    /// *"...either to or from this line"* - a destination that depends on where the card started:
    /// out of this line to anywhere, or in from anywhere to here. The only `Where` that is a rule
    /// about the two ends together rather than about one of them.
    | ToOrFromHere

/// A play something on the table forbids, and which of the three it is.
///
/// Its own type rather than three refusals, because a refusal is what a *player* is told and this
/// is what the *field* answers - the machine asks the same question before it chooses, and gets
/// the same answer without any English in it.
type Barred =
    | NoPlayHere
    | NoFaceDownHere
    | OnlyFaceDown

/// When a card points at several and only wants one of them, which one.
type Pick =
    /// Whichever the player likes, which is most of them.
    | Whichever
    | Highest
    | Lowest

/// Which cards on the table a command is about.
///
/// A record with a default and one combinator per field, rather than a case per shape, because
/// what a card points at is always some corner of this and a card should read as a phrase:
/// `any |> theirs |> uncovered` is one line and no new type.
type Selector =
    { Whose: Whose
      Where: Where
      /// Either way up, when it is `None`.
      Showing: Face option
      /// Only the top card of a stack, which is the only one whose text is in play.
      Uncovered: bool
      /// Only cards with something on top of them - *"1 of your face-up covered cards"*, which is
      /// the half of a stack whose text is silent, and therefore the half worth digging out.
      Covered: bool
      /// Only cards worth one of these. Empty means any - and the worth meant is what a card is
      /// worth *on the table*, so a face-down five is a two.
      Worth: int list
      /// *"1 other card"* - anything but the card whose text is talking.
      NotThis: bool
      /// *"Flip this card"* - the card whose text is talking, and nothing else. The opposite
      /// narrowing to `NotThis`, and the only selector that never asks anybody anything.
      JustThis: bool
      /// *"...and shift **that card**"* - whatever the command before this one landed on, and
      /// nothing else. The only narrowing that reads the game rather than the board: everything
      /// else here can be settled by looking at the table, and this one needs to know what just
      /// happened on it.
      ///
      /// It narrows rather than names, so it composes with the rest: *"that card, if it is face
      /// down"* is this and `faceDown` together, and a card that has left the table since is not
      /// among the targets at all - which is "still valid, checked when it resolves" doing the
      /// work rather than a special case.
      WasChosen: bool
      /// Narrowed to the best or worst of what is left. Everything tied for it survives, so a
      /// card asking for the highest of two fives still asks which.
      Pick: Pick }

module Select =

    let any =
        { Whose = Anyone
          Where = AnyLine
          Showing = None
          Uncovered = false
          Covered = false
          Worth = []
          NotThis = false
          JustThis = false
          WasChosen = false
          Pick = Whichever }

    let yours selector = { selector with Whose = Yours }
    let theirs selector = { selector with Whose = Theirs }
    let here selector = { selector with Where = ThisLine }
    let elsewhere selector = { selector with Where = OtherLines }
    let faceDown selector = { selector with Showing = Some FaceDown }
    let faceUp selector = { selector with Showing = Some FaceUp }
    let uncovered selector = { selector with Uncovered = true }
    let covered selector = { selector with Covered = true }
    let worth values selector = { selector with Worth = values }
    let other selector = { selector with NotThis = true }
    let this' selector = { selector with JustThis = true }
    let thatCard selector = { selector with WasChosen = true }
    let highest selector = { selector with Pick = Highest }
    let lowest selector = { selector with Pick = Lowest }

/// How many, when a card does not simply print a number.
///
/// *"Draw cards equal to that card's value"* is the first thing in this game that has to look back
/// at what a previous command did rather than at the board - and the only reason a session
/// remembers which card was last chosen.
type Count =
    | Just of int
    /// What the card the command before this one landed on is worth. Nothing, if there was none.
    | WorthOfChosen
    /// *"The amount discarded plus 1"* - how many the command before this one actually did, and
    /// that many again plus a number. The one count that reads a tally rather than the board.
    | HowManyPlus of int
    /// *"For every 2 cards in this line"* - how many the selector reaches, divided by that and
    /// rounded down. A number counted off the board rather than printed on the card.
    | PerCards of int * Selector

/// One command. A card's text is a *list* of these, and so is what a command sets off.
///
/// The list is the point. "Flip a card. Draw a card." is two things, and between them the game
/// looks at the table again - because flipping a card face up puts that card's own text on the
/// pile, and it resolves before the draw. A tree with a `Then` node could not say that.
///
/// Five to begin with, and the set grows once there are cards that need more. Each one either
/// needs no choosing at all or picks exactly one card, so "delete two" is the command twice -
/// which is also the reading that puts a look-at-the-table between the two deletions.
type Command =
    | Draw of Count
    /// One card out of the hand, chosen by whoever is doing it.
    | Discard
    | Delete of Selector
    | Flip of Selector
    /// A card off the table and back into the hand of whoever it is sitting in front of.
    | Return of Selector
    /// A card to another line on the same side of the table, and the `Where` is the *destination*
    /// rather than what the selector reads: `AnyLine` is a card that may go anywhere and is the
    /// command that asks twice, `ThisLine` is *"shift 1 card **to this line**"* and asks once
    /// because the answer is already printed, and `OtherLines` is *"to another line"*.
    ///
    /// Worth being two things in one command rather than two commands: where a card comes from is
    /// the selector's business and where it goes is not, and a card that says both says them in
    /// different halves of the same sentence.
    | Shift of Selector * Where
    /// The whole hand down and five up, as an effect rather than as an action.
    | Refreshing'
    /// *"Play the top card of your deck face-down in another line."* Not from the hand, and not
    /// the turn's action - a card off the deck, which is a card neither player has seen.
    | FromDeck of Face * Where
    /// *"Play 1 card face-down in another line."* Out of the hand, and **not** the turn's action -
    /// which is the whole value of it: a free card down, on top of whatever you were going to do
    /// anyway.
    ///
    /// It asks twice, and the line goes first: with the line settled, whether a card may go there
    /// is the ordinary legality question, so the second half is the same asking every other
    /// command does. *"Play 1 card"* with no face named is `Either` of the two, which is exactly
    /// what a player choosing which way up to play it is doing.
    | PlayFromHand of Face * Where
    /// *"Give 1 card from your hand to your opponent."* Chosen by the one giving it - and this is
    /// what hands back a card taken off the top of their deck by a second compile, which is the
    /// only place the difference between *holding* a card and it being *yours* is worth anything.
    | Give
    /// *"Take 1 random card from your opponent's hand."* The only place the generator is asked
    /// for anything after the deal, and the reason it is: a card taken at random is a card
    /// neither player chose.
    | TakeAtRandom
    /// *"Your opponent cannot compile next turn."* The only command whose effect outlives the
    /// turn it was made on, and therefore the only one that is **remembered** rather than asked
    /// of the board.
    | StopTheirCompile
    /// *"Reveal 1 card from your hand."* Shown to both players and put straight back - which
    /// needs **no state at all**, because what a reveal leaves behind is knowledge, and knowledge
    /// at this table is the log. The card goes on lying where it was lying.
    | Reveal
    /// *"Your opponent reveals their hand."* The same thing said about all of it.
    | RevealTheirHand
    /// *"Swap the positions of 2 of your protocols."* A rearrangement, but only the three that
    /// one swap can reach - which is what makes it a different card from the one the control
    /// component forces.
    | Swap
    /// *"Rearrange your protocols"*, and *"rearrange **their** protocols"* - which is why this
    /// carries a `Whose`. Either way the player whose card it is does the choosing; what the
    /// `Whose` settles is which side's three actually move.
    ///
    /// Unlike the rearrangement the control component forces, standing pat **is** on offer here:
    /// the component says *a different order* and these cards say *rearrange*.
    | Rearrange of Whose
    /// *"Draw the top card of your opponent's deck."* The second-compile steal, said as a command
    /// - and the only other way a card crosses the table into a hand.
    | TakeTheirTop
    /// *"Reveal 1 face-down card."* A card on the **table** rather than one in hand, and like
    /// every reveal it moves nothing: what it leaves behind is knowledge, and it leaves that in
    /// the log where both players read it. It does set `Chose`, which is what lets the rest of
    /// the sentence say *that card*.
    | Show of Selector
    /// *"Discard 1 or more cards."* One forced, and then offered again for as long as there is
    /// anything left and the player keeps saying yes - leaving how many were done where the next
    /// command can read it.
    | OneOrMore of Command
    /// *"...play the top card of your deck face-down **under** this card."* The only way a card
    /// arrives at the bottom of a stack rather than the top - so it covers nothing, sets off no
    /// interrupt, and is covered by everything already there.
    | UnderThis of Face
    /// *"For every 2 cards in this line, ..."* - the command that many times over. Nought times
    /// is a command that does nothing, which is the ordinary case rather than a mistake.
    | Times of Count * Command
    /// *"...all cards..."* - the command carried out on **every** card it points at, and nobody
    /// asked: there is no choice to be made when the answer is all of them.
    | Every of Command
    /// *"...in 1 line"* - a line is chosen first, and the command then runs as though it had been
    /// printed on a card standing in that line. Which is exactly what it does: the command runs
    /// with its `Source` moved, so a selector saying `here` says the chosen line.
    | InAChosenLine of Command
    /// *"...in each other line"* - the command once per line but this one, the same way.
    | InEachOtherLine of Command
    /// *"...in each line where you have a card"* - the same again, but only where the player
    /// carrying it out has something standing. The first thing that asks a question about a
    /// **line** rather than about a card in one.
    | InEachLineHolding of Command
    /// *"...in 1 other line with 8 or more cards"* - a chosen line, out of the ones deep enough
    /// and not this one. The other question about a line, and the only place a card counts what a
    /// line *holds* rather than what it is worth: a line of eight twos and a line of eight fives
    /// are the same line to this.
    | InAChosenLineOf of atLeast: int * Command
    /// *"You may X."* The player is asked whether, and may say no. A command they could not have
    /// carried out anyway is not offered at all - being asked to decline something impossible is
    /// a prompt that wastes a turn.
    | May of Command
    /// *"X. If you do, Y."* Y happens only if X **actually did something**. A discard with an
    /// empty hand did not happen, so nothing after it does either; and declining a `May` is the
    /// same answer said a different way, which is what lets a card sit on the table until its
    /// owner wants it.
    | IfYouDo of Command * Command list
    /// *"If this card is covering a card, ..."* - a condition on the board rather than on what a
    /// command did, which is what makes it a different thing from `IfYouDo`. Nothing has happened
    /// yet when this is read; it is a question about where the card carrying it is standing.
    ///
    /// A card at the bottom of its stack simply finds nothing to do, the same way a command with
    /// no targets does - so the sentence after it still runs.
    | IfCovering of Command list
    /// *"Either X or Y."* One of the two, and there is no third answer - which is what makes it a
    /// different thing from `May`, where the third answer is *neither*. `May(Either(x, y))` is the
    /// card that offers all three, and two of the ninety say exactly that.
    ///
    /// A branch that could not be carried out is not on offer, so a choice with one live half is
    /// that half done without asking, and a choice with none fizzles.
    | Either of Command * Command
    /// The same command, done by the other player - and `Yours` then means theirs.
    | Opposing of Command

/// What a card does continuously, while it is face up and uncovered.
///
/// The expensive half, and expensive for a reason an `Command` is not: a command runs once and is
/// gone, but one of these has to be *asked* at every point in the rules it touches. `FaceDownWorth`
/// is asked by the value of a stack; `TheyCannotPlayHere` by every play. There are few of those
/// places and they are all in `Field`, but they have to be found once and not forgotten.
///
/// **Every one of these is printed on a real card.** Two that were not - a card that counts as
/// nothing, and a card nothing may delete - came out again when the ninety arrived and neither
/// turned out to be among them. Vocabulary with no card behind it is a rule this game does not
/// have, and it reads as one that it does.
type Ongoing =

    // --- what a whole line is worth ----------------------------------------------------------
    //
    // The expensive ones: what a single card is worth can be answered by looking at that card, and
    // these cannot. A line's total is a fact about **both stacks facing each other**, so asking it
    // means asking the field.

    /// Every face-down card in this stack is worth that instead of the usual two.
    | FaceDownWorth of int
    /// This player's total in this line, increased by that.
    | LinePlus of int
    /// ...and by that again for every face-down card in the same line, which is the one card
    /// whose modifier is counted rather than printed.
    | LinePlusPerFaceDown of int
    /// The *other* player's total in this line, reduced by that. The only standing rule that
    /// reaches across the table.
    | TheirLineMinus of int

    // --- what a player may do -----------------------------------------------------------------
    //
    // The fourth place a standing rule has to be asked from, and the first outside the value of a
    // stack: these are asked when somebody tries to *move*, so they live in `Turn` rather than in
    // `Resolving`. A rule nobody remembered to ask is a rule that does nothing, which is why each
    // of these is named for the question it answers.

    /// The other player may not play into this line at all.
    | TheyCannotPlayHere
    /// ...or may not play into it face down, which leaves them only a card whose protocol is on
    /// it.
    | TheyCannotPlayFaceDownHere
    /// ...or may play face down and nothing else, anywhere on the table.
    | TheyMustPlayFaceDown
    /// And the one that opens a door rather than shutting one: this player may play face up on
    /// any line, protocol or no protocol.
    | YouMayPlayAnywhere

    /// *"Skip your check cache phase."* The one standing rule asked by a **phase** rather than by a
    /// value or by a move: the cache check looks for it before it counts anybody`s hand, and a
    /// player with this standing keeps whatever a card drew them.
    | SkipsCacheCheck

    /// *"Ignore all middle commands of cards in this line."* The only rule that **subtracts**:
    /// everything else here adds something to the game, and this one takes a card's voice away.
    /// A card shown in a silenced line still lands, still counts, and says nothing.
    | Silence

/// What is printed on a card.
///
/// Looked up rather than carried on the card itself, which is the one structural decision here:
/// a `Card` stays a protocol and a number, so it stays cheap to compare, stays the thing a hand
/// is a list of, and stays what a player types. Text is a fact *about* a card, and two cards
/// with the same protocol and number could not have different text anyway.
///
/// What is printed on a card, in the three boxes it is printed in.
///
/// **The boxes are a fact about what can be seen, not about when things happen.** A card played
/// on top of another one covers its middle and bottom and leaves its top showing - so the three
/// boxes are two visibility zones and a trigger:
///
///   * **top** - in play whenever the card is face up, *including while it is covered*.
///   * **middle** - fires the moment that box becomes **shown**, which is three different
///     things: the card played face up, the card flipped face up, and the card *uncovered*
///     again by whatever was over it leaving. A card can say its middle piece more than once.
///   * **bottom** - in play only while the card is face up and uncovered. Covered, it stops.
///
/// Which of them a standing rule or a timed command is printed in is the card's own business
/// and the reason it matters is visibility: a rule in the top box survives being built on, and
/// the same rule in the bottom box does not.
/// What a card listens for.
///
/// Four of these, and every one of them is printed in a **top** box - so unlike the start and end
/// boxes they go on listening after something is played over the card, which is what
/// *"even if this card is covered"* on Spirit-3 is saying out loud.
///
/// They are triggers on things the game already does rather than on anything new, which is why
/// they fire from one place: the pile watches what a command actually reported and asks the board
/// whether anybody was listening.
type Trigger =
    /// *"After you draw cards"* - and drawing nothing is not drawing.
    | YouDraw
    /// *"After you delete cards"*, meaning cards **your commands** deleted. A line wiped by
    /// compiling is not a deletion anybody did, which is why Speed-2 needs a trigger of its own.
    | YouDelete
    /// *"After your opponent discards cards"* - so this one listens for the other seat.
    | TheyDiscard
    /// *"After you clear cache"* - the phase, which happens every turn whether or not the hand was
    /// over its limit.
    | YouClearCache

type Text =
    { /// The top box: a standing rule that a covering card cannot silence.
      Top: Ongoing list
      /// ...and what it listens for, which a covering card cannot silence either.
      After: (Trigger * Command list) list
      /// The middle box: what fires when the box becomes shown.
      Shown: Command list
      /// The bottom box: a standing rule, silenced by anything played over it.
      Bottom: Ongoing list
      /// *"Start: …"* - the first thing a turn does, before the control component is taken and
      /// before anything is compiled. Silenced by a cover like everything else in the box.
      ///
      /// The order is the reason it is its own field rather than the end box read twice: a card
      /// that deletes at the start of a turn changes what the lines are worth, and therefore
      /// changes who is leading two of them and what compiles.
      AtStart: Command list
      /// *"End: …"* - the last thing, after the action and after the cache has been checked.
      AtEnd: Command list

      /// *"When this card would be covered: First, …"* - an **interrupt**. It resolves before the
      /// covering card lands, and the card then lands on whatever the interrupting left behind:
      /// a card that flips itself face down is covered face down, and one that deletes itself is
      /// not covered at all because it is no longer there.
      ///
      /// The only trigger built so far, and the awkward one - it is the only thing in the game
      /// that happens *during* a move rather than before or after one.
      WhenCovered: Command list
      /// *"When this card would be covered **or flipped**: First, ..."* - the same interrupt on the
      /// other thing that can happen to a card where it lies. One card carries both, and it
      /// carries them as two boxes saying the same thing rather than as one box with a set of
      /// triggers, because they fire from two different places in the rules.
      WhenFlipped: Command list
      /// *"When this card would be deleted by compiling: First, ..."* - the other interrupt, and
      /// the only one that fires on something **no card asked for**. Compiling wipes a line, both
      /// players' cards alike; this is the one way out, and it runs before the sweeping so the
      /// card is somewhere else by the time it happens.
      WhenCompiled: Command list }

/// Where a command came from: whose card said it, which card, and which line that card is in.
///
/// Carried for three reasons - the log can name the card that is talking, `ThisLine` needs to
/// know which line that is, and a command can ask whether its own source is still on the table.
type Source =
    { Owner: PlayerId
      /// The card whose text is talking.
      Saying: Card
      Line: int }

/// One card a question could be answered with, and where it is.
type Target =
    | OnTable of PlayerId * line: int * Placed
    | InHand of PlayerId * Card

module Target =

    let card =
        function
        | OnTable(_, _, placed) -> placed.Card
        | InHand(_, card) -> card

    let owner =
        function
        | OnTable(seat, _, _) -> seat
        | InHand(seat, _) -> seat

/// What a question is asking for.
///
/// Two kinds, and the second is the reason the first is not simply a list of cards: the control
/// component makes a player rearrange their protocols, and that is a question no card wrote. If
/// the pile can carry it, the pile is general enough.
type Wanting =
    /// One of these cards, and what will be done with it. Never empty - a command with nothing
    /// to point at fizzles instead of asking.
    | ACard of Command * Target list
    /// One of these orders, and **whose** protocols go into it - which is not always the player
    /// being asked. The control component makes you rearrange your own; a card can make you
    /// rearrange *theirs*, and then the chooser and the side are two different seats.
    | AnOrder of whose: PlayerId * Protocol list list
    /// One of these lines, for the card already picked. The second half of a shift, and the only
    /// place one command asks twice.
    | ALine of moving: Target * to': int list
    /// Yes or no, for a command a card offers rather than insists on.
    | Whether of Command
    /// One of these lines, and then this command runs in it. The other half of *"in 1 line"*, and
    /// the second question that wants a line rather than a card - `ALine` moves a card that is
    /// already picked, and this one picks where a command is going to happen.
    | ALineFor of Command * to': int list
    /// *"Either discard 1 card or flip this card"* - one of two commands, and not a `Whether`:
    /// there is no declining, only choosing which. Asked only when **both** could be done; a card
    /// with an empty hand is not offered the discard, it simply flips.
    | OneOf of Command * Command

/// An answer: a card, a line, a yes or a no, or which of two commands - depending on what was
/// asked.
type Chosen =
    | TheCard of Card
    | TheLine of int
    | Yes
    | No
    /// *"Either ... or ..."*, answered. Which of the two rather than whether, so these are their
    /// own two answers and not `Yes` and `No` wearing a different hat - a record that said *yes*
    /// where a card offered a choice would be a record nobody could read back.
    | TheFirst
    | TheSecond

/// The game stopped, waiting on somebody.
///
/// The chooser is not always the player whose turn it is: "your opponent discards one" stops on
/// them, and until they answer nothing moves. That is the whole reason `Session.active` has to
/// ask the pile before it asks the stage.
type Question =
    { Chooser: PlayerId
      /// The card that is asking, where a card is asking. A rearrangement forced by the control
      /// component is asked by the rules themselves and has no card to name.
      Because: Source option
      Wanting: Wanting }

/// What is waiting to happen, newest first.
///
/// A pile rather than a queue: what a command causes resolves before what was already waiting,
/// which is what makes "flip a card, draw a card" resolve the flipped card's own text before
/// the draw. The two housekeeping steps sit at the bottom of it for the same reason - a turn
/// ends after everything a card set off has finished, and putting that on the pile rather than
/// in a flag means it is the same mechanism doing the waiting.
type Pending =
    | Run of Command * Source
    | Ask of Question
    /// The action is over: hand the turn on.
    | EndTurn
    /// A turn beginning: take the control component if it is owed, and work out what has been
    /// won.
    | BeginTurn
    /// These lines, compiled - *after* any rearrangement the control component forced, which is
    /// the whole reason this is a step of its own rather than something `BeginTurn` finished.
    | Compiling of lines: int list
    /// The hand put down and five taken up - after any rearrangement, for the same reason.
    | Refreshing
    /// *"1 or more"* still going: the command to keep offering, and how many have been done. The
    /// only step that carries a tally, and the only question in the game with no fixed size.
    | Repeating of Command * Source * tally: int
    /// The check cache phase: a hand over its limit discarded back down to it, one card at a
    /// time and chosen by the player it belongs to. A step rather than a calculation, because
    /// every card of it is a question.
    | Trimming
    /// A card laid on a line, held back until whatever it is about to cover has had its say.
    /// The one step that is a *move* half-finished, which is what an interrupt is.
    | Placing of PlayerId * Placed * line: int * from: int option
    /// The tail of an *"if you do"*, waiting under the command it depends on. It runs if that
    /// command did something and is thrown away if it did not - which is the pile doing the
    /// waiting again, because whether a command did anything is not known until it has finished
    /// asking.
    | Gate of Command list * Source
    /// A card about to be turned over, held back until whatever it has to say about that has been
    /// said. `Placing`'s twin: the two things that can happen to a card where it lies, and the two
    /// a card can interrupt.
    | Turning of PlayerId * Placed * line: int
    /// Lines about to be wiped by compiling, and whatever is standing in them that has something
    /// to say about that. On the pile ahead of the `Compiling` step, so a card can get itself out
    /// before the sweeping - which is the only reason it is a step of its own.
    | Escaping of lines: int list
    /// The start of a turn, before anything else in it: the start command of everything this
    /// player has face up and uncovered. `Closing`'s mirror, and it goes first for a reason -
    /// a card that deletes here changes who is leading, and therefore what compiles.
    | Opening
    /// The end of a turn, before it is handed on: the bottom command of everything this player
    /// has face up and uncovered.
    | Closing
