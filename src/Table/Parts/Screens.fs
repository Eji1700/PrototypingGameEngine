namespace TCModel.Table

module Screens =

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
                | "" -> "TCModel"
                | title -> title
             with _ ->
                 "TCModel")

    let marking wanted =
        try
            System.Console.Title <- if wanted then $"* {wasCalled.Value}" else wasCalled.Value
        with _ ->
            ()

    let held () =
        if steering () then
            printf "Press any key."
            System.Console.ReadKey true |> ignore

    let redrawn (before: int) (text: string) =
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

        for _ in lines.Length .. before - 1 do
            over.AppendLine(System.String(' ', width)) |> ignore

        printf "%s" (over.ToString())
        lines.Length

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
