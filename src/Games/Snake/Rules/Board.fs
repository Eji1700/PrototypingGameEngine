namespace TCModel.Snake

/// One square of the board, counted from the top-left corner: rows down, columns across, both
/// from one. Nothing about what is standing on it.
type Cell = { Row: int; Column: int }

/// Which way a snake is going. Four, and the whole of what a move is at this game.
type Direction =
    | North
    | East
    | South
    | West

module Direction =

    /// Clockwise from the top, which is the order every screen lists them in.
    let all = [ North; East; South; West ]

    /// The way you may not turn, which is the one rule of this game nobody thinks of as a
    /// rule: a snake that turned back would be moving into its own neck, and that is not a
    /// death, it is a thing that cannot happen.
    let opposite =
        function
        | North -> South
        | East -> West
        | South -> North
        | West -> East

/// The board, and the one thing that can be done on it: a step.
///
/// The edges are walls. [Life](../../Life/Rules/Grid.fs) joins them, and the two games are
/// worth reading side by side for it: there, a glider that ran off the edge would end a game
/// that is meant to run for ever; here, an edge you can run into is most of what makes the
/// board a game at all.
module Board =

    [<Literal>]
    let Width = 24

    [<Literal>]
    let Height = 14

    let holds cell =
        cell.Row >= 1 && cell.Row <= Height && cell.Column >= 1 && cell.Column <= Width

    /// The board as rows of cells, top to bottom - for anything laying it out in a shape.
    let rows =
        [ for row in 1..Height -> [ for column in 1..Width -> { Row = row; Column = column } ] ]

    let all = List.concat rows

    /// The next cell that way, wall or no wall. Whether it is on the board is `holds`' answer
    /// and the rules' business: a step off the edge is a legal thing to ask for and a fatal
    /// thing to do, and a function that quietly refused to leave the board would make the
    /// wall unhittable.
    let along direction cell =
        match direction with
        | North -> { cell with Row = cell.Row - 1 }
        | South -> { cell with Row = cell.Row + 1 }
        | East -> { cell with Column = cell.Column + 1 }
        | West -> { cell with Column = cell.Column - 1 }

    /// How far apart two cells are, counted in steps - which is the only measure of distance
    /// this game has, because a snake moves one square at a time and never diagonally.
    let apart one other =
        abs (one.Row - other.Row) + abs (one.Column - other.Column)
