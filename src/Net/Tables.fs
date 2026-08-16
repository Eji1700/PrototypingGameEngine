namespace TCModel.Net

open TCModel.Engine
open TCModel.Table

/// What the wire needs of a table, and the whole of it: somebody sits down, somebody says
/// something, somebody goes, and each of those comes back as a list of things to say.
///
/// No type parameters, and that is the point of it existing. The hub below is built by the
/// framework's own container from a type named in a route, and a *generic* type named there
/// cannot be tied to the game being played: F# infers the arguments of `MapHub<TableHub<_,_,_>>`
/// as `obj`, the container is then asked for a `TableHub<obj,obj,obj>` it has never heard of,
/// and hub activation throws. What that looks like from the far end is a console that
/// negotiates, connects, and is dropped without a word - which is why this was found by
/// joining a table rather than by anything that reads code.
///
/// So the types stop here, the same way they stop at `Chosen`: by the time the wire is
/// involved everything being said is a string or a number anyway.
type Table =
    /// Take a seat, or come back to one. `resuming` is the token of a seat already held;
    /// `offered` is the one a new seat would be given, minted outside so the lobby stays a
    /// value. The view and the colours are the words a console sends, read at this end
    /// against the game actually being played.
    abstract Sits: console: string * offered: string * resuming: string option * view: string * palette: string -> Post list

    abstract Said: console: string * line: string -> Post list

    abstract Left: console: string -> Post list

    /// What this table looks like from the door: how far along it is, how many seats are
    /// going spare, and who is at the ones that are not.
    ///
    /// The only member here that does not change anything, and the only one a house of tables
    /// needs before somebody has decided which table they are joining. Taken under the same
    /// lock as the rest, so a list of tables cannot catch one halfway through a move.
    abstract Standing: Lobby.Standing

/// The one lobby this process is hosting.
///
/// Every change goes through here under a lock, so the pure fold inside never sees two
/// players at once and the game can never be half-moved. What comes back out is the list
/// of things to say, which is the only part that touches the wire.
///
/// Generic in the game for the same reason the lobby is: nothing here reads a board. It is
/// a lock, a mutable slot and a file being written.
type Held<'Move, 'State, 'Notice>(opening: Lobby<'Move, 'State, 'Notice>, keep: Model<'Move, 'State, 'Notice> -> unit) =
    let gate = obj ()
    let mutable lobby = opening

    member _.Change(change: Lobby<'Move, 'State, 'Notice> -> Lobby<'Move, 'State, 'Notice> * Post list) =
        lock gate (fun () ->
            let next, posts = change lobby
            lobby <- next
            // The record is written after every change rather than at the end, because
            // a table with people at it can lose its host without warning.
            keep (Lobby.model next)
            posts)

    interface Table with
        member this.Sits(console, offered, resuming, view, palette) =
            let game = Lobby.game lobby

            // A view a table has never heard of is no reason to turn a player away; they can
            // ask for another once they are sitting down. Colours the table does not know are
            // passed over the same way, one at a time, by `Palette.read`.
            let palette = Palette.read game.Slots palette

            let view =
                Playable.byName AtATerminal palette game view
                |> Result.defaultValue (Playable.plainest AtATerminal palette game)

            this.Change(Lobby.join console offered resuming view)

        member this.Said(console, line) = this.Change(Lobby.said console line)

        member this.Left console = this.Change(Lobby.left console)

        // Under the gate like every other reading of the lobby, and it has to be: the slot is
        // written by whichever thread last moved the game, and a description read beside that
        // write is a description of neither state.
        member _.Standing = lock gate (fun () -> Lobby.described lobby)

/// The same, for the one hot seat this process is serving to a browser.
///
/// `Solo` says what a typed line does and what it wants written down; this does the writing
/// and hands back what to show. The record goes out after every change here too, for a
/// reason a local game did not have before: a page has no way of putting the game down on
/// its way out, so there is no last moment to save at.
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

    member _.Said(console, line) =
        lock gate (fun () ->
            let next, posts, doing = Solo.said (fresh ()) console line solo
            solo <- next

            // A record the player asked for is answered where they asked - the board is
            // already on its way down the stream, and a file's name is not part of a board.
            // Through the table rather than as a bare `Told`, so that it comes out in the
            // words the reader's own view speaks: a page wants markup where a terminal
            // wants a line.
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
                // A console that only read the game back has nothing to write on its way out.
                | Leaving(None, _) -> []

            posts @ said)

/// Everything the table says, said.
///
/// A console at a terminal has a socket SignalR is holding open; a console in a browser has
/// a stream holding itself open. Which of the two any console is, is written into its id
/// and nowhere else - the lobby that addressed the post has no idea there are two kinds,
/// Opening a table, without knowing what a move is at this game.
///
/// The second of the two seams a house of tables needs, and the one that did not already
/// exist. `Table` above is what a house does with a table once it has one; this is where one
/// comes from - and it has to be its own thing because dealing is the last part of hosting
/// that is still generic in the game. `Server.host` takes a model that has already been dealt,
/// which is fine for a process holding one table and no use at all to something holding a
/// list, which must be able to deal on demand while holding nothing but `Table`s.
///
/// The types stop here for the third time in this program, and by the same trick each time:
/// an interface with no parameters, implemented by closing over a game. `Rules` seals what a
/// game *is*, `Playable` seals how it is read, `Chosen` seals it for a list to hold, and this
/// seals it for a house to deal from.
[<NoComparison; NoEquality>]
type Hosting =
    /// What the game is called, and to a person. A house is one game's house, so it says so at
    /// the top of every page it draws.
    abstract Name: string
    abstract Title: string

    /// How many may sit down, so a house can refuse a table of the wrong size before dealing
    /// one rather than after.
    abstract Fewest: int
    abstract Most: int

    /// Every way this game can be played, the plainest first: the name, and a line about it.
    ///
    /// What the "new table" form offers. A game with one way offers a list of one and the form
    /// has nothing to ask, which is the ordinary case.
    abstract Ways: (string * string) list

    /// Deal one and hand back something to play it.
    ///
    /// `sitters` says who is a person and who is the machine and at what strength, in the
    /// words `Seating` already reads - so the form that asks is the seat list this program has
    /// already written and already checked, and how many are playing is the length of it
    /// rather than a second thing to keep in step.
    ///
    /// `way` is which of `Ways` to deal, and it is carried here rather than read from a
    /// settings file because it belongs to the table and not to the machine the house is
    /// running on: two people must be able to hold a plain game and a game with the optional
    /// rule in it in the same house at the same time. A name that is none of this game's is
    /// the plainest way, which is the same answer a settings file gets for a way that has
    /// since been renamed.
    abstract Deals: sitters: Sitter list * seed: uint64 option * way: string option -> Result<Table, string>

    /// And take one up off a record, which is the same question asked of a file - and the
    /// whole of what a house needs to come back from being restarted.
    abstract Resumes: path: string -> Result<Table, string>

module Hosting =

    /// A game as one of those: every way it can be played, the plainest first.
    ///
    /// Beside `Play.chosen` in spirit and not in place, and the reason is the test scripts.
    /// Everything from here down is checkable with `dotnet fsi` because nothing in this file
    /// has met a socket; `Play.fs` is compiled after the whole of the wire and could not be
    /// loaded into a script without it. A seam whose only implementation cannot be checked
    /// without a web server is a seam that will be checked by starting a web server.
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

        /// A dealt game with its record already being kept. Both ways in end here, because a
        /// table taken up off a file and a table dealt fresh differ in where the model came
        /// from and in nothing after that.
        let table (game: Playable<'Move, 'State, 'Notice>) model sitters stamp =
            let rivals =
                game.Seating (Model.seed model) (Seating.machines sitters) (Model.state model)

            // Written after every change rather than at the end, for the reason the one-table
            // host already gives: a table with people at it can lose its host without warning.
            // In a house that stops being a precaution and becomes the way back - a house is
            // rebuilt from these files and from nothing else.
            let keep model =
                if not (Journal.isEmpty model.Journal) then
                    Transcript.save game stamp sitters model.Journal |> ignore

            Held(Lobby.opened game model rivals, keep) :> Table

        { new Hosting with
            member _.Name = plainest.Name
            member _.Title = plainest.Title
            member _.Fewest = plainest.Fewest
            member _.Most = plainest.Most
            member _.Ways = ways |> List.map (fun way -> way.Name, way.Blurb)

            member _.Deals(sitters, seed, way) =
                let game = dealing way

                Update.start game.Rules (List.length sitters) (seed |> Option.defaultWith clock)
                |> Result.map (fun model -> table game model sitters (stamping game))

            member _.Resumes path =
                // Every way this game can be played is tried, because a record says which of
                // them it is and a house holding both must open each as itself. The plainest
                // is asked first, so a record from before this game had a second way - written
                // when its name said nothing - is still taken up as the game it was.
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
