namespace TCModel.Table

open System
open System.IO

/// What somebody has settled on, kept between sittings: how a board is drawn, and what is
/// drawn in what colour.
///
/// None of it is part of any game. It is how a game is *read* - the same things that already
/// stay out of the record and out of the wire, so that two people at one networked table can
/// read one board in colours that have nothing to do with each other. All that is added here
/// is that putting the program down no longer forgets them.
///
/// **There is no second language.** Every line in the file is a line somebody could have
/// typed at the screen it belongs to: `view rich` is what the menu takes, and `red crimson`
/// is what the colour screen takes, read back by those very readers. So a settings file
/// cannot come to hold something no screen can express, a screen cannot come to offer
/// something no file can hold, and what a line means is settled in one place rather than two.
/// The same bargain the record keeps, one level out - a record is the moves a player typed,
/// and this is the answers they gave.
///
/// The colours are per game and the view may be, because a game's slots are its own: a game
/// of stones colours three factions and a game of nine squares colours two marks, and there
/// is no one list to keep them in. What every game shares - which way of drawing is wanted
/// first - is said once above them all, and a game may still say otherwise for itself.
[<NoComparison; NoEquality>]
type Settings =
    private
        {
            /// The view every game opens in, where one has been settled on.
            Drawn: string option
            /// Per game, in the order the file said them: the view that game opens in, and
            /// the lines the colour screen would take to put its colours where they are.
            ///
            /// An association list rather than a map, and that is the whole reason for it:
            /// a file somebody has read and edited comes back out in the order they left it,
            /// rather than reshuffled into whatever order a map felt like.
            Games: (string * (string option * string list)) list
        }

module Settings =

    /// Nothing settled on: what a program with no settings file has, and what every game
    /// therefore opens exactly as it always did.
    let none = { Drawn = None; Games = [] }

    [<Literal>]
    let private DrawnWord = "view"

    let private forGame name settings =
        settings.Games
        |> List.tryFind (fun (said, _) -> said = name)
        |> Option.map snd

    // --- what a game opens with ------------------------------------------------------------

    /// The view a game opens in: its own if it said one, and otherwise the one said above all
    /// of them. `None` where neither, and then the game opens the way it always did.
    ///
    /// Not checked against the game here, because this file has no game to check it against -
    /// which of the views a game offers is a question only the game can answer, and it is
    /// asked where the view is actually built.
    let drawn name settings =
        forGame name settings
        |> Option.bind fst
        |> Option.orElse settings.Drawn

    /// The palette a game opens in: whatever it says its slots start out as, with the lines
    /// that were kept for it laid over the top.
    ///
    /// What was wrong comes back beside it rather than being swallowed, and the palette is
    /// whole either way. A line naming a colour that has since been dropped from a colours
    /// file, or a slot from a game that has since been rewritten, is a line worth a sentence
    /// on a screen - and no reason at all to refuse somebody a board.
    let palette name slots settings =
        forGame name settings
        |> Option.map snd
        |> Option.defaultValue []
        |> List.fold
            (fun (palette, problems) (line: string) ->
                match line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray with
                | [ slot; colour ] ->
                    match Palette.set slot colour palette with
                    | Ok palette -> palette, problems
                    | Error problem -> palette, problems @ [ $"[{name}] {problem}" ]
                | _ -> palette, problems @ [ $"[{name}] '{line.Trim()}' is not '<what> <colour>'." ])
            (Palette.standard slots, [])

    // --- and what is settled on -------------------------------------------------------------

    /// These colours and this view, kept for this game.
    ///
    /// The colours are written out of the palette rather than remembered as they were typed,
    /// so what is kept is where the colours actually stand - a slot walked through nineteen
    /// shades with the arrows leaves one line behind it and not nineteen.
    ///
    /// A game already in the file keeps its place in it; a new one goes on the end. Which is
    /// the same rule the colours file follows, and for the same reason: a file somebody keeps
    /// coming back to should not rearrange itself under them.
    ///
    /// The view is written down twice - once for this game and once above all of them - and
    /// that is what makes it a *default* rather than a note about one game. Somebody who has
    /// settled on rich has settled on it for the games they have not opened yet as much as for
    /// this one; and a game they later settle differently keeps its own answer, because its own
    /// line is read first. The colours cannot be shared the same way and are not: a slot is a
    /// game's own, and there is no one list of them to keep.
    let keeping name view palette settings =
        let kept =
            Some view,
            (Palette.slots palette
             |> List.map (fun slot -> $"{slot.Key} {(Palette.inSlot slot palette).Name}"))

        { Drawn = Some view
          Games =
            if settings.Games |> List.exists (fun (said, _) -> said = name) then
                settings.Games
                |> List.map (fun (said, was) -> if said = name then said, kept else said, was)
            else
                settings.Games @ [ (name, kept) ] }

    // --- the file ---------------------------------------------------------------------------

    let private heading =
        [ "# What this program was left set to, and picks up again next time."
          "#"
          "# Every line here is one you could type at the screen it belongs to: 'view <name>'"
          "# is what the menu takes, and '<what> <colour>' is what the colour screen takes."
          "# So there is nothing in this file that cannot be said at a screen, and nothing at"
          "# a screen that cannot be written here."
          "#"
          "# A name in square brackets opens one game's own settings. Anything above the first"
          "# of them is said about every game at once."
          "#"
          "# Written by the 'save' row on the settings screen. Editing it by hand is fine -"
          "# it is read the same way either round." ]

    let write settings =
        let colours name =
            match forGame name settings with
            | None -> []
            | Some(view, lines) -> (view |> Option.toList |> List.map (fun said -> $"{DrawnWord} {said}")) @ lines

        String.concat
            Environment.NewLine
            (heading
             @ [ "" ]
             @ (settings.Drawn |> Option.toList |> List.map (fun said -> $"{DrawnWord} {said}"))
             @ (settings.Games
                |> List.collect (fun (name, _) -> [ ""; $"[{name}]" ] @ colours name))
             @ [ "" ])

    /// Read one back: what was settled on, and what in the file could not be made sense of.
    ///
    /// Nothing here refuses. A settings file is how somebody likes to read a game, and a line
    /// of it that has gone stale - a colour since dropped, a game since renamed - is worth
    /// saying something about and worth nothing at all to stop on. What it cannot make sense
    /// of it says so about and leaves out, and the rest still stands.
    let read (text: string) =
        let meaningful =
            text.Split '\n'
            |> Array.map (fun line ->
                match line.IndexOf '#' with
                | -1 -> line.Trim()
                | at -> line.Substring(0, at).Trim())
            |> Array.filter (fun line -> line <> "")
            |> List.ofArray

        // Which game's settings the lines are landing in, as the file is walked down. `None`
        // is above the first heading, which is what is said about every game at once.
        let folding (settings, into, problems) (line: string) =
            let wrong said = settings, into, problems @ [ said ]

            let words =
                line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

            /// One line put where the heading above it says. A game's lines are kept as they
            /// were written rather than read here, because what a slot is called is the game's
            /// own answer and the game whose settings these are may not even be in this build.
            let under name said =
                let was = forGame name settings |> Option.defaultValue (None, [])

                let now =
                    match said with
                    | Choice1Of2 view -> Some view, snd was
                    | Choice2Of2 colour -> fst was, snd was @ [ colour ]

                { settings with
                    Games =
                        if settings.Games |> List.exists (fun (had, _) -> had = name) then
                            settings.Games
                            |> List.map (fun (had, before) -> if had = name then had, now else had, before)
                        else
                            settings.Games @ [ (name, now) ] },
                into,
                problems

            match line.StartsWith "[", line.EndsWith "]" with
            | true, true ->
                let name = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant()

                if name = "" then
                    wrong "'[]' does not name a game."
                else
                    // Named rather than started afresh, so a file that opens the same game
                    // twice adds to it rather than throwing the first half away.
                    let settings =
                        if settings.Games |> List.exists (fun (had, _) -> had = name) then
                            settings
                        else
                            { settings with
                                Games = settings.Games @ [ (name, (None, [])) ] }

                    settings, Some name, problems
            | true, false
            | false, true -> wrong $"'{line}' opens a game's settings but does not close them. Say '[{line.Trim('[', ']')}]'."
            | false, false ->
                match words, into with
                | [ word; said ], _ when word.ToLowerInvariant() = DrawnWord ->
                    match into with
                    | Some name -> under name (Choice1Of2(said.ToLowerInvariant()))
                    | None ->
                        { settings with
                            Drawn = Some(said.ToLowerInvariant()) },
                        into,
                        problems
                | [ _; _ ], Some name -> under name (Choice2Of2(line.ToLowerInvariant()))
                | [ _; _ ], None ->
                    wrong $"'{line}' colours something, and nothing above it says which game. Put a '[<game>]' line over it."
                | _ -> wrong $"'{line}' is not '{DrawnWord} <name>' or '<what> <colour>'."

        let settings, _, problems = meaningful |> List.fold folding (none, None, [])
        settings, problems

    /// Where the settings are kept, beside the records.
    let source =
        Path.Combine(Directory.GetCurrentDirectory(), "settings.txt")

    /// What is on disk, and what was wrong with it. No file at all is not a complaint: it is
    /// the ordinary case, and what it means is that nothing has been settled on yet.
    ///
    /// Read every time it is asked for rather than once, unlike the colours file. The two are
    /// different in the one way that matters: colours are what the program was *built* able to
    /// draw, and these are what somebody has just this moment chosen - a settings file kept in
    /// hand would be a file this program overwrote with a stale copy of itself the moment two
    /// of it were open at once.
    let load () =
        try
            if File.Exists source then read (File.ReadAllText source) else none, []
        with problem ->
            none, [ $"The settings could not be read: {problem.Message}" ]

    /// Put them down, and say where they went - or why they did not go anywhere.
    ///
    /// Written beside itself and moved over the top, for the reason the record gives for the
    /// same dance: writing straight to a file is not one step, and a settings file cut off
    /// halfway is not an unreadable file that says so - it is a shorter one that reads
    /// perfectly well, with somebody's colours quietly gone.
    let save settings =
        try
            let beside = source + ".writing"
            File.WriteAllText(beside, write settings)
            File.Move(beside, source, true)
            Ok(Path.GetRelativePath(Directory.GetCurrentDirectory(), source))
        with problem ->
            Error $"The settings could not be kept: {problem.Message}"
