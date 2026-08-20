namespace TCModel.Diplomacy

type Move =
    | Give of at: ProvinceId * Instruction
    | Take of at: ProvinceId
    | Commit
    | Whisper of who: Power option * text: string
    | Resign

type Happening =
    | Wrote of Power * ProvinceId * Instruction
    | Erased of Power * ProvinceId
    | Ready of Power
    | Passed of Passing
    | Opened of Stage * year: int
    | Whispered of Power * Power option * string
    | WalkedAway of Power
    | GameEnded of Ending

type Refusal =
    | Rejected of ProvinceId * Fault
    | NothingWritten of ProvinceId
    | AlreadyFinished of Power
    | ThatIsEnough of Power * owed: int
    | TalkingToYourself

type Notice =
    | Happened of Happening
    | Refused of Refusal

module Turn =

    let private acting play =
        match Session.awaited play with
        | power :: _ -> Some power
        | [] -> None

    let private written power play =
        play.Written
        |> Map.toList
        |> List.filter (fun (province, says) ->
            match says with
            | Builds _ -> Position.ownerOf province play.Board = Some power
            | _ ->
                Position.at province play.Board |> Option.map (fun piece -> piece.Power) = Some power
                || play.Beaten
                   |> List.exists (fun beaten -> beaten.From = province && beaten.Piece.Power = power))

    let private checking power play (order: Order) =
        match play.Stage with
        | Moving _ -> Orders.forMovement play.Board power order

        | Falling _ ->
            match play.Beaten |> List.tryFind (fun beaten -> beaten.From = order.At) with
            | None -> Error(NotDislodged order.At)
            | Some beaten when beaten.Piece.Power <> power -> Error(NotYours(order.At, beaten.Piece.Power))
            | Some beaten -> Orders.forRetreat beaten.Options order

        | Building -> Orders.forWinter play.Board power (Session.owed power play.Board) order

    let private roomForMore power play (says: Instruction) =
        let owing = Session.owed power play.Board
        let mine = written power play

        match says with
        | Builds _ ->
            let already =
                mine
                |> List.filter (fun (_, says) ->
                    match says with
                    | Builds _ -> true
                    | _ -> false)

            already |> List.length < owing
        | Disbands when play.Stage = Building ->
            let already =
                mine
                |> List.filter (fun (_, says) ->
                    match says with
                    | Disbands -> true
                    | _ -> false)

            already |> List.length < -owing
        | _ -> true

    let private toldOf session passings =
        (passings |> List.map (Passed >> Happened))
        @ (match session with
           | Finished(_, ending) -> [ Happened(GameEnded ending) ]
           | InPlay play -> [ Happened(Opened(play.Stage, play.Year)) ])

    let asked move session =
        match session with
        | Finished _ -> None, []
        | InPlay play ->

        match acting play with
        | None -> None, []
        | Some power ->

        match move with
        | Whisper(Some heard, _) when heard = power -> None, [ Refused TalkingToYourself ]

        | Whisper(heard, text) -> Some(InPlay play), [ Happened(Whispered(power, heard, text)) ]

        | Resign ->
            let session, passings = Session.walkAway power play
            Some session, Happened(WalkedAway power) :: toldOf session passings

        | Take at ->
            match Map.tryFind at play.Written with
            | None -> None, [ Refused(NothingWritten at) ]
            | Some _ when written power play |> List.exists (fst >> (=) at) |> not -> None, [ Refused(NothingWritten at) ]
            | Some _ ->
                Some(
                    InPlay
                        { play with
                            Written = Map.remove at play.Written }
                ),
                [ Happened(Erased(power, at)) ]

        | Commit ->
            if Set.contains power play.Sealed then
                None, [ Refused(AlreadyFinished power) ]
            else
                let session, passings = Session.seal power play
                Some session, Happened(Ready power) :: toldOf session passings

        | Give(at, says) ->
            match checking power play { At = at; Says = says } with
            | Error fault -> None, [ Refused(Rejected(at, fault)) ]
            | Ok settled when not (roomForMore power play settled) ->
                None, [ Refused(ThatIsEnough(power, Session.owed power play.Board)) ]
            | Ok settled ->
                Some(
                    InPlay
                        { play with
                            Written = Map.add at settled play.Written }
                ),
                [ Happened(Wrote(power, at, settled)) ]
