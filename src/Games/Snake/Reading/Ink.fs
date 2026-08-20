namespace TCModel.Snake

open TCModel.Engine
open TCModel.Table
open TCModel.Snake

module Ink =

    [<Literal>]
    let Wreck = "#"

    [<Literal>]
    let Food = "*"

    [<Literal>]
    let Empty = "."

    let key seat = string (Words.letter seat)

    let head seat =
        string (System.Char.ToUpperInvariant(Words.letter seat))

    let body seat = string (Words.letter seat)

    let private slot place standard =
        let seat = Seat.at place

        { Key = key seat
          Draws = $"{Words.player seat}, head and all"
          Shows = $"{body seat}{body seat}{head seat}"
          Standard = standard }

    let food =
        { Key = "food"
          Draws = "the food, wherever it has landed"
          Shows = Food + Food + Food
          Standard = Palette.gold }

    let slots =
        [ slot 1 Palette.moss
          slot 2 Palette.crimson
          slot 3 Palette.azure
          slot 4 (Palette.named "violet")
          food ]

    let marking =
        { Patterns = [ @"(?<snake>\bSnake [A-Z]\b)" ]
          Paint =
            fun palette found ->
                let letter = System.Char.ToLowerInvariant(found.Value[found.Value.Length - 1])
                Tint.wrap (Palette.inkOf (string letter) palette) found.Value }
