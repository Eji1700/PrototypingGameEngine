namespace TCModel.Life

type Move =
    | Step of generations: int
    | Toggle of cell: Cell
    | Clear

type Happening =
    | Ran of generations: int * reached: int * living: int
    | Settled of generation: int
    | DiedOut of generation: int
    | Toggled of cell: Cell * alive: bool
    | Swept of living: int

type Refusal =
    | NoSuchCell of said: Cell
    | NoSuchRun of said: int
    | NothingWouldChange of generation: int
    | NothingLeft

type Notice =
    | Happened of Happening
    | Refused of Refusal

module Turn =

    [<Literal>]
    let Longest = 100

    let private onwards world =
        let next = Grid.step world.Cells

        if next = world.Cells then
            None
        else
            Some
                { world with
                    Cells = next
                    Behind = (world.Cells :: world.Behind) |> List.truncate 2
                    Generation = world.Generation + 1 }

    let rec private running left ran world =
        if left = 0 then
            world, ran, None
        else
            match onwards world with
            | None -> world, ran, Some(Settled world.Generation)
            | Some world ->
                if World.isEmpty world then
                    world, ran + 1, Some(DiedOut world.Generation)
                else
                    running (left - 1) (ran + 1) world

    let asked move world =
        match move with
        | Toggle cell when not (Grid.holds cell) -> None, [ Refused(NoSuchCell cell) ]

        | Toggle cell ->
            let alive = not (World.alive cell world)

            Some
                { world with
                    Cells = (if alive then Set.add cell world.Cells else Set.remove cell world.Cells)
                    Behind = [] },
            [ Happened(Toggled(cell, alive)) ]

        | Clear when World.isEmpty world -> None, [ Refused NothingLeft ]

        | Clear ->
            Some
                { world with
                    Cells = Set.empty
                    Behind = [] },
            [ Happened(Swept(World.living world)) ]

        | Step generations when generations < 1 || generations > Longest -> None, [ Refused(NoSuchRun generations) ]

        | Step _ when World.isEmpty world -> None, [ Refused NothingLeft ]

        | Step generations ->
            match running generations 0 world with
            | _, 0, _ -> None, [ Refused(NothingWouldChange world.Generation) ]
            | world, ran, ending ->
                Some world,
                Happened(Ran(ran, world.Generation, World.living world))
                :: (ending |> Option.map Happened |> Option.toList)
