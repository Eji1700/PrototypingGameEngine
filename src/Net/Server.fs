namespace TCModel.Net

open System
open System.Net
open System.Net.Sockets
open System.Threading.RateLimiting
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.HttpOverrides
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open TCModel.Domain
open TCModel.App
open TCModel.Console

/// The one lobby this process is hosting.
///
/// Every change goes through here under a lock, so the pure fold inside never sees two
/// players at once and the game can never be half-moved. What comes back out is the list
/// of things to say, which is the only part that touches the wire.
type Held(opening: Lobby, keep: Model -> unit) =
    let gate = obj ()
    let mutable lobby = opening

    member _.Change(change: Lobby -> Lobby * Post list) =
        lock gate (fun () ->
            let next, posts = change lobby
            lobby <- next
            // The record is written after every change rather than at the end, because
            // a table with people at it can lose its host without warning.
            keep (Lobby.model next)
            posts)

/// The same, for the one hot seat this process is serving to a browser.
///
/// `Solo` says what a typed line does and what it wants written down; this does the writing
/// and hands back what to show. The record goes out after every change here too, for a
/// reason a local game did not have before: a page has no way of putting the game down on
/// its way out, so there is no last moment to save at.
type Aside(opening: Solo, fresh: unit -> string, keep: Model -> string -> string option) =
    let gate = obj ()
    let mutable solo = opening

    member _.Change(change: Solo -> Solo * Post list) =
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
                | Leaving(model, stamp) -> alsoTold model stamp true

            posts @ said)

/// Everything the table says, said.
///
/// A console at a terminal has a socket SignalR is holding open; a console in a browser has
/// a stream holding itself open. Which of the two any console is, is written into its id
/// and nowhere else - the lobby that addressed the post has no idea there are two kinds,
/// and does not need one.
module Wire =

    // The clients are said as the generic interface rather than as `IHubClients`, because
    // that is the one thing both ends of this have in common: a hub answering a call holds
    // `IHubCallerClients`, and a page's request holds the hub's own `IHubClients`, and
    // neither of those is the other.
    let deliver (clients: IHubClients<IClientProxy>) (pages: Browser.Pages) posts =
        posts
        |> List.map (fun post ->
            if Browser.isPage post.To then
                Browser.send pages post
                Task.CompletedTask
            else

            let console = clients.Client post.To

            match post.Say with
            | Seated(seat, token) -> console.SendAsync(Protocol.Call.Seated, box seat, box token)
            | Screen text -> console.SendAsync(Protocol.Call.Screen, box text)
            | Told text -> console.SendAsync(Protocol.Call.Told, box text)
            | TurnedAway why -> console.SendAsync(Protocol.Call.TurnedAway, box why)
            | Nudged -> console.SendAsync Protocol.Call.Nudged)
        |> Task.WhenAll

/// The wire itself: it turns a call into a change and a change back into calls, and
/// knows nothing else about the game.
type TableHub(held: Held, pages: Browser.Pages) =
    inherit Hub()

    let deliver (clients: IHubCallerClients) posts = Wire.deliver clients pages posts

    member this.Join(token: string, view: string, palette: string) =
        let resuming = if String.IsNullOrWhiteSpace token then None else Some token

        // A view a table has never heard of is no reason to turn a player away; they can
        // ask for another once they are sitting down. Colours the table does not know are
        // passed over the same way, one at a time, by `Palette.read`.
        let palette = Palette.read palette

        let view =
            View.byName AtATerminal palette view |> Result.defaultValue (View.plain palette)

        // A fresh token is minted out here and handed in, so the lobby stays a value:
        // a table that invented its own tokens could not be folded twice to the same
        // answer, and nothing in this codebase is allowed to be that sort of thing.
        held.Change(Lobby.join this.Context.ConnectionId (Guid.NewGuid().ToString "N") resuming view)
        |> deliver this.Clients

    member this.Say(line: string) =
        held.Change(Lobby.said this.Context.ConnectionId line) |> deliver this.Clients

    override this.OnDisconnectedAsync(_) =
        held.Change(Lobby.left this.Context.ConnectionId) |> deliver this.Clients

module Server =

    /// Every address this machine can be reached at, so whoever is hosting can read one
    /// out to the room.
    let private reachableAt reach =
        try
            Dns.GetHostAddresses(Dns.GetHostName())
            |> Array.filter (fun address -> address.AddressFamily = AddressFamily.InterNetwork)
            |> Array.map (fun address -> Reach.at reach (string address), "on this network")
            |> List.ofArray
        with _ ->
            []

    /// Where this table is, in the order the addresses are worth trying, with a word on each
    /// saying who it is for.
    ///
    /// What was given with `--at` comes first and is not checked against anything: it is the
    /// name in somebody's DNS, the tunnel that forwards here, the port a router sends on.
    /// None of that is knowable from inside this process, and the one machine that does know
    /// is the one whoever typed it is sitting at.
    let private addresses reach =
        (Reach.told reach
         |> Option.map (fun given -> given, "what you told them to use")
         |> Option.toList)
        @ reachableAt reach
        @ [ Reach.at reach "localhost", "for anyone on this machine" ]

    /// What a player is told to type, and what they are told to open. Both written from the
    /// declaration the command line is read by, so what is read out to somebody is something
    /// this program is certain to accept.
    let private takeSeatAt reach where =
        [ ""
          $"    dotnet run -- {Launch.write (Launch.Join(where, None, Reach.word reach))}"
          ""
          $"  or open {Reach.opened reach where} in a browser."
          "" ]

    /// How long a table waits on a console before deciding it has gone.
    ///
    /// The defaults were written for a request that takes a moment; this is a socket held
    /// open across a game that takes an evening, over whatever is between two houses. So the
    /// table speaks up more often than it is asked to - a connection with nothing on it is
    /// closed by things in the middle, and the same silence that a browser's stream has to
    /// keep warm has to be kept warm here - and it waits a good deal longer before giving up
    /// on the far end, because a console that has merely gone quiet for twenty seconds is
    /// not a console that has gone.
    ///
    /// Losing one costs a player their seat until they come back to it, which they can, so
    /// none of this is a correctness matter. It is the difference between a game that
    /// survives a bad minute and one that spends it reconnecting.
    ///
    /// And a cap on how much a console may say at once, which is new here and is nothing to
    /// do with latency: a table anybody can reach is a table anybody can send anything to,
    /// and what a player is ever entitled to send is one typed line.
    let private waiting (options: HubOptions) =
        options.KeepAliveInterval <- TimeSpan.FromSeconds 15.0
        options.ClientTimeoutInterval <- TimeSpan.FromSeconds 60.0
        options.HandshakeTimeout <- TimeSpan.FromSeconds 30.0
        options.MaximumReceiveMessageSize <- Nullable 32768L

    /// Where the table listens, and what it is wrapped in on the way out.
    ///
    /// A certificate held here is bound with the port; everything else listens in the clear,
    /// including a table behind a tunnel or a proxy - there the encryption ends at that
    /// door, and what reaches this one is plain http from the machine next to it. What the
    /// difference costs is that this process can no longer see whether a player is speaking
    /// https, which is why the forwarded headers below are read: a table that guessed would
    /// mark its cookies for a connection the browser is not using.
    let private listening (builder: WebApplicationBuilder) reach =
        match reach.Wrapping with
        | Kept(certificate, password) ->
            builder.WebHost.ConfigureKestrel(fun options ->
                options.ListenAnyIP(
                    reach.Port,
                    fun listen ->
                        match password with
                        | Some password -> listen.UseHttps(certificate, password) |> ignore
                        | None -> listen.UseHttps certificate |> ignore
                ))
            |> ignore
        | InTheClear
        | Ahead -> builder.WebHost.UseUrls $"http://0.0.0.0:{reach.Port}" |> ignore

    /// How fast anybody may get the word wrong.
    ///
    /// A bucket that holds this many tries and drips one back every so often, which is the
    /// shape the thing being guarded actually has: a person who mistypes a word twice, or a
    /// browser that fetches two or three things before it has been handed a cookie, is not
    /// somebody to slow down, and the eleventh wrong answer in five seconds is nobody's
    /// fingers.
    let private bucket many (dripping: TimeSpan) =
        TokenBucketRateLimiterOptions(
            TokenLimit = many,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = dripping,
            QueueLimit = 0,
            AutoReplenishment = true
        )

    /// And what stands in front of the whole of it: what somebody in front of this said
    /// about the player, and the word at the door.
    let private guarded (app: WebApplication) standing reach =
        match reach.Wrapping with
        | InTheClear
        | Kept _ -> ()
        | Ahead ->
            // Every proxy trusted, which is not a thing to do lightly and is the only
            // honest answer here: a tunnel hands a table a connection from an address that
            // changes without warning, and there is nothing in this program that could know
            // the right one. What it is trusted *for* is one thing - saying that the player
            // at the far end is speaking https - and the whole of what that settles is
            // whether a cookie is marked secure.
            let forwarded =
                ForwardedHeadersOptions(
                    ForwardedHeaders =
                        (ForwardedHeaders.XForwardedFor
                         ||| ForwardedHeaders.XForwardedProto
                         ||| ForwardedHeaders.XForwardedHost)
                )

            forwarded.KnownIPNetworks.Clear()
            forwarded.KnownProxies.Clear()
            app.UseForwardedHeaders forwarded |> ignore

        match reach.Doorway with
        | Ajar -> ()
        | Locked _ ->

            // Only wrong answers are counted, which is what makes counting them safe. A
            // player who has the word never touches either of these, however fast they play,
            // so nothing here can come between somebody and a game they were invited to.
            //
            // Two, because one of them can be got round. The first is per caller, and past a
            // tunnel that is the address the tunnel says it came from - which anybody who can
            // reach this machine directly is free to make up, and by making up a new one each
            // time would have a fresh bucket every try. So the second counts the door itself,
            // however many addresses the tries arrive from. What that costs when it is spent
            // is that somebody arriving with the *wrong* word is told to wait rather than
            // shown the box to type it into; somebody arriving with the right one is let in
            // regardless, which is the half that matters.
            let caller =
                PartitionedRateLimiter.Create<HttpContext, string>(fun ctx ->
                    let whoever =
                        match ctx.Connection.RemoteIpAddress with
                        | null -> "somewhere"
                        | address -> string address

                    RateLimitPartition.GetTokenBucketLimiter(whoever, (fun _ -> bucket 10 (TimeSpan.FromSeconds 5.0))))

            let door = new TokenBucketRateLimiter(bucket 60 (TimeSpan.FromSeconds 1.0))

            // One place for the whole table rather than one per address, because the ways in
            // are not all pages: a console at a terminal arrives at the hub, which is not
            // routed through anything below and would otherwise be a door left open beside a
            // locked one.
            app.Use(
                Func<HttpContext, RequestDelegate, Task>(fun ctx next ->
                    if Reach.admits reach (Browser.presented ctx) then
                        Browser.remember reach ctx
                        next.Invoke ctx
                    else

                    // Both asked whatever the first says, so that a caller with an empty
                    // bucket still spends the door's: the two together are what a stranger
                    // is held to, and taking the second only when the first allowed it would
                    // let somebody with a new address every time past the pair of them.
                    let waiting =
                        use mine = caller.AttemptAcquire ctx
                        use all = door.AttemptAcquire()
                        not (mine.IsAcquired && all.IsAcquired)

                    if waiting then Browser.tooOften ctx else Browser.turned standing ctx)
            )
            |> ignore

    /// The four addresses a browser needs, mapped over whatever table is behind them.
    ///
    /// The same four whether there is a lobby back there or one hot seat, which is the
    /// whole point of `Sitting` being four functions: a page is a page.
    let private serving (app: WebApplication) standing sitting pages =
        app.MapGet(Html.Client, RequestDelegate(fun ctx -> Browser.script ctx))
        |> ignore

        app.MapGet("/", RequestDelegate(fun ctx -> Browser.page standing ctx)) |> ignore

        app.MapGet(Html.Stream, RequestDelegate(fun ctx -> Browser.stream standing sitting pages ctx :> Task))
        |> ignore

        app.MapPost(Html.Say, RequestDelegate(fun ctx -> Browser.say sitting ctx :> Task))
        |> ignore

        app.MapPost(Html.Amiss, RequestDelegate(fun ctx -> Browser.amiss ctx :> Task))
        |> ignore

    /// Open a table and wait at it. Blocks until the host stops the process, which is
    /// how a table is closed: there is no move for closing one, because no player at it
    /// has the standing to close it on everybody else.
    ///
    /// The seating settles who is waited for and who is not. A seat the machine plays is
    /// played here, by this process, and is never an empty chair; the rest are sat down at
    /// from a console or a browser, whether that console is in this room or two rooms away.
    ///
    /// `playing` is what to do once the table is listening and before it is waited on, which
    /// at a table with a seat of the host's own is a console sitting down at it from this very
    /// machine. It comes in as a function because nothing here could do it: a client is
    /// compiled after this file, and rightly - the table has no business knowing there is
    /// such a thing. What it means for the shape of this is that the waiting comes apart in
    /// two: the table is started, somebody plays at it, and when they get up it goes on
    /// standing, because their leaving their seat is not the same as closing the room.
    let host reach model sitters keep playing =
        let builder = WebApplication.CreateBuilder()

        let rivals =
            Rival.seating (Model.seed model) (Seating.machines sitters) (Model.game model)

        let held = Held(Lobby.opened model rivals, keep)
        let pages = Browser.Pages()

        // The console is a board, not a log. Anything the framework wants to say would
        // land in the middle of it.
        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddSignalR(waiting) |> ignore
        builder.Services.AddSingleton<Held> held |> ignore
        builder.Services.AddSingleton<Browser.Pages> pages |> ignore
        listening builder reach

        let app = builder.Build()
        guarded app Palette.standard reach
        app.MapHub<TableHub>(Protocol.Path) |> ignore

        // What a page needs of the table, which is what a console needs of it: a way to
        // change it, and a way for everybody to hear what came of that. The hub's own
        // clients rather than a caller's, because a move made in a browser has to reach
        // the terminals, and there is no call in progress to borrow them from.
        let hub = app.Services.GetRequiredService<IHubContext<TableHub>>()

        let sitting: Browser.Sitting =
            { Watching = fun console view -> held.Change(Lobby.join console (Guid.NewGuid().ToString "N") None view)
              Said = fun console line -> held.Change(Lobby.said console line)
              Gone = fun console -> held.Change(Lobby.left console)
              Deliver = fun posts -> Wire.deliver hub.Clients pages posts |> ignore }

        // A hosted table settles nothing about colour on anybody's behalf: a console says
        // what it wants when it joins, and so does a page.
        serving app Palette.standard sitting pages

        let seats = Game.playerCount (Model.game model)
        let mine, theirs = Seating.awaited sitters

        printfn ""
        printfn "=== A table for %d, waiting to be joined ===" seats
        printfn ""
        Seating.roster sitters |> List.iter (printfn "%s")
        printfn ""

        // The word first, because everything under it carries it and somebody reading this
        // out to a room needs to have seen it before they get to the address.
        match Reach.word reach with
        | Some code ->
            printfn "  The word at this table's door:  %s" code
            printfn ""
        | None ->
            printfn "  No word at the door: whoever can reach the address below may sit down."
            printfn ""

        // The machine's seats are already filled, so what is read out to the room is the
        // chairs that are not - and some of those are very often the host's own. One of those
        // is taken from here as soon as the table is up, so what is left to say about it is
        // only how to take the *rest*, and at most tables there are none.
        let claimed = if Option.isSome playing then 1 else 0

        if mine > 0 then
            match claimed, mine with
            | 1, 1 ->
                printfn "  One of these seats is yours, and this console is about to take it."
                printfn ""
            | 1, mine ->
                printfn "  %d of these seats are yours. This console takes one; the others are taken" mine
                printfn "  from another terminal on this machine, by running:"
                takeSeatAt reach (Reach.at reach "localhost") |> List.iter (printfn "%s")
            | _, 1 ->
                printfn "  One of these seats is yours, at this machine. Take it by running:"
                takeSeatAt reach (Reach.at reach "localhost") |> List.iter (printfn "%s")
            | _, mine ->
                printfn "  %d of these seats are yours, at this machine. Take one by running:" mine
                takeSeatAt reach (Reach.at reach "localhost") |> List.iter (printfn "%s")

        if theirs > 0 then
            if theirs = 1 then
                printfn "  One is somebody else's, from their own machine. They run:"
            else
                printfn "  %d are somebody else's, from their own machines. Each of them runs:" theirs

            takeSeatAt reach (Reach.told reach |> Option.defaultValue "<address>")
            |> List.iter (printfn "%s")

            printfn "  Both sit down at this one table, which is at:"
            printfn ""

            addresses reach
            |> List.iter (fun (address, who) -> printfn "    %-44s (%s)" address who)

            printfn ""

            // Said where it is true rather than everywhere, and said in terms of what it
            // costs: a game read by whoever is between two houses is a game whose bags are
            // no longer private, which is the one thing the rules are built around.
            match reach.Wrapping, Reach.told reach with
            | InTheClear, Some _ ->
                printfn "  This table speaks http, so anything between it and a player can read the"
                printfn "  boards going past - which at this game means their stones. Over anything"
                printfn "  further than a network you trust, put it behind a tunnel or a proxy that"
                printfn "  holds a certificate and say --behind, or hold one here with --cert."
                printfn ""
            | (InTheClear | Kept _ | Ahead), _ -> ()

        match mine + theirs - claimed with
        | 1 -> printfn "  The game begins once that seat is taken. Ctrl+C closes the table."
        | waited -> printfn "  The game begins once all %d open seats are taken. Ctrl+C closes the table." waited

        printfn ""

        match playing with
        | None ->
            app.Run()
            0
        | Some playing ->
            // Started rather than run, so that there is a table for the console below to sit
            // down at - `Start` comes back when the port is answering and not before. And
            // waited on afterwards rather than stopped, because a player leaving their seat
            // is not the same as closing the room: whoever else is here is still playing, and
            // the table stands until Ctrl+C as it always did.
            app.Start()
            playing ()
            app.WaitForShutdown()
            0

    /// Play a game here, in a browser, with nobody else involved.
    ///
    /// This is `play` with a page instead of a terminal, not `host` with the waiting taken
    /// out. There are no seats: it is the one hot seat a keyboard has, and the screen
    /// belongs to whoever is to play, so it starts the moment it is opened and every move
    /// is yours to make. Which is also why there is no hub here - there is nobody to reach.
    let serve reach standing solo fresh keep =
        let builder = WebApplication.CreateBuilder()

        let aside = Aside(solo, fresh, keep)
        let pages = Browser.Pages()

        builder.Logging.ClearProviders() |> ignore
        listening builder reach

        let app = builder.Build()
        guarded app standing reach

        let sitting: Browser.Sitting =
            { Watching = fun console view -> aside.Change(Solo.watching console { Notes = true; View = view })
              Said = fun console line -> aside.Said(console, line)
              Gone = fun console -> aside.Change(Solo.gone console)
              Deliver = fun posts -> posts |> List.iter (Browser.send pages) }

        serving app standing sitting pages

        let seats = Game.playerCount (Model.game (Solo.model solo))

        printfn ""
        printfn "=== A game for %d, to play in a browser ===" seats
        printfn ""
        printfn "  Open:"
        printfn ""
        printfn "    %s" (Reach.opened reach (Reach.at reach "localhost"))
        printfn ""
        printfn "  One seat, and it changes hands with the turn - the same as playing at"
        printfn "  this keyboard. Ctrl+C puts it down."
        printfn ""

        match Reach.word reach with
        | Some code ->
            printfn "  The word at the door is in that address, and again here: %s" code
            printfn ""
        | None -> ()

        printfn "  Others can watch and play too, at:"
        printfn ""

        addresses reach
        |> List.iter (fun (address, who) -> printfn "    %-44s (%s)" (Reach.opened reach address) who)

        printfn ""

        app.Run()
        0
