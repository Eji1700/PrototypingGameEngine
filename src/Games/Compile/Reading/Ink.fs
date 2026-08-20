namespace TCModel.Compile

open TCModel.Engine
open TCModel.Table
open TCModel.Compile

module Ink =

    let key seat = $"p{PlayerId.value seat}"

    let private slot seat standard =
        { Key = key seat
          Draws = $"{Words.player seat}'s protocols, stacks and cards"
          Shows = Words.player seat
          Standard = standard }

    let slots = [ slot (Seat.at 1) Palette.crimson; slot (Seat.at 2) Palette.azure ]

    let ink palette seat = Palette.inkOf (key seat) palette

    let marking =
        { Patterns = [ @"(?<seat>\bPlayer [12]\b)" ]
          Paint =
            fun palette found ->
                let seat = if found.Value.EndsWith "1" then Seat.at 1 else Seat.at 2
                Tint.wrap (ink palette seat) found.Value }
