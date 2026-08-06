namespace TCModel.Net

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Threading.Channels
open Falco.Datastar
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
open Microsoft.Extensions.Primitives
open TCModel.Console

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

    /// Where a page fetches its client, opens its stream and says what was typed. `Html`
    /// writes these into the markup; this serves them. They are one set of names so the two
    /// cannot drift.
    [<Literal>]
    let private Cookie = "tcmodel-console"

    /// Console ids for pages are marked as such, so that delivering a screen can tell a
    /// page from a terminal without having to ask either of them.
    [<Literal>]
    let Prefix = "page-"

    let isPage (console: string) = console.StartsWith Prefix

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
    type Sitting =
        { Watching: string -> View -> Post list
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
        | TurnedAway why -> Some(Piece(Html.says why))
        | Nudged -> Some(Doing Html.Nudge)
        | Seated _ -> None

    let send (pages: Pages) (post: Post) =
        saying post.Say |> Option.iter (fun frame -> pages.Send(post.To, frame))

    // --- the console a browser is -----------------------------------------------------------

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

            ctx.Response.Cookies.Append(
                Cookie,
                minted,
                CookieOptions(HttpOnly = true, SameSite = SameSiteMode.Strict, MaxAge = TimeSpan.FromDays 7.0)
            )

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
    let private paletteOf standing (ctx: HttpContext) =
        match ctx.Request.Query.TryGetValue "colours" with
        | true, given when given.Count > 0 -> Palette.read (String.Join(" ", given.ToArray()))
        | _ -> standing

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
    let page standing (ctx: HttpContext) =
        let palette = paletteOf standing ctx
        consoleOf ctx |> ignore
        ctx.Response.ContentType <- "text/html; charset=utf-8"
        ctx.Response.WriteAsync(Html.page palette)

    /// A line typed in a browser. It may come in the address, which is how a button says
    /// what it does, or in the signals, which is how the box at the bottom says what was
    /// typed into it. Either way it is a line, and it goes to the same parser as any other.
    ///
    /// The signals are read as `Html.Signals` - the very record the page was started with -
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
                    match! Request.getSignals<Html.Signals> ctx with
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

            // The board comes back down the stream like any other, so all this has to
            // answer with is an empty box to type the next line into - said as the same
            // record the page was started with.
            do! Response.ofPatchSignals Html.nothingTyped ctx
        }

    /// A page saying it has gone wrong, printed where the game was started.
    ///
    /// This is the only thing in the program that prints anything the game did not say. It
    /// earns its place because of how the browser half fails: an expression the client
    /// cannot make sense of stops that one control and nothing else, on a page that draws
    /// perfectly, in a browser two rooms from whoever is hosting. Without this, the way
    /// that gets noticed is somebody mentioning that a button seems dead.
    ///
    /// Held to a line, because it is a browser talking and a browser can say anything.
    let amiss (ctx: HttpContext) =
        task {
            use reader = new StreamReader(ctx.Request.Body)
            let! said = reader.ReadToEndAsync()

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
    let stream standing (sitting: Sitting) (pages: Pages) (ctx: HttpContext) =
        task {
            let console = consoleOf ctx
            let view = View.html (paletteOf standing ctx)

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

                while reading do
                    let! more = channel.Reader.WaitToReadAsync ctx.RequestAborted

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
            with
            // The page has gone. That is how a browser says goodbye - there is no other
            // way for it to - so it is the ordinary ending rather than a failure.
            | :? OperationCanceledException ->
                ()

            pages.Close(console, channel)
            sitting.Gone console |> sitting.Deliver
        }
