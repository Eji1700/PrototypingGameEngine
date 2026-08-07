namespace TCModel.Engine

/// Seats played by something that is not a person, and when they take their turns.
///
/// What a machine *chooses* is the game's own and is nowhere near here - it comes in as one
/// function, from a seat's state to a move. What is here is only the *when*, which is the
/// same at every game: a machine plays as soon as the game reaches its seat, and the run of
/// them between one person's move and their next is played out before the prompt comes back.
///
/// Here rather than at either table, because both tables want it and there is one answer. A
/// machine at one keyboard and a machine at a seat nobody drove to are the same machine.
module Machines =

    /// The machine at the seat to act, if that seat is one of theirs and there is still a
    /// game to play.
    let private toAct (rules: Rules<_, _, _>) rivals model =
        let standing = Model.state model

        if rules.Over standing then
            None
        else
            rivals |> List.tryFind (fst >> (=) (rules.Active standing)) |> Option.map snd

    /// Whether the game is standing at a seat a machine plays. Asked by a table walking a
    /// game backwards: a move taken back has to take the machines' answers to it back with
    /// it, or one `undo` would simply be answered again before the board was looked at.
    let holds rules rivals model =
        toAct rules rivals model |> Option.isSome

    let private withRival playerId rival rivals =
        rivals
        |> List.map (fun (other, was) -> if other = playerId then other, rival else other, was)

    /// Let the machines take their turns, and give back the game they left and themselves as
    /// they now stand - each having moved its own generator on.
    ///
    /// They play one after another for as long as the seat to act is one of theirs, so a line
    /// typed by a person is answered by everybody between them and their next turn - which is
    /// what sitting down opposite a machine looks like.
    ///
    /// A move that left the game exactly where it found it stops them, and that is the whole
    /// of what stops them. Nothing a machine picks should be refused - a sensible one asks
    /// the rules what they will take before it chooses - but one that had somehow found a
    /// move the rules would not have would otherwise be asked for it again, and again, with
    /// the turn never passing and nothing on the screen to say why.
    let rec answering rules plays rivals model =
        match toAct rules rivals model with
        | None -> model, rivals
        | Some rival ->
            let seat = rules.Active(Model.state model)

            match plays (Model.state model) rival with
            | None -> model, rivals
            | Some(move, rival) ->
                let next = Update.update rules (Make move) model
                let rivals = withRival seat rival rivals

                // Whether the position moved, asked of the history rather than of the two
                // states: a refused move leaves the timeline exactly as long as it was, and
                // that is a cheaper question than whether two whole games are the same one -
                // and one the engine can ask of a game it knows nothing about.
                if Timeline.movesMade next.Timeline = Timeline.movesMade model.Timeline then
                    next, rivals
                else
                    answering rules plays rivals next
