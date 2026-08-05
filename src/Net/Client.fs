namespace TCModel.Net

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.SignalR.Client

/// A console at somebody else's table.
///
/// It holds no game and knows no rules. It sends the line that was typed and prints what
/// comes back, which is already a board drawn for this player and nobody else - so there
/// is nothing here that could show a player something they should not see, because there
/// is nothing here to show.
module Client =

    /// An address as a player would say it - "greg-pc", "192.168.1.9:5000", a whole URL -
    /// filled out into the one the table is actually listening on.
    let private endpoint (given: string) =
        let text = if given.Contains "://" then given else "http://" + given

        match Uri.TryCreate(text, UriKind.Absolute) with
        | true, uri ->
            let builder = UriBuilder(uri)

            if uri.IsDefaultPort then
                builder.Port <- Protocol.DefaultPort

            if builder.Path = "/" then
                builder.Path <- Protocol.Path

            Ok(builder.Uri.ToString())
        | _ -> Error $"'{given}' is not an address I can reach."

    /// Print what arrived, then the prompt again, so a board that lands while the player
    /// is reading does not leave them staring at a bare line.
    let private show (text: string) =
        Console.Write text
        Console.Write "> "
        Console.Out.Flush()

    let private wait (task: Task) = task.GetAwaiter().GetResult()

    let join given resuming =
        match endpoint given with
        | Error problem ->
            eprintfn "%s" problem
            1
        | Ok url ->

        let connection =
            HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build()

        // The token the table gives back. Kept so a console that drops can come back to
        // the same seat rather than being handed a new one - or worse, none at all.
        let mutable token = resuming |> Option.defaultValue ""

        connection.On<int, string>(
            Protocol.Call.Seated,
            fun seat mine ->
                token <- mine
                printfn ""
                printfn "You are Player %d. If you drop, this brings you back to the same seat:" seat
                printfn ""
                printfn "  dotnet run -- join %s %s" given mine
        )
        |> ignore

        connection.On<string>(Protocol.Call.Screen, show) |> ignore
        connection.On<string>(Protocol.Call.Told, fun text -> show (text + Environment.NewLine)) |> ignore

        connection.On<string>(
            Protocol.Call.TurnedAway,
            fun why ->
                printfn ""
                printfn "%s" why
        )
        |> ignore

        // Coming back after a drop has to say who this console was, or the table would
        // hand it an empty seat and the player would lose their stones.
        connection.add_Reconnected(fun _ -> connection.InvokeAsync(Protocol.Call.Join, box token))

        try
            wait (connection.StartAsync())
        with problem ->
            eprintfn "There is no table at %s - %s" url problem.Message
            exit 1

        wait (connection.InvokeAsync(Protocol.Call.Join, box token))

        let rec loop () =
            match Console.ReadLine() with
            | null -> ()
            | line ->
                wait (connection.InvokeAsync(Protocol.Call.Say, box line))
                loop ()

        loop ()
        wait (connection.StopAsync())
        0
