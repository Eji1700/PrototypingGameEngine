namespace Prototyping.MyGame

type Move =
    | Take of count: int
    | Resign

type Happening =
    | Taken of seat: int * count: int * left: int
    | GameEnded of Ending

type Refusal =
    | NotThatMany of said: int
    | NotSoManyLeft of said: int * left: int

type Notice =
    | Happened of Happening
    | Refused of Refusal

module Turn =

    /// The whole of how this game is played, and it cannot fail: a move the rules will not take
    /// comes back as no new state and a notice saying why. That is what lets every table above be
    /// a fold, and every rule here be checked without a keyboard or a screen.
    let asked move round =
        match round, move with
        | Finished _, _ -> None, []

        | InPlay play, Resign ->
            let ending = Abandoned play.ToPlay
            Some(Finished(play, ending)), [ Happened(GameEnded ending) ]

        | InPlay _, Take count when not (Row.takeable count) -> None, [ Refused(NotThatMany count) ]

        | InPlay play, Take count when count > play.Left -> None, [ Refused(NotSoManyLeft(count, play.Left)) ]

        | InPlay play, Take count ->
            let left = play.Left - count
            let taken = Happened(Taken(play.ToPlay, count, left))

            if left = 0 then
                let ending = TookTheLast play.ToPlay
                Some(Finished({ play with Left = 0 }, ending)), [ taken; Happened(GameEnded ending) ]
            else
                Some(
                    InPlay
                        { play with
                            Left = left
                            ToPlay = Round.after play
                            Turn = play.Turn + 1 }
                ),
                [ taken ]
