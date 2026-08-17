namespace TCModel.Snake

open TCModel.Engine

/// How a snake stopped, which is the only thing that ever happens to one.
///
/// Four ways, and three of them are the same mistake at different speeds: the wall, somebody
/// else, and itself. The fourth is putting the game down.
type Fate =
    | HitWall
    | HitItself
    | HitAnother of PlayerId
    | GaveUp

/// One snake: where it is, which way it is going, how much of it is still to grow, how much
/// it has eaten, and whether it is still moving.
///
/// The body is head first, and it is a list rather than a set because the order *is* the
/// snake: the head is where it acts and the tail is the square it gives back each step. A set
/// of cells would draw the same picture and could not be moved.
type Snake =
    {
        Body: Cell list
        Facing: Direction
        /// Segments still owed from food already eaten. A snake grows by not giving its tail
        /// back, which is why eating and lengthening happen one step apart rather than at once.
        Growing: int
        Eaten: int
        /// How it stopped, if it has. `None` is a snake still moving, and it is the only thing
        /// in this game that says whose turn may come round again.
        Fate: Fate option
    }

module Snake =

    /// How long one starts, and what a piece of food is worth. Three is long enough to have a
    /// neck to turn back into, which is what makes the one refusal in this game mean anything.
    [<Literal>]
    let Length = 3

    [<Literal>]
    let PerFood = 1

    let head snake = List.head snake.Body

    let length snake = List.length snake.Body

    let isAlive snake = Option.isNone snake.Fate

    let covers cell snake = snake.Body |> List.contains cell

    /// The body without the square the tail is about to give back - what a snake will occupy
    /// once it has moved.
    ///
    /// Only ever asked of the snake that is moving, and that is the whole of why it exists:
    /// the square your own tail is in this moment is a square you may move into, because it
    /// will be empty by the time you are there. Nobody else's tail is going anywhere this
    /// turn, so nobody else's is asked.
    let behind snake =
        snake.Body |> List.truncate (length snake - 1)

    /// A snake at the deal: `Length` segments in a line, head first and the rest trailing
    /// back the way it came from.
    let dealt facing head =
        let back = Direction.opposite facing

        { Body = [ 2..Length ] |> List.scan (fun cell _ -> Board.along back cell) head
          Facing = facing
          Growing = 0
          Eaten = 0
          Fate = None }

    /// Move the head one square that way, and give the tail back unless there is growing to
    /// do. Whether the square was a legal one to move into is settled before this is called -
    /// a snake that has hit something does not move at all, it stops.
    let moved direction snake =
        { snake with
            Body =
                Board.along direction (head snake)
                :: (if snake.Growing > 0 then snake.Body else behind snake)
            Facing = direction
            Growing = max 0 (snake.Growing - 1) }

    /// What eating does, which is not to lengthen it: it owes itself a segment, and pays that
    /// on the next step by keeping its tail.
    let fed snake =
        { snake with
            Growing = snake.Growing + PerFood
            Eaten = snake.Eaten + 1 }

    let stopped fate snake = { snake with Fate = Some fate }
