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

    let private stepping (word: string) =
        match Commands.tryInt word with
        | Some generations -> Ok(Send(Make(Step generations)))
        | None -> Error $"'{word}' is not a number of generations. Say 'step 10', or 'step' for one."

    let line typed =
        match Commands.lowered typed with
        // `run` turns the rule the other way from wherever it is, because that is what one key
        // can do; `start` and `stop` say which outright, for a record and for anybody who would
        // rather not count.
        | [ "run" ]
        | [ "p" ] -> Ok(Send(Make(Running None)))
        | [ "start" ]
        | [ "go" ] -> Ok(Send(Make(Running(Some true))))
        | [ "stop" ]
        | [ "pause" ]
        | [ "halt" ] -> Ok(Send(Make(Running(Some false))))
        // The clock's own move, spelt out. Here for the console that cannot press anything - a
        // game piped in from a file, or a record replaying - so a board driven by hand and one
        // driven by the clock are the same game and the same record.
        | [ "beat" ] -> Ok(Send(Make Beat))
        | [ "faster" ]
        | [ "quicker" ]
        | [ "+" ] -> Ok(Send(Make Faster))
        | [ "slower" ]
        | [ "-" ] -> Ok(Send(Make Slower))
        | [ "speed"; notch ] ->
            match Commands.tryInt notch with
            | Some notch -> Ok(Send(Make(Speed notch)))
            | None -> Error $"'{notch}' is not a speed. They run from {World.Slowest} to {World.Fastest}."
        | [ "step" ]
        | [ "s" ] -> Ok(Send(Make(Step 1)))
        | [ "step"; n ]
        | [ "s"; n ] -> stepping n
        | [ "clear" ] -> Ok(Send(Make Clear))
        | [ "toggle"; c ]
        | [ "t"; c ] -> toggling c
        | [ "why"; c ]
        | [ "ask"; c ] -> asking c
        | [ one ] when (Commands.tryInt one).IsSome -> stepping one
        | [ one ] -> toggling one
        | _ ->
            Error
                "Say a cell to turn it on or off - 'f7' - or 'run' to start and stop the rule, 'step' for one generation. 'help' has the rest."
