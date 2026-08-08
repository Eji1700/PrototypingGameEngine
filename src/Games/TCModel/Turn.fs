namespace TCModel.Domain

open TCModel.Engine

/// Something a player does with the game itself, on their turn. Every move is asked for by
/// the player to act, and either the rules allow it or they do not.
type Move =
    | Recruit of color: StoneColor * into: RegionId
    | Battle of color: StoneColor * target: RegionId * driven: Casualties
    | March of color: StoneColor * from: RegionId * into: RegionId * count: int
    | Negotiate
    /// Finish a negotiation by handing a stone back to the reserve.
    | Settle of handBack: StoneColor
    /// Stop playing. The game ends where it stands.
    | Resign

/// What the active player owes before their turn can end. A turn is either open to any
/// action, or waiting on a stone to go back to the reserve - never both.
type Phase =
    | AwaitingAction
    | AwaitingReturn of drawn: StoneColor

/// A game still being played.
type Play =
    {
        Game: Game
        Phase: Phase
        /// Turns spent negotiating, or skipped for want of stones, without a stone being
        /// played in between. The game ends when every player has done so in a row.
        Negotiations: int
        Turn: int
    }

/// A game that has finished. There is no phase and no turn to take.
type Over =
    { Game: Game
      Ending: Ending
      Turn: int }

/// A game is either in play or over, and the two offer different things to do, so no code
/// has to ask whether the game it holds is still running.
///
/// This is where this game stands, whole: the position, whose turn it is, what they owe
/// before it can end, and how far the run of negotiations has got. It is what the engine
/// carries around as a state and never looks inside - the timeline holds these, the record
/// replays into these, and `Rules` below is the only place that knows what one is made of.
type Session =
    | InPlay of Play
    | Finished of Over

/// What this game has to say. The engine wraps these in its own `Told` alongside the things
/// it says itself, so undo and redo and a line nobody could read are not this game's problem.
type Notice =
    | Happened of Event
    | Refused of Rejection

// This game poured into the engine's shapes, and named once.
//
// The three type arguments never vary inside a game - a game has one kind of move, one kind
// of state and one kind of notice, all the way through - so carrying them about in every
// signature above this line would be three words of noise per screen, per table, per test.
// Written down here, everything else goes on saying `Model` and means this one.

type Msg = TCModel.Engine.Msg<Move>

type Told = TCModel.Engine.Told<Move, Notice>

type Entry = TCModel.Engine.Entry<Move, Notice>

type Model = TCModel.Engine.Model<Move, Session, Notice>

module Session =

    let game session =
        match session with
        | InPlay play -> play.Game
        | Finished over -> over.Game

    let turn session =
        match session with
        | InPlay play -> play.Turn
        | Finished over -> over.Turn

    let isOver session =
        match session with
        | Finished _ -> true
        | InPlay _ -> false

/// The flow of a turn: whose it is, when it ends, and when the game does.
///
/// This used to sit in the layer above, and moving it down is what let that layer become an
/// engine: nothing here is true of games in general. A turn that stays open until a stone
/// goes back, a run of negotiations that ends the game, a player with nothing left to play
/// being stepped over - those are this game's rules, said in the same breath as the rest of
/// them rather than one storey up.
module Turn =

    let private finish ending (play: Play) =
        Finished
            { Game = play.Game
              Ending = ending
              Turn = play.Turn }

    /// Close the turn and hand on. `negotiated` says whether the turn was spent negotiating;
    /// playing a stone breaks the run and resets the count. A player holding nothing is
    /// stepped over, which counts as a negotiation in its own right.
    let private endTurn negotiated (play: Play) =
        let rec handOver play events =
            if play.Negotiations >= Game.playerCount play.Game then
                let ending = if Game.allBagsEmpty play.Game then AllPlayedOut else AllNegotiated

                finish ending play, events @ [ GameEnded ending ]
            else
                let play =
                    { play with
                        Game =
                            { play.Game with
                                Table = Table.advance play.Game.Table }
                        Phase = AwaitingAction
                        Turn = play.Turn + 1 }

                let player = Game.active play.Game

                if Player.isEmptyHanded player then
                    handOver
                        { play with
                            Negotiations = play.Negotiations + 1 }
                        (events @ [ TurnSkipped player.Id ])
                else
                    InPlay play, events

        handOver
            { play with
                Negotiations = if negotiated then play.Negotiations + 1 else 0 }
            []

    /// What a move did, followed by the turn changing hands.
    let private thenEndTurn negotiated (play: Play) (game, event) =
        let session, events = endTurn negotiated { play with Game = game }
        session, event :: events

    /// Carry out a move, then close the turn. Negotiating is the exception: it leaves the
    /// turn open until a stone goes back.
    let private carry move (play: Play) =
        match move with
        | Recruit(color, into) -> Actions.recruit color into play.Game |> Result.map (thenEndTurn false play)
        | Battle(color, target, driven) ->
            Actions.battle color target driven play.Game
            |> Result.map (thenEndTurn false play)
        | March(color, from, into, count) ->
            Actions.march color from into count play.Game
            |> Result.map (thenEndTurn false play)
        | Negotiate ->
            Actions.negotiate play.Game
            |> Result.map (fun (game, drawn, event) ->
                InPlay
                    { play with
                        Game = game
                        Phase = AwaitingReturn drawn },
                [ event ])
        | Settle color -> Actions.settle color play.Game |> Result.map (thenEndTurn true play)
        | Resign -> Ok(finish Abandoned play, [ GameEnded Abandoned ])

    /// Whether the move may be asked for at all just now, before the rules of the move itself
    /// have their say. A draw from the reserve must be settled before anything else can
    /// happen; walking away is allowed at any point.
    let private attempt move (play: Play) =
        match move, play.Phase with
        | Resign, _ -> carry move play
        | Settle _, AwaitingReturn _ -> carry move play
        | Settle _, AwaitingAction -> Error NothingToSettle
        | _, AwaitingReturn drawn -> Error(MustSettleFirst drawn)
        | _, AwaitingAction -> carry move play

    /// Ask for a move, in the shape the engine takes an answer in: where the game now stands
    /// if it moved at all, and what there is to say about it either way.
    let asked move session =
        match session with
        // The engine asks nothing of a finished game, so this is only reachable by a game
        // asking itself - and the honest answer is that nothing happened.
        | Finished _ -> None, []
        | InPlay play ->
            match attempt move play with
            | Ok(session, events) -> Some session, events |> List.map Happened
            | Error rejection -> None, [ Refused rejection ]
