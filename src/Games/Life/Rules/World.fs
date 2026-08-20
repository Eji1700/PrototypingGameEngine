namespace TCModel.Life

open TCModel.Common
open TCModel.Engine

type World =
    { Cells: Cells
      Behind: Cells list
      Generation: int
      Rng: Rng }

module World =

    [<Literal>]
    let Density = 30

    let dealt seed =
        let cells, rng =
            Grid.all
            |> List.fold
                (fun (cells, rng) cell ->
                    let roll, rng = Rng.intBelow 100 rng
                    (if roll < Density then Set.add cell cells else cells), rng)
                (Set.empty, Rng.ofSeed seed)

        { Cells = cells
          Behind = []
          Generation = 0
          Rng = rng }

    let living world = Set.count world.Cells

    let alive cell world = Set.contains cell world.Cells

    let isEmpty world = Set.isEmpty world.Cells

    let settled world = Grid.step world.Cells = world.Cells

    let beating world =
        match world.Behind with
        | _ :: before :: _ -> before = world.Cells
        | _ -> false


    let seat = Seat.at 1

    let active _ = seat

    let turn world = world.Generation

    let reseed world = Rng.next world.Rng |> fst
