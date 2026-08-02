/// The shell around the MVU core: read a line, fold it into the model, render.
module TCModel.Program

open System

let private clockSeed () = uint64 DateTime.UtcNow.Ticks

/// Command line: [players] [seed], either of which may be left off.
let private dealFrom (argv: string array) =
    let players =
        match argv with
        | [||] -> Ok Setup.MinPlayers
        | _ ->
            match Int32.TryParse argv[0] with
            | true, n when n >= Setup.MinPlayers && n <= Setup.MaxPlayers -> Ok n
            | _ -> Error $"Usage: dotnet run -- [players {Setup.MinPlayers}-{Setup.MaxPlayers}] [seed]"

    let seed =
        match argv with
        | [| _; seed |] ->
            match UInt64.TryParse seed with
            | true, value -> Ok value
            | _ -> Error "Usage: the seed must be a whole number."
        | _ -> Ok(clockSeed ())

    match players, seed with
    | Ok players, Ok seed -> Ok(Setup.init players seed)
    | Error problem, _
    | _, Error problem -> Error problem

/// Fold console input into the model until the game reports itself over.
let rec private loop model =
    printf "%s" (View.render model)

    match model.Status with
    | Over _ -> model
    | InProgress ->
        printf "> "

        match Console.ReadLine() with
        | null -> { model with Status = Over "no more input" }
        | line ->
            match Input.parse line with
            | Ok Input.Nothing -> loop model
            | Ok Input.Help ->
                printfn "%s" View.help
                loop model
            | Ok(Input.Game msg) -> loop (Update.update msg model)
            | Error problem -> loop (Update.note problem model)

[<EntryPoint>]
let main argv =
    match Board.problems with
    | _ :: _ as problems ->
        eprintfn "The map does not hang together:"
        problems |> List.iter (eprintfn "  %s")
        1
    | [] ->

    match dealFrom argv with
    | Error problem ->
        eprintfn "%s" problem
        1
    | Ok model ->
        printfn "%s" View.help
        loop model |> ignore
        0
