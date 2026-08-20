namespace TCModel.Net

open System
open TCModel.Table

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

    /// Whether a table can be swept away. Never while anybody is reading it, and never while a game is
    /// being played - only one nobody ever sat down at, or one that finished a day ago.
    let spent keeping (age: TimeSpan) (standing: Lobby.Standing) =
        if standing.Reading > 0 then
            false
        else
            match standing.Stage with
            | Lobby.Finished -> age > keeping.Finished
            | Lobby.Filling when standing.Sat = 0 -> age > keeping.Unused
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

    let mutable due: Map<string, DateTime> = Map.empty

    let holding way table =
        let opened =
            { Id = naming ()
              At = now ()
              Way = way
              Table = table }

        lock gate (fun () -> tables <- tables @ [ opened ])
        opened

    member _.Name = hosting.Name
    member _.Title = hosting.Title

    member _.Fewest = hosting.Fewest
    member _.Most = hosting.Most
    member _.Ways = hosting.Ways

    member _.Listed =
        lock gate (fun () -> tables |> List.map (fun opened -> opened, opened.Table.Standing))
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
            let at = now ()

            let going, staying =
                tables
                |> List.partition (fun opened -> Housekeeping.spent keeping (at - opened.At) opened.Table.Standing)

            tables <- staying
            going |> List.map (fun opened -> opened.Id))

    /// Beat whichever tables are due. Each keeps its own next time, so a house of games running at
    /// different speeds is driven by one clock ticking faster than any of them.
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

    member this.Beating(every: TimeSpan, deliver: Post list -> unit) =
        let clock = new Timers.Timer(every.TotalMilliseconds, AutoReset = true)

        clock.Elapsed.Add(fun _ ->
            match this.Beat(now ()) with
            | [] -> ()
            | posts -> deliver posts)

        clock.Start()
        clock :> IDisposable

    member this.Sweeping(every: TimeSpan, told: string list -> unit) =
        let broom = new Timers.Timer(every.TotalMilliseconds, AutoReset = true)

        broom.Elapsed.Add(fun _ ->
            match this.Swept() with
            | [] -> ()
            | gone -> told gone)

        broom.Start()
        broom :> IDisposable
