namespace Prototyping.TicTacToe

open Prototyping.Engine

module Words =

    let mark =
        function
        | Cross -> "X"
        | Nought -> "O"

    let named =
        function
        | Cross -> "Crosses"
        | Nought -> "Noughts"

    let player playerId = mark (Session.markAt playerId)

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
        | NoSuchSquare said -> $"There is no square {said}. They are numbered 1 to {Squares.Count}."
        | AlreadyTaken(where, taken) -> $"Square {where} already has {mark taken} in it."

    let command =
        Msg.written (function
            | Place square' -> $"place {square'}"
            | Resign -> "resign")

    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    let saidTo _ notice = said notice
