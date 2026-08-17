namespace TCModel.TicTacToe

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.TicTacToe

/// A typed line as this game's own move.
///
/// Short, and it should be: `undo`, `save`, `view rich`, `resign`, `quit` and `restart` mean
/// the same thing whatever is on the board and have already been read, once, for every game
/// there is. What is left here is the words this game invented, which is a number.
module Parse =

    /// A square, in the words a player would say it in. Refused where it was typed rather
    /// than turned into a move nobody asked for: `place seven` is a fair thing to try.
    let private square (word: string) =
        match Commands.tryInt word with
        | Some n -> Ok(Send(Make(Place n)))
        | None -> Error $"'{word}' is not a square. They are numbered 1 to {Squares.Side * Squares.Side}."

    /// The whole of what this game reads.
    ///
    /// A bare number is a move, because on a board where there is only one thing to do,
    /// naming the square *is* saying what to do - and it is what everybody types anyway.
    /// The long way round is kept because it is what a record is written in, and a record
    /// that read as a column of bare digits would be a record nobody could skim.
    let line typed =
        match Commands.lowered typed with
        | [ n ] -> square n
        | [ "place"; n ]
        | [ "mark"; n ]
        | [ "p"; n ] -> square n
        | _ -> Error "Say a square's number to take it - '5', or 'place 5'. 'help' has the rest."
