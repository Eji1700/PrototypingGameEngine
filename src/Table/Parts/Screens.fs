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

    /// The narrowest a window is taken to be - a terminal mid-resize can answer nought, and twenty
    /// columns is the least a row's marker, digit and name can be laid out in - and what a
    /// redirected console is written to fit, having no window to ask about.
    [<Literal>]
    let private Narrowest = 20

    [<Literal>]
    let private Usual = 80

    let private across () =
        try
            max Narrowest (System.Console.WindowWidth - 1)
        with _ ->
            Usual

    /// Draw over what is already on the screen rather than clearing it first, which is what keeps a
    /// board redrawn several times a second from flickering. Every line is padded to the width and
    /// any line the last drawing used and this one does not is blanked, so nothing is left behind.
    /// A screen identical to the one already there is not written again. Answers with what is now
    /// on the terminal, to be passed back in next time - after `cleared`, that is `nothing`.
    let redrawn (before: Drawn) (text: string) =
        if before.Text = text then
            before
        else

        let width = across ()

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

    /// How long to wait between looks at the keyboard: short enough that a key is never felt to
    /// lag, long enough that a table waiting on its next beat is not spinning.
    [<Literal>]
    let private Glance = 8

    let awaiting (until: System.DateTime) =
        let rec waiting () =
            if System.Console.KeyAvailable then
                Some(System.Console.ReadKey true)
            elif System.DateTime.UtcNow >= until then
                None
            else
                System.Threading.Thread.Sleep Glance
                waiting ()

        waiting ()

    /// The fewest lines a window is taken to have. A terminal that answers with fewer - or with
    /// none, mid-resize - is given eight, which is too few to cut a board to, so the board is drawn
    /// whole rather than cut to nothing.
    [<Literal>]
    let private Shortest = 8

    /// How many lines there are to draw into, or no limit at all where there is no window to ask
    /// about - which is every redirected console, and nothing reading one is looking at edges.
    let private room () =
        try
            max Shortest (System.Console.WindowHeight - 1)
        with _ ->
            System.Int32.MaxValue

    /// The fewest spare lines worth cutting a board to: one for the line that says it was cut, and
    /// two of the board itself.
    [<Literal>]
    let private Worth = 3

    /// A body trimmed to what is left after everything that must be drawn.
    ///
    /// The rows, the note and the prompt are how somebody gets off a screen, so they are never what
    /// gets cut; a board is the part that is as long as it likes. What is cut says so, because a
    /// board that quietly stopped early would be a board somebody read the wrong answer off.
    let fitting room rest (says: string -> string) (body: string list) =
        let spare = room - rest

        if List.length body <= spare || spare < Worth then
            body
        else
            let kept = List.truncate (spare - 1) body

            // At least two lines are cut whenever any are: the body ran past the spare lines, and
            // one of those went to say so. So the count is never one, and never reads wrong.
            kept
            @ [ says $"  ... {List.length body - List.length kept} more lines, and this screen is taller than the window" ]

    /// A screen to steer with the arrows - unless input is coming from somewhere that has no keys to
    /// press, in which case the screen is printed once and a line is read.
    ///
    /// `above` is something already drawn - a board, in a view of its own choosing - and it is
    /// printed as it stands rather than handed to `says`. Painting a drawing that has already been
    /// painted throws it away: what a rich board is made of is escapes rather than markup, and the
    /// second pass eats them. A menu has nothing drawn for it and passes "".
    let askingOver (says: string -> string) said (above: string) screen at =
        let lines (text: string) =
            text.Replace("\r\n", "\n").Split '\n' |> Array.toList

        let frame standing index =
            let showing, _ = Keys.facing standing

            let tail =
                [ yield! lines (says (Keys.draw (across ()) index showing))

                  if said <> "" then yield! lines (says said)

                  // Whose the keyboard is, said where somebody is about to press a key. A mode with
                  // no sign of itself is a mode people press keys into and wonder at.
                  if Keys.typing standing then
                      yield $"> {standing.Buffer}_"
                  elif steering () then
                      yield says "  (space to type a line)"
                      yield "> "
                  else
                      yield "> " ]

            let body = if above = "" then [] else lines above

            String.concat System.Environment.NewLine (fitting (room ()) (List.length tail) says body @ tail)

        let rec steer drawn standing =
            let _, index = Keys.facing standing

            // Drawn over rather than cleared and written again. Clearing first and then writing more
            // lines than the window holds is what made a tall screen arrive in pieces: the terminal
            // scrolled what had just been cleared, and the eye caught the halves.
            let drawn = redrawn drawn (frame standing (Some index))

            match Keys.answer (Keys.pressed (Keys.typing standing) (System.Console.ReadKey true)) standing with
            | Keys.Steering next -> steer drawn next
            | Keys.Answered line -> Some line, Keys.path standing

        if steering () then
            // Cleared once, and drawn over from then on. The first frame has whatever was on the
            // terminal before it under it; every frame after is written over the last.
            cleared ()
            steer nothing (Keys.standing screen at)
        else
            printf "%s" (frame (Keys.standing screen at) None)

            match System.Console.ReadLine() with
            | null -> None, at
            | line -> Some line, at

    /// The same, for a screen that is the whole of what is on the terminal - which is every menu.
    let asking says said screen at = askingOver says said "" screen at
