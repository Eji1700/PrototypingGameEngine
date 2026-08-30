namespace Prototyping.Net

open System
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http.Connections.Client
open Microsoft.AspNetCore.SignalR.Client
open Prototyping.Table

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

    /// The status a door answered with, wherever the client library buried it - which is what says
    /// whether the table refused us or nothing answered at all.
    let rec private statusOf (problem: exn) =
        match problem with
        | :? HttpRequestException as http when http.StatusCode.HasValue -> Some(int http.StatusCode.Value)
        | :? AggregateException as several -> several.InnerExceptions |> Seq.tryPick statusOf
        | _ ->
            match problem.InnerException with
            | null -> None
            | inner -> statusOf inner

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

        connection.ServerTimeout <- Protocol.GivenUp
        connection.HandshakeTimeout <- Protocol.Handshake
        connection.KeepAliveInterval <- Protocol.KeepAlive

        let token = ref (resuming |> Option.defaultValue "")

        // Set once the table has nothing more for this console - it got up, or it was turned away -
        // so that the prompt is not left waiting for lines nothing will answer.
        let over = new ManualResetEventSlim(false)
        let turned = ref false

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
                turned.Value <- true
                over.Set()
        )
        |> ignore

        connection.On<string>(
            Protocol.Call.GotUp,
            fun said ->
                printfn ""
                printfn "%s" said
                over.Set()
        )
        |> ignore

        connection.On(Protocol.Call.Nudged, Action(fun () -> ring rings)) |> ignore

        // A sound the board made is not a summons, so this does not mark the window title the way a
        // nudge does - and a terminal keeps its one bell for the sounds that are worth one.
        connection.On<string>(
            Protocol.Call.Rang,
            fun word ->
                match Sound.byWord word with
                | Some sound when rings && Sound.worthABell sound -> bell ()
                | Some _
                | None -> ()
        )
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
                match statusOf problem with
                | Some 401
                | Some 403 ->
                    match code with
                    | None -> eprintfn "That table has a word at its door. Say it with --code <word>."
                    | Some _ -> eprintfn "That table has a word at its door, and that is not it."

                    false
                | Some 429 ->
                    eprintfn "That door has heard too many wrong words lately and is not answering for the moment."
                    eprintfn "Try again in a few seconds."
                    false
                | Some status ->
                    eprintfn "Something at %s answered %d, which is not a table." url status
                    false
                | None when attempts <= 1 ->
                    eprintfn "Nothing answered at %s. Is the table open, and is that its address?" url
                    false
                | None ->
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
                    if not over.IsSet then
                        printfn ""
                        printfn "That did not reach the table. Say it again in a moment."
                        show ""

                if not over.IsSet then reading ()

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
        if turned.Value then 1 else 0
