namespace TCModel.Turncoats

open TCModel.Common
open TCModel.Engine

module Playing =


    let private refused =
        function
        | TooFewPlayers n -> $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
        | TooManyPlayers n -> $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."

    let opening game =
        InPlay
            { Game = game
              Phase = AwaitingAction
              Negotiations = 0
              Turn = 1 }

    let rules: Rules<Move, Session, Notice> =
        { Deal = fun players seed -> Setup.deal players seed |> Result.map opening |> Result.mapError refused
          Play = Turn.asked
          Active = fun session -> (Game.active (Session.game session)).Id
          Turn = Session.turn
          Over = Session.isOver
          Seats = fun session -> Game.playerCount (Session.game session)
          Reseed = fun session -> Rng.next (Session.game session).Rng |> fst }


    let session model = Model.state model

    let game model = Session.game (Model.state model)

    let isOver model = Session.isOver (Model.state model)


    let update msg model = Update.update rules msg model

    let start players seed = Update.start rules players seed

    let replay players seed moves = Update.replay rules players seed moves
