namespace Prototyping.Snake

open Prototyping.Common

type Direction =
    | North
    | East
    | South
    | West

module Direction =

    let all = [ North; East; South; West ]

    let opposite =
        function
        | North -> South
        | East -> West
        | South -> North
        | West -> East

module Board =

    [<Literal>]
    let Width = 24

    [<Literal>]
    let Height = 14

    let grid = { Width = Width; Height = Height }

    let holds = Grid.holds grid

    let rows = Grid.rows grid

    let all = Grid.all grid

    let along direction cell =
        match direction with
        | North -> { cell with Row = cell.Row - 1 }
        | South -> { cell with Row = cell.Row + 1 }
        | East -> { cell with Column = cell.Column + 1 }
        | West -> { cell with Column = cell.Column - 1 }

    let apart one other =
        abs (one.Row - other.Row) + abs (one.Column - other.Column)
