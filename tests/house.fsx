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

open System
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
    table.Sits("one", "tok-one", None, AtATerminal, "plain", "") |> ignore
    table.Sits("two", "tok-two", None, AtATerminal, "plain", "") |> ignore
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
         table.Sits("one", "tok-one", None, AtATerminal, "plain", "") |> ignore
         table.Standing.Sat))

/// The screen one console was drawn, out of what a table said.
let private screenFor console posts =
    posts
    |> List.tryPick (fun post ->
        match post.To, post.Say with
        | at, Screen text when at = console -> Some text
        | _ -> None)
    |> Option.defaultValue ""

// The whole point of a table being *told* which kind of console is sitting down. A page and a
// terminal cannot read the same screens - `html` means nothing to a terminal and `rich` means
// nothing to a browser - so a table asked for a view has to know which list to look in. Before
// this, the browser's way in resolved its own view out here and the table only ever answered
// for terminals; a house serving both through one set of routes cannot work that way.
report
    "a page and a terminal at one table are drawn by the ways of reading each of them can read"
    (Ok(false, true))
    (hosting().Deals(twoPeople, Some 42UL, None)
     |> Result.map (fun table ->
         // A terminal asking for `html` is not asking for something that does not exist, it
         // is asking for something it could not draw - so it gets the plainest one it can.
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
             table.Sits("back", "tok-back", None, AtATerminal, "plain", "") |> ignore
             before, table.Standing.Sat
     | found -> failwith $"expected one record, found {List.length found}")

// --- the rules a house keeps, as values -------------------------------------------------
//
// `Housekeeping` is apart from the house on purpose: what a house *holds* is live tables
// behind a lock, and what it *decides* needs no table, no lock and no clock that moves. So
// the rule that can throw somebody's game away is asked here about tables that do not exist,
// at ages that have not happened.

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

// The one worth being most careful about. A half-full table is not an unused one: somebody
// took a seat, their seat is being kept for them, and a house that swept it away because
// nobody was looking would be a house that lost a game somebody is coming back to.
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

// --- and the order they are shown in ----------------------------------------------------

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

// --- a house holding several ------------------------------------------------------------

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

// A table names the way it was dealt, which is the fact a dealt table cannot be asked for and
// a list of two games of Compile would be useless without.
report
    "and each table remembers which way it was dealt"
    "turncoats"
    (let it = house ()

     match it.Opens(twoPeople, Some 42UL, None) with
     | Error said -> failwith said
     | Ok opened -> opened.Way)

// --- and forgetting them ----------------------------------------------------------------

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

// --- and something that does the sweeping -----------------------------------------------
//
// `Swept` is thoroughly checked above and was, until this, never called by anything: the
// timer that calls it lived four lines inside the web host, where no check could reach it, so
// a rule that was certain and a house that would grow for ever were one missing line apart.
//
// Milliseconds rather than the five minutes a real house uses, and a real clock rather than
// the stopped one - a table has to actually become old for this to mean anything.

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

// The other half of the same line, and the one worth being sure of: a table somebody is at is
// not swept by a timer any more than by a hand. The rule says so and is checked above; this
// says the timer asks the rule rather than deciding for itself.
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

// --- and taking a house's games back up ---------------------------------------------------
//
// `Resumes` is checked above, one record at a time. What `--fill` adds is the sweep of `logs/`
// that finds them, which is what makes a restart a pause rather than a loss - so it is worth
// checking that the records a house writes are the records a house can find again.

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
