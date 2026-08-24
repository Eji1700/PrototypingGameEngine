namespace Prototyping.Table

/// What is on the terminal, so the same screen is not written over itself again. A value rather
/// than a line count because a count cannot answer "is this already there", and it lives beside
/// `cleared` because a terminal just wiped has nothing on it to be identical to.
[<NoComparison>]
type Drawn = { Lines: int; Text: string }

module Screens =

    let nothing = { Lines = 0; Text = "" }

    let steering () = not System.Console.IsInputRedirected

    let cleared () =
        try
            System.Console.Clear()
        with _ ->
            ()

    let private wasCalled =
        lazy
            (try
                match System.Console.Title with
                | "" -> "Proto"
                | title -> title
             with _ ->
                 "Proto")

    let marking wanted =
        try
            System.Console.Title <- if wanted then $"* {wasCalled.Value}" else wasCalled.Value
        with _ ->
            ()

    let held () =
        if steering () then
            printf "Press any key."
            System.Console.ReadKey true |> ignore

    /// Draw over what is already on the screen rather than clearing it first, which is what keeps a
    /// board redrawn several times a second from flickering. Every line is padded to the width and
    /// any line the last drawing used and this one does not is blanked, so nothing is left behind.
    /// A screen identical to the one already there is not written again. Answers with what is now
    /// on the terminal, to be passed back in next time - after `cleared`, that is `nothing`.
    let redrawn (before: Drawn) (text: string) =
        if before.Text = text then
            before
        else

        let width =
            try
                max 20 (System.Console.WindowWidth - 1)
            with _ ->
                80

        let lines = text.Replace("\r\n", "\n").Split '\n'

        let room =
            try
                System.Console.WindowHeight
            with _ ->
                0

        if room > 0 && lines.Length >= room then
            cleared ()
        else
            try
                System.Console.SetCursorPosition(0, 0)
            with _ ->
                ()

        let over = System.Text.StringBuilder()

        for line in lines do
            over.AppendLine(if line.Length >= width then line else line.PadRight width)
            |> ignore

        for _ in lines.Length .. before.Lines - 1 do
            over.AppendLine(System.String(' ', width)) |> ignore

        printf "%s" (over.ToString())

        { Lines = lines.Length; Text = text }

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

    /// A screen to steer with the arrows - unless input is coming from somewhere that has no keys to
    /// press, in which case the screen is printed once and a line is read.
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
