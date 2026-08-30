namespace Prototyping.Net

open System
open System.Threading
open Prototyping.Table

/// A clock that cannot catch itself up: each tick is set only once the last has finished, from what
/// that tick asked for, so two never overlap however long one takes. What a tick throws is handed
/// to `amiss`, which says when to try again - a `Timers.Timer` swallows it, and a table whose rules
/// throw would be asked again in silence twenty-five times a second.
module Clock =

    let ticking (first: TimeSpan) (tick: unit -> TimeSpan) (amiss: exn -> TimeSpan) : IDisposable =
        let held: Timer option ref = ref None

        let due (wait: TimeSpan) =
            match held.Value with
            | Some timer ->
                // A tick still running as the clock was disposed has nothing left to set.
                try
                    timer.Change(wait, Timeout.InfiniteTimeSpan) |> ignore
                with :? ObjectDisposedException ->
                    ()
            | None -> ()

        let beat _ =
            due (
                try
                    tick ()
                with problem ->
                    amiss problem
            )

        let timer =
            new Timer(TimerCallback beat, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan)

        held.Value <- Some timer
        due first
        timer :> IDisposable

[<NoComparison; NoEquality>]
type Opened =
    { Id: string
      At: DateTime
      Way: string
      Table: Table }

type Keeping =
    { Unused: TimeSpan; Finished: TimeSpan }

module Housekeeping =

    let ordinary =
        { Unused = TimeSpan.FromHours 1.0
          Finished = TimeSpan.FromDays 1.0 }

    /// Whether a table can be swept away, `age` being how long it has stood as it stands. Never
    /// while anybody is reading it, and never while a game is being played, however long a turn
    /// takes: only one nobody ever sat at or played at, once it has waited `Unused`, or one that
    /// finished `Finished` ago. A table taken up off a record has nobody at it and a game in it,
    /// and is kept.
    let spent keeping (age: TimeSpan) (standing: Lobby.Standing) =
        if standing.Reading > 0 then
            false
        else
            match standing.Stage with
            | Lobby.Finished -> age > keeping.Finished
            | Lobby.Filling when standing.Sat = 0 && not standing.Begun -> age > keeping.Unused
            | Lobby.Filling
            | Lobby.Underway -> false

    // Tables somebody could sit down at first, then ones filling, then games under way, then finished
    // ones - and newest first within each. Somebody arriving at the page wants a seat.
    let private rank (standing: Lobby.Standing) =
        match standing.Stage with
        | Lobby.Filling when standing.Sat < standing.Places - standing.Machines -> 0
        | Lobby.Filling -> 1
        | Lobby.Underway -> 2
        | Lobby.Finished -> 3

    let listed (tables: (Opened * Lobby.Standing) list) =
        tables
        |> List.sortBy (fun (opened, standing) -> rank standing, -opened.At.Ticks)

type House(hosting: Hosting, now: unit -> DateTime, naming: unit -> string, keeping: Keeping) =
    let gate = obj ()
    let mutable tables: Opened list = []

    // The house's own memory of each table: when its stage last changed, which is what a sweep
    // measures from, and when its next beat falls due. Both let go of a table as it is swept.
    let mutable since: Map<string, Lobby.Stage * DateTime> = Map.empty
    let mutable due: Map<string, DateTime> = Map.empty

    // Named inside the gate, so two tables opened at one moment cannot be handed one name between
    // them.
    let holding way (table: Table) =
        lock gate (fun () ->
            let opened =
                { Id = naming ()
                  At = now ()
                  Way = way
                  Table = table }

            tables <- tables @ [ opened ]
            since <- Map.add opened.Id (table.Standing.Stage, opened.At) since
            opened)

    // Every table, where it stands, and how long it has stood there - measured from the moment
    // this house saw its stage change, since a game that finishes on Thursday was not finished on
    // Monday when it was opened. Under the gate.
    let standings at =
        [ for opened in tables do
              let standing = opened.Table.Standing

              let changed =
                  match Map.tryFind opened.Id since with
                  | Some(stage, moment) when stage = standing.Stage -> moment
                  | Some _
                  | None -> at

              since <- Map.add opened.Id (standing.Stage, changed) since
              yield opened, standing, at - changed ]

    member _.Listed =
        lock gate (fun () -> standings (now ()) |> List.map (fun (opened, standing, _) -> opened, standing))
        |> Housekeeping.listed

    member _.At(id: string) =
        lock gate (fun () -> tables |> List.tryFind (fun opened -> opened.Id = id))

    member _.Opens(sitters, seed, way) =
        hosting.Deals(sitters, seed, way)
        |> Result.map (holding (way |> Option.defaultValue hosting.Name))

    member _.Resumes(path: string) =
        hosting.Resumes path |> Result.map (holding hosting.Name)

    member _.Swept() =
        lock gate (fun () ->
            let going =
                standings (now ())
                |> List.filter (fun (_, standing, age) -> Housekeeping.spent keeping age standing)
                |> List.map (fun (opened, _, _) -> opened.Id)

            tables <- tables |> List.filter (fun opened -> not (List.contains opened.Id going))
            since <- going |> List.fold (fun since id -> Map.remove id since) since
            due <- going |> List.fold (fun due id -> Map.remove id due) due
            going)

    /// Beat whichever tables are due. Each keeps its own next time - a game with no clock has none
    /// - so a house of games at different speeds is driven by one clock ticking faster than any of
    /// them. The tables are asked outside the gate, since a beat takes each one's own lock.
    member _.Beat(at: DateTime) =
        let asking =
            lock gate (fun () ->
                tables
                |> List.filter (fun opened ->
                    due
                    |> Map.tryFind opened.Id
                    |> Option.forall (fun (when': DateTime) -> at >= when')))

        asking
        |> List.collect (fun opened ->
            let posts, wait = opened.Table.Beats()

            let next =
                match wait with
                | Some wait -> at + wait
                | None -> DateTime.MaxValue

            // A table swept while it was being asked is not remembered on the strength of its answer.
            lock gate (fun () ->
                if tables |> List.exists (fun other -> other.Id = opened.Id) then
                    due <- Map.add opened.Id next due)

            posts)

    member this.Beating(every: TimeSpan, deliver: Post list -> unit) =
        Clock.ticking
            every
            (fun () ->
                match this.Beat(now ()) with
                | [] -> ()
                | posts -> deliver posts

                every)
            (fun problem ->
                eprintfn "A beat went wrong at a table in this house, which carries on: %s" problem.Message
                every)

    member this.Sweeping(every: TimeSpan, told: string list -> unit) =
        Clock.ticking
            every
            (fun () ->
                match this.Swept() with
                | [] -> ()
                | gone -> told gone

                every)
            (fun problem ->
                eprintfn "The sweep went wrong, and is tried again: %s" problem.Message
                every)
