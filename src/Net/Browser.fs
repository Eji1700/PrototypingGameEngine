namespace TCModel.Net

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Falco.Datastar
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.Extensions.Primitives
open TCModel.Table

module Browser =

    [<Literal>]
    let private Cookie = "tcmodel-console"

    [<Literal>]
    let Prefix = "page-"

    [<NoComparison; NoEquality>]
    type Drawn =
        { Shell: Shell
          Slots: Slot list
          Standard: Palette }

    let isPage (console: string) = console.StartsWith Prefix

    let private Beat = TimeSpan.FromSeconds 15.0


    type Frame =
        | Piece of html: string
        | Doing of script: string

    /// The pages currently reading, and the token each of them was given for its seat.
    ///
    /// A page that reloads opens a second stream before the first has noticed it is gone, so opening
    /// one for a console that already has one closes the old one rather than leaving both writing to
    /// a browser that is only listening to the newer.
    type Pages() =
        let gate = obj ()
        let streams = Dictionary<string, Channel<Frame>>()
        let seats = Dictionary<string, string>()

        member _.Open console =
            let channel = Channel.CreateUnbounded<Frame>()

            lock gate (fun () ->
                match streams.TryGetValue console with
                | true, before -> before.Writer.TryComplete() |> ignore
                | _ -> ()

                streams[console] <- channel)

            channel

        member _.Close(console, channel: Channel<Frame>) =
            lock gate (fun () ->
                match streams.TryGetValue console with
                | true, current when Object.ReferenceEquals(current, channel) -> streams.Remove console |> ignore
                | _ -> ())

            channel.Writer.TryComplete() |> ignore

        member _.Send(console, frame) =
            lock gate (fun () ->
                match streams.TryGetValue console with
                | true, channel -> channel.Writer.TryWrite frame |> ignore
                | _ -> ())

        member _.Remember(console, token) =
            lock gate (fun () -> seats[console] <- token)

        member _.Seat console =
            lock gate (fun () ->
                match seats.TryGetValue console with
                | true, token -> Some token
                | _ -> None)

    [<NoComparison; NoEquality>]
    type Sitting =
        { Watching: string -> string -> string -> Post list
          Said: string -> string -> Post list
          Gone: string -> Post list
          Deliver: Post list -> unit }


    let private saying =
        function
        | Screen text
        | Told text -> Some(Piece text)
        | TurnedAway why -> Some(Piece(Page.says why))
        | GotUp said -> Some(Piece(Page.says said))
        | Nudged -> Some(Doing Page.Nudge)
        | Seated _ -> None

    let send (pages: Pages) (post: Post) =
        saying post.Say |> Option.iter (fun frame -> pages.Send(post.To, frame))


    let private kept (ctx: HttpContext) =
        CookieOptions(
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays 7.0
        )

    let private consoleOf (ctx: HttpContext) =
        match ctx.Request.Cookies.TryGetValue Cookie with
        | true, known when isPage known -> known
        | _ ->
            let minted = Prefix + Guid.NewGuid().ToString "N"

            ctx.Response.Cookies.Append(Cookie, minted, kept ctx)

            minted

    [<Literal>]
    let private AtTable = "tcmodel-table"

    let sitAt (id: string) (ctx: HttpContext) =
        ctx.Response.Cookies.Append(AtTable, id, kept ctx)

    let tableOf (ctx: HttpContext) =
        match ctx.Request.Cookies.TryGetValue AtTable with
        | true, id when id <> "" -> Some id
        | _ -> None

    let private paletteOf (drawn: Drawn) (ctx: HttpContext) =
        match ctx.Request.Query.TryGetValue "colours" with
        | true, given when given.Count > 0 -> Palette.read drawn.Slots (String.Join(" ", given.ToArray()))
        | _ -> drawn.Standard


    let presented (ctx: HttpContext) =
        [ (match ctx.Request.Query.TryGetValue Reach.Asked with
           | true, given when given.Count > 0 -> Some(string given[0])
           | _ -> None)
          (match ctx.Request.Headers.TryGetValue Reach.Header with
           | true, given when given.Count > 0 -> Some(string given[0])
           | _ -> None)
          (match ctx.Request.Cookies.TryGetValue Reach.Cookie with
           | true, given -> Some given
           | _ -> None) ]
        |> List.choose id

    let remember reach (ctx: HttpContext) =
        match Reach.word reach with
        | None -> ()
        | Some code ->
            match ctx.Request.Cookies.TryGetValue Reach.Cookie with
            | true, held when held = code -> ()
            | _ -> ctx.Response.Cookies.Append(Reach.Cookie, code, kept ctx)

    let turned (drawn: Drawn) (ctx: HttpContext) =
        if ctx.Request.Method = HttpMethods.Get && ctx.Request.Path = PathString "/" then
            ctx.Response.StatusCode <- 401
            ctx.Response.ContentType <- "text/html; charset=utf-8"

            ctx.Response.WriteAsync(Page.locked drawn.Shell (paletteOf drawn ctx) (not (List.isEmpty (presented ctx))))
        else
            ctx.Response.StatusCode <- 403
            ctx.Response.ContentType <- "text/plain; charset=utf-8"
            ctx.Response.WriteAsync "This table has a word at the door, and that is not it."

    let tooOften (ctx: HttpContext) =
        ctx.Response.StatusCode <- 429
        ctx.Response.Headers.RetryAfter <- StringValues "60"
        ctx.Response.ContentType <- "text/plain; charset=utf-8"
        ctx.Response.WriteAsync "Too many wrong answers at this door. Try again in a minute."


    let private client =
        lazy
            (use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream "datastar.js"
             use reader = new StreamReader(stream)
             reader.ReadToEnd())

    let script (ctx: HttpContext) =
        ctx.Response.ContentType <- "text/javascript; charset=utf-8"
        ctx.Response.Headers.CacheControl <- "public, max-age=86400"
        ctx.Response.WriteAsync client.Value

    let page (drawn: Drawn) (ctx: HttpContext) =
        let palette = paletteOf drawn ctx
        consoleOf ctx |> ignore
        ctx.Response.ContentType <- "text/html; charset=utf-8"
        ctx.Response.WriteAsync(Page.page drawn.Shell palette)

    let private lineOf (ctx: HttpContext) =
        task {
            match ctx.Request.Query.TryGetValue "line" with
            | true, given when given.Count > 0 -> return string given[0]
            | _ ->
                try
                    match! Request.getSignals<Page.Signals> ctx with
                    | ValueSome signals -> return (signals.Line |> Option.ofObj |> Option.defaultValue "")
                    | ValueNone -> return ""
                with _ ->
                    return ""
        }

    let say (sitting: Sitting) (ctx: HttpContext) =
        task {
            let console = consoleOf ctx
            let! line = lineOf ctx

            sitting.Said console line |> sitting.Deliver

            do! Response.ofPatchSignals Page.nothingTyped ctx
        }

    [<Literal>]
    let private Enough = 50

    // A page that is broken enough to report an error is usually broken enough to report it over and
    // over. The count is never reset, so a run prints a handful and then says nothing.
    let private complained = ref 0

    let amiss (ctx: HttpContext) =
        task {
            if Interlocked.Increment &complained.contents > Enough then
                ctx.Response.StatusCode <- 204
            else

            use reader = new StreamReader(ctx.Request.Body)

            let held = Array.zeroCreate<char> 400
            let! read = reader.ReadBlockAsync(held, 0, held.Length)
            let said = String(held, 0, read)

            let line =
                said.Replace('\n', ' ').Replace('\r', ' ')
                |> fun said -> if said.Length > 300 then said.Substring(0, 300) + "..." else said

            eprintfn "A page reports: %s" line
            ctx.Response.StatusCode <- 204
        }

    let stream (drawn: Drawn) (sitting: Sitting) (pages: Pages) (ctx: HttpContext) =
        task {
            let console = consoleOf ctx

            let asked =
                match ctx.Request.Query.TryGetValue "view" with
                | true, given when given.Count > 0 -> string given[0]
                | _ -> ""

            let colours =
                match ctx.Request.Query.TryGetValue "colours" with
                | true, given when given.Count > 0 -> String.Join(" ", given.ToArray())
                | _ -> ""

            // A stream is only useful if what is written to it goes out at once, which means turning
            // off buffering here and asking any nginx in front not to add its own.
            ctx.Features.Get<IHttpResponseBodyFeature>()
            |> Option.ofObj
            |> Option.iter (fun body -> body.DisableBuffering())

            let channel = pages.Open console

            do!
                Response.sseStartResponseWithHeaders
                    ctx
                    [ KeyValuePair<string, StringValues>("X-Accel-Buffering", StringValues "no") ]

            let seated = sitting.Watching console asked colours

            for post in seated do
                match post.To, post.Say with
                | at, Seated(_, token) when at = console -> pages.Remember(console, token)
                | _ -> ()

            sitting.Deliver seated

            try
                let mutable reading = true

                let mutable waiting = channel.Reader.WaitToReadAsync(ctx.RequestAborted).AsTask()

                // Whichever comes first: something to send, or the heartbeat falling due. A quiet
                // stream still has to say something now and again, or a proxy between here and the
                // browser closes it as idle. The timer is cancelled either way so that a busy stream
                // does not leave one behind on every pass.
                while reading do
                    use beat = CancellationTokenSource.CreateLinkedTokenSource ctx.RequestAborted
                    let! settled = Task.WhenAny(waiting, Task.Delay(Beat, beat.Token))
                    beat.Cancel()

                    if not (Object.ReferenceEquals(settled, waiting)) then
                        do! Response.sseExecuteScript ctx Page.Alive
                        do! ctx.Response.Body.FlushAsync ctx.RequestAborted
                    else

                    let! more = waiting

                    if not more then
                        reading <- false
                    else
                        let mutable frame = Piece ""

                        while channel.Reader.TryRead &frame do
                            match frame with
                            | Piece html -> do! Response.sseStringElements ctx html
                            | Doing script -> do! Response.sseExecuteScript ctx script

                        do! ctx.Response.Body.FlushAsync ctx.RequestAborted
                        waiting <- channel.Reader.WaitToReadAsync(ctx.RequestAborted).AsTask()
            with :? OperationCanceledException ->
                ()

            pages.Close(console, channel)
            sitting.Gone console |> sitting.Deliver
        }
