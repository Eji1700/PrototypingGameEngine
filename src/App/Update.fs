namespace TCModel.App

open TCModel.Common
open TCModel.Domain

/// The U of MVU: a pure transition from a message and a model to the next model.
/// The domain owns the rules of each action; what lives here is the flow of a turn -
/// whose it is, when it ends, and when the game does.
module Update =

    /// Deal a fresh game. The player count is the only thing that can be wrong, and
    /// the table reports it, so a dealt game always has a legal number of players.
    let private deal players seed =
        match Setup.deal players seed with
        | Ok game ->
            Ok
                { Seed = seed
                  Session =
                    InPlay
                        { Game = game
                          Phase = AwaitingAction
                          Negotiations = 0
                          Turn = 1 }
                  Log = [] }
        | Error problem -> Error problem

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
                let ending =
                    if Game.allBagsEmpty play.Game then AllPlayedOut else AllNegotiated

                finish ending play, events @ [ GameEnded ending ]
            else
                let play =
                    { play with
                        Game = { play.Game with Table = Table.advance play.Game.Table }
                        Phase = AwaitingAction
                        Turn = play.Turn + 1 }

                let player = Game.active play.Game

                if Player.isEmptyHanded player then
                    handOver { play with Negotiations = play.Negotiations + 1 } (events @ [ TurnSkipped player.Id ])
                else
                    InPlay play, events

        handOver
            { play with
                Negotiations = if negotiated then play.Negotiations + 1 else 0 }
            []

    /// What an action did, followed by the turn changing hands.
    let private thenEndTurn negotiated (play: Play) (game, event) =
        let session, events = endTurn negotiated { play with Game = game }
        session, event :: events

    /// Carry out an action, then close the turn. Negotiating is the exception: it
    /// leaves the turn open until a stone goes back.
    let private act action (play: Play) =
        match action with
        | Recruit(color, into) -> Actions.recruit color into play.Game |> Result.map (thenEndTurn false play)
        | Battle(color, target, driven) -> Actions.battle color target driven play.Game |> Result.map (thenEndTurn false play)
        | March(color, from, into, count) ->
            Actions.march color from into count play.Game |> Result.map (thenEndTurn false play)
        | Negotiate ->
            Actions.negotiate play.Game
            |> Result.map (fun (game, drawn, event) ->
                InPlay { play with Game = game; Phase = AwaitingReturn drawn }, [ event ])

    let private settle color (play: Play) =
        Actions.settle color play.Game |> Result.map (thenEndTurn true play)

    /// Apply an outcome: on success the session moves on and the events are recorded,
    /// on refusal the game is untouched and the objection is noted.
    let private apply outcome model =
        match outcome with
        | Ok(session, events) ->
            { model with Session = session }
            |> Model.recordAll (events |> List.map Happened)
        | Error rejection -> model |> Model.record (Refused rejection)

    /// Record something the shell could not make sense of.
    let note text model = model |> Model.record (Misunderstood text)

    let update msg model =
        match msg, model.Session with
        | Restart(players, seed), session ->
            let game = Session.game session
            let players = players |> Option.defaultValue (Game.playerCount game)

            let seed =
                match seed with
                | Some seed -> seed
                | None -> Rng.next game.Rng |> fst

            match deal players seed with
            | Ok dealt -> dealt
            | Error(TooFewPlayers n) -> model |> note $"{n} is too few players."
            | Error(TooManyPlayers n) -> model |> note $"{n} is too many players."

        | _, Finished _ -> model

        | Quit, InPlay play ->
            { model with Session = finish Abandoned play }
            |> Model.record (Happened(GameEnded Abandoned))

        // A draw from the reserve must be settled before the turn can move on.
        | Settle color, InPlay({ Phase = AwaitingReturn _ } as play) -> model |> apply (settle color play)
        | _, InPlay { Phase = AwaitingReturn drawn } -> model |> Model.record (Refused(MustSettleFirst drawn))

        | Act action, InPlay({ Phase = AwaitingAction } as play) -> model |> apply (act action play)
        | Settle _, InPlay { Phase = AwaitingAction } -> model |> Model.record (Refused NothingToSettle)

    /// Deal the first game of a session.
    let start players seed = deal players seed
