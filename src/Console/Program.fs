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

/// Deal a game. The table is the only thing that can refuse, and it says why, so a
/// count that came from a person is answered in their words rather than swallowed.
let private dealt players seed =
    match Update.start players seed with
    | Ok model -> Ok model
    | Error(TooFewPlayers n) -> Error $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
    | Error(TooManyPlayers n) -> Error $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."

/// Command line: [players] [seed], either of which may be left off.
let private dealFrom (argv: string array) =
    let usage = $"Usage: dotnet run -- [players {Table.MinPlayers}-{Table.MaxPlayers}] [seed]"

    let players =
        match argv with
        | [||] -> Ok Table.MinPlayers
        | _ -> Parse.tryPlayerCount argv[0] |> Result.mapError (fun _ -> usage)

    let seed =
        match argv with
        | [| _; given |] -> Parse.trySeed given |> Result.mapError (fun _ -> "Usage: the seed must be a whole number.")
        | _ -> Ok(clockSeed ())

    match players, seed with
    | Ok players, Ok seed -> dealt players seed |> Result.mapError (fun _ -> usage)
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

/// The start menu, which runs until there is a game to play or nobody left to play it.
/// Everything it offers either deals a game or comes back round to here, so what it
/// hands on is a game or nothing at all.
let rec private welcome () =
    printf "%s" Menu.screen
    printf "> "

    /// Say what went wrong and ask again. The menu is the only place to be when there
    /// is no game yet, so nothing here can leave except by being asked to.
    let retry problem =
        printfn "%s" problem
        welcome ()

    match Console.ReadLine() with
    | null -> None
    | line ->
        match Menu.choose line with
        | Ok Menu.Waiting -> welcome ()
        | Ok Menu.Leave -> None
        | Ok Menu.Rules ->
            printfn "%s" Render.help
            welcome ()
        | Ok(Menu.Deal(players, seed)) ->
            match dealt players (seed |> Option.defaultValue (clockSeed ())) with
            | Ok model -> Some model
            | Error problem -> retry problem
        | Ok(Menu.Replay path) ->
            match replayFrom path with
            | Ok model -> Some model
            | Error problem -> retry problem
        | Error problem -> retry problem

let private play model =
    loop (stampNow ()) true model |> ignore
    0

[<EntryPoint>]
let main argv =
    match Board.problems with
    | _ :: _ as problems ->
        eprintfn "The map does not hang together:"
        problems |> List.iter (eprintfn "  %s")
        1
    | [] ->

    // Arguments say what to deal and go straight to the board, so a game can still be
    // started from a script or a shortcut exactly as before. With none, the menu asks.
    match argv with
    | [||] ->
        match welcome () with
        | Some model -> play model
        | None -> 0
    | _ ->
        let opening =
            match argv with
            | [| "replay"; path |] -> replayFrom path
            | _ -> dealFrom argv

        match opening with
        | Error problem ->
            eprintfn "%s" problem
            1
        | Ok model ->
            printfn "%s" Render.help
            play model
