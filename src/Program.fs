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

/// Say the parts of what the table said that a terminal has to print for itself. A screen is
/// not one of them: the loop below draws the board afresh each turn, because a terminal
/// cannot patch one in place the way a page can.
///
/// The bell is answered the same way a joined table answers it, and for the same reason -
/// but at one keyboard it never rings, because the only console at the table is the one that
/// just typed something and a table never nudges the player who spoke.
let private tell posts =
    for post in posts do
        match post.Say with
        | Told text
        | TurnedAway text -> printf "%s" (text + Environment.NewLine)
        | Nudged -> printf "\a"
        | Screen _
        | Seated _ -> ()

/// Fold what is typed here into the game. Once it is over the loop stays open, so the
/// record can be read and walked back through before the table is cleared.
///
/// What a line *means* is `Solo`'s, not this file's. All that is left here is a keyboard,
/// a screen and a file - which is why the local game can be checked without any of the
/// three, the same way the networked one can.
let rec private loop solo =
    Solo.board Keyboard solo |> Option.iter (printf "%s")
    printf "%s" (if Model.isOver (Solo.model solo) then "(over) > " else "> ")

    let heard line =
        let next, posts, doing = Solo.said (stampNow ()) Keyboard line solo

        tell posts
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
let private serveFor palette sitters seed reach =
    match dealt (List.length sitters) seed with
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
        // the only moment anybody is really there - and the machines have already played up
        // to the first seat a person has to fill by the time it does.
        let solo, doing =
            Solo.opened (stampNow ()) model
            |> Solo.against (Rival.seating (Model.seed model) (Seating.machines sitters) (Model.game model))

        errand doing
        Server.serve reach palette solo stampNow keep

/// Open a table for players at their own machines. Nothing comes back: the table waits
/// until whoever opened it stops the process, because no one player may close it on all
/// the others.
///
/// The seating goes in whole rather than as a count, because a table may have the machine
/// at some of its seats - those are played here and are never waited for. The reach goes in
/// whole for the same reason: how far the table can be reached and what it takes to sit down
/// at it are settled together or they contradict each other.
let private hostFor view sitters seed reach =
    match dealt (List.length sitters) seed with
    | Error problem ->
        eprintfn "%s" problem
        1
    | Ok model ->
        let stamp = stampNow ()

        // A seat of the host's own is taken from here, by a console sitting down at this very
        // table over the same wire everybody else arrives on. Nothing about the table is
        // special-cased for it: it joins, it is handed a seat and a token, it is drawn a board
        // per turn, and if the process it is inside had been on another machine the table
        // could not tell the difference. Which is the point - a seat played by a shortcut
        // would be a second way of sitting down, and there is no room here for two.
        let mine, _ = Seating.awaited sitters

        let playing =
            if mine = 0 then
                None
            else
                Some(fun () -> Client.join (Reach.at reach "localhost") None (Reach.word reach) view |> ignore)

        let keep model =
            if not (Journal.isEmpty model.Journal) then
                Transcript.save stamp model.Journal |> ignore

        Server.host reach model sitters keep playing

/// What the menu settled on: a game to play at this keyboard, or a way of playing that
/// runs to its own end and only has an exit code to give back.
[<NoComparison; NoEquality>]
type private Opening =
    | Play of Model * View * Sitter list
    | Done of code: int

/// Whether there is somebody at the keyboard to steer with. A line piped in cannot press an
/// arrow, and a redirected console throws rather than answering for one, so a screen shown
/// to one of those is shown whole and read a line at a time exactly as it always was.
let private steering () = not Console.IsInputRedirected

/// Nothing here is worth losing a turn over: a console that will not clear is a console the
/// menu scrolls in, which is how it read before there was anything to move.
let private cleared () =
    try
        Console.Clear()
    with _ ->
        ()

/// Hold the screen until a key, for the one thing the menu prints that is longer than the
/// menu. It is about to be wiped, and nobody reads the rules in the time it takes to press
/// something.
let private held () =
    if steering () then
        printf "Press any key."
        Console.ReadKey true |> ignore

/// Ask a screen for a line.
///
/// What comes back is a line in the words a person would have typed, so on the other side of
/// this is the same reader that has always been there - the arrows are a way of typing
/// rather than a second way of meaning something. Where the highlight was left comes back
/// with it: walking a colour along changes the palette, which builds the screen again, and
/// the cursor has to still be on the slot that is being changed.
let private asking (view: View) said screen at =
    let rec steer standing =
        let showing, index = Keys.facing standing
        cleared ()
        printf "%s" (view.Says(Keys.draw (Some index) showing))

        if said <> "" then printfn "%s" (view.Says said)

        printf "> %s" standing.Buffer

        match Keys.answer (Keys.pressed (Keys.typing standing) (Console.ReadKey true)) standing with
        | Keys.Steering next -> steer next
        | Keys.Answered line -> Some line, Keys.started standing

    if steering () then
        steer (Keys.standing screen at)
    else
        printf "%s" (view.Says(Keys.draw None screen))

        if said <> "" then printfn "%s" (view.Says said)

        printf "> "

        match Console.ReadLine() with
        | null -> None, at
        | line -> Some line, at

/// The colour screen, which runs until the player is done with it and gives back the view
/// they came in reading - the same one, in whatever colours they settled on.
///
/// It is shown through that view, so the sample colours on it are drawn by the very thing
/// that will be drawing the board.
let rec private colouring (view: View) at said =
    match asking view said (Options.screen view.Palette) at with
    | None, _ -> view
    | Some line, at ->
        match Options.choose view.Palette line with
        | Ok Options.Done -> view
        | Ok Options.Same -> colouring view at ""
        | Ok(Options.Changed palette) -> colouring (View.recoloured palette view) at ""
        | Error problem -> colouring view at problem

/// What the menu settled on, once it is a game rather than another screen.
///
/// Shared by the front door and the seat list, because a seating dealt from one is dealt
/// exactly as it is from the other - and because a way of opening a game added to only one
/// of the two would be a way of opening a game a player could not find.
let private starting (view: View) choice =
    let clocked seed =
        seed |> Option.defaultValue (clockSeed ())

    match choice with
    | Menu.Deal(sitters, seed) ->
        dealt (List.length sitters) (clocked seed)
        |> Result.map (fun model -> Play(model, view, sitters))
    // In whatever colours the player settled on here, which is the same promise the
    // command line's --colour keeps.
    //
    // A line that said nothing about how far the table reaches is answered the way the
    // command line answers the same silence: a word at the door, made up here. Everything the
    // seat list and the screen behind it send says the whole of it, so this is only reached
    // by somebody typing the short way round.
    | Menu.Serve(sitters, seed, reach) ->
        Ok(Done(serveFor view.Palette sitters (clocked seed) (reach |> Option.defaultWith Reach.fresh)))
    | Menu.Host(sitters, seed, reach) -> Ok(Done(hostFor view sitters (clocked seed) (reach |> Option.defaultWith Reach.fresh)))
    | Menu.Join(address, code) -> Ok(Done(Client.join address None code view))
    | Menu.Replay path -> replayFrom path |> Result.map (fun model -> Play(model, view, []))
    // The rest are screens rather than games. The front door answers every one of them
    // itself, so what is left here is somebody at the seat list asking for one of them from
    // there - which is a fair thing to type and wants an answer rather than a shrug.
    | Menu.Sitting _
    | Menu.Reaching _
    | Menu.Rules
    | Menu.Looking _
    | Menu.Options
    | Menu.Leave
    | Menu.Backing
    | Menu.Waiting -> Error "That is settled at the menu. Say 'back' to go there, or name the seats."

/// The two screens a game is settled on, which run until there is one to open or are backed
/// out of: who is in each seat, and how far the table those seats are at will reach.
///
/// One loop each and one word between them, because every line either of them sends says the
/// whole of both - a seating and a reach - so neither has anything to remember and either can
/// hand the other everything it needs. The word is the one thing here that had to be made up
/// rather than read, so it is made up once, out here, and passed along: a screen that invented
/// one as it drew would show a different word every time it was drawn.
let rec private sitting (view: View) word sitters reach at said =
    match asking view said (Menu.seats sitters reach) at with
    | None, _ -> Some(Done 0)
    | Some line, at -> answering view word sitters reach at line sitting

and private reaching (view: View) word sitters reach at said =
    match asking view said (Menu.reaches word sitters reach) at with
    | None, _ -> Some(Done 0)
    | Some line, at -> answering view word sitters reach at line reaching

/// What either of them does with a line, which is the same thing: the seats and the reach
/// come back out of it, and where they are shown next is the line's own answer.
and private answering view word sitters reach at line asked =
    let again sitters reach said = asked view word sitters reach at said

    match Menu.choose view.Palette line with
    | Ok(Menu.Sitting(sitters, asked)) -> sitting view word sitters (asked |> Option.defaultValue reach) at ""
    | Ok(Menu.Reaching(sitters, reach)) -> reaching view word sitters reach at ""
    | Ok Menu.Waiting -> again sitters reach ""
    | Ok Menu.Backing -> None
    // Going is going, from wherever it is said.
    | Ok Menu.Leave -> Some(Done 0)
    | Ok chosen ->
        match starting view chosen with
        | Ok opening -> Some opening
        | Error problem -> again sitters reach problem
    | Error problem -> again sitters reach problem

/// The start menu, which runs until it has settled on a game. Everything it offers either
/// opens one or comes back round to here, so there is no way out of it but the two, and no
/// way to be at the prompt with nothing to play.
let rec private welcome (view: View) at said =
    // The menu is shown in the view it is offering, so 'view rich' shows what rich looks
    // like before a whole game is committed to it.
    match asking view said (Menu.screen view) at with
    | None, _ -> Done 0
    | Some line, at ->

    /// Say what went wrong and ask again, with the cursor where it was left. The menu is the
    /// only place to be when there is no game yet, so nothing here leaves except by being
    /// asked to.
    let retry problem = welcome view at problem

    match Menu.choose view.Palette line with
    // There is nothing behind the front door, so backing out of it is asking again.
    | Ok Menu.Waiting
    | Ok Menu.Backing -> welcome view at ""
    | Ok Menu.Leave -> Done 0
    | Ok Menu.Rules ->
        printfn "%s" view.Rules
        held ()
        welcome view at ""
    | Ok(Menu.Looking chosen) -> welcome chosen at ""
    | Ok Menu.Options -> welcome (colouring view 0 "") at ""
    | Ok(Menu.Sitting(sitters, asked)) ->
        // One word for this way through the menu, made up here because a screen cannot make
        // one up: it is what the door starts out holding, and what walking the door shut
        // again puts back, so that a player who opens it to look and changes their mind is
        // not handed a different table than the one they were reading a moment ago.
        let word = Reach.minted ()

        match sitting view word sitters (asked |> Option.defaultValue (Reach.locked word)) 0 "" with
        | Some opening -> opening
        | None -> welcome view at ""
    | Ok chosen ->
        match starting view chosen with
        | Ok opening -> opening
        | Error problem -> retry problem
    | Error problem -> retry problem

/// Sit down at a game and play it here. The board the player is looking at when they
/// arrive is drawn by the same code that draws every one after it, because sitting down is
/// simply the first thing that happens at the table.
let private play view sitters model =
    // The machines take their seats before anybody sits down to watch, so that a table where
    // the first move is theirs has already had it made by the time the first board is drawn.
    let seated, doing =
        Solo.opened (stampNow ()) model
        |> Solo.against (Rival.seating (Model.seed model) (Seating.machines sitters) (Model.game model))

    errand doing

    let solo, posts = seated |> Solo.watching Keyboard { Notes = true; View = view }

    tell posts
    loop solo |> ignore
    0

/// Act on what a command line asked for.
///
/// Reading a command line stops at a `Launch`, and this is what one is handed to. So there
/// is one place that knows what opening a game actually involves, and adding a way in means
/// adding a case rather than another road through `main`.
let private opening (view: View) launch =
    let orElse sitters outcome =
        match outcome with
        | Ok model ->
            printfn "%s" view.Rules
            play view sitters model
        | Error problem ->
            eprintfn "%s" problem
            1

    let clocked seed =
        seed |> Option.defaultValue (clockSeed ())

    // The command line names the machines rather than the seats - `--rival hard` is the seat
    // after yours - so what it asks for is a seating said shorter, and it is spelt out into
    // one here. There is one kind of table below this line, and it is the seating.
    match launch with
    | Launch.Deal(players, seed, rivals) -> orElse (Seating.after players rivals) (dealt players (clocked seed))
    | Launch.Serve(players, seed, rivals, reach) -> serveFor view.Palette (Seating.after players rivals) (clocked seed) reach
    | Launch.Host(players, seed, reach) -> hostFor view (Seating.hosting players) (clocked seed) reach
    | Launch.Join(address, token, code) -> Client.join address token code view
    | Launch.Replay path -> orElse [] (replayFrom path)

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
        match welcome (View.plain Palette.standard) 0 "" with
        | Play(model, view, sitters) -> play view sitters model
        | Done code -> code
    | _ -> Launch.run opening argv
