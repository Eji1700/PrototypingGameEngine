namespace TCModel.Net

open System
open TCModel.Engine
open TCModel.Table

type Table =
    abstract Sits:
        console: string * offered: string * resuming: string option * shown: Shown * view: string * palette: string -> Post list

    abstract Said: console: string * line: string -> Post list

    abstract Left: console: string -> Post list

    abstract Beats: unit -> Post list * TimeSpan

    abstract Standing: Lobby.Standing

/// A lobby behind a lock, and the record written out after every change.
///
/// Everything below this is immutable and knows nothing about who else is at the table; this is the
/// one place where several consoles meet, so it is the one place that has to take a turn at a time.
type Held<'Move, 'State, 'Notice>(opening: Lobby<'Move, 'State, 'Notice>, keep: Model<'Move, 'State, 'Notice> -> unit) =
    let gate = obj ()
    let mutable lobby = opening

    member _.Change(change: Lobby<'Move, 'State, 'Notice> -> Lobby<'Move, 'State, 'Notice> * Post list) =
        lock gate (fun () ->
            let next, posts = change lobby
            lobby <- next
            keep (Lobby.model next)
            posts)

    interface Table with
        member this.Sits(console, offered, resuming, shown, view, palette) =
            let game = Lobby.game lobby

            let palette = Palette.read game.Slots palette

            let view =
                Playable.byName shown palette game view
                |> Result.defaultValue (Playable.plainest shown palette game)

            this.Change(Lobby.join console offered resuming view)

        member this.Said(console, line) = this.Change(Lobby.said console line)

        member this.Left console = this.Change(Lobby.left console)

        member this.Beats() =
            lock gate (fun () ->
                match (Lobby.game lobby).Pulse with
                | None -> [], TimeSpan.FromMinutes 1.0
                | Some pulse ->
                    let next, posts = Lobby.beaten lobby
                    lobby <- next
                    keep (Lobby.model next)
                    posts, pulse.Every(Model.state (Lobby.model next)))

        member _.Standing = lock gate (fun () -> Lobby.described lobby)

type Aside<'Move, 'State, 'Notice>
    (opening: Solo<'Move, 'State, 'Notice>, fresh: unit -> string, keep: Model<'Move, 'State, 'Notice> -> string -> string option)
    =
    let gate = obj ()
    let mutable solo = opening

    member _.Change(change: Solo<'Move, 'State, 'Notice> -> Solo<'Move, 'State, 'Notice> * Post list) =
        lock gate (fun () ->
            let next, posts = change solo
            solo <- next
            posts)

    member this.Watches(console, shown, view, palette) =
        let game = Solo.game solo
        let palette = Palette.read game.Slots palette

        let view =
            Playable.byName shown palette game view
            |> Result.defaultValue (Playable.plainest shown palette game)

        this.Change(Solo.watching console { Margins = Margins.all; View = view })

    member _.Beats() =
        lock gate (fun () ->
            match (Solo.game solo).Pulse with
            | None -> [], TimeSpan.FromMinutes 1.0
            | Some pulse ->
                let next, posts, doing = Solo.beaten solo
                solo <- next

                match doing with
                | Keeping(model, stamp, _) -> keep model stamp |> ignore
                | Carrying
                | Leaving _ -> ()

                posts, pulse.Every(Model.state (Solo.model next)))

    member _.Said(console, line) =
        lock gate (fun () ->
            let next, posts, doing = Solo.said (fresh ()) console line solo
            solo <- next

            let alsoTold model stamp announce =
                match keep model stamp with
                | Some path when announce -> Solo.saying console $"Record saved to {path}." next
                | Some _
                | None -> []

            let said =
                match doing with
                | Carrying -> []
                | Keeping(model, stamp, announce) -> alsoTold model stamp announce
                | Leaving(Some model, stamp) -> alsoTold model stamp true
                | Leaving(None, _) -> []

            posts @ said)

[<NoComparison; NoEquality>]
type Hosting =
    abstract Name: string
    abstract Title: string

    abstract Fewest: int
    abstract Most: int

    abstract Shell: Shell
    abstract Slots: Slot list
    abstract Standard: Palette

    abstract Ways: (string * string) list

    abstract Deals: sitters: Sitter list * seed: uint64 option * way: string option -> Result<Table, string>

    abstract Resumes: path: string -> Result<Table, string>

module Hosting =

    let of'
        (ways: Playable<'Move, 'State, 'Notice> list)
        (clock: unit -> uint64)
        (stamping: Playable<'Move, 'State, 'Notice> -> string)
        =
        let plainest = List.head ways

        let dealing way =
            match way with
            | Some name ->
                ways
                |> List.tryFind (fun offered -> offered.Name = name)
                |> Option.defaultValue plainest
            | None -> plainest

        let table (game: Playable<'Move, 'State, 'Notice>) model sitters stamp =
            let rivals =
                game.Seating (Model.seed model) (Seating.machines sitters) (Model.state model)

            let keep model =
                if not (Journal.isEmpty model.Journal) then
                    Transcript.save game stamp sitters model.Journal |> ignore

            Held(Lobby.opened game model rivals, keep) :> Table

        { new Hosting with
            member _.Name = plainest.Name
            member _.Title = plainest.Title
            member _.Fewest = plainest.Fewest
            member _.Most = plainest.Most
            member _.Shell = plainest.Page
            member _.Slots = plainest.Slots
            member _.Standard = Playable.standard plainest
            member _.Ways = ways |> List.map (fun way -> way.Name, way.Blurb)

            member _.Deals(sitters, seed, way) =
                let game = dealing way

                Update.start game.Rules (List.length sitters) (seed |> Option.defaultWith clock)
                |> Result.map (fun model -> table game model sitters (stamping game))

            member _.Resumes path =
                let attempt =
                    ways
                    |> List.fold
                        (fun outcome game ->
                            match outcome with
                            | Ok _ -> outcome
                            | Error _ ->
                                Transcript.takenUp (fun _ -> "") game path
                                |> Result.map (fun (model, sitters, stamp, _) ->
                                    table game model sitters (stamp |> Option.defaultValue (stamping game))))
                        (Error $"There is no record at '{path}'.")

                attempt }
