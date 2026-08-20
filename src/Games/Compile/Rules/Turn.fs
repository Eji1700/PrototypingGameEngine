namespace TCModel.Compile

open TCModel.Engine

type Move =
    | Take of Protocol
    | Arrange of Protocol list
    | Play of Card * line: int * Face
    | Refresh
    | Choose of Chosen
    | Resign

module Turn =

    let private walkedAway seat session =
        let ending = Abandoned seat
        Some { session with Stage = Done ending }, [ Happened(GameEnded ending) ]


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


    let private objection (side: Side) order =
        if List.length order <> Protocol.Each then
            Some(NotThree(List.length order))
        else
            match
                order
                |> List.tryFind (fun protocol -> order |> List.filter ((=) protocol) |> List.length > 1)
            with
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

                match Session.arranging session with
                | Some _ -> Some session, [ laid ]
                | None ->
                    let both =
                        Session.seats |> List.map (fun seat -> seat, (Session.side seat session).Order)

                    Some(Session.dealHands session), [ laid; Happened(Revealed both); Happened HandsDealt ]

        | Drafting _
        | Playing
        | Done _ -> None, [ Refused(NotNow(Session.doing session)) ]


    let private refresh session =
        match session.Stage with
        | Playing ->
            let seat = session.ToPlay

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

            if List.isEmpty side.Hand then
                None, [ Refused MustRefresh ]
            elif not (Lines.holds line) then
                None, [ Refused(NoSuchLine line) ]
            elif not (Side.holds card side) then
                None, [ Refused(NotInHand card) ]
            elif (Field.barred seat line face session.Field).IsSome then
                None, [ Refused(Forbidden((Field.barred seat line face session.Field).Value, line)) ]
            elif face = FaceUp && not (Field.allows seat card line session.Field) then
                None, [ Refused(NotFacingThere(card, line, Field.facingLines seat card session.Field)) ]
            else
                let placed = Placed.laid face card

                let session, told =
                    { session with
                        Field =
                            session.Field
                            |> Field.update seat (fun side ->
                                { side with
                                    Hand = side.Hand |> List.filter ((<>) card) }) }
                    |> Resolving.ending
                    |> Resolving.laying seat placed line None
                    |> fun session -> Resolving.settle session []

                Some session, told

        | Drafting _
        | Arranging
        | Done _ -> None, [ Refused(NotNow(Session.doing session)) ]


    let private answering move session =
        match Session.asking session, move with
        | Some question, Choose chosen -> Resolving.choosing question chosen session
        | Some question, Arrange order -> Resolving.ordering question order session
        | Some question, _ -> None, [ Refused(AnswerFirst question.Wanting) ]
        | None, _ -> None, [ Refused(NotNow(Session.doing session)) ]

    let asked move session =
        match session.Stage, move with
        | Done _, _ -> None, []

        | _, Resign -> walkedAway (Session.active session) session

        | _, move when (Session.asking session).IsSome -> answering move session

        | _, Choose _ -> answering move session
        | _, Take protocol -> take protocol session
        | _, Arrange order -> arrange order session
        | _, Play(card, line, face) -> play card line face session
        | _, Refresh -> refresh session
