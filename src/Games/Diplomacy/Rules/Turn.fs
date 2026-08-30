namespace Prototyping.Diplomacy

type Move =
    | Give of at: ProvinceId * Instruction
    | Take of at: ProvinceId
    | Commit
    | Whisper of who: Power option * text: string
    | Resign

// An order written or taken back carries the stage it was written in, because what another power
// may be told of it turns on that and a notice is read with nothing else to hand.
type Happening =
    | Wrote of Power * ProvinceId * Instruction * during: Stage
    | Erased of Power * ProvinceId * during: Stage
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

    let private checking power play (order: Order) =
        match play.Stage with
        | Moving _ -> Orders.forMovement play.Board power order

        | Falling _ ->
            match play.Beaten |> List.tryFind (fun beaten -> beaten.From = order.At) with
            | None -> Error(NotDislodged order.At)
            | Some beaten when beaten.Piece.Power <> power -> Error(NotYours(order.At, beaten.Piece.Power))
            | Some beaten -> Orders.forRetreat beaten.Options order

        | Building -> Orders.forWinter play.Board power (Session.owed power play.Board) order

    // Whether a winter has room for one more build or removal. What is already written at this
    // province does not count, since the new order replaces it: a build written again is still the
    // one build, as a move written again is still the one move.
    let private roomForMore power play at (says: Instruction) =
        let owing = Session.owed power play.Board

        let others =
            Session.writtenBy power play |> List.filter (fst >> (<>) at) |> List.map snd

        match says with
        | Builds _ ->
            let already =
                others
                |> List.filter (function
                    | Builds _ -> true
                    | _ -> false)

            List.length already < owing
        | Disbands when play.Stage = Building -> List.length (others |> List.filter ((=) Disbands)) < -owing
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
            match Session.writtenBy power play |> List.tryFind (fst >> (=) at) with
            | None -> None, [ Refused(NothingWritten at) ]
            | Some _ ->
                Some(
                    InPlay
                        { play with
                            Written = Map.remove at play.Written }
                ),
                [ Happened(Erased(power, at, play.Stage)) ]

        | Commit ->
            if Set.contains power play.Sealed then
                None, [ Refused(AlreadyFinished power) ]
            else
                let session, passings = Session.seal power play
                Some session, Happened(Ready power) :: toldOf session passings

        | Give(at, says) ->
            match checking power play { At = at; Says = says } with
            | Error fault -> None, [ Refused(Rejected(at, fault)) ]
            | Ok settled when not (roomForMore power play at settled) ->
                None, [ Refused(ThatIsEnough(power, Session.owed power play.Board)) ]
            | Ok settled ->
                Some(
                    InPlay
                        { play with
                            Written = Map.add at settled play.Written }
                ),
                [ Happened(Wrote(power, at, settled, play.Stage)) ]
