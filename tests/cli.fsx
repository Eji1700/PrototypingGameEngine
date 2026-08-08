// The command line, both ways round.
//
// `Launch` reads the arguments the process was started with, and writes a command line the
// program will later be handed back - the line a dropped player is told to type to get back
// to their seat. Those two used to be different libraries, and the danger between them was an
// option renamed on one side and not the other, which would leave the program printing
// instructions it would not accept. It happened: `--cert-password` on one side was
// `--certpassword` on the other, and the round trip below is what said so.
//
// They are one declaration now, so that particular way of being wrong is gone by
// construction. What the round trip is still worth is everything else: a line the program
// writes has to come back holding the very launch it was written from, whatever the surface
// grows next.
//
//   dotnet fsi tests/cli.fsx

#r "nuget: FsCheck, 3.3.3"

#load "Whole.fsx"

open System
open FsCheck
open FsCheck.FSharp
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.Domain
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

// --- anything the program can be asked to open --------------------------------------------

/// Somewhere for a certificate to be.
///
/// The command line looks for the file before it opens anything, which is worth doing - a
/// table that fell over on the certificate after dealing would have taken the game with it -
/// and means a line naming one has to name a real file if the far end of the round trip is
/// to accept it. Nothing here reads it, and nothing here is a certificate.
let private certificate =
    let path = IO.Path.Combine(IO.Path.GetTempPath(), "tcmodel-check.pfx")
    IO.File.WriteAllText(path, "not a certificate")
    path

let private launches =
    let players = Gen.choose (Table.MinPlayers, Table.MaxPlayers)

    let seed =
        Gen.oneof [ Gen.constant None; Gen.choose (0, 1_000_000) |> Gen.map (uint64 >> Some) ]

    // Addresses and paths as people really write them - a bare name, a name and port, a
    // whole URL, a path with a space in it that would not survive being split on spaces.
    let address =
        Gen.elements [ "greg-pc"; "192.168.1.9:5000"; "http://localhost:5000/table" ]

    let token = Gen.elements [ None; Some "a1b2c3"; Some "0f9e8d7cb6a5" ]

    let code = Gen.elements [ None; Some "kbd4-9mtx-7rfp" ]

    let path = Gen.elements [ "logs/one.log"; "C:/Games/My Records/last night.log" ]

    // How far a table can be reached, said every way the program can write one. These are in
    // here for the round trip below: a table now says several things about its own door on
    // its way out and has to come back holding all of them, and an option spelt one way by
    // the writer and another by the reader is precisely the failure this file exists for.
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

    // A table with the machine at some of it. Never at more seats than there are after the
    // first: the first is yours, and a line the program could not have written is not a line
    // worth asking whether it reads back.
    let dealing =
        players
        |> Gen.bind (fun players ->
            let seatings =
                [ []; [ "easy" ]; [ "medium"; "hard" ]; [ "hard"; "easy"; "medium" ] ]
                |> List.filter (fun rivals -> List.length rivals <= players - 1)

            Gen.zip seed (Gen.elements seatings)
            |> Gen.map (fun (seed, rivals) -> players, seed, rivals))

    Gen.oneof
        [ dealing |> Gen.map Launch.Deal
          Gen.zip dealing reach
          |> Gen.map (fun ((players, seed, rivals), reach) -> Launch.Serve(players, seed, rivals, reach))
          Gen.zip (Gen.zip players seed) reach
          |> Gen.map (fun ((players, seed), reach) -> Launch.Host(players, seed, reach))
          Gen.zip (Gen.zip address token) code
          |> Gen.map (fun ((address, token), code) -> Launch.Join(address, token, code))
          path |> Gen.map Launch.Replay ]
    |> Arb.fromGen

// --- what the program writes, the program reads ----------------------------------------------

holds
    "a launch written out and read back is the same launch"
    (Prop.forAll launches (fun launch -> Launch.read playing (Launch.words launch) = Ok launch))

report
    "a line still carrying the runner in front of it is read all the same"
    (Ok(Launch.Join("greg-pc", Some "a1b2c3", None)))
    (Launch.read playing [ "dotnet"; "run"; "--"; "join"; "greg-pc"; "--token"; "a1b2c3" ])

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

// --- and what the program writes, the command line accepts --------------------------------------

/// Run the real front door over a set of arguments, and give back what it made of them.
/// `Launch.run` is what `main` calls, so this is the program's door and not a model of it -
/// including the exit code, which is what a script reads.
///
/// What it says while refusing something is caught rather than printed. Half these checks
/// hand it arguments it ought to refuse, and a run of usage text between the lines of a test
/// report is no help to anybody reading one.
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

// An option nobody has, which is the same kind of mistake and was not always treated as one.
// A line read past rather than refused is quiet in the one place this program cannot afford
// quiet: '--cod sesame' opened a table with a word made up here rather than the word that was
// meant, and said nothing about it.

report "an option nobody has is turned away too" true (turnedAway [ "play"; "2"; "--vew"; "rich" ])

report "including one that is nearly the word at a door" true (turnedAway [ "host"; "3"; "--cod"; "sesame" ])

// And an address nobody could open. It is the one thing said at the door that this machine
// cannot check by trying it - what is out front is somebody else's business - so what is
// checked is that it is an address at all, because a link is going to be built out of it.

report "an address that is not one is refused before a table is opened" true (turnedAway [ "host"; "3"; "--at"; "my table" ])

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
    (0, Some(Launch.Deal(3, None, [ "hard"; "easy" ])))
    (through [ "play"; "3"; "--rival"; "hard"; "--rival"; "easy" ])

// A table that opens a port makes up a word for its door when nobody says otherwise, so the
// two commands that do cannot be held up whole against anything: what the word is, is not
// knowable from out here, which is the entire point of it. So the door is taken off for the
// checks about everything else, and put back for the checks about doors.

let private without launch =
    match launch with
    | Launch.Serve(players, seed, rivals, reach) -> Launch.Serve(players, seed, rivals, { reach with Doorway = Ajar })
    | Launch.Host(players, seed, reach) -> Launch.Host(players, seed, { reach with Doorway = Ajar })
    | opened -> opened

let private unlocked words =
    let code, opened = through words
    code, opened |> Option.map without

report
    "a browser's table takes them the same way"
    (0, Some(Launch.Serve(2, Some 42UL, [ "medium" ], Reach.ajar)))
    (unlocked [ "serve"; "2"; "--seed"; "42"; "-r"; "medium" ])

// --- how far a table is opened ------------------------------------------------------------
//
// A table on a network everybody in the room is on is guarded by the room. One reachable from
// anywhere is guarded by nothing, and a seat once taken is kept for whoever took it - so the
// first stranger through the door ends the game for somebody, and there is no move for
// standing them up again. Hence a word at the door by default, and hence the flags: every one
// of them is a way of saying how far this table is meant to reach.

let private door words =
    match through words with
    | 0, Some(Launch.Host(_, _, reach)) -> Ok reach.Doorway
    | 0, Some(Launch.Serve(_, _, _, reach)) -> Ok reach.Doorway
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

// Everything below is somebody meaning one of two quite different things, and there is no
// way to tell which - so each is refused at the door rather than settled quietly.

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
             3,
             None,
             { Reach.ajar with
                 Wrapping = Ahead
                 Address = Some "stones.example.org" }
         )
     ))
    (unlocked [ "host"; "3"; "--behind"; "--at"; "stones.example.org"; "--open" ])

// --- the other door ---------------------------------------------------------------------------
//
// Running the program with no arguments at all opens the menu instead, which asks the same
// questions in a different grammar. Only the newest part of it is held to anything here: how
// many are playing is not asked for after `vs`, because saying who you are playing has
// already said it - one seat for you and one for each machine named - and a menu that got
// that sum wrong would deal a table with an empty chair at it.

let private chosen line = Menu.choose playing standard line

/// A choice as something that can be compared. `Menu.Choice` carries a view, and a view is
/// a bundle of functions, so the choices cannot be held up against each other whole.
///
/// A seating comes back as the words it would be typed in, which is the shape it is easiest
/// to be wrong about and the shape a person actually says: 'you hard' either is a seat for
/// each of you or it is not.
let private dealing choice =
    match choice with
    | Ok(Menu.Deal(sitters, seed)) -> Ok("deal", Seating.line sitters, seed)
    | Ok(Menu.Serve(sitters, seed, _)) -> Ok("serve", Seating.line sitters, seed)
    | Ok(Menu.Host(sitters, seed, _)) -> Ok("host", Seating.line sitters, seed)
    | Ok(Menu.Sitting(sitters, _)) -> Ok("seats", Seating.line sitters, None)
    | Ok _ -> Error "that is not a game to deal"
    | Error problem -> Error problem

/// And how far the table it opens will reach, which every one of those lines can say too -
/// said back as the words it would be typed in, for the same reason.
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

// --- and the seatings, said long and said short --------------------------------------------
//
// A seating is one sitter to a seat, and how many are playing is how long it is - so the count
// and the seats cannot disagree, which is the one sum the old menu could get wrong. Everything
// shorter than that is built out of a whole seating rather than beside one, so a shorthand
// cannot come to mean something the long way round does not.

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

// A page on this machine is one hot seat, the same as this keyboard is. There is nobody for a
// seat at it to be at the far end of, so a seating with anybody joining is not one it can take.

report
    "a browser's game cannot have anybody joining it"
    true
    (match chosen "serve you joins" with
     | Error problem -> problem.Contains "one hot seat"
     | Ok _ -> false)

report "and the menu says the machine is on offer" true ((Keys.draw None (Menu.screen playing plain)).Contains "vs <skill>...")

// A table somebody was told about is an address and a word, so that is what the menu takes.
// Coming back to a seat already held is the other thing, and is a command line - written by
// the program, for the player to hand back to it - rather than anything typed here.

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

// --- the same door, opened with the arrow keys ------------------------------------------------
//
// A row on a screen is not a second way of meaning something: it stands for a line, and
// choosing it hands that line to the very reader a person typing it would have reached. That
// is the whole design, and it is only worth anything if it is true of every row - including
// the ones two lists down, which nobody scrolls past on the way to anywhere.
//
// So the first check here walks the whole tree and insists the reader understands every line
// on it. A row that came to offer something `Menu.choose` has never heard of would be a dead
// end a player finds by pressing Enter on it, and nothing else in the program would notice.

let private front = Menu.screen playing plain

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

/// Every line one row can come to: the one it stands for, and the two its left and right
/// make. A row that only writes the start of a line is finished off here, because what is
/// being checked is the grammar around the part somebody types rather than the part itself.
///
/// A number stands in for it, being the one thing that is a fair example of all four things
/// a row here waits for: a file, an address, a port, and a word at a door.
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
    (unread (Options.choose standard) (Options.screen standard))

// The seat list is not reached by opening a row - it is reached by a line, and comes back as
// one - so it has to be walked over separately. Every seating there is, at every size the
// table takes, because a row there is built from what is already in the seat beside it.

let private everySeating =
    let rec grown seats =
        if seats = 0 then
            [ [] ]
        else
            grown (seats - 1)
            |> List.collect (fun rest -> Seating.all playing.Skills |> List.map (fun sitter -> sitter :: rest))

    [ Table.MinPlayers .. Table.MaxPlayers ] |> List.collect grown

/// Every seat list carries how far its table reaches, so there is a second list to walk over
/// it: the reaches a screen can hold. Not every reach there is - a port is a number and an
/// address is a name - but every *shape* of one, which is what the rows are built out of.
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
    (match (Options.screen standard).Backs with
     | Some line ->
         match Options.choose standard line with
         | Ok Options.Done -> true
         | _ -> false
     | None -> false)

report
    "and no two rows on one screen answer to the same number"
    []
    (everyScreen front
     |> List.collect (fun screen -> screen.Rows |> List.choose (fun row -> row.Digit) |> List.countBy id)
     |> List.filter (fun (_, many) -> many > 1))

// --- and that the keys reach them -------------------------------------------------------------

let private key press =
    ConsoleKeyInfo('\000', press, false, false, false)

let private letter (typed: char) =
    ConsoleKeyInfo(typed, enum<ConsoleKey> 0, false, false, false)

/// A screen, walked over by a run of key presses, and whatever line it gave up.
let private walking screen keys =
    let rec next standing keys =
        match keys with
        | [] -> None
        | key :: rest ->
            match Keys.answer (Keys.pressed (Keys.typing standing) key) standing with
            | Keys.Steering standing -> next standing rest
            | Keys.Answered line -> Some line

    next (Keys.standing screen 0) keys

let private walked keys = walking front keys

report "the number of a row picks it outright" (Some "colours") (walked [ letter '5' ])

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

// The one place the two readings of a key meet. With nothing typed the letters steer; with a
// line underway every letter belongs to it, or an address with an 'a' in it could not be
// spelt out at all.

report
    "and once a line is underway the steering letters are letters again"
    (Some "join sad")
    (walked (
        [ 'j'; 'o'; 'i'; 'n'; ' '; 's'; 'a'; 'd' ]
        |> List.map letter
        |> fun keys -> keys @ [ key ConsoleKey.Enter ]
    ))

report
    "backing out of a list opened by mistake comes back to the one it was opened from"
    (Some "quit")
    (walked [ letter '1'; key ConsoleKey.Escape; letter '7' ])

report
    "and backing out of the front door does nothing, there being nothing behind it"
    (Some "quit")
    (walked [ key ConsoleKey.Escape; letter '7' ])

// --- and the seats, which are the same idea again -----------------------------------------
//
// A seat's row stands for the whole seating with that one seat changed, so walking a seat
// along is a way of typing and there is nothing to remember between presses. Which means the
// list can be walked here exactly as a person walks it: press, read the line, build the
// screen again from what it said.

/// One press at the seat list, and the seating it came back holding.
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

// And the rows underneath the seats, which are what a seating is finally *for*. Which of them
// is offered is the seating's own answer: a table anybody is joining is opened and waited at,
// and is not a thing a browser on this machine could hold.

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

// And what it opens is exactly the reach on the screen too, which is the half of this that
// the seat list could not say at all before: a table opened from a menu now says where it
// listens, what it is carried in, and what it takes to sit down at it.

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

// The screen behind it, walked the way a person walks it: press, read the line, build the
// screen again from what it said. Nothing is remembered between presses there either.

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

// The two that want words write the line as far as they can and wait for the rest, which is
// how the port and the address are said. What the row writes has to be a line the reader
// takes once the rest arrives - and the part being changed goes last, because the last word
// about a part is the one that counts.

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

// Left and right on the colour screen walk one slot through the nineteen. Nothing is
// remembered between presses - the line says the whole of the change - so walking the list
// right round has to come back to the colour it set out from.

let private colours = Options.screen standard

let private walkedRight times palette =
    List.replicate times (key ConsoleKey.RightArrow)
    |> List.fold
        (fun palette press ->
            match walking (Options.screen palette) [ press ] with
            | Some line ->
                match Options.choose palette line with
                | Ok(Options.Changed changed) -> changed
                | _ -> palette
            | None -> palette)
        palette

report "right walks a slot on to the next colour" (Some "red ember") (walking colours [ key ConsoleKey.RightArrow ])

report
    "and left to the one before, which from the first is the last"
    (Some "red slate")
    (walking colours [ key ConsoleKey.LeftArrow ])

report
    "walking right round the colours comes back where it started"
    [ "ember"; "crimson" ]
    [ (Palette.shadeOf "red" (walkedRight 1 standard)).Name
      (Palette.shadeOf "red" (walkedRight (List.length Palette.shades) standard)).Name ]

// Where the cursor was left comes back with the line, because the screen is built again from
// the palette every time one changes and the cursor has to still be on the slot being walked.

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
