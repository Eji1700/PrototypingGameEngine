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
      Uncovered: bool }

module Select =

    let any =
        { Whose = Anyone
          Where = AnyLine
          Showing = None
          Uncovered = false }

    let yours selector = { selector with Whose = Yours }
    let theirs selector = { selector with Whose = Theirs }
    let here selector = { selector with Where = ThisLine }
    let faceDown selector = { selector with Showing = Some FaceDown }
    let faceUp selector = { selector with Showing = Some FaceUp }
    let uncovered selector = { selector with Uncovered = true }

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
    | Draw of int
    /// One card out of the hand, chosen by whoever is doing it.
    | Discard
    | Delete of Selector
    | Flip of Selector
    /// A card off the table and back into the hand of whoever it is sitting in front of.
    | Return of Selector
    /// A card to another line on the same side of the table. The only command that asks twice -
    /// which card, and then where - because there is no line it obviously goes to.
    | Shift of Selector
    /// A card back to the player whose protocol it belongs to. What undoes a card taken off the
    /// top of somebody's deck by a second compile, and the only place the difference between
    /// *holding* a card and it being *yours* is worth anything.
    | Rehome of Selector
    /// The whole hand down and five up, as an effect rather than as an action.
    | Refreshing'
    /// *"You may X."* The player is asked whether, and may say no. A command they could not have
    /// carried out anyway is not offered at all - being asked to decline something impossible is
    /// a prompt that wastes a turn.
    | May of Command
    /// *"X. If you do, Y."* Y happens only if X **actually did something**. A discard with an
    /// empty hand did not happen, so nothing after it does either; and declining a `May` is the
    /// same answer said a different way, which is what lets a card sit on the table until its
    /// owner wants it.
    | IfYouDo of Command * Command list
    /// The same command, done by the other player - and `Yours` then means theirs.
    | Opposing of Command

/// What a card does continuously, while it is face up and uncovered.
///
/// The expensive half, and expensive for a reason an `Command` is not: a command runs once and is
/// gone, but one of these has to be *asked* at every point in the rules it touches. `CountsAs` is
/// asked by the value of a stack; `Unbreakable` by every deletion. There are few of those places
/// and they are all in `Field` and `Resolving`, but they have to be found once and not forgotten.
type Ongoing =
    /// This card is worth that, whatever is printed on it and whichever way up it is lying.
    | CountsAs of int
    /// And nothing may delete it.
    | Unbreakable

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
type Text =
    { /// The top box: a standing rule that a covering card cannot silence.
      Top: Ongoing list
      /// The middle box: what fires when the box becomes shown.
      Shown: Command list
      /// The bottom box: a standing rule, silenced by anything played over it.
      Bottom: Ongoing list
      /// ...and its end-of-turn command, silenced the same way.
      AtEnd: Command list

      /// *"When this card would be covered: First, …"* - an **interrupt**. It resolves before the
      /// covering card lands, and the card then lands on whatever the interrupting left behind:
      /// a card that flips itself face down is covered face down, and one that deletes itself is
      /// not covered at all because it is no longer there.
      ///
      /// The only trigger built so far, and the awkward one - it is the only thing in the game
      /// that happens *during* a move rather than before or after one.
      WhenCovered: Command list }

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
    /// One of these orders. Every arrangement of that player's protocols *except* the one they
    /// are in now, because the component forces a different one and standing pat is not on offer.
    | AnOrder of Protocol list list
    /// One of these lines, for the card already picked. The second half of a shift, and the only
    /// place one command asks twice.
    | ALine of moving: Target * to': int list
    /// Yes or no, for a command a card offers rather than insists on.
    | Whether of Command

/// An answer, which is a card, a line, or a yes or a no, depending on what was asked.
type Chosen =
    | TheCard of Card
    | TheLine of int
    | Yes
    | No

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
    /// The check cache phase: a hand over its limit discarded back down to it, one card at a
    /// time and chosen by the player it belongs to. A step rather than a calculation, because
    /// every card of it is a question.
    | Trimming
    /// A card laid on a line, held back until whatever it is about to cover has had its say.
    /// The one step that is a *move* half-finished, which is what an interrupt is.
    | Placing of PlayerId * Placed * line: int
    /// The tail of an *"if you do"*, waiting under the command it depends on. It runs if that
    /// command did something and is thrown away if it did not - which is the pile doing the
    /// waiting again, because whether a command did anything is not known until it has finished
    /// asking.
    | Gate of Command list * Source
    /// The end of a turn, before it is handed on: the bottom command of everything this player
    /// has face up and uncovered.
    | Closing
