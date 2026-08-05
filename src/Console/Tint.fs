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

    // --- reading a palette ------------------------------------------------------------
    //
    // Which colour is drawn for what is `Palette`'s to say, and a player's to change. What
    // is left here is where each one is used, which is not.

    [<Literal>]
    let private HeadingInk = "bold"

    /// A faction's colour as markup says it.
    let ink palette color = Palette.ink (Palette.forColor color palette)

    /// The same colour as Spectre's own, for the widgets that take one rather than a name.
    let color palette faction = (Palette.forColor faction palette).Color

    /// The reader's own seat, and the arrow marking whoever is to play.
    let yours (palette: Palette) = Palette.ink palette.Yours

    /// Ground nobody may enter, and stones nobody can see.
    let hidden (palette: Palette) = Palette.ink palette.Hidden

    /// A colour by its first letter, which serves for a stone's glyph and for its name
    /// alike: the three are written R, B and G and called Red, Blue and Green, so one
    /// letter tells them apart wherever they are written.
    let private inkOfLetter palette letter =
        match letter with
        | 'R' -> ink palette Red
        | 'B' -> ink palette Blue
        | _ -> ink palette Green

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
    let private letterByLetter palette (text: string) =
        text
        |> Seq.map (fun c ->
            if c = 'R' || c = 'B' || c = 'G' then
                wrap (inkOfLetter palette c) (string c)
            else
                string c)
        |> String.concat ""

    let private mark palette (found: Match) =
        let matched (name: string) = found.Groups[name].Success

        if matched "heading" || matched "block" then wrap HeadingInk found.Value
        elif matched "you" || matched "active" then wrap (yours palette) found.Value
        elif matched "dead" then wrap (hidden palette) found.Value
        // "Green", "Gx4" and a lone "G" all begin with the letter that names the colour,
        // so all three go the same way.
        elif matched "named" || matched "tally" || matched "glyph" then
            wrap (inkOfLetter palette found.Value[0]) found.Value
        elif matched "rules" then
            wrap $"bold {inkOfLetter palette found.Value[1]}" found.Value
        else
            letterByLetter palette found.Value

    /// Plain text in, Spectre markup out. The escaping has to come first: the board is
    /// full of square brackets - every region is numbered "[ 5]" - and to Spectre a
    /// square bracket opens a colour.
    let markup palette (text: string) =
        rules.Replace(Markup.Escape text, mark palette)

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
    let paint palette (text: string) =
        try
            renderAt 1000 (Markup(markup palette text))
        with _ ->
            // Colour is decoration. If it cannot be had the game is still perfectly
            // readable without it, and losing a turn to a bad escape would not be.
            text
