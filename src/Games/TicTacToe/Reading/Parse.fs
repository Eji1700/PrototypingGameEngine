namespace Prototyping.TicTacToe

open Prototyping.Engine
open Prototyping.Table

module Parse =

    let private square (word: string) =
        match Commands.tryInt word with
        | Some n -> Ok(Send(Make(Place n)))
        | None -> Error $"'{word}' is not a square. They are numbered 1 to {Squares.Count}."

    let line typed =
        match Commands.lowered typed with
        | [ n ]
        | [ "place"; n ]
        | [ "mark"; n ]
        | [ "p"; n ] -> square n
        | _ -> Error "Say a square's number to take it - '5', or 'place 5'. 'help' has the rest."
