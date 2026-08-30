namespace Prototyping.Cascade

open Prototyping.Common
open Prototyping.Table

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

    /// The ways of drawing an elbow, a step of wear apiece. They differ in the weight of the line
    /// and not only its colour, so a board drawn in plain text still shows which cells have been
    /// round the most. Each string runs in `Facing.all` order, and is indexed by it; how many there
    /// are is the rules' `Session.Steps`, and `Offer` holds the two counts against each other.
    let steps = [ "└┌┐┘"; "╰╭╮╯"; "┗┏┓┛"; "╚╔╗╝" ]

    let private stepped wear =
        steps[min (max wear 0) (List.length steps - 1)]

    let elbow wear facing =
        let step = stepped wear
        string step[Facing.all |> List.findIndex ((=) facing)]

    /// A cell caught half way through its turn. There is no box-drawing character for an elbow at
    /// forty-five degrees, so what is drawn instead is the way its corner points - which reads as
    /// motion rather than as a shape, and motion is what it is.
    let private pointing =
        function
        | North -> "^"
        | East -> ">"
        | South -> "v"
        | West -> "<"

    let turning facing = pointing (Facing.halfway facing)

    /// The slot a cell is drawn in at each step of wear. The names are steps of a fire rather
    /// than shades of one colour, so a player who has recoloured them can still tell four apart.
    let worn = [ Elbow; Worn; Hot; Bright ]

    let wornBy wear =
        worn[min (max wear 0) (List.length worn - 1)]

    let slots =
        [ { Key = Elbow
            Draws = "a cell as it was dealt"
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
            Draws =
              "a cell in the middle of a turn, the one that has just finished one, and the cells named in what the game says"
            Shows = "^>v"
            Standard = Palette.gold }

          { Key = Lit
            Draws = "the light that runs along a row, a column or a square that has come up"
            Shows = "***"
            Standard = Palette.crimson } ]

    /// The cells named in what the game says, painted as a cell turning is. A name is a column's
    /// letter and a row's number, held to the board's own size so that 'a17' is left alone.
    let marking =
        let rows = [ Board.Height .. -1 .. 1 ] |> List.map string |> String.concat "|"

        { Patterns = [ $@"(?<cell>\b[a-{Seq.last Board.letters}](?:{rows})\b)" ]
          Paint = fun palette found -> Tint.wrap (Palette.inkOf Turning palette) found.Value }
