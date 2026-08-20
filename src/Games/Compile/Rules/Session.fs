namespace TCModel.Compile

open TCModel.Common
open TCModel.Engine

type Ending =
    | Won of PlayerId
    | Abandoned of PlayerId

type Control =
    | NotInPlay
    | InTheMiddle
    | HeldBy of PlayerId

type Stage =
    | Drafting of pool: Protocol list
    | Arranging
    | Playing
    | Done of Ending

type Doing =
    | TheDraft
    | TheProtocols
    | ThePlay
    | AChoice
    | Nothing

type Session =
    { Stage: Stage
      Field: Field
      ToPlay: PlayerId
      Turn: int

      Control: Control

      Pile: Pending list

      Revealed: Set<Card>

      NoCompile: PlayerId option

      Chose: Card option

      Done: int

      Rng: Rng }

module Session =

    [<Literal>]
    let Seats = 2

    let seats = [ for place in 1..Seats -> Seat.at place ]

    let other seat =
        if seat = Seat.at 1 then Seat.at 2 else Seat.at 1

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

    let withControl session = session.Control <> NotInPlay

    let asking session =
        match session.Pile with
        | Ask question :: _ -> Some question
        | _ -> None

    let side seat session = Field.side seat session.Field

    let picksMade session =
        seats |> List.sumBy (fun seat -> (side seat session).Drafted |> List.length)

    let arranging session =
        seats |> List.tryFind (fun seat -> (side seat session).Order |> List.isEmpty)

    /// Whose turn it is to say something - which during a card's resolution is whoever the card is
    /// asking, not whoever is playing. A question stops the game wherever it stands, including in
    /// the middle of the other player's turn.
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

    let reseed session = fst (Rng.next session.Rng)

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
