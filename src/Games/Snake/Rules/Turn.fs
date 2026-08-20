namespace TCModel.Snake

open TCModel.Engine

type Move =
    | Go of Direction
    | Onward
    | Steer of seat: PlayerId * way: Direction
    | Beat
    | Faster
    | Slower
    | Speed of notch: int
    | Resign

type Happening =
    | Went of PlayerId * Direction
    | Ate of PlayerId * eaten: int * grown: int
    | Turned of PlayerId * Direction
    | Wound of notch: int
    | Stopped of PlayerId * Fate
    | GameEnded of Ending

type Refusal =
    | CannotTurnBack of Direction
    | HasStopped of PlayerId
    | NoSuchSnake of PlayerId
    | NoSuchSpeed of said: int
    | NotThisPace of said: string

type Notice =
    | Happened of Happening
    | Refused of Refusal

type Ahead =
    | Wall
    | Into of PlayerId
    | Food
    | Clear

module Turn =

    let private into seat target growing play =
        Session.snakes play
        |> List.tryPick (fun (other, snake) ->
            let body = if other = seat && not growing then Snake.behind snake else snake.Body

            if List.contains target body then Some other else None)

    let private backwards seat way play =
        let snake = Session.snakeAt seat play
        Snake.neck snake = Some(Board.along way (Snake.head snake))

    let ahead seat direction play =
        let snake = Session.snakeAt seat play
        let target = Board.along direction (Snake.head snake)
        let eating = play.Food = Some target

        if not (Board.holds target) then
            Wall
        else
            match into seat target (eating || snake.Growing > 0) play with
            | Some other -> Into other
            | None -> if eating then Food else Clear

    let private stepping seat direction play =
        let snake = Session.snakeAt seat play
        let there = ahead seat direction play

        let stops fate =
            { play with
                Snakes = Map.add seat (Snake.stopped fate snake) play.Snakes },
            [ Happened(Stopped(seat, fate)) ]

        match there with
        | Wall -> stops HitWall
        | Into other when other = seat -> stops HitItself
        | Into other -> stops (HitAnother other)
        | Food
        | Clear ->

            let moved = Snake.moved direction snake
            let fed = if there = Food then Snake.fed moved else moved

            let play =
                { play with
                    Snakes = Map.add seat fed play.Snakes }

            if there = Food then
                Session.feeding play,
                [ Happened(Went(seat, direction))
                  Happened(Ate(seat, fed.Eaten, Snake.length fed + fed.Growing)) ]
            else
                play, [ Happened(Went(seat, direction)) ]

    let private taking seat direction play =
        let played, told = stepping seat direction play

        match Session.finished played with
        | Some ending -> Some(Finished(played, ending)), told @ [ Happened(GameEnded ending) ]
        | None -> Some(InPlay(Session.onwards played)), told


    let private beating play =
        let moving = Session.living play

        let headed =
            moving
            |> List.map (fun seat ->
                let snake = Session.snakeAt seat play
                seat, Board.along snake.Facing (Snake.head snake))

        let eater =
            headed
            |> List.tryFind (fun (_, target) -> play.Food = Some target)
            |> Option.map fst

        let growing seat =
            let snake = Session.snakeAt seat play
            snake.Growing > 0 || eater = Some seat

        let standing =
            Session.snakes play
            |> List.collect (fun (seat, snake) ->
                if Snake.isAlive snake && not (growing seat) then Snake.behind snake else snake.Body)
            |> Set.ofList

        let fate seat target =
            if not (Board.holds target) then
                Some HitWall
            elif Set.contains target standing then
                Session.snakes play
                |> List.tryPick (fun (other, snake) ->
                    let body =
                        if Snake.isAlive snake && not (growing other) then Snake.behind snake else snake.Body

                    if List.contains target body then Some other else None)
                |> Option.map (fun other -> if other = seat then HitItself else HitAnother other)
                |> Option.orElse (Some HitItself)
            else
                headed
                |> List.tryPick (fun (other, theirs) -> if other <> seat && theirs = target then Some(HitAnother other) else None)

        let stopping =
            headed
            |> List.choose (fun (seat, target) -> fate seat target |> Option.map (fun how -> seat, how))

        let told = stopping |> List.map (fun (seat, how) -> Happened(Stopped(seat, how)))

        let snakes =
            headed
            |> List.fold
                (fun snakes (seat, _) ->
                    let snake = Session.snakeAt seat play

                    match stopping |> List.tryFind (fst >> (=) seat) with
                    | Some(_, how) -> Map.add seat (Snake.stopped how snake) snakes
                    | None ->
                        let moved = Snake.moved snake.Facing snake
                        Map.add seat (if eater = Some seat then Snake.fed moved else moved) snakes)
                play.Snakes

        let played =
            { play with
                Snakes = snakes
                Turn = play.Turn + 1 }

        let eaten =
            match eater with
            | Some seat when not (stopping |> List.exists (fst >> (=) seat)) ->
                let snake = Session.snakeAt seat played
                [ Happened(Ate(seat, snake.Eaten, Snake.length snake + snake.Growing)) ]
            | Some _
            | None -> []

        let played =
            if eater.IsSome && not (List.isEmpty eaten) then Session.feeding played else played

        { played with
            ToPlay = Session.foremost played },
        told @ eaten

    let asked move session =
        let ending play told =
            match Session.finished play with
            | Some over -> Some(Finished(play, over)), told @ [ Happened(GameEnded over) ]
            | None -> Some(InPlay play), told

        match session, move with
        | Finished _, _ -> None, []

        | InPlay play, Resign when play.Pace = Clock ->
            let stopping = Session.living play

            let stopped =
                { play with
                    Snakes =
                        stopping
                        |> List.fold
                            (fun snakes seat -> Map.add seat (Snake.stopped GaveUp (Session.snakeAt seat play)) snakes)
                            play.Snakes }

            ending stopped (stopping |> List.map (fun seat -> Happened(Stopped(seat, GaveUp))))

        | InPlay play, Resign ->
            let seat = play.ToPlay

            let stopped =
                { play with
                    Snakes = Map.add seat (Snake.stopped GaveUp (Session.snakeAt seat play)) play.Snakes }

            let told = [ Happened(Stopped(seat, GaveUp)) ]

            match Session.finished stopped with
            | Some over -> Some(Finished(stopped, over)), told @ [ Happened(GameEnded over) ]
            | None -> Some(InPlay(Session.onwards stopped)), told


        | InPlay play, (Go _ | Onward) when play.Pace = Clock ->
            None, [ Refused(NotThisPace "a direction is a turn of the head here, and the beat is what moves anybody") ]

        | InPlay play, Onward -> taking play.ToPlay (Session.snakeAt play.ToPlay play).Facing play

        | InPlay play, Go direction when backwards play.ToPlay direction play -> None, [ Refused(CannotTurnBack direction) ]

        | InPlay play, Go direction -> taking play.ToPlay direction play


        | InPlay play, (Steer _ | Beat | Faster | Slower | Speed _) when play.Pace = Turns ->
            None,
            [ Refused(NotThisPace "this way of playing takes a step when you say a direction, and waits for you in between") ]


        | InPlay play, Speed notch when notch < Session.Slowest || notch > Session.Fastest -> None, [ Refused(NoSuchSpeed notch) ]

        | InPlay play, Speed notch when notch = play.Speed -> None, []
        | InPlay play, Faster when play.Speed = Session.Fastest -> None, []
        | InPlay play, Slower when play.Speed = Session.Slowest -> None, []

        | InPlay play, (Faster | Slower | Speed _ as winding) ->
            let notch =
                match winding with
                | Faster -> play.Speed + 1
                | Slower -> play.Speed - 1
                | Speed notch -> notch
                | _ -> play.Speed

            Some(InPlay { play with Speed = notch }), [ Happened(Wound notch) ]

        | InPlay play, Beat ->
            let played, told = beating play
            ending played told

        | InPlay play, Steer(seat, _) when not (List.contains seat play.Seats) -> None, [ Refused(NoSuchSnake seat) ]

        | InPlay play, Steer(seat, _) when not (Snake.isAlive (Session.snakeAt seat play)) -> None, [ Refused(HasStopped seat) ]

        | InPlay play, Steer(seat, way) when backwards seat way play -> None, [ Refused(CannotTurnBack way) ]

        | InPlay play, Steer(seat, way) when way = (Session.snakeAt seat play).Facing -> None, []

        | InPlay play, Steer(seat, way) ->
            let snake = Session.snakeAt seat play

            Some(
                InPlay
                    { play with
                        Snakes = Map.add seat { snake with Facing = way } play.Snakes }
            ),
            [ Happened(Turned(seat, way)) ]
