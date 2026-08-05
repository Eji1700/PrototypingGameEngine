namespace TCModel.Net

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text
open System.Text.Json
open System.Threading.Channels
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Features
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
        let streams = Dictionary<string, Channel<string>>()
        let seats = Dictionary<string, string>()

        /// Open a stream for a page, closing whatever it had open before. A second tab in
        /// the same browser is the same console, so it takes the stream over rather than
        /// sitting down again beside itself.
        member _.Open console =
            let channel = Channel.CreateUnbounded<string>()

            lock gate (fun () ->
                match streams.TryGetValue console with
                | true, before -> before.Writer.TryComplete() |> ignore
                | _ -> ()

                streams[console] <- channel)

            channel

        /// Let a stream go, unless the console has already opened another - in which case
        /// the newer one is the one that counts and this is the tail of an old request.
        member _.Close(console, channel: Channel<string>) =
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

    /// What the browser side needs of the table it is sitting at: a way to change it, and a
    /// way to tell everybody what came of that.
    ///
    /// Both come in from outside because delivering reaches consoles this file knows
    /// nothing about - the players at terminals - and what those are is settled a file
    /// later. All that matters here is that a change made by a page reaches all of them.
    [<NoComparison; NoEquality>]
    type Sitting =
        { Change: (Lobby -> Lobby * Post list) -> Post list
          Deliver: Post list -> unit }

    // --- what goes down a stream -----------------------------------------------------------

    /// One thing said to a page, in the words its client reads.
    ///
    /// The payload is written a line at a time because that is what the format allows and
    /// because the board needs it: the cascades and the rules are laid out as written text,
    /// newlines and all, and a newline is what separates one instruction from the next. Sent
    /// as several lines it arrives as one string with the newlines back in it.
    let private frame event key (payload: string) =
        let said = StringBuilder()
        said.Append("event: ").Append(event: string).Append('\n') |> ignore

        for line in payload.Split([| "\r\n"; "\n" |], StringSplitOptions.None) do
            said.Append("data: ").Append(key: string).Append(' ').Append(line).Append('\n')
            |> ignore

        said.Append('\n').ToString()

    /// A screen, patched over whatever is standing in its place.
    ///
    /// Nothing says where it goes. Every fragment `Html` draws is one element with an id on
    /// it, and the client puts an element where the element of that id already is - so what
    /// decides whether a board lands on the board or beside it is the fragment itself,
    /// which is the one place that knows.
    let private elements = frame "datastar-patch-elements" "elements"

    /// What a page is sent for each thing the table says to it. `Seated` is the exception:
    /// it says which seat was taken, which the page has no use for - a browser knows itself
    /// by its cookie - and which is caught on the way past and remembered instead.
    let private saying =
        function
        | Screen text
        | Told text -> Some(elements text)
        | TurnedAway why -> Some(elements (Html.says why))
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
    /// the wire and read by the same function. A page that asks for nothing gets the
    /// standard ones, and a colour the table has never heard of is passed over rather than
    /// being a reason to turn anybody away.
    ///
    /// Said more than once, because that is what a form does: each of the five choosers on
    /// the page is one `colours`, and what they add up to is the palette. Joined back into
    /// the one line `Palette.read` takes, which is also the line that goes down a wire.
    let private paletteOf (ctx: HttpContext) =
        match ctx.Request.Query.TryGetValue "colours" with
        | true, given when given.Count > 0 -> Palette.read (String.Join(" ", given.ToArray()))
        | _ -> Palette.standard

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
    let page (ctx: HttpContext) =
        let palette = paletteOf ctx
        consoleOf ctx |> ignore
        ctx.Response.ContentType <- "text/html; charset=utf-8"
        ctx.Response.WriteAsync(Html.page palette)

    /// A line typed in a browser. It may come in the address, which is how a button says
    /// what it does, or in the body, which is how the box at the bottom says what was typed
    /// into it. Either way it is a line, and it goes to the same parser as any other.
    let private lineOf (ctx: HttpContext) =
        task {
            match ctx.Request.Query.TryGetValue "line" with
            | true, given when given.Count > 0 -> return string given[0]
            | _ ->
                use reader = new StreamReader(ctx.Request.Body)
                let! body = reader.ReadToEndAsync()

                // Nothing typed is a line too - it redraws the board - so a body that says
                // nothing is answered with nothing rather than with a complaint.
                try
                    use parsed = JsonDocument.Parse body

                    match parsed.RootElement.TryGetProperty "line" with
                    | true, value -> return (value.GetString() |> Option.ofObj |> Option.defaultValue "")
                    | _ -> return ""
                with _ ->
                    return ""
        }

    let say (sitting: Sitting) (ctx: HttpContext) =
        task {
            let console = consoleOf ctx
            let! line = lineOf ctx

            sitting.Change(Lobby.said console line) |> sitting.Deliver

            // The board comes back down the stream like any other, so all this has to
            // answer with is an empty box to type the next line into.
            ctx.Response.ContentType <- "application/json"
            do! ctx.Response.WriteAsync """{"line": ""}"""
        }

    /// The stream a page holds open, and everything that happens while it does.
    ///
    /// Sitting down happens here rather than when the page was served, because a seat is
    /// only worth having while somebody is holding it: a page fetched and closed again has
    /// nobody at it, and the table would be waiting on a chair nobody is in.
    let stream (sitting: Sitting) (pages: Pages) (ctx: HttpContext) =
        task {
            let console = consoleOf ctx
            let view = View.html (paletteOf ctx)

            ctx.Response.ContentType <- "text/event-stream"
            ctx.Response.Headers.CacheControl <- "no-cache"
            // Anything between here and the browser that would rather collect a whole
            // response before passing it on has to be told not to. A stream that is only
            // delivered once it ends is not a stream.
            ctx.Response.Headers["X-Accel-Buffering"] <- "no"

            ctx.Features.Get<IHttpResponseBodyFeature>()
            |> Option.ofObj
            |> Option.iter (fun body -> body.DisableBuffering())

            let channel = pages.Open console
            do! ctx.Response.Body.FlushAsync ctx.RequestAborted

            let seated =
                sitting.Change(Lobby.join console (Guid.NewGuid().ToString "N") (pages.Seat console) view)

            // Which seat this browser was given, kept so that the next visit on the same
            // cookie comes back to it rather than being handed a stranger's stones.
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
                        let mutable said = ""

                        while channel.Reader.TryRead &said do
                            do! ctx.Response.WriteAsync(said, ctx.RequestAborted)

                        do! ctx.Response.Body.FlushAsync ctx.RequestAborted
            with
            // The page has gone. That is how a browser says goodbye - there is no other
            // way for it to - so it is the ordinary ending rather than a failure.
            | :? OperationCanceledException ->
                ()

            pages.Close(console, channel)
            sitting.Change(Lobby.left console) |> sitting.Deliver
        }
