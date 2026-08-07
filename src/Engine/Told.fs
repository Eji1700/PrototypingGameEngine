namespace TCModel.Engine

/// What the game said back.
///
/// Everything a player is ever told passes through here, and it comes from two places. The
/// game says what happened in its own words - `Said`, carrying whatever a game calls the
/// things it has to say - and the engine says the rest: that a move was taken back, that
/// there was nothing to take back, that the game had already finished, that a line made no
/// sense at all.
///
/// Split that way because the engine cannot invent a game's notices and a game should not
/// have to invent the engine's. A game that had to write its own `NothingToTakeBack` would
/// be a game with an opinion about undo, and undo is not its business.
type Told<'Move, 'Notice> =
    /// What the game itself said, in whatever it says things in.
    | Said of 'Notice
    | TookBack of Msg<'Move>
    | MadeAgain of Msg<'Move>
    | NothingToTakeBack
    | NothingToMakeAgain
    /// A move was asked for after the game had finished.
    | GameIsOver
    /// Something that never became a move at all: a line nobody could read, or a deal
    /// nobody could make.
    | Misunderstood of string
