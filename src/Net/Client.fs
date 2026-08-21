namespace TCModel.Net

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http.Connections.Client
open Microsoft.AspNetCore.SignalR.Client
open TCModel.Table

module Client =

    // Backing off by doubling up to half a minute, and then trying at that for as long as the console
    // is left running. A table that is restarted while somebody is away should still be there when
    // they come back to the keyboard.
    type private Patient() =
        interface IRetryPolicy with
            member _.NextRetryDelay(context: RetryContext) =
                Nullable(TimeSpan.FromSeconds(min 30.0 (2.0 ** float (min context.PreviousRetryCount 5))))

    let private show (text: string) =
        System.Console.Write text
        System.Console.Write "> "
        System.Console.Out.Flush()

    let private bell () =
        System.Console.Write '\a'
        System.Console.Out.Flush()

    let private ring rings =
        if rings then bell ()

        Screens.marking true

    let private looked () = Screens.marking false

    let private wait (task: Task) = task.GetAwaiter().GetResult()

    let join game given resuming code table rings (chosen: View<_, _, _>) =
        let where =
            match table with
            | Some name -> $"{Protocol.Path}/{name}"
            | None -> Protocol.Path

        match Reach.endpoint where given with
        | Error problem ->
            eprintfn "%s" problem
            1
        | Ok url ->

        let connection =
            HubConnectionBuilder()
                .WithUrl(
                    url,
                    fun (options: HttpConnectionOptions) ->
                        match code with
                        | Some code -> options.Headers[Reach.Header] <- code
                        | None -> ()
                )
                .WithAutomaticReconnect(Patient())
                .Build()

        connection.ServerTimeout <- TimeSpan.FromSeconds 60.0
        connection.HandshakeTimeout <- TimeSpan.FromSeconds 30.0
        connection.KeepAliveInterval <- TimeSpan.FromSeconds 15.0

        let token = ref (resuming |> Option.defaultValue "")

        let up = ref false

        let over = new ManualResetEventSlim(false)

        let sitDown () =
            connection.InvokeAsync(Protocol.Call.Join, box token.Value, box chosen.Name, box (Palette.write chosen.Palette))

        connection.On<int, string>(
            Protocol.Call.Seated,
            fun seat mine ->
                token.Value <- mine
                printfn ""
                printfn "You are at seat %d. If you drop, this brings you back to it:" seat
                printfn ""
                printfn "  %s" (Launch.written game (Launch.Join(given, Some mine, code, table)))
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

        connection.On<string>(
            Protocol.Call.GotUp,
            fun said ->
                printfn ""
                printfn "%s" said
                up.Value <- true
                over.Set()
        )
        |> ignore

        connection.On(Protocol.Call.Nudged, Action(fun () -> ring rings)) |> ignore

        // A terminal has one bell and no way to say which of three sounds it is making, so all
        // three are it, and a board that rang twice in a beat rings once. The marking a nudge
        // leaves on the window title is not made: a sound the board made is not a summons.
        connection.On(Protocol.Call.Rang, Action(fun () -> if rings then bell ()))
        |> ignore

        connection.add_Reconnecting (fun _ ->
            printfn ""
            printfn "The table stopped answering. Your seat is kept - still trying to reach it."
            show ""
            Task.CompletedTask)

        // The connection comes back with a new id, so the table has no idea it is the same console.
        // Sitting down again with the token it was given is what claims the same seat back.
        connection.add_Reconnected (fun _ ->
            printfn ""
            printfn "Back at the table."
            sitDown ())

        let rec arriving attempts =
            try
                wait (connection.StartAsync())
                true
            with problem ->
                let refused = problem.Message.Contains "401" || problem.Message.Contains "403"

                if refused then
                    eprintfn "That table would not let me in - %s" problem.Message
                    eprintfn "It has a word at its door. Say it with --code <word>."
                    false
                elif attempts <= 1 then
                    eprintfn "There is no table at %s - %s" url problem.Message
                    false
                else
                    eprintfn "No answer from %s yet - trying again." url
                    Thread.Sleep 2000
                    arriving (attempts - 1)

        if not (arriving 3) then
            1
        else

        wait (sitDown ())

        let rec reading () =
            match System.Console.ReadLine() with
            | null -> ()
            | line ->
                looked ()

                try
                    wait (connection.InvokeAsync(Protocol.Call.Say, box line))
                with _ ->
                    if not up.Value then
                        printfn ""
                        printfn "That did not reach the table. Say it again in a moment."
                        show ""

                if not up.Value then reading ()

        let hands =
            Thread(
                ThreadStart(fun () ->
                    reading ()
                    over.Set()),
                IsBackground = true
            )

        hands.Start()
        over.Wait()
        wait (connection.StopAsync())
        looked ()
        0
