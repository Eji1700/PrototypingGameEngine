namespace TCModel.Snake

open TCModel.Common
open TCModel.Engine

/// How a game finished.
///
/// Two, and which of them a table gets depends on how many sat down. At a table of one there
/// is nobody to be last: the snake stops, and what is worth saying is what it ate. At a table
/// of more, the game is over the moment one is left moving, and that one has won.
type Ending =
    | LastMoving of PlayerId
    | NobodyMoving

/// Which way this table is being played, and the only thing that differs between the two.
///
/// `Turns` is a game of turns like every other game in this program: a snake moves when the
/// person playing it says so, one at a time, round the table. `Clock` is the arcade game: the
/// snakes move together whenever the beat comes round, and what anybody types only turns them.
///
/// The rules underneath are the same rules. A step is a step, the wall is the wall, and the
/// square your own tail is leaving is yours either way - which is why this is a word in the
/// state rather than two games in two folders.
type Pace =
    | Turns
    | Clock

/// A game still going: the snakes, the food, whose turn it is, and the generator the next
/// piece of food will come from.
///
/// The generator is in the state and is used all through the game rather than only at the
/// deal, which is the thing this game has that [Life](../../Life/Rules/World.fs) does not: a
/// piece of food is drawn every time one is eaten, so where the next one lands is part of the
/// position and travels with it. Undo takes the draw back with the move, and a record replays
/// to the same board because the generator was folded rather than consulted.
type Play =
    {
        Snakes: Map<PlayerId, Snake>
        /// Every seat, in dealing order, living or not.
        Seats: PlayerId list
        /// Nothing at all only if there is nowhere left to put any.
        Food: Cell option
        /// Whose turn it is at a game of turns. At a game on a clock it is nobody's, and this
        /// is the first snake still moving - which is what the record writes down as having
        /// asked for the beat, and what a screen with one seat to mark marks.
        ToPlay: PlayerId
        /// Turns round the table, or beats. The two are the same count read two ways, and
        /// both are what "how long has this been going" means at that pace.
        Turn: int
        Pace: Pace
        /// How fast the clock is wanted, from 1 to 9 - and *not* how long a beat is, which is
        /// milliseconds and is the table's business.
        ///
        /// In the position rather than in a setting, and that is the one surprising thing about
        /// it. A clock could hardly be more obviously "how this is being read" - except that
        /// what a game hands the table is `Every: 'State -> TimeSpan`, so the only way the game
        /// can have an opinion is for the state to hold one. Which turns out to be the right
        /// place anyway: winding it up is a move like any other, so it goes into the record, it
        /// replays, it can be taken back, and a record says how fast it was being played.
        Speed: int
        Rng: Rng
    }

/// Where the game stands, which is the one thing the engine ever asks about.
type Session =
    | InPlay of Play
    | Finished of Play * Ending

module Session =

    [<Literal>]
    let Fewest = 1

    /// Four, because four snakes on a board this size is a game and five is a scramble - and
    /// because a seat is drawn as a letter, and the letters are worth being able to say out
    /// loud.
    [<Literal>]
    let Most = 4

    /// How fast the clock may be wound, as notches rather than as milliseconds: a player asks
    /// for a game that is quicker, not for one that is two hundred and twenty. What a notch
    /// comes to in time is settled where the clock is, which is `Offer`.
    [<Literal>]
    let Slowest = 1

    [<Literal>]
    let Fastest = 9

    /// Where it starts. Middling on purpose - the range is only worth having if the ordinary
    /// game is somewhere a player would want to move away from in both directions.
    [<Literal>]
    let Ordinary = 5

    let play =
        function
        | InPlay play -> play
        | Finished(play, _) -> play

    let snakeAt seat play = Map.find seat play.Snakes

    let snakes play =
        play.Seats |> List.map (fun seat -> seat, snakeAt seat play)

    let living play =
        play.Seats |> List.filter (fun seat -> Snake.isAlive (snakeAt seat play))

    /// Every square with something of a snake on it, dead or alive. A snake that has stopped
    /// is still lying there, and everybody else still has to go round it - which is most of
    /// what makes a table of four different from four tables of one.
    let covered play =
        play.Snakes
        |> Map.toSeq
        |> Seq.collect (fun (_, snake) -> snake.Body)
        |> Set.ofSeq

    let free play =
        let taken = covered play
        Board.all |> List.filter (fun cell -> not (Set.contains cell taken))

    /// Put a piece of food somewhere nothing is standing, drawn from the game's own
    /// generator. Nowhere left to put one is answered with none rather than with a throw: a
    /// board covered in snake is a fair end to a game and not a broken one.
    let feeding play =
        match free play with
        | [] -> { play with Food = None }
        | cells ->
            let picked, rng = Rng.intBelow (List.length cells) play.Rng

            { play with
                Food = Some cells[picked]
                Rng = rng }

    // --- the deal ---------------------------------------------------------------------------
    //
    // Snakes are laid out rather than drawn for: where each starts is worked out from the seat
    // and the count, so a table of four is four snakes evenly down the board facing each other
    // in pairs, and a table of one is one snake in the middle. The only thing the seed settles
    // is the food - which is the whole of the chance in this game.

    let private start players place =
        let row = (place + 1) * Board.Height / (players + 1)

        // Even seats start on the left facing across, odd ones on the right facing back, so
        // that two snakes at one table open pointing at each other rather than in a queue.
        if place % 2 = 0 then
            Snake.dealt East { Row = row; Column = 1 + Snake.Length }
        else
            Snake.dealt
                West
                { Row = row
                  Column = Board.Width - Snake.Length }

    let dealt pace players seed =
        let seats = [ for place in 1..players -> Seat.at place ]

        let snakes =
            seats |> List.mapi (fun place seat -> seat, start players place) |> Map.ofList

        InPlay(
            feeding
                { Snakes = snakes
                  Seats = seats
                  Food = None
                  ToPlay = List.head seats
                  Turn = 1
                  Pace = pace
                  Speed = Ordinary
                  Rng = Rng.ofSeed seed }
        )

    // --- what the engine asks ------------------------------------------------------------

    let active session = (play session).ToPlay

    let turn session = (play session).Turn

    let seats session = List.length (play session).Seats

    let isOver =
        function
        | InPlay _ -> false
        | Finished _ -> true

    let ending =
        function
        | InPlay _ -> None
        | Finished(_, ending) -> Some ending

    let reseed session = Rng.next (play session).Rng |> fst

    // --- and how the turn goes round -------------------------------------------------------

    /// The next seat still moving, and the turn it lands on. A turn is a round of the table,
    /// so it goes up when the order comes back to somebody at or before whoever just played -
    /// which is what makes "turn 12" mean the same thing at a table of one and a table of four.
    ///
    /// Whoever is left is asked of the snakes rather than remembered, so a seat that stopped
    /// this move is skipped from this move on, and nothing has to be told.
    let onwards play =
        let count = List.length play.Seats
        let at = play.Seats |> List.findIndex ((=) play.ToPlay)

        let rec next step =
            let index = (at + step) % count

            if Snake.isAlive (snakeAt play.Seats[index] play) then Some index
            elif step >= count then None
            else next (step + 1)

        match next 1 with
        | None -> play
        | Some index ->
            { play with
                ToPlay = play.Seats[index]
                Turn = (if index <= at then play.Turn + 1 else play.Turn) }

    /// The first snake still moving.
    ///
    /// Who the record writes down as having asked for a beat, at a pace where nobody's turn it
    /// is - and the seat a screen with one to mark marks. Somebody has to be named: a record
    /// says who asked for every move in it, and "the clock" is not a seat at the table.
    let foremost play =
        match living play with
        | seat :: _ -> seat
        | [] -> play.ToPlay

    /// Whether that was the end of it: nobody left moving, or - at a table of more than one -
    /// exactly one left, who has therefore won.
    let finished play =
        match living play with
        | [] -> Some NobodyMoving
        | [ last ] when List.length play.Seats > 1 -> Some(LastMoving last)
        | _ -> None
