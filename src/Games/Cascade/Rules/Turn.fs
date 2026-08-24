namespace Prototyping.Cascade

open Prototyping.Engine

type Move =
    | Touch of Cell
    | Point of Way
    | Press
    | Beat
    | Faster
    | Slower
    | Speed of notch: int
    | Resign

type Happening =
    | Touched of Cell
    | CameUp of Shape * at: int
    | Settled of Run
    | Halted of Run
    | Wound of notch: int
    | GaveIn of left: int
    | GameEnded of Tally

type Refusal =
    | StillTurning of turning: int
    | NoneLeft
    | NoSuchCell of said: Cell
    | NoSuchSpeed of said: int

type Notice =
    | Happened of Happening
    | Refused of Refusal

module Turn =

    /// The cells a wave sets off next, read off the board as it stands *after* the wave landed. A
    /// cell that has just turned reaches along the two arms it now has, and whatever it reaches
    /// has to be reaching back.
    ///
    /// Nothing skips a cell mid-turn because everything turning landed on this same beat, and a
    /// cell that already turned during this activation is not spared - which is what lets a
    /// cascade come back over its own ground, and why there is a cap on how far one may go.
    let private setOff landed cells =
        landed
        |> List.collect (fun cell ->
            Facing.arms (Map.find cell cells).Facing
            |> List.map (fun way -> way, Board.along way cell)
            |> List.filter (fun (way, other) ->
                Board.holds other
                && Facing.reaches (Way.opposite way) (Map.find other cells).Facing)
            |> List.map snd)
        |> Set.ofList

    /// Which shapes are wholly turned over that were not before. A shape comes up once an
    /// activation, and `Rotated` only grows while one is running, so a shape that has come up
    /// stays up - hence subtracting the ones already counted rather than the ones already whole.
    let private cameUp made rotated =
        Shape.all
        |> List.filter (fun shape ->
            not (List.contains shape made)
            && Shape.cells shape |> List.forall (fun cell -> Set.contains cell rotated))

    /// One beat: everything that was turning lands, and whatever that sets off begins turning.
    /// Every cell in a wave lands at once and the board is read only once they all have - which is
    /// what makes this waves rather than a walk over the board in some order, and why two cells
    /// that set each other off turn together rather than one after the other.
    let private landing run play =
        let wave = play.Wave + 1
        let landed = Set.toList play.Turning

        let cells =
            landed
            |> List.fold
                (fun cells cell ->
                    let standing = Map.find cell cells

                    Map.add
                        cell
                        { Facing = Facing.turned standing.Facing
                          Turned = standing.Turned + 1
                          Landed = wave }
                        cells)
                play.Cells

        let rotated = Set.union run.Rotated play.Turning
        let rotations = run.Rotations + List.length landed
        let made = cameUp run.Made rotated

        let halted =
            rotations >= Session.MostRotations || run.Waves + 1 >= Session.MostWaves

        let turning = if halted then Set.empty else setOff landed cells
        let settled = Set.isEmpty turning

        let run =
            { run with
                Waves = run.Waves + 1
                Rotations = rotations
                Rotated = rotated
                Made = run.Made @ made
                Halted = halted }

        let lit =
            (play.Lit @ [ for shape in made -> { Shape = shape; Since = wave } ])
            |> List.filter (fun lit -> wave - lit.Since < Session.Lingers)

        let lined = Session.lines made > 0
        let ended = settled && (play.Left = 0 || halted)

        let tally =
            if not settled then
                play.Tally
            else
                { play.Tally with
                    Rotations = play.Tally.Rotations + run.Rotations
                    Lines = play.Tally.Lines + Session.lines run.Made
                    Squares = play.Tally.Squares + Session.squares run.Made }

        // What the wave did, then what the board now is - never more than one of each.
        let sounding =
            [ if lined then yield Lined
              elif Session.squares made > 0 then yield Squared
              elif not settled then yield Landing

              if settled then yield (if ended then Ending else Resting) ]

        let played =
            { play with
                Cells = cells
                Turning = turning
                Wave = wave
                Run = Some run
                Tally = tally
                Lit = lit
                Sounding = sounding

                // Struck for whatever a terminal would have rung its one bell for, so a reader who
                // can hear and one who cannot are told the same thing.
                Struck =
                    if sounding |> List.exists Session.strikes then
                        Some wave
                    else
                        play.Struck |> Option.filter (fun since -> wave - since < Session.Lingers) }

        let told =
            [ for shape in made -> Happened(CameUp(shape, run.Rotations))

              if settled then
                  yield (if run.Halted then Happened(Halted run) else Happened(Settled run)) ]

        if settled && played.Left = 0 then
            Some(Finished(played, Spent)), told @ [ Happened(GameEnded played.Tally) ]
        else
            Some(InPlay played), told

    /// A sound belongs to the move that made it and to no other. Sounding is read off the state
    /// *after* a move, so a sound left lying there is heard again after the next move too - moving
    /// the hand about a board that had just come to rest used to ring the chime every step. Only a
    /// beat can land anything, so only a beat may leave a sound behind, and every other move is
    /// quiet by construction rather than by remembering to say so.
    let private hushed =
        function
        | InPlay play -> InPlay { play with Sounding = [] }
        | Finished(play, ending) -> Finished({ play with Sounding = [] }, ending)

    let rec asked move session =
        match move with
        | Beat -> asking move session
        | _ ->
            match asking move session with
            | Some played, told -> Some(hushed played), told
            | None, told -> None, told

    and private asking move session =
        match session, move with
        | Finished _, _ -> None, []

        | InPlay play, Resign ->
            Some(
                Finished(
                    { play with
                        Turning = Set.empty
                        Left = 0
                        Lit = [] },
                    GaveUp
                )
            ),
            [ Happened(GaveIn play.Left); Happened(GameEnded play.Tally) ]

        // A board at rest may still have something to show - shapes lit, the strike running down
        // it - and a beat is what carries those along. Once nothing is moving and nothing showing,
        // a beat takes nothing and says nothing, and `Update` leaves no line in the record.
        | InPlay play, Beat when Session.atRest play && not (Session.settling play) -> None, []

        | InPlay play, Beat when Session.atRest play ->
            let wave = play.Wave + 1

            Some(
                InPlay
                    { play with
                        Wave = wave
                        Lit = play.Lit |> List.filter (fun lit -> wave - lit.Since < Session.Lingers)
                        Sounding = []
                        Struck = play.Struck |> Option.filter (fun since -> wave - since < Session.Lingers) }
            ),
            []

        | InPlay play, Beat ->
            match play.Run with
            | None -> None, []
            | Some run -> landing run play

        // A push that would take the hand off the edge moves nothing, and `Update` does not write
        // down a move that took nothing and said nothing.
        | InPlay play, Point way when Session.pushed way play = play.At -> None, []

        | InPlay play, Point way ->
            Some(
                InPlay
                    { play with
                        At = Session.pushed way play }
            ),
            []

        | InPlay play, Press -> asked (Touch play.At) session

        | InPlay _, Touch cell when not (Board.holds cell) -> None, [ Refused(NoSuchCell cell) ]

        | InPlay play, Touch _ when not (Session.atRest play) -> None, [ Refused(StillTurning(Set.count play.Turning)) ]

        | InPlay play, Touch _ when play.Left = 0 -> None, [ Refused NoneLeft ]

        | InPlay play, Touch cell ->
            Some(
                InPlay
                    { play with
                        Turning = Set.singleton cell
                        Left = play.Left - 1
                        Run = Some(Session.opened cell)
                        Tally =
                            { play.Tally with
                                Touches = play.Tally.Touches + 1 }
                        Lit = [] }
            ),
            [ Happened(Touched cell) ]

        | InPlay _, Speed notch when notch < Session.Slowest || notch > Session.Fastest -> None, [ Refused(NoSuchSpeed notch) ]

        | InPlay play, Speed notch when notch = play.Speed -> None, []
        | InPlay play, Faster when play.Speed = Session.Fastest -> None, []
        | InPlay play, Slower when play.Speed = Session.Slowest -> None, []

        | InPlay play, (Faster | Slower | Speed _ as winding) ->
            let notch =
                match winding with
                | Faster -> play.Speed + 1
                | Slower -> play.Speed - 1
                | Speed notch -> notch
                | _ -> play.Speed

            Some(InPlay { play with Speed = notch }), [ Happened(Wound notch) ]
