module TCModel.Program

open System
open System.IO
open TCModel.Domain
open TCModel.App
open TCModel.Console
open TCModel.Net

let private clockSeed () = uint64 DateTime.UtcNow.Ticks

/// Names the file one game's record will be kept in. Taken once when the game is dealt,
/// so every save of that game writes over the same file.
let private stampNow () =
    DateTime.Now.ToString "yyyy-MM-dd-HHmmss"

/// The console watching its own game. There is only ever one of these at a keyboard, so
/// what it is called matters no more than that `Solo` and this agree on it.
[<Literal>]
let private Keyboard = "keyboard"

/// Do what a typed line asked of the world.
///
/// Writing a file is the whole of what that ever is. `Solo` decides *whether* - including
/// the awkward case where a restart has to write the game it just swept away rather than
/// the one in hand - and this does it, which is the only reason there is a `Program.fs`
/// between a keyboard and a fold.
let private errand doing =
    let write model stamp announce =
        if announce then
            let path = Transcript.save stamp model.Journal
            printfn "Record saved to %s" (Path.GetRelativePath(Directory.GetCurrentDirectory(), path))
        elif not (Journal.isEmpty model.Journal) then
            Transcript.save stamp model.Journal |> ignore

    match doing with
    | Carrying -> ()
    | Keeping(model, stamp, announce) -> write model stamp announce
    | Leaving(model, stamp) -> if not (Journal.isEmpty model.Journal) then write model stamp true

/// Fold what is typed here into the game. Once it is over the loop stays open, so the
/// record can be read and walked back through before the table is cleared.
///
/// What a line *means* is `Solo`'s, not this file's. All that is left here is a keyboard,
/// a screen and a file - which is why the local game can be checked without any of the
/// three, the same way the networked one can.
let rec private loop solo =
    // A terminal cannot patch a screen in place, so the board is drawn afresh each turn -
    // and the `Screen` posts below are dropped, because this is them.
    Solo.board Keyboard solo |> Option.iter (printf "%s")
    printf "%s" (if Model.isOver (Solo.model solo) then "(over) > " else "> ")

    let heard line =
        let next, posts, doing = Solo.said (stampNow ()) Keyboard line solo

        for post in posts do
            match post.Say with
            | Told text
            | TurnedAway text -> printf "%s" (text + Environment.NewLine)
            | Screen _
            | Seated _ -> ()

        errand doing

        match doing with
        | Leaving _ -> Solo.model next
        | Carrying
        | Keeping _ -> loop next

    match Console.ReadLine() with
    // Nothing more coming. The same as saying so, and answered the same way, so that a
    // game piped in from a file still ends up written down.
    | null -> heard "quit"
    | line -> heard line

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

/// Deal a game and serve it to a browser on this machine. Nothing comes back: like a
/// hosted table it runs until the process is stopped, because a page has no way of saying
/// the game is over and done with.
let private serveFor palette players seed =
    match dealt players seed with
    | Error problem ->
        eprintfn "%s" problem
        1
    | Ok model ->
        let keep model stamp =
            if Journal.isEmpty model.Journal then
                None
            else
                let path = Transcript.save stamp model.Journal
                Some(Path.GetRelativePath(Directory.GetCurrentDirectory(), path))

        // Nobody is watching yet. A browser adds itself when it opens its stream, which is
        // the only moment anybody is really there.
        Server.serve Protocol.DefaultPort palette (Solo.opened (stampNow ()) model) stampNow keep

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

        Server.host Protocol.DefaultPort model (fun model ->
            if not (Journal.isEmpty model.Journal) then
                Transcript.save stamp model.Journal |> ignore)

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
        | Ok(Menu.Serve(players, seed)) ->
            // In whatever colours the player settled on here, which is the same promise
            // the command line's --colour keeps.
            Done(serveFor view.Palette players (seed |> Option.defaultValue (clockSeed ())))
        | Ok(Menu.Host(players, seed)) -> Done(hostFor players (seed |> Option.defaultValue (clockSeed ())))
        | Ok(Menu.Join(address, token)) -> Done(Client.join address token view)
        | Ok(Menu.Replay path) ->
            match replayFrom path with
            | Ok model -> Play(model, view)
            | Error problem -> retry problem
        | Error problem -> retry problem

/// Sit down at a game and play it here. The board the player is looking at when they
/// arrive is drawn by the same code that draws every one after it, because sitting down is
/// simply the first thing that happens at the table.
let private play view model =
    let solo, _ =
        Solo.opened (stampNow ()) model
        |> Solo.watching Keyboard { Notes = true; View = view }

    loop solo |> ignore
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
    | Launch.Serve(players, seed) -> serveFor view.Palette players (seed |> Option.defaultValue (clockSeed ()))
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
