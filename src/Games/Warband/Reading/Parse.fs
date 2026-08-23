namespace TCModel.Warband

open TCModel.Engine
open TCModel.Table

module Parse =

    let private hex (word: string) =
        match Formation.read word with
        | Some hex -> Ok hex
        | None ->
            Error
                $"'{word}' is not a hex. They are a rank and a step across it - 'f1' to 'f3' at the front, 'm1' to 'm4' in the middle, 'b1' to 'b3' at the back."

    let private kind (word: string) =
        match Kinds.byName word with
        | Some kind -> Ok kind
        | None -> Error $"'{word}' is not a kind of unit. There is {Kinds.names}."

    let private mustering what where =
        match kind what, hex where with
        | Ok kind, Ok hex -> Ok(Send(Make(Muster(kind, hex))))
        | Error problem, _
        | _, Error problem -> Error problem

    /// Every line this game reads. The commands every game shares - undo, help, save, quit and the
    /// rest - are read before this is reached, so nothing about them belongs here.
    ///
    /// The two-word muster is last, since it would otherwise swallow 'why f1' and every other pair
    /// of words the game answers.
    let line typed =
        match Commands.lowered typed with
        | [ "run" ]
        | [ "p" ] -> Ok(Send(Make(Running None)))
        | [ "start" ]
        | [ "go" ] -> Ok(Send(Make(Running(Some true))))
        | [ "stop" ]
        | [ "pause" ]
        | [ "halt" ] -> Ok(Send(Make(Running(Some false))))

        // The clock's own move, spelt out. Here for the console that cannot press anything - a
        // game piped in from a file, or a record replaying - so a battle watched and one folded
        // out by hand are the same game and the same record.
        | [ "beat" ] -> Ok(Send(Make Beat))
        | [ "step" ]
        | [ "s" ] -> Ok(Send(Make Step))

        | [ "why"; what ]
        | [ "ask"; what ] -> Ok(Asking what)

        | [ "muster"; what; where ]
        | [ what; where ] -> mustering what where

        | _ ->
            Error "Say a kind and a hex to muster one - 'bowman b2' - or 'why bowman' to hear what one does. 'help' has the rest."
