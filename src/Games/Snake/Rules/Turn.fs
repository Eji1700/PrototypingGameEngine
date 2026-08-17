namespace TCModel.Snake

open TCModel.Engine

/// Everything a player may ask this game to do.
///
/// Five, and they fall into two halves, one per pace. At a game of turns a direction *is* a
/// step and the player takes one when they say so; on a clock a direction only turns the head
/// and the beat is what moves anybody - so `Steer` says which snake, because at a table where
/// nobody is waiting for anybody there is no such thing as whose turn it is.
///
/// One type for both, because they are one game. A move belonging to the other pace is refused
/// in words rather than left out of the type: the two ways write different records and read
/// different lines, and the one thing that must not happen is a line from one quietly meaning
/// something at the other.
type Move =
    /// A game of turns: one square that way, now.
    | Go of Direction
    /// The same, the way you are already facing.
    | Onward
    /// On a clock: turn that snake's head, and nothing else. It moves when the beat does.
    | Steer of seat: PlayerId * way: Direction
    /// And the beat: every snake still moving takes one square, at once.
    | Beat
    /// Wind the clock up or down a notch, or straight to a number. Three moves rather than one
    /// because a key can only ever mean "quicker" - what that comes to depends on where it
    /// already is, and a parser has no state to work that out from.
    | Faster
    | Slower
    | Speed of notch: int
    | Resign

type Happening =
    | Went of PlayerId * Direction
    | Ate of PlayerId * eaten: int * grown: int
    | Turned of PlayerId * Direction
    | Wound of notch: int
    | Stopped of PlayerId * Fate
    | GameEnded of Ending

type Refusal =
    /// The one rule of this game nobody thinks of as a rule.
    | CannotTurnBack of Direction
    /// A snake that has stopped is steered by nobody, including whoever was steering it.
    | HasStopped of PlayerId
    | NoSuchSnake of PlayerId
    | NoSuchSpeed of said: int
    /// A move that belongs to the other way of playing this game.
    | NotThisPace of said: string

/// What this game has to say, and the whole of it. Nothing about undo, nothing about a line
/// nobody could read: those are the engine's and are said once, above, in words that suit any
/// game.
type Notice =
    | Happened of Happening
    | Refused of Refusal

/// What is one square that way, which is the only question this game ever asks of the board.
///
/// Worth being a type rather than three tests written out wherever they are wanted, because
/// three things ask it and they must not come to disagree: the rules, to say what the step
/// did; the machine, to pick a step; and the screen, to answer a player asking what is over
/// there before they commit to it.
type Ahead =
    | Wall
    | Into of PlayerId
    | Food
    | Clear

/// How a turn goes: one square, one snake, and everything that could be in the way of it.
module Turn =

    /// Whoever the head would run into, if anybody.
    ///
    /// The moving snake is asked about the body it will have rather than the one it has, so
    /// following your own tail round is a turn and not a death. Everybody else is asked about
    /// the body they have, because nobody else is moving this turn.
    let private into seat target growing play =
        Session.snakes play
        |> List.tryPick (fun (other, snake) ->
            let body = if other = seat && not growing then Snake.behind snake else snake.Body

            if List.contains target body then Some other else None)

    /// Whether that way is back into the snake's own neck, which is the one thing this game
    /// refuses rather than allowing and killing you for.
    ///
    /// A fact about the body and not about the facing. The two are the same thing at a game of
    /// turns, where a snake moves the instant it is turned - and are not at a game on a clock,
    /// where a player can turn twice between two beats. Asked this way, both paces get the rule
    /// they meant.
    let private backwards seat way play =
        let snake = Session.snakeAt seat play
        Snake.neck snake = Some(Board.along way (Snake.head snake))

    /// What lies one square that way from a seat's head.
    let ahead seat direction play =
        let snake = Session.snakeAt seat play
        let target = Board.along direction (Snake.head snake)
        let eating = play.Food = Some target

        if not (Board.holds target) then
            Wall
        else
            match into seat target (eating || snake.Growing > 0) play with
            | Some other -> Into other
            | None -> if eating then Food else Clear

    /// One snake, one square that way. Back comes the position it left and what there is to
    /// say - and the snake either moved, ate, or stopped.
    let private stepping seat direction play =
        let snake = Session.snakeAt seat play
        let there = ahead seat direction play

        let stops fate =
            { play with
                Snakes = Map.add seat (Snake.stopped fate snake) play.Snakes },
            [ Happened(Stopped(seat, fate)) ]

        match there with
        | Wall -> stops HitWall
        | Into other when other = seat -> stops HitItself
        | Into other -> stops (HitAnother other)
        | Food
        | Clear ->

            let moved = Snake.moved direction snake
            let fed = if there = Food then Snake.fed moved else moved

            let play =
                { play with
                    Snakes = Map.add seat fed play.Snakes }

            // The next piece of food is drawn now rather than at the top of the next turn, so
            // that the board a player is looking at is the board they are playing on.
            if there = Food then
                Session.feeding play,
                [ Happened(Went(seat, direction))
                  Happened(Ate(seat, fed.Eaten, Snake.length fed + fed.Growing)) ]
            else
                play, [ Happened(Went(seat, direction)) ]

    /// A move, once it is settled which way that is: take the step, hand the turn on, and see
    /// whether that was the end of it.
    let private taking seat direction play =
        let played, told = stepping seat direction play

        match Session.finished played with
        | Some ending -> Some(Finished(played, ending)), told @ [ Happened(GameEnded ending) ]
        | None -> Some(InPlay(Session.onwards played)), told

    // --- and the same thing on a clock -------------------------------------------------------

    /// The beat: every snake still moving takes one square, at once.
    ///
    /// At once is the whole of the difference, and it costs three rules a game of turns never
    /// needed. Every tail that was going to move counts as gone, so four snakes can follow each
    /// other round a ring and none of them is bitten by a square that was about to be empty. Two
    /// heads that pick the same square both stop, because neither of them got there first. And a
    /// snake that stops on this beat still leaves its body where it fell, for whoever is still
    /// going to run into afterwards.
    ///
    /// The order the snakes are asked in decides only one thing, and it is worth saying which:
    /// two heads reaching the same piece of food on the same beat. The one nearer the front of
    /// the table eats it - somebody has to, and a rule that fed both would be a rule that put
    /// two pieces of food on the board out of one.
    let private beating play =
        let moving = Session.living play

        let headed =
            moving
            |> List.map (fun seat ->
                let snake = Session.snakeAt seat play
                seat, Board.along snake.Facing (Snake.head snake))

        let eater =
            headed
            |> List.tryFind (fun (_, target) -> play.Food = Some target)
            |> Option.map fst

        /// Whether this snake keeps its tail through the beat, which is what decides whether the
        /// square the tail is on is somewhere anybody may move into.
        let growing seat =
            let snake = Session.snakeAt seat play
            snake.Growing > 0 || eater = Some seat

        /// Every square that will still have something on it once the beat is done.
        let standing =
            Session.snakes play
            |> List.collect (fun (seat, snake) ->
                if Snake.isAlive snake && not (growing seat) then Snake.behind snake else snake.Body)
            |> Set.ofList

        /// What stopped this one, if anything did.
        let fate seat target =
            if not (Board.holds target) then
                Some HitWall
            elif Set.contains target standing then
                Session.snakes play
                |> List.tryPick (fun (other, snake) ->
                    let body =
                        if Snake.isAlive snake && not (growing other) then Snake.behind snake else snake.Body

                    if List.contains target body then Some other else None)
                |> Option.map (fun other -> if other = seat then HitItself else HitAnother other)
                |> Option.orElse (Some HitItself)
            else
                headed
                |> List.tryPick (fun (other, theirs) -> if other <> seat && theirs = target then Some(HitAnother other) else None)

        let stopping =
            headed
            |> List.choose (fun (seat, target) -> fate seat target |> Option.map (fun how -> seat, how))

        let told = stopping |> List.map (fun (seat, how) -> Happened(Stopped(seat, how)))

        let snakes =
            headed
            |> List.fold
                (fun snakes (seat, _) ->
                    let snake = Session.snakeAt seat play

                    match stopping |> List.tryFind (fst >> (=) seat) with
                    | Some(_, how) -> Map.add seat (Snake.stopped how snake) snakes
                    | None ->
                        let moved = Snake.moved snake.Facing snake
                        Map.add seat (if eater = Some seat then Snake.fed moved else moved) snakes)
                play.Snakes

        let played =
            { play with
                Snakes = snakes
                Turn = play.Turn + 1 }

        // Nothing is said about a snake that simply moved. A beat two or three times a second
        // would fill the log with a line per snake per beat and push everything worth reading
        // off the top of it - and the board is right there, already showing it.
        let eaten =
            match eater with
            | Some seat when not (stopping |> List.exists (fst >> (=) seat)) ->
                let snake = Session.snakeAt seat played
                [ Happened(Ate(seat, snake.Eaten, Snake.length snake + snake.Growing)) ]
            | Some _
            | None -> []

        let played =
            if eater.IsSome && not (List.isEmpty eaten) then Session.feeding played else played

        { played with
            ToPlay = Session.foremost played },
        told @ eaten

    /// What the engine asks of a game: a move and where it stands, and the position it left
    /// along with whatever there is to say.
    ///
    /// Total, like every game's: `None` for the position means nothing moved, and the notices
    /// say why. A refusal is something this game *says*, not something that breaks it, which
    /// is what lets every table above be a fold.
    let asked move session =
        /// Whatever a move left, said as the engine wants it: the position, and an ending if
        /// that was one.
        let ending play told =
            match Session.finished play with
            | Some over -> Some(Finished(play, over)), told @ [ Happened(GameEnded over) ]
            | None -> Some(InPlay play), told

        match session, move with
        // The engine refuses moves after the game is over and says so itself, so this is
        // unreachable rather than wrong. Answered all the same, because a total function is
        // cheaper than an argument about which of two files is guarding it.
        | Finished _, _ -> None, []

        // Giving up means the same thing at both paces and comes to something different at
        // each, which is exactly what putting a game down is: at a game of turns it is your
        // own snake, and at a game on a clock - where the others are not waiting on you and
        // never were - it is the whole board.
        | InPlay play, Resign when play.Pace = Clock ->
            let stopping = Session.living play

            let stopped =
                { play with
                    Snakes =
                        stopping
                        |> List.fold
                            (fun snakes seat -> Map.add seat (Snake.stopped GaveUp (Session.snakeAt seat play)) snakes)
                            play.Snakes }

            ending stopped (stopping |> List.map (fun seat -> Happened(Stopped(seat, GaveUp))))

        | InPlay play, Resign ->
            let seat = play.ToPlay

            let stopped =
                { play with
                    Snakes = Map.add seat (Snake.stopped GaveUp (Session.snakeAt seat play)) play.Snakes }

            let told = [ Happened(Stopped(seat, GaveUp)) ]

            match Session.finished stopped with
            | Some over -> Some(Finished(stopped, over)), told @ [ Happened(GameEnded over) ]
            | None -> Some(InPlay(Session.onwards stopped)), told

        // --- a game of turns --------------------------------------------------------------

        | InPlay play, (Go _ | Onward) when play.Pace = Clock ->
            None, [ Refused(NotThisPace "a direction is a turn of the head here, and the beat is what moves anybody") ]

        | InPlay play, Onward -> taking play.ToPlay (Session.snakeAt play.ToPlay play).Facing play

        // The one refusal, and it is about where the neck is rather than about which way the
        // head is pointing - see `backwards`.
        | InPlay play, Go direction when backwards play.ToPlay direction play -> None, [ Refused(CannotTurnBack direction) ]

        | InPlay play, Go direction -> taking play.ToPlay direction play

        // --- and a game on a clock ----------------------------------------------------------

        | InPlay play, (Steer _ | Beat | Faster | Slower | Speed _) when play.Pace = Turns ->
            None,
            [ Refused(NotThisPace "this way of playing takes a step when you say a direction, and waits for you in between") ]

        // --- winding the clock ----------------------------------------------------------------
        //
        // The clock is the table's, and how fast it is wanted is the game's - which is what this
        // is: a notch, kept in the position, that the pulse reads off it.

        | InPlay play, Speed notch when notch < Session.Slowest || notch > Session.Fastest -> None, [ Refused(NoSuchSpeed notch) ]

        // Asking for the speed it is already at, and asking for quicker at the quickest, are the
        // same thing: nothing to do. Said with silence rather than a refusal, because both are
        // what a player leaning on a key is asking for, and a line of the log per press is a log
        // with the game pushed off the top of it - the screen says which notch it is on.
        | InPlay play, Speed notch when notch = play.Speed -> None, []
        | InPlay play, Faster when play.Speed = Session.Fastest -> None, []
        | InPlay play, Slower when play.Speed = Session.Slowest -> None, []

        | InPlay play, (Faster | Slower | Speed _ as winding) ->
            let notch =
                match winding with
                | Faster -> play.Speed + 1
                | Slower -> play.Speed - 1
                | Speed notch -> notch
                // Nothing else reaches here: the three above are the whole of this case.
                | _ -> play.Speed

            Some(InPlay { play with Speed = notch }), [ Happened(Wound notch) ]

        | InPlay play, Beat ->
            let played, told = beating play
            ending played told

        | InPlay play, Steer(seat, _) when not (List.contains seat play.Seats) -> None, [ Refused(NoSuchSnake seat) ]

        | InPlay play, Steer(seat, _) when not (Snake.isAlive (Session.snakeAt seat play)) -> None, [ Refused(HasStopped seat) ]

        // Where the neck is, rather than which way the head points - and at this pace that is
        // the whole of the rule rather than a nicety. Turning is not moving here, so a player
        // can turn twice inside one beat: north and then east, quicker than the clock, used to
        // leave a snake facing east with its neck still to the east, and the beat after it ran
        // into itself for a pair of presses that were each perfectly legal.
        | InPlay play, Steer(seat, way) when backwards seat way play -> None, [ Refused(CannotTurnBack way) ]

        // Turning to where it already points is not a refusal and not a move: it is what a
        // player leaning on a key is asking for, and the honest answer to it is nothing at all.
        // Answered as a position that did not change, so it stays out of the timeline - a
        // record of a game held at three beats a second has enough in it already.
        | InPlay play, Steer(seat, way) when way = (Session.snakeAt seat play).Facing -> None, []

        | InPlay play, Steer(seat, way) ->
            let snake = Session.snakeAt seat play

            Some(
                InPlay
                    { play with
                        Snakes = Map.add seat { snake with Facing = way } play.Snakes }
            ),
            [ Happened(Turned(seat, way)) ]
