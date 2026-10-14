namespace Prototyping.Table

open Prototyping.Common

/// What is on the terminal, so the same screen is not written over itself again. A value rather
/// than a line count because a count cannot answer "is this already there", and it lives beside
/// `cleared` because a terminal just wiped has nothing on it to be identical to.
[<NoComparison>]
type Drawn =
    {
        Lines: int
        Text: string

        /// The window the text was drawn into. A window made another size since has the text
        /// somewhere else on it, and drawing over it there leaves the old one showing through.
        Across: int
        Down: int
    }

module Screens =

    let nothing =
        { Lines = 0
          Text = ""
          Across = 0
          Down = 0 }

    let steering () = not System.Console.IsInputRedirected

    // The letters that steer, as this keyboard has them. Kept here rather than handed to every
    // `asking`, because they belong to the keyboard and not to any one screen - and the keyboard page
    // changes them while it is being steered by them.
    let mutable private letters = Keymap.standard

    let lettered keymap = letters <- keymap

    let lettering () = letters

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

        // A window made another size since the last drawing has that drawing somewhere else on it,
        // wrapped or scrolled, so there is nothing to draw over; it is wiped and drawn afresh.
        let resized = before.Lines > 0 && (width <> before.Across || room <> before.Down)

        if resized || (room > 0 && lines.Length >= room) then
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

        if not resized then
            for _ in lines.Length .. before.Lines - 1 do
                over.AppendLine(System.String(' ', width)) |> ignore

        printf "%s" (over.ToString())

        { Lines = lines.Length
          Text = text
          Across = width
          Down = room }

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

    /// Where a body of `length` lines is cut, if it is: how far it is scrolled once that is kept
    /// between its head and its foot, how far down its foot is, and how far a page goes. A page is
    /// the board seen between the two cut lines less two, so the last lines read are still there to
    /// read on from.
    let private reach room rest length scrolled =
        let spare = room - rest

        if length <= spare || spare < Worth then
            None
        else
            let deepest = length - (spare - 1)
            Some(min deepest (max 0 scrolled), deepest, max 1 (spare - 4))

    /// A body trimmed to what is left after everything that must be drawn, `scrolled` lines down.
    ///
    /// The rows, the note and the prompt are how somebody gets off a screen, so they are never what
    /// gets cut; a board is the part that is as long as it likes. What is cut says so, at whichever
    /// end it is cut, because a board that quietly stopped early would be a board somebody read the
    /// wrong answer off.
    let fitting room rest (says: string -> string) scrolled (body: string list) =
        match reach room rest (List.length body) scrolled with
        | None -> body
        | Some(scrolled, deepest, _) ->
            let above =
                if scrolled = 0 then [] else [ says $"""  ... {Counting.several "line" "lines" scrolled} above""" ]

            let below = scrolled < deepest
            let shown = room - rest - List.length above - (if below then 1 else 0)
            let kept = body |> List.skip scrolled |> List.truncate shown

            // At least two lines are cut below whenever any are: a foot one line out of sight is
            // in sight once the line saying so is not needed. So the count is never one.
            above
            @ kept
            @ [ if below then
                    says $"  ... {List.length body - scrolled - shown} more lines, and this screen is taller than the window" ]

    /// A screen to steer by its keys - unless input is coming from somewhere that has no keys to
    /// press, in which case the screen is printed once and a line is read.
    ///
    /// `above` is something already drawn - a board, in a view of its own choosing - and it is
    /// printed as it stands rather than handed to `says`. Painting a drawing that has already been
    /// painted throws it away: what a rich board is made of is escapes rather than markup, and the
    /// second pass eats them. A menu has nothing drawn for it and passes "".
    let askingOver (says: string -> string) said (above: string) (board: Keys.Steered) at =
        let lines (text: string) =
            text.Replace("\r\n", "\n").Split '\n' |> Array.toList

        // Answers with the frame, how far down the board it was drawn and how far a page goes, both
        // nought for a board that fits: only the frame knows the room, and the scroll is kept as
        // drawn so that a press past either end is not one more to press back.
        let frame standing index scrolled =
            let showing, _ = Keys.facing standing

            let tail cut =
                [ yield! lines (says (Keys.draw (across ()) index showing))

                  if said <> "" then yield! lines (says said)

                  // Whose the keyboard is, said where somebody is about to press a key. A mode with
                  // no sign of itself is a mode people press keys into and wonder at.
                  if steering () then
                      let shown = Keys.prompt (lettering ()) cut standing
                      let hints, typed = List.splitAt (List.length shown - 1) shown

                      yield! List.map says hints
                      yield! typed
                  else
                      yield "> " ]

            let body = if above = "" then [] else lines above
            let room = room ()

            // Naming the scroll keys or not changes a word on the hint and not how many lines it is.
            let plain = tail false
            let rest = List.length plain
            let reached = reach room rest (List.length body) scrolled

            let text =
                String.concat
                    System.Environment.NewLine
                    (fitting room rest says scrolled body
                     @ if Option.isSome reached then tail true else plain)

            match reached with
            | Some(scrolled, _, page) -> text, scrolled, page
            | None -> text, 0, 0

        // The scroll is held here and nowhere else: whatever answers a line is drawn afresh, and may
        // be another page, so it is read from its head again.
        let rec steer drawn standing scrolled =
            let _, index = Keys.facing standing
            let text, scrolled, page = frame standing (Some index) scrolled

            // Drawn over rather than cleared and written again. Clearing first and then writing more
            // lines than the window holds is what made a tall screen arrive in pieces: the terminal
            // scrolled what had just been cleared, and the eye caught the halves.
            let drawn = redrawn drawn text

            match Keys.answer (lettering ()) (System.Console.ReadKey true) standing with
            | Keys.Steering next -> steer drawn next scrolled
            | Keys.Scrolling(next, Keys.Windowful by) -> steer drawn next (scrolled + by * page)
            | Keys.Scrolling(next, Keys.Head) -> steer drawn next 0
            | Keys.Scrolling(next, Keys.Foot) -> steer drawn next System.Int32.MaxValue
            | Keys.Answered line -> Some line, Keys.path standing

        if steering () then
            // Cleared once, and drawn over from then on. The first frame has whatever was on the
            // terminal before it under it; every frame after is written over the last.
            cleared ()
            steer nothing (Keys.standing board at) 0
        else
            let text, _, _ = frame (Keys.standing board at) None 0
            printf "%s" text

            match System.Console.ReadLine() with
            | null -> None, at
            | line -> Some line, at

    /// The same, for a screen that is the whole of what is on the terminal - which is every menu.
    let asking says said screen at =
        askingOver says said "" (Keys.steered screen) at
