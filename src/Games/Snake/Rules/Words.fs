namespace Prototyping.Snake

open Prototyping.Common
open Prototyping.Engine

module Words =

    let letter seat =
        char (int 'a' + PlayerId.value seat - 1)

    let player seat =
        $"Snake {System.Char.ToUpperInvariant(letter seat)}"

    let direction =
        function
        | North -> "north"
        | East -> "east"
        | South -> "south"
        | West -> "west"

    let towards =
        function
        | North -> "up"
        | East -> "right"
        | South -> "down"
        | West -> "left"

    let segments = Counting.several "segment" "segments"

    let steps = Counting.several "step" "steps"

    let eaten = Counting.orNone "nothing yet" "piece" "pieces"

    let fate =
        function
        | HitWall -> "ran into the wall"
        | HitItself -> "ran into itself"
        | HitAnother other -> $"ran into {player other}"
        | GaveUp -> "gave the game up"

    let ending =
        function
        | LastMoving seat -> $"{player seat} is the last one moving"
        | NobodyMoving -> "nothing is left moving"

    let scored play over =
        let tally seat =
            let snake = Session.snakeAt seat play
            $"at {segments (Snake.length snake)}, having eaten {eaten snake.Eaten}"

        match over, play.Seats with
        | NobodyMoving, [ seat ] ->
            let how =
                (Session.snakeAt seat play).Fate
                |> Option.map fate
                |> Option.defaultValue "stopped"

            $"{player seat} {how}, {tally seat}"
        | LastMoving seat, _ -> $"{ending over}, {tally seat}"
        | NobodyMoving, _ -> ending over

    let event =
        function
        | Went(who, way) -> $"{player who} goes {direction way}."
        | Ate(who, pieces, grown) -> $"{player who} eats - {eaten pieces} now, and {segments grown} once it has grown."
        | Turned(who, way) -> $"{player who} turns {direction way}."
        | Wound notch -> $"The clock is wound to {notch} of {Notch.Fastest}."
        | Stopped(who, how) -> $"{player who} {fate how}."
        | GameEnded over -> $"The game is over: {ending over}."

    let rejection =
        function
        | CannotTurnBack way ->
            $"A snake cannot turn back into its own neck, and {direction way} is where this one's neck is. The other three are open."
        | HasStopped who -> $"{player who} has stopped. Nothing steers it now."
        | NoSuchSnake who -> $"There is no {player who} at this table."
        | NoSuchSpeed said -> Notch.unknown said
        | NotThisPace Clock ->
            "Not in this way of playing - a direction is a turn of the head here, and the beat is what moves anybody."
        | NotThisPace Turns ->
            "Not in this way of playing - this way of playing takes a step when you say a direction, and waits for you in between."

    let command =
        Msg.written (function
            | Go way -> direction way
            | Onward
            | Beat -> "go"
            | Steer(seat, way) -> $"{letter seat} {direction way}"
            | Wind winding -> Notch.written winding
            | Resign -> "resign")

    let said =
        function
        | Happened happening -> event happening
        | Refused refusal -> rejection refusal

    let saidTo _ notice = said notice
