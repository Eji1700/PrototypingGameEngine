namespace TCModel.Net

open System
open System.Net
open System.Net.Sockets
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open TCModel.Domain
open TCModel.App
open TCModel.Console

/// The one lobby this process is hosting, and the only thing in the whole program that
/// is not a value.
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
            | TurnedAway why -> console.SendAsync(Protocol.Call.TurnedAway, box why))
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
    let private reachableAt port =
        try
            Dns.GetHostAddresses(Dns.GetHostName())
            |> Array.filter (fun address -> address.AddressFamily = AddressFamily.InterNetwork)
            |> Array.map (fun address -> $"  {address}:{port}")
            |> List.ofArray
        with _ ->
            []

    /// Open a table and wait at it. Blocks until the host stops the process, which is
    /// how a table is closed: there is no move for closing one, because no player at it
    /// has the standing to close it on everybody else.
    let host port model keep =
        let builder = WebApplication.CreateBuilder()

        let held = Held(Lobby.opened model, keep)
        let pages = Browser.Pages()

        // The console is a board, not a log. Anything the framework wants to say would
        // land in the middle of it.
        builder.Logging.ClearProviders() |> ignore
        builder.Services.AddSignalR() |> ignore
        builder.Services.AddSingleton<Held> held |> ignore
        builder.Services.AddSingleton<Browser.Pages> pages |> ignore
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}") |> ignore

        let app = builder.Build()
        app.MapHub<TableHub>(Protocol.Path) |> ignore

        // What a page needs of the table, which is what a console needs of it: a way to
        // change it, and a way for everybody to hear what came of that. The hub's own
        // clients rather than a caller's, because a move made in a browser has to reach
        // the terminals, and there is no call in progress to borrow them from.
        let hub = app.Services.GetRequiredService<IHubContext<TableHub>>()

        let sitting: Browser.Sitting =
            { Change = held.Change
              Deliver = fun posts -> Wire.deliver hub.Clients pages posts |> ignore }

        app.MapGet(Html.Client, RequestDelegate(fun ctx -> Browser.script ctx))
        |> ignore

        app.MapGet("/", RequestDelegate(fun ctx -> Browser.page ctx)) |> ignore

        app.MapGet(Html.Stream, RequestDelegate(fun ctx -> Browser.stream sitting pages ctx :> Task))
        |> ignore

        app.MapPost(Html.Say, RequestDelegate(fun ctx -> Browser.say sitting ctx :> Task))
        |> ignore

        let seats = Game.playerCount (Model.game model)

        printfn ""
        printfn "=== A table for %d, waiting to be joined ===" seats
        printfn ""
        printfn "  Each player either runs:"
        printfn ""
        printfn "    dotnet run -- join <address>"
        printfn ""
        printfn "  or opens <address> in a browser. Both sit down at this one table."
        printfn ""
        printfn "  This table is at:"
        printfn ""
        reachableAt port |> List.iter (printfn "%s")
        printfn "  localhost:%d          (for anyone on this machine)" port
        printfn ""
        printfn "  The game begins once all %d seats are taken. Ctrl+C closes the table." seats
        printfn ""

        app.Run()
        0
