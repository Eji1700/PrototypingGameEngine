namespace Prototyping.Life

open Prototyping.Common
open Prototyping.Engine

type World =
    {
        Cells: Cells

        /// The generation after this one, kept beside it. A board is asked whether it has settled
        /// far more often than it moves - every drawing asks - and with the answer already here
        /// that is a comparison rather than a step of the rule.
        Next: Cells

        Behind: Cells list
        Generation: int
        Running: bool
        Speed: int
        Rng: Rng
    }

/// Where the rule has got to with a board. One answer, read by the heading in a few words and
/// by the box beside the board in a sentence, so the two cannot disagree.
[<RequireQualifiedAccess>]
type Condition =
    | Empty
    | Settled
    | Beating
    | Going

module World =

    [<Literal>]
    let Density = 30

    let private holding cells world =
        { world with
            Cells = cells
            Next = Torus.step cells }

    let dealt seed =
        let cells, rng =
            Torus.all
            |> List.fold
                (fun (cells, rng) cell ->
                    let roll, rng = Rng.intBelow 100 rng
                    (if roll < Density then Set.add cell cells else cells), rng)
                (Set.empty, Rng.ofSeed seed)

        { Cells = cells
          Next = Torus.step cells
          Behind = []
          Generation = 0
          // Dealt running, because a soup nobody has asked to see is a soup that has done
          // nothing. Stopping it is a keypress.
          Running = true
          Speed = Notch.Ordinary
          Rng = rng }

    let living world = Set.count world.Cells

    let alive cell world = Set.contains cell world.Cells

    let isEmpty world = Set.isEmpty world.Cells

    let settled world = world.Next = world.Cells

    // A pattern that flips between two states: the generation before last is where it stands now.
    // `Behind` keeps only two, which is enough for this and not enough for longer periods.
    let beating world =
        match world.Behind with
        | _ :: before :: _ -> before = world.Cells
        | _ -> false

    let condition world =
        if isEmpty world then Condition.Empty
        elif settled world then Condition.Settled
        elif beating world then Condition.Beating
        else Condition.Going

    /// The board as somebody drew it. What was behind it no longer leads up to it, so a board
    /// just drawn on knows nothing about beating.
    let drawn cells world =
        { holding cells world with Behind = [] }

    /// One generation on, or nothing where the rule has nothing more to do.
    let onwards world =
        if settled world then
            None
        else
            Some
                { holding world.Next world with
                    Behind = (world.Cells :: world.Behind) |> List.truncate 2
                    Generation = world.Generation + 1 }


    let seat = Seat.at 1

    let active _ = seat

    let turn world = world.Generation

    let reseed world = Rng.next world.Rng |> fst
