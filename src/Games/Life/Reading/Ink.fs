namespace Prototyping.Life

open Prototyping.Table
open Prototyping.Life

module Ink =

    [<Literal>]
    let Key = "live"

    [<Literal>]
    let Living = "#"

    [<Literal>]
    let Empty = "."

    let slots =
        [ { Key = Key
            Draws = "the living cells, and the cells named in what the game says"
            Shows = String.replicate 3 Living
            Standard = Palette.moss } ]

    let ink palette = Palette.inkOf Key palette

    let marking =
        { Patterns = [ @"(?<cell>\b[a-z][0-9]{1,2}\b)" ]
          Paint = fun palette found -> Tint.wrap (ink palette) found.Value }
