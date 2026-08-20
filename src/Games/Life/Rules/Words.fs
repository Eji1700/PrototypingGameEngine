namespace TCModel.Life

open TCModel.Engine

module Words =

    let cell = Grid.name

    let cells =
        function
        | 1 -> "1 cell"
        | many -> $"{many} cells"

    let generations =
        function
        | 1 -> "1 generation"
        | many -> $"{many} generations"

    let player (_: PlayerId) = "The watcher"

    let seated yours playerId =
        player playerId + (if yours then " (you)" else "")

    let event =
        function
        | Ran(ran, reached, living) -> $"Ran {generations ran} to generation {reached}, and {cells living} are alive."
        | Settled generation -> $"It has settled at generation {generation}: the next generation would be this one again."
        | DiedOut generation -> $"The last of them died at generation {generation}. Nothing can follow an empty board."
        | Toggled(where, alive) -> if alive then $"{cell where} comes alive." else $"{cell where} dies."
        | Swept living -> $"The board is swept: {cells living} gone, and an empty grid to draw on."

    let rejection =
        function
        | NoSuchCell said ->
            $"There is no cell {cell said}. The columns run a to {Grid.letters[Grid.Width - 1]} and the rows 1 to {Grid.Height}."
        | NoSuchRun said -> $"A run of {said}? Say a number of generations from 1 to {Turn.Longest}."
        | NothingWouldChange generation ->
            $"Nothing would change: at generation {generation} this board is a still life, and the next generation would be this one again. Turn a cell on, take a move back, or restart."
        | NothingLeft -> "There is nothing on the board. Turn some cells on - 'f7' - or restart for another soup."

    let command =
        Msg.written (function
            | Step 1 -> "step"
            | Step generations -> $"step {generations}"
            | Toggle where -> $"toggle {cell where}"
            | Clear -> "clear")

    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    let saidTo _ notice = said notice
