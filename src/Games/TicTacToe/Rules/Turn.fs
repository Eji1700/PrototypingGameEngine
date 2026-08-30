namespace Prototyping.TicTacToe

type Move =
    | Place of square: int
    | Resign

type Happening =
    | Placed of Mark * square: int
    | GameEnded of Ending

type Refusal =
    | NoSuchSquare of said: int
    | AlreadyTaken of square: int * Mark

type Notice =
    | Happened of Happening
    | Refused of Refusal

module Turn =

    let private finished board =
        match Board.winning board with
        | Some(mark, line) -> Some(Won(mark, line))
        | None -> if Board.isFull board then Some Drawn else None

    let asked move session =
        match session, move with
        | Finished _, _ -> None, []

        | InPlay play, Resign ->
            let ending = Abandoned play.ToPlay
            Some(Finished(play, ending)), [ Happened(GameEnded ending) ]

        | InPlay _, Place square when not (Squares.holds square) -> None, [ Refused(NoSuchSquare square) ]

        | InPlay play, Place square ->
            match Board.at square play.Board with
            | Some taken -> None, [ Refused(AlreadyTaken(square, taken)) ]
            | None ->
                let board = Board.place square play.ToPlay play.Board
                let placed = Happened(Placed(play.ToPlay, square))

                match finished board with
                | Some ending -> Some(Finished({ play with Board = board }, ending)), [ placed; Happened(GameEnded ending) ]
                | None ->
                    Some(
                        InPlay
                            { play with
                                Board = board
                                ToPlay = Mark.other play.ToPlay
                                Turn = play.Turn + 1 }
                    ),
                    [ placed ]
