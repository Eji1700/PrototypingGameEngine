namespace Prototyping.TicTacToe

open Prototyping.Engine
open Prototyping.Table
open Prototyping.TicTacToe

module Parse =

    let private square (word: string) =
        match Commands.tryInt word with
        | Some n -> Ok(Send(Make(Place n)))
        | None -> Error $"'{word}' is not a square. They are numbered 1 to {Squares.Side * Squares.Side}."

    let line typed =
        match Commands.lowered typed with
        | [ n ] -> square n
        | [ "place"; n ]
        | [ "mark"; n ]
        | [ "p"; n ] -> square n
        | _ -> Error "Say a square's number to take it - '5', or 'place 5'. 'help' has the rest."
