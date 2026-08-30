namespace Prototyping.Table

open System
open System.IO

[<NoComparison; NoEquality>]
type Kept =
    { Drawn: string option
      Plays: string option
      Colours: string list }

[<NoComparison; NoEquality>]
type Settings =
    private
        {
          // The same name as `Kept.Drawn` above, and F# gives an un-annotated `{ Drawn = ... }`
          // to whichever record was declared last - so every site that builds either says which.
          Drawn: string option

          Bell: bool option
          Games: (string * Kept) list }

module Settings =

    let private nothing =
        { Drawn = None
          Plays = None
          Colours = [] }

    let none =
        { Drawn = None
          Bell = None
          Games = [] }

    [<Literal>]
    let private DrawnWord = "view"

    [<Literal>]
    let private BellWord = "bell"

    [<Literal>]
    let private PlaysWord = "plays"

    let private forGame name settings =
        settings.Games |> List.tryFind (fun (said, _) -> said = name) |> Option.map snd


    let drawn name settings =
        forGame name settings
        |> Option.bind (fun kept -> kept.Drawn)
        |> Option.orElse settings.Drawn

    let plays name settings =
        forGame name settings |> Option.bind (fun kept -> kept.Plays)

    let bell settings =
        settings.Bell |> Option.defaultValue true

    let palette name slots settings =
        forGame name settings
        |> Option.map (fun kept -> kept.Colours)
        |> Option.defaultValue []
        |> List.fold
            (fun (palette, problems) (line: string) ->
                match
                    line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
                    |> List.ofArray
                with
                | [ slot; colour ] ->
                    match Palette.set slot colour palette with
                    | Ok palette -> palette, problems
                    | Error problem -> palette, problems @ [ $"[{name}] {problem}" ]
                | _ -> palette, problems @ [ $"[{name}] '{line.Trim()}' is not '<what> <colour>'." ])
            (Palette.standard slots, [])


    let private changing name change settings =
        { settings with
            Games =
                if settings.Games |> List.exists (fun (said, _) -> said = name) then
                    settings.Games
                    |> List.map (fun (said, was) -> if said = name then said, change was else said, was)
                else
                    settings.Games @ [ (name, change nothing) ] }

    let keeping name view palette (settings: Settings) =
        let colours =
            Palette.slots palette
            |> List.map (fun slot -> $"{slot.Key} {(Palette.inSlot slot palette).Name}")

        { settings with Drawn = Some view }
        |> changing name (fun (kept: Kept) ->
            { kept with
                Drawn = Some view
                Colours = colours })

    let playing name way settings =
        settings |> changing name (fun kept -> { kept with Plays = Some way })

    let ringing on settings = { settings with Bell = Some on }


    let private heading =
        [ "# What this program was left set to, and picks up again next time."
          "#"
          "# Every line here is one you could type at the screen it belongs to: 'view <name>'"
          "# and 'bell on' are what the settings pages take, '<what> <colour>' is what the Video"
          "# page takes and 'plays <name>' what the Game page takes. So there is nothing in this"
          "# file that cannot be said at a screen, and nothing at a screen that cannot be written"
          "# here."
          "#"
          "# A name in square brackets opens one game's own settings. Anything above the first"
          "# of them is said about every game at once."
          "#"
          "# Written by the 'save' row on the settings screen. Editing it by hand is fine -"
          "# it is read the same way either round." ]

    let private said on = if on then "on" else "off"

    let write settings =
        let under name =
            match forGame name settings with
            | None -> []
            | Some kept ->
                (kept.Drawn |> Option.toList |> List.map (fun said -> $"{DrawnWord} {said}"))
                @ (kept.Plays |> Option.toList |> List.map (fun way -> $"{PlaysWord} {way}"))
                @ kept.Colours

        String.concat
            Environment.NewLine
            (heading
             @ [ "" ]
             @ (settings.Drawn |> Option.toList |> List.map (fun name -> $"{DrawnWord} {name}"))
             @ (settings.Bell |> Option.toList |> List.map (fun on -> $"{BellWord} {said on}"))
             @ (settings.Games
                |> List.collect (fun (name, _) -> [ ""; $"[{name}]" ] @ under name))
             @ [ "" ])

    let read (text: string) =
        // A file written by another program can open with a byte order mark; left in, it is the
        // first letter of the first line, and 'view rich' or '[compile]' is no longer read as itself.
        let meaningful =
            text.TrimStart('\uFEFF').Split '\n'
            |> Array.map (fun line ->
                match line.IndexOf '#' with
                | -1 -> line.Trim()
                | at -> line.Substring(0, at).Trim())
            |> Array.filter (fun line -> line <> "")
            |> List.ofArray

        let folding (settings, into, problems) (line: string) =
            let wrong said = settings, into, problems @ [ said ]

            let words =
                line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
                |> List.ofArray

            let under name change =
                changing name change settings, into, problems

            match line.StartsWith "[", line.EndsWith "]" with
            | true, true ->
                let name = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant()

                if name = "" then
                    wrong "'[]' does not name a game."
                else
                    changing name id settings, Some name, problems
            | true, false
            | false, true -> wrong $"'{line}' opens a game's settings but does not close them. Say '[{line.Trim('[', ']')}]'."
            | false, false ->
                match words |> List.map (fun word -> word.ToLowerInvariant()), into with
                | [ word; name ], Some game when word = DrawnWord -> under game (fun kept -> { kept with Drawn = Some name })
                | [ word; name ], None when word = DrawnWord -> { settings with Drawn = Some name }, into, problems
                | [ word; way ], Some game when word = PlaysWord -> under game (fun kept -> { kept with Plays = Some way })
                | [ word; _ ], None when word = PlaysWord ->
                    wrong $"'{line}' says how one game is played, and nothing above it says which. Put a '[<game>]' line over it."
                | [ word; on ], None when word = BellWord ->
                    match on with
                    | "on" -> { settings with Bell = Some true }, into, problems
                    | "off" -> { settings with Bell = Some false }, into, problems
                    | _ -> wrong $"'{line}' is not '{BellWord} on' or '{BellWord} off'."
                | [ word; _ ], Some _ when word = BellWord ->
                    wrong
                        $"'{line}' is said about every game at once, so it goes above the first '[<game>]' line rather than under one."
                | [ _; _ ], Some game ->
                    under game (fun kept ->
                        { kept with
                            Colours = kept.Colours @ [ line.ToLowerInvariant() ] })
                | [ _; _ ], None ->
                    wrong $"'{line}' colours something, and nothing above it says which game. Put a '[<game>]' line over it."
                | _ -> wrong $"'{line}' is not '{DrawnWord} <name>', '{BellWord} on', '{PlaysWord} <name>' or '<what> <colour>'."

        let settings, _, problems = meaningful |> List.fold folding (none, None, [])
        settings, problems

    let private source = Path.Combine(Directory.GetCurrentDirectory(), "settings.txt")

    let load () =
        try
            if File.Exists source then read (File.ReadAllText source) else none, []
        with problem ->
            none, [ $"The settings could not be read: {problem.Message}" ]

    let save settings =
        try
            let beside = source + ".writing"
            File.WriteAllText(beside, write settings)
            File.Move(beside, source, true)
            Ok(Path.GetRelativePath(Directory.GetCurrentDirectory(), source))
        with problem ->
            Error $"The settings could not be kept: {problem.Message}"
