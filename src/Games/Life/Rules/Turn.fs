namespace TCModel.Life

/// Everything a player may ask this game to do, which is three things: let the rule run, reach
/// in and change a cell, or sweep the board and start drawing on it.
///
/// There is no move that takes anything from anybody, and none that ends the game. Which is
/// the shape of this game rather than a gap in it - a player here is not opposed, they are
/// winding a thing up and watching it go.
type Move =
    /// Let the rule run, that many generations, stopping early if there is nothing left to
    /// happen. One is what `step` means; the rest is watching a run go by without typing the
    /// same word forty times.
    | Step of generations: int
    /// Turn one cell out, or on. The only way anything gets onto the board that the deal did
    /// not put there.
    | Toggle of cell: Cell
    /// Sweep the lot, for somebody who would rather draw than watch. Not an ending and not a
    /// restart: the generation stands where it stood, and the next thing typed is a cell.
    | Clear

type Happening =
    /// How far it actually ran, where that leaves it, and how many are left alive.
    | Ran of generations: int * reached: int * living: int
    /// It stopped short: the next generation would be this one again.
    | Settled of generation: int
    /// Or it stopped short because there was nothing left to have a next generation.
    | DiedOut of generation: int
    | Toggled of cell: Cell * alive: bool
    | Swept of living: int

type Refusal =
    | NoSuchCell of said: Cell
    | NoSuchRun of said: int
    /// A run asked for at a board that has already stopped moving, or at one with nothing on
    /// it. Nothing happened, so nothing is written into the history - but it was asked, so it
    /// is said.
    | NothingWouldChange of generation: int
    | NothingLeft

/// What this game has to say, and the whole of it. Nothing about undo, nothing about a line
/// nobody could read: those are the engine's and are said once, above, in words that suit any
/// game.
type Notice =
    | Happened of Happening
    | Refused of Refusal

/// How a turn goes.
///
/// The rule itself is `Grid.step` and is four lines. What is here is the other half of a
/// turn - how far a run goes, what stops it, and what a board that has stopped moving does
/// with somebody asking it to move again.
module Turn =

    /// The longest run one move may ask for. A cap rather than a rule of the game: a person
    /// typing `step 100000` at a prompt is waiting on a board they cannot see any of, and a
    /// hundred generations of a board this size is a blink.
    [<Literal>]
    let Longest = 100

    /// One generation on, or nothing at all where the rule would leave the board exactly as it
    /// found it. `None` is a still life - or an empty board, which is the same answer for the
    /// same reason - and it is the only thing that stops a run short of what was asked.
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

    /// A run: where it got to, how many generations it really moved, and what stopped it if
    /// anything did.
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

    /// What the engine asks of a game: a move and where it stands, and the position it left
    /// along with whatever there is to say.
    ///
    /// Total, like every game's: `None` for the position means nothing moved, and the notices
    /// say why. A refusal is something this game *says*, not something that breaks it, which
    /// is what lets every table above be a fold.
    let asked move world =
        match move with
        // A cell off the board can only be typed or read off a record - the board draws none -
        // and either way it is a fair thing to have asked, so it is answered rather than
        // swallowed on the way in.
        | Toggle cell when not (Grid.holds cell) -> None, [ Refused(NoSuchCell cell) ]

        | Toggle cell ->
            let alive = not (World.alive cell world)

            Some
                { world with
                    Cells = (if alive then Set.add cell world.Cells else Set.remove cell world.Cells)
                    // A board somebody has just drawn on is not two generations into anything.
                    // What was behind it was a run that no longer leads here, and leaving it
                    // would have the screen call a board that has just been altered by hand a
                    // beating one.
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
            // It never moved at all, which at a board with something on it can only mean one
            // thing: it is a still life, and was one before the move was asked for.
            | _, 0, _ -> None, [ Refused(NothingWouldChange world.Generation) ]
            | world, ran, ending ->
                Some world,
                Happened(Ran(ran, world.Generation, World.living world))
                :: (ending |> Option.map Happened |> Option.toList)
