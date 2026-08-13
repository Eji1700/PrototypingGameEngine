namespace TCModel.Compile

open TCModel.Engine

/// Everything a player may ask this game to do.
///
/// Three of them are three different games - taking a protocol, laying the three of them out
/// against the lines, putting a card on a stack - and the fourth is the one every game has.
/// A move made in the wrong stage is refused rather than unreadable: `draft fire` is a fair
/// thing to type, and being told the draft is over is a better answer than being told nobody
/// understood.
type Move =
    /// Take a protocol at the draft.
    | Take of Protocol
    /// Lay your three out against the lines, first for line one.
    | Arrange of Protocol list
    /// A card out of your hand, onto one of your stacks.
    | Play of Card * line: int
    | Resign

type Happening =
    | Drafted of PlayerId * Protocol
    /// All six taken; the protocols are settled and the lines are not.
    | DraftEnded
    | Arranged of PlayerId * Protocol list
    /// Both decks built, shuffled and drawn from.
    | HandsDealt
    | Played of PlayerId * Card * line: int
    | GameEnded of Ending

type Refusal =
    /// A move for a stage the game is not in, carrying the stage it is in - because what
    /// helps a player who drafted at the wrong moment is being told what the game is asking
    /// for now, and only the game knows that.
    | NotNow of Doing
    | AlreadyTaken of Protocol
    | NotDrafted of Protocol
    | NotThree of said: int
    | SaidTwice of Protocol
    | NotInHand of Card
    | NoSuchLine of said: int

/// What this game has to say, and the whole of it. Nothing about undo and nothing about a
/// line nobody could read: those are the engine's, and are said once, above, in words that
/// suit any game.
type Notice =
    | Happened of Happening
    | Refused of Refusal

/// How a turn goes at each of the three stages.
///
/// Total, like every game's: `None` for the position means nothing moved and the notices say
/// why. A refusal is something this game *says*, not something that breaks it, which is what
/// lets every table above be a fold.
module Turn =

    let private walkedAway seat session =
        let ending = Abandoned seat
        Some { session with Stage = Done ending }, [ Happened(GameEnded ending) ]

    // --- the draft --------------------------------------------------------------------------

    let private take protocol session =
        match session.Stage with
        | Drafting pool when not (List.contains protocol pool) -> None, [ Refused(AlreadyTaken protocol) ]

        | Drafting pool ->
            let seat = Session.active session

            let session =
                { session with
                    Field = session.Field |> Field.update seat (Side.drafted protocol)
                    Turn = session.Turn + 1 }

            let taken = Happened(Drafted(seat, protocol))

            // The draft ends on its own count rather than on an empty pool: six of the twelve
            // are taken and the other six are never seen again.
            if Session.picksMade session = Draft.Picks then
                Some { session with Stage = Arranging }, [ taken; Happened DraftEnded ]
            else
                Some
                    { session with
                        Stage = Drafting(pool |> List.filter ((<>) protocol)) },
                [ taken ]

        | Arranging
        | Playing
        | Done _ -> None, [ Refused(NotNow(Session.doing session)) ]

    // --- laying the protocols out against the lines -------------------------------------------

    /// Anything wrong with an order, in the order the sentences are worth saying: the wrong
    /// number of protocols first, because nothing else can be checked until there are three.
    let private objection (side: Side) order =
        if List.length order <> Protocol.Each then
            Some(NotThree(List.length order))
        else
            match order |> List.tryFind (fun protocol -> order |> List.filter ((=) protocol) |> List.length > 1) with
            | Some twice -> Some(SaidTwice twice)
            | None ->
                order
                |> List.tryFind (fun protocol -> not (List.contains protocol side.Drafted))
                |> Option.map NotDrafted

    let private arrange order session =
        match session.Stage with
        | Arranging ->
            let seat = Session.active session

            match objection (Session.side seat session) order with
            | Some refusal -> None, [ Refused refusal ]
            | None ->
                let session =
                    { session with
                        Field = session.Field |> Field.update seat (Side.arranged order)
                        Turn = session.Turn + 1 }

                let laid = Happened(Arranged(seat, order))

                // The second player to lay theirs out is the one who starts the game: there is
                // nothing left to settle, so the decks are built and both hands drawn.
                match Session.arranging session with
                | Some _ -> Some session, [ laid ]
                | None -> Some(Session.dealHands session), [ laid; Happened HandsDealt ]

        | Drafting _
        | Playing
        | Done _ -> None, [ Refused(NotNow(Session.doing session)) ]

    // --- playing a card ----------------------------------------------------------------------

    let private play card line session =
        match session.Stage with
        | Playing ->
            let seat = session.ToPlay
            let side = Session.side seat session

            if not (Lines.holds line) then
                None, [ Refused(NoSuchLine line) ]
            elif not (Side.holds card side) then
                None, [ Refused(NotInHand card) ]
            else
                Some
                    { session with
                        Field = session.Field |> Field.update seat (Side.played card line)
                        ToPlay = Session.other seat
                        Turn = session.Turn + 1 },
                [ Happened(Played(seat, card, line)) ]

        | Drafting _
        | Arranging
        | Done _ -> None, [ Refused(NotNow(Session.doing session)) ]

    /// What the engine asks of a game: a move and where it stands, and the position it left
    /// along with whatever there is to say.
    let asked move session =
        match session.Stage, move with
        // The engine refuses moves after the game is over and says so itself, so this is
        // unreachable rather than wrong. Answered all the same, because a total function is
        // cheaper than an argument about which of two files is guarding it.
        | Done _, _ -> None, []

        | _, Resign -> walkedAway (Session.active session) session
        | _, Take protocol -> take protocol session
        | _, Arrange order -> arrange order session
        | _, Play(card, line) -> play card line session
