namespace Prototyping.Net

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
open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

module Wire =

    let deliver (clients: IHubClients<IClientProxy>) (pages: Pages) posts =
        posts
        |> List.map (fun post ->
            if Browser.isPage post.To then
                Browser.send pages post
                Task.CompletedTask
            else

            let console = clients.Client post.To

            match post.Say with
            | ToPlayer.Seated(seat, token) -> console.SendAsync(Protocol.Call.Seated, box seat, box token)
            | ToPlayer.Screen text -> console.SendAsync(Protocol.Call.Screen, box text)
            | ToPlayer.Told text -> console.SendAsync(Protocol.Call.Told, box text)
            | ToPlayer.TurnedAway why -> console.SendAsync(Protocol.Call.TurnedAway, box why)
            | ToPlayer.GotUp said -> console.SendAsync(Protocol.Call.GotUp, box said)
            | ToPlayer.Nudged -> console.SendAsync Protocol.Call.Nudged
            | ToPlayer.Rang sound -> console.SendAsync(Protocol.Call.Rang, box (Sound.word sound)))
        |> Task.WhenAll

type Finding =
    abstract At: HttpContext -> Table option

type TableHub(finding: Finding, pages: Pages) =
    inherit Hub()

    let deliver (clients: IHubCallerClients) posts = Wire.deliver clients pages posts

    member private this.Theirs: Table option =
        match this.Context.GetHttpContext() with
        | null -> None
        | ctx -> finding.At ctx

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

        Table.sits table this.Context.ConnectionId resuming AtATerminal view palette
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

    // Every address this machine answers to on its network, for the lines that say where a table
    // is. Nothing here is worth failing for: a machine with no name of its own, or no network at
    // all, still opens a table, and the localhost line said beneath these is always there.
    let private network () =
        try
            Dns.GetHostAddresses(Dns.GetHostName())
            |> Array.filter (fun address -> address.AddressFamily = AddressFamily.InterNetwork)
            |> Array.map string
            |> List.ofArray
        with _ ->
            []

    let private waiting (options: HubOptions) =
        options.KeepAliveInterval <- Protocol.KeepAlive
        options.ClientTimeoutInterval <- Protocol.GivenUp
        options.HandshakeTimeout <- Protocol.Handshake
        options.MaximumReceiveMessageSize <- Nullable 32768L

    // Any address, wrapped or in the clear: a table that answered on IPv4 alone in the clear and
    // on both with a certificate was two tables to find.
    let private listening (builder: WebApplicationBuilder) reach =
        builder.WebHost.ConfigureKestrel(fun options ->
            match reach.Wrapping with
            | Kept(certificate, password) ->
                options.ListenAnyIP(
                    reach.Port,
                    fun listen ->
                        match password with
                        | Some password -> listen.UseHttps(certificate, password) |> ignore
                        | None -> listen.UseHttps certificate |> ignore
                )
            | InTheClear
            | Ahead -> options.ListenAnyIP reach.Port)
        |> ignore

    let private bucket many (dripping: TimeSpan) =
        TokenBucketRateLimiterOptions(
            TokenLimit = many,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = dripping,
            QueueLimit = 0,
            AutoReplenishment = true
        )

    // How long a wrong answer costs: the bucket for one address gives a token back this often, and
    // it is what the door tells a stranger to wait.
    let private dripping = TimeSpan.FromSeconds 5.0

    let private guarded (app: WebApplication) (drawn: Browser.Drawn) (place: string) reach =
        match reach.Wrapping with
        | InTheClear
        | Kept _ -> ()
        | Ahead ->
            let forwarded =
                ForwardedHeadersOptions(
                    ForwardedHeaders =
                        (ForwardedHeaders.XForwardedFor
                         ||| ForwardedHeaders.XForwardedProto
                         ||| ForwardedHeaders.XForwardedHost)
                )

            // Whoever said --behind is telling us there is a proxy in front, and we have no way to
            // know its address, so the forwarded headers are taken at their word. That is only safe
            // because it was asked for: a table with nothing in front of it never gets here.
            forwarded.KnownIPNetworks.Clear()
            forwarded.KnownProxies.Clear()
            app.UseForwardedHeaders forwarded |> ignore

        match reach.Doorway with
        | Ajar -> ()
        | Locked _ ->

            // Two buckets, and a wrong answer has to get past both: one per address, so that guessing
            // at the word is slow, and one across everything, so that guessing from many addresses at
            // once is slow too. Only wrong answers are counted - the right word passes straight
            // through and play is never rate limited.
            let caller =
                PartitionedRateLimiter.Create<HttpContext, string>(fun ctx ->
                    let whoever =
                        match ctx.Connection.RemoteIpAddress with
                        | null -> "somewhere"
                        | address -> string address

                    RateLimitPartition.GetTokenBucketLimiter(whoever, (fun _ -> bucket 10 dripping)))

            let door = new TokenBucketRateLimiter(bucket 60 (TimeSpan.FromSeconds 1.0))

            app.Use(
                Func<HttpContext, RequestDelegate, Task>(fun ctx next ->
                    if Reach.admits reach (Browser.presented ctx) then
                        Browser.remember reach ctx
                        next.Invoke ctx
                    else

                    let waiting =
                        use mine = caller.AttemptAcquire ctx
                        use all = door.AttemptAcquire()
                        not (mine.IsAcquired && all.IsAcquired)

                    if waiting then Browser.tooOften dripping ctx else Browser.turned drawn place ctx)
            )
            |> ignore

    /// The routes every table serves a page by: the script, the stream, a line said and a fault
    /// reported. Which table a page is at is the caller's to say, and a page at none is sent to
    /// the front.
    let private serving
        (app: WebApplication)
        (drawn: Browser.Drawn)
        (pages: Pages)
        (sitting: HttpContext -> Browser.Sitting option)
        =
        let at doing (ctx: HttpContext) : Task =
            match sitting ctx with
            | Some sitting -> doing sitting ctx
            | None ->
                ctx.Response.Redirect "/"
                Task.CompletedTask

        app.MapGet(Page.Client, RequestDelegate(fun ctx -> Browser.script ctx))
        |> ignore

        app.MapGet(Page.Stream, RequestDelegate(at (fun sitting ctx -> Browser.stream drawn sitting pages ctx :> Task)))
        |> ignore

        app.MapPost(Page.Say, RequestDelegate(at (fun sitting ctx -> Browser.say sitting ctx :> Task)))
        |> ignore

        app.MapPost(Page.Amiss, RequestDelegate(fun ctx -> Browser.amiss ctx :> Task))
        |> ignore

    /// The clock a real-time game runs on: set again from what the last beat asked for, so a game
    /// that changes speed is followed. A beat that throws is said, and the table tries again in a
    /// second rather than stopping.
    let private keeping (first: TimeSpan) (beats: unit -> Post list * TimeSpan option) deliver =
        Clock.ticking
            first
            (fun () ->
                let posts, wait = beats ()
                deliver posts
                wait |> Option.defaultValue Threading.Timeout.InfiniteTimeSpan)
            (fun problem ->
                eprintfn "A beat went wrong at this table, which waits a second and tries again: %s" problem.Message
                TimeSpan.FromSeconds 1.0)

    let host game reach model sitters keep playing =
        let builder = WebApplication.CreateBuilder()

        let held = Held(Lobby.openedFor game model sitters, keep)
        let table = held :> Table
        let pages = Pages()

        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddSignalR(waiting) |> ignore

        builder.Services.AddSingleton<Finding>(
            { new Finding with
                member _.At _ = Some table }
        )
        |> ignore

        builder.Services.AddSingleton<Pages> pages |> ignore
        listening builder reach

        let app = builder.Build()
        let drawn = Browser.drawn game

        guarded app drawn "table" reach
        app.MapHub<TableHub>(Protocol.Path) |> ignore

        let hub = app.Services.GetRequiredService<IHubContext<TableHub>>()

        let sitting: Browser.Sitting =
            { Watching = fun console view palette -> Table.sits table console None InABrowser view palette
              Said = fun console line -> held.Change(Lobby.said console line)
              Gone = fun console -> held.Change(Lobby.left console)
              Deliver = fun posts -> Wire.deliver hub.Clients pages posts |> ignore }

        app.MapGet("/", RequestDelegate(fun ctx -> Browser.page drawn ctx)) |> ignore
        serving app drawn pages (fun _ -> Some sitting)

        let clock =
            game.Pulse
            |> Option.map (fun pulse ->
                keeping (pulse.Every(Model.state model)) table.Beats (fun posts ->
                    Wire.deliver hub.Clients pages posts |> ignore))

        Announce.hosted game reach sitters (Option.isSome playing) (network ())
        |> List.iter (printfn "%s")

        match playing with
        | None -> app.Run()
        | Some playing ->
            app.Start()
            playing ()
            app.WaitForShutdown()

        clock |> Option.iter (fun timer -> timer.Dispose())
        0

    let serve reach standing solo fresh keep =
        let builder = WebApplication.CreateBuilder()

        let aside = Aside(solo, fresh, keep)
        let pages = Pages()

        builder.Logging.ClearProviders() |> ignore
        listening builder reach

        let game = Solo.game solo

        let app = builder.Build()

        let drawn =
            { Browser.drawn game with
                Standard = standing }

        guarded app drawn "table" reach

        let sitting: Browser.Sitting =
            { Watching = fun console view palette -> aside.Watches(console, InABrowser, view, palette)
              Said = fun console line -> aside.Said(console, line)
              Gone = fun console -> aside.Change(Solo.gone console)
              Deliver = fun posts -> posts |> List.iter (Browser.send pages) }

        app.MapGet("/", RequestDelegate(fun ctx -> Browser.page drawn ctx)) |> ignore
        serving app drawn pages (fun _ -> Some sitting)

        let clock =
            game.Pulse
            |> Option.map (fun pulse ->
                keeping (pulse.Every(Model.state (Solo.model solo))) aside.Beats (List.iter (Browser.send pages)))

        Announce.served game reach (game.Rules.Seats(Model.state (Solo.model solo))) (network ())
        |> List.iter (printfn "%s")

        app.Run()
        clock |> Option.iter (fun timer -> timer.Dispose())
        0


    // The route value naming a table. Not `id`, which is also the name SignalR gives its own query
    // value on the same address - and this route has bitten once already (DESIGN.md).
    [<Literal>]
    let private TableRoute = "table"

    let house (hosting: Hosting) reach filling =
        let builder = WebApplication.CreateBuilder()

        let drawn: Browser.Drawn =
            { Shell = hosting.Shell
              Slots = hosting.Slots
              Standard = hosting.Standard }

        // Spelt out because `House` on its own is the command-line case of that name, which
        // `Launch` leaves unqualified.
        let home =
            Prototyping.Net.House(hosting, (fun () -> DateTime.Now), Reach.minted, Housekeeping.ordinary)

        let pages = Pages()

        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddSignalR(waiting) |> ignore
        builder.Services.AddSingleton<Pages> pages |> ignore

        builder.Services.AddSingleton<Finding>(
            { new Finding with
                member _.At ctx =
                    match ctx.Request.RouteValues.TryGetValue TableRoute with
                    | true, table -> home.At(string table) |> Option.map (fun opened -> opened.Table)
                    | _ -> None }
        )
        |> ignore

        listening builder reach

        let app = builder.Build()
        guarded app drawn "house" reach

        app.MapHub<TableHub>(Protocol.Path + "/{" + TableRoute + "}") |> ignore

        let hub = app.Services.GetRequiredService<IHubContext<TableHub>>()

        let sitting (opened: Opened) : Browser.Sitting =
            { Watching = fun console view palette -> Table.sits opened.Table console None InABrowser view palette
              Said = fun console line -> opened.Table.Said(console, line)
              Gone = fun console -> opened.Table.Left console
              Deliver = fun posts -> Wire.deliver hub.Clients pages posts |> ignore }

        let elsewhere (ctx: HttpContext) =
            ctx.Response.Redirect "/"
            Task.CompletedTask

        app.MapGet(
            "/",
            RequestDelegate(fun ctx ->
                let rows =
                    home.Listed
                    |> List.map (fun (opened, standing) ->
                        { Page.Where = Browser.tableAt opened.Id
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

                ctx.Response.ContentType <- "text/html; charset=utf-8"
                ctx.Response.WriteAsync(Page.house hosting.Shell hosting.Standard [ hosting.Fewest .. hosting.Most ] rows))
        )
        |> ignore

        // A table is dealt for a POST and nothing else: a link prefetched or previewed is a GET,
        // and every one of those used to open a table that then sat on the list for an hour. How
        // many are held to what the game takes before anything is dealt, and a refusal is said.
        app.MapPost(
            Page.Open,
            RequestDelegate(fun ctx ->
                task {
                    let! asked =
                        task {
                            if ctx.Request.HasFormContentType then
                                let! form = ctx.Request.ReadFormAsync()

                                return
                                    (match form.TryGetValue "players" with
                                     | true, given when given.Count > 0 -> string given[0]
                                     | _ -> "")
                            else
                                return ""
                        }

                    let dealt =
                        Commands.tryPlayerCount (hosting.Fewest, hosting.Most) asked
                        |> Result.bind (fun players -> home.Opens(Seating.hosting players, None, None))

                    match dealt with
                    | Ok opened ->
                        Browser.sitAt opened.Id ctx
                        ctx.Response.Redirect(Browser.tableAt opened.Id)
                    | Error why -> do! Browser.refused why ctx
                }
                :> Task)
        )
        |> ignore

        app.MapGet(
            Browser.tableAt ("{" + TableRoute + "}"),
            RequestDelegate(fun ctx ->
                match ctx.Request.RouteValues.TryGetValue TableRoute with
                | true, table when (home.At(string table)).IsSome ->
                    Browser.sitAt (string table) ctx
                    Browser.page drawn ctx
                | _ -> elsewhere ctx)
        )
        |> ignore

        serving app drawn pages (fun ctx -> Browser.tableOf ctx |> Option.bind home.At |> Option.map sitting)

        Announce.housed hosting reach (network ()) |> List.iter (printfn "%s")

        if filling then
            let taken =
                Transcript.saved ()
                |> List.filter (fun record -> record.Game = Some hosting.Name)
                |> List.filter (fun record ->
                    match home.Resumes record.Path with
                    | Ok _ -> true
                    | Error why ->
                        eprintfn "  Could not take up %s: %s" (IO.Path.GetFileName record.Path) why
                        false)

            printfn "  Took up %s from logs/." (Counting.orNone "no games" "game" "games" (List.length taken))
            printfn ""

        use _ =
            home.Sweeping(
                TimeSpan.FromMinutes 5.0,
                (fun gone -> printfn "  Swept %s nobody was at." (Counting.several "table" "tables" (List.length gone)))
            )

        // The house beats faster than any game does; each table is asked only when its own next beat
        // has come round, which `House.Beat` keeps track of.
        use _ =
            home.Beating(TimeSpan.FromMilliseconds 40.0, (fun posts -> Wire.deliver hub.Clients pages posts |> ignore))

        app.Run()
        0
