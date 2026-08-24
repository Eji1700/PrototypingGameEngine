namespace Prototyping.MyGame

open Prototyping.Engine

type Ending =
    | TookTheLast of seat: int
    | Abandoned of seat: int

type Play =
    { Left: int
      Players: int
      ToPlay: int
      Turn: int }

/// A game in play or a game finished, rather than a game carrying an `IsOver` flag: an ending is
/// only a thing once there is one, and this way the compiler is what says so.
type Round =
    | InPlay of Play
    | Finished of Play * Ending

module Round =

    let dealt players =
        InPlay
            { Left = Row.Dealt
              Players = players
              ToPlay = 1
              Turn = 1 }

    let play =
        function
        | InPlay play -> play
        | Finished(play, _) -> play

    let left round = (play round).Left

    let seats round = (play round).Players

    let turn round = (play round).Turn

    let isOver =
        function
        | InPlay _ -> false
        | Finished _ -> true

    let active round = Seat.at (play round).ToPlay

    let after play =
        if play.ToPlay = play.Players then 1 else play.ToPlay + 1
