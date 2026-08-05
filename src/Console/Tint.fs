namespace TCModel.Console

open System.IO
open System.Text.RegularExpressions
open Spectre.Console

/// Colour laid over a board that has already been drawn.
///
/// The board is written once, in plain text, by `Render` - and it is written by counting
/// characters into columns, so a colour code slipped in while it was being laid out would
/// push everything after it sideways. So nothing is coloured until the writing is done and
/// every column is where it belongs. What happens here cannot move a single character: it
/// only wraps some of them.
///
/// That is also what lets a player at a networked table choose their own colours. The wire
/// carries the plain board and this runs at the far end, so the table neither knows nor
/// cares how anyone is reading it.
module Tint =

    // --- the palette, which is the whole of what is decided here ----------------------

    /// Eight-bit colours rather than the sixteen named ones, because black stones drawn
    /// in black are no use on a black screen, and dark blue is not much better.
    [<Literal>]
    let private Red = "red1"

    [<Literal>]
    let private Blue = "dodgerblue1"

    [<Literal>]
    let private Black = "grey70"

    [<Literal>]
    let private Heading = "bold"

    /// The reader's own seat, and the arrow marking whoever is to play.
    [<Literal>]
    let private Yours = "green"

    /// Ground nobody may enter.
    [<Literal>]
    let private Dead = "dim"

    /// A colour by its glyph, which is how a stone is written on the map and in a tally.
    let private colorFor letter =
        match letter with
        | 'R' -> Red
        | 'B' -> Blue
        | _ -> Black

    /// A colour by its name, which cannot go through the glyph above: Blue and Black
    /// both begin with a B, and only the whole word tells them apart.
    let private colorNamed word =
        match word with
        | "Red" -> Red
        | "Blue" -> Blue
        | _ -> Black

    let private wrap style (text: string) = $"[{style}]{text}[/]"

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
          // Who rules a region, as ">R", and who is level in it, as "=BK".
          @"(?<ruler>[>=][RBK]+)"
          // A stone standing on the map, on its own.
          @"(?<glyph>(?<![A-Za-z0-9])[RBK](?![A-Za-z0-9]))" ]
        |> String.concat "|"

    let private rules = Regex(marks, RegexOptions.Multiline ||| RegexOptions.Compiled)

    /// Colour every letter of a run one by one, leaving anything else as it was - so
    /// ">R" keeps its arrow and "=BK" keeps its sign.
    let private letterByLetter (text: string) =
        text
        |> Seq.map (fun c ->
            if c = 'R' || c = 'B' || c = 'K' then
                wrap (colorFor c) (string c)
            else
                string c)
        |> String.concat ""

    let private mark (found: Match) =
        let matched (name: string) = found.Groups[name].Success

        if matched "heading" || matched "block" then wrap Heading found.Value
        elif matched "you" || matched "active" then wrap Yours found.Value
        elif matched "dead" then wrap Dead found.Value
        elif matched "named" then wrap (colorNamed found.Value) found.Value
        elif matched "tally" || matched "glyph" then wrap (colorFor found.Value[0]) found.Value
        else letterByLetter found.Value

    // --- putting it through Spectre -----------------------------------------------------

    /// Spectre writes to a console; this one writes into a string, so what comes back can
    /// be printed here, sent down a wire, or written to a file like any other text.
    ///
    /// The width is set far past anything the board needs, because Spectre would otherwise
    /// fold long lines at the width of whatever it thinks it is writing to - and the map
    /// is one long line after another.
    let private through (markup: string) =
        use writer = new StringWriter()

        let console =
            AnsiConsole.Create(
                AnsiConsoleSettings(
                    Ansi = AnsiSupport.Yes,
                    ColorSystem = ColorSystemSupport.EightBit,
                    Out = AnsiConsoleOutput(writer)
                )
            )

        console.Profile.Width <- 1000
        console.Markup markup
        writer.ToString()

    /// Plain text in, the same text in colour out.
    let paint (text: string) =
        try
            // Escaped first: the board is full of square brackets - every region is
            // numbered "[ 5]" - and to Spectre a square bracket opens a colour.
            through (rules.Replace(Markup.Escape text, mark))
        with _ ->
            // Colour is decoration. If it cannot be had, the game is still perfectly
            // readable without it, and losing a turn to a bad escape would not be.
            text
