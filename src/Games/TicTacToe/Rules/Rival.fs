namespace Prototyping.TicTacToe

open Prototyping.Common
open Prototyping.Engine

type Skill =
    { Name: string
      Describe: string
      Depth: int
      Slips: int }

type Rival = { Skill: Skill; Rng: Rng }

module Rival =

    let private worth mark =
        function
        | Won(winner, _) -> if winner = mark then 1 else -1
        | Drawn -> 0
        | Abandoned who -> if who = mark then -1 else 1

    // Outside the range a real position scores, so a side starting from one of these takes the first
    // move it looks at. `Worst` doubles as the score for a move the rules refuse, which is one no
    // side would ever choose.
    let private Worst = -2
    let private Best = 2

    /// What the position is worth to `mark` with both sides playing on, cut off at `depth` - which
    /// is what makes the skills differ, since the search itself is the same for all of them. Depth
    /// running out scores nothing rather than guessing.
    ///
    /// Alpha-beta: alpha is the best the side to move has already secured elsewhere, beta the best
    /// the other side has. Once they cross, the rest of this branch cannot be chosen and is dropped.
    let rec private forced depth mark alpha beta session =
        match session with
        | Finished(_, ending) -> worth mark ending
        | InPlay _ when depth <= 0 -> 0
        | InPlay play ->
            let mine = play.ToPlay = mark

            let rec walk squares alpha beta best =
                match squares with
                | [] -> best
                | square :: rest ->
                    let score =
                        match Turn.asked (Place square) session with
                        | Some next, _ -> forced (depth - 1) mark alpha beta next
                        | None, _ -> Worst

                    let best = if mine then max best score else min best score
                    let alpha = if mine then max alpha best else alpha
                    let beta = if mine then beta else min beta best

                    if alpha >= beta then best else walk rest alpha beta best

            match Board.free play.Board with
            | [] -> 0
            | free -> walk free alpha beta (if mine then Worst else Best)

    let plays session rival =
        match session with
        | Finished _ -> None
        | InPlay play ->

        match Board.free play.Board with
        | [] -> None
        | free ->

        let scored =
            free
            |> List.map (fun square ->
                match Turn.asked (Place square) session with
                | Some next, _ -> square, forced (rival.Skill.Depth - 1) play.ToPlay Worst Best next
                | None, _ -> square, Worst)

        let slip, rng = Rng.intBelow 100 rival.Rng

        let wanted =
            if slip < rival.Skill.Slips then
                free
            else
                let best = scored |> List.map snd |> List.max
                scored |> List.filter (fun (_, worth) -> worth = best) |> List.map fst

        let picked, rng = Rng.intBelow (List.length wanted) rng

        Some(Place wanted[picked], { rival with Rng = rng })


    let easy =
        { Name = "easy"
          Describe = "takes a win it can see, and often plays somewhere else anyway"
          Depth = 1
          Slips = 40 }

    let medium =
        { Name = "medium"
          Describe = "takes a win and blocks yours, and looks no further"
          Depth = 3
          Slips = 15 }

    let hard =
        { Name = "hard"
          Describe = "plays the game out to the end before moving, so it cannot be beaten"
          Depth = Squares.Side * Squares.Side
          Slips = 0 }

    let all = [ easy; medium; hard ]

    let names = Machines.named (fun skill -> skill.Name) all

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    let seating (seed: uint64) sitting =
        Machines.seating (Mark.all |> List.map Session.seatOf) seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng })
