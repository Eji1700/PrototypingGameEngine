namespace TCModel.TicTacToe

open TCModel.Common

/// How well a machine plays.
///
/// Two numbers, and neither of them is a strategy. This game is small enough to be solved
/// outright - nine squares, and every line of play can be walked to its end - so there is
/// nothing to be clever about and nothing to weigh. What is left is how *far* a machine
/// looks and how often it does not play what it saw, which between them are the whole of
/// what "easy" and "hard" can honestly mean here.
///
/// Which is worth saying beside the other game, where a machine has weights on five things
/// and still cannot see the end of a game. Both are machines; only one of them could ever
/// be perfect, and the reason is the game rather than the writing.
type Skill =
    {
        Name: string
        Describe: string
        /// How many moves ahead it walks. Nine is the whole game from an empty board, which
        /// at this size is perfect play.
        Depth: int
        /// Out of a hundred, how often it plays something other than the best it saw. This is
        /// what makes a beatable opponent out of a solved game: a machine that never slipped
        /// could not be beaten, only drawn with, and losing every game is nobody's idea of an
        /// easy one.
        Slips: int
    }

/// A machine at a seat: how it plays, and its own generator.
///
/// The generator is what breaks ties - several moves are very often worth exactly the same,
/// and a machine that always took the first would play the same game every time - and what
/// decides a slip. It travels with the machine, so the same table dealt twice plays the same
/// twice.
type Rival = { Skill: Skill; Rng: Rng }

module Rival =

    /// What a finished game is worth to one mark. Nothing in between: this game is won,
    /// drawn or lost, and there is no position worth half of one.
    let private worth mark =
        function
        | Won(winner, _) -> if winner = mark then 1 else -1
        | Drawn -> 0
        | Abandoned who -> if who = mark then -1 else 1

    /// Worse than losing and better than winning: the two ends a search starts from, and
    /// what an unreachable branch is worth.
    let private Worst = -2
    let private Best = 2

    /// What a position is worth to `mark`, looking this far ahead: the best it can force,
    /// assuming the other side plays as well as it can.
    ///
    /// A position still going when the looking runs out is worth nothing, which is the whole
    /// of the guesswork here. It is not a heuristic anybody had to invent - it is what "I
    /// cannot see how this ends" means - and it is what makes a shallow machine beatable
    /// rather than merely slower.
    ///
    /// `alpha` and `beta` are the best either side has already secured further up. Once one
    /// of this position's branches is worse for whoever is choosing than something they
    /// could already have had, the rest of the branches cannot change what they will pick -
    /// so the walk stops. It changes no answer, only how long the answer takes, and at nine
    /// squares from an empty board that is the difference between a machine that answers and
    /// one a player waits on.
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
                        // A move the rules would not take cannot come from a free square, so
                        // this is unreachable. Answered as the worst thing there is, so that
                        // a machine could never come to prefer one.
                        | None, _ -> Worst

                    let best = if mine then max best score else min best score
                    let alpha = if mine then max alpha best else alpha
                    let beta = if mine then beta else min beta best

                    if alpha >= beta then best else walk rest alpha beta best

            match Board.free play.Board with
            | [] -> 0
            | free -> walk free alpha beta (if mine then Worst else Best)

    /// Which move a machine plays, and the machine as it then stands.
    ///
    /// This is the whole of what a game has to hand the engine about a seat it plays: the
    /// *when* is `Machines`', and is the same at every game.
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
                // A full window on every one of them, deliberately: the pruning inside is
                // free, but pruning at this level would stop as soon as a move could not
                // *beat* the best so far - and what is wanted here is every move that
                // matches it, so that the generator can pick between them.
                | Some next, _ -> square, forced (rival.Skill.Depth - 1) play.ToPlay Worst Best next
                | None, _ -> square, Worst)

        let slip, rng = Rng.intBelow 100 rival.Rng

        // Either everything it may play, or only the best of it - and then one of those at
        // random, so a machine with three moves it rates alike does not always take the
        // lowest-numbered square.
        let wanted =
            if slip < rival.Skill.Slips then
                free
            else
                let best = scored |> List.map snd |> List.max
                scored |> List.filter (fun (_, worth) -> worth = best) |> List.map fst

        let picked, rng = Rng.intBelow (List.length wanted) rng

        Some(Place wanted[picked], { rival with Rng = rng })

    // --- the three on offer ------------------------------------------------------------------

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

    /// Worst to best, which is the order they are offered in.
    let all = [ easy; medium; hard ]

    let names = all |> List.map (fun skill -> skill.Name) |> String.concat ", "

    let byName (name: string) =
        let wanted = name.ToLowerInvariant()

        match all |> List.tryFind (fun skill -> skill.Name = wanted) with
        | Some skill -> Ok skill
        | None -> Error $"'{name}' is not a machine I have. There is {names}."

    /// Seat the machines named - one entry per seat, in dealing order, naming the skill or
    /// nobody - each with a generator of its own drawn from the deal and from where the seat
    /// sits, so that moving a machine along a seat hands it the generator that seat has
    /// always had.
    let seating (seed: uint64) sitting =
        Mark.all
        |> List.indexed
        |> List.choose (fun (seat, mark) ->
            sitting
            |> List.tryItem seat
            |> Option.flatten
            |> Option.map (fun skill ->
                Session.seatOf mark,
                { Skill = skill
                  Rng = Rng.ofSeed (seed + uint64 seat) }))
