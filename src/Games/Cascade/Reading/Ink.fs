namespace TCModel.Cascade

open TCModel.Table
open TCModel.Cascade

module Ink =

    [<Literal>]
    let Elbow = "elbow"

    [<Literal>]
    let Worn = "worn"

    [<Literal>]
    let Hot = "hot"

    [<Literal>]
    let Bright = "bright"

    [<Literal>]
    let Turning = "turning"

    [<Literal>]
    let Lit = "lit"

    /// The four ways of drawing an elbow, a step of wear apiece.
    ///
    /// They differ in the weight of the line and not only in its colour, and that is the point:
    /// a board drawn in plain text has no colour at all, and a player reading one still has to
    /// be able to see which cells have been round the most. Each string runs in the order
    /// `Facing.all` does, so the glyph is looked up by where the facing sits in that list.
    let private steps = [ "└┌┐┘"; "╰╭╮╯"; "┗┏┓┛"; "╚╔╗╝" ]

    let private stepped wear =
        steps[min (max wear 0) (List.length steps - 1)]

    let elbow wear facing =
        let step = stepped wear
        string step[Facing.all |> List.findIndex ((=) facing)]

    /// A cell caught half way through its turn.
    ///
    /// There is no box-drawing character for an elbow at forty-five degrees, so what is drawn
    /// instead is the way its corner is pointing - which is the arm it is turning its first arm
    /// onto. It reads as motion rather than as a shape, which is what it is.
    let private pointing =
        function
        | North -> "^"
        | East -> ">"
        | South -> "v"
        | West -> "<"

    let turning facing = pointing (Facing.halfway facing)

    /// Which slot a cell of that much wear is drawn in. The names are steps of a fire rather
    /// than shades of one colour, because a player who has recoloured them should still be able
    /// to tell four of them apart.
    let wornBy wear =
        match max wear 0 with
        | 0 -> Elbow
        | 1 -> Worn
        | 2 -> Hot
        | _ -> Bright

    let slots =
        [ { Key = Elbow
            Draws = "a cell as it was dealt, and the cells named in what the game says"
            Shows = "└┌┐"
            Standard = Palette.named "slate" }

          { Key = Worn
            Draws = $"a cell that has turned {Session.PerStep} times or more"
            Shows = "╰╭╮"
            Standard = Palette.named "teal" }

          { Key = Hot
            Draws = $"a cell that has turned {2 * Session.PerStep} times or more"
            Shows = "┗┏┓"
            Standard = Palette.named "sky" }

          { Key = Bright
            Draws = $"a cell that has turned {3 * Session.PerStep} times or more"
            Shows = "╚╔╗"
            Standard = Palette.named "bone" }

          { Key = Turning
            Draws = "a cell in the middle of a turn, and the one that has just finished one"
            Shows = "^>v"
            Standard = Palette.gold }

          { Key = Lit
            Draws = "the light that runs along a row, a column or a square that has come up"
            Shows = "***"
            Standard = Palette.crimson } ]

    let marking =
        { Patterns = [ @"(?<cell>\b[a-p](?:1[0-6]|[1-9])\b)" ]
          Paint = fun palette found -> Tint.wrap (Palette.inkOf Turning palette) found.Value }
