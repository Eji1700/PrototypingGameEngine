namespace TCModel.Domain

open TCModel.Common
open TCModel.Engine

/// This game, as the engine takes one - and then the engine with this game already in it.
///
/// The record at the top is the seam, and it is worth reading as the list of what a game has
/// to answer to be playable here: how to deal one, what a move does, whose turn it is, which
/// turn it is on, whether it is over, how many are playing, and where the next deal's seed
/// comes from. Seven questions. Everything else the timeline, the record, the tables, the
/// seats, the machines, the wire and the screens do, they do without ever seeing a stone.
///
/// What follows it is the other half of plugging a game in: the engine's own machinery with
/// those rules bound into it, and the state read back out as what it actually is. That is why
/// nothing above this file mentions the seam at all - the prompt, the three views, the table
/// at one keyboard, the table over a wire and the record on disk all say `Playing.update` and
/// `Playing.game`, exactly as they said `Update.update` and `Model.game` before there was an
/// engine to be on the other side of.
module Playing =

    // --- this game, as the engine takes one ---------------------------------------------

    let private refused =
        function
        | TooFewPlayers n -> $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
        | TooManyPlayers n -> $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."

    /// The first turn of a freshly dealt game: everybody holding their bag, nobody owing a
    /// stone, and no negotiations behind anybody yet.
    let private opening game =
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
          // Out of the game's own generator rather than off the clock, so a game restarted
          // twice from the same record restarts the same way twice.
          Reseed = fun session -> Rng.next (Session.game session).Rng |> fst }

    // --- reading a model that holds one --------------------------------------------------

    let session model = Model.state model

    let game model = Session.game (Model.state model)

    let isOver model = Session.isOver (Model.state model)

    // --- and moving it on ------------------------------------------------------------------

    let update msg model = Update.update rules msg model

    let start players seed = Update.start rules players seed

    let replay players seed moves = Update.replay rules players seed moves
