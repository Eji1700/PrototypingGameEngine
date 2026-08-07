namespace TCModel.Console

/// Driving a screen at a real terminal: clear it, draw it, read a key, hand back a line.
///
/// The other half of `Keys`, and the only half that touches anything. `Keys` says what a
/// screen reads like and what a press comes to, purely and testably; this is what turns that
/// into a person at a keyboard. Neither knows what is being chosen - a screen here is a title,
/// some prose, some rows and a note, and every one of them stands for a line somebody could
/// have typed instead.
///
/// So nothing in this file mentions the game, or even the menu. It is above `View` only
/// because a screen is drawn through the same view that will be drawing the board, which is
/// how the colours a player is picking are shown in the colours they are picking.
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

    /// Hold the screen until a key, for the things that are longer than the screen about to
    /// wipe them. Nobody reads at the speed of a keypress, so this is only ever a courtesy.
    let held () =
        if steering () then
            printf "Press any key."
            System.Console.ReadKey true |> ignore

    /// Ask a screen for a line.
    ///
    /// What comes back is a line in the words a person would have typed, so on the other side
    /// of this is the same reader that has always been there - the arrows are a way of typing
    /// rather than a second way of meaning something. Where the highlight was left comes back
    /// with it: walking a colour along changes the palette, which builds the screen again, and
    /// the cursor has to still be on the slot that is being changed.
    let asking (view: View) said screen at =
        let rec steer standing =
            let showing, index = Keys.facing standing
            cleared ()
            printf "%s" (view.Says(Keys.draw (Some index) showing))

            if said <> "" then printfn "%s" (view.Says said)

            printf "> %s" standing.Buffer

            match Keys.answer (Keys.pressed (Keys.typing standing) (System.Console.ReadKey true)) standing with
            | Keys.Steering next -> steer next
            | Keys.Answered line -> Some line, Keys.started standing

        if steering () then
            steer (Keys.standing screen at)
        else
            printf "%s" (view.Says(Keys.draw None screen))

            if said <> "" then printfn "%s" (view.Says said)

            printf "> "

            match System.Console.ReadLine() with
            | null -> None, at
            | line -> Some line, at
