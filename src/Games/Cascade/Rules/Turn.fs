namespace TCModel.Cascade

open TCModel.Engine

type Move =
    | Touch of Cell
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

    /// The cells a wave sets off next.
    ///
    /// This is the whole of the rule, and it is read off the board as it stands *after* the wave
    /// has landed. A cell that has just turned reaches out along the two arms it now has, and
    /// whatever it reaches has to be reaching back: an arm pointing east finds a cell with an arm
    /// pointing west, or it finds nothing.
    ///
    /// Nothing here skips a cell in the middle of a turn, and nothing needs to - everything that
    /// was turning landed on this same beat, so there is no such cell left to skip. Nor is a cell
    /// that has already turned during this activation spared, which is what lets a cascade come
    /// back over its own ground, and why there is a cap on how far one may go.
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

    /// Which shapes are wholly turned over that were not before.
    ///
    /// A shape comes up once an activation. `Rotated` only ever grows while one is running, so a
    /// shape that has come up stays up - which is why it is the ones already counted that are
    /// taken out, rather than the ones already whole.
    let private cameUp made rotated =
        Shape.all
        |> List.filter (fun shape ->
            not (List.contains shape made)
            && Shape.cells shape |> List.forall (fun cell -> Set.contains cell rotated))

    /// One beat: everything that was turning lands, and whatever that sets off begins turning.
    ///
    /// Every cell in a wave lands at once, and the board is read for what comes next only once
    /// they all have. That is what makes a cascade something that happens in waves rather than a
    /// walk over the board in some order, and it is why two cells that set each other off turn
    /// together rather than one after the other.
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

        let tally =
            if not settled then
                play.Tally
            else
                { play.Tally with
                    Rotations = play.Tally.Rotations + run.Rotations
                    Lines = play.Tally.Lines + Session.lines run.Made
                    Squares = play.Tally.Squares + Session.squares run.Made }

        let played =
            { play with
                Cells = cells
                Turning = turning
                Wave = wave
                Run = Some run
                Tally = tally
                Lit = lit
                Sounding =
                    Some(
                        if not (List.isEmpty made) then Completing
                        elif settled then Resting
                        else Landing
                    ) }

        let told =
            [ for shape in made -> Happened(CameUp(shape, run.Rotations))

              if settled then
                  yield (if run.Halted then Happened(Halted run) else Happened(Settled run)) ]

        if settled && played.Left = 0 then
            Some(Finished(played, Spent)), told @ [ Happened(GameEnded played.Tally) ]
        else
            Some(InPlay played), told

    let asked move session =
        match session, move with
        | Finished _, _ -> None, []

        | InPlay play, Resign ->
            Some(
                Finished(
                    { play with
                        Turning = Set.empty
                        Left = 0
                        Lit = []
                        Sounding = None },
                    GaveUp
                )
            ),
            [ Happened(GaveIn play.Left); Happened(GameEnded play.Tally) ]

        // The clock beats over a board with nothing turning on it for as long as nobody touches
        // anything, and a beat that found nothing to do did not happen: it takes nothing and says
        // nothing, and `Update` leaves no line in the record for it.
        | InPlay play, Beat when Session.atRest play -> None, []

        | InPlay play, Beat ->
            match play.Run with
            | None -> None, []
            | Some run -> landing run play

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
                        Lit = []
                        Sounding = None }
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
