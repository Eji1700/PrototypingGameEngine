namespace TCModel.Cascade

open TCModel.Engine

module Words =

    let cell = Board.name

    let cells =
        function
        | 1 -> "1 cell"
        | many -> $"{many} cells"

    let turns =
        function
        | 1 -> "1 turn"
        | many -> $"{many} turns"

    let touches =
        function
        | 0 -> "no touches"
        | 1 -> "1 touch"
        | many -> $"{many} touches"

    let waves =
        function
        | 1 -> "1 wave"
        | many -> $"{many} waves"

    let shape =
        function
        | Rank row -> $"row {row}"
        | File column -> $"column {Board.letters[column - 1]}"
        | Square at -> $"the square at {cell at}"

    let shapes =
        function
        | [] -> "nothing whole"
        | made -> made |> List.map shape |> String.concat ", "

    let way =
        function
        | North -> "up"
        | East -> "right"
        | South -> "down"
        | West -> "left"

    let facing =
        function
        | UpRight -> "up and right"
        | RightDown -> "right and down"
        | DownLeft -> "down and left"
        | LeftUp -> "left and up"

    let player (_: PlayerId) = "The hand"

    let seated yours playerId =
        player playerId + (if yours then " (you)" else "")

    let private settling (run: Run) =
        let whole =
            match run.Made with
            | [] -> "."
            | made -> $", bringing up {shapes made}."

        $"The cascade from {cell run.From} came to rest after {turns run.Rotations} over {waves run.Waves}{whole}"

    let event =
        function
        | Touched where -> $"{cell where} begins turning."
        | CameUp(what, at) -> $"{shape what} has turned over, {turns at} in."
        | Settled run -> settling run
        | Halted run ->
            settling run
            + $" It was stopped there: a cascade is held to {Session.MostRotations} turns over {Session.MostWaves} waves."
        | Wound notch -> $"A quarter turn now takes {Session.quarter notch}ms. Notch {notch}."
        | GaveIn left -> $"Put down with {touches left} unspent."
        | GameEnded tally ->
            $"{turns tally.Rotations} in all, over {touches tally.Touches}: {tally.Lines} whole rows or columns, and {tally.Squares} squares."

    let rejection =
        function
        | StillTurning 1 -> "A cell is still turning. Nothing may be touched until the board comes to rest."
        | StillTurning turning -> $"{turning} cells are still turning. Nothing may be touched until the board comes to rest."
        | NoneLeft -> $"No touches left. A board is worth {Session.Touches} - 'restart' deals another."
        | NoSuchCell said ->
            $"There is no cell {cell said}. The columns run a to {Board.letters[Board.Width - 1]} and the rows 1 to {Board.Height}."
        | NoSuchSpeed said -> $"A speed of {said}? The notches run from {Session.Slowest} to {Session.Fastest}."

    let command =
        Msg.written (function
            | Touch where -> cell where
            | Point North -> "up"
            | Point East -> "right"
            | Point South -> "down"
            | Point West -> "left"
            | Press -> "press"
            | Beat -> "beat"
            | Faster -> "faster"
            | Slower -> "slower"
            | Speed notch -> $"speed {notch}"
            | Resign -> "resign")

    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    let saidTo _ notice = said notice
