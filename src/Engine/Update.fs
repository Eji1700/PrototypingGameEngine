namespace TCModel.Engine

/// The U of MVU, and the whole of what the engine does with a game: a pure transition from
/// a message and a model to the next model.
///
/// The game owns the rules of a move. What lives here is everything that is true of a
/// turn-based game whatever it is - that asking is written down whether or not it carried,
/// that a game walks backwards and forwards through its own history, that a finished game
/// takes no more moves, that a restart is a fresh deal drawn from the one before it.
///
/// Nothing here can fail, because `Rules.Play` cannot: a refused move is a model with a line
/// in it. That is what lets every table above this be a fold, and every one of them be
/// checked without a keyboard, a screen or a socket.
module Update =

    /// Said out in full rather than with wildcards, and it has to be: dealing is the one
    /// place a model is built rather than folded, so it is the one place where nothing else
    /// ties the model's move and notice to the rules that made it. Left to be inferred, this
    /// would hand back a model generic in both - and a table could then be built out of one
    /// game's rules and another game's moves, which is not a sentence that should typecheck.
    let private dealt (rules: Rules<'Move, 'State, 'Notice>) players seed : Result<Model<'Move, 'State, 'Notice>, string> =
        rules.Deal players seed
        |> Result.map (fun state ->
            { Timeline = Timeline.ofDeal state
              Journal = Journal.ofDeal players seed
              Log = [] })

    /// Ask for a move. Whatever comes of it, the record gains a line: a refusal leaves the
    /// position exactly where it was, but it was still asked for.
    let private make (rules: Rules<_, _, _>) move model =
        let asked = Make move
        let standing = Model.state model

        if rules.Over standing then
            model |> Model.happen rules asked [ GameIsOver ] model.Timeline
        else
            match rules.Play move standing with
            | Some state, told ->
                let timeline = Timeline.advance asked state model.Timeline
                model |> Model.happen rules asked (told |> List.map Said) timeline
            | None, told -> model |> Model.happen rules asked (told |> List.map Said) model.Timeline

    /// Step the finger back or forward along the timeline. Either way the move is written
    /// into the record, so the record shows the game as it was really played.
    let private walk rules asked step nothingThere told model =
        match step model.Timeline with
        | Some(timeline, move) -> model |> Model.happen rules asked [ told move ] timeline
        | None -> model |> Model.happen rules asked [ nothingThere ] model.Timeline

    /// Abandon this game and deal another. The old record is left behind whole - it is the
    /// table's business to have saved it - and the new game starts its own.
    let private restart (rules: Rules<_, _, _>) players seed model =
        let standing = Model.state model
        let players = players |> Option.defaultValue (rules.Seats standing)
        let seed = seed |> Option.defaultValue (rules.Reseed standing)

        match dealt rules players seed with
        | Ok fresh -> fresh
        | Error problem -> model |> Model.record (Misunderstood problem)

    let update rules msg model =
        match msg with
        | Make move -> make rules move model
        | Undo -> walk rules Undo Timeline.undo NothingToTakeBack TookBack model
        | Redo -> walk rules Redo Timeline.redo NothingToMakeAgain MadeAgain model
        | Restart(players, seed) -> restart rules players seed model

    /// Record something nobody could make sense of. It never became a move, so it stays out
    /// of the record and merely goes up on screen.
    let note text model =
        model |> Model.record (Misunderstood text)

    /// Deal the first game of a session.
    let start rules players seed = dealt rules players seed

    /// Play a whole game again from its deal. Because `update` is pure and a game keeps its
    /// own generator, folding the recorded moves over a fresh deal arrives at exactly the
    /// state they were recorded from - undone moves and all.
    let replay rules players seed moves =
        dealt rules players seed
        |> Result.map (fun model -> moves |> List.fold (fun model msg -> update rules msg model) model)
