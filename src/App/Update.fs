namespace TCModel.App

open TCModel.Common
open TCModel.Domain

/// The U of MVU: a pure transition from a message and a model to the next model.
/// The domain owns the rules of each move; what lives here is the flow of a turn -
/// whose it is, when it ends, when the game does - and the game's memory of itself.
module Update =

    /// Deal a fresh game. The player count is the only thing that can be wrong, and
    /// the table reports it, so a dealt game always has a legal number of players.
    let private deal players seed =
        Setup.deal players seed
        |> Result.map (fun game ->
            let dealt =
                InPlay
                    { Game = game
                      Phase = AwaitingAction
                      Negotiations = 0
                      Turn = 1 }

            { Timeline = Timeline.ofDeal dealt
              Journal = Journal.ofDeal players seed
              Log = [] })

    let private finish ending (play: Play) =
        Finished
            { Game = play.Game
              Ending = ending
              Turn = play.Turn }

    /// Close the turn and hand on. `negotiated` says whether the turn was spent
    /// negotiating; playing a stone breaks the run and resets the count. A player
    /// holding nothing is stepped over, which counts as a negotiation in its own right.
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

    /// Carry out a move, then close the turn. Negotiating is the exception: it leaves
    /// the turn open until a stone goes back.
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

    /// Whether the move may be asked for at all just now, before the rules of the move
    /// itself have their say. A draw from the reserve must be settled before anything
    /// else can happen; walking away is allowed at any point.
    let private attempt move (play: Play) =
        match move, play.Phase with
        | Resign, _ -> carry move play
        | Settle _, AwaitingReturn _ -> carry move play
        | Settle _, AwaitingAction -> Error NothingToSettle
        | _, AwaitingReturn drawn -> Error(MustSettleFirst drawn)
        | _, AwaitingAction -> carry move play

    /// Ask for a move. Whatever comes of it, the record gains a line: a refusal leaves
    /// the position exactly where it was, but it was still asked for.
    let private make move model =
        let asked = Make move

        match Model.session model with
        | Finished _ -> model |> Model.happen asked [ GameIsOver ] model.Timeline
        | InPlay play ->
            match attempt move play with
            | Ok(session, events) ->
                let timeline = Timeline.advance asked session model.Timeline
                model |> Model.happen asked (events |> List.map Happened) timeline
            | Error rejection -> model |> Model.happen asked [ Refused rejection ] model.Timeline

    /// Step the finger back or forward along the timeline. Either way the move is
    /// written into the record, so the record shows the game as it was really played.
    let private walk asked step nothingThere told model =
        match step model.Timeline with
        | Some(timeline, move) -> model |> Model.happen asked [ told move ] timeline
        | None -> model |> Model.happen asked [ nothingThere ] model.Timeline

    /// Abandon this game and deal another. The old record is left behind whole - it is
    /// the shell's business to have saved it - and the new game starts its own.
    let private restart players seed model =
        let game = Model.game model
        let players = players |> Option.defaultValue (Game.playerCount game)
        let seed = seed |> Option.defaultValue (Rng.next game.Rng |> fst)

        match deal players seed with
        | Ok dealt -> dealt
        | Error(TooFewPlayers n) -> model |> Model.record (Misunderstood $"{n} is too few players.")
        | Error(TooManyPlayers n) -> model |> Model.record (Misunderstood $"{n} is too many players.")

    let update msg model =
        match msg with
        | Make move -> make move model
        | Undo -> walk Undo Timeline.undo NothingToTakeBack TookBack model
        | Redo -> walk Redo Timeline.redo NothingToMakeAgain MadeAgain model
        | Restart(players, seed) -> restart players seed model

    /// Record something the shell could not make sense of. It never became a move, so
    /// it stays out of the record and merely goes up on screen.
    let note text model =
        model |> Model.record (Misunderstood text)

    /// Deal the first game of a session.
    let start players seed = deal players seed

    /// Play a whole game again from its deal. Because `update` is pure and the
    /// generator travels inside the game, folding the recorded moves over a fresh deal
    /// arrives at exactly the state they were recorded from - undone moves and all.
    let replay players seed moves =
        deal players seed
        |> Result.map (fun model -> moves |> List.fold (fun model msg -> update msg model) model)
