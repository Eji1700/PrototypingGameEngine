namespace Prototyping.Net

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Threading
open System.Threading.Tasks
open Falco.Datastar
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.Extensions.Primitives
open Prototyping.Common
open Prototyping.Table

module Browser =

    [<Literal>]
    let private Cookie = "proto-console"

    [<Literal>]
    let Prefix = "page-"

    /// Where a house keeps a table's board, under the table's name.
    [<Literal>]
    let At = "/at"

    let tableAt (id: string) = $"{At}/{id}"

    [<NoComparison; NoEquality>]
    type Drawn =
        { Shell: Shell
          Slots: Slot list
          Standard: Palette }

    let drawn (game: Playable<_, _, _>) : Drawn =
        { Shell = game.Page
          Slots = game.Slots
          Standard = Playable.standard game }

    let isPage (console: string) = console.StartsWith Prefix

    /// What the wire does with a page: seats it, hears it, notices it go, and delivers what the
    /// table says back. `Watching` is handed the console's name, then the view and the colours the
    /// page asked for as the words it sent - the same three `Table.Sits` names.
    [<NoComparison; NoEquality>]
    type Sitting =
        { Watching: string -> string -> string -> Post list
          Said: string -> string -> Post list
          Gone: string -> Post list
          Deliver: Post list -> unit }


    let private saying =
        function
        | ToPlayer.Screen text
        | ToPlayer.Told text -> Some(Piece text)
        | ToPlayer.TurnedAway why -> Some(Piece(Page.says why))
        | ToPlayer.GotUp said -> Some(Piece(Page.says said))
        | ToPlayer.Nudged -> Some(Doing Page.Nudge)
        | ToPlayer.Rang sound -> Some(Doing(Page.rang sound))
        | ToPlayer.Seated _ -> None

    let send (pages: Pages) (post: Post) =
        saying post.Say |> Option.iter (fun frame -> pages.Send(post.To, frame))


    /// A value from the query by name - the several it may have been given joined by a space,
    /// which is how colours arrive, one slot to a word.
    let private queried name (ctx: HttpContext) =
        match ctx.Request.Query.TryGetValue name with
        | true, given when given.Count > 0 -> Some(String.Join(" ", given.ToArray()))
        | _ -> None

    // Lax rather than Strict: a table's address is handed round in chat and mail, and a link
    // followed from there is a cross-site navigation that a Strict cookie stays home for - so a
    // player who had the word and a seat would arrive with neither.
    let private kept (ctx: HttpContext) =
        CookieOptions(HttpOnly = true, Secure = ctx.Request.IsHttps, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromDays 7.0)

    let private consoleOf (ctx: HttpContext) =
        match ctx.Request.Cookies.TryGetValue Cookie with
        | true, known when isPage known -> known
        | _ ->
            let minted = Prefix + Guid.NewGuid().ToString "N"

            ctx.Response.Cookies.Append(Cookie, minted, kept ctx)

            minted

    [<Literal>]
    let private AtTable = "proto-table"

    let sitAt (id: string) (ctx: HttpContext) =
        ctx.Response.Cookies.Append(AtTable, id, kept ctx)

    let tableOf (ctx: HttpContext) =
        match ctx.Request.Cookies.TryGetValue AtTable with
        | true, id when id <> "" -> Some id
        | _ -> None

    let private paletteOf (drawn: Drawn) (ctx: HttpContext) =
        queried "colours" ctx
        |> Option.map (Palette.read drawn.Slots)
        |> Option.defaultValue drawn.Standard


    let presented (ctx: HttpContext) =
        [ queried Reach.Asked ctx
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

    /// Shown the door. A request that would have been shown a page - the front, or a table's board
    /// at a house, which is the address people share - gets the page with somewhere to type the
    /// word, since `403` is not an instruction anybody can act on. Anything else gets the word
    /// alone: a stream or a line is nothing a person reads.
    let turned (drawn: Drawn) (place: string) (ctx: HttpContext) =
        let path = ctx.Request.Path

        let page =
            ctx.Request.Method = HttpMethods.Get
            && (path = PathString "/" || path.StartsWithSegments(PathString At))

        if page then
            ctx.Response.StatusCode <- 401
            ctx.Response.ContentType <- "text/html; charset=utf-8"

            ctx.Response.WriteAsync(Page.locked drawn.Shell (paletteOf drawn ctx) (not (List.isEmpty (presented ctx))))
        else
            ctx.Response.StatusCode <- 403
            ctx.Response.ContentType <- "text/plain; charset=utf-8"
            ctx.Response.WriteAsync $"This {place} has a word at the door, and that is not it."

    /// Too many wrong words from one address; `dripping` is how long the door takes to hear
    /// another, and what it tells the stranger to wait.
    let tooOften (dripping: TimeSpan) (ctx: HttpContext) =
        let seconds = int (ceil dripping.TotalSeconds)
        ctx.Response.StatusCode <- 429
        ctx.Response.Headers.RetryAfter <- StringValues(string seconds)
        ctx.Response.ContentType <- "text/plain; charset=utf-8"

        ctx.Response.WriteAsync
            $"""Too many wrong answers at this door. Try again in {Counting.several "second" "seconds" seconds}."""

    /// A form that asked for something the house will not deal is told why, in plain words, rather
    /// than sent back to the front page with no word about it.
    let refused (why: string) (ctx: HttpContext) =
        ctx.Response.StatusCode <- 400
        ctx.Response.ContentType <- "text/plain; charset=utf-8"
        ctx.Response.WriteAsync why


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

    // What was typed: on the query for a button, otherwise in the signals the page posts. A body
    // that is not those signals - a post from something other than the page, or one that stopped
    // half way - reads as nothing typed rather than as a fault.
    let private lineOf (ctx: HttpContext) =
        task {
            match queried "line" ctx with
            | Some line -> return line
            | None ->
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
            let asked = queried "view" ctx |> Option.defaultValue ""
            let colours = queried "colours" ctx |> Option.defaultValue ""

            // A stream is only useful if what is written to it goes out at once, which means turning
            // off buffering here and asking any nginx in front not to add its own.
            ctx.Features.Get<IHttpResponseBodyFeature>()
            |> Option.ofObj
            |> Option.iter (fun body -> body.DisableBuffering())

            let outgoing = pages.Open console

            try
                try
                    do!
                        Response.sseStartResponseWithHeaders
                            ctx
                            [ KeyValuePair<string, StringValues>("X-Accel-Buffering", StringValues "no") ]

                    sitting.Watching console asked colours |> sitting.Deliver

                    let mutable reading = true

                    let mutable waiting = outgoing.Coming(ctx.RequestAborted).AsTask()

                    // Whichever comes first: something to send, or the heartbeat falling due. A quiet
                    // stream still has to say something now and again, or a proxy between here and
                    // the browser closes it as idle. The timer is cancelled either way so that a busy
                    // stream does not leave one behind on every pass.
                    while reading do
                        use beat = CancellationTokenSource.CreateLinkedTokenSource ctx.RequestAborted
                        let! settled = Task.WhenAny(waiting, Task.Delay(Protocol.KeepAlive, beat.Token))
                        beat.Cancel()

                        if not (Object.ReferenceEquals(settled, waiting)) then
                            do! Response.sseExecuteScript ctx Page.Alive
                            do! ctx.Response.Body.FlushAsync ctx.RequestAborted
                        else

                        let! more = waiting

                        if not more then
                            reading <- false
                        else
                            for frame in outgoing.Taken() do
                                match frame with
                                | Piece html -> do! Response.sseStringElements ctx html
                                | Doing script -> do! Response.sseExecuteScript ctx script

                            do! ctx.Response.Body.FlushAsync ctx.RequestAborted
                            waiting <- outgoing.Coming(ctx.RequestAborted).AsTask()
                with :? OperationCanceledException ->
                    ()
            finally
                // However the stream ended - the page going, or a socket reset in the middle of a
                // write - the table is told the console has gone, but only if this was still its
                // stream: a reload's old stream ending after the new one sat down would otherwise
                // get the page up from the seat it had just come back to.
                if pages.Close(console, outgoing) then sitting.Gone console |> sitting.Deliver
        }
