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

    let painter marking =
        let markup = markup marking

        fun palette (text: string) ->
            try
                renderAt 1000 (Markup(markup palette text))
            with _ ->
                text
