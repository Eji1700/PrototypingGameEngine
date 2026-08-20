namespace TCModel.Turncoats

open System.Text.RegularExpressions
open TCModel.Table

module Ink =

    let key color = (Words.color color).ToLowerInvariant()

    [<Literal>]
    let Hidden = "hidden"

    let ink palette color = Palette.inkOf (key color) palette

    let color palette faction =
        (Palette.shadeOf (key faction) palette).Color

    let hidden palette = Palette.inkOf Hidden palette

    let private ofLetter palette letter =
        match letter with
        | 'R' -> ink palette Red
        | 'B' -> ink palette Blue
        | _ -> ink palette Green

    let private letterByLetter palette (text: string) =
        text
        |> Seq.map (fun c -> if c = 'R' || c = 'B' || c = 'G' then Tint.wrap (ofLetter palette c) (string c) else string c)
        |> String.concat ""

    let marking =
        { Patterns =
            [ @"(?<dead>\b(?:dead|unclaimed)\b)"
              @"(?<named>\b(?:Red|Blue|Green)\b)"
              @"(?<tally>\b[RBG]x[0-9]+)"
              @"(?<home>\([RBG]\))"
              @"(?<rules>>[RBG])"
              @"(?<tied>=[RBG]+)"
              @"(?<glyph>(?<![A-Za-z0-9])[RBG](?![A-Za-z0-9]))" ]
          Paint =
            fun palette (found: Match) ->
                let matched (name: string) = found.Groups[name].Success

                if matched "dead" then
                    Tint.wrap (hidden palette) found.Value
                elif matched "named" || matched "tally" || matched "glyph" then
                    Tint.wrap (ofLetter palette found.Value[0]) found.Value
                elif matched "rules" then
                    Tint.wrap $"bold {ofLetter palette found.Value[1]}" found.Value
                else
                    letterByLetter palette found.Value }

    let markup = Tint.markup marking

    let paint = Tint.painter marking

    let slots =
        (StoneColor.all
         |> List.map (fun color ->
             { Key = key color
               Draws = $"{Words.color color} stones, and the regions {Words.color color} rules"
               Shows = $"{Words.color color}   {Words.glyph color} {Words.glyph color}   >{Words.glyph color}"
               Standard =
                 match color with
                 | Red -> Palette.crimson
                 | Blue -> Palette.azure
                 | Green -> Palette.moss }))
        @ [ { Key = Hidden
              Draws = "what is held back from you, and ground nobody may enter"
              Shows = "dead"
              Standard = Palette.slate } ]
