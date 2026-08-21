namespace TCModel.Life

type Move =
    | Step of generations: int
    /// One generation, played by the clock rather than asked for. Quiet where a typed step
    /// speaks, and nothing at all where the rule is stopped or has nowhere left to go - which
    /// is what keeps a running board out of the log and out of the record.
    | Beat
    | Toggle of cell: Cell
    | Clear
    /// Start the rule, stop it, or - saying nothing about which - turn it the other way.
    | Running of on: bool option
    | Faster
    | Slower
    | Speed of notch: int

type Happening =
    | Ran of generations: int * reached: int * living: int
    | Started of generation: int
    | Halted of generation: int
    | Wound of notch: int
    | Settled of generation: int
    | DiedOut of generation: int
    | Toggled of cell: Cell * alive: bool
    | Swept of living: int

type Refusal =
    | NoSuchCell of said: Cell
    | NoSuchRun of said: int
    | NothingWouldChange of generation: int
    | NothingLeft
    | NoSuchSpeed of said: int

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

        // --- the clock ---------------------------------------------------------------------

        // Nothing at all rather than a refusal, which is what makes a stopped board cost nothing:
        // the engine leaves out a move the game neither took nor spoke about, so a clock beating
        // over a world nobody started writes no lines and draws no boards. The same answer serves
        // a board that has settled or died.
        | Beat when not world.Running || World.isEmpty world -> None, []

        | Beat ->
            match onwards world with
            | None -> None, []
            | Some world ->
                Some world,
                if World.isEmpty world then
                    [ Happened(DiedOut world.Generation) ]
                elif World.settled world then
                    [ Happened(Settled world.Generation) ]
                else
                    // The board already says which generation this is, three times a second - a
                    // line saying the same would be a log with nothing else in it.
                    []

        // --- and whether it is running -------------------------------------------------------

        | Running wanted ->
            let on = wanted |> Option.defaultValue (not world.Running)

            if on = world.Running then
                None, []
            else
                Some { world with Running = on }, [ Happened(if on then Started world.Generation else Halted world.Generation) ]

        | Speed notch when notch < World.Slowest || notch > World.Fastest -> None, [ Refused(NoSuchSpeed notch) ]

        | Speed notch when notch = world.Speed -> None, []
        | Faster when world.Speed = World.Fastest -> None, []
        | Slower when world.Speed = World.Slowest -> None, []

        | Faster
        | Slower
        | Speed _ ->
            let notch =
                match move with
                | Faster -> world.Speed + 1
                | Slower -> world.Speed - 1
                | Speed notch -> notch
                | _ -> world.Speed

            Some { world with Speed = notch }, [ Happened(Wound notch) ]
