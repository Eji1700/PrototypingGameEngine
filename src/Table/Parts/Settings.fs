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
/// What was settled at one game.
///
/// Never seen outside the type below, whose only field holding one is private. It is a record
/// rather than the tuple it started as because there are three things in it now, two of them
/// optional strings, and a pair of those in a tuple is a pair nobody can read.
[<NoComparison; NoEquality>]
type Kept =
    { /// The view this game opens in.
      Drawn: string option
      /// Which of this game's ways of being played was settled on, where it has more than
      /// one. A name, and this file does not check it against anything - which of them there
      /// are is the game's own answer, and the game may not even be in this build.
      Plays: string option
      /// The lines the colour screen would take to put its colours where they are.
      Colours: string list }

[<NoComparison; NoEquality>]
type Settings =
    private
        {
            /// The view every game opens in, where one has been settled on.
            Drawn: string option
            /// Whether a terminal rings when the turn comes round and nobody asked for it.
            ///
            /// Above the games rather than under each of them, because whether somebody wants
            /// a beep is a fact about the room they are sitting in and not about which game is
            /// on the table. `None` is nobody having said, which rings - that is what every
            /// table did before there was a way to say otherwise.
            Bell: bool option
            /// Per game, in the order the file said them.
            ///
            /// An association list rather than a map, and that is the whole reason for it:
            /// a file somebody has read and edited comes back out in the order they left it,
            /// rather than reshuffled into whatever order a map felt like.
            Games: (string * Kept) list
        }

module Settings =

    /// Nothing settled at one game.
    let private nothing =
        { Drawn = None
          Plays = None
          Colours = [] }

    /// Nothing settled on: what a program with no settings file has, and what every game
    /// therefore opens exactly as it always did.
    let none =
        { Drawn = None
          Bell = None
          Games = [] }

    [<Literal>]
    let private DrawnWord = "view"

    /// Said above the games, and read at the Audio screen.
    [<Literal>]
    let private BellWord = "bell"

    /// Said under a game, and read at that game's own screen.
    [<Literal>]
    let private PlaysWord = "plays"

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
        |> Option.bind (fun kept -> kept.Drawn)
        |> Option.orElse settings.Drawn

    /// Which of this game's ways of being played was settled on. `None` where nothing was, and
    /// then the game is played the way it is played when nobody has said - which is the first
    /// way it offers.
    ///
    /// Not checked against the game here, for the reason the view is not: which ways there are
    /// is the game's own answer, and a name that no longer matches one of them is a line worth
    /// a sentence and no reason to refuse anybody a game.
    let plays name settings =
        forGame name settings |> Option.bind (fun kept -> kept.Plays)

    /// Whether a terminal rings when the turn comes round unasked.
    ///
    /// Nobody having said means it rings, which is what every table did before there was a way
    /// to say otherwise - a setting nobody has touched should not change what the program does.
    let bell settings = settings.Bell |> Option.defaultValue true

    /// The palette a game opens in: whatever it says its slots start out as, with the lines
    /// that were kept for it laid over the top.
    ///
    /// What was wrong comes back beside it rather than being swallowed, and the palette is
    /// whole either way. A line naming a colour that has since been dropped from a colours
    /// file, or a slot from a game that has since been rewritten, is a line worth a sentence
    /// on a screen - and no reason at all to refuse somebody a board.
    let palette name slots settings =
        forGame name settings
        |> Option.map (fun kept -> kept.Colours)
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
    /// One game's own answers, changed and put back where they were.
    ///
    /// Every page of the settings screen keeps a different part of them, and none of them may
    /// tread on the rest: keeping colours from the Video page must not forget which way of
    /// playing was settled at the Game page, and the only way to be sure of that is for none of
    /// them to write a whole `Kept` from nothing. So each says what it changes and this puts
    /// the change back over what was already there.
    ///
    /// A game already in the file keeps its place in it; a new one goes on the end. Which is
    /// the same rule the colours file follows, and for the same reason: a file somebody keeps
    /// coming back to should not rearrange itself under them.
    let private changing name change settings =
        { settings with
            Games =
                if settings.Games |> List.exists (fun (said, _) -> said = name) then
                    settings.Games
                    |> List.map (fun (said, was) -> if said = name then said, change was else said, was)
                else
                    settings.Games @ [ (name, change nothing) ] }

    /// These colours and this view, kept for this game.
    ///
    /// The colours are written out of the palette rather than remembered as they were typed,
    /// so what is kept is where the colours actually stand - a slot walked through nineteen
    /// shades with the arrows leaves one line behind it and not nineteen.
    ///
    /// The view is written down twice - once for this game and once above all of them - and
    /// that is what makes it a *default* rather than a note about one game. Somebody who has
    /// settled on rich has settled on it for the games they have not opened yet as much as for
    /// this one; and a game they later settle differently keeps its own answer, because its own
    /// line is read first. The colours cannot be shared the same way and are not: a slot is a
    /// game's own, and there is no one list of them to keep.
    let keeping name view palette (settings: Settings) =
        let colours =
            Palette.slots palette
            |> List.map (fun slot -> $"{slot.Key} {(Palette.inSlot slot palette).Name}")

        { settings with Drawn = Some view }
        |> changing name (fun (kept: Kept) ->
            { kept with
                Drawn = Some view
                Colours = colours })

    /// This way of playing, kept for this game. Under the name of the game that offers the
    /// ways rather than the name of the way settled on, because the second of those is what
    /// this line *says* and a file that kept it under itself would answer a question with
    /// itself.
    let playing name way settings =
        settings |> changing name (fun kept -> { kept with Plays = Some way })

    /// And whether the bell rings, which is kept above all of them.
    let ringing on settings = { settings with Bell = Some on }

    // --- the file ---------------------------------------------------------------------------

    let private heading =
        [ "# What this program was left set to, and picks up again next time."
          "#"
          "# Every line here is one you could type at the screen it belongs to: 'view <name>'"
          "# and 'bell on' are what the settings pages take, and '<what> <colour>' is what the"
          "# Video page takes. So there is nothing in this file that cannot be said at a"
          "# screen, and nothing at a screen that cannot be written here."
          "#"
          "# A name in square brackets opens one game's own settings. Anything above the first"
          "# of them is said about every game at once."
          "#"
          "# Written by the 'save' row on the settings screen. Editing it by hand is fine -"
          "# it is read the same way either round." ]

    /// On and off, spelt the way somebody would say them rather than as `true` and `false`.
    /// This file is read by people and the screen it mirrors has an on and an off on it.
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
            let under name change =
                changing name change settings, into, problems

            match line.StartsWith "[", line.EndsWith "]" with
            | true, true ->
                let name = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant()

                if name = "" then
                    wrong "'[]' does not name a game."
                else
                    // Named rather than started afresh, so a file that opens the same game
                    // twice adds to it rather than throwing the first half away.
                    changing name id settings, Some name, problems
            | true, false
            | false, true -> wrong $"'{line}' opens a game's settings but does not close them. Say '[{line.Trim('[', ']')}]'."
            | false, false ->
                match words |> List.map (fun word -> word.ToLowerInvariant()), into with
                | [ word; name ], Some game when word = DrawnWord -> under game (fun kept -> { kept with Drawn = Some name })
                | [ word; name ], None when word = DrawnWord -> { settings with Drawn = Some name }, into, problems
                // Which way a game is played is a game's own answer, so it is only ever said
                // under one - there is no way of playing that every game has.
                | [ word; way ], Some game when word = PlaysWord -> under game (fun kept -> { kept with Plays = Some way })
                | [ word; _ ], None when word = PlaysWord ->
                    wrong $"'{line}' says how one game is played, and nothing above it says which. Put a '[<game>]' line over it."
                // And the bell is the other way round: it is nobody's game in particular, so it
                // is only ever said above all of them.
                | [ word; on ], None when word = BellWord ->
                    match on with
                    | "on" -> { settings with Bell = Some true }, into, problems
                    | "off" -> { settings with Bell = Some false }, into, problems
                    | _ -> wrong $"'{line}' is not '{BellWord} on' or '{BellWord} off'."
                | [ word; _ ], Some _ when word = BellWord ->
                    wrong $"'{line}' is said about every game at once, so it goes above the first '[<game>]' line rather than under one."
                | [ _; _ ], Some game -> under game (fun kept -> { kept with Colours = kept.Colours @ [ line.ToLowerInvariant() ] })
                | [ _; _ ], None ->
                    wrong $"'{line}' colours something, and nothing above it says which game. Put a '[<game>]' line over it."
                | _ -> wrong $"'{line}' is not '{DrawnWord} <name>', '{BellWord} on', '{PlaysWord} <name>' or '<what> <colour>'."

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
