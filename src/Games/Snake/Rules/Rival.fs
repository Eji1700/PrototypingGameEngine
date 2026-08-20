namespace TCModel.Snake

open TCModel.Common
open TCModel.Engine

type Skill =
    { Name: string
      Describe: string
      Counts: bool
      Slips: int }

type Rival = { Skill: Skill; Rng: Rng }

module Rival =

    let private ways snake =
        Direction.all |> List.filter (fun way -> way <> Direction.opposite snake.Facing)

    let private room play seat direction =
        let snake = Session.snakeAt seat play
        let start = Board.along direction (Snake.head snake)
        let blocked = Session.covered play

        let rec fill seen queue =
            match queue with
            | [] -> Set.count seen
            | cell :: rest when Set.contains cell seen -> fill seen rest
            | cell :: rest ->
                let seen = Set.add cell seen

                let onwards =
                    Direction.all
                    |> List.map (fun way -> Board.along way cell)
                    |> List.filter (fun next ->
                        Board.holds next
                        && not (Set.contains next blocked)
                        && not (Set.contains next seen))

                fill seen (onwards @ rest)

        if not (Board.holds start) || Set.contains start blocked then 0 else fill Set.empty [ start ]

    let private worth skill play seat direction =
        match Turn.ahead seat direction play with
        | Wall
        | Into _ -> None
        | there ->
            let snake = Session.snakeAt seat play
            let target = Board.along direction (Snake.head snake)

            let space =
                if skill.Counts then room play seat direction else Board.Width * Board.Height

            let nearer =
                match play.Food with
                | Some food -> -(Board.apart target food)
                | None -> 0

            Some((if space >= Snake.length snake then 1 else 0), (if there = Food then 1 else 0), nearer, space)

    let plays session rival =
        match session with
        | Finished _ -> None
        | InPlay play ->

        let seat = play.ToPlay
        let open' = ways (Session.snakeAt seat play)

        let rated =
            open'
            |> List.choose (fun way -> worth rival.Skill play seat way |> Option.map (fun worth -> way, worth))

        let slip, rng = Rng.intBelow 100 rival.Rng

        let wanted =
            if List.isEmpty rated then
                open'
            elif slip < rival.Skill.Slips then
                rated |> List.map fst
            else
                let best = rated |> List.map snd |> List.max
                rated |> List.filter (fun (_, worth) -> worth = best) |> List.map fst

        let picked, rng = Rng.intBelow (List.length wanted) rng

        Some(Go wanted[picked], { rival with Rng = rng })


    let easy =
        { Name = "easy"
          Describe = "heads for the food, looks no further than the next square, and often goes elsewhere anyway"
          Counts = false
          Slips = 35 }

    let medium =
        { Name = "medium"
          Describe = "heads for the food and will not walk into anything, which is not the same as staying alive"
          Counts = false
          Slips = 5 }

    let hard =
        { Name = "hard"
          Describe = "counts the room a step leaves it in before taking it, and will pass up food to keep some"
          Counts = true
          Slips = 0 }

    let all = [ easy; medium; hard ]

    let names = Machines.named (fun skill -> skill.Name) all

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    let seating (seed: uint64) sitting =
        Machines.seating [ for place in 1 .. Session.Most -> Seat.at place ] seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng })
