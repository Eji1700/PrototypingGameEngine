namespace Prototyping.TicTacToe

open Prototyping.Table

module Ink =

    let key =
        function
        | Cross -> "x"
        | Nought -> "o"

    let private slot mark standard =
        { Key = key mark
          Draws = $"the {(Words.named mark).ToLowerInvariant()}, and the line that wins with them"
          Shows = $"{Words.mark mark} {Words.mark mark} {Words.mark mark}"
          Standard = standard }

    let slots = [ slot Cross Palette.crimson; slot Nought Palette.azure ]

    let ink palette mark = Palette.inkOf (key mark) palette

    let marking =
        { Patterns = [ @"(?<named>\b(?:Crosses|Noughts)\b)"; @"(?<mark>\b[XO]\b)" ]
          Paint =
            fun palette found ->
                let mark =
                    if found.Value.StartsWith "X" || found.Value.StartsWith "C" then Cross else Nought

                Tint.wrap (ink palette mark) found.Value }
