namespace TCModel.TicTacToe

/// Which of the two marks. Nothing else about a player is here: whose mark it is, is the
/// seat's business, and whose seat it is, is the table's.
type Mark =
    | Cross
    | Nought

module Mark =

    /// In the order they play. Crosses go first, which is the whole of what deciding who
    /// goes first amounts to at this game - there is nothing to deal and no dice to throw.
    let all = [ Cross; Nought ]

    let other =
        function
        | Cross -> Nought
        | Nought -> Cross

/// The squares, and the runs of them worth having.
///
/// Numbered the way a keypad is: 1 to 9, left to right and top to bottom, so the number a
/// player types is the square they are looking at. Everything below is worked out from the
/// side rather than written down, because a board written out by hand is a board that can be
/// written out wrong - and `Faults` is where this game says whether it was.
module Squares =

    [<Literal>]
    let Side = 3

    let all = [ 1 .. Side * Side ]

    let holds square = square >= 1 && square <= Side * Side

    /// Which row and column a square is in, counting from one - for anything laying the
    /// board out in a shape rather than in a line.
    let rowOf square = (square - 1) / Side + 1

    let columnOf square = (square - 1) % Side + 1

    /// The board as rows of squares, top to bottom.
    let rows =
        [ for row in 0 .. Side - 1 -> [ for column in 1..Side -> row * Side + column ] ]

    let private columns =
        [ for column in 1..Side -> [ for row in 0 .. Side - 1 -> row * Side + column ] ]

    let private diagonals =
        [ [ for step in 0 .. Side - 1 -> step * Side + step + 1 ]
          [ for step in 0 .. Side - 1 -> step * Side + (Side - step) ] ]

    /// Every run that wins: the rows, the columns and the two diagonals.
    let lines = rows @ columns @ diagonals
