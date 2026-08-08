namespace TCModel.Table

open System.IO
open System.Text.RegularExpressions
open Spectre.Console
open Spectre.Console.Rendering

/// What a game says gets coloured, beyond the things every game does.
///
/// Colour is laid over a board that has already been drawn - drawn by counting characters
/// into columns, so nothing may be coloured until every column is where it belongs. That is
/// why this is a list of patterns and not a way of building a board: what happens here
/// cannot move a single character, it only wraps some of them.
///
/// One pass, with the alternatives in the order they should win, so nothing that has already
/// been painted can be matched again by a later rule. The table's own rules go first - a
/// heading is a heading in any game - and the game's follow in the order it gives them.
[<NoComparison; NoEquality>]
type Marking =
    {
        /// Named groups, in the order they should win. The names are the game's own; nothing
        /// here reads them except to hand the match back.
        Patterns: string list
        /// The markup one of this game's matches should be wrapped in.
        Paint: Palette -> Match -> string
    }

/// Colour, and the machinery for getting Spectre's writing back out as text.
///
/// `paint` lays colour over a drawn board. `renderAt` is the other half: Spectre writes to a
/// console, and this one writes into a string, so what comes back can be printed, sent down
/// a wire, or written to a file like any other text. A view that builds a board out of
/// Spectre's own widgets comes through here to turn it into something a game can hand about.
module Tint =

    [<Literal>]
    let private HeadingInk = "bold"

    let wrap style (text: string) = $"[{style}]{text}[/]"

    /// The reader's own seat, and the arrow marking whoever is to play.
    let yours palette = Palette.ink (Palette.own palette)

    /// What every screen has whatever is being played: a title line, the block titles under
    /// it, the mark on your own seat and the arrow on whoever is to act.
    ///
    /// These come first so a game cannot accidentally repaint them, and they are here rather
    /// than in each game because a game that had to say what a heading looks like would be
    /// a game that could disagree with the next one about it.
    let private ours =
        [ @"(?<tableHeading>^={3}.*?={3}(?=\r?$))"
          @"(?<tableBlock>^[A-Z][A-Z ]*[A-Z](?=\r?$))"
          @"(?<tableYou>\(you\))"
          @"(?<tableActive>->)" ]

    /// Plain text in, Spectre markup out - painted but not yet rendered, for a view that is
    /// putting the result inside a widget of Spectre's rather than printing it.
    ///
    /// Built once per marking and kept, because a game's regex is compiled and a board is
    /// painted every turn. Ask for this where the view is built, not on every board.
    let markup (marking: Marking) =
        let rules =
            Regex(String.concat "|" (ours @ marking.Patterns), RegexOptions.Multiline ||| RegexOptions.Compiled)

        let mark palette (found: Match) =
            let matched (name: string) = found.Groups[name].Success

            if matched "tableHeading" || matched "tableBlock" then wrap HeadingInk found.Value
            elif matched "tableYou" || matched "tableActive" then wrap (yours palette) found.Value
            else marking.Paint palette found

        fun palette (text: string) ->
            // The escaping has to come first: a board may be full of square brackets - a
            // region numbered "[ 5]", a cell drawn as "[ ]" - and to Spectre a square
            // bracket opens a colour.
            rules.Replace(Markup.Escape text, mark palette)

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
    let painter marking =
        let markup = markup marking

        fun palette (text: string) ->
            try
                renderAt 1000 (Markup(markup palette text))
            with _ ->
                // Colour is decoration. If it cannot be had the game is still perfectly
                // readable without it, and losing a turn to a bad escape would not be.
                text
