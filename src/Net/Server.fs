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
            | Nudged -> console.SendAsync Protocol.Call.Nudged
            | Rang _ -> console.SendAsync Protocol.Call.Rang)
        |> Task.WhenAll

type Finding =
    abstract At: HttpContext -> Table option

type TableHub(finding: Finding, pages: Browser.Pages) =
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

    let private reachableAt reach =
        try
            Dns.GetHostAddresses(Dns.GetHostName())
            |> Array.filter (fun address -> address.AddressFamily = AddressFamily.InterNetwork)
            |> Array.map (fun address -> Reach.at reach (string address), "on this network")
            |> List.ofArray
        with _ ->
            []

    let private addresses reach =
        (Reach.told reach
         |> Option.map (fun given -> given, "what you told them to use")
         |> Option.toList)
        @ reachableAt reach
        @ [ Reach.at reach "localhost", "for anyone on this machine" ]

    let private takeSeatAt game reach table where =
        [ ""
          $"    {Launch.written game (Launch.Join(where, None, Reach.word reach, table))}"
          ""
          $"  or open {Reach.opened reach where} in a browser."
          "" ]

    let private waiting (options: HubOptions) =
        options.KeepAliveInterval <- TimeSpan.FromSeconds 15.0
        options.ClientTimeoutInterval <- TimeSpan.FromSeconds 60.0
        options.HandshakeTimeout <- TimeSpan.FromSeconds 30.0
        options.MaximumReceiveMessageSize <- Nullable 32768L

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

    let private bucket many (dripping: TimeSpan) =
        TokenBucketRateLimiterOptions(
            TokenLimit = many,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = dripping,
            QueueLimit = 0,
            AutoReplenishment = true
        )

    let private guarded (app: WebApplication) (drawn: Browser.Drawn) reach =
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

                    RateLimitPartition.GetTokenBucketLimiter(whoever, (fun _ -> bucket 10 (TimeSpan.FromSeconds 5.0))))

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

                    if waiting then Browser.tooOften ctx else Browser.turned drawn ctx)
            )
            |> ignore

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

    /// The clock a real-time game runs on. A one-shot timer that sets itself again from what the last
    /// beat asked for, rather than a repeating one - so a game that changes speed is followed, and two
    /// beats can never overlap however long one of them takes. A beat that throws waits a second and
    /// carries on rather than stopping the table.
    let private keeping (first: TimeSpan) (beats: unit -> Post list * TimeSpan) deliver =
        let held = ref None

        let due (wait: TimeSpan) =
            match held.Value with
            | Some(timer: Threading.Timer) -> timer.Change(wait, Threading.Timeout.InfiniteTimeSpan) |> ignore
            | None -> ()

        let beat _ =
            let wait =
                try
                    let posts, wait = beats ()
                    deliver posts
                    wait
                with _ ->
                    TimeSpan.FromSeconds 1.0

            due wait

        let timer =
            new Threading.Timer(
                Threading.TimerCallback beat,
                null,
                Threading.Timeout.InfiniteTimeSpan,
                Threading.Timeout.InfiniteTimeSpan
            )

        held.Value <- Some timer
        due first
        timer

    let host game reach model sitters keep playing =
        let builder = WebApplication.CreateBuilder()

        let rivals =
            game.Seating (Model.seed model) (Seating.machines sitters) (Model.state model)

        let held = Held(Lobby.opened game model rivals, keep)
        let pages = Browser.Pages()

        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddSignalR(waiting) |> ignore

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

        let hub = app.Services.GetRequiredService<IHubContext<TableHub>>()

        let sitting: Browser.Sitting =
            { Watching =
                fun console view palette ->
                    (held :> Table).Sits(console, Guid.NewGuid().ToString "N", None, InABrowser, view, palette)
              Said = fun console line -> held.Change(Lobby.said console line)
              Gone = fun console -> held.Change(Lobby.left console)
              Deliver = fun posts -> Wire.deliver hub.Clients pages posts |> ignore }

        serving app drawn sitting pages

        let clock =
            game.Pulse
            |> Option.map (fun pulse ->
                keeping (pulse.Every(Model.state model)) (held :> Table).Beats (fun posts ->
                    Wire.deliver hub.Clients pages posts |> ignore))

        let seats = game.Rules.Seats(Model.state model)
        let mine, theirs = Seating.awaited sitters

        printfn ""
        printfn "=== A table for %d, waiting to be joined ===" seats
        printfn ""
        Seating.roster game.Skills sitters |> List.iter (printfn "%s")
        printfn ""

        match Reach.word reach with
        | Some code ->
            printfn "  The word at this table's door:  %s" code
            printfn ""
        | None ->
            printfn "  No word at the door: whoever can reach the address below may sit down."
            printfn ""

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

        let waited =
            match playing with
            | None ->
                app.Run()
                0
            | Some playing ->
                app.Start()
                playing ()
                app.WaitForShutdown()
                0

        clock |> Option.iter (fun timer -> timer.Dispose())
        waited

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

        let clock =
            game.Pulse
            |> Option.map (fun pulse ->
                keeping (pulse.Every(Model.state (Solo.model solo))) aside.Beats (List.iter (Browser.send pages)))

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
        clock |> Option.iter (fun timer -> timer.Dispose())
        0


    let private tableAt (id: string) = $"/at/{id}"

    let house (hosting: Hosting) reach filling =
        let builder = WebApplication.CreateBuilder()

        let drawn: Browser.Drawn =
            { Shell = hosting.Shell
              Slots = hosting.Slots
              Standard = hosting.Standard }

        let home =
            TCModel.Net.House(hosting, (fun () -> DateTime.Now), Reach.minted, Housekeeping.ordinary)

        let pages = Browser.Pages()

        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddSignalR(waiting) |> ignore
        builder.Services.AddSingleton<Browser.Pages> pages |> ignore

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

        app.MapHub<TableHub>(Protocol.Path + "/{id}") |> ignore

        let theirs (ctx: HttpContext) =
            Browser.tableOf ctx |> Option.bind home.At

        let hub = app.Services.GetRequiredService<IHubContext<TableHub>>()

        let sitting (opened: Opened) : Browser.Sitting =
            { Watching =
                fun console view palette ->
                    opened.Table.Sits(console, Guid.NewGuid().ToString "N", None, InABrowser, view, palette)
              Said = fun console line -> opened.Table.Said(console, line)
              Gone = fun console -> opened.Table.Left console
              Deliver = fun posts -> Wire.deliver hub.Clients pages posts |> ignore }

        let elsewhere (ctx: HttpContext) =
            ctx.Response.Redirect "/"
            Task.CompletedTask

        let atTheirTable (ctx: HttpContext) doing =
            match theirs ctx with
            | Some opened -> doing opened
            | None -> elsewhere ctx

        app.MapGet(Page.Client, RequestDelegate(fun ctx -> Browser.script ctx))
        |> ignore

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
                | Error _ -> elsewhere ctx)
        )
        |> ignore

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

        if filling then
            let found =
                Transcript.saved ()
                |> List.filter (fun record -> record.Game = Some hosting.Name)
                |> List.choose (fun record -> home.Resumes record.Path |> Result.toOption)

            printfn "  Took up %d game(s) from logs/." (List.length found)

        home.Sweeping(TimeSpan.FromMinutes 5.0, (fun gone -> printfn "  Swept %d table(s) nobody was at." (List.length gone)))
        |> ignore

        // The house beats faster than any game does; each table is asked only when its own next beat
        // has come round, which `House.Beat` keeps track of.
        home.Beating(TimeSpan.FromMilliseconds 40.0, (fun posts -> Wire.deliver hub.Clients pages posts |> ignore))
        |> ignore

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
