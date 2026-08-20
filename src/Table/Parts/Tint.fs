namespace TCModel.Table

open System.IO
open System.Text.RegularExpressions
open Spectre.Console
open Spectre.Console.Rendering

[<NoComparison; NoEquality>]
type Marking =
    { Patterns: string list
      Paint: Palette -> Match -> string }

module Tint =

    [<Literal>]
    let private HeadingInk = "bold"

    let wrap style (text: string) = $"[{style}]{text}[/]"

    let yours palette = Palette.ink (Palette.own palette)

    let private ours =
        [ @"(?<tableHeading>^={3}.*?={3}(?=\r?$))"
          @"(?<tableBlock>^[A-Z][A-Z ]*[A-Z](?=\r?$))"
          @"(?<tableYou>\(you\))"
          @"(?<tableActive>->)" ]

    /// A game's own patterns and the ones every board shares, compiled into one alternation of
    /// named groups - so the text is walked once and the first pattern to match a piece of it wins.
    /// The text is escaped before the rules run, so a board that draws a '[' does not become markup.
    let markup (marking: Marking) =
        let rules =
            Regex(String.concat "|" (ours @ marking.Patterns), RegexOptions.Multiline ||| RegexOptions.Compiled)

        let mark palette (found: Match) =
            let matched (name: string) = found.Groups[name].Success

            if matched "tableHeading" || matched "tableBlock" then wrap HeadingInk found.Value
            elif matched "tableYou" || matched "tableActive" then wrap (yours palette) found.Value
            else marking.Paint palette found

        fun palette (text: string) -> rules.Replace(Markup.Escape text, mark palette)

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

    // Rendered far wider than any terminal, because this is painting a line of text rather than
    // laying out a screen: Spectre would otherwise wrap it to a width nothing here has asked for.
    let painter marking =
        let markup = markup marking

        fun palette (text: string) ->
            try
                renderAt 1000 (Markup(markup palette text))
            with _ ->
                text
