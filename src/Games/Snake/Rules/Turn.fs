namespace TCModel.Snake

open TCModel.Engine

/// Everything a player may ask this game to do: go a way, go on the way you were going, or
/// stop.
///
/// There is no move that does not move. Which is the shape of this game rather than a gap in
/// it - a snake that stood still would be a snake that could wait out the board.
type Move =
    | Go of Direction
    /// Straight on, which is what a player wanting the same thing again means and would
    /// otherwise have to remember for themselves.
    | Onward
    | Resign

type Happening =
    | Went of PlayerId * Direction
    | Ate of PlayerId * eaten: int * grown: int
    | Stopped of PlayerId * Fate
    | GameEnded of Ending

/// The one thing this game refuses, and the only rule of it nobody thinks of as a rule.
type Refusal = CannotTurnBack of Direction

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

    /// What the engine asks of a game: a move and where it stands, and the position it left
    /// along with whatever there is to say.
    ///
    /// Total, like every game's: `None` for the position means nothing moved, and the notices
    /// say why. A refusal is something this game *says*, not something that breaks it, which
    /// is what lets every table above be a fold.
    let asked move session =
        match session, move with
        // The engine refuses moves after the game is over and says so itself, so this is
        // unreachable rather than wrong. Answered all the same, because a total function is
        // cheaper than an argument about which of two files is guarding it.
        | Finished _, _ -> None, []

        | InPlay play, Resign ->
            let seat = play.ToPlay

            let stopped =
                { play with
                    Snakes = Map.add seat (Snake.stopped GaveUp (Session.snakeAt seat play)) play.Snakes }

            let told = [ Happened(Stopped(seat, GaveUp)) ]

            match Session.finished stopped with
            | Some ending -> Some(Finished(stopped, ending)), told @ [ Happened(GameEnded ending) ]
            | None -> Some(InPlay(Session.onwards stopped)), told

        | InPlay play, Onward -> taking play.ToPlay (Session.snakeAt play.ToPlay play).Facing play

        // The one refusal. A snake of one segment could turn back into nothing and would still
        // be refused: which way it may not go is a fact about the way it is facing, not about
        // how long it is, and a rule that changed with the length would be one nobody could
        // hold in their head.
        | InPlay play, Go direction when direction = Direction.opposite (Session.snakeAt play.ToPlay play).Facing ->
            None, [ Refused(CannotTurnBack direction) ]

        | InPlay play, Go direction -> taking play.ToPlay direction play
