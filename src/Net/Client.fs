namespace TCModel.Net

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.AspNetCore.Http.Connections.Client
open Microsoft.AspNetCore.SignalR.Client
open TCModel.Table

/// A console at somebody else's table.
///
/// It holds no game and knows no rules. It sends the line that was typed and prints what
/// comes back, which is already a board drawn for this player and nobody else - so there
/// is nothing here that could show a player something they should not see, because there
/// is nothing here to show.
module Client =

    /// How long a console keeps trying to get back to a table it has lost.
    ///
    /// The usual answer is a handful of goes and then give up, which is right for a page
    /// somebody can reload and wrong for a seat. The seat is *kept*: nobody else may take
    /// it, the game will not go on without it, and the table is content to wait all evening.
    /// So this waits too, backing off to half a minute and then staying there for as long as
    /// the player leaves the window open - there is nothing else it could sensibly do, and
    /// nothing whatever is lost by doing it.
    type private Patient() =
        interface IRetryPolicy with
            member _.NextRetryDelay(context: RetryContext) =
                Nullable(TimeSpan.FromSeconds(min 30.0 (2.0 ** float (min context.PreviousRetryCount 5))))

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

    /// Ask for the player's attention, in the two ways a terminal has of asking.
    ///
    /// **A bell**, which is a single character and has meant this since before there were
    /// windows to put a terminal in. What becomes of it is the terminal's business and the
    /// player's: most of them flash the window in the taskbar when it is not the one being
    /// looked at, some make a sound, and one told to do neither does neither. None of that
    /// is worth second-guessing from here - a console cannot see whether anybody is watching
    /// it anyway. Whether to ring at all is the player's answer, given at the Audio page and
    /// handed in, because that is a question about their room rather than about the table.
    ///
    /// **And the title**, which is the same thing the browser already does with its tab, and
    /// is here so that the two consoles behave alike. It does not depend on how a terminal
    /// was configured, which the bell entirely does: a terminal told to flash nothing and
    /// sound nothing still shows a title, so a player who has turned the bell off is not left
    /// with a table that cannot get their attention at all.
    ///
    /// Not settled by the Audio page, and deliberately: a title is not a noise, it interrupts
    /// nobody, and there is nothing about it worth turning off.
    let private ring rings =
        if rings then
            System.Console.Write '\a'
            System.Console.Out.Flush()

        Screens.marking true

    /// And put it back, which is what the browser does when somebody looks again. Here that
    /// is a line being typed: a console cannot see whether anybody is watching, but somebody
    /// who has just typed at it plainly is.
    let private looked () = Screens.marking false

    let private wait (task: Task) = task.GetAwaiter().GetResult()

    /// `rings` is the Audio page's answer, handed in rather than read here for the reason
    /// everything else about how a game is read is handed in: this file knows about a wire and
    /// a keyboard, and nothing about what somebody settled on.
    let join game given resuming code rings (chosen: View<_, _, _>) =
        match Reach.endpoint Protocol.Path given with
        | Error problem ->
            eprintfn "%s" problem
            1
        | Ok url ->

        // The word at the door goes in a header rather than in the address, because it is
        // sent on every request this makes and an address is a thing that gets written down:
        // in a log, in a shell's history, in whatever a proxy keeps. A browser has no way of
        // setting one and so is handed a cookie instead; a console has no cookie jar worth
        // the name and so does this.
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

        // The same waiting the table keeps, said from this end. A console that has heard
        // nothing for twenty seconds is not a console whose table has gone - out past a
        // local network that is an ordinary minute - and the two ends have to agree about
        // that or one of them will keep hanging up on a game the other thinks is fine.
        connection.ServerTimeout <- TimeSpan.FromSeconds 60.0
        connection.HandshakeTimeout <- TimeSpan.FromSeconds 30.0
        connection.KeepAliveInterval <- TimeSpan.FromSeconds 15.0

        // Read from inside the handlers below, which is why it is a cell rather than a
        // plain mutable. It is what the table gives back, kept so a console that drops can
        // come back to the same seat rather than being handed a new one - or worse, none.
        let token = ref (resuming |> Option.defaultValue "")

        /// Whether the table has said this console is up from it.
        ///
        /// A cell for the same reason the token is one: it is set from inside a handler,
        /// which runs on the wire's own thread. The handler sets it and nothing else - the
        /// socket is the main thread's to hang up, and a handler that closed the connection
        /// it was being called from would be closing it from inside itself.
        let up = ref false

        /// And a console with nothing left to do, which is either of two quite different
        /// things arriving on two different threads: the table saying this seat is up, or a
        /// keyboard with nothing more coming.
        let over = new ManualResetEventSlim(false)

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
                printfn "You are at seat %d. If you drop, this brings you back to it:" seat
                printfn ""
                // Written from the same declaration the command line is read by, so what
                // a player is told to type is something the program is certain to accept -
                // including the word at the door, which they would otherwise have to
                // remember they had been given.
                printfn "  %s" (Launch.written game (Launch.Join(given, Some mine, code)))
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

        // The table saying this console is done, which arrives as the answer to the line
        // that said so. No prompt after it, because there is nothing more to type.
        connection.On<string>(
            Protocol.Call.GotUp,
            fun said ->
                printfn ""
                printfn "%s" said
                up.Value <- true
                over.Set()
        )
        |> ignore

        // Nothing to print. The board saying it is your turn is already on its way down the
        // same wire; this is only the part of that a player two rooms away can hear.
        connection.On(Protocol.Call.Nudged, Action(fun () -> ring rings)) |> ignore

        // What a player is told when the wire goes, which is a thing that happens between
        // houses and hardly ever within one. Worth saying out loud both times: a board that
        // has stopped changing looks exactly like a game where nobody has moved, and a
        // player who does not know which they are looking at will sit there politely.
        connection.add_Reconnecting (fun _ ->
            printfn ""
            printfn "The table stopped answering. Your seat is kept - still trying to reach it."
            show ""
            Task.CompletedTask)

        // Coming back after a drop has to say who this console was, or the table would
        // hand it an empty seat and the player would lose their stones.
        connection.add_Reconnected (fun _ ->
            printfn ""
            printfn "Back at the table."
            sitDown ())

        /// Sit down, and keep trying for a little while first.
        ///
        /// A table further off than a room is not always up before the player who was told
        /// about it, and a first attempt that lands in the half-second before the far end
        /// starts listening is not worth an error message. What *is* worth one is a door
        /// that answers and refuses, which is a different thing entirely and says so.
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

        // Given up on rather than exited from, because this is no longer always a process of
        // its own: a table that seats its host runs one of these inside itself, and a console
        // that could not find its way to a seat is no reason to close the table on everybody
        // who did.
        if not (arriving 3) then
            1
        else

        wait (sitDown ())

        // Read a line and say it, until either the keyboard or the table is done.
        //
        // Whether a line ends the game is the table's to decide and not this end's. A console
        // that read `quit` for itself would be a second opinion about what it means, free to
        // differ from the one the game keeps - and wrong at a table that never sat it down in
        // the first place. So it types the word like any other, and what comes back is what
        // stops it.
        let rec reading () =
            match System.Console.ReadLine() with
            | null -> ()
            | line ->
                // Somebody is plainly here, so the window stops asking for them.
                looked ()
                // A line typed while the wire is down is a line that goes nowhere, and the
                // thing not to do about it is fall over: the seat is still this player's,
                // the game has not moved, and saying it again in a moment will work. This is
                // the one place a console can be left holding something the table never
                // heard, so it is the one place that has to say so - unless the wire is going
                // because this console is, in which case there is nothing to say again.
                try
                    wait (connection.InvokeAsync(Protocol.Call.Say, box line))
                with _ ->
                    if not up.Value then
                        printfn ""
                        printfn "That did not reach the table. Say it again in a moment."
                        show ""

                if not up.Value then reading ()

        // On a thread of its own, and waited on from here, because the two things that end a
        // console cannot wait for each other. `ReadLine` is already blocked by the time the
        // table's answer arrives - the player has typed their last line and the console has
        // gone straight back to asking for another - and a keyboard nobody is going to touch
        // again is a wait that never comes back. Nor can it be got round by checking a flag
        // after each line: what the table says arrives on the wire's own thread, and there is
        // nothing to say how late it may be.
        //
        // So the reader is left where it stands when the table is the one that finished. It
        // is blocked on a keyboard, it holds nothing, and it goes when the process does.
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
        // Whatever the window was called before this console had it, it is called again. A
        // player who leaves a table should not be left with a tab still asking for them.
        looked ()
        0
