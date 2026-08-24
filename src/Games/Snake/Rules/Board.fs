namespace Prototyping.Snake

type Cell = { Row: int; Column: int }

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

    let holds cell =
        cell.Row >= 1 && cell.Row <= Height && cell.Column >= 1 && cell.Column <= Width

    let rows =
        [ for row in 1..Height -> [ for column in 1..Width -> { Row = row; Column = column } ] ]

    let all = List.concat rows

    let along direction cell =
        match direction with
        | North -> { cell with Row = cell.Row - 1 }
        | South -> { cell with Row = cell.Row + 1 }
        | East -> { cell with Column = cell.Column + 1 }
        | West -> { cell with Column = cell.Column - 1 }

    let apart one other =
        abs (one.Row - other.Row) + abs (one.Column - other.Column)
