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

    /// Eight-bit colours rather than the sixteen named ones, because black stones drawn
    /// in black are no use on a black screen, and dark blue is not much better.
    [<Literal>]
    let private RedInk = "red1"

    [<Literal>]
    let private BlueInk = "dodgerblue1"

    [<Literal>]
    let private BlackInk = "grey70"

    [<Literal>]
    let private HeadingInk = "bold"

    /// The reader's own seat, and the arrow marking whoever is to play.
    [<Literal>]
    let Yours = "green"

    /// Ground nobody may enter, and stones nobody can see.
    [<Literal>]
    let Hidden = "grey37"

    /// A colour as markup says it.
    let ink =
        function
        | Red -> RedInk
        | Blue -> BlueInk
        | Black -> BlackInk

    /// The same colour as Spectre's own, for the widgets that take one rather than a name.
    let color =
        function
        | Red -> Color.Red1
        | Blue -> Color.DodgerBlue1
        | Black -> Color.Grey70

    /// A colour by its glyph, which is how a stone is written on the map and in a tally.
    let private inkOfGlyph letter =
        match letter with
        | 'R' -> RedInk
        | 'B' -> BlueInk
        | _ -> BlackInk

    /// A colour by its name, which cannot go through the glyph above: Blue and Black
    /// both begin with a B, and only the whole word tells them apart.
    let private inkOfName word =
        match word with
        | "Red" -> RedInk
        | "Blue" -> BlueInk
        | _ -> BlackInk

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
          // Colours named in prose: "Red rules the region", "2 Black".
          @"(?<named>\b(?:Red|Blue|Black)\b)"
          // A tally, as "Rx4".
          @"(?<tally>\b[RBK]x[0-9]+)"
          // A home's own colour, as "(R)".
          @"(?<home>\([RBK]\))"
          // Who rules a region, as ">R" - the one thing on the map worth spotting from
          // across the room - and who is level in it, as "=BK".
          @"(?<rules>>[RBK])"
          @"(?<tied>=[RBK]+)"
          // A stone standing on the map, on its own.
          @"(?<glyph>(?<![A-Za-z0-9])[RBK](?![A-Za-z0-9]))" ]
        |> String.concat "|"

    let private rules = Regex(marks, RegexOptions.Multiline ||| RegexOptions.Compiled)

    /// Colour every letter of a run one by one, leaving anything else as it was - so
    /// "=BK" keeps its sign and each colour level in the region keeps its own.
    let private letterByLetter (text: string) =
        text
        |> Seq.map (fun c ->
            if c = 'R' || c = 'B' || c = 'K' then
                wrap (inkOfGlyph c) (string c)
            else
                string c)
        |> String.concat ""

    let private mark (found: Match) =
        let matched (name: string) = found.Groups[name].Success

        if matched "heading" || matched "block" then wrap HeadingInk found.Value
        elif matched "you" || matched "active" then wrap Yours found.Value
        elif matched "dead" then wrap Hidden found.Value
        elif matched "named" then wrap (inkOfName found.Value) found.Value
        elif matched "tally" || matched "glyph" then wrap (inkOfGlyph found.Value[0]) found.Value
        elif matched "rules" then wrap $"bold {inkOfGlyph found.Value[1]}" found.Value
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
