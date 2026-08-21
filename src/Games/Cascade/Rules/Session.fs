namespace TCModel.Cascade

open TCModel.Common
open TCModel.Engine

type Standing =
    { Facing: Facing
      Turned: int
      Landed: int }

/// A shape that has just come up. Kept for a few beats after it does, because the light that runs
/// along a shape is drawn from how long ago that was.
type Lit = { Shape: Shape; Since: int }

/// One activation: everything a touch set off, from the touch to the board coming to rest.
type Run =
    { From: Cell
      Waves: int
      Rotations: int
      Rotated: Set<Cell>
      Made: Shape list
      Halted: bool }

/// The same four numbers, totalled over every activation there has been.
type Tally =
    { Touches: int
      Rotations: int
      Lines: int
      Squares: int }

/// What the board is making a noise about, on this beat and no other.
///
/// State rather than something read out of the notices: a table reads it *after* a move, and two
/// boards a minute apart hold the same cells - only this says which has just had a wave land on
/// it. It is also what makes a game taken up from a record sound like the one it was saved from.
///
/// At most two, and never three piled onto what was one moment: what the wave did, and what the
/// board is now.
type Sounding =
    | Landing
    | Squared
    | Lined
    | Resting
    | Ending

type Play =
    {
        Cells: Map<Cell, Standing>
        Turning: Set<Cell>

        /// Where the hand is resting. In the state rather than beside it because a key press here
        /// stands for a line the game reads - so moving the hand is an ordinary move, recorded and
        /// undoable, and a board taken up from a record comes back with the hand where it was left.
        At: Cell
        Wave: int
        Left: int
        Speed: int
        Run: Run option
        Tally: Tally
        Lit: Lit list
        Sounding: Sounding list

        /// The beat the board was last struck on, if the strike still shows. A band of light run
        /// down the whole board is what a reader with no colour at all - `plain` - has in place of
        /// hearing the bell.
        Struck: int option

        Rng: Rng
    }

type Ending =
    | Spent
    | GaveUp

type Session =
    | InPlay of Play
    | Finished of Play * Ending

module Session =

    [<Literal>]
    let Fewest = 1

    [<Literal>]
    let Most = 1

    /// How many touches a board is worth. The number is most of the game: a cascade you cannot
    /// start again is a board you have to choose where to touch.
    [<Literal>]
    let Touches = 12

    /// What stops a cascade that will not stop itself. A cell that has finished turning may be set
    /// off again - what makes this a chain rather than a flood fill, and also what lets a run go
    /// round a loop for ever. No ordinary board comes near either ceiling; they are here so that
    /// "nothing may be touched while anything is turning" cannot become "nothing may be touched".
    [<Literal>]
    let MostWaves = 200

    [<Literal>]
    let MostRotations = 4096

    [<Literal>]
    let Slowest = 1

    [<Literal>]
    let Fastest = 9

    [<Literal>]
    let Ordinary = 5

    /// How long a quarter turn takes, in milliseconds, at each notch. Five is the half second the
    /// rules are written in; the board does the same thing at every notch and takes longer over it.
    let quarter notch = 1000 - 100 * notch

    /// How many beats a shape that has come up goes on being lit for.
    [<Literal>]
    let Lingers = 3

    /// How many quarter turns a cell makes before it is drawn a step further along, and how many
    /// steps there are to go along.
    [<Literal>]
    let PerStep = 5

    [<Literal>]
    let Steps = 4

    let play =
        function
        | InPlay play -> play
        | Finished(play, _) -> play

    let fresh =
        { Touches = 0
          Rotations = 0
          Lines = 0
          Squares = 0 }

    let opened cell =
        { From = cell
          Waves = 0
          Rotations = 0
          Rotated = Set.empty
          Made = []
          Halted = false }

    let dealt seed =
        let rec filling cells rng =
            function
            | [] -> cells, rng
            | cell :: rest ->
                let picked, rng = Rng.intBelow (List.length Facing.all) rng

                filling
                    (Map.add
                        cell
                        { Facing = Facing.all[picked]
                          Turned = 0
                          Landed = 0 }
                        cells)
                    rng
                    rest

        let cells, rng = filling Map.empty (Rng.ofSeed seed) Board.all

        InPlay
            { Cells = cells
              Turning = Set.empty
              At =
                { Row = (Board.Height + 1) / 2
                  Column = (Board.Width + 1) / 2 }
              Wave = 0
              Left = Touches
              Speed = Ordinary
              Run = None
              Tally = fresh
              Lit = []
              Sounding = []
              Struck = None
              Rng = rng }

    let active (_: Session) = Seat.at 1

    let seats (_: Session) = Fewest

    /// A turn is a touch. Beats are not turns of their own: a cascade is one thing a player did,
    /// however many beats it took to finish.
    let turn session = (play session).Tally.Touches + 1

    let isOver =
        function
        | InPlay _ -> false
        | Finished _ -> true

    let ending =
        function
        | InPlay _ -> None
        | Finished(_, ending) -> Some ending

    let reseed session = Rng.next (play session).Rng |> fst

    let standing cell play = Map.find cell play.Cells

    let facing cell play = (standing cell play).Facing

    let isTurning cell play = Set.contains cell play.Turning

    let atRest play = Set.isEmpty play.Turning

    /// Where the hand goes when pushed that way. A push off the edge does not move it, rather than
    /// wrapping or being refused - a hand held against the edge is what a player expects.
    let pushed way play =
        let there = Board.along way play.At
        if Board.holds there then there else play.At

    /// How worn a cell is: a step further along every `PerStep` quarter turns, and never past the
    /// last step there is a way of drawing.
    let wear cell play =
        min (Steps - 1) ((standing cell play).Turned / PerStep)

    /// Whether a cell finished a turn on the beat the board is standing on - drawn as a flash,
    /// heard as the tap.
    let justLanded cell play =
        play.Wave > 0 && (standing cell play).Landed = play.Wave

    /// Which soundings the board is *struck* for as well as heard. Said here because the rules may
    /// not reach the table, but it has to stay the same three a terminal rings its one bell for or
    /// sight and hearing drift apart - `Offer` holds the two lists against each other in `Faults`.
    let strikes =
        function
        | Landing
        | Squared -> false
        | Lined
        | Resting
        | Ending -> true

    let sounding play = play.Sounding

    /// Whether the board has something left to *show*, which is not the same as something left to
    /// do: a cascade at rest is still lighting its shapes and running the strike down itself, and
    /// until that finishes the clock has a reason to go on beating.
    let settling play =
        not (atRest play) || not (List.isEmpty play.Lit) || play.Struck.IsSome

    /// How far down the board the strike has got, in rows, or nothing if it is not showing. Beats
    /// and the frames within them are counted together, so the band travels on across a beat
    /// boundary rather than starting again at each one.
    let struck pictures frame play =
        play.Struck
        |> Option.map (fun since -> ((play.Wave - since) * pictures + frame) * 2)

    /// The activation being watched, running or finished. A board nobody has touched has none,
    /// which is not the same as one of nought.
    let run play = play.Run

    let lines shapes =
        shapes |> List.filter Shape.isLine |> List.length

    let squares shapes =
        shapes |> List.filter (Shape.isLine >> not) |> List.length
