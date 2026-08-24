namespace Prototyping.Warband

open Prototyping.Table

module Ink =

    /// Two slots and no more. Your own squad is drawn in the colour the table already keeps for
    /// "yours", which every game here shares, so the only colour this game has to name is the one
    /// the other squad is drawn in - and the hexes, which belong to neither.
    [<Literal>]
    let Foe = "foe"

    [<Literal>]
    let Hex = "hex"

    let slots =
        [ { Key = Foe
            Draws = "the squad across the field from you"
            Shows = "Ward Ride Bow"
            Standard = Palette.crimson }

          { Key = Hex
            Draws = "the hexes, and the hexes named in what the game says"
            Shows = "f1 m2 b3"
            Standard = Palette.bone } ]

    /// Where the same colour is put back into text that was written as plain words. A hex is a
    /// letter and a digit, which is a small enough thing to say by accident - so it is anchored at
    /// both ends and the digit is held to the four a rank can be that wide.
    let marking =
        { Patterns = [ @"(?<hex>\b[fmb][1-4]\b)" ]
          Paint = fun palette found -> Tint.wrap (Palette.inkOf Hex palette) found.Value }
