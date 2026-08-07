namespace TCModel.Engine

/// Everything a game can be asked to do, whatever game it is.
///
/// One case is the game's own - a move, whatever a move is there - and the other three are
/// the engine's, and are the same at every table: walk the game's history back, walk it
/// forward, or leave it behind for a fresh deal. A game that wanted to say anything about
/// those would be a game arguing with the machinery rather than using it.
type Msg<'Move> =
    | Make of 'Move
    /// Take the last move back, whoever made it.
    | Undo
    /// Make again the move that was last taken back.
    | Redo
    /// Abandon this game and deal a fresh one. Anything left unsaid is carried over from
    /// the game in progress.
    | Restart of players: int option * seed: uint64 option
