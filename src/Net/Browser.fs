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

/// The browser's end of a table.
///
/// A page is a console like any other. It says the line a player typed and is sent back the
/// screen they would have been looking at, which is the whole of the protocol and the same
/// one a terminal speaks - `Lobby` cannot tell the two apart and is never asked to. What is
/// different is only how the words travel: a terminal has a socket held open by SignalR,
/// and a page has one held open by itself, reading a stream of patches.
///
/// So there is no second table and no second set of rules. Somebody at a keyboard and
/// somebody in a browser sit down at the same game, take their turns in the same order, and
/// are each drawn a board of their own from the one position.
module Browser =

    /// Where a page fetches its client, opens its stream and says what was typed. `Page`
    /// writes these into the markup; this serves them. They are one set of names so the two
    /// cannot drift.
    [<Literal>]
    let private Cookie = "tcmodel-console"

    /// Console ids for pages are marked as such, so that delivering a screen can tell a
    /// page from a terminal without having to ask either of them.
    [<Literal>]
    let Prefix = "page-"

    let isPage (console: string) = console.StartsWith Prefix

    /// How often a stream with nothing to say says something anyway.
    ///
    /// Well inside the minute or so that the usual things standing between a browser and a
    /// table out on the internet - a tunnel, a proxy, a load balancer - wait before deciding
    /// a quiet connection is a dead one. The page allows for a few of these going missing
    /// before it does anything about it, so this is short enough to be sure and long enough
    /// to be nothing.
    let private Beat = TimeSpan.FromSeconds 15.0

    // --- who is reading ------------------------------------------------------------------

    /// What goes down a page's stream.
    ///
    /// Two kinds, because not everything a table says to a browser is part of the page. A
    /// board is: it is one element with an id, and it lands where the element of that id
    /// already is. A nudge is not - there is nothing on the page it could replace, because
    /// being told the turn has come round while you were elsewhere is something the browser
    /// does rather than something it draws.
    ///
    /// So the second kind is a line of the page's own script, which the client runs and
    /// throws away. What it says is `Html`'s, beside the script that answers it; all that is
    /// settled here is that a stream carries the two and no third thing.
    type Frame =
        | Piece of html: string
        | Doing of script: string

    /// The pages with a stream open, and the seats they have been given.
    ///
    /// This and `Held` are the only things in the program that are not values. A stream is
    /// a socket somebody is holding open, which is not the sort of thing that can be folded
    /// over, so it is kept here at the edge and everything inland stays pure.
    ///
    /// The seat a page was given is remembered against the browser rather than against the
    /// stream. A page that is reloaded, or a laptop that is closed and opened again, comes
    /// back with the same cookie and is handed the same stones - which is the same promise
    /// `--token` makes a console, kept without anybody having to write a token down.
    type Pages() =
        let gate = obj ()
        let streams = Dictionary<string, Channel<Frame>>()
        let seats = Dictionary<string, string>()

        /// Open a stream for a page, closing whatever it had open before. A second tab in
        /// the same browser is the same console, so it takes the stream over rather than
        /// sitting down again beside itself.
        member _.Open console =
            let channel = Channel.CreateUnbounded<Frame>()

            lock gate (fun () ->
                match streams.TryGetValue console with
                | true, before -> before.Writer.TryComplete() |> ignore
                | _ -> ()

                streams[console] <- channel)

            channel

        /// Let a stream go, unless the console has already opened another - in which case
        /// the newer one is the one that counts and this is the tail of an old request.
        member _.Close(console, channel: Channel<Frame>) =
            lock gate (fun () ->
                match streams.TryGetValue console with
                | true, current when Object.ReferenceEquals(current, channel) -> streams.Remove console |> ignore
                | _ -> ())

            channel.Writer.TryComplete() |> ignore

        /// Say something down a page's stream. A page that has gone is not an error: the
        /// table will hear about it when the request finishes, and until then anything
        /// addressed to it simply has nowhere to go.
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

    /// The whole of what the browser side needs of a table: somebody arrives, somebody says
    /// something, somebody goes, and whatever came of it gets shown.
    ///
    /// Four functions rather than the table itself, because there are two kinds of table
    /// and a page is worth having at both. `Lobby` seats people at their own machines and
    /// hands out tokens; `Solo` is one hot seat with everybody round it. Neither is named
    /// here, and nothing below can tell which it is serving.
    ///
    /// Delivering comes in from outside for a second reason: at a networked table it
    /// reaches consoles this file knows nothing about - the players at terminals - and what
    /// those are is settled a file later.
    [<NoComparison; NoEquality>]
    type Sitting<'Move, 'State, 'Notice> =
        { Watching: string -> View<'Move, 'State, 'Notice> -> Post list
          Said: string -> string -> Post list
          Gone: string -> Post list
          Deliver: Post list -> unit }

    // --- what goes down a stream -----------------------------------------------------------

    /// What a page is shown for each thing the table says to it.
    ///
    /// The fragment and nothing else. Where it goes is not said here and is not `Browser`'s
    /// to say: every fragment `Html` draws is one element with an id on it, and the client
    /// puts an element where the element of that id already is - so what decides whether a
    /// board lands on the board or beside it is the fragment itself, which is the one place
    /// that knows.
    ///
    /// `Seated` is the exception. It says which seat was taken, which the page has no use
    /// for - a browser knows itself by its cookie - and which is caught on the way past and
    /// remembered instead.
    let private saying =
        function
        | Screen text
        | Told text -> Some(Piece text)
        | TurnedAway why -> Some(Piece(Page.says why))
        // The page is very often gone by the time this could reach it - closing the tab is
        // how a browser puts a game down - and where it is not, saying so is the whole of
        // what there is to do. A page has no loop to come out of.
        | GotUp said -> Some(Piece(Page.says said))
        | Nudged -> Some(Doing Page.Nudge)
        | Seated _ -> None

    let send (pages: Pages) (post: Post) =
        saying post.Say |> Option.iter (fun frame -> pages.Send(post.To, frame))

    // --- the console a browser is -----------------------------------------------------------

    /// How anything this table hands a browser to keep is kept.
    ///
    /// `Secure` is settled per request rather than once, because whether a browser is
    /// talking to this over https is not something the table knows about itself: with a
    /// tunnel or a proxy in front, the encryption ends there and the request arrives here in
    /// the clear, carrying the header that says what it was. Marked secure on a plain
    /// connection the cookie would be dropped and never sent back, which at a table means a
    /// player who loses their seat on every reload.
    let private kept (ctx: HttpContext) =
        CookieOptions(
            HttpOnly = true,
            Secure = ctx.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays 7.0
        )

    /// Which console this browser is, minting one if it has not been here before.
    ///
    /// The cookie is the whole of a page's identity. It is not a claim on a seat - the table
    /// still hands those out, and still refuses when there are none left - only a way of
    /// being the same visitor twice, which is what lets a reload find its own stones again.
    let private consoleOf (ctx: HttpContext) =
        match ctx.Request.Cookies.TryGetValue Cookie with
        | true, known when isPage known -> known
        | _ ->
            let minted = Prefix + Guid.NewGuid().ToString "N"

            ctx.Response.Cookies.Append(Cookie, minted, kept ctx)

            minted

    /// The colours this page asked to be drawn in, in the same words a console sends down
    /// the wire and read by the same function. A colour the table has never heard of is
    /// passed over rather than being a reason to turn anybody away.
    ///
    /// Said more than once, because that is what a form does: each of the five choosers on
    /// the page is one `colours`, and what they add up to is the palette. Joined back into
    /// the one line `Palette.read` takes, which is also the line that goes down a wire.
    ///
    /// A page that asks for nothing gets `standing` - whatever the command line settled on
    /// when the game was opened, so that `--colour blue=teal` means something to the first
    /// browser through the door as well as to a terminal.
    ///
    /// What there is to colour is the game's, which is why the game comes in: a palette read
    /// against the wrong list of slots would quietly drop every colour it named.
    let private paletteOf game standing (ctx: HttpContext) =
        match ctx.Request.Query.TryGetValue "colours" with
        | true, given when given.Count > 0 -> Palette.read game.Slots (String.Join(" ", given.ToArray()))
        | _ -> standing

    // --- the word at the door ---------------------------------------------------------------
    //
    // A table further away than a network is reachable by everybody, which includes everybody
    // who was not invited - and a seat, once taken, is kept for whoever took it. There is no
    // move for standing a stranger up again, and there should not be: the same rule is what
    // lets a player close their laptop and come back to their own stones. So the stranger has
    // to be stopped at the door, and here is where a request meets it.
    //
    // Nothing here decides anything. `Reach.admits` does that, on a value, where it can be
    // checked; this only finds what was presented and says what a refusal looks like.

    /// Everything this request presents as the word, wherever it was carried.
    ///
    /// Three places, because there are three kinds of arrival and each can only manage some
    /// of them. A browser sent a whole address carries it in the address the first time and
    /// in a cookie ever after; a console at a terminal has no address bar and no cookie jar,
    /// so it sets a header on everything it sends.
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

    /// Hand a browser that got it right the word back, so that the rest of what a page
    /// fetches - its client, its stream, every line it types - carries it without the
    /// address having to. It is not a key to anything: it is the same word they just
    /// presented, kept so they need not present it again by hand.
    let remember reach (ctx: HttpContext) =
        match Reach.word reach with
        | None -> ()
        | Some code ->
            match ctx.Request.Cookies.TryGetValue Reach.Cookie with
            | true, held when held = code -> ()
            | _ -> ctx.Response.Cookies.Append(Reach.Cookie, code, kept ctx)

    /// And what somebody who got it wrong is shown.
    ///
    /// Whoever is on the other side of this is a player who was sent an address, so the
    /// front door answers with something they can act on: a page with one box on it. Every
    /// other address answers with the bare refusal, because nothing else here is opened by a
    /// person - they are the page's own fetches, and a stream handed a form instead of a
    /// board would be a stranger sort of failure than the one it is reporting.
    let turned game standing (ctx: HttpContext) =
        if ctx.Request.Method = HttpMethods.Get && ctx.Request.Path = PathString "/" then
            ctx.Response.StatusCode <- 401
            ctx.Response.ContentType <- "text/html; charset=utf-8"

            ctx.Response.WriteAsync(Page.locked game.Page (paletteOf game standing ctx) (not (List.isEmpty (presented ctx))))
        else
            ctx.Response.StatusCode <- 403
            ctx.Response.ContentType <- "text/plain; charset=utf-8"
            ctx.Response.WriteAsync "This table has a word at the door, and that is not it."

    /// And what somebody who has got it wrong rather a lot is shown.
    ///
    /// Plain words at every address including the front door, because getting here takes ten
    /// wrong answers inside a minute and nobody's fingers do that. What the far end is
    /// actually being told is in the header: come back later, and here is how much later.
    let tooOften (ctx: HttpContext) =
        ctx.Response.StatusCode <- 429
        ctx.Response.Headers.RetryAfter <- StringValues "60"
        ctx.Response.ContentType <- "text/plain; charset=utf-8"
        ctx.Response.WriteAsync "Too many wrong answers at this door. Try again in a minute."

    // --- what is served ------------------------------------------------------------------------

    /// Datastar's own client, carried inside this program rather than fetched from
    /// anywhere. A table opened on a machine with no way out to the internet is exactly the
    /// table this game is for.
    let private client =
        lazy
            (use stream = Assembly.GetExecutingAssembly().GetManifestResourceStream "datastar.js"
             use reader = new StreamReader(stream)
             reader.ReadToEnd())

    let script (ctx: HttpContext) =
        ctx.Response.ContentType <- "text/javascript; charset=utf-8"
        ctx.Response.Headers.CacheControl <- "public, max-age=86400"
        ctx.Response.WriteAsync client.Value

    /// The page itself. It carries no game: it opens a stream and the table answers with a
    /// board, which is the same way every board after it arrives.
    let page game standing (ctx: HttpContext) =
        let palette = paletteOf game standing ctx
        consoleOf ctx |> ignore
        ctx.Response.ContentType <- "text/html; charset=utf-8"
        ctx.Response.WriteAsync(Page.page game.Page palette)

    /// A line typed in a browser. It may come in the address, which is how a button says
    /// what it does, or in the signals, which is how the box at the bottom says what was
    /// typed into it. Either way it is a line, and it goes to the same parser as any other.
    ///
    /// The signals are read as `Page.Signals` - the very record the page was started with -
    /// so the name of the one thing a page holds is settled in one place and read back by
    /// the same declaration that wrote it.
    let private lineOf (ctx: HttpContext) =
        task {
            match ctx.Request.Query.TryGetValue "line" with
            | true, given when given.Count > 0 -> return string given[0]
            | _ ->
                // Nothing typed is a line too - it redraws the board - so a request that
                // carries no signals at all is answered with nothing rather than a
                // complaint.
                try
                    match! Request.getSignals<Page.Signals> ctx with
                    | ValueSome signals -> return (signals.Line |> Option.ofObj |> Option.defaultValue "")
                    | ValueNone -> return ""
                with _ ->
                    return ""
        }

    let say (sitting: Sitting<_, _, _>) (ctx: HttpContext) =
        task {
            let console = consoleOf ctx
            let! line = lineOf ctx

            sitting.Said console line |> sitting.Deliver

            // The board comes back down the stream like any other, so all this has to
            // answer with is an empty box to type the next line into - said as the same
            // record the page was started with.
            do! Response.ofPatchSignals Page.nothingTyped ctx
        }

    /// A page saying it has gone wrong, printed where the game was started.
    ///
    /// This is the only thing in the program that prints anything the game did not say. It
    /// earns its place because of how the browser half fails: an expression the client
    /// cannot make sense of stops that one control and nothing else, on a page that draws
    /// perfectly, in a browser two rooms from whoever is hosting. Without this, the way
    /// that gets noticed is somebody mentioning that a button seems dead.
    ///
    /// How many of these a table will print before it stops listening to them.
    ///
    /// Nothing on the page sends more than a handful - the script that reports these gives up
    /// after eight, having swallowed its own trouble - but what is at the far end of this is
    /// not necessarily the page. Anybody sitting at the table can post here as fast as they
    /// like, and what that buys them is the host's board scrolling away in the middle of a
    /// game. The door keeps strangers out; this is what keeps a guest from wearing out their
    /// welcome, and fifty is far more than a real fault has ever needed.
    [<Literal>]
    let private Enough = 50

    let private complained = ref 0

    /// Held to a line, because it is a browser talking and a browser can say anything.
    let amiss (ctx: HttpContext) =
        task {
            if Interlocked.Increment &complained.contents > Enough then
                ctx.Response.StatusCode <- 204
            else

            use reader = new StreamReader(ctx.Request.Body)

            // As much as is going to be printed and not a character more. What is at the far
            // end of this is a browser, and out past a network it is not necessarily one of
            // ours: reading to the end of whatever a stranger cares to send is not a thing to
            // do on the strength of a line that is about to be trimmed to three hundred
            // characters anyway.
            let held = Array.zeroCreate<char> 400
            let! read = reader.ReadBlockAsync(held, 0, held.Length)
            let said = String(held, 0, read)

            let line =
                said.Replace('\n', ' ').Replace('\r', ' ')
                |> fun said -> if said.Length > 300 then said.Substring(0, 300) + "..." else said

            eprintfn "A page reports: %s" line
            ctx.Response.StatusCode <- 204
        }

    /// The stream a page holds open, and everything that happens while it does.
    ///
    /// Sitting down happens here rather than when the page was served, because a seat is
    /// only worth having while somebody is holding it: a page fetched and closed again has
    /// nobody at it, and the table would be waiting on a chair nobody is in.
    let stream game standing (sitting: Sitting<_, _, _>) (pages: Pages) (ctx: HttpContext) =
        task {
            let console = consoleOf ctx

            // The first way of drawing this game that a browser can show. Which one that is
            // is the game's business; that a page gets one it can read is this file's.
            let view = Playable.plainest InABrowser (paletteOf game standing ctx) game

            ctx.Features.Get<IHttpResponseBodyFeature>()
            |> Option.ofObj
            |> Option.iter (fun body -> body.DisableBuffering())

            let channel = pages.Open console

            // The headers a stream needs are the client library's business rather than this
            // file's. The one added here is not: anything between the table and the browser
            // that would rather collect a whole response before passing it on has to be told
            // not to, and a stream only delivered once it ends is not a stream.
            do!
                Response.sseStartResponseWithHeaders
                    ctx
                    [ KeyValuePair<string, StringValues>("X-Accel-Buffering", StringValues "no") ]

            let seated = sitting.Watching console view

            // Which seat this browser was given, kept so that the next visit on the same
            // cookie comes back to it rather than being handed a stranger's stones. A table
            // with no seats at it says nothing here, and nothing is what gets remembered.
            for post in seated do
                match post.To, post.Say with
                | at, Seated(_, token) when at = console -> pages.Remember(console, token)
                | _ -> ()

            sitting.Deliver seated

            try
                let mutable reading = true

                // One waiter at a time, kept across the loop rather than asked for again
                // each time round it: the loop now comes round for two reasons, and a fresh
                // waiter on every beat would leave a queue of them on the channel.
                let mutable waiting = channel.Reader.WaitToReadAsync(ctx.RequestAborted).AsTask()

                while reading do
                    // Nothing said for a while is the state a turn-based game between people
                    // who are thinking is *usually* in, and it is indistinguishable - from
                    // both ends - from a connection that has quietly died. Out past a local
                    // network something in between will close an idle stream inside a
                    // minute. So the table says something harmless every so often, which
                    // keeps the wire warm and gives the page a silence worth measuring.
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
                            // Which is where a screen becomes a patch. What that looks like
                            // on the wire - the event's name, how a screen with newlines in
                            // it is carried without arriving in pieces - is the client
                            // library's to know and no longer written out here. The same
                            // goes for a knock: what reaches the browser is a script the
                            // client runs and takes off the page again.
                            match frame with
                            | Piece html -> do! Response.sseStringElements ctx html
                            | Doing script -> do! Response.sseExecuteScript ctx script

                        do! ctx.Response.Body.FlushAsync ctx.RequestAborted
                        waiting <- channel.Reader.WaitToReadAsync(ctx.RequestAborted).AsTask()
            with
            // The page has gone. That is how a browser says goodbye - there is no other
            // way for it to - so it is the ordinary ending rather than a failure.
            | :? OperationCanceledException ->
                ()

            pages.Close(console, channel)
            sitting.Gone console |> sitting.Deliver
        }
