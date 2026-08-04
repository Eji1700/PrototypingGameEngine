module TCModel.Console.Program

open System
open System.IO
open TCModel.Domain
open TCModel.App

let private clockSeed () = uint64 DateTime.UtcNow.Ticks

/// Names the file one game's record will be kept in. Taken once when the game is dealt,
/// so every save of that game writes over the same file.
let private stampNow () = DateTime.Now.ToString "yyyy-MM-dd-HHmmss"

let private keep stamp model =
    let path = Transcript.save stamp model.Journal
    printfn "Record saved to %s" (Path.GetRelativePath(Directory.GetCurrentDirectory(), path))

/// Save without saying so, for the times the game ends on its own and the player has not
/// asked for anything.
let private keepQuietly stamp model =
    if not (Journal.isEmpty model.Journal) then
        Transcript.save stamp model.Journal |> ignore

/// Fold console input into the model. Once the game is over the loop stays open, so the
/// record can be read and walked back through before the table is cleared.
///
/// `notes` is the one thing the shell remembers for itself: whether the board comes with
/// the writing that explains it. It is how the game is being read rather than how it is
/// being played, so it stays out of the model and out of the record, and a fresh deal is
/// still read the way the player was reading the last one.
let rec private loop stamp notes model =
    printf "%s" (Render.model notes model)
    printf "%s" (if Model.isOver model then "(over) > " else "> ")

    match Console.ReadLine() with
    | null -> leave stamp model
    | line ->
        match Parse.line line with
        | Ok Parse.Nothing -> loop stamp notes model
        | Ok Parse.Leave -> leave stamp model
        | Ok Parse.Help ->
            printfn "%s" Render.help
            loop stamp notes model
        | Ok(Parse.Notes wanted) -> loop stamp (wanted |> Option.defaultValue (not notes)) model
        | Ok Parse.Recount ->
            printfn "%s" (Render.history model)
            loop stamp notes model
        | Ok Parse.Keep ->
            keep stamp model
            loop stamp notes model
        | Ok(Parse.Explain regionId) ->
            printfn "%s" (Render.explainRule regionId model)
            loop stamp notes model
        | Ok(Parse.Send(Restart _ as msg)) ->
            // The old game's record is closed and kept before the table is cleared.
            keepQuietly stamp model
            loop (stampNow ()) notes (Update.update msg model)
        | Ok(Parse.Send msg) ->
            let next = Update.update msg model
            // A game that has just ended writes itself down without being asked.
            if Model.isOver next && not (Model.isOver model) then
                keepQuietly stamp next

            loop stamp notes next
        | Error problem -> loop stamp notes (Update.note problem model)

/// Put the game down. One still in play is resigned first, so the record says how it
/// ended rather than simply stopping.
and private leave stamp model =
    let model =
        if Model.isOver model then
            model
        else
            Update.update (Make Resign) model

    if not (Journal.isEmpty model.Journal) then
        keep stamp model

    model

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

/// Play a saved game again and stop where it left off, from where `undo` walks back
/// through every state it passed on the way.
let private replayFrom path =
    if not (File.Exists path) then
        Error $"There is no record at '{path}'."
    else
        Transcript.read (File.ReadAllText path)
        |> Result.bind (fun reading ->
            Update.replay reading.Players reading.Seed reading.Moves
            |> Result.mapError (fun _ -> $"'{path}' asks for a number of players the game does not take.")
            |> Result.map (fun model ->
                printfn "Replayed %d move(s) from %s." (List.length reading.Moves) path
                printfn "Take them back with 'undo', or read them with 'history'."
                model))

[<EntryPoint>]
let main argv =
    match Board.problems with
    | _ :: _ as problems ->
        eprintfn "The map does not hang together:"
        problems |> List.iter (eprintfn "  %s")
        1
    | [] ->

    let dealt =
        match argv with
        | [| "replay"; path |] -> replayFrom path
        | _ -> dealFrom argv

    match dealt with
    | Error problem ->
        eprintfn "%s" problem
        1
    | Ok model ->
        printfn "%s" Render.help
        loop (stampNow ()) true model |> ignore
        0
