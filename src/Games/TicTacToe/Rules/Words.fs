namespace TCModel.TicTacToe

open TCModel.Engine

/// Putting the game into English. The rules report what happened in their own terms;
/// everything a player actually reads is written here.
module Words =

    /// The one character a mark is drawn as, which is also what a seat is called: at this
    /// game the two really are the same thing, and pretending otherwise would leave a board
    /// full of Xs beside a heading about Player 1.
    let mark =
        function
        | Cross -> "X"
        | Nought -> "O"

    let named =
        function
        | Cross -> "Crosses"
        | Nought -> "Noughts"

    let player playerId = mark (Session.markAt playerId)

    /// A seat as one screen names it, with the reader's own marked. Every view does this
    /// and the game is unreadable without it over a network, where the seat to play is very
    /// often not the seat reading.
    let seated yours playerId =
        player playerId + (if yours then " (you)" else "")

    let square (n: int) = string n

    let ending =
        function
        | Won(winner, line) ->
            let squares = line |> List.map square |> String.concat ", "
            $"{named winner} has three in a row - {squares}"
        | Drawn -> "the board is full and neither has three in a row"
        | Abandoned who -> $"{named who} walked away"

    let event =
        function
        | Placed(who, where) -> $"{mark who} takes square {where}."
        | GameEnded e -> $"The game is over: {ending e}."

    let rejection =
        function
        | NoSuchSquare said -> $"There is no square {said}. They are numbered 1 to {Squares.Side * Squares.Side}."
        | AlreadyTaken(where, taken) -> $"Square {where} already has {mark taken} in it."

    /// A message written the way a player types it. The record is kept in the same words
    /// the prompt takes, so a game can be read back and played again without a second
    /// language standing between the two.
    /// Only this game's own moves are written here. `undo`, `redo` and `restart` are the
    /// engine's words and are written once, by the engine, in `Msg.written`.
    let command =
        Msg.written (function
            | Place square' -> $"place {square'}"
            | Resign -> "resign")

    /// What this game itself said, and the whole of what it has to say for itself.
    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    /// The same, as much of it as one seat may know - which here is all of it. Everything at
    /// this game is on the table, which is exactly why it is worth having beside one where
    /// it is not: nothing above this line has to be told that there is nothing to hide.
    let saidTo _ notice = said notice
