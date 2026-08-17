namespace TCModel.Net

open System
// For `Post`, which is what a table hands back and the only thing from that layer this file
// names. Nothing here has met a socket and nothing here draws a board.
open TCModel.Table

/// One table in a house: the table itself, and the three things the house knows about it that
/// the table does not.
///
/// A table was never told which of them it is, when it was opened, or which way it was dealt,
/// and it should not be. The house made it and did the naming, so the house is where those
/// live - and a `Table` that answered for them would be answering a question it was never
/// asked.
[<NoComparison; NoEquality>]
type Opened =
    {
        /// What this table is called in a link. Minted rather than counted, so that knowing
        /// about one table says nothing whatever about the others.
        Id: string
        /// When it was dealt, which is the only thing anything below reads a clock for.
        At: DateTime
        /// Which way it was dealt, as the name that way answers to. Kept because it is not
        /// otherwise recoverable from a dealt table, and a list that could not say which of two
        /// tables was the game with the optional rule in it would be a list with a trap in it.
        Way: string
        Table: Table
    }

/// How long a table that has stopped being played is kept.
///
/// Two spans and not one, because the two ways a table stops mattering are not alike. A
/// **finished** game is worth reading for a while - somebody wants to walk the last few moves
/// back - so this is about the list staying short rather than about anything being lost, the
/// record on disk outliving the table either way.
///
/// An **unused** one is a table somebody opened and walked away from before anybody sat down,
/// and it is worth less: nothing was ever played at it. It goes sooner.
///
/// What must never go is a table with people at it, however long they have been thinking. A
/// game of Diplomacy between two time zones can sit untouched for a day and is not abandoned.
type Keeping =
    { Unused: TimeSpan; Finished: TimeSpan }

/// The rules a house keeps, as values.
///
/// Apart from the house itself on purpose. What a house *holds* is live tables behind a lock;
/// what it *decides* is which tables are worth keeping and what order to show them in, and
/// neither of those needs a table, a lock or a clock that moves. So they are functions of a
/// description and an age, and can be asked about a table that does not exist at a time that
/// has not happened - which is what lets the awkward cases be checked rather than argued
/// about.
module Housekeeping =

    /// A day to read a finished game, an hour for one nobody ever sat at.
    let ordinary =
        { Unused = TimeSpan.FromHours 1.0
          Finished = TimeSpan.FromDays 1.0 }

    /// Whether a table has stopped being worth keeping.
    ///
    /// The one rule in this file that can throw away somebody's game, so it is the one written
    /// down on its own.
    ///
    /// Never while anybody is reading it, and never merely because it is old. A table with a
    /// console attached is a table somebody is looking at, whatever state it is in.
    let spent keeping (age: TimeSpan) (standing: Lobby.Standing) =
        if standing.Reading > 0 then
            false
        else
            match standing.Stage with
            | Lobby.Finished -> age > keeping.Finished
            // Nobody has ever sat at it. A table half full is *not* this case: somebody took a
            // seat and may be coming back to it, and their seat is being kept for exactly that.
            | Lobby.Filling when standing.Sat = 0 -> age > keeping.Unused
            | Lobby.Filling
            | Lobby.Underway -> false

    /// Where a table stands in a list, lowest first.
    ///
    /// Somebody arriving at a house is usually looking for a game to join, so the tables they
    /// could actually sit down at come first. Then the ones being played, which are worth
    /// knowing are there. Then the finished ones, which are a record rather than a game.
    let private rank (standing: Lobby.Standing) =
        match standing.Stage with
        | Lobby.Filling when standing.Sat < standing.Places - standing.Machines -> 0
        | Lobby.Filling -> 1
        | Lobby.Underway -> 2
        | Lobby.Finished -> 3

    /// The list as a house shows it: joinable first, newest first within that.
    let listed (tables: (Opened * Lobby.Standing) list) =
        tables
        |> List.sortBy (fun (opened, standing) -> rank standing, -opened.At.Ticks)

/// A house of tables: several games of one game, and everything that changes which.
///
/// **It is one game's house.** `Turncoats` holds games of Turncoats and knows of no other.
/// There is no front door above the games and there should not be: a container runs one
/// program, one port, one game, and a list of every game on a machine is a different thing at
/// a different level, most likely not written in F# at all.
///
/// Nothing here has met a socket. What crosses the wire is a table's business and `Server`'s;
/// this is a lock, a list, and the answers to four questions - which tables there are, how one
/// is opened, which one a word names, and which have stopped being worth keeping.
///
/// Mutable and behind a gate, for the reason `Held` is: what it holds are live tables, and two
/// people opening one at the same moment must not be able to make two tables with one name or
/// lose one of them entirely.
///
/// The clock and the naming are handed in rather than read, so a check can say what it expects
/// of a table an hour old without waiting an hour.
type House(hosting: Hosting, now: unit -> DateTime, naming: unit -> string, keeping: Keeping) =
    let gate = obj ()
    let mutable tables: Opened list = []

    /// When each table is next due a beat, against its name. Empty at a house of a game that
    /// waits, and empty for a table nobody has beaten yet - which is read as "now", so a table
    /// opened between two sweeps of the clock does not sit still until the next one.
    let mutable due: Map<string, DateTime> = Map.empty

    /// Kept once here rather than at both ways in, which are otherwise the same three lines.
    let holding way table =
        let opened =
            { Id = naming ()
              At = now ()
              Way = way
              Table = table }

        lock gate (fun () -> tables <- tables @ [ opened ])
        opened

    /// Which game this house is a house of, for the top of every page it draws.
    member _.Name = hosting.Name
    member _.Title = hosting.Title

    /// What the "new table" form has to offer: how many may sit down, and the ways to play.
    member _.Fewest = hosting.Fewest
    member _.Most = hosting.Most
    member _.Ways = hosting.Ways

    /// Every table, in the order a house shows them, each with what it looks like this moment.
    ///
    /// A snapshot, and it has to be: each table's standing is read under that table's own
    /// lock, and by the time the list is drawn any of them may have moved on. That is the
    /// right answer rather than a compromise - a list of tables is a thing somebody read a
    /// moment ago, and pretending otherwise would mean holding every table in the house still
    /// while one page was drawn.
    member _.Listed =
        lock gate (fun () -> tables |> List.map (fun opened -> opened, opened.Table.Standing))
        |> Housekeeping.listed

    /// The table a word names, or nothing.
    ///
    /// Nothing rather than a refusal, because the word may be a link somebody kept from a
    /// table that has since been swept away - and that is not an error, it is a game that is
    /// over.
    member _.At(id: string) =
        lock gate (fun () -> tables |> List.tryFind (fun opened -> opened.Id = id))

    /// Open one, and hand back the table and the word that names it.
    member _.Opens(sitters, seed, way) =
        hosting.Deals(sitters, seed, way)
        |> Result.map (holding (way |> Option.defaultValue hosting.Name))

    /// And take one up off a record, which is how a house comes back from being stopped.
    ///
    /// A house holds nothing that is not also on disk - every table writes its record after
    /// every change - so filling one from `logs/` is the whole of what restarting costs.
    /// Whether to do that at startup is the host's to say, which is why this is a call and not
    /// something that happens.
    member _.Resumes(path: string) =
        hosting.Resumes path |> Result.map (holding hosting.Name)

    /// Forget the tables that have stopped being worth keeping, and say which those were.
    ///
    /// Said rather than done quietly, because a house throwing something away is the one thing
    /// here worth being able to read in a log afterwards.
    ///
    /// Nothing on disk is touched. The record outlives the table by design: a game swept off
    /// this list is a game somebody can still take up again from the file it wrote.
    member _.Swept() =
        lock gate (fun () ->
            let at = now ()

            let going, staying =
                tables
                |> List.partition (fun opened -> Housekeeping.spent keeping (at - opened.At) opened.Table.Standing)

            tables <- staying
            going |> List.map (fun opened -> opened.Id))

    /// Beat every table that is due one, and say what came of each.
    ///
    /// A house of a game that does not wait is a house where several boards are moving at
    /// once, each on its own clock: two snakes at two tables run at whatever speed the snake
    /// at that table has got to. So when a table is next due comes back from the table itself
    /// with the beat, and is kept here against its name.
    ///
    /// A table that has been swept away simply stops being asked - the list is read afresh
    /// each time round - and a house of a game that waits answers a minute for everything and
    /// costs a lock a minute per table.
    member _.Beat(at: DateTime) =
        let asking = lock gate (fun () -> tables)

        asking
        |> List.collect (fun opened ->
            let ready =
                lock gate (fun () ->
                    due
                    |> Map.tryFind opened.Id
                    |> Option.forall (fun (when': DateTime) -> at >= when'))

            if not ready then
                []
            else
                let posts, wait = opened.Table.Beats()
                lock gate (fun () -> due <- Map.add opened.Id (at + wait) due)
                posts)

    /// And something to keep the time, for a house whose game does not wait.
    ///
    /// Here rather than beside the web host for the reason `Sweeping` is: a timer started
    /// inside `Server.house` is lines no check can reach. `every` is how often the house looks
    /// rather than how often a table beats - a table says that for itself - so it wants to be
    /// short enough that the shortest beat lands near where it was asked for.
    member this.Beating(every: TimeSpan, deliver: Post list -> unit) =
        let clock = new Timers.Timer(every.TotalMilliseconds, AutoReset = true)

        clock.Elapsed.Add(fun _ ->
            match this.Beat(now ()) with
            | [] -> ()
            | posts -> deliver posts)

        clock.Start()
        clock :> IDisposable

    /// And something to do the sweeping, because otherwise nothing does.
    ///
    /// Here rather than beside the web host, and that is the whole reason it moved: a timer
    /// started inside `Server.house` is four lines no check can reach, sitting between a rule
    /// that is thoroughly checked and a house that would quietly grow for ever if the two were
    /// never joined up. Started here, a check can hand in a span of milliseconds and watch a
    /// table go without anybody asking for it.
    ///
    /// `told` is handed whatever went, and is called only when something did - a house saying
    /// "swept nothing" every five minutes for a week is a log nobody reads.
    ///
    /// Handed back rather than kept, because what starts a timer should be what stops it. A
    /// process that runs until somebody closes it never will, which is the ordinary case and
    /// is why nothing here does it for them.
    member this.Sweeping(every: TimeSpan, told: string list -> unit) =
        let broom = new Timers.Timer(every.TotalMilliseconds, AutoReset = true)

        broom.Elapsed.Add(fun _ ->
            match this.Swept() with
            | [] -> ()
            | gone -> told gone)

        broom.Start()
        broom :> IDisposable
