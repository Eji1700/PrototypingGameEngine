namespace Prototyping.Cascade

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

module Parse =

    // A cell off the board reads as a cell all the same, and it is the rules that refuse it - so
    // the refusal is written down, as Life's is, rather than lost at the prompt.
    let private cell (word: string) =
        match Board.read word with
        | Some cell -> Ok cell
        | None -> Error(Grid.unnamed Board.grid word)

    let private touching word =
        cell word |> Result.map (fun cell -> Send(Make(Touch cell)))

    let private asking word =
        cell word |> Result.map (fun _ -> Asking word)

    let line typed =
        match Commands.lowered typed with
        | [ "up" ]
        | [ "w" ] -> Ok(Send(Make(Point North)))
        | [ "down" ]
        | [ "s" ] -> Ok(Send(Make(Point South)))
        | [ "left" ]
        | [ "a" ] -> Ok(Send(Make(Point West)))
        | [ "right" ]
        | [ "d" ] -> Ok(Send(Make(Point East)))
        | [ "press" ]
        | [ "touch" ] -> Ok(Send(Make Press))
        | [ "beat" ]
        | [ "tick" ] -> Ok(Send(Make Beat))
        | Notch.Winds winding -> winding |> Result.map (fun winding -> Send(Make(Wind winding)))
        | [ "touch"; c ]
        | [ "t"; c ] -> touching c
        | [ "why"; c ]
        | [ "ask"; c ] -> asking c
        | [ one ] -> touching one
        | _ -> Error "Say a cell to set it turning - 'f7'. 'why f7' says what it would reach. 'help' has the rest."
