module TCModel.Console.Program

open System
open System.IO
open TCModel.Domain
open TCModel.App
open TCModel.Net

let private clockSeed () = uint64 DateTime.UtcNow.Ticks

/// Names the file one game's record will be kept in. Taken once when the game is dealt,
/// so every save of that game writes over the same file.
let private stampNow () =
    DateTime.Now.ToString "yyyy-MM-dd-HHmmss"

let private keep stamp model =
    let path = Transcript.save stamp model.Journal
    printfn "Record saved to %s" (Path.GetRelativePath(Directory.GetCurrentDirectory(), path))

/// Save without saying so, for the times the game ends on its own and the player has not
/// asked for anything.
let private keepQuietly stamp model =
    if not (Journal.isEmpty model.Journal) then
        Transcript.save stamp model.Journal |> ignore

/// How the person at this keyboard is reading the game: whether the board comes with the
/// writing that explains it, and in what hand it is drawn.
///
/// Neither is part of the game. Both are how it is being read rather than how it is being
/// played, so they stay out of the model and out of the record - and a fresh deal is still
/// read the way the player was reading the last one.
[<NoComparison; NoEquality>]
type private Reading = { Notes: bool; View: View }

/// Fold console input into the model. Once the game is over the loop stays open, so the
/// record can be read and walked back through before the table is cleared.
let rec private loop stamp (reading: Reading) model =
    // One keyboard, so the screen belongs to whoever is to play and the beholder
    // changes hands with the turn. Over a network each console has a seat of its own.
    let beholder = Game.active (Model.game model)

    /// Everything a player reads goes out through the view they chose, and nothing goes
    /// out any other way.
    let showLine (text: string) =
        printf "%s" (text + Environment.NewLine)

    printf "%s" (reading.View.Board reading.Notes beholder model)
    printf "%s" (if Model.isOver model then "(over) > " else "> ")

    match Console.ReadLine() with
    | null -> leave stamp model
    | line ->
        match Parse.line line with
        | Ok Parse.Nothing -> loop stamp reading model
        | Ok Parse.Leave -> leave stamp model
        | Ok Parse.Help ->
            showLine reading.View.Rules
            loop stamp reading model
        | Ok(Parse.Notes wanted) ->
            loop
                stamp
                { reading with
                    Notes = wanted |> Option.defaultValue (not reading.Notes) }
                model
        | Ok(Parse.Looking name) ->
            match View.byName reading.View.Shown reading.View.Palette name with
            | Ok view -> loop stamp { reading with View = view } model
            | Error problem -> loop stamp reading (Update.note problem model)
        | Ok Parse.Recount ->
            showLine (reading.View.History beholder model)
            loop stamp reading model
        | Ok Parse.Keep ->
            keep stamp model
            loop stamp reading model
        | Ok(Parse.Explain regionId) ->
            showLine (reading.View.Ruling regionId model)
            loop stamp reading model
        | Ok(Parse.Send(Restart _ as msg)) ->
            // The old game's record is closed and kept before the table is cleared.
            keepQuietly stamp model
            loop (stampNow ()) reading (Update.update msg model)
        | Ok(Parse.Send msg) ->
            let next = Update.update msg model
            // A game that has just ended writes itself down without being asked.
            if Model.isOver next && not (Model.isOver model) then keepQuietly stamp next

            loop stamp reading next
        | Error problem -> loop stamp reading (Update.note problem model)

/// Put the game down. One still in play is resigned first, so the record says how it
/// ended rather than simply stopping.
and private leave stamp model =
    let model = if Model.isOver model then model else Update.update (Make Resign) model

    if not (Journal.isEmpty model.Journal) then keep stamp model

    model

/// Deal a game. The table is the only thing that can refuse, and it says why, so a
/// count that came from a person is answered in their words rather than swallowed.
let private dealt players seed =
    match Update.start players seed with
    | Ok model -> Ok model
    | Error(TooFewPlayers n) -> Error $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
    | Error(TooManyPlayers n) -> Error $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."

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

/// Open a table for players at their own machines. Nothing comes back: the table waits
/// until whoever opened it stops the process, because no one player may close it on all
/// the others.
let private hostFor players seed =
    match dealt players seed with
    | Error problem ->
        eprintfn "%s" problem
        1
    | Ok model ->
        let stamp = stampNow ()
        Server.host Protocol.DefaultPort model (keepQuietly stamp)

/// What the menu settled on: a game to play at this keyboard, or a way of playing that
/// runs to its own end and only has an exit code to give back.
[<NoComparison; NoEquality>]
type private Opening =
    | Play of Model * View
    | Done of code: int

/// The colour screen, which runs until the player is done with it and gives back the view
/// they came in reading - the same one, in whatever colours they settled on.
///
/// It is shown through that view, so the sample colours on it are drawn by the very thing
/// that will be drawing the board.
let rec private colouring (view: View) =
    printf "%s" (view.Says(Options.screen view.Palette))
    printf "> "

    match Console.ReadLine() with
    | null -> view
    | line ->
        match Options.choose view.Palette line with
        | Ok Options.Done -> view
        | Ok Options.Same -> colouring view
        | Ok(Options.Changed palette) -> colouring (View.recoloured palette view)
        | Error problem ->
            printfn "%s" (view.Says problem)
            colouring view

/// The start menu, which runs until it has settled on one of those. Everything it offers
/// either opens a game or comes back round to here, so there is no way out of it but the
/// two, and no way to be at the prompt with nothing to play.
let rec private welcome (view: View) =
    // The menu is shown in the view it is offering, so 'view rich' shows what rich looks
    // like before a whole game is committed to it.
    printf "%s" (view.Says(Menu.screen view))
    printf "> "

    /// Say what went wrong and ask again. The menu is the only place to be when there is
    /// no game yet, so nothing here leaves except by being asked to.
    let retry problem =
        printfn "%s" (view.Says problem)
        welcome view

    match Console.ReadLine() with
    | null -> Done 0
    | line ->
        match Menu.choose view.Palette line with
        | Ok Menu.Waiting -> welcome view
        | Ok Menu.Leave -> Done 0
        | Ok Menu.Rules ->
            printfn "%s" view.Rules
            welcome view
        | Ok(Menu.Looking chosen) -> welcome chosen
        | Ok Menu.Options -> welcome (colouring view)
        | Ok(Menu.Deal(players, seed)) ->
            match dealt players (seed |> Option.defaultValue (clockSeed ())) with
            | Ok model -> Play(model, view)
            | Error problem -> retry problem
        | Ok(Menu.Host(players, seed)) -> Done(hostFor players (seed |> Option.defaultValue (clockSeed ())))
        | Ok(Menu.Join(address, token)) -> Done(Client.join address token view)
        | Ok(Menu.Replay path) ->
            match replayFrom path with
            | Ok model -> Play(model, view)
            | Error problem -> retry problem
        | Error problem -> retry problem

let private play view model =
    loop (stampNow ()) { Notes = true; View = view } model |> ignore
    0

/// Act on what a command line asked for.
///
/// Everything that reads a command line - `Shell` at the door, `Launch` reading a line the
/// program wrote itself - stops at a `Launch` and hands it here. So there is one place
/// that knows what opening a game actually involves, and adding a way in means adding a
/// case rather than another road through `main`.
let private opening (view: View) launch =
    let orElse outcome =
        match outcome with
        | Ok model ->
            printfn "%s" view.Rules
            play view model
        | Error problem ->
            eprintfn "%s" problem
            1

    match launch with
    | Launch.Deal(players, seed) -> orElse (dealt players (seed |> Option.defaultValue (clockSeed ())))
    | Launch.Host(players, seed) -> hostFor players (seed |> Option.defaultValue (clockSeed ()))
    | Launch.Join(address, token) -> Client.join address token view
    | Launch.Replay path -> orElse (replayFrom path)

[<EntryPoint>]
let main argv =
    match Board.problems with
    | _ :: _ as problems ->
        eprintfn "The map does not hang together:"
        problems |> List.iter (eprintfn "  %s")
        1
    | [] ->

    // Arguments say what to open and go straight to it, so a game can still be started
    // from a script or a shortcut. With none, the menu asks - and a game opened from the
    // menu is read in whatever the player set there rather than in what the shell would
    // have defaulted to.
    match argv with
    | [||] ->
        match welcome (View.plain Palette.standard) with
        | Play(model, view) -> play view model
        | Done code -> code
    | _ -> Shell.run opening argv
