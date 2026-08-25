module Prototyping.Play

open System
open System.IO
open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Net

let private clockSeed () = uint64 DateTime.UtcNow.Ticks

let private stamping (game: Playable<_, _, _>) =
    fun () -> Transcript.stamping game.Name DateTime.Now

[<Literal>]
let private Keyboard = "keyboard"

let private errand game sitters doing =
    let write model stamp announce =
        if announce then
            let path = Transcript.save game stamp sitters model.Journal
            [ $"Record saved to {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}" ]
        else
            if not (Journal.isEmpty model.Journal) then
                Transcript.save game stamp sitters model.Journal |> ignore

            []

    match doing with
    | Carrying -> []
    | Keeping(model, stamp, announce) -> write model stamp announce
    | Leaving(Some model, stamp) -> if Journal.isEmpty model.Journal then [] else write model stamp true
    | Leaving(None, _) -> []

/// A terminal has one bell, so a batch of posts that a board rang three times over rings once -
/// this is the only place that knows how many posts came out of a single move.
let private tell rings posts =
    let heard =
        posts
        |> List.exists (fun post ->
            match post.Say with
            | Nudged -> true
            | Rang sound -> Sound.worthABell sound
            | Seated _
            | Screen _
            | Told _
            | TurnedAway _
            | GotUp _ -> false)

    if rings && heard then printf "\a"

    posts
    |> List.choose (fun post ->
        match post.Say with
        | Told text
        | TurnedAway text
        | GotUp text -> Some text
        | Nudged
        | Rang _
        | Screen _
        | Seated _ -> None)

/// Playing a game of turns at this keyboard.
///
/// Two ways in, and `at` is why they are one function. A game that offers rows for where it stands
/// is *steered* - the arrows and w/a/s/d walk them, Enter takes the marked one, and Enter with a
/// line underway sends the line instead, so the prompt is never taken away. A game that offers
/// none, or a console that cannot read a key at all, gets the loop it always had.
///
/// `at` is where the mark had got to, carried across a move on purpose: every line rebuilds the
/// screen from the state it left behind, and a mark that went back to the top each time would make
/// walking one row through its choices impossible.
let rec private loop rings sitters said at solo =
    let show lines =
        for line in lines do
            printf "%s" (line + Environment.NewLine)

    let heard at line =
        let next, posts, doing = Solo.said (stamping (Solo.game solo) ()) Keyboard line solo
        let answered = tell rings posts @ errand (Solo.game solo) sitters doing

        match doing with
        | Leaving _ ->
            show answered
            Solo.model next
        | Carrying
        | Keeping _ -> loop rings sitters answered at next

    match (if Screens.steering () then Solo.steering Keyboard solo else None) with
    | Some screen ->
        let says = Solo.painting Keyboard solo |> Option.defaultValue id

        Screens.cleared ()

        match Screens.asking says (String.concat Environment.NewLine said) screen at with
        | None, at -> heard at "quit"
        | Some line, at -> heard at line

    | None ->
        Solo.board Keyboard solo |> Option.iter (printf "%s")
        show said
        printf "%s" (if Solo.isOver solo then "(over) > " else "> ")

        match Console.ReadLine() with
        | null -> heard at "quit"
        | line -> heard at line

/// Playing a game that runs on a clock at this keyboard, drawn over itself as it goes. The loop
/// waits for a keypress, the next frame or the next beat, whichever comes first; holding stops the
/// clock by waiting a day instead of an interval.
///
/// A beat is a move and a frame is not. `Solo.beaten` folds a beat into the model; a frame folds
/// nothing, and all that differs between two frames is how far through the beat `Margins.Phase`
/// says the drawing is. So a game with nothing moving between beats asks for no frames and this is
/// the loop it always was, and one that does gets its in-between pictures without any of them
/// reaching the timeline or the record.
let rec private racing rings sitters said (pulse: Pulse<_, _>) solo =
    let show lines =
        for line in lines do
            printf "%s" (line + Environment.NewLine)

    // A game gets first refusal on every key but the two this loop cannot give up: Enter opens the
    // line prompt and Escape puts the game down, and a game that could take those could leave
    // somebody at a board with no way out. `h` holds as well as the space bar always does, so a
    // game that takes the space bar does not cost a player the one key that stops the clock.
    let (|Typing|Holding|Restarting|Leaving'|Steering|Idle|) (key: ConsoleKeyInfo) =
        match key.Key with
        | ConsoleKey.Enter -> Typing
        | ConsoleKey.Escape -> Leaving'
        | _ ->
            match pulse.Pressed key with
            | Some line -> Steering line
            | None ->
                match key.Key with
                | ConsoleKey.Spacebar
                | ConsoleKey.H -> Holding
                | ConsoleKey.R -> Restarting
                | _ -> Idle

    // What to tell the player stops the clock, which is not the same key at every game.
    let holds =
        if (pulse.Pressed(ConsoleKeyInfo(' ', ConsoleKey.Spacebar, false, false, false))).IsSome then
            "h"
        else
            "space"

    let rec spin holding wanted drawn said since due solo =
        let over = Solo.isOver solo
        let still = holding || over
        let standing = Model.state (Solo.model solo)
        let now = DateTime.UtcNow

        // How far this drawing is between the last beat and the next. Held or over, it is pinned at
        // the beat - a board nobody is going to move again should not be caught mid-stride.
        let phase = if still then 0.0 else Pulse.phase since due now

        // The notes and the list of commands go while the board is moving, since at three beats a
        // second nobody could read them. The log stays, because what the game has just said is the
        // one thing somebody watching a board move actually wants.
        let showing =
            (if still then
                 wanted
             else
                 { Margins.none with
                     Logged = wanted.Logged })
            |> Margins.through phase

        let screen =
            String.concat
                Environment.NewLine
                [ yield (Solo.drawnAt showing Keyboard solo |> Option.defaultValue "")
                  yield! said
                  yield
                      if over then
                          "  (over) r to deal another - Enter to type a line - Esc to put it down"
                      elif holding then
                          $"  (held) {holds} to go on - r to deal another - Enter to type a line - Esc to put it down"
                      else
                          $"  {holds} to hold and read the rest - Enter to type a line - Esc to put it down" ]

        let drawn = Screens.redrawn drawn screen

        // `drawn` is passed in rather than taken from above so the one caller that wipes the
        // terminal first can say so - an identical screen is not written again, which would leave
        // the player looking at the blank it was cleared to.
        let heard holding drawn since due line solo =
            let next, posts, doing = Solo.said (stamping (Solo.game solo) ()) Keyboard line solo
            let answered = tell rings posts @ errand (Solo.game solo) sitters doing

            // What the player asked for, running clock or not: the margins this loop imposes on a
            // moving board are never written back into their choice.
            let wanted = Solo.margins Keyboard next |> Option.defaultValue wanted

            match doing with
            | Leaving _ ->
                show answered
                Solo.model next
            | Carrying
            | Keeping _ -> spin holding wanted drawn answered since due next

        let afresh () =
            let now = DateTime.UtcNow
            now, now + pulse.Every(Model.state (Solo.model solo))

        // Frames are asked for from where the game stands, so a board with nothing moving on it
        // asks for none and this waits for the beat itself.
        let waiting =
            if still then now + TimeSpan.FromDays 1.0 else Pulse.waking (pulse.Frames standing) since due now

        match Screens.awaiting waiting with
        | None when DateTime.UtcNow < due -> spin holding wanted drawn said since due solo
        | None ->
            let next, posts, doing = Solo.beaten solo
            let since = DateTime.UtcNow
            let due = since + pulse.Every(Model.state (Solo.model next))

            spin holding wanted drawn (tell rings posts @ errand (Solo.game solo) sitters doing) since due next
        | Some key ->
            match key with
            | Leaving' -> heard holding drawn since due "quit" solo
            | Holding ->
                let since, due = afresh ()
                spin (not holding) wanted drawn said since due solo
            | Restarting when still ->
                let since, due = afresh ()
                heard holding drawn since due "restart" solo
            | Restarting -> spin holding wanted drawn said since due solo
            | Typing ->
                printf "> "

                match Console.ReadLine() with
                | null -> heard holding drawn since due "quit" solo
                | line ->
                    Screens.cleared ()
                    let since, due = afresh ()
                    heard holding Screens.nothing since due line solo
            | Steering _ when over -> spin holding wanted drawn said since due solo
            | Steering line -> heard holding drawn since due line solo
            | Idle -> spin holding wanted drawn said since due solo

    if Screens.steering () then
        Screens.cleared ()
        let opened = DateTime.UtcNow

        spin
            false
            (Solo.margins Keyboard solo |> Option.defaultValue Margins.all)
            Screens.nothing
            said
            opened
            (opened + pulse.Every(Model.state (Solo.model solo)))
            solo
    else
        loop rings sitters said 0 solo

// Annotated because `Playable` and `View` both carry a field called `Rules`, and with the two of
// them now in different assemblies it is the view's - a string - that a bare `game.Rules` finds.
let private dealt (game: Playable<_, _, _>) players seed = Update.start game.Rules players seed

let private takeUp game path =
    let elsewhere other =
        match Invoked.another other with
        | Some line -> $" Take it up with '{line} replay {path}'."
        | None -> ""

    Transcript.takenUp elsewhere game path
    |> Result.map (fun (model, sitters, stamp, moves) ->
        printfn "Took up %s from %s." (Counting.several "move" "moves" moves) path
        printfn "Take them back with 'undo', or read them with 'history'."
        model, sitters, stamp)

let private machines game sitters model =
    game.Seating (Model.seed model) (Seating.machines sitters) (Model.state model)

let private serveFor game palette reach (model, sitters, stamp) =
    let keep model stamp =
        if Journal.isEmpty model.Journal then
            None
        else
            let path = Transcript.save game stamp sitters model.Journal
            Some(Path.GetRelativePath(Directory.GetCurrentDirectory(), path))

    let solo, doing =
        Solo.opened game stamp model |> Solo.against (machines game sitters model)

    errand game sitters doing |> ignore
    Server.serve reach palette solo (stamping game) keep

let private hostFor game view rings reach (model, sitters, stamp) =
    let mine, _ = Seating.awaited sitters

    let playing =
        if mine = 0 then
            None
        else
            Some(fun () ->
                match Client.join game (Reach.at reach "localhost") None (Reach.word reach) None rings view with
                | 0 ->
                    printfn ""
                    printfn "  The table is still open to whoever else is at it. Ctrl+C closes it."
                    printfn ""
                | _ -> ())

    let keep model =
        if not (Journal.isEmpty model.Journal) then
            Transcript.save game stamp sitters model.Journal |> ignore

    Server.host game reach model sitters keep playing

[<NoComparison; NoEquality>]
type private Settled<'Move, 'State, 'Notice> =
    { Ways: Playable<'Move, 'State, 'Notice> list
      Game: Playable<'Move, 'State, 'Notice>
      View: View<'Move, 'State, 'Notice>
      Rings: bool }

module private Settled =

    let ways settled =
        settled.Ways |> List.map (fun way -> way.Name, way.Blurb)

    let playing name settled =
        match settled.Ways |> List.tryFind (fun way -> way.Name = name) with
        | None -> settled
        | Some game ->
            { settled with
                Game = game
                View = Playable.recoloured settled.View.Palette game settled.View }

[<NoComparison; NoEquality>]
type private Opening<'Move, 'State, 'Notice> =
    | Play of Settled<'Move, 'State, 'Notice> * Model<'Move, 'State, 'Notice> * Sitter list * stamp: string
    | Done of code: int
    | Back

let rec private settling settled at said =
    match Screens.asking settled.View.Says said (Options.screen (List.length settled.Ways)) at with
    | None, _ -> settled
    | Some line, at ->
        match Options.choose line with
        | Error problem -> settling settled at problem
        | Ok Options.Done -> settled
        | Ok(Options.Opening Options.Audio) -> settling (listening settled 0 "") at ""
        | Ok(Options.Opening Options.Video) -> settling (watching settled 0 "") at ""
        | Ok(Options.Opening Options.Game) -> settling (playing settled 0 "") at ""
        | Ok step ->
            match wayOut settled step (fun settled said -> settling settled at said) with
            | Some again -> again
            | None -> settling settled at ""

and private listening settled at said =
    let view = settled.View

    match Screens.asking view.Says said (Options.audio settled.Rings) at with
    | None, _ -> settled
    | Some line, at ->
        match Options.chooseAudio line with
        | Error problem -> listening settled at problem
        | Ok Options.Done -> settled
        | Ok(Options.Ringing on) -> listening { settled with Rings = on } at ""
        | Ok step ->
            match wayOut settled step (fun settled said -> listening settled at said) with
            | Some again -> again
            | None -> listening settled at ""

and private watching settled at said =
    let view = settled.View

    let views =
        Playable.offered AtATerminal view.Palette settled.Game
        |> List.map (fun view -> view.Name)

    match Screens.asking view.Says said (Options.video views view.Name view.Palette) at with
    | None, _ -> settled
    | Some line, at ->
        match Options.chooseVideo view.Palette line with
        | Error problem -> watching settled at problem
        | Ok Options.Done -> settled
        | Ok(Options.Changed palette) ->
            watching
                { settled with
                    View = Playable.recoloured palette settled.Game view }
                at
                ""
        | Ok(Options.Drawn name) ->
            match Playable.byName AtATerminal view.Palette settled.Game name with
            | Ok chosen -> watching { settled with View = chosen } at ""
            | Error problem -> watching settled at problem
        | Ok step ->
            match wayOut settled step (fun settled said -> watching settled at said) with
            | Some again -> again
            | None -> watching settled at ""

and private playing settled at said =
    let view = settled.View

    match Screens.asking view.Says said (Options.game (Settled.ways settled) settled.Game.Name) at with
    | None, _ -> settled
    | Some line, at ->
        match Options.chooseGame (Settled.ways settled) line with
        | Error problem -> playing settled at problem
        | Ok Options.Done -> settled
        | Ok(Options.Playing name) -> playing (Settled.playing name settled) at ""
        | Ok step ->
            match wayOut settled step (fun settled said -> playing settled at said) with
            | Some again -> again
            | None -> playing settled at ""

and private wayOut settled step onwards =
    match step with
    | Options.Same -> Some(onwards settled "")
    | Options.Keep ->
        let settings, _ = Settings.load ()

        let kept =
            settings
            |> Settings.keeping settled.Game.Name settled.View.Name settled.View.Palette
            |> Settings.ringing settled.Rings
            |> Settings.playing (List.head settled.Ways).Name settled.Game.Name

        match Settings.save kept with
        | Ok path -> Some(onwards settled $"Kept in {path}. Every game opened from here on opens like this.")
        | Error problem -> Some(onwards settled problem)
    | _ -> None

let private starting settled choice =
    let game, view = settled.Game, settled.View

    let clocked seed =
        seed |> Option.defaultValue (clockSeed ())

    let dealing sitters seed =
        dealt game (List.length sitters) (clocked seed)
        |> Result.map (fun model -> model, sitters, stamping game ())

    match choice with
    | Menu.Deal(sitters, seed) ->
        dealing sitters seed
        |> Result.map (fun (model, sitters, stamp) -> Play(settled, model, sitters, stamp))
    | Menu.Serve(sitters, seed, reach) ->
        dealing sitters seed
        |> Result.map (fun table -> Done(serveFor game view.Palette (reach |> Option.defaultWith Reach.fresh) table))
    | Menu.Host(sitters, seed, reach) ->
        dealing sitters seed
        |> Result.map (fun table -> Done(hostFor game view settled.Rings (reach |> Option.defaultWith Reach.fresh) table))
    | Menu.Join(address, code) -> Ok(Done(Client.join game address None code None settled.Rings view))
    | Menu.Replay path ->
        takeUp game path
        |> Result.map (fun (model, sitters, stamp) -> Play(settled, model, sitters, stamp |> Option.defaultWith (stamping game)))
    | Menu.Sitting _
    | Menu.Reaching _
    | Menu.Continuing
    | Menu.Working
    | Menu.Rules
    | Menu.Looking _
    | Menu.Options
    | Menu.Leave
    | Menu.Backing
    | Menu.Waiting -> Error "That is settled at the menu. Say 'back' to go there, or name the seats."

let rec private sitting settled word sitters reach at said =
    match Screens.asking settled.View.Says said (Menu.seats settled.Game sitters reach) at with
    | None, _ -> Some(Done 0)
    | Some line, at -> answering settled word sitters reach at line sitting

and private reaching settled word sitters reach at said =
    match Screens.asking settled.View.Says said (Menu.reaches word sitters reach) at with
    | None, _ -> Some(Done 0)
    | Some line, at -> answering settled word sitters reach at line reaching

and private answering settled word sitters reach at line asked =
    let again sitters reach said =
        asked settled word sitters reach at said

    match Menu.choose settled.Game settled.View.Palette line with
    | Ok(Menu.Sitting(sitters, asked)) -> sitting settled word sitters (asked |> Option.defaultValue reach) at ""
    | Ok(Menu.Reaching(sitters, reach)) -> reaching settled word sitters reach at ""
    | Ok Menu.Waiting -> again sitters reach ""
    | Ok Menu.Backing -> None
    | Ok Menu.Leave -> Some(Done 0)
    | Ok chosen ->
        match starting settled chosen with
        | Ok opening -> Some opening
        | Error problem -> again sitters reach problem
    | Error problem -> again sitters reach problem

/// The game's own section of the menu, if it has one.
///
/// `Menu.choose` is deliberately not consulted here. At a bench, the game's words come first and
/// the menu's do not get in the way - otherwise a bench with a row called '3' would deal a game of
/// three instead, and every word a bench wanted would be one the menu had not already taken. The
/// four that navigate are answered here and by name, so nothing a game does can take them away.
let rec private working settled (aside: Aside) at said =
    match Screens.asking settled.View.Says said (aside.Screen()) at with
    | None, _ -> Some(Done 0)
    | Some line, at ->

    let again = working settled aside at

    match Commands.lowered line with
    | [] -> again ""
    | [ "back" ]
    | [ "menu" ] -> None
    | [ "quit" ]
    | [ "exit" ]
    | [ "q" ] -> Some(Done 0)
    | _ ->
        match aside.Read line with
        | Ok said -> again said
        | Error problem -> again problem

let rec private taking settled at said =
    let game = settled.Game

    let records =
        Transcript.saved ()
        |> List.filter (fun record -> record.Game = Some game.Name || record.Game = None)

    match Screens.asking settled.View.Says said (Menu.continuing game records) at with
    | None, _ -> Some(Done 0)
    | Some line, at ->
        match Menu.choose game settled.View.Palette line with
        | Ok Menu.Waiting
        | Ok Menu.Continuing -> taking settled at ""
        | Ok Menu.Backing -> None
        | Ok Menu.Leave -> Some(Done 0)
        | Ok chosen ->
            match starting settled chosen with
            | Ok opening -> Some opening
            | Error problem -> taking settled at problem
        | Error problem -> taking settled at problem

let rec private welcome settled behind at said =
    let game, view = settled.Game, settled.View

    match Screens.asking view.Says said (Menu.screen game view behind) at with
    | None, _ -> Done 0
    | Some line, at ->

    let retry problem = welcome settled behind at problem

    let again () = welcome settled behind at ""

    match Menu.choose game view.Palette line with
    | Ok Menu.Waiting -> again ()
    | Ok Menu.Backing -> if behind then Back else again ()
    | Ok Menu.Leave -> Done 0
    | Ok Menu.Rules ->
        printfn "%s" view.Rules
        Screens.held ()
        again ()
    | Ok(Menu.Looking chosen) -> welcome { settled with View = chosen } behind at ""
    | Ok Menu.Options -> welcome (settling settled 0 "") behind at ""
    | Ok Menu.Continuing ->
        match taking settled 0 "" with
        | Some opening -> opening
        | None -> again ()
    | Ok Menu.Working ->
        match game.Aside with
        | None -> again ()
        | Some aside ->
            match working settled aside 0 "" with
            | Some opening -> opening
            | None -> again ()
    | Ok(Menu.Sitting(sitters, asked)) ->
        let word = Reach.minted ()

        match sitting settled word sitters (asked |> Option.defaultValue (Reach.locked word)) 0 "" with
        | Some opening -> opening
        | None -> again ()
    | Ok chosen ->
        match starting settled chosen with
        | Ok opening -> opening
        | Error problem -> retry problem
    | Error problem -> retry problem

let private play settled sitters stamp model =
    let game = settled.Game

    let seated, doing =
        Solo.opened game stamp model |> Solo.against (machines game sitters model)

    let kept = errand game sitters doing

    let solo, posts =
        seated
        |> Solo.watching
            Keyboard
            { Margins = Margins.all
              Hushed = false
              View = settled.View }

    let said = kept @ tell settled.Rings posts

    match game.Pulse with
    | Some pulse -> racing settled.Rings sitters said pulse solo |> ignore
    | None -> loop settled.Rings sitters said 0 solo |> ignore

    0

let private opening settled launch =
    let game, view = settled.Game, settled.View

    let clocked seed =
        seed |> Option.defaultValue (clockSeed ())

    let table others start =
        match start with
        | Start.Dealt(players, seed, rivals) ->
            dealt game players (clocked seed)
            |> Result.map (fun model -> model, Seating.after players rivals |> Seating.resuming others, stamping game ())
        | Start.Saved path ->
            takeUp game path
            |> Result.map (fun (model, sitters, stamp) ->
                model, Seating.resuming others sitters, stamp |> Option.defaultWith (stamping game))

    let onward how outcome =
        match outcome with
        | Ok table -> how table
        | Error problem ->
            eprintfn "%s" problem
            1

    match launch with
    | Launch.Play start ->
        table Here start
        |> onward (fun (model, sitters, stamp) ->
            printfn "%s" view.Rules
            play settled sitters stamp model)
    | Launch.Serve(start, reach) -> table Here start |> onward (serveFor game view.Palette reach)
    | Launch.Host(start, reach) -> table Elsewhere start |> onward (hostFor game view settled.Rings reach)
    | Launch.Join(address, token, code, table) -> Client.join game address token code table settled.Rings view
    | Launch.House(reach, filling) ->
        let hosting =
            Net.Hosting.of' settled.Ways clockSeed (fun game -> Transcript.stamping game.Name DateTime.Now)

        Server.house hosting reach filling


[<NoComparison; NoEquality>]
type Chosen =
    abstract Name: string
    abstract Title: string
    abstract Blurb: string

    abstract Fewest: int
    abstract Most: int

    abstract Names: string list

    abstract As: string -> Chosen

    abstract Faults: string list

    abstract FromCommandLine: string seq -> int

    abstract FromMenu: behind: bool -> int option

let rec private making (ways: Playable<'Move, 'State, 'Notice> list) (game: Playable<'Move, 'State, 'Notice>) (asked: bool) =
    let settling settings =
        if asked then
            game
        else
            match Settings.plays (List.head ways).Name settings with
            | Some name -> ways |> List.tryFind (fun way -> way.Name = name) |> Option.defaultValue game
            | None -> game

    { new Chosen with
        member _.Name = game.Name
        member _.Title = game.Title
        member _.Blurb = game.Blurb
        member _.Fewest = game.Fewest
        member _.Most = game.Most
        member _.Names = ways |> List.map (fun way -> way.Name)

        member this.As name =
            ways
            |> List.tryFind (fun way -> way.Name = name)
            |> Option.map (fun way -> making ways way true)
            |> Option.defaultValue this

        member _.Faults = game.Faults

        member _.FromCommandLine argv =
            let settings, _ = Settings.load ()
            let opened = settling settings
            let view, _ = Playable.opening AtATerminal settings opened

            let settled =
                { Ways = ways
                  Game = opened
                  View = view
                  Rings = Settings.bell settings }

            Launch.run opened (fun view launch -> opening { settled with View = view } launch) argv

        member _.FromMenu behind =
            let settings, unread = Settings.load ()
            let opened = settling settings
            let plain, stale = Playable.opening AtATerminal settings opened

            let said = String.concat Environment.NewLine (Palette.complaints @ unread @ stale)

            let settled =
                { Ways = ways
                  Game = opened
                  View = plain
                  Rings = Settings.bell settings }

            match welcome settled behind 0 said with
            | Play(settled, model, sitters, stamp) -> Some(play settled sitters stamp model)
            | Done code -> Some code
            | Back -> None }

let chosen ways game = making ways game false


let opened (game: Chosen) open' =
    match game.Faults with
    | _ :: _ as problems ->
        eprintfn $"{game.Title} does not hang together:"
        problems |> List.iter (eprintfn "  %s")
        Some 1
    | [] -> open' game

let alone (game: Chosen) words =
    let opening =
        match words with
        | [] -> fun (game: Chosen) -> game.FromMenu false
        | launch -> fun (game: Chosen) -> Some(game.FromCommandLine launch)

    opened game opening |> Option.defaultValue 0

let only (ways: Playable<'Move, 'State, 'Notice> list) argv =
    Invoked.isTheOnlyGame ()
    alone (chosen ways (List.head ways)) (List.ofArray argv)
