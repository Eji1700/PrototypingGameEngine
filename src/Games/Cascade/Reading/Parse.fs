namespace TCModel.Cascade

open TCModel.Engine
open TCModel.Table
open TCModel.Cascade

module Parse =

    let private cell (word: string) =
        match Board.read word with
        | Some cell when Board.holds cell -> Ok cell
        | Some _
        | None ->
            Error
                $"'{word}' is not a cell. They are named by column and row - 'f7' is column f, row 7, and they run to {Board.letters[Board.Width - 1]}{Board.Height}."

    let private touching word =
        cell word |> Result.map (fun cell -> Send(Make(Touch cell)))

    let private asking word =
        cell word |> Result.map (fun _ -> Asking word)

    let line typed =
        match Commands.lowered typed with
        | [ "beat" ]
        | [ "tick" ] -> Ok(Send(Make Beat))
        | [ "faster" ]
        | [ "quicker" ]
        | [ "+" ] -> Ok(Send(Make Faster))
        | [ "slower" ]
        | [ "-" ] -> Ok(Send(Make Slower))
        | [ "speed"; notch ] ->
            match Commands.tryInt notch with
            | Some notch -> Ok(Send(Make(Speed notch)))
            | None -> Error $"'{notch}' is not a speed. They run from {Session.Slowest} to {Session.Fastest}."
        | [ "touch"; c ]
        | [ "t"; c ] -> touching c
        | [ "why"; c ]
        | [ "ask"; c ] -> asking c
        | [ one ] -> touching one
        | _ -> Error "Say a cell to set it turning - 'f7'. 'why f7' says what it would reach. 'help' has the rest."
