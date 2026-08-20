namespace TCModel.TicTacToe

type Mark =
    | Cross
    | Nought

module Mark =

    let all = [ Cross; Nought ]

    let other =
        function
        | Cross -> Nought
        | Nought -> Cross

module Squares =

    [<Literal>]
    let Side = 3

    let all = [ 1 .. Side * Side ]

    let holds square = square >= 1 && square <= Side * Side

    let rowOf square = (square - 1) / Side + 1

    let columnOf square = (square - 1) % Side + 1

    let rows =
        [ for row in 0 .. Side - 1 -> [ for column in 1..Side -> row * Side + column ] ]

    let private columns =
        [ for column in 1..Side -> [ for row in 0 .. Side - 1 -> row * Side + column ] ]

    let private diagonals =
        [ [ for step in 0 .. Side - 1 -> step * Side + step + 1 ]
          [ for step in 0 .. Side - 1 -> step * Side + (Side - step) ] ]

    let lines = rows @ columns @ diagonals
