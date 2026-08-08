namespace TCModel.TicTacToe

/// Everything a player may ask this game to do, which is one thing and the one thing every
/// game has: put a mark somewhere, or put the game down.
type Move =
    | Place of square: int
    | Resign

type Happening =
    | Placed of Mark * square: int
    | GameEnded of Ending

type Refusal =
    | NoSuchSquare of said: int
    | AlreadyTaken of square: int * Mark

/// What this game has to say, and the whole of it. Nothing about undo, nothing about a line
/// nobody could read: those are the engine's and are said once, above, in words that suit
/// any game.
type Notice =
    | Happened of Happening
    | Refused of Refusal

/// How a turn goes, which at this game is the shortest rule there is: the square has to
/// exist and has to be free, and then the game either ends or comes round to the other mark.
module Turn =

    /// Whether that move finished it.
    let private finished board =
        match Board.winning board with
        | Some(mark, line) -> Some(Won(mark, line))
        | None -> if Board.isFull board then Some Drawn else None

    /// What the engine asks of a game: a move and where it stands, and the position it left
    /// along with whatever there is to say.
    ///
    /// Total, like every game's: `None` for the position means nothing moved, and the notices
    /// say why. A refusal is something this game *says*, not something that breaks it, which
    /// is what lets every table above be a fold.
    let asked move session =
        match session, move with
        // The engine refuses moves after the game is over and says so itself, so this is
        // unreachable rather than wrong. Answered all the same, because a total function is
        // cheaper than an argument about which of two files is guarding it.
        | Finished _, _ -> None, []

        | InPlay play, Resign ->
            let ending = Abandoned play.ToPlay
            Some(Finished(play, ending)), [ Happened(GameEnded ending) ]

        | InPlay play, Place square when not (Squares.holds square) -> None, [ Refused(NoSuchSquare square) ]

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
