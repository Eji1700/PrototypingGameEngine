namespace TCModel.Snake

open TCModel.Common
open TCModel.Engine

type Ending =
    | LastMoving of PlayerId
    | NobodyMoving

type Pace =
    | Turns
    | Clock

type Play =
    { Snakes: Map<PlayerId, Snake>
      Seats: PlayerId list
      Food: Cell option
      ToPlay: PlayerId
      Turn: int
      Pace: Pace
      Speed: int
      Rng: Rng }

type Session =
    | InPlay of Play
    | Finished of Play * Ending

module Session =

    [<Literal>]
    let Fewest = 1

    [<Literal>]
    let Most = 4

    [<Literal>]
    let Slowest = 1

    [<Literal>]
    let Fastest = 9

    [<Literal>]
    let Ordinary = 5

    let play =
        function
        | InPlay play -> play
        | Finished(play, _) -> play

    let snakeAt seat play = Map.find seat play.Snakes

    let snakes play =
        play.Seats |> List.map (fun seat -> seat, snakeAt seat play)

    let living play =
        play.Seats |> List.filter (fun seat -> Snake.isAlive (snakeAt seat play))

    let covered play =
        play.Snakes
        |> Map.toSeq
        |> Seq.collect (fun (_, snake) -> snake.Body)
        |> Set.ofSeq

    let free play =
        let taken = covered play
        Board.all |> List.filter (fun cell -> not (Set.contains cell taken))

    let feeding play =
        match free play with
        | [] -> { play with Food = None }
        | cells ->
            let picked, rng = Rng.intBelow (List.length cells) play.Rng

            { play with
                Food = Some cells[picked]
                Rng = rng }


    let private start players place =
        let row = (place + 1) * Board.Height / (players + 1)

        if place % 2 = 0 then
            Snake.dealt East { Row = row; Column = 1 + Snake.Length }
        else
            Snake.dealt
                West
                { Row = row
                  Column = Board.Width - Snake.Length }

    let dealt pace players seed =
        let seats = [ for place in 1..players -> Seat.at place ]

        let snakes =
            seats |> List.mapi (fun place seat -> seat, start players place) |> Map.ofList

        InPlay(
            feeding
                { Snakes = snakes
                  Seats = seats
                  Food = None
                  ToPlay = List.head seats
                  Turn = 1
                  Pace = pace
                  Speed = Ordinary
                  Rng = Rng.ofSeed seed }
        )


    let active session = (play session).ToPlay

    let turn session = (play session).Turn

    let seats session = List.length (play session).Seats

    let isOver =
        function
        | InPlay _ -> false
        | Finished _ -> true

    let ending =
        function
        | InPlay _ -> None
        | Finished(_, ending) -> Some ending

    let reseed session = Rng.next (play session).Rng |> fst


    let onwards play =
        let count = List.length play.Seats
        let at = play.Seats |> List.findIndex ((=) play.ToPlay)

        let rec next step =
            let index = (at + step) % count

            if Snake.isAlive (snakeAt play.Seats[index] play) then Some index
            elif step >= count then None
            else next (step + 1)

        match next 1 with
        | None -> play
        | Some index ->
            { play with
                ToPlay = play.Seats[index]
                Turn = (if index <= at then play.Turn + 1 else play.Turn) }

    let foremost play =
        match living play with
        | seat :: _ -> seat
        | [] -> play.ToPlay

    let finished play =
        match living play with
        | [] -> Some NobodyMoving
        | [ last ] when List.length play.Seats > 1 -> Some(LastMoving last)
        | _ -> None
