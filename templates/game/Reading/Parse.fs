namespace Prototyping.MyGame

open Prototyping.Engine
open Prototyping.Table

module Parse =

    let private taking word =
        match Commands.tryInt word with
        | Some count -> Ok(Send(Make(Take count)))
        | None -> Error $"'{word}' is not a number of tokens. Say 'take 2', or just '2'."

    /// Every line this game reads. The commands every game shares - undo, help, save, quit and the
    /// rest - are read before this is reached, so nothing about them belongs here.
    let line typed =
        match Commands.lowered typed with
        | [ "take"; n ]
        | [ "t"; n ] -> taking n
        | [ one ] -> taking one
        | _ -> Error $"Say how many to take - '2', or 'take 2'. 'help' has the rest."
