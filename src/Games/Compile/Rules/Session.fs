namespace TCModel.Compile

open TCModel.Common
open TCModel.Engine

/// How a game finished.
///
/// One way, so far. What a player is trying to *do* has not been said yet, so the only
/// ending this game knows is somebody putting it down - and saying that plainly is better
/// than inventing a win nobody asked for.
type Ending =
    /// Somebody walked away, and which seat.
    | Abandoned of PlayerId

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
    let dealt (seed: uint64) =
        { Stage = Drafting Protocol.all
          Field = Field.ofSeats seats
          ToPlay = Seat.at 1
          Turn = 1
          Rng = Rng.ofSeed seed }

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
    let active session =
        match session.Stage with
        | Drafting _ -> Draft.picking (picksMade session) |> Option.defaultValue session.ToPlay
        | Arranging -> arranging session |> Option.defaultValue session.ToPlay
        | Playing
        | Done _ -> session.ToPlay

    let doing session =
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
                    Field.withSide seat ({ side with Deck = deck } |> Side.drew Deck.HandSize) field, rng)
                (session.Field, session.Rng)

        { session with
            Field = field
            Rng = rng
            Stage = Playing
            ToPlay = Seat.at 1 }
