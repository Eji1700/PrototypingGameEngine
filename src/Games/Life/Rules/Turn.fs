namespace Prototyping.Life

open Prototyping.Common

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
    | Wind of Winding

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

    let rec private running left ran world =
        if left = 0 then
            world, ran, None
        else
            match World.onwards world with
            | None -> world, ran, Some(Settled world.Generation)
            | Some world ->
                if World.isEmpty world then
                    world, ran + 1, Some(DiedOut world.Generation)
                else
                    running (left - 1) (ran + 1) world

    let asked move world =
        match move with
        | Toggle cell when not (Torus.holds cell) -> None, [ Refused(NoSuchCell cell) ]

        | Toggle cell ->
            let alive = not (World.alive cell world)

            Some(World.drawn (if alive then Set.add cell world.Cells else Set.remove cell world.Cells) world),
            [ Happened(Toggled(cell, alive)) ]

        | Clear when World.isEmpty world -> None, [ Refused NothingLeft ]

        | Clear -> Some(World.drawn Set.empty world), [ Happened(Swept(World.living world)) ]

        | Step generations when generations < 1 || generations > Longest -> None, [ Refused(NoSuchRun generations) ]

        | Step _ when World.isEmpty world -> None, [ Refused NothingLeft ]

        | Step generations ->
            match running generations 0 world with
            | _, 0, _ -> None, [ Refused(NothingWouldChange world.Generation) ]
            | world, ran, ending ->
                Some world,
                Happened(Ran(ran, world.Generation, World.living world))
                :: (ending |> Option.map Happened |> Option.toList)

        // Nothing at all rather than a refusal, which is what makes a stopped board cost nothing:
        // the engine leaves out a move the game neither took nor spoke about, so a clock beating
        // over a world nobody started writes no lines and draws no boards. The same answer serves
        // a board that has settled or died.
        | Beat when not world.Running || World.isEmpty world -> None, []

        | Beat ->
            match World.onwards world with
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

        | Running wanted ->
            let on = wanted |> Option.defaultValue (not world.Running)

            if on = world.Running then
                None, []
            else
                Some { world with Running = on }, [ Happened(if on then Started world.Generation else Halted world.Generation) ]

        | Wind(Winding.Speed notch) when not (Notch.holds notch) -> None, [ Refused(NoSuchSpeed notch) ]

        | Wind winding ->
            match Notch.wound winding world.Speed with
            | None -> None, []
            | Some notch -> Some { world with Speed = notch }, [ Happened(Wound notch) ]
