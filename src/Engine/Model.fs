namespace TCModel.Engine

type Model<'Move, 'State, 'Notice> =
    { Timeline: Timeline<'Move, 'State>
      Journal: Journal<'Move, 'Notice>
      Log: Told<'Move, 'Notice> list }

module Model =

    [<Literal>]
    let LogDepth = 12

    let state model = Timeline.present model.Timeline

    let seed model = Journal.seed model.Journal

    let players model = Journal.players model.Journal

    let record notice model =
        { model with
            Log = notice :: model.Log |> List.truncate LogDepth }

    let happen (rules: Rules<_, _, _>) asked told timeline model =
        let asking = state model

        { model with
            Timeline = timeline
            Journal = Journal.write (rules.Turn asking) (rules.Active asking) asked told model.Journal
            Log = (List.rev told) @ model.Log |> List.truncate LogDepth }
