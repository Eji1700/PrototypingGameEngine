// Where a table comes from, which is the seam a house of tables needed and the one that did
// not already exist. `Table` says what a house does with a table once it has one; `Hosting`
// says how it gets one, and it is the last part of hosting that is still generic in the game.
//
// Every check here runs without a socket, and that is not a happy accident - it is why
// `Table`, `Held` and `Hosting` live below the wire rather than in `Server.fs` with it. A seam
// whose only implementation cannot be exercised without a web server is a seam that will be
// exercised by starting a web server, and then it is not checked, it is smoke-tested.
//
//   dotnet fsi tests/house.fsx

#load "Whole.fsx"

open System.IO
open TCModel.Table
open TCModel.Net
open TCModel.Turncoats
open Harness
open Whole

/// A clock that does not move, so a table dealt twice is dealt the same game twice and a check
/// can say what it expects. The real one reads `DateTime.UtcNow.Ticks`.
let private stopped () = 4242UL

/// What a record written by these checks is called.
///
/// A stamp rather than a path: `Transcript` decides where records live - `logs/`, beside
/// wherever the game was started from - and a check has no business saying otherwise. What it
/// does have business doing is finding its own leavings again, so the stamp says plainly that
/// a check wrote it, and the end of this file sweeps them.
///
/// The shape is not free. A stamp is a clock and then the game's name, and `Transcript.gameOf`
/// reads the name as *everything after the clock's four parts* - so a stamp with a word of its
/// own wedged in the middle is a record of a game called `check-turncoats`, which `Resumes`
/// then rightly refuses. The marker has to be the clock itself, and a game dealt in the year
/// nothing is a game no player wrote.
let private stampedFor (game: Playable<_, _, _>) = $"0000-00-00-000000-{game.Name}"

let private hosting () =
    Hosting.of' Offer.ways stopped stampedFor

/// Every record these checks wrote.
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

/// Two people, no machine - the shape most of these are about.
let private twoPeople = Seating.here 2

/// A table with both seats filled, which is the only state a game is played in.
///
/// Said out in full, because this game has a `Table` of its own - the map - and its own names
/// win inside its own namespace. The same trap `Program.fs` walks round with `TCModel.Play`.
let private full (table: TCModel.Net.Table) =
    table.Sits("one", "tok-one", None, "plain", "") |> ignore
    table.Sits("two", "tok-two", None, "plain", "") |> ignore
    table

// --- what a house is told before it deals anything ------------------------------------

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

// --- dealing one ----------------------------------------------------------------------

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

// The whole point of `Deals` taking a seating rather than a count: who is a person and who is
// the machine is one answer, given once, in the words the seat list already reads - so how
// many are playing is the length of it rather than a second thing to keep in step.
report
    "a seating with the machine in it deals a table with a seat that is never waited for"
    (Ok(2, 1, 0))
    (hosting().Deals(Seating.after 2 [ "easy" ], Some 42UL, None)
     |> Result.map (fun table ->
         let door = table.Standing
         door.Places, door.Machines, door.Sat))

// A name that is none of this game's is the plainest way, which is the answer a settings file
// already gets for a way that has since been renamed. A house must not refuse to deal over it.
report
    "a way this game does not have deals the plainest one rather than refusing"
    (Ok 2)
    (hosting().Deals(twoPeople, Some 42UL, Some "no-such-way")
     |> Result.map (fun table -> table.Standing.Places))

// --- and that what comes back is a table, not merely a description ----------------------

report
    "somebody can sit down at a table a house dealt"
    (Ok 1)
    (hosting().Deals(twoPeople, Some 42UL, None)
     |> Result.map (fun table ->
         table.Sits("one", "tok-one", None, "plain", "") |> ignore
         table.Standing.Sat))

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

// --- coming back from a record ---------------------------------------------------------
//
// The part that makes a restarted house a pause rather than a disaster. A table writes its
// record after every change already, so a house is rebuilt out of the same files a player
// could read, with no second format and nothing to keep in step.

report
    "a record that is not there is refused rather than thrown"
    true
    (match hosting().Resumes(Path.Combine(Path.GetTempPath(), "tcmodel-no-such-record.log")) with
     | Error said -> said <> ""
     | Ok _ -> false)

/// A table played a move and left on disk, which is what a house that stopped would find
/// waiting for it. The machine is at a seat, because that is the fact a resumed table is
/// easiest to be wrong about: who was at it is written nowhere but the record.
let private leftBehind () =
    swept ()

    match hosting().Deals(Seating.after 2 [ "easy" ], Some 42UL, None) with
    | Error said -> failwith said
    | Ok table ->
        table.Sits("one", "tok-one", None, "plain", "") |> ignore
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

// A table off a record has the game and nobody at it: the seats are the game's, the moves are
// the record's, and the people have to come back to them. Which is the honest reading of a
// house that was restarted - it holds the games, not the players.
report
    "a resumed table is waiting for its players again, and lets one back into a seat"
    (Lobby.Filling, 1)
    (match leftBehind () with
     | [ path ] ->
         match hosting().Resumes path with
         | Error said -> failwith said
         | Ok table ->
             let before = table.Standing.Stage
             table.Sits("back", "tok-back", None, "plain", "") |> ignore
             before, table.Standing.Sat
     | found -> failwith $"expected one record, found {List.length found}")

swept ()

report "and the checks take their own records away again" [] (ours ())

finish ()
