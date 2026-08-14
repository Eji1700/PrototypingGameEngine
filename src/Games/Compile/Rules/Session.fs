namespace TCModel.Compile

open TCModel.Common
open TCModel.Engine

/// How a game finished.
type Ending =
    /// All three of their protocols compiled, which is the whole object of the game.
    | Won of PlayerId
    /// Somebody walked away, and which seat.
    | Abandoned of PlayerId

/// The control component: whether the optional rule is being played at all, and if it is, where
/// the component is sitting.
///
/// One field answers both questions, which is why it is a type rather than a `bool` and a
/// `PlayerId option` that could disagree. `NotInPlay` never changes, and the game it describes is
/// the game without the rule.
type Control =
    | NotInPlay
    /// Nobody has led two lanes yet, so it sits between the players.
    | InTheMiddle
    | HeldBy of PlayerId

/// Where in the game it is, and therefore what kind of move may be made.
///
/// A game of this is three games in a row: one where protocols are taken, one where they are
/// laid out against the lines, and one where cards are played. They have different moves and
/// a different sense of whose turn it is, which is exactly what a stage is for - and it is
/// the game's own, because a phase that runs until six picks are made is nobody else's rule.
type Stage =
    /// Choosing protocols, 1-2-2-1, out of what is left of the twelve.
    | Drafting of pool: Protocol list
    /// Setting which protocol faces which line. Who is still to do it is read off the field,
    /// so there is nothing to carry here.
    | Arranging
    /// Cards, from the hand, onto the stacks.
    | Playing
    | Done of Ending

/// Which of the three things the game is asking for, and nothing about what is left in the
/// pool or what is on the table.
///
/// A stage said small enough to travel in a refusal. A player who typed a card at the draft
/// wants to be told what the game *is* asking for, and telling them that means a refusal has
/// to carry the stage - but a refusal carrying the whole of one would carry the pool and the
/// ending with it, which is a notice quoting the position back at itself.
type Doing =
    | TheDraft
    | TheProtocols
    | ThePlay
    /// Mid-effect: a card has stopped to ask somebody something, and nothing else can happen
    /// until it is answered.
    | AChoice
    | Nothing

/// Where the game stands, which is the one thing the engine ever asks about.
///
/// The field is here from the first pick rather than from the deal, empty and filling: a
/// player's protocols are their side of it before their deck is, and a stage that had to
/// carry the draft's results and then hand them on would be two places a protocol could be.
type Session =
    { Stage: Stage
      Field: Field
      /// Whose turn it is once there is play to be had. Before then it is worked out from
      /// how far the draft or the arranging has got, which is what `active` is for.
      ToPlay: PlayerId
      Turn: int

      /// Where the control component is, and whether there is one at all.
      Control: Control

      /// What is waiting to happen, newest first. Empty almost always, and the whole of what
      /// "the game is mid-effect" means.
      Pile: Pending list

      /// Every card face up on the table when the pile was last looked at.
      ///
      /// A set of cards rather than of places, because a card *is* a place here: no protocol is
      /// drafted twice, so all thirty-six cards in a game are distinct and one of them names
      /// itself. It also survives a stack shifting under it, which an index would not.
      ///
      /// What is in here and no longer face up drops out; what is face up and not in here has
      /// just been turned over, and its text goes on the pile. The difference is the only
      /// trigger this game has.
      Revealed: Set<Card>

      /// Who may not compile when their turn next begins.
      ///
      /// The one thing in this game that is **remembered** rather than asked of the board. Every
      /// other standing rule is a card lying face up somewhere, and stops the moment that card is
      /// covered, flipped or deleted; this one was said once and outlives the card that said it,
      /// so there is nowhere to read it from but here.
      NoCompile: PlayerId option

      /// The card the command that last finished landed on, which is what *"that card"* means.
      ///
      /// Kept beside `Did` and for the same reason: a command that stopped to ask has not landed
      /// on anything until the answer comes back, and the command reading it may be several moves
      /// away by then.
      Chose: Card option

      /// How many things the command that last finished actually did.
      ///
      /// A number rather than a yes: *"if you do"* reads whether it is more than nought, and
      /// *"the amount discarded"* reads the number itself. It has to be kept rather than worked
      /// out, because a command that stops to ask has not finished doing anything until the
      /// answer comes back - which may be several moves later.
      Done: int

      /// The shuffle, and whatever else is drawn later. It travels in the state so that a
      /// game is a value and a seed is a whole game.
      Rng: Rng }

module Session =

    /// Two, and exactly two. They sit opposite each other, which is what a line running
    /// across the table means.
    [<Literal>]
    let Seats = 2

    let seats = [ for place in 1..Seats -> Seat.at place ]

    /// The other seat. Two players, so this is total.
    let other seat =
        if seat = Seat.at 1 then Seat.at 2 else Seat.at 1

    /// A fresh game: twelve protocols on the table, both sides empty, and nothing dealt. The
    /// shuffle is not done here - there is nothing to shuffle until the draft says what a
    /// deck is made of.
    ///
    /// Whether the control component is in play is settled here and never again, because it is
    /// settled by *which game this is*: the two are two `Playable`s built from one function, and
    /// the flag is baked in where the value is.
    let dealt control (seed: uint64) =
        { Stage = Drafting Protocol.all
          Field = Field.ofSeats seats
          ToPlay = Seat.at 1
          Turn = 1
          Control = control
          Pile = []
          Revealed = Set.empty
          NoCompile = None
          Chose = None
          Done = 0
          Rng = Rng.ofSeed seed }

    let holdsControl seat session = session.Control = HeldBy seat

    /// Whether this is the game with the component in it at all.
    let withControl session = session.Control <> NotInPlay

    /// What the game is waiting to be told, if it is waiting on anything.
    let asking session =
        match session.Pile with
        | Ask question :: _ -> Some question
        | _ -> None

    let side seat session = Field.side seat session.Field

    /// How many protocols have been taken between them, which is how far the draft has got.
    /// Counted off the field rather than kept, so it cannot disagree with what is on it.
    let picksMade session =
        seats |> List.sumBy (fun seat -> (side seat session).Drafted |> List.length)

    /// The first seat that has not laid its protocols out yet, in seating order.
    let arranging session =
        seats |> List.tryFind (fun seat -> (side seat session).Order |> List.isEmpty)

    /// Whose turn it is. Three different questions under one name, which is the point of it
    /// being asked of the game: the draft has an order of its own, the arranging goes round
    /// once, and play alternates.
    /// Whose turn it is. Four different questions under one name, which is the point of it being
    /// asked of the game rather than worked out above it: a card that has stopped to ask
    /// somebody something outranks everything, and the somebody is very often not the player
    /// whose turn it is. Under that, the draft has an order of its own, the arranging goes round
    /// once, and play alternates.
    let active session =
        match asking session with
        | Some question -> question.Chooser
        | None ->

        match session.Stage with
        | Drafting _ -> Draft.picking (picksMade session) |> Option.defaultValue session.ToPlay
        | Arranging -> arranging session |> Option.defaultValue session.ToPlay
        | Playing
        | Done _ -> session.ToPlay

    let doing session =
        match asking session with
        | Some _ -> AChoice
        | None ->

        match session.Stage with
        | Drafting _ -> TheDraft
        | Arranging -> TheProtocols
        | Playing -> ThePlay
        | Done _ -> Nothing

    let turn session = session.Turn

    let isOver session =
        match session.Stage with
        | Done _ -> true
        | Drafting _
        | Arranging
        | Playing -> false

    let ending session =
        match session.Stage with
        | Done ending -> Some ending
        | Drafting _
        | Arranging
        | Playing -> None

    /// A seed for the next game, drawn out of this one rather than off the clock - so a game
    /// restarted from a record restarts the same way twice.
    let reseed session = fst (Rng.next session.Rng)

    /// Build both decks out of the protocols each player arranged, shuffle them, and draw an
    /// opening hand. This is the moment the game stops being about protocols and starts being
    /// about cards, and it happens once.
    let dealHands session =
        let field, rng =
            seats
            |> List.fold
                (fun (field, rng) seat ->
                    let side = Field.side seat field
                    let deck, rng = Deck.shuffled (Deck.ofProtocols side.Order) rng
                    let side, rng = Side.drawing Deck.HandSize { side with Deck = deck } rng
                    Field.withSide seat side field, rng)
                (session.Field, session.Rng)

        { session with
            Field = field
            Rng = rng
            Stage = Playing
            ToPlay = Seat.at 1 }
