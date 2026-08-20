namespace TCModel.Diplomacy

open TCModel.Table
open TCModel.Diplomacy

module Ink =

    let key = Power.key

    let private standardFor =
        function
        | Austria -> Palette.crimson
        | England -> Palette.azure
        | France -> Palette.named "sky"
        | Germany -> Palette.bone
        | Italy -> Palette.moss
        | Russia -> Palette.named "violet"
        | Turkey -> Palette.gold

    let private slot power =
        { Key = key power
          Draws = $"{Power.name power}: its units, its centres and its name"
          Shows = $"{Power.letter power}  {Power.name power}"
          Standard = standardFor power }

    [<Literal>]
    let Sea = "sea"

    let private water =
        { Key = Sea
          Draws = "the open water, and the tildes round the name of a sea"
          Shows = "~nth~  the North Sea"
          Standard = Palette.named "teal" }

    let slots = (Power.all |> List.map slot) @ [ water ]

    let ink palette power = Palette.inkOf (key power) palette

    let hidden _ = Palette.ink Palette.slate

    let marking =
        let words =
            Power.all
            |> List.collect (fun power -> [ Power.name power; Power.adjective power ])
            |> String.concat "|"

        { Patterns = [ $@"(?<power>\b(?:{words})\b)" ]
          Paint =
            fun palette found ->
                match Power.byName found.Value with
                | Some power -> Tint.wrap (ink palette power) found.Value
                | None -> found.Value }
