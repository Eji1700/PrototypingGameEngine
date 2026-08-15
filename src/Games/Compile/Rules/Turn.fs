namespace TCModel.Compile

open TCModel.Engine

/// Everything a player may ask this game to do.
///
/// Three of them are three different games - taking a protocol, laying the three of them out
/// against the lines, putting a card on a stack - and the rest are what a turn is made of. A
/// move made in the wrong stage is refused rather than unreadable: `draft fire` is a fair thing
/// to type, and being told the draft is over is a better answer than being told nobody
/// understood.
type Move =
    /// Take a protocol at the draft.
    | Take of Protocol
    /// Lay your three out against the lines, first for line one.
    | Arrange of Protocol list
    /// A card out of your hand, onto one of your stacks, which way up.
    | Play of Card * line: int * Face
    /// The whole hand away and five fresh ones up - instead of playing, not as well as.
    | Refresh
    /// An answer to whatever the game has stopped to ask - a card, or a line. Not an action:
    /// the turn is already under way, and this is what lets it carry on.
    | Choose of Chosen
    | Resign

/// How a turn goes at each of the stages.
///
/// Total, like every game's: `None` for the position means nothing moved and the notices say
/// why. A refusal is something this game *says*, not something that breaks it, which is what
/// lets every table above be a fold.
///
/// Short, and it should be. What a move sets off is `Resolving`'s, and what a card says is
/// `Printed`'s; what is here is only which moves the rules will take and when.
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

                // The second player to lay theirs out is the one who ends the settling: both are
                // turned over at once, the decks are built, and both hands are drawn.
                //
                // Turned over *at once* is the whole reason the seats come round one at a time
                // and the game is still the game two people would play across a table. Neither
                // chose knowing the other's, so which of them was asked first does not matter.
                match Session.arranging session with
                | Some _ -> Some session, [ laid ]
                | None ->
                    let both =
                        Session.seats
                        |> List.map (fun seat -> seat, (Session.side seat session).Order)

                    Some(Session.dealHands session), [ laid; Happened(Revealed both); Happened HandsDealt ]

        | Drafting _
        | Playing
        | Done _ -> None, [ Refused(NotNow(Session.doing session)) ]

    // --- the two actions -----------------------------------------------------------------------

    /// The whole hand down, five up, and that was the turn.
    ///
    /// The second clock of the game. Five cards is five turns of tempo, and the turn spent
    /// getting five more is a turn the other player spends getting closer to ten - which is what
    /// makes a hand of cards you cannot use a real problem rather than an inconvenience.
    let private refresh session =
        match session.Stage with
        | Playing ->
            let seat = session.ToPlay

            // The turn ends at the bottom of the pile, the refreshing sits on that, and - if the
            // control component is being held - a rearrangement nobody asked for sits on top of
            // both. Then the pile is settled, and it stops on the rearrangement if there is one.
            let session, told = session |> Resolving.ending |> Resolving.refreshing seat

            let session, more = Resolving.settle session told
            Some session, more

        | Drafting _
        | Arranging
        | Done _ -> None, [ Refused(NotNow(Session.doing session)) ]

    let private play card line face session =
        match session.Stage with
        | Playing ->
            let seat = session.ToPlay
            let side = Session.side seat session

            // Asked first, because with nothing in hand every card is a card not in hand and
            // that is the least useful of the true things the game could say.
            if List.isEmpty side.Hand then
                None, [ Refused MustRefresh ]
            elif not (Lines.holds line) then
                None, [ Refused(NoSuchLine line) ]
            elif not (Side.holds card side) then
                None, [ Refused(NotInHand card) ]
            // What something on the table forbids, asked before what the protocols allow: being
            // told a line is shut is more use than being told the wrong protocol is on it.
            elif (Field.barred seat line face session.Field).IsSome then
                None, [ Refused(Forbidden((Field.barred seat line face session.Field).Value, line)) ]
            // Face down goes anywhere, which is the whole of what makes a hand of unplayable
            // cards still a hand. Face up has to meet a protocol - unless something of theirs
            // says it does not.
            elif face = FaceUp && not (Field.allows seat card line session.Field) then
                None, [ Refused(NotFacingThere(card, line, Field.facingLines seat card session.Field)) ]
            else
                let placed = { Card = card; Face = face }

                // The card leaves the hand at once and lands in its own good time: whatever it is
                // about to cover has the right to say something first, and until that has been
                // said the card is in the air. The end of the turn goes on the bottom of the pile
                // under all of it, so everything the card sets off happens before the turn is
                // handed on.
                let session, told =
                    { session with
                        Field =
                            session.Field
                            |> Field.update seat (fun side -> { side with Hand = side.Hand |> List.filter ((<>) card) }) }
                    |> Resolving.ending
                    |> Resolving.laying seat placed line None
                    |> fun session -> Resolving.settle session []

                Some session, told

        | Drafting _
        | Arranging
        | Done _ -> None, [ Refused(NotNow(Session.doing session)) ]

    // --- answering whatever has stopped the game ---------------------------------------------
    //
    // Two kinds of question and two words for them, and the second word is one the game already
    // had: `arrange` lays the protocols out at the start of a game and answers the rearrangement
    // the control component forces. It is the same thing said at two moments, so it reads the
    // same, writes the same into a record, and needed nothing new from the parser.

    let private answering move session =
        match Session.asking session, move with
        | Some question, Choose chosen -> Resolving.choosing question chosen session
        | Some question, Arrange order -> Resolving.ordering question order session
        | Some question, _ -> None, [ Refused(AnswerFirst question.Wanting) ]
        | None, _ -> None, [ Refused(NotNow(Session.doing session)) ]

    /// What the engine asks of a game: a move and where it stands, and the position it left
    /// along with whatever there is to say.
    let asked move session =
        match session.Stage, move with
        // The engine refuses moves after the game is over and says so itself, so this is
        // unreachable rather than wrong. Answered all the same, because a total function is
        // cheaper than an argument about which of two files is guarding it.
        | Done _, _ -> None, []

        | _, Resign -> walkedAway (Session.active session) session

        // A question on the pile stops everything. Answering it is the only move that carries,
        // and the refusal says what is being asked - which is the only thing that will move the
        // game on.
        | _, move when (Session.asking session).IsSome -> answering move session

        | _, Choose _ -> answering move session
        | _, Take protocol -> take protocol session
        | _, Arrange order -> arrange order session
        | _, Play(card, line, face) -> play card line face session
        | _, Refresh -> refresh session
