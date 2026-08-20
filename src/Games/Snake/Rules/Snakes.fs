namespace TCModel.Snake

open TCModel.Engine

type Fate =
    | HitWall
    | HitItself
    | HitAnother of PlayerId
    | GaveUp

type Snake =
    { Body: Cell list
      Facing: Direction
      Growing: int
      Eaten: int
      Fate: Fate option }

module Snake =

    [<Literal>]
    let Length = 3

    [<Literal>]
    let PerFood = 1

    let head snake = List.head snake.Body

    let neck snake = snake.Body |> List.tryItem 1

    let length snake = List.length snake.Body

    let isAlive snake = Option.isNone snake.Fate

    let covers cell snake = snake.Body |> List.contains cell

    let behind snake =
        snake.Body |> List.truncate (length snake - 1)

    let dealt facing head =
        let back = Direction.opposite facing

        { Body = [ 2..Length ] |> List.scan (fun cell _ -> Board.along back cell) head
          Facing = facing
          Growing = 0
          Eaten = 0
          Fate = None }

    let moved direction snake =
        { snake with
            Body =
                Board.along direction (head snake)
                :: (if snake.Growing > 0 then snake.Body else behind snake)
            Facing = direction
            Growing = max 0 (snake.Growing - 1) }

    let fed snake =
        { snake with
            Growing = snake.Growing + PerFood
            Eaten = snake.Eaten + 1 }

    let stopped fate snake = { snake with Fate = Some fate }
