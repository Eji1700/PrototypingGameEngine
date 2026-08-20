#load "Whole.fsx"

open System
open System.IO
open TCModel.Table
open TCModel.Net
open TCModel.Turncoats
open Harness
open Whole

let private stopped () = 4242UL

let private stampedFor (game: Playable<_, _, _>) = $"0000-00-00-000000-{game.Name}"

let private hosting () =
    Hosting.of' Offer.ways stopped stampedFor

let private ours () =
    let folder = Path.Combine(Directory.GetCurrentDirectory(), "logs")

    if Directory.Exists folder then
        Directory.GetFiles(folder, "0000-00-00-000000-*.log") |> List.ofArray
    else
        []

let private swept () =
    for path in ours () do
        try
            File.Delete path
        with _ ->
            ()

swept ()

let private twoPeople = Seating.here 2

let private full (table: TCModel.Net.Table) =
    table.Sits("one", "tok-one", None, AtATerminal, "plain", "") |> ignore
    table.Sits("two", "tok-two", None, AtATerminal, "plain", "") |> ignore
    table


report
    "a house says which game it is, for the top of every page it draws"
    ("turncoats", true)
    (let house = hosting () in house.Name, house.Title <> "")

report
    "and how many may sit down, so a table of the wrong size is refused before it is dealt"
    (playing.Fewest, playing.Most)
    (let house = hosting () in house.Fewest, house.Most)

report "the ways it can be played are offered by name, for the form that asks" [ "turncoats" ] ((hosting ()).Ways |> List.map fst)

report
    "and every way offered says something about itself, or the form has a blank row on it"
    []
    ((hosting ()).Ways |> List.filter (fun (_, says) -> says = "") |> List.map fst)


report
    "a house deals a table, and it comes back filling up with every seat going spare"
    (Ok(Lobby.Filling, 2, 0, 0))
    (hosting().Deals(twoPeople, Some 42UL, None)
     |> Result.map (fun table ->
         let door = table.Standing
         door.Stage, door.Places, door.Sat, door.Machines))

report
    "a table the game will not deal is refused in the game's own words rather than thrown"
    true
    (match hosting().Deals(Seating.here 99, None, None) with
     | Error said -> said <> ""
     | Ok _ -> false)

report
    "a seating with the machine in it deals a table with a seat that is never waited for"
    (Ok(2, 1, 0))
    (hosting().Deals(Seating.after 2 [ "easy" ], Some 42UL, None)
     |> Result.map (fun table ->
         let door = table.Standing
         door.Places, door.Machines, door.Sat))

report
    "a way this game does not have deals the plainest one rather than refusing"
    (Ok 2)
    (hosting().Deals(twoPeople, Some 42UL, Some "no-such-way")
     |> Result.map (fun table -> table.Standing.Places))


report
    "somebody can sit down at a table a house dealt"
    (Ok 1)
    (hosting().Deals(twoPeople, Some 42UL, None)
     |> Result.map (fun table ->
         table.Sits("one", "tok-one", None, AtATerminal, "plain", "") |> ignore
         table.Standing.Sat))

let private screenFor console posts =
    posts
    |> List.tryPick (fun post ->
        match post.To, post.Say with
        | at, Screen text when at = console -> Some text
        | _ -> None)
    |> Option.defaultValue ""

report
    "a page and a terminal at one table are drawn by the ways of reading each of them can read"
    (Ok(false, true))
    (hosting().Deals(twoPeople, Some 42UL, None)
     |> Result.map (fun table ->
         let terminal = table.Sits("one", "tok-one", None, AtATerminal, "html", "")
         let page = table.Sits("two", "tok-two", None, InABrowser, "", "")
         (screenFor "one" terminal).Contains "<div", (screenFor "two" page).Contains "<div"))

report
    "and once it is full it is under way"
    (Ok Lobby.Underway)
    (hosting().Deals(twoPeople, Some 42UL, None)
     |> Result.map (full >> fun table -> table.Standing.Stage))

report
    "a console that leaves keeps its seat, so the table is still full and one console short"
    (Ok(2, 1))
    (hosting().Deals(twoPeople, Some 42UL, None)
     |> Result.map (fun table ->
         full table |> ignore
         table.Left "two" |> ignore
         let door = table.Standing
         door.Sat, door.Reading))


report
    "a record that is not there is refused rather than thrown"
    true
    (match hosting().Resumes(Path.Combine(Path.GetTempPath(), "tcmodel-no-such-record.log")) with
     | Error said -> said <> ""
     | Ok _ -> false)

let private leftBehind () =
    swept ()

    match hosting().Deals(Seating.after 2 [ "easy" ], Some 42UL, None) with
    | Error said -> failwith said
    | Ok table ->
        table.Sits("one", "tok-one", None, AtATerminal, "plain", "") |> ignore
        table.Said("one", "recruit r 3") |> ignore
        ours ()

report "a table a house dealt writes its record as it is played, without being asked to" 1 (leftBehind () |> List.length)

report
    "and a house takes that record back up, with the machine at the seat it was playing"
    (Lobby.Filling, 2, 1, 0)
    (match leftBehind () with
     | [ path ] ->
         match hosting().Resumes path with
         | Error said -> failwith said
         | Ok table ->
             let door = table.Standing
             door.Stage, door.Places, door.Machines, door.Sat
     | found -> failwith $"expected one record, found {List.length found}")

report
    "a resumed table is waiting for its players again, and lets one back into a seat"
    (Lobby.Filling, 1)
    (match leftBehind () with
     | [ path ] ->
         match hosting().Resumes path with
         | Error said -> failwith said
         | Ok table ->
             let before = table.Standing.Stage
             table.Sits("back", "tok-back", None, AtATerminal, "plain", "") |> ignore
             before, table.Standing.Sat
     | found -> failwith $"expected one record, found {List.length found}")


let private standing stage places machines sat reading : Lobby.Standing =
    { Stage = stage
      Places = places
      Machines = machines
      Sat = sat
      Reading = reading
      Sitters = [] }

let private long = TimeSpan.FromDays 7.0
let private brief = TimeSpan.FromMinutes 1.0

let private spent age table =
    Housekeeping.spent Housekeeping.ordinary age table

report
    "a table nobody ever sat at goes once it is old enough"
    (false, true)
    (spent brief (standing Lobby.Filling 2 0 0 0), spent long (standing Lobby.Filling 2 0 0 0))

report "a table somebody took a seat at is never swept, however long it waits" false (spent long (standing Lobby.Filling 2 0 1 0))

report "and a game being played is never swept, however long a turn takes" false (spent long (standing Lobby.Underway 2 0 2 0))

report
    "a finished game is kept a while and then goes"
    (false, true)
    (spent brief (standing Lobby.Finished 2 0 2 0), spent long (standing Lobby.Finished 2 0 2 0))

report
    "but never while somebody still has it open, whatever state it is in"
    [ false; false; false ]
    ([ Lobby.Filling; Lobby.Underway; Lobby.Finished ]
     |> List.map (fun stage -> spent long (standing stage 2 0 2 1)))


let private at (minutes: float) = DateTime(2026, 1, 1).AddMinutes minutes

let private listedAs entries =
    entries
    |> List.map (fun (name, minutes, standing) ->
        { Id = name
          At = at minutes
          Way = "turncoats"
          Table = Unchecked.defaultof<TCModel.Net.Table> },
        standing)
    |> Housekeeping.listed
    |> List.map (fun (opened, _) -> opened.Id)

report
    "a house shows the tables you could sit down at first, then the games, then the records"
    [ "spare"; "full-and-waiting"; "playing"; "over" ]
    (listedAs
        [ "over", 4.0, standing Lobby.Finished 2 0 2 0
          "playing", 3.0, standing Lobby.Underway 2 0 2 0
          "full-and-waiting", 2.0, standing Lobby.Filling 2 1 1 0
          "spare", 1.0, standing Lobby.Filling 2 0 0 0 ])

report
    "and the newest of each first, so a table just opened is at the top of the list"
    [ "new"; "old" ]
    (listedAs
        [ "old", 1.0, standing Lobby.Filling 2 0 0 0
          "new", 9.0, standing Lobby.Filling 2 0 0 0 ])


let private clock = ref (at 0.0)
let private named = ref 0

let private house () =
    clock.Value <- at 0.0
    named.Value <- 0

    House(
        hosting (),
        (fun () -> clock.Value),
        (fun () ->
            named.Value <- named.Value + 1
            $"table-{named.Value}"),
        Housekeeping.ordinary
    )

report
    "a new house is empty, and says which game it is a house of"
    ("turncoats", 0)
    (let it = house () in it.Name, List.length it.Listed)

report
    "tables opened at a house are all held, each under a name of its own"
    ([ "table-1"; "table-2" ], 2)
    (let it = house ()
     it.Opens(twoPeople, Some 42UL, None) |> ignore
     it.Opens(twoPeople, Some 43UL, None) |> ignore
     it.Listed |> List.map (fun (opened, _) -> opened.Id) |> List.sort, List.length it.Listed)

report
    "a table a house opened can be found again by its name, and one nobody opened cannot"
    (true, false)
    (let it = house ()

     match it.Opens(twoPeople, Some 42UL, None) with
     | Error said -> failwith said
     | Ok opened -> (it.At opened.Id).IsSome, (it.At "no-such-table").IsSome)

report
    "a table the game refuses is not held, so a house is not filled with tables that failed"
    0
    (let it = house ()
     it.Opens(Seating.here 99, None, None) |> ignore
     List.length it.Listed)

report
    "and each table remembers which way it was dealt"
    "turncoats"
    (let it = house ()

     match it.Opens(twoPeople, Some 42UL, None) with
     | Error said -> failwith said
     | Ok opened -> opened.Way)


report
    "nothing is swept from a house whose tables were all just opened"
    ([], 2)
    (let it = house ()
     it.Opens(twoPeople, Some 42UL, None) |> ignore
     it.Opens(twoPeople, Some 43UL, None) |> ignore
     it.Swept(), List.length it.Listed)

report
    "a table nobody sat at is swept once the clock has moved past it, and is named as it goes"
    ([ "table-1" ], 0)
    (let it = house ()
     it.Opens(twoPeople, Some 42UL, None) |> ignore
     clock.Value <- at (60.0 * 24.0)
     it.Swept(), List.length it.Listed)

report
    "while one somebody is sitting at is left where it is, however far the clock has gone"
    ([], 1)
    (let it = house ()

     match it.Opens(twoPeople, Some 42UL, None) with
     | Error said -> failwith said
     | Ok opened ->
         opened.Table.Sits("one", "tok-one", None, AtATerminal, "plain", "") |> ignore
         clock.Value <- at (60.0 * 24.0 * 30.0)
         it.Swept(), List.length it.Listed)

report
    "and a swept table is gone from the list rather than merely hidden from it"
    false
    (let it = house ()

     match it.Opens(twoPeople, Some 42UL, None) with
     | Error said -> failwith said
     | Ok opened ->
         clock.Value <- at (60.0 * 24.0)
         it.Swept() |> ignore
         (it.At opened.Id).IsSome)


let private quickly = TimeSpan.FromMilliseconds 40.0

let private impatient () =
    House(hosting (), (fun () -> DateTime.Now), (fun () -> Guid.NewGuid().ToString "N"), { Unused = quickly; Finished = quickly })

report
    "a house sweeps on its own, without anybody asking it to"
    (1, 0)
    (let it = impatient ()
     it.Opens(twoPeople, Some 42UL, None) |> ignore
     let before = List.length it.Listed

     use _ = it.Sweeping(quickly, ignore)
     Threading.Thread.Sleep 600

     before, List.length it.Listed)

report
    "and says what it took away, so a house that forgets a game says so out loud"
    true
    (let it = impatient ()
     it.Opens(twoPeople, Some 42UL, None) |> ignore
     let heard = ResizeArray<string>()

     use _ = it.Sweeping(quickly, (fun gone -> heard.AddRange gone))
     Threading.Thread.Sleep 600

     heard.Count >= 1)

report
    "but a table with somebody sitting at it is left alone however often the sweeping happens"
    1
    (let it = impatient ()

     match it.Opens(twoPeople, Some 42UL, None) with
     | Error said -> failwith said
     | Ok opened ->
         opened.Table.Sits("one", "tok-one", None, AtATerminal, "plain", "") |> ignore

         use _ = it.Sweeping(quickly, ignore)
         Threading.Thread.Sleep 600

         List.length it.Listed)


report
    "the records a house wrote are the ones it would find on the way back up"
    true
    (let written = leftBehind ()

     let found =
         Transcript.saved ()
         |> List.filter (fun record -> record.Game = Some (hosting ()).Name)
         |> List.map (fun record -> record.Path)

     written
     |> List.forall (fun path -> found |> List.exists (fun other -> other.EndsWith(IO.Path.GetFileName path))))

report
    "and taking every one of them up fills a house with them"
    true
    (let written = leftBehind ()
     let it = house ()

     let taken = written |> List.choose (fun path -> it.Resumes path |> Result.toOption)

     List.length taken = List.length written
     && List.length it.Listed = List.length written)

swept ()

report "and the checks take their own records away again" [] (ours ())

finish ()
