namespace TCModel.Table

/// Driving a screen at a real terminal: clear it, draw it, read a key, hand back a line.
///
/// The other half of `Keys`, and the only half that touches anything. `Keys` says what a
/// screen reads like and what a press comes to, purely and testably; this is what turns that
/// into a person at a keyboard. Neither knows what is being chosen - a screen here is a title,
/// some prose, some rows and a note, and every one of them stands for a line somebody could
/// have typed instead.
///
/// So nothing in this file mentions the game, or even the menu. A screen is written out
/// through whatever is doing the saying - which for every screen about a game is the very
/// view that will be drawing the board, so the colours a player is picking are shown to them
/// in the colours they are picking. The one screen that comes before there is a game to have
/// a view of hands in `id`, and reads as the plain text it is.
module Screens =

    /// Whether there is somebody at the keyboard to steer with. A line piped in cannot press
    /// an arrow, and a redirected console throws rather than answering for one, so a screen
    /// shown to one of those is shown whole and read a line at a time exactly as it always was.
    let steering () = not System.Console.IsInputRedirected

    /// Nothing here is worth losing a turn over: a console that will not clear is a console
    /// the screens scroll in, which is how they read before there was anything to move.
    let cleared () =
        try
            System.Console.Clear()
        with _ ->
            ()

    /// What this window is called, kept so it can be put back. Read once, because a terminal
    /// that will not say is a terminal this asks only once and then stops asking.
    ///
    /// Windows answers; a good many others throw rather than tell you, so the fallback is a
    /// name rather than nothing - a window whose title cannot be read still has one set, and
    /// putting back an empty string would leave a tab with no name on it at all.
    let private wasCalled =
        lazy
            (try
                match System.Console.Title with
                | "" -> "TCModel"
                | title -> title
             with _ ->
                 "TCModel")

    /// Mark this window as wanting somebody, or put it back as it was.
    ///
    /// The terminal's own half of a nudge, and the one part of it that does not depend on how
    /// the terminal was set up. A bell is a request a terminal is free to ignore - Windows
    /// Terminal makes no sound and flashes nothing unless its `bellStyle` says to - but a
    /// title is shown by all of them, on the tab and under the mouse in the taskbar.
    ///
    /// The same thing the browser does with its tab, and here so that the two consoles behave
    /// alike: a player at a page and a player at a terminal should not need different habits
    /// to notice the same game waiting for them.
    ///
    /// Swallowed like everything else here. A console that will not be renamed is a console
    /// with a bell and a board, which is how this read before there was a title to set.
    let marking wanted =
        try
            System.Console.Title <- if wanted then $"* {wasCalled.Value}" else wasCalled.Value
        with _ ->
            ()

    /// Hold the screen until a key, for the things that are longer than the screen about to
    /// wipe them. Nobody reads at the speed of a keypress, so this is only ever a courtesy.
    let held () =
        if steering () then
            printf "Press any key."
            System.Console.ReadKey true |> ignore

    /// A key, or the time being up - whichever comes first.
    ///
    /// The one thing in this program that watches a clock, and it is nine lines in the file
    /// whose whole job is touching things. Everything above it is a fold: a game that does
    /// not wait says how long a table should leave between beats, the table plays one when
    /// this says nobody pressed anything, and the beat is a move like every other move.
    ///
    /// Polled rather than waited on, because a console cannot be asked for a key *until* a
    /// moment. The wait is short enough that a key feels immediate and long enough that a
    /// game running at three or four beats a second is not a core spinning.
    let awaiting (until: System.DateTime) =
        let rec waiting () =
            if System.Console.KeyAvailable then
                Some(System.Console.ReadKey true)
            elif System.DateTime.UtcNow >= until then
                None
            else
                System.Threading.Thread.Sleep 8
                waiting ()

        waiting ()

    /// Ask a screen for a line.
    ///
    /// What comes back is a line in the words a person would have typed, so on the other side
    /// of this is the same reader that has always been there - the arrows are a way of typing
    /// rather than a second way of meaning something. Where the highlight was left comes back
    /// with it: walking a colour along changes the palette, which builds the screen again, and
    /// the cursor has to still be on the slot that is being changed.
    let asking (says: string -> string) said screen at =
        let rec steer standing =
            let showing, index = Keys.facing standing
            cleared ()
            printf "%s" (says (Keys.draw (Some index) showing))

            if said <> "" then printfn "%s" (says said)

            printf "> %s" standing.Buffer

            match Keys.answer (Keys.pressed (Keys.typing standing) (System.Console.ReadKey true)) standing with
            | Keys.Steering next -> steer next
            | Keys.Answered line -> Some line, Keys.started standing

        if steering () then
            steer (Keys.standing screen at)
        else
            printf "%s" (says (Keys.draw None screen))

            if said <> "" then printfn "%s" (says said)

            printf "> "

            match System.Console.ReadLine() with
            | null -> None, at
            | line -> Some line, at
