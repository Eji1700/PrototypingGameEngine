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

open System
open FsCheck
open FsCheck.FSharp
open TCModel.Domain
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

    Gen.oneof
        [ Gen.zip players seed |> Gen.map Launch.Deal
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

// --- the defaults --------------------------------------------------------------------------------

report
    "a game asked for with no number is dealt for the fewest that can play"
    (0, Some(Launch.Deal(Table.MinPlayers, None)))
    (through [ "play" ])

report "a seed left unsaid is left unsaid, for the clock to answer" (0, Some(Launch.Deal(3, None))) (through [ "play"; "3" ])

report "and a seed given is carried through" (0, Some(Launch.Deal(3, Some 42UL))) (through [ "play"; "3"; "--seed"; "42" ])

report
    "how the board is drawn can be said in either spelling"
    (0, Some(Launch.Deal(2, None)))
    (through [ "play"; "2"; "--color"; "blue=teal" ])

finish ()
