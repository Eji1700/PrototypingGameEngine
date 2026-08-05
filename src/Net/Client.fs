namespace TCModel.Net

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.SignalR.Client
open TCModel.Console

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
    ///
    /// The table sends the board plainly and it is made to look like something here, at
    /// the console it is going to. So a player picks how they read the game for
    /// themselves, and two people at the same table can pick differently.
    let private show (view: View) (text: string) =
        System.Console.Write(view.Show text)
        System.Console.Write "> "
        System.Console.Out.Flush()

    let private wait (task: Task) = task.GetAwaiter().GetResult()

    let join given resuming (chosen: View) =
        match endpoint given with
        | Error problem ->
            eprintfn "%s" problem
            1
        | Ok url ->

        let connection =
            HubConnectionBuilder().WithUrl(url).WithAutomaticReconnect().Build()

        // Both of these are read from inside the handlers below, which is why they are
        // cells rather than plain mutables.
        //
        // The token is what the table gives back, kept so a console that drops can come
        // back to the same seat rather than being handed a new one - or worse, none.
        let token = ref (resuming |> Option.defaultValue "")

        // The view never crosses the wire: the table sends one plain board and each
        // console makes of it what it likes, so a player can change their mind mid-game
        // without the game hearing about it.
        let view = ref chosen

        connection.On<int, string>(
            Protocol.Call.Seated,
            fun seat mine ->
                token.Value <- mine
                printfn ""
                printfn "You are Player %d. If you drop, this brings you back to the same seat:" seat
                printfn ""
                printfn "  dotnet run -- join %s %s" given mine
        )
        |> ignore

        connection.On<string>(Protocol.Call.Screen, fun text -> show view.Value text) |> ignore

        connection.On<string>(Protocol.Call.Told, (fun text -> show view.Value (text + Environment.NewLine)))
        |> ignore

        connection.On<string>(
            Protocol.Call.TurnedAway,
            fun why ->
                printfn ""
                printfn "%s" why
        )
        |> ignore

        // Coming back after a drop has to say who this console was, or the table would
        // hand it an empty seat and the player would lose their stones.
        connection.add_Reconnected(fun _ -> connection.InvokeAsync(Protocol.Call.Join, box token.Value))

        try
            wait (connection.StartAsync())
        with problem ->
            eprintfn "There is no table at %s - %s" url problem.Message
            exit 1

        wait (connection.InvokeAsync(Protocol.Call.Join, box token.Value))

        let rec loop () =
            match System.Console.ReadLine() with
            | null -> ()
            | line ->
                // How the board is drawn is this console's own business, so it is
                // answered here and never sent. Everything else is the table's.
                match Parse.line line with
                | Ok(Parse.Looking name) ->
                    match View.byName name with
                    | Ok chosen ->
                        view.Value <- chosen
                        show view.Value ""
                    | Error problem -> show view.Value (problem + Environment.NewLine)
                | _ -> wait (connection.InvokeAsync(Protocol.Call.Say, box line))

                loop ()

        loop ()
        wait (connection.StopAsync())
        0
