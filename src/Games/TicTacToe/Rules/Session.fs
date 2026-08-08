namespace TCModel.TicTacToe

open TCModel.Engine

/// How a game finished.
///
/// The winning line comes with the win, because every screen wants to point at it and none
/// of them should have to work it out again from a board that has stopped changing.
type Ending =
    | Won of Mark * line: int list
    | Drawn
    /// Somebody walked away, and which of them.
    | Abandoned of Mark

/// A game still going: what is on the board, whose turn it is, and which turn.
type Play =
    { Board: Board
      ToPlay: Mark
      Turn: int }

/// Where the game stands, which is the one thing the engine ever asks about.
type Session =
    | InPlay of Play
    | Finished of Play * Ending

module Session =

    /// A fresh game. There is nothing to deal and nothing to shuffle - which is exactly the
    /// contrast worth having beside a game that does both.
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

    // --- marks and seats ------------------------------------------------------------------
    //
    // Two seats, and the mark at each is settled by the dealing order rather than kept
    // anywhere: seat one plays the crosses because the crosses go first. Written as a pair of
    // functions rather than a table so that neither can be looked up and found missing.

    let seatOf =
        function
        | Cross -> Seat.at 1
        | Nought -> Seat.at 2

    let markAt seat =
        if PlayerId.value seat = 1 then Cross else Nought

    let active session = seatOf (play session).ToPlay
