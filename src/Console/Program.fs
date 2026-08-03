module TCModel.Console.Program

open System
open TCModel.Domain
open TCModel.App

let private clockSeed () = uint64 DateTime.UtcNow.Ticks

/// Command line: [players] [seed], either of which may be left off.
let private dealFrom (argv: string array) =
    let usage = $"Usage: dotnet run -- [players {Table.MinPlayers}-{Table.MaxPlayers}] [seed]"

    let players =
        match argv with
        | [||] -> Ok Table.MinPlayers
        | _ ->
            match Int32.TryParse argv[0] with
            | true, n when n >= Table.MinPlayers && n <= Table.MaxPlayers -> Ok n
            | _ -> Error usage

    let seed =
        match argv with
        | [| _; given |] ->
            match UInt64.TryParse given with
            | true, value -> Ok value
            | _ -> Error "Usage: the seed must be a whole number."
        | _ -> Ok(clockSeed ())

    match players, seed with
    | Ok players, Ok seed ->
        match Update.start players seed with
        | Ok model -> Ok model
        | Error _ -> Error usage
    | Error problem, _
    | _, Error problem -> Error problem

/// Fold console input into the model until the game reports itself over.
let rec private loop model =
    printf "%s" (Render.model model)

    if Model.isOver model then
        model
    else

    printf "> "

    match Console.ReadLine() with
    | null -> Update.update Quit model
    | line ->
        match Parse.line line with
        | Ok Parse.Nothing -> loop model
        | Ok Parse.Help ->
            printfn "%s" Render.help
            loop model
        | Ok(Parse.Explain regionId) ->
            printfn "%s" (Render.explainRule regionId model)
            loop model
        | Ok(Parse.Send msg) -> loop (Update.update msg model)
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
        printfn "%s" Render.help
        loop model |> ignore
        0
