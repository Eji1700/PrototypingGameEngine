namespace TCModel.Domain

open System.Text.RegularExpressions
open TCModel.Table

/// What this game colours, and where each colour is used.
///
/// This came out of `Tint` when there was a second game to answer to, and the split is worth
/// reading as the line between the two: a heading is a heading and `(you)` is your seat at
/// any game, so those stayed generic; `[RBG]`, the word "Red", a tally written `Gx4` and the
/// mark on whoever rules a region are this board's alphabet and could not.
///
/// Which colour is drawn for what is the player's to change and travels in the palette. What
/// is here is only where each one is used, which is not.
module Ink =

    /// The word a person types for a faction's colour, which is also the key it is kept
    /// under in a palette. The board's own word, lowered - whatever the factions come to be
    /// called, this is what a player will be typing.
    let key color = (Words.color color).ToLowerInvariant()

    /// What is held back from a reader, and ground nobody may enter. One key, because both
    /// are the absence of anything to see.
    [<Literal>]
    let Hidden = "hidden"

    /// A faction's colour as markup says it.
    let ink palette color = Palette.inkOf (key color) palette

    /// The same colour as Spectre's own, for the widgets that take one rather than a name.
    let color palette faction =
        (Palette.shadeOf (key faction) palette).Color

    let hidden palette = Palette.inkOf Hidden palette

    /// A colour by its first letter, which serves for a stone's glyph and for its name
    /// alike: the three are written R, B and G and called Red, Blue and Green, so one
    /// letter tells them apart wherever they are written.
    let private ofLetter palette letter =
        match letter with
        | 'R' -> ink palette Red
        | 'B' -> ink palette Blue
        | _ -> ink palette Green

    /// Colour every letter of a run one by one, leaving anything else as it was - so
    /// "=BG" keeps its sign and each colour level in the region keeps its own.
    let private letterByLetter palette (text: string) =
        text
        |> Seq.map (fun c -> if c = 'R' || c = 'B' || c = 'G' then Tint.wrap (ofLetter palette c) (string c) else string c)
        |> String.concat ""

    /// This board's alphabet, in the order the rules should win, so nothing already painted
    /// can be matched again by a later one.
    let marking =
        { Patterns =
            [ // Ground nobody may enter, and ground nobody holds: both are the absence of
              // anything to see, which is what the hidden colour is for.
              @"(?<dead>\b(?:dead|unclaimed)\b)"
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
          Paint =
            fun palette (found: Match) ->
                let matched (name: string) = found.Groups[name].Success

                if matched "dead" then
                    Tint.wrap (hidden palette) found.Value
                // "Green", "Gx4" and a lone "G" all begin with the letter that names the
                // colour, so all three go the same way.
                elif matched "named" || matched "tally" || matched "glyph" then
                    Tint.wrap (ofLetter palette found.Value[0]) found.Value
                elif matched "rules" then
                    Tint.wrap $"bold {ofLetter palette found.Value[1]}" found.Value
                else
                    letterByLetter palette found.Value }

    /// Painted but not rendered, for text going inside one of Spectre's own widgets.
    let markup = Tint.markup marking

    /// Plain text in, the same text in colour out. Both are built once, because the regex
    /// behind them is compiled and a board is painted every turn.
    let paint = Tint.painter marking

    /// What takes a colour in this game: the three factions, and what is held back.
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
