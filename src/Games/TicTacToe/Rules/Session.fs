namespace TCModel.TicTacToe

open TCModel.Engine

type Ending =
    | Won of Mark * line: int list
    | Drawn
    | Abandoned of Mark

type Play =
    { Board: Board
      ToPlay: Mark
      Turn: int }

type Session =
    | InPlay of Play
    | Finished of Play * Ending

module Session =

    let dealt =
        InPlay
            { Board = Board.empty
              ToPlay = Cross
              Turn = 1 }

    let play =
        function
        | InPlay play -> play
        | Finished(play, _) -> play

    let board session = (play session).Board

    let turn session = (play session).Turn

    let isOver =
        function
        | InPlay _ -> false
        | Finished _ -> true

    let ending =
        function
        | InPlay _ -> None
        | Finished(_, ending) -> Some ending


    let seatOf =
        function
        | Cross -> Seat.at 1
        | Nought -> Seat.at 2

    let markAt seat =
        if PlayerId.value seat = 1 then Cross else Nought

    let active session = seatOf (play session).ToPlay
