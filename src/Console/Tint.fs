namespace TCModel.Console

open System.IO
open System.Text.RegularExpressions
open Spectre.Console
open Spectre.Console.Rendering
open TCModel.Domain

/// Colour, and the machinery for getting Spectre's writing back out as text.
///
/// Two things live here because both views want them. `paint` lays colour over a board
/// that `Render` has already drawn - and it is drawn by counting characters into columns,
/// so nothing may be coloured until every column is where it belongs. What happens here
/// cannot move a single character: it only wraps some of them.
///
/// `render` is the other half: Spectre writes to a console, and this one writes into a
/// string, so what comes back can be printed, sent down a wire, or written to a file like
/// any other text. `Rich` builds a board out of Spectre's own widgets and comes through
/// here to turn it into something a game can hand about.
module Tint =

    // --- the palette, which is the whole of what is decided here ----------------------

    /// Eight-bit colours rather than the sixteen named ones, and each a good deal brighter
    /// than the word for it: a stone drawn in the flat version of its own colour is hard to
    /// pick out of a map, and on a dark screen a dark blue is barely there at all.
    [<Literal>]
    let private RedInk = "red1"

    [<Literal>]
    let private BlueInk = "dodgerblue1"

    [<Literal>]
    let private GreenInk = "green3"

    [<Literal>]
    let private HeadingInk = "bold"

    /// The reader's own seat, and the arrow marking whoever is to play. Warm, so that it
    /// stands apart from all three factions - it sits right beside a bag full of them.
    [<Literal>]
    let Yours = "gold1"

    /// Ground nobody may enter, and stones nobody can see.
    [<Literal>]
    let Hidden = "grey37"

    /// A colour as markup says it.
    let ink =
        function
        | Red -> RedInk
        | Blue -> BlueInk
        | Green -> GreenInk

    /// The same colour as Spectre's own, for the widgets that take one rather than a name.
    let color =
        function
        | Red -> Color.Red1
        | Blue -> Color.DodgerBlue1
        | Green -> Color.Green3

    /// A colour by its first letter, which serves for a stone's glyph and for its name
    /// alike: the three are written R, B and G and called Red, Blue and Green, so one
    /// letter tells them apart wherever they are written.
    let private inkOfLetter letter =
        match letter with
        | 'R' -> RedInk
        | 'B' -> BlueInk
        | _ -> GreenInk

    let wrap style (text: string) = $"[{style}]{text}[/]"

    // --- what gets which ---------------------------------------------------------------

    /// One pass, with the alternatives in the order they should win, so nothing that has
    /// already been painted can be matched again by a later rule.
    let private marks =
        [ // A screen's own title line, and the block titles under it.
          @"(?<heading>^={3}.*?={3}(?=\r?$))"
          @"(?<block>^[A-Z][A-Z ]*[A-Z](?=\r?$))"
          @"(?<you>\(you\))"
          @"(?<active>->)"
          @"(?<dead>\bdead\b)"
          // Colours named in prose: "Red rules the region", "2 Green".
          @"(?<named>\b(?:Red|Blue|Green)\b)"
          // A tally, as "Rx4".
          @"(?<tally>\b[RBG]x[0-9]+)"
          // A home's own colour, as "(R)".
          @"(?<home>\([RBG]\))"
          // Who rules a region, as ">R" - the one thing on the map worth spotting from
          // across the room - and who is level in it, as "=BG".
          @"(?<rules>>[RBG])"
          @"(?<tied>=[RBG]+)"
          // A stone standing on the map, on its own.
          @"(?<glyph>(?<![A-Za-z0-9])[RBG](?![A-Za-z0-9]))" ]
        |> String.concat "|"

    let private rules = Regex(marks, RegexOptions.Multiline ||| RegexOptions.Compiled)

    /// Colour every letter of a run one by one, leaving anything else as it was - so
    /// "=BG" keeps its sign and each colour level in the region keeps its own.
    let private letterByLetter (text: string) =
        text
        |> Seq.map (fun c ->
            if c = 'R' || c = 'B' || c = 'G' then
                wrap (inkOfLetter c) (string c)
            else
                string c)
        |> String.concat ""

    let private mark (found: Match) =
        let matched (name: string) = found.Groups[name].Success

        if matched "heading" || matched "block" then wrap HeadingInk found.Value
        elif matched "you" || matched "active" then wrap Yours found.Value
        elif matched "dead" then wrap Hidden found.Value
        // "Green", "Gx4" and a lone "G" all begin with the letter that names the colour,
        // so all three go the same way.
        elif matched "named" || matched "tally" || matched "glyph" then
            wrap (inkOfLetter found.Value[0]) found.Value
        elif matched "rules" then wrap $"bold {inkOfLetter found.Value[1]}" found.Value
        else letterByLetter found.Value

    /// Plain text in, Spectre markup out. The escaping has to come first: the board is
    /// full of square brackets - every region is numbered "[ 5]" - and to Spectre a
    /// square bracket opens a colour.
    let markup (text: string) = rules.Replace(Markup.Escape text, mark)

    // --- putting it through Spectre -----------------------------------------------------

    /// Spectre folds anything wider than the console it thinks it is writing to. Nothing
    /// here knows what console that will be - the text may be going down a wire to one
    /// nobody here can ask about - so the width is said outright and the wrapping is left
    /// to whatever the text finally lands in.
    let renderAt width (what: IRenderable) =
        use writer = new StringWriter()

        let console =
            AnsiConsole.Create(
                AnsiConsoleSettings(
                    Ansi = AnsiSupport.Yes,
                    ColorSystem = ColorSystemSupport.EightBit,
                    Out = AnsiConsoleOutput(writer)
                )
            )

        console.Profile.Width <- width
        console.Write what
        writer.ToString()

    /// Plain text in, the same text in colour out.
    let paint (text: string) =
        try
            renderAt 1000 (Markup(markup text))
        with _ ->
            // Colour is decoration. If it cannot be had the game is still perfectly
            // readable without it, and losing a turn to a bad escape would not be.
            text
