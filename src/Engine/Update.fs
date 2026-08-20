namespace TCModel.Engine

module Update =

    let private dealt (rules: Rules<'Move, 'State, 'Notice>) players seed : Result<Model<'Move, 'State, 'Notice>, string> =
        rules.Deal players seed
        |> Result.map (fun state ->
            { Timeline = Timeline.ofDeal state
              Journal = Journal.ofDeal players seed
              Log = [] })

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

    let private walk rules asked step nothingThere told model =
        match step model.Timeline with
        | Some(timeline, move) -> model |> Model.happen rules asked [ told move ] timeline
        | None -> model |> Model.happen rules asked [ nothingThere ] model.Timeline

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

    let note text model =
        model |> Model.record (Misunderstood text)

    let start rules players seed = dealt rules players seed

    let replay rules players seed moves =
        dealt rules players seed
        |> Result.map (fun model -> moves |> List.fold (fun model msg -> update rules msg model) model)
