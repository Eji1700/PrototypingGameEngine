namespace TCModel.Life

open TCModel.Common
open TCModel.Engine

/// Where the game stands, which is the one thing the engine ever asks about.
///
/// One case and no ending. Every other game here has a `Finished` beside its `InPlay`, because
/// every other game here is won: there is a line of three, a last centre taken, a player who
/// walked away. This one is not played against anybody, cannot be won, and does not finish -
/// it only ever arrives somewhere the rule has nothing more to do, which is a fact about the
/// cells rather than a second shape for the state to be in.
type World =
    {
        Cells: Cells
        /// The two generations behind this one, newest first.
        ///
        /// Carried because a still board and a beating one look identical in a single frame,
        /// and the difference is the only thing anybody watching actually wants to know. A
        /// blinker is not stuck, and a block is: one of them is worth going on watching.
        Behind: Cells list
        /// Counted from the deal, which is generation nought - the soup as it was drawn,
        /// before the rule has been applied to it even once.
        Generation: int
        /// The generator the soup was drawn from, kept so that a restart can draw the next
        /// seed out of this game rather than off the clock. Nothing after the deal is random,
        /// so it never moves again.
        Rng: Rng
    }

module World =

    /// Out of a hundred, how much of the board the deal fills. Low enough that the pattern is
    /// made of shapes rather than of one enormous blob, high enough that something survives
    /// the first few generations - which between them is the whole of what a soup is for.
    [<Literal>]
    let Density = 30

    /// A fresh world: every square of the board asked of the generator once, in order, so that
    /// the same seed is the same soup - on this machine, on another, and a year from now off a
    /// record.
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

    /// Whether the rule has nothing left to work on.
    ///
    /// Not an ending, and the difference is worth the sentence. An empty board is the one
    /// position no generation can follow - but it is still a board, and drawing a glider on
    /// one and letting it go is half of what anybody does with this game. So the *game* is
    /// never over here, and what this answers is only whether the rule has anything to do.
    let isEmpty world = Set.isEmpty world.Cells

    /// Whether the next generation is this one again. A block, a beehive, a loaf: nothing more
    /// will happen, and a game that let somebody go on asking would be a game answering with
    /// the same board for ever and never saying why.
    let settled world = Grid.step world.Cells = world.Cells

    /// Whether it is back where it was two generations ago - a blinker, a toad, a beacon. It
    /// will do this for ever too, but it is doing something, so it is said rather than stopped.
    let beating world =
        match world.Behind with
        | _ :: before :: _ -> before = world.Cells
        | _ -> false

    // --- and the seat ---------------------------------------------------------------------
    //
    // One, and it is not a player's. Nobody is opposed here and nothing is taken in turn: the
    // rule plays the game and the person watching decides when it runs and where to poke it.
    // The engine wants a seat to act all the same - a record says who asked for a move, a
    // table hands out a seat - so there is one, and it is always this one.

    let seat = Seat.at 1

    let active _ = seat

    let turn world = world.Generation

    let reseed world = Rng.next world.Rng |> fst
