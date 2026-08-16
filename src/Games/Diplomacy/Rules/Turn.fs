namespace TCModel.Diplomacy

/// Everything a player may ask this game to do.
///
/// Five things, and only one of them is an order. That is the shape of this game rather than
/// an accident of the seam: a power writes as many orders as it has units, changes its mind
/// about any of them, and then says it is finished - and until every power has said so,
/// nothing on the board has moved.
///
/// Nothing here says *who* is asking. The engine hands a move to whoever `Active` said was to
/// play, and every table above it holds the other seats to that, so a move that named its own
/// author would be a second opinion about a thing already settled.
type Move =
    /// Write an order for the piece in that province, replacing whatever was written for it.
    | Give of at: ProvinceId * Instruction
    /// Take that order back, leaving the piece with none.
    | Take of at: ProvinceId
    /// These orders are final. When the last power says it, the phase resolves.
    | Commit
    /// A word to one power, or to the table. The one move that changes nothing on the board -
    /// and the one this game could least do without.
    | Whisper of who: Power option * text: string
    /// Walk away. The power's units stand where they are and are taken off as they are pushed
    /// out, which is what these rules call civil disorder. A game of seven does not end
    /// because one of them left.
    | Resign

/// What the game did.
type Happening =
    /// An order written. Only the power that wrote it may read what it says, which is the
    /// whole of what makes this game the game it is.
    | Wrote of Power * ProvinceId * Instruction
    | Erased of Power * ProvinceId
    | Ready of Power
    /// A phase resolved, with everything that came of it.
    | Passed of Passing
    /// And the phase that follows, so that a player reading down the log knows what they are
    /// being asked for.
    | Opened of Stage * year: int
    /// Said to one power, or to everybody. `None` for the hearer means everybody.
    | Whispered of Power * Power option * string
    | WalkedAway of Power
    | GameEnded of Ending

type Refusal =
    /// An order the piece could not carry out, and why.
    | Rejected of ProvinceId * Fault
    | NothingWritten of ProvinceId
    | AlreadyFinished of Power
    /// More builds or removals written than the centres came to.
    | ThatIsEnough of Power * owed: int
    | TalkingToYourself

/// What this game has to say, and the whole of it.
type Notice =
    | Happened of Happening
    | Refused of Refusal

/// How a turn goes.
///
/// Which at this game is not a turn at all. Everybody moves at once, so what a "turn" is here
/// is one power writing down what it intends and sealing it; the board only changes when the
/// seventh of them does. That the seats come round one at a time is the engine's shape, and it
/// costs nothing, because nobody may read anybody else's orders until they are all in - which
/// is exactly the guarantee writing them at the same table in the same room is supposed to
/// give.
module Turn =

    /// The power whose move this is. `Active` already said, and this is the same answer read
    /// off the same place, so there is no second opinion to keep in step.
    let private acting play =
        match Session.awaited play with
        | power :: _ -> Some power
        | [] -> None

    /// Everything this power has written this phase. Worked out from the board rather than
    /// kept alongside the orders, so there is no way for an order to belong to one power in
    /// the map and another in the tally.
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

    /// One order, checked against the phase the game is in. The three phases take three
    /// different sets of orders and each of them refuses the others by name, so a player who
    /// writes a perfectly good movement order in a winter is told which it is rather than that
    /// it made no sense.
    let private checking power play (order: Order) =
        match play.Stage with
        | Moving _ -> Orders.forMovement play.Board power order

        | Falling _ ->
            match play.Beaten |> List.tryFind (fun beaten -> beaten.From = order.At) with
            | None -> Error(NotDislodged order.At)
            | Some beaten when beaten.Piece.Power <> power -> Error(NotYours(order.At, beaten.Piece.Power))
            | Some beaten -> Orders.forRetreat beaten.Options order

        | Building -> Orders.forWinter play.Board power (Session.owed power play.Board) order

    /// Whether this power has already written as many builds or removals as its centres allow.
    /// Nothing stops it changing its mind about which - a build taken back frees the place up
    /// again, which is why this counts what stands rather than what has ever been asked for.
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

    /// Everything a phase resolving has to say: what each of the phases it walked through came
    /// to, and then either how the game ended or what is being asked for next.
    let private toldOf session passings =
        (passings |> List.map (Passed >> Happened))
        @ (match session with
           | Finished(_, ending) -> [ Happened(GameEnded ending) ]
           | InPlay play -> [ Happened(Opened(play.Stage, play.Year)) ])

    /// What the engine asks of a game: a move and where it stands, and the position it left
    /// along with whatever there is to say.
    ///
    /// Total, like every game's. A refusal is a thing this game *says* - `None` for the
    /// position and a notice giving the reason - and never something the machinery has to
    /// handle, which is what lets every table above it be a fold.
    let asked move session =
        match session with
        // The engine refuses moves after the game is over and says so itself, so this is
        // unreachable rather than wrong.
        | Finished _ -> None, []
        | InPlay play ->

        match acting play with
        // Nobody is owed a question and the game is not over, which cannot happen: a phase
        // with nobody in it is walked through as it is entered. Answered all the same.
        | None -> None, []
        | Some power ->

        match move with
        | Whisper(Some heard, _) when heard = power -> None, [ Refused TalkingToYourself ]

        | Whisper(heard, text) ->
            // The board does not move, and the record still gains a line. What was said is a
            // fact about the game whether or not it changed a province - and at this game
            // more often than not it is the only thing that did.
            Some(InPlay play), [ Happened(Whispered(power, heard, text)) ]

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
