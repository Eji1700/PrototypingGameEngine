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
open TCModel.Engine
open TCModel.Table

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
            | GotUp said -> console.SendAsync(Protocol.Call.GotUp, box said)
            | Nudged -> console.SendAsync Protocol.Call.Nudged)
        |> Task.WhenAll

/// Which table a connection is for.
///
/// One question with two answers, and it is asked rather than assumed because the hub below
/// is built by the framework from a type named in a route and cannot be handed a table when
/// it is registered. A process hosting one table answers the same way every time; a house
/// reads the route and looks the name up, and answers `None` for a name it does not know -
/// a link kept from a table that has since been swept away.
///
/// An interface rather than a function, because what registers it is a container that goes
/// by type.
type Finding =
    abstract At: HttpContext -> Table option

/// The wire itself: it turns a call into a change and a change back into calls, and
/// knows nothing else about the game.
///
/// Not generic, for the reason `Table` gives at length, and not holding a table either: it
/// holds the *question*. A house has several and which one a connection means is written in
/// the address it negotiated at, which is a thing only its own request can say.
type TableHub(finding: Finding, pages: Browser.Pages) =
    inherit Hub()

    let deliver (clients: IHubCallerClients) posts = Wire.deliver clients pages posts

    /// The table this connection is at, or nothing.
    ///
    /// Read per call rather than kept from the first one. A hub is made afresh for every
    /// call anyway - that is what the framework does with them - so there is nowhere to keep
    /// it that would not be a lie about how long it had been true.
    member private this.Theirs: Table option =
        match this.Context.GetHttpContext() with
        | null -> None
        | ctx -> finding.At ctx

    /// A console that arrived at a table which is not there, told so in the same words a
    /// full table uses and then let go. Saying nothing would leave it negotiated, connected
    /// and waiting on a board that is never coming - which is the exact failure this whole
    /// arrangement has produced once before.
    member private this.Nowhere() : Task =
        task {
            do! this.Clients.Caller.SendAsync(Protocol.Call.TurnedAway, box "There is no table by that name here.")
            this.Context.Abort()
        }

    member this.Join(token: string, view: string, palette: string) =
        match this.Theirs with
        | None -> this.Nowhere()
        | Some table ->

        let resuming = if String.IsNullOrWhiteSpace token then None else Some token

        // A fresh token is minted out here and handed in, so the lobby stays a value:
        // a table that invented its own tokens could not be folded twice to the same
        // answer, and nothing in this codebase is allowed to be that sort of thing.
        table.Sits(this.Context.ConnectionId, Guid.NewGuid().ToString "N", resuming, AtATerminal, view, palette)
        |> deliver this.Clients

    member this.Say(line: string) =
        match this.Theirs with
        | None -> this.Nowhere()
        | Some table -> table.Said(this.Context.ConnectionId, line) |> deliver this.Clients

    override this.OnDisconnectedAsync(_) =
        match this.Theirs with
        | None -> Task.CompletedTask
        | Some table -> table.Left this.Context.ConnectionId |> deliver this.Clients

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
    /// `table` is which of a house's this is, and nothing at a process holding one.
    let private takeSeatAt game reach table where =
        [ ""
          $"    {Launch.written game (Launch.Join(where, None, Reach.word reach, table))}"
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
    let private guarded (app: WebApplication) (drawn: Browser.Drawn) reach =
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

                    if waiting then Browser.tooOften ctx else Browser.turned drawn ctx)
            )
            |> ignore

    /// The four addresses a browser needs, mapped over whatever table is behind them.
    ///
    /// The same four whether there is a lobby back there or one hot seat, which is the
    /// whole point of `Sitting` being four functions: a page is a page.
    let private serving (app: WebApplication) (drawn: Browser.Drawn) sitting pages =
        app.MapGet(Page.Client, RequestDelegate(fun ctx -> Browser.script ctx))
        |> ignore

        app.MapGet("/", RequestDelegate(fun ctx -> Browser.page drawn ctx)) |> ignore

        app.MapGet(Page.Stream, RequestDelegate(fun ctx -> Browser.stream drawn sitting pages ctx :> Task))
        |> ignore

        app.MapPost(Page.Say, RequestDelegate(fun ctx -> Browser.say sitting ctx :> Task))
        |> ignore

        app.MapPost(Page.Amiss, RequestDelegate(fun ctx -> Browser.amiss ctx :> Task))
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
    let host game reach model sitters keep playing =
        let builder = WebApplication.CreateBuilder()

        let rivals =
            game.Seating (Model.seed model) (Seating.machines sitters) (Model.state model)

        let held = Held(Lobby.opened game model rivals, keep)
        let pages = Browser.Pages()

        // The console is a board, not a log. Anything the framework wants to say would
        // land in the middle of it.
        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddSignalR(waiting) |> ignore
        // Registered as what the wire asks for rather than as what it is: the hub takes a
        // `Finding`, and a container asked for a closed generic it was never given would drop
        // every console that tried to sit down.
        //
        // One table, so the question has one answer however it is asked. A house registers a
        // `Finding` that reads the route instead, and the hub cannot tell the difference -
        // which is the whole reason it asks rather than being handed a table.
        builder.Services.AddSingleton<Finding>(
            { new Finding with
                member _.At _ = Some(held :> Table) }
        )
        |> ignore

        builder.Services.AddSingleton<Browser.Pages> pages |> ignore
        listening builder reach

        let app = builder.Build()

        let drawn: Browser.Drawn =
            { Shell = game.Page
              Slots = game.Slots
              Standard = Playable.standard game }

        guarded app drawn reach
        app.MapHub<TableHub>(Protocol.Path) |> ignore

        // What a page needs of the table, which is what a console needs of it: a way to
        // change it, and a way for everybody to hear what came of that. The hub's own
        // clients rather than a caller's, because a move made in a browser has to reach
        // the terminals, and there is no call in progress to borrow them from.
        let hub = app.Services.GetRequiredService<IHubContext<TableHub>>()

        let sitting: Browser.Sitting =
            { Watching =
                fun console view palette ->
                    (held :> Table).Sits(console, Guid.NewGuid().ToString "N", None, InABrowser, view, palette)
              Said = fun console line -> held.Change(Lobby.said console line)
              Gone = fun console -> held.Change(Lobby.left console)
              Deliver = fun posts -> Wire.deliver hub.Clients pages posts |> ignore }

        // A hosted table settles nothing about colour on anybody's behalf: a console says
        // what it wants when it joins, and so does a page.
        serving app drawn sitting pages

        let seats = game.Rules.Seats(Model.state model)
        let mine, theirs = Seating.awaited sitters

        printfn ""
        printfn "=== A table for %d, waiting to be joined ===" seats
        printfn ""
        Seating.roster game.Skills sitters |> List.iter (printfn "%s")
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

                takeSeatAt game reach None (Reach.at reach "localhost")
                |> List.iter (printfn "%s")
            | _, 1 ->
                printfn "  One of these seats is yours, at this machine. Take it by running:"

                takeSeatAt game reach None (Reach.at reach "localhost")
                |> List.iter (printfn "%s")
            | _, mine ->
                printfn "  %d of these seats are yours, at this machine. Take one by running:" mine

                takeSeatAt game reach None (Reach.at reach "localhost")
                |> List.iter (printfn "%s")

        if theirs > 0 then
            if theirs = 1 then
                printfn "  One is somebody else's, from their own machine. They run:"
            else
                printfn "  %d are somebody else's, from their own machines. Each of them runs:" theirs

            takeSeatAt game reach None (Reach.told reach |> Option.defaultValue "<address>")
            |> List.iter (printfn "%s")

            printfn "  Both sit down at this one table, which is at:"
            printfn ""

            addresses reach
            |> List.iter (fun (address, who) -> printfn "    %-44s (%s)" address who)

            printfn ""

            // Said where it is true rather than everywhere, and said in terms of what it
            // costs. A board drawn for one seat is drawn for that seat alone - at a game with
            // anything held back, whoever is between two houses can read what was held.
            match reach.Wrapping, Reach.told reach with
            | InTheClear, Some _ ->
                printfn "  This table speaks http, so anything between it and a player can read the"
                printfn "  boards going past - and a board is drawn for one seat and nobody else."
                printfn "  Over anything further than a network you trust, put it behind a tunnel or"
                printfn "  a proxy that holds a certificate and say --behind, or hold one here with"
                printfn "  --cert."
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

        let game = Solo.game solo

        let app = builder.Build()

        let drawn: Browser.Drawn =
            { Shell = game.Page
              Slots = game.Slots
              Standard = standing }

        guarded app drawn reach

        let sitting: Browser.Sitting =
            { Watching = fun console view palette -> aside.Watches(console, InABrowser, view, palette)
              Said = fun console line -> aside.Said(console, line)
              Gone = fun console -> aside.Change(Solo.gone console)
              Deliver = fun posts -> posts |> List.iter (Browser.send pages) }

        serving app drawn sitting pages

        let seats = game.Rules.Seats(Model.state (Solo.model solo))

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

    // --- a house of them ----------------------------------------------------------------

    /// Where a *browser* goes for one of a house's tables. One place, because the page that
    /// links to a table and the route that answers for one are written a hundred lines apart.
    ///
    /// Not `/table/{id}`, and that is worth the sentence it costs. The hub lives there - a
    /// console dials `Protocol.Path` with the table's name on the end of it - and a page
    /// mapped at the same address wins the `GET`, so a console negotiated, was told where to
    /// connect, and was handed a page of HTML where its transport should have been. What it
    /// reports then is "the server disconnected before the handshake could be started", which
    /// sounds like anything at all except two routes fighting over one address.
    ///
    /// Nothing complains: both registrations are legal and the first match wins. It was found
    /// by joining a table, which is now the second time that has been the only way to find
    /// something in this file.
    let private tableAt (id: string) = $"/at/{id}"

    /// A house: several games of this one, listed on a page, dealt as people ask for them.
    ///
    /// What a house does *not* do is add a second way to play. Every board it serves is drawn
    /// by the same `Browser` handlers and the same hub a single hosted table uses, against the
    /// same `Table`; all a house adds is a front door, a name in a cookie, and a name in the
    /// hub's route.
    let house (hosting: Hosting) reach filling =
        let builder = WebApplication.CreateBuilder()

        let drawn: Browser.Drawn =
            { Shell = hosting.Shell
              Slots = hosting.Slots
              Standard = hosting.Standard }

        // Said out in full, because `Argument` has a `House` case for the command line and its
        // cases are in scope here - the same trap `Launch.written` walks round with `Name`.
        let home =
            TCModel.Net.House(hosting, (fun () -> DateTime.Now), Reach.minted, Housekeeping.ordinary)

        let pages = Browser.Pages()

        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddSignalR(waiting) |> ignore
        builder.Services.AddSingleton<Browser.Pages> pages |> ignore

        // Which table a console meant, read off the route it negotiated at. The hub is the
        // very same hub a single hosted table uses; all that differs is the answer to this.
        //
        // A name nobody knows here is answered with nothing rather than with the first table
        // to hand, and the hub turns that console away in words. A link kept from a table
        // that has since been swept is exactly this case, and it is not an error - it is a
        // game that is over.
        builder.Services.AddSingleton<Finding>(
            { new Finding with
                member _.At ctx =
                    match ctx.Request.RouteValues.TryGetValue "id" with
                    | true, id -> home.At(string id) |> Option.map (fun opened -> opened.Table)
                    | _ -> None }
        )
        |> ignore

        listening builder reach

        let app = builder.Build()
        guarded app drawn reach

        // The hub, at a route with the table's name in it. `Protocol.Path` is where a console
        // looks and `Client.join` puts the name on the end of it, so the two ends are written
        // from one constant and a house cannot come to disagree with the consoles dialling it.
        app.MapHub<TableHub>(Protocol.Path + "/{id}") |> ignore

        /// The table this browser is reading, if it is reading one that is still here.
        let theirs (ctx: HttpContext) =
            Browser.tableOf ctx |> Option.bind home.At

        // The hub's own clients rather than a caller's, for the reason the hosted table gives:
        // a move made in a browser has to reach the terminals at that table, and there is no
        // call in progress to borrow them from.
        let hub = app.Services.GetRequiredService<IHubContext<TableHub>>()

        /// What a page does at a table, which is what a page does at any table: the same four
        /// functions the one-table host builds, over whichever table this browser is at.
        let sitting (opened: Opened) : Browser.Sitting =
            { Watching =
                fun console view palette ->
                    opened.Table.Sits(console, Guid.NewGuid().ToString "N", None, InABrowser, view, palette)
              Said = fun console line -> opened.Table.Said(console, line)
              Gone = fun console -> opened.Table.Left console
              // Through the wire and not straight to the pages, which it was while a house
              // served browsers alone. A table in a house can have a terminal at it now, and a
              // page that delivered only to pages would move the game under a console that
              // was never told.
              Deliver = fun posts -> Wire.deliver hub.Clients pages posts |> ignore }

        /// A page whose table has gone - swept away, or a link kept from a house that has been
        /// restarted since. Sent back to the front rather than shown an error: the table is
        /// not missing, it is over, and what somebody wants next is the list.
        let elsewhere (ctx: HttpContext) =
            ctx.Response.Redirect "/"
            Task.CompletedTask

        let atTheirTable (ctx: HttpContext) doing =
            match theirs ctx with
            | Some opened -> doing opened
            | None -> elsewhere ctx

        app.MapGet(Page.Client, RequestDelegate(fun ctx -> Browser.script ctx))
        |> ignore

        // The front page, which is a list and a handful of links and nothing else.
        app.MapGet(
            "/",
            RequestDelegate(fun ctx ->
                let rows =
                    home.Listed
                    |> List.map (fun (opened, standing) ->
                        { Page.Where = tableAt opened.Id
                          Page.Name = opened.Id
                          Page.Stage =
                            match standing.Stage with
                            | Lobby.Filling -> "waiting"
                            | Lobby.Underway -> "being played"
                            | Lobby.Finished -> "finished"
                          Page.Seats = $"{standing.Sat} of {standing.Places - standing.Machines} seated"
                          Page.Sitters = String.Join(", ", standing.Sitters)
                          Page.Spare =
                            standing.Stage = Lobby.Filling
                            && standing.Sat < standing.Places - standing.Machines })

                let opening =
                    [ for players in hosting.Fewest .. hosting.Most -> players, $"/open?players={players}" ]

                ctx.Response.ContentType <- "text/html; charset=utf-8"
                ctx.Response.WriteAsync(Page.house hosting.Shell hosting.Standard opening rows))
        )
        |> ignore

        // Opening one. A link rather than a form, because everything it has to say is one
        // number - and a link is a thing somebody can send to whoever they are playing.
        app.MapGet(
            "/open",
            RequestDelegate(fun ctx ->
                let players =
                    match ctx.Request.Query.TryGetValue "players" with
                    | true, given when given.Count > 0 ->
                        match Int32.TryParse(string given[0]) with
                        | true, many -> many
                        | _ -> hosting.Fewest
                    | _ -> hosting.Fewest

                match home.Opens(Seating.here players, None, None) with
                | Ok opened ->
                    Browser.sitAt opened.Id ctx
                    ctx.Response.Redirect(tableAt opened.Id)
                    Task.CompletedTask
                // The game refused the size, in its own words. Back to the front, where the
                // sizes on offer are the ones it would not have refused.
                | Error _ -> elsewhere ctx)
        )
        |> ignore

        // A table. The board itself arrives down the stream like every other board there is;
        // all this does is remember which table this browser is at and hand over the shell.
        app.MapGet(
            tableAt "{id}",
            RequestDelegate(fun ctx ->
                match ctx.Request.RouteValues.TryGetValue "id" with
                | true, id when (home.At(string id)).IsSome ->
                    Browser.sitAt (string id) ctx
                    Browser.page drawn ctx
                | _ -> elsewhere ctx)
        )
        |> ignore

        app.MapGet(
            Page.Stream,
            RequestDelegate(fun ctx -> atTheirTable ctx (fun opened -> Browser.stream drawn (sitting opened) pages ctx :> Task))
        )
        |> ignore

        app.MapPost(
            Page.Say,
            RequestDelegate(fun ctx -> atTheirTable ctx (fun opened -> Browser.say (sitting opened) ctx :> Task))
        )
        |> ignore

        app.MapPost(Page.Amiss, RequestDelegate(fun ctx -> Browser.amiss ctx :> Task))
        |> ignore

        // Every game in `logs/` offered again, for a house coming back up. Said out loud,
        // because a house that quietly filled itself with a hundred old games would be a house
        // nobody could find a game in.
        if filling then
            let found =
                Transcript.saved ()
                |> List.filter (fun record -> record.Game = Some hosting.Name)
                |> List.choose (fun record -> home.Resumes record.Path |> Result.toOption)

            printfn "  Took up %d game(s) from logs/." (List.length found)

        // Nothing else calls this, so something has to. A timer rather than a sweep on every
        // request: what it costs is a walk of a short list, and what it buys is a house that
        // does not grow while nobody is looking at it.
        let broom =
            new Timers.Timer(TimeSpan.FromMinutes(5.0).TotalMilliseconds, AutoReset = true)

        broom.Elapsed.Add(fun _ ->
            match home.Swept() with
            | [] -> ()
            | gone -> printfn "  Swept %d table(s) nobody was at." (List.length gone))

        broom.Start()

        printfn ""
        printfn "=== A house of %s ===" hosting.Title
        printfn ""
        printfn "  Open in a browser:"
        printfn ""

        for where, who in addresses reach do
            printfn "    %-44s %s" (Reach.opened reach where) who

        printfn ""
        printfn "  Whoever opens a table there reads its address out to whoever is playing."
        printfn ""
        printfn "  A player at a terminal joins one by name, which the list on that page shows:"
        printfn ""

        printfn
            "    %s"
            (Launch.writtenFor hosting.Name (Launch.Join(Reach.at reach "localhost", None, Reach.word reach, Some "<table>")))

        match Reach.word reach with
        | Some word -> printfn "  The word at the door is %s." word
        | None -> printfn "  There is no word at the door."

        printfn ""

        app.Run()
        0
