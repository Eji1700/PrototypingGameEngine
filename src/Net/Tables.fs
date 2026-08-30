namespace Prototyping.Net

open System
open Prototyping.Engine
open Prototyping.Table

type Table =
    abstract Sits:
        console: string * offered: string * resuming: string option * shown: Shown * view: string * palette: string -> Post list

    abstract Said: console: string * line: string -> Post list

    abstract Left: console: string -> Post list

    /// Beat once, and say when the next is due - or that none ever is, at a game with no clock.
    abstract Beats: unit -> Post list * TimeSpan option

    abstract Standing: Lobby.Standing

module Table =

    /// Sitting down with a token minted here for the seat, the way a door's word is minted: twelve
    /// letters a person can read out and type back, since the token is what a console is told to
    /// say with --token to get its seat back. The one place a token is made, where three used to be.
    let sits (table: Table) console resuming shown view palette =
        table.Sits(console, Reach.minted (), resuming, shown, view, palette)

/// The view a console asked for by name, as its kind of console reads it and in the colours it
/// sent - or the plainest of its kind where the name means nothing here, since a console that
/// mistyped a view still wants to sit down.
module private Arriving =

    let viewed shown (game: Playable<_, _, _>) (view: string) (palette: string) =
        let palette = Palette.read game.Slots palette

        Playable.byName shown palette game view
        |> Result.defaultValue (Playable.plainest shown palette game)

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
        // The game is read off `lobby` outside the lock, which is safe for the one thing read: a
        // table's game never changes hands.
        member this.Sits(console, offered, resuming, shown, view, palette) =
            this.Change(Lobby.join console offered resuming (Arriving.viewed shown (Lobby.game lobby) view palette))

        member this.Said(console, line) = this.Change(Lobby.said console line)

        member this.Left console = this.Change(Lobby.left console)

        member this.Beats() =
            lock gate (fun () ->
                match (Lobby.game lobby).Pulse with
                | None -> [], None
                | Some pulse ->
                    let next, posts = Lobby.beaten lobby
                    lobby <- next
                    keep (Lobby.model next)
                    posts, Some(pulse.Every(Model.state (Lobby.model next))))

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

    // As at `Held.Sits`: `solo` is read outside the lock for its game alone, which never changes.
    member this.Watches(console, shown, view, palette) =
        this.Change(
            Solo.watching
                console
                { Margins = Margins.all
                  Hushed = false
                  View = Arriving.viewed shown (Solo.game solo) view palette }
        )

    member _.Beats() =
        lock gate (fun () ->
            match (Solo.game solo).Pulse with
            | None -> [], None
            | Some pulse ->
                let next, posts, doing = Solo.beaten solo
                solo <- next

                match doing with
                | Keeping(model, stamp, _) -> keep model stamp |> ignore
                | Carrying
                | Leaving _ -> ()

                posts, Some(pulse.Every(Model.state (Solo.model next))))

    member _.Said(console, line) =
        lock gate (fun () ->
            let next, posts, doing = Solo.said fresh console line solo
            solo <- next

            let alsoTold model stamp announce =
                match keep model stamp with
                | Some path when announce -> Solo.saying console (Transcript.announced path) next
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
            let keep model =
                Transcript.kept game stamp sitters model |> ignore

            Held(Lobby.openedFor game model sitters, keep) :> Table

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

            // Every way of playing is tried in turn, since which one a record is of is not in its
            // name; the first that reads it is the one, and the last refusal is what is said.
            member _.Resumes path =
                let taken game =
                    Transcript.takenUp (fun _ -> "") game path
                    |> Result.map (fun (taken: Transcript.TakenUp<_, _, _>) ->
                        table game taken.Model taken.Sitters (taken.Stamp |> Option.defaultValue (stamping game)))

                List.tail ways
                |> List.fold
                    (fun outcome game ->
                        match outcome with
                        | Ok _ -> outcome
                        | Error _ -> taken game)
                    (taken plainest) }
