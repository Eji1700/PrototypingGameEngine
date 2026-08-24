namespace Prototyping.Life

open Prototyping.Common
open Prototyping.Engine

type World =
    {
        Cells: Cells
        Behind: Cells list
        Generation: int
        /// Whether the rule is running of its own accord. The clock beats either way; a world that
        /// is not running answers a beat with nothing, which the engine leaves out of the record.
        Running: bool
        /// How fast it is wanted, from 1 to 9. What a notch is worth in time is `Offer`'s.
        Speed: int
        Rng: Rng
    }

module World =

    [<Literal>]
    let Density = 30

    [<Literal>]
    let Slowest = 1

    [<Literal>]
    let Fastest = 9

    [<Literal>]
    let Ordinary = 5

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
          // Dealt running, because a soup nobody has asked to see is a soup that has done
          // nothing. Stopping it is a keypress.
          Running = true
          Speed = Ordinary
          Rng = rng }

    let living world = Set.count world.Cells

    let alive cell world = Set.contains cell world.Cells

    let isEmpty world = Set.isEmpty world.Cells

    let settled world = Grid.step world.Cells = world.Cells

    // A pattern that flips between two states: the generation before last is where it stands now.
    // `Behind` keeps only two, which is enough for this and not enough for longer periods.
    let beating world =
        match world.Behind with
        | _ :: before :: _ -> before = world.Cells
        | _ -> false


    let seat = Seat.at 1

    let active _ = seat

    let turn world = world.Generation

    let reseed world = Rng.next world.Rng |> fst
