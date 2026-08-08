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

module Told =

    /// Anything a table has been told, in words.
    ///
    /// The game's own notices go to the game, through `says`; the rest are the engine
    /// talking, and those sentences are here because they are the same whatever is being
    /// played. A game that had to write its own "there is nothing left to take back" would
    /// be a game with an opinion about undo, and two games saying it differently would be
    /// two games disagreeing about a thing neither of them owns.
    ///
    /// `write` is how a move is put back into the words a player would have typed, which is
    /// the only thing about a game the sentences below need. Nothing here is a `Rules` or a
    /// table: a game's own renderer can call this, and does.
    let inWords says write =
        function
        | Said notice -> says notice
        | TookBack msg -> $"Taken back: {write msg}."
        | MadeAgain msg -> $"Made again: {write msg}."
        | NothingToTakeBack -> "There is nothing left to take back - this is the deal itself."
        | NothingToMakeAgain -> "There is nothing to make again."
        | GameIsOver -> "The game is over, so there is nothing left to play. Take a move back to look at it again, or restart."
        | Misunderstood text -> text
