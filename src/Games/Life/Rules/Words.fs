namespace TCModel.Life

open TCModel.Engine

/// Putting the game into English. The rules report what happened in their own terms;
/// everything a player actually reads is written here.
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

    /// What the one seat is called.
    ///
    /// Not a player, and the word says so. Nobody is opposed at this game and nothing is
    /// taken in turn - the rule plays it, and the person at the keyboard decides when it runs
    /// and where to reach into it.
    let player (_: PlayerId) = "The watcher"

    /// A seat as one screen names it, with the reader's own marked. There is only ever one
    /// here, and it is still marked: a table over a wire draws a board for whoever is reading
    /// it, and this game is read at one of those too.
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

    /// A message written the way a player types it. The record is kept in the same words the
    /// prompt takes, so a game can be read back and played again without a second language
    /// standing between the two.
    /// Only this game's own moves are written here. `undo`, `redo` and `restart` are the
    /// engine's words and are written once, by the engine, in `Msg.written`.
    let command =
        Msg.written (function
            | Step 1 -> "step"
            | Step generations -> $"step {generations}"
            | Toggle where -> $"toggle {cell where}"
            | Clear -> "clear")

    /// What this game itself said, and the whole of what it has to say for itself.
    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    /// The same, as much of it as one seat may know - which here is all of it, and could
    /// hardly be otherwise: there is one seat, and the whole of the position is on the board
    /// in front of it.
    let saidTo _ notice = said notice
