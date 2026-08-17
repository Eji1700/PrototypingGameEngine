namespace TCModel.Snake

open TCModel.Engine

/// Putting the game into English. The rules report what happened in their own terms;
/// everything a player actually reads is written here.
module Words =

    /// The letter a seat is, which is also the letter its snake is drawn with. At this game
    /// those really are one thing - a board of four snakes is unreadable unless what you are
    /// called is what you can see - so the seat is named after the letter rather than the
    /// letter being picked to suit a name.
    let letter seat =
        char (int 'a' + PlayerId.value seat - 1)

    let player seat =
        $"Snake {System.Char.ToUpperInvariant(letter seat)}"

    /// A seat as one screen names it, with the reader's own marked. Every view does this, and
    /// the game is unreadable without it over a network, where the seat to play is very often
    /// not the seat reading.
    let seated yours seat =
        player seat + (if yours then " (you)" else "")

    let direction =
        function
        | North -> "north"
        | East -> "east"
        | South -> "south"
        | West -> "west"

    /// Which way that is on the screen in front of somebody, for the places where a compass
    /// point is not the plainest thing to say.
    let towards =
        function
        | North -> "up"
        | East -> "right"
        | South -> "down"
        | West -> "left"

    let segments =
        function
        | 1 -> "1 segment"
        | many -> $"{many} segments"

    let steps =
        function
        | 1 -> "1 step"
        | many -> $"{many} steps"

    let eaten =
        function
        | 0 -> "nothing yet"
        | 1 -> "1 piece"
        | many -> $"{many} pieces"

    let fate =
        function
        | HitWall -> "ran into the wall"
        | HitItself -> "ran into itself"
        | HitAnother other -> $"ran into {player other}"
        | GaveUp -> "gave the game up"

    /// How it ended, in one clause. Short on purpose: this is a line of the log, and the line
    /// above it already says which snake stopped and how.
    let ending =
        function
        | LastMoving seat -> $"{player seat} is the last one moving"
        | NobodyMoving -> "nothing is left moving"

    /// The same, for the heading of a screen that has the board to hand - which is where the
    /// score belongs, because a game of one ends with a score and a game of four with a winner.
    let scored play over =
        let count seat =
            let snake = Session.snakeAt seat play
            $"{player seat} at {segments (Snake.length snake)}, having eaten {eaten snake.Eaten}"

        match over, play.Seats with
        | NobodyMoving, [ seat ] ->
            let snake = Session.snakeAt seat play
            let how = snake.Fate |> Option.map fate |> Option.defaultValue "stopped"
            $"{player seat} {how}, at {segments (Snake.length snake)} and {eaten snake.Eaten} eaten"
        | LastMoving seat, _ -> $"{ending over} - {count seat}"
        | NobodyMoving, _ -> ending over

    let event =
        function
        | Went(who, way) -> $"{player who} goes {direction way}."
        | Ate(who, pieces, grown) -> $"{player who} eats - {eaten pieces} now, and {segments grown} once it has grown."
        | Turned(who, way) -> $"{player who} turns {direction way}."
        | Stopped(who, how) -> $"{player who} {fate how}."
        | GameEnded over -> $"The game is over: {ending over}."

    let rejection =
        function
        | CannotTurnBack way ->
            $"A snake cannot turn back into its own neck, and {direction way} is the way this one came. The other three are open."
        | HasStopped who -> $"{player who} has stopped. Nothing steers it now."
        | NoSuchSnake who -> $"There is no {player who} at this table."
        | NotThisPace why -> $"Not at this way of playing: {why}."

    /// A message written the way a player types it. The record is kept in the same words the
    /// prompt takes, so a game can be read back and played again without a second language
    /// standing between the two.
    ///
    /// Only this game's own moves are written here. `undo`, `redo` and `restart` are the
    /// engine's words and are written once, by the engine, in `Msg.written`.
    /// At a game with two paces, in the words that pace takes. `go` is a step at a game of
    /// turns and a beat on a clock, and each way reads its own line back into its own move - so
    /// a record can only ever mean one of them. A steer names its snake outright, because a
    /// record that left that to be worked out from whose turn it was would be the record of a
    /// game that has turns.
    let command =
        Msg.written (function
            | Go way -> direction way
            | Onward
            | Beat -> "go"
            | Steer(seat, way) -> $"{letter seat} {direction way}"
            | Resign -> "resign")

    /// What this game itself said, and the whole of what it has to say for itself.
    let said =
        function
        | Happened happening -> event happening
        | Refused refusal -> rejection refusal

    /// The same, as much of it as one seat may know - which here is all of it. Every snake is
    /// on the board in front of everybody, and the only thing nobody knows is where the next
    /// piece of food will land, which is nobody's secret either.
    let saidTo _ notice = said notice
