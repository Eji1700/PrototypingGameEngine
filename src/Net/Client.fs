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

            if uri.IsDefaultPort then builder.Port <- Protocol.DefaultPort

            if builder.Path = "/" then builder.Path <- Protocol.Path

            Ok(builder.Uri.ToString())
        | _ -> Error $"'{given}' is not an address I can reach."

    /// Print what arrived, then the prompt again, so a board that lands while the player
    /// is reading does not leave them staring at a bare line.
    ///
    /// Nothing is done to the text here. A view lays a whole screen out and so needs the
    /// game to do it, and the game is at the table - so the board arrives already drawn
    /// the way this player asked for it, and this end only has to print it.
    let private show (text: string) =
        System.Console.Write text
        System.Console.Write "> "
        System.Console.Out.Flush()

    /// Ask for the player's attention, in the one way a terminal has of asking.
    ///
    /// A bell, which is a single character and has meant this since before there were
    /// windows to put a terminal in. What becomes of it is the terminal's business and the
    /// player's: most of them flash the window in the taskbar when it is not the one being
    /// looked at, some make a sound, and one told to do neither does neither. None of that
    /// is worth second-guessing from here - and a console cannot see whether anybody is
    /// watching it anyway, so it rings whenever the table says the turn arrived unasked.
    let private ring () =
        System.Console.Write '\a'
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

        // Read from inside the handlers below, which is why it is a cell rather than a
        // plain mutable. It is what the table gives back, kept so a console that drops can
        // come back to the same seat rather than being handed a new one - or worse, none.
        let token = ref (resuming |> Option.defaultValue "")

        /// Sitting down says who this console is and how it would like to read, because a
        /// board is drawn at the table and the table has to know before it draws one. The
        /// colours go with it for the same reason: a board arrives already coloured, so a
        /// palette chosen at this end is no use unless the other end has it.
        let sitDown () =
            connection.InvokeAsync(Protocol.Call.Join, box token.Value, box chosen.Name, box (Palette.write chosen.Palette))

        connection.On<int, string>(
            Protocol.Call.Seated,
            fun seat mine ->
                token.Value <- mine
                printfn ""
                printfn "You are Player %d. If you drop, this brings you back to the same seat:" seat
                printfn ""
                // Written from the same declaration the command line is read by, so what
                // a player is told to type is something the program is certain to accept.
                printfn "  dotnet run -- %s" (Launch.write (Launch.Join(given, Some mine)))
        )
        |> ignore

        connection.On<string>(Protocol.Call.Screen, show) |> ignore

        connection.On<string>(Protocol.Call.Told, fun text -> show (text + Environment.NewLine))
        |> ignore

        connection.On<string>(
            Protocol.Call.TurnedAway,
            fun why ->
                printfn ""
                printfn "%s" why
        )
        |> ignore

        // Nothing to print. The board saying it is your turn is already on its way down the
        // same wire; this is only the part of that a player two rooms away can hear.
        connection.On(Protocol.Call.Nudged, Action ring) |> ignore

        // Coming back after a drop has to say who this console was, or the table would
        // hand it an empty seat and the player would lose their stones.
        connection.add_Reconnected (fun _ -> sitDown ())

        try
            wait (connection.StartAsync())
        with problem ->
            eprintfn "There is no table at %s - %s" url problem.Message
            exit 1

        wait (sitDown ())

        let rec loop () =
            match System.Console.ReadLine() with
            | null -> ()
            | line ->
                wait (connection.InvokeAsync(Protocol.Call.Say, box line))
                loop ()

        loop ()
        wait (connection.StopAsync())
        0
