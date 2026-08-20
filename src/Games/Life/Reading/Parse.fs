namespace TCModel.Life

open TCModel.Engine
open TCModel.Table
open TCModel.Life

module Parse =

    let private cell (word: string) =
        match Grid.read word with
        | Some cell -> Ok cell
        | None -> Error $"'{word}' is not a cell. They are named by column and row - 'f7' is column f, row 7."

    let private toggling word =
        cell word |> Result.map (fun cell -> Send(Make(Toggle cell)))

    let private asking word =
        cell word |> Result.map (fun _ -> Asking word)

    let private running (word: string) =
        match Commands.tryInt word with
        | Some generations -> Ok(Send(Make(Step generations)))
        | None -> Error $"'{word}' is not a number of generations. Say 'step 10', or 'step' for one."

    let line typed =
        match Commands.lowered typed with
        | [ "step" ]
        | [ "s" ]
        | [ "run" ] -> Ok(Send(Make(Step 1)))
        | [ "step"; n ]
        | [ "s"; n ]
        | [ "run"; n ] -> running n
        | [ "clear" ] -> Ok(Send(Make Clear))
        | [ "toggle"; c ]
        | [ "t"; c ] -> toggling c
        | [ "why"; c ]
        | [ "ask"; c ] -> asking c
        | [ one ] when (Commands.tryInt one).IsSome -> running one
        | [ one ] -> toggling one
        | _ ->
            Error
                "Say a cell to turn it on or off - 'f7' - or 'step' to let the rule run, 'step 10' for ten. 'help' has the rest."
