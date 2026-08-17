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

module Msg =

    /// A message as the line a player would have typed for it, given the game's own words
    /// for its own moves.
    ///
    /// Which is what a record is made of, so the three cases the engine owns are written
    /// here for the same reason the type has them. A line is read back through `Commands`,
    /// which takes `undo`, `redo` and `restart` before a game sees it at all - so a game
    /// spelling them out for itself is a game that could spell them differently from the
    /// only reader that will ever have to take them back. Six games had written the same
    /// six lines, and one of them wrong would have been a record that replayed as a
    /// different game.
    let written move msg =
        match msg with
        | Make move' -> move move'
        | Undo -> "undo"
        | Redo -> "redo"
        | Restart(None, None) -> "restart"
        | Restart(None, Some seed) -> $"restart {seed}"
        | Restart(Some players, None) -> $"players {players}"
        | Restart(Some players, Some seed) -> $"players {players} {seed}"
