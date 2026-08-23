namespace TCModel.MyGame

open TCModel.Table

module Ink =

    [<Literal>]
    let Tokens = "tokens"

    /// What a player may recolour, and what it is called when they do. A slot is named for what it
    /// draws rather than for the colour it happens to be, so `--color tokens=teal` reads.
    let slots =
        [ { Key = Tokens
            Draws = "the tokens still on the row"
            Shows = "* * * * *"
            Standard = Palette.gold } ]

    /// Where the same colour is put back into text that was written as plain words - the row
    /// itself, and any count of tokens the game reads out as it goes.
    let marking =
        { Patterns = [ @"(?<row>\*(?: \*)*)"; @"(?<count>\b\d+ tokens?\b)" ]
          Paint = fun palette found -> Tint.wrap (Palette.inkOf Tokens palette) found.Value }
