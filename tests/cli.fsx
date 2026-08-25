#r "nuget: FsCheck, 3.3.3"

#load "Whole.fsx"

open System
open FsCheck
open FsCheck.FSharp
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Turncoats
open Harness
open Whole

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


let private certificate =
    let path = IO.Path.Combine(IO.Path.GetTempPath(), "proto-check.pfx")
    IO.File.WriteAllText(path, "not a certificate")
    path

let private launches =
    let players = Gen.choose (Table.MinPlayers, Table.MaxPlayers)

    let seed =
        Gen.oneof [ Gen.constant None; Gen.choose (0, 1_000_000) |> Gen.map (uint64 >> Some) ]

    let address =
        Gen.elements [ "greg-pc"; "192.168.1.9:5000"; "http://localhost:5000/table" ]

    let token = Gen.elements [ None; Some "a1b2c3"; Some "0f9e8d7cb6a5" ]

    let code = Gen.elements [ None; Some "kbd4-9mtx-7rfp" ]

    let table = Gen.elements [ None; Some "kbd4-9mtx-7rfp"; Some "7rfp-kbd4-9mtx" ]

    let path = Gen.elements [ "logs/one.log"; "C:/Games/My Records/last night.log" ]

    let reach =
        Gen.elements
            [ Reach.ajar
              { Reach.ajar with
                  Doorway = Locked "kbd4-9mtx-7rfp" }
              { Reach.ajar with
                  Port = 8443
                  Doorway = Locked "one-two-three" }
              { Reach.ajar with
                  Wrapping = Ahead
                  Address = Some "https://stones.example.org" }
              { Reach.ajar with
                  Wrapping = Kept(certificate, None)
                  Address = Some "stones.example.org" }
              { Reach.ajar with
                  Port = 443
                  Wrapping = Kept(certificate, Some "hunter two") } ]

    let dealing =
        players
        |> Gen.bind (fun players ->
            let seatings =
                [ []; [ "easy" ]; [ "medium"; "hard" ]; [ "hard"; "easy"; "medium" ] ]
                |> List.filter (fun rivals -> List.length rivals <= players - 1)

            Gen.zip seed (Gen.elements seatings)
            |> Gen.map (fun (seed, rivals) -> players, seed, rivals))

    let starting =
        Gen.oneof [ dealing |> Gen.map Start.Dealt; path |> Gen.map Start.Saved ]

    let hosting =
        Gen.oneof
            [ Gen.zip players seed
              |> Gen.map (fun (players, seed) -> Start.Dealt(players, seed, []))
              path |> Gen.map Start.Saved ]

    Gen.oneof
        [ starting |> Gen.map Launch.Play
          Gen.zip starting reach |> Gen.map Launch.Serve
          Gen.zip hosting reach |> Gen.map Launch.Host
          Gen.zip (Gen.zip address token) (Gen.zip code table)
          |> Gen.map (fun ((address, token), (code, table)) -> Launch.Join(address, token, code, table))
          Gen.zip reach (Gen.elements [ true; false ]) |> Gen.map Launch.House ]
    |> Arb.fromGen


holds
    "a launch written out and read back is the same launch"
    (Prop.forAll launches (fun launch -> Launch.read playing (Launch.words launch) = Ok launch))

report
    "a line still carrying the runner in front of it is read all the same"
    (Ok(Launch.Join("greg-pc", Some "a1b2c3", None, None)))
    (Launch.read playing [ "dotnet"; "run"; "--"; "join"; "greg-pc"; "--token"; "a1b2c3" ])

report
    "a console can say which table of a house it means"
    (Ok(Launch.Join("greg-pc", None, None, Some "kbd4-9mtx-7rfp")))
    (Launch.read playing [ "join"; "greg-pc"; "--table"; "kbd4-9mtx-7rfp" ])

report
    "a line that says nothing to open is refused, saying what there is to open"
    true
    (match Launch.read playing [] with
     | Error problem -> problem.Contains "does not say what to open" && problem.Contains "replay"
     | Ok _ -> false)

report
    "and one that is all options and no command is refused where the first of them is"
    true
    (match Launch.read playing [ "--seed"; "42" ] with
     | Error problem -> problem.Contains "seed"
     | Ok _ -> false)


let private through (words: string list) =
    let mutable opened = None
    let said = new IO.StringWriter()
    let out, err = Console.Out, Console.Error

    let code =
        try
            Console.SetOut said
            Console.SetError said

            Launch.run
                playing
                (fun _ launch ->
                    opened <- Some launch
                    0)
                words
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


let private turnedAway words =
    let code, opened = through words
    code <> 0 && opened = None

report "a table of nine is turned away at the door" true (turnedAway [ "play"; "9" ])

report "so is a view nobody has" true (turnedAway [ "play"; "2"; "--view"; "fancy" ])

report "so is a colour nobody has" true (turnedAway [ "play"; "2"; "--colour"; "blue=beige" ])

report "so is a colour for something nobody draws" true (turnedAway [ "play"; "2"; "--colour"; "walls=teal" ])

report "so is a colour said the wrong way round" true (turnedAway [ "play"; "2"; "--colour"; "teal" ])

report "and a command nobody has" true (turnedAway [ "frobnicate" ])


report "an option nobody has is turned away too" true (turnedAway [ "play"; "2"; "--vew"; "rich" ])

report "including one that is nearly the word at a door" true (turnedAway [ "host"; "3"; "--cod"; "sesame" ])


report "an address that is not one is refused before a table is opened" true (turnedAway [ "host"; "3"; "--at"; "my table" ])


report "a way of playing nobody has is turned away" true (turnedAway [ "play"; "2"; "--rival"; "cunning" ])

report
    "and so are more machines than there are seats for them"
    true
    (turnedAway [ "play"; "2"; "--rival"; "easy"; "--rival"; "hard" ])

report "which `serve` says too, dealing the same table" true (turnedAway [ "serve"; "2"; "--rival"; "easy"; "--rival"; "hard" ])


let private saved = "logs/2026-08-02-215823-2p-seed42.log"

report "a count said alongside a record is refused" true (turnedAway [ "play"; "2"; "--from"; saved ])

report "and a seed" true (turnedAway [ "play"; "--from"; saved; "--seed"; "42" ])

report "and a machine" true (turnedAway [ "serve"; "--from"; saved; "--rival"; "hard" ])

report
    "and the refusal says which of the two to drop"
    true
    (match Launch.read playing [ "play"; "2"; "--from"; saved ] with
     | Error problem -> problem.Contains "--from" && problem.Contains "how many are playing"
     | Ok _ -> false)


report
    "a game asked for with no number is dealt for the fewest that can play"
    (0, Some(Launch.Play(Start.Dealt(Table.MinPlayers, None, []))))
    (through [ "play" ])

report
    "a seed left unsaid is left unsaid, for the clock to answer"
    (0, Some(Launch.Play(Start.Dealt(3, None, []))))
    (through [ "play"; "3" ])

report
    "and a seed given is carried through"
    (0, Some(Launch.Play(Start.Dealt(3, Some 42UL, []))))
    (through [ "play"; "3"; "--seed"; "42" ])

report
    "how the board is drawn can be said in either spelling"
    (0, Some(Launch.Play(Start.Dealt(2, None, []))))
    (through [ "play"; "2"; "--color"; "blue=teal" ])

report
    "a game with nobody said to play it is a game between people"
    (0, Some(Launch.Play(Start.Dealt(3, None, []))))
    (through [ "play"; "3" ])

report
    "and the machines are taken in the order they were named"
    (0, Some(Launch.Play(Start.Dealt(3, None, [ "hard"; "easy" ]))))
    (through [ "play"; "3"; "--rival"; "hard"; "--rival"; "easy" ])


let private without launch =
    match launch with
    | Launch.Serve(start, reach) -> Launch.Serve(start, { reach with Doorway = Ajar })
    | Launch.Host(start, reach) -> Launch.Host(start, { reach with Doorway = Ajar })
    | opened -> opened

let private unlocked words =
    let code, opened = through words
    code, opened |> Option.map without

report
    "a browser's table takes them the same way"
    (0, Some(Launch.Serve(Start.Dealt(2, Some 42UL, [ "medium" ]), Reach.ajar)))
    (unlocked [ "serve"; "2"; "--seed"; "42"; "-r"; "medium" ])


report
    "a saved game can be taken up at this keyboard"
    (0, Some(Launch.Play(Start.Saved saved)))
    (through [ "play"; "--from"; saved ])

report "or in a browser" (0, Some(Launch.Serve(Start.Saved saved, Reach.ajar))) (unlocked [ "serve"; "--from"; saved; "--open" ])

report
    "or as a table others join"
    (0, Some(Launch.Host(Start.Saved saved, Reach.ajar)))
    (unlocked [ "host"; "--from"; saved; "--open" ])


report
    "and 'replay' is the short way of saying the same thing"
    (through [ "play"; "--from"; saved ])
    (through [ "replay"; saved ])


let private door words =
    match through words with
    | 0, Some(Launch.Host(_, reach)) -> Ok reach.Doorway
    | 0, Some(Launch.Serve(_, reach)) -> Ok reach.Doorway
    | code, _ -> Error code

report
    "a table opened with nothing said about its door gets a word for it"
    true
    (match door [ "host"; "3" ] with
     | Ok(Locked code) -> code.Length >= 12
     | Ok Ajar
     | Error _ -> false)

report
    "and so does a game served to a browser"
    true
    (match door [ "serve"; "2" ] with
     | Ok(Locked _) -> true
     | _ -> false)

report "a word given is the word used" (Ok(Locked "open sesame")) (door [ "host"; "3"; "--code"; "open sesame" ])

report "and a table said to be open has no word at all" (Ok Ajar) (door [ "host"; "3"; "--open" ])

report "the same word twice running is the same word" (door [ "host"; "3" ]) (door [ "serve"; "2" ])


report "a table cannot be both open and locked" true (turnedAway [ "host"; "3"; "--open"; "--code"; "sesame" ])

report "nor be its own word for nothing" true (turnedAway [ "host"; "3"; "--code"; "  " ])

report "a certificate nobody has is refused before anything is dealt" true (turnedAway [ "host"; "3"; "--cert"; "nowhere.pfx" ])

report
    "and https cannot both end here and end in front of this"
    true
    (turnedAway [ "host"; "3"; "--cert"; certificate; "--behind" ])

report
    "a password with no certificate to unlock says so"
    true
    (turnedAway [ "host"; "3"; "--behind"; "--cert-password"; "hunter two" ])

report "and a port nobody has is not a port" true (turnedAway [ "host"; "3"; "--port"; "70000" ])

report
    "a table behind something that holds the certificate speaks https all the same"
    (0,
     Some(
         Launch.Host(
             Start.Dealt(3, None, []),
             { Reach.ajar with
                 Wrapping = Ahead
                 Address = Some "stones.example.org" }
         )
     ))
    (unlocked [ "host"; "3"; "--behind"; "--at"; "stones.example.org"; "--open" ])


let private chosen line = Menu.choose playing standard line

let private settings palette =
    let drawn =
        Playable.offered AtATerminal palette playing |> List.map (fun view -> view.Name)

    Options.video drawn (List.head drawn) palette

let private dealing choice =
    match choice with
    | Ok(Menu.Deal(sitters, seed)) -> Ok("deal", Seating.line sitters, seed)
    | Ok(Menu.Serve(sitters, seed, _)) -> Ok("serve", Seating.line sitters, seed)
    | Ok(Menu.Host(sitters, seed, _)) -> Ok("host", Seating.line sitters, seed)
    | Ok(Menu.Sitting(sitters, _)) -> Ok("seats", Seating.line sitters, None)
    | Ok _ -> Error "that is not a game to deal"
    | Error problem -> Error problem

let private reaching choice =
    match choice with
    | Ok(Menu.Serve(_, _, reach))
    | Ok(Menu.Host(_, _, reach))
    | Ok(Menu.Sitting(_, reach)) -> Ok(reach |> Option.map Reach.line)
    | Ok(Menu.Reaching(_, reach)) -> Ok(Some(Reach.line reach))
    | Ok _ -> Error "that is not a table to open"
    | Error problem -> Error problem

report
    "'vs' deals a seat for you and one for each machine named"
    (Ok("deal", "you easy hard", None))
    (dealing (chosen "vs easy hard"))

report "and the browser's table is asked for the same way" (Ok("serve", "you medium", None)) (dealing (chosen "serve vs medium"))

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


report "a bare number is still a table of people" (Ok("deal", "you you you you", None)) (dealing (chosen "4"))

report "and it still takes a seed after it" (Ok("deal", "you you", Some 42UL)) (dealing (chosen "2 42"))

report "a seat may be named outright, one word each" (Ok("deal", "you hard you", None)) (dealing (chosen "play you hard you"))

report "and the machine may have the first of them" (Ok("deal", "medium you", None)) (dealing (chosen "play medium you"))

report
    "anybody joining makes it a table to open rather than a game to deal"
    (Ok("host", "you hard joins", None))
    (dealing (chosen "play you hard joins"))

report "which is all 'host' ever meant, said shorter" (Ok("host", "joins joins joins", None)) (dealing (chosen "host 3"))

report "and 'vs' is the long way round too" (dealing (chosen "play you medium")) (dealing (chosen "vs medium"))

report
    "a seating the table would refuse is refused where it is named"
    true
    (match chosen "play you" with
     | Error problem -> problem.Contains "table of 1"
     | Ok _ -> false)

report
    "and a word nobody has is refused there too, saying what there is"
    true
    (match chosen "play you sneaky you" with
     | Error problem -> problem.Contains "is not somebody to seat"
     | Ok _ -> false)


report
    "a browser's game cannot have anybody joining it"
    true
    (match chosen "serve you joins" with
     | Error problem -> problem.Contains "one hot seat"
     | Ok _ -> false)

report
    "and the menu says the machine is on offer"
    true
    ((Keys.draw None (Menu.screen playing plain false)).Contains "vs <skill>...")


report
    "a table joined from the menu carries the word at its door"
    true
    (match chosen "join stones.example.org kbd4-9mtx-7rfp" with
     | Ok(Menu.Join(address, Some code)) -> address = "stones.example.org" && code = "kbd4-9mtx-7rfp"
     | _ -> false)

report
    "and one with no word said says none"
    true
    (match chosen "join greg-pc" with
     | Ok(Menu.Join(_, None)) -> true
     | _ -> false)


let private front = Menu.screen playing plain false

let rec private everyRow (screen: Keys.Screen) =
    screen.Rows
    |> List.collect (fun row ->
        row
        :: (match row.Pick with
            | Keys.Opens under -> everyRow under
            | _ -> []))

let rec private everyScreen (screen: Keys.Screen) =
    screen
    :: (screen.Rows
        |> List.collect (fun row ->
            match row.Pick with
            | Keys.Opens under -> everyScreen under
            | _ -> []))

let private linesOf (row: Keys.Row) =
    (match row.Pick with
     | Keys.Sends line -> [ line ]
     | Keys.Types text -> [ text + "5000" ]
     | Keys.Opens _ -> [])
    @ (match row.Turns with
       | Some turn -> [ turn -1; turn 1 ]
       | None -> [])

let private unread reader screen =
    everyRow screen
    |> List.collect linesOf
    |> List.filter (fun line ->
        match reader line with
        | Ok _ -> false
        | Error _ -> true)

report "every row the menu offers stands for a line the menu itself can read" [] (unread chosen front)

report
    "and the same on the colour screen, where left and right walk a slot through the colours"
    []
    (unread (Options.chooseVideo standard) (settings standard))


let private everySeating =
    let rec grown seats =
        if seats = 0 then
            [ [] ]
        else
            grown (seats - 1)
            |> List.collect (fun rest -> Seating.all playing.Skills |> List.map (fun sitter -> sitter :: rest))

    [ Table.MinPlayers .. Table.MaxPlayers ] |> List.collect grown

let private everyReach =
    [ Reach.ajar
      Reach.locked "kbd4-9mtx-7rfp"
      { Reach.locked "kbd4-9mtx-7rfp" with
          Port = 8443
          Wrapping = Ahead
          Address = Some "stones.example.org" }
      { Reach.ajar with
          Wrapping = Kept("stones.pfx", None) } ]

report
    "and every row on every seat list there could be stands for a line the menu can read"
    []
    (everySeating
     |> List.collect (fun sitters ->
         everyReach
         |> List.collect (fun reach -> unread chosen (Menu.seats playing sitters reach))))

report
    "and so does every row on the screen behind it, whatever it is holding"
    []
    (everySeating
     |> List.truncate 40
     |> List.collect (fun sitters ->
         everyReach
         |> List.collect (fun reach -> unread chosen (Menu.reaches "kbd4-9mtx-7rfp" sitters reach))))

report
    "a front door with a list of games behind it can be walked back out of"
    true
    (front.Backs = None && (Menu.screen playing plain true).Backs = Some "back")

report
    "and that way out is a line the menu reads as going back"
    true
    (match (Menu.screen playing plain true).Backs with
     | Some line ->
         match chosen line with
         | Ok Menu.Backing -> true
         | _ -> false
     | None -> false)

report
    "the way back out of the seat list is a line too, and it goes back rather than dealing"
    true
    (match (Menu.seats playing (Seating.here 2) Reach.ajar).Backs with
     | Some line ->
         match chosen line with
         | Ok Menu.Backing -> true
         | _ -> false
     | None -> false)

report
    "and the way back out of the one behind it lands at the seats, holding what was settled"
    (Ok(Some "port:8443 open behind at:stones.example.org"))
    (match
        (Menu.reaches
            "kbd4-9mtx-7rfp"
            (Seating.hosting 2)
            { Reach.ajar with
                Port = 8443
                Wrapping = Ahead
                Address = Some "stones.example.org" })
            .Backs
     with
     | Some line -> reaching (chosen line)
     | None -> Error "there was no way back")

report
    "the way back out of a screen is a line too"
    true
    (match (settings standard).Backs with
     | Some line ->
         match Options.chooseVideo standard line with
         | Ok Options.Done -> true
         | _ -> false
     | None -> false)


let private waysOffered =
    [ "compile", "Draft three protocols and play across the table."
      "compile-control", "The same, with the optional rule." ]

report
    "every row on the settings menu stands for a line the menu itself reads"
    []
    (unread Options.choose (Options.screen (List.length waysOffered)))

report
    "and every row on the Audio page, including the one left and right turn over"
    []
    (unread Options.chooseAudio (Options.audio true)
     @ unread Options.chooseAudio (Options.audio false))

report
    "and every row on the Game page, whichever way is in play"
    []
    (waysOffered
     |> List.collect (fun (name, _) -> unread (Options.chooseGame waysOffered) (Options.game waysOffered name)))

report
    "a game with one way to play it still draws a page that reads"
    []
    (unread (Options.chooseGame [ List.head waysOffered ]) (Options.game [ List.head waysOffered ] "compile"))

report
    "the way back out of each page is a line that page reads as going back"
    [ true; true; true; true ]
    ([ Options.screen 2, Options.choose
       Options.audio true, Options.chooseAudio
       Options.game waysOffered "compile", Options.chooseGame waysOffered
       settings standard, Options.chooseVideo standard ]
     |> List.map (fun ((screen: Keys.Screen), reader) ->
         match screen.Backs with
         | Some line ->
             match reader line with
             | Ok Options.Done -> true
             | _ -> false
         | None -> false))

report
    "'save' is a line every page reads as keeping the lot"
    [ true; true; true ]
    ([ Options.chooseAudio
       Options.chooseGame waysOffered
       Options.chooseVideo standard ]
     |> List.map (fun reader ->
         match reader "save" with
         | Ok Options.Keep -> true
         | _ -> false))


let private saidBack settings =
    Settings.write settings |> Settings.read |> fst

report
    "a bell turned off is written down and read back off"
    (false, true)
    (Settings.none |> Settings.ringing false |> saidBack |> Settings.bell,
     Settings.none |> Settings.ringing true |> saidBack |> Settings.bell)

report
    "and nobody having said anything about it rings, which is what every table did before there was a way to say"
    true
    (Settings.bell Settings.none)

report
    "a way of playing is written down under the game that offers it, and read back"
    (Some "compile-control")
    (Settings.none
     |> Settings.playing "compile" "compile-control"
     |> saidBack
     |> Settings.plays "compile")

report
    "keeping this game's colours does not forget which way it is being played"
    (Some "compile-control", Some "plain")
    (let kept =
        Settings.none
        |> Settings.playing "compile" "compile-control"
        |> Settings.keeping "compile" "plain" standard
        |> saidBack

     Settings.plays "compile" kept, Settings.drawn "compile" kept)

report
    "a way of playing said above the games is complained about rather than swallowed"
    true
    (Settings.read "plays compile-control" |> snd |> List.isEmpty |> not)

report
    "and a bell said under one game is too, being nobody's game in particular"
    true
    (Settings.read "[compile]\nbell on" |> snd |> List.isEmpty |> not)

report
    "a bell that is neither on nor off is a complaint and not a silence"
    true
    (Settings.read "bell sometimes" |> snd |> List.isEmpty |> not)

report
    "and no two rows on one screen answer to the same number"
    []
    (everyScreen front
     |> List.collect (fun screen -> screen.Rows |> List.choose (fun row -> row.Digit) |> List.countBy id)
     |> List.filter (fun (_, many) -> many > 1))


let private key press =
    ConsoleKeyInfo('\000', press, false, false, false)

let private letter (typed: char) =
    ConsoleKeyInfo(typed, enum<ConsoleKey> 0, false, false, false)

let private walkingAt at screen keys =
    let rec next standing keys =
        match keys with
        | [] -> None
        | key :: rest ->
            match Keys.answer (Keys.pressed (Keys.typing standing) key) standing with
            | Keys.Steering standing -> next standing rest
            | Keys.Answered line -> Some line

    next (Keys.standing screen at) keys

let private walking screen keys = walkingAt 0 screen keys

let private walked keys = walking front keys

report "the number of a row picks it outright" (Some "settings") (walked [ letter '5' ])

report
    "and a number on the list it opens is the answer to what that list asked"
    (Some "seats you you you")
    (walked [ letter '1'; letter '3' ])

report
    "which is the same line typing it would have sent"
    (Ok("seats", "you you you", None))
    (dealing (chosen "seats you you you"))

let private taking down =
    walked (List.replicate 5 down @ [ key ConsoleKey.Enter ])

report "the arrows walk down the list" (Some "rules") (taking (key ConsoleKey.DownArrow))

report "and w and s are the same two keys" (Some "rules") (taking (letter 's'))

report "up from the top of the list is the bottom of it" (Some "quit") (walked [ letter 'w'; key ConsoleKey.Enter ])

report
    "a row that needs more writes the part it knows and waits for the rest"
    (Some "join elsewhere")
    (walked (
        [ letter '2' ]
        @ ([ 'e'; 'l'; 's'; 'e'; 'w'; 'h'; 'e'; 'r'; 'e' ] |> List.map letter)
        @ [ key ConsoleKey.Enter ]
    ))


report
    "and once a line is underway the steering letters are letters again"
    (Some "join sad")
    (walked (
        [ 'j'; 'o'; 'i'; 'n'; ' '; 's'; 'a'; 'd' ]
        |> List.map letter
        |> fun keys -> keys @ [ key ConsoleKey.Enter ]
    ))

// The prompt is a mode, and this is why it had to become one. Reading "is somebody typing" off
// "has anything been typed yet" left every line beginning with w, a, s or d impossible to say: the
// first press steered instead of starting the line, so there was never a line under way for the
// rest of it to belong to. The space bar hands the keyboard over, and then the four are letters.
report
    "a line may begin with one of the steering letters, once the prompt is open"
    (Some "settings")
    (walked (
        [ key ConsoleKey.Spacebar ]
        @ ([ 's'; 'e'; 't'; 't'; 'i'; 'n'; 'g'; 's' ] |> List.map letter)
        @ [ key ConsoleKey.Enter ]
    ))

report
    "and the space that opened it is not one of the letters"
    (Some "rules")
    (walked (
        [ key ConsoleKey.Spacebar ]
        @ ([ 'r'; 'u'; 'l'; 'e'; 's' ] |> List.map letter)
        @ [ key ConsoleKey.Enter ]
    ))

report
    "with the prompt shut those same letters still steer"
    (Some "rules")
    (walked (List.replicate 5 (letter 's') @ [ key ConsoleKey.Enter ]))

report
    "escape leaves the prompt rather than the screen, and what was half-typed goes with it"
    (Some "quit")
    (walked (
        [ key ConsoleKey.Spacebar ]
        @ ([ 's'; 'a'; 'v' ] |> List.map letter)
        @ [ key ConsoleKey.Escape; letter '7' ]
    ))

report
    "and rubbing a line back to nothing leaves the prompt as well"
    (Some "quit")
    (walked
        [ key ConsoleKey.Spacebar
          letter 'x'
          key ConsoleKey.Backspace
          key ConsoleKey.Backspace
          letter '7' ])

report
    "a row that writes the beginning of a line hands the keyboard over with it"
    (Some "join wsad")
    (walked (
        [ letter '2' ]
        @ ([ 'w'; 's'; 'a'; 'd' ] |> List.map letter)
        @ [ key ConsoleKey.Enter ]
    ))

report
    "backing out of a list opened by mistake comes back to the one it was opened from"
    (Some "quit")
    (walked [ letter '1'; key ConsoleKey.Escape; letter '7' ])

report
    "and backing out of the front door does nothing, there being nothing behind it"
    (Some "quit")
    (walked [ key ConsoleKey.Escape; letter '7' ])


let private pressed press sitters =
    match walking (Menu.seats playing sitters Reach.ajar) [ press ] with
    | Some line ->
        match chosen line with
        | Ok(Menu.Sitting(changed, _)) -> changed
        | _ -> sitters
    | None -> sitters

let private walkingSeat times press =
    List.replicate times press
    |> List.fold (fun sitters press -> pressed press sitters) (Seating.here 3)

report "right walks a seat on to the machine" "easy you you" (Seating.line (walkingSeat 1 (key ConsoleKey.RightArrow)))

report
    "and on again, through the machines and out the far side"
    "joins you you"
    (Seating.line (walkingSeat 4 (key ConsoleKey.RightArrow)))

report
    "walking a seat right round comes back where it started"
    "you you you"
    (Seating.line (walkingSeat (List.length (Seating.all playing.Skills)) (key ConsoleKey.RightArrow)))

report "and left from the first is the last" "joins you you" (Seating.line (walkingSeat 1 (key ConsoleKey.LeftArrow)))

report
    "the seat's own number walks it too, the same as the arrow"
    (Seating.line (walkingSeat 1 (key ConsoleKey.RightArrow)))
    (Seating.line (pressed (letter '1') (Seating.here 3)))


let private under sitters =
    (Menu.seats playing sitters Reach.ajar).Rows
    |> List.skip (List.length sitters)
    |> List.map (fun row -> row.Says)

report
    "a seating nobody is joining is dealt here, or read in a browser"
    [ "Deal"; "In a browser"; "How it is reached" ]
    (under (Seating.here 2))

report
    "and one somebody is joining is a table to open, and nothing else"
    [ "Open the table"; "How it is reached" ]
    (under (Seating.hosting 2))

report
    "picking the row that opens it opens exactly the seating on the screen"
    (Ok("host", "you hard joins", None))
    (dealing (
        chosen (
            walking (Menu.seats playing [ Here; Machine "hard"; Elsewhere ] Reach.ajar) [ letter '4' ]
            |> Option.defaultValue ""
        )
    ))


report
    "and exactly the reach it was holding"
    (Ok(Some "port:5000 word:kbd4-9mtx-7rfp behind"))
    (reaching (
        chosen (
            walking
                (Menu.seats
                    playing
                    (Seating.hosting 2)
                    { Reach.locked "kbd4-9mtx-7rfp" with
                        Wrapping = Ahead })
                [ letter '3' ]
            |> Option.defaultValue ""
        )
    ))


let private settling presses reach =
    match walking (Menu.reaches "kbd4-9mtx-7rfp" (Seating.hosting 2) reach) presses with
    | Some line ->
        match chosen line with
        | Ok(Menu.Reaching(_, changed)) -> changed
        | _ -> reach
    | None -> reach

report
    "the door walks open, and back to the word it was holding"
    [ "port:5000 open clear"; "port:5000 word:kbd4-9mtx-7rfp clear" ]
    [ Reach.line (settling [ letter '1' ] (Reach.locked "kbd4-9mtx-7rfp"))
      Reach.line (settling [ letter '1' ] (settling [ letter '1' ] (Reach.locked "kbd4-9mtx-7rfp"))) ]

report
    "and what carries it walks from the clear to behind whatever holds the certificate"
    "port:5000 word:kbd4-9mtx-7rfp behind"
    (Reach.line (settling [ key ConsoleKey.DownArrow; key ConsoleKey.RightArrow ] (Reach.locked "kbd4-9mtx-7rfp")))


let private typed row rest reach =
    match
        walking
            (Menu.reaches "kbd4-9mtx-7rfp" (Seating.hosting 2) reach)
            ([ row ] @ (rest |> Seq.map letter |> List.ofSeq) @ [ key ConsoleKey.Enter ])
    with
    | Some line ->
        match chosen line with
        | Ok(Menu.Reaching(_, changed)) -> Reach.line changed
        | Ok _ -> "that was not the screen it came back to"
        | Error problem -> problem
    | None -> "nothing came back"

report
    "the port is typed, and lands on the end of the line it was already holding"
    "port:8443 word:kbd4-9mtx-7rfp clear"
    (typed (letter '3') "8443" (Reach.locked "kbd4-9mtx-7rfp"))

report
    "and so is the address players are told"
    "port:5000 word:kbd4-9mtx-7rfp clear at:stones.example.org"
    (typed (letter '4') "stones.example.org" (Reach.locked "kbd4-9mtx-7rfp"))

report
    "and a port nobody has is refused there, in the words it was typed in"
    true
    ((typed (letter '3') "70000" (Reach.locked "kbd4-9mtx-7rfp")).Contains "is not a port")


let private colours = settings standard

[<Literal>]
let private FirstSlot = 1

let private walkedRight times palette =
    List.replicate times (key ConsoleKey.RightArrow)
    |> List.fold
        (fun palette press ->
            match walkingAt FirstSlot (settings palette) [ press ] with
            | Some line ->
                match Options.chooseVideo palette line with
                | Ok(Options.Changed changed) -> changed
                | _ -> palette
            | None -> palette)
        palette

let private along steps =
    let names = Palette.shades |> List.map (fun shade -> shade.Name)

    let at =
        names
        |> List.tryFindIndex (fun name -> name = (Palette.shadeOf "red" standard).Name)
        |> Option.defaultValue 0

    names[((at + steps) % names.Length + names.Length) % names.Length]

report
    "right walks a slot on to the next colour"
    (Some $"red {along 1}")
    (walkingAt FirstSlot colours [ key ConsoleKey.RightArrow ])

report
    "and left to the one before, which from the first is the last"
    (Some $"red {along -1}")
    (walkingAt FirstSlot colours [ key ConsoleKey.LeftArrow ])

report
    "walking right round the colours comes back where it started"
    [ along 1; along 0 ]
    [ (Palette.shadeOf "red" (walkedRight 1 standard)).Name
      (Palette.shadeOf "red" (walkedRight (List.length Palette.shades) standard)).Name ]


report "right walks the top row on to the next way of drawing" (Some "view rich") (walking colours [ key ConsoleKey.RightArrow ])


report
    "and the cursor comes back where it was left, however deep it went"
    3
    (let rec next standing keys =
        match keys with
        | [] -> Keys.started standing
        | key :: rest ->
            match Keys.answer (Keys.pressed (Keys.typing standing) key) standing with
            | Keys.Steering standing -> next standing rest
            | Keys.Answered _ -> Keys.started standing

     next
         (Keys.standing colours 0)
         [ key ConsoleKey.DownArrow
           key ConsoleKey.DownArrow
           key ConsoleKey.DownArrow
           key ConsoleKey.RightArrow ])

finish ()
