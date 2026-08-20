namespace TCModel.Engine

type Step<'Move, 'State> = { Move: Msg<'Move>; After: 'State }

type Timeline<'Move, 'State> =
    private
        { Dealt: 'State
          Made: Step<'Move, 'State> list
          TakenBack: Step<'Move, 'State> list }

module Timeline =

    let ofDeal state =
        { Dealt = state
          Made = []
          TakenBack = [] }

    let present timeline =
        match timeline.Made with
        | [] -> timeline.Dealt
        | step :: _ -> step.After

    // Making a move throws away whatever had been taken back: the game only ever has one
    // future, and it is the one just played into.
    let advance move state timeline =
        { timeline with
            Made = { Move = move; After = state } :: timeline.Made
            TakenBack = [] }

    let undo timeline =
        match timeline.Made with
        | [] -> None
        | step :: earlier ->
            Some(
                { timeline with
                    Made = earlier
                    TakenBack = step :: timeline.TakenBack },
                step.Move
            )

    let redo timeline =
        match timeline.TakenBack with
        | [] -> None
        | step :: later ->
            Some(
                { timeline with
                    Made = step :: timeline.Made
                    TakenBack = later },
                step.Move
            )

    let movesMade timeline = List.length timeline.Made

    let movesTakenBack timeline = List.length timeline.TakenBack

    let moves timeline =
        timeline.Made |> List.rev |> List.map (fun step -> step.Move)

    let states timeline =
        timeline.Dealt
        :: (timeline.Made |> List.rev |> List.map (fun step -> step.After))
