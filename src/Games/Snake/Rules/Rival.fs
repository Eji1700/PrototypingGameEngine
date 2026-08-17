namespace TCModel.Snake

open TCModel.Common
open TCModel.Engine

/// How well a machine plays.
///
/// Two things, and the first of them is the whole of what "good at snake" means. Anybody can
/// see that the square in front of them is empty; what kills a snake is the square it will
/// want four moves from now, and the cheapest honest way to ask about that is how much room a
/// step leaves it standing in. A machine that only looks one square ahead eats well and then
/// walls itself into a pocket, every time, and that is exactly what `easy` and `medium` do.
type Skill =
    {
        Name: string
        Describe: string
        /// Whether it counts the room a step leaves it in before taking it.
        Counts: bool
        /// Out of a hundred, how often it plays something other than the best it saw. What
        /// makes a beatable opponent out of one that is merely careful.
        Slips: int
    }

/// A machine at a seat: how it plays, and its own generator.
///
/// The generator breaks ties - several ways are very often worth exactly the same, and a
/// machine that always took the first would drive every snake into the same wall - and decides
/// a slip. It travels with the machine, so the same table dealt twice plays the same twice.
type Rival = { Skill: Skill; Rng: Rng }

module Rival =

    /// The three ways a snake may go, which is every way but back.
    let private ways snake =
        Direction.all |> List.filter (fun way -> way <> Direction.opposite snake.Facing)

    /// How much room a step leaves: how many squares can be reached from the one it lands on,
    /// counting nothing anybody is lying on.
    ///
    /// This is the whole difference between the machines. A snake in a pocket of four squares
    /// is dead in four moves whatever it does next, and the only moment it could have known
    /// that was before it went in.
    let private room play seat direction =
        let snake = Session.snakeAt seat play
        let start = Board.along direction (Snake.head snake)
        let blocked = Session.covered play

        let rec fill seen queue =
            match queue with
            | [] -> Set.count seen
            | cell :: rest when Set.contains cell seen -> fill seen rest
            | cell :: rest ->
                let seen = Set.add cell seen

                let onwards =
                    Direction.all
                    |> List.map (fun way -> Board.along way cell)
                    |> List.filter (fun next ->
                        Board.holds next
                        && not (Set.contains next blocked)
                        && not (Set.contains next seen))

                fill seen (onwards @ rest)

        if not (Board.holds start) || Set.contains start blocked then 0 else fill Set.empty [ start ]

    /// What a step is worth to a machine, or nothing at all where it would be the last one it
    /// took.
    ///
    /// Room first and food second, in that order, and the order is the opinion: a machine that
    /// ate its way into a pocket has eaten its last. Then the food, then the distance to it -
    /// so a step that eats beats a step that approaches, and a step that approaches beats one
    /// that wanders.
    let private worth skill play seat direction =
        match Turn.ahead seat direction play with
        | Wall
        | Into _ -> None
        | there ->
            let snake = Session.snakeAt seat play
            let target = Board.along direction (Snake.head snake)

            let space =
                if skill.Counts then room play seat direction else Board.Width * Board.Height

            let nearer =
                match play.Food with
                | Some food -> -(Board.apart target food)
                | None -> 0

            Some((if space >= Snake.length snake then 1 else 0), (if there = Food then 1 else 0), nearer, space)

    /// Which way a machine goes, and the machine as it then stands.
    ///
    /// This is the whole of what a game has to hand the engine about a seat it plays: the
    /// *when* is `Machines`', and is the same at every game.
    let plays session rival =
        match session with
        | Finished _ -> None
        | InPlay play ->

        let seat = play.ToPlay
        let open' = ways (Session.snakeAt seat play)

        let rated =
            open'
            |> List.choose (fun way -> worth rival.Skill play seat way |> Option.map (fun worth -> way, worth))

        let slip, rng = Rng.intBelow 100 rival.Rng

        // Every way it may go, only the best of them, or - where every way it may go is a
        // death - whatever is left. A snake with nowhere to go still has to go somewhere, and
        // saying so here is cheaper than a machine that answers nothing and stalls the table.
        let wanted =
            if List.isEmpty rated then
                open'
            elif slip < rival.Skill.Slips then
                rated |> List.map fst
            else
                let best = rated |> List.map snd |> List.max
                rated |> List.filter (fun (_, worth) -> worth = best) |> List.map fst

        let picked, rng = Rng.intBelow (List.length wanted) rng

        Some(Go wanted[picked], { rival with Rng = rng })

    // --- the three on offer ------------------------------------------------------------------

    let easy =
        { Name = "easy"
          Describe = "heads for the food, looks no further than the next square, and often goes elsewhere anyway"
          Counts = false
          Slips = 35 }

    let medium =
        { Name = "medium"
          Describe = "heads for the food and will not walk into anything, which is not the same as staying alive"
          Counts = false
          Slips = 5 }

    let hard =
        { Name = "hard"
          Describe = "counts the room a step leaves it in before taking it, and will pass up food to keep some"
          Counts = true
          Slips = 0 }

    /// Worst to best, which is the order they are offered in.
    let all = [ easy; medium; hard ]

    let names = Machines.named (fun skill -> skill.Name) all

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    /// Seat the machines named. Which seats there are is this game's answer - they are dealt
    /// in order, up to four of them - and everything else about it is the engine's.
    let seating (seed: uint64) sitting =
        Machines.seating [ for place in 1 .. Session.Most -> Seat.at place ] seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng })
