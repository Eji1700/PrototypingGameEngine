// The command line: the two halves of it, and that they agree.
//
// `Shell` reads the arguments the process was started with, and `Launch` writes a command
// line the program will later be handed back - the line a dropped player is told to type
// to get back to their seat. Two libraries, each doing what it is good at, and one danger
// between them: an option renamed on one side and not the other would leave the program
// printing instructions it will not accept.
//
// So the last check here is the one that matters. It takes the line `Launch` writes, feeds
// it to the real command surface - the same `Shell.describe` the program runs, not a copy
// of it - and insists the far end comes out holding what the near end sent.
//
//   dotnet fsi tests/cli.fsx

#r "nuget: FsCheck, 3.3.3"
#r "nuget: Argu, 6.2.5"
#r "nuget: Falco.Datastar, 1.3.0"
#r "nuget: Falco.Markup, 1.4.0"
#r "nuget: Spectre.Console, 0.51.1"
#r "nuget: Spectre.Console.Cli, 0.51.1"

#load "Harness.fsx"
#load "../src/App/Messages.fs"
#load "../src/App/Session.fs"
#load "../src/App/Rival.fs"
#load "../src/App/Timeline.fs"
#load "../src/App/Journal.fs"
#load "../src/App/Model.fs"
#load "../src/App/Update.fs"
#load "../src/Console/Waiting.fs"
#load "../src/Console/Words.fs"
#load "../src/Console/Render.fs"
#load "../src/Console/Parse.fs"
#load "../src/Console/Palette.fs"
#load "../src/Console/Tint.fs"
#load "../src/Console/Rich.fs"
#load "../src/Console/Html.fs"
#load "../src/Console/View.fs"
#load "../src/Console/Launch.fs"
#load "../src/Console/Shell.fs"
#load "../src/Console/Menu.fs"

open System
open FsCheck
open FsCheck.FSharp
open TCModel.Domain
open TCModel.App
open TCModel.Console
open Harness

let private config =
    Config.QuickThrowOnFailure.WithMaxTest(200).WithQuietOnSuccess(true)

let private holds name property =
    let failure =
        try
            Check.One(config, property)
            None
        with problem ->
            Some problem.Message

    match failure with
    | None -> report name true true
    | Some message ->
        report name true false

        message.Split '\n'
        |> Array.iter (fun line -> printfn "     %s" (line.TrimEnd()))

// --- anything the program can be asked to open --------------------------------------------

let private launches =
    let players = Gen.choose (Table.MinPlayers, Table.MaxPlayers)

    let seed =
        Gen.oneof [ Gen.constant None; Gen.choose (0, 1_000_000) |> Gen.map (uint64 >> Some) ]

    // Addresses and paths as people really write them - a bare name, a name and port, a
    // whole URL, a path with a space in it that would not survive being split on spaces.
    let address =
        Gen.elements [ "greg-pc"; "192.168.1.9:5000"; "http://localhost:5000/table" ]

    let token = Gen.elements [ None; Some "a1b2c3"; Some "0f9e8d7cb6a5" ]

    let path = Gen.elements [ "logs/one.log"; "C:/Games/My Records/last night.log" ]

    // A table with the machine at some of it. Never at more seats than there are after the
    // first: the first is yours, and a line the program could not have written is not a line
    // worth asking whether it reads back.
    let dealing =
        players
        |> Gen.bind (fun players ->
            let seatings =
                [ []
                  [ Rival.easy ]
                  [ Rival.medium; Rival.hard ]
                  [ Rival.hard; Rival.easy; Rival.medium ] ]
                |> List.filter (fun rivals -> List.length rivals <= players - 1)

            Gen.zip seed (Gen.elements seatings)
            |> Gen.map (fun (seed, rivals) -> players, seed, rivals))

    Gen.oneof
        [ dealing |> Gen.map Launch.Deal
          dealing |> Gen.map Launch.Serve
          Gen.zip players seed |> Gen.map Launch.Host
          Gen.zip address token |> Gen.map Launch.Join
          path |> Gen.map Launch.Replay ]
    |> Arb.fromGen

// --- what the program writes, the program reads ----------------------------------------------

holds
    "a launch written out and read back is the same launch"
    (Prop.forAll launches (fun launch -> Launch.read (Launch.words launch) = Ok launch))

report
    "a line still carrying the runner in front of it is read all the same"
    (Ok(Launch.Join("greg-pc", Some "a1b2c3")))
    (Launch.read [ "dotnet"; "run"; "--"; "join"; "greg-pc"; "--token"; "a1b2c3" ])

report
    "a line that says nothing to open is refused"
    true
    (match Launch.read [ "--seed"; "42" ] with
     | Error problem -> problem.Contains "does not say what to open"
     | Ok _ -> false)

// --- and what the program writes, the command line accepts --------------------------------------

/// Run the real command surface over a set of arguments, and give back what it made of
/// them. The commands are `Shell.describe`'s own, so this is the program's front door and
/// not a model of it.
///
/// What it says while refusing something is caught rather than printed. Half these checks
/// hand it arguments it ought to refuse, and a run of Spectre's error panels between the
/// lines of a test report is no help to anybody reading one.
let private through (words: string list) =
    let mutable opened = None
    let said = new IO.StringWriter()
    let out, err = Console.Out, Console.Error

    let code =
        try
            Console.SetOut said
            Console.SetError said

            Shell.run
                (fun _ launch ->
                    opened <- Some launch
                    0)
                (Array.ofList words)
        finally
            Console.SetOut out
            Console.SetError err

    code, opened

holds
    "the command line accepts every line the program can write, and reads it the same way"
    (Prop.forAll launches (fun launch ->
        match through (Launch.words launch) with
        | 0, Some opened -> opened = launch
        | _ -> false))

// --- and answers a person who gets it wrong ------------------------------------------------------

// The exit code is what a script reads, and nothing may be opened on the way out.

let private turnedAway words =
    let code, opened = through words
    code <> 0 && opened = None

report "a table of nine is turned away at the door" true (turnedAway [ "play"; "9" ])

report "so is a view nobody has" true (turnedAway [ "play"; "2"; "--view"; "fancy" ])

report "so is a colour nobody has" true (turnedAway [ "play"; "2"; "--colour"; "blue=beige" ])

report "so is a colour for something nobody draws" true (turnedAway [ "play"; "2"; "--colour"; "walls=teal" ])

report "so is a colour said the wrong way round" true (turnedAway [ "play"; "2"; "--colour"; "teal" ])

report "and a command nobody has" true (turnedAway [ "frobnicate" ])

// The machine is asked for by name, and only for seats that exist. The first is yours, so a
// game for two has one to give away and a second one asked for is somebody meaning
// something else - which is worth saying at the door rather than at the table.

report "a way of playing nobody has is turned away" true (turnedAway [ "play"; "2"; "--rival"; "cunning" ])

report
    "and so are more machines than there are seats for them"
    true
    (turnedAway [ "play"; "2"; "--rival"; "easy"; "--rival"; "hard" ])

report "which `serve` says too, dealing the same table" true (turnedAway [ "serve"; "2"; "--rival"; "easy"; "--rival"; "hard" ])

// --- the defaults --------------------------------------------------------------------------------

report
    "a game asked for with no number is dealt for the fewest that can play"
    (0, Some(Launch.Deal(Table.MinPlayers, None, [])))
    (through [ "play" ])

report "a seed left unsaid is left unsaid, for the clock to answer" (0, Some(Launch.Deal(3, None, []))) (through [ "play"; "3" ])

report "and a seed given is carried through" (0, Some(Launch.Deal(3, Some 42UL, []))) (through [ "play"; "3"; "--seed"; "42" ])

report
    "how the board is drawn can be said in either spelling"
    (0, Some(Launch.Deal(2, None, [])))
    (through [ "play"; "2"; "--color"; "blue=teal" ])

report "a game with nobody said to play it is a game between people" (0, Some(Launch.Deal(3, None, []))) (through [ "play"; "3" ])

report
    "and the machines are taken in the order they were named"
    (0, Some(Launch.Deal(3, None, [ Rival.hard; Rival.easy ])))
    (through [ "play"; "3"; "--rival"; "hard"; "--rival"; "easy" ])

report
    "a browser's table takes them the same way"
    (0, Some(Launch.Serve(2, Some 42UL, [ Rival.medium ])))
    (through [ "serve"; "2"; "--seed"; "42"; "-r"; "medium" ])

// --- the other door ---------------------------------------------------------------------------
//
// Running the program with no arguments at all opens the menu instead, which asks the same
// questions in a different grammar. Only the newest part of it is held to anything here: how
// many are playing is not asked for after `vs`, because saying who you are playing has
// already said it - one seat for you and one for each machine named - and a menu that got
// that sum wrong would deal a table with an empty chair at it.

let private chosen line = Menu.choose Palette.standard line

/// A choice as something that can be compared. `Menu.Choice` carries a view, and a view is
/// a bundle of functions, so the choices cannot be held up against each other whole.
let private dealing choice =
    match choice with
    | Ok(Menu.Deal(players, seed, rivals)) -> Ok("deal", players, seed, rivals |> List.map (fun skill -> skill.Name))
    | Ok(Menu.Serve(players, seed, rivals)) -> Ok("serve", players, seed, rivals |> List.map (fun skill -> skill.Name))
    | Ok _ -> Error "that is not a game to deal"
    | Error problem -> Error problem

report
    "'vs' deals a seat for you and one for each machine named"
    (Ok("deal", 3, None, [ "easy"; "hard" ]))
    (dealing (chosen "vs easy hard"))

report
    "and the browser's table is asked for the same way"
    (Ok("serve", 2, None, [ "medium" ]))
    (dealing (chosen "serve vs medium"))

report
    "'vs' with nobody named says what to name"
    true
    (match chosen "vs" with
     | Error problem -> problem.Contains "easy, medium, hard"
     | Ok _ -> false)

report
    "and a table it would deal too big is refused, rather than dealt"
    true
    (match chosen "vs easy easy hard hard medium" with
     | Error problem -> problem.Contains $"table of 6"
     | Ok _ -> false)

report
    "a way of playing nobody has is refused here too"
    true
    (match chosen "vs cunning" with
     | Error problem -> problem.Contains "not a way for the machine to play"
     | Ok _ -> false)

// And the seatings the menu offers on its own line still mean what they meant.

report "a bare number is still a table of people" (Ok("deal", 4, None, [])) (dealing (chosen "4"))

report "and the menu says the machine is on offer" true ((Menu.screen (View.plain Palette.standard)).Contains "vs <skill>...")

finish ()
