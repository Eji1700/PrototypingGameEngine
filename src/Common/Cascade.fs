namespace TCModel.Common

module Cascade =

    [<NoComparison; NoEquality>]
    type Measure<'Label, 'T> =
        { Label: 'Label
          Score: 'T -> int
          Shown: 'T -> int }

    let by label score =
        { Label = label
          Score = score
          Shown = score }

    let byFewest label count =
        { Label = label
          Score = count >> (~-)
          Shown = count }

    type Step<'Label, 'T> =
        { Label: 'Label
          Standing: ('T * int) list
          Survivors: 'T list }

    let narrowBy score candidates =
        match candidates with
        | []
        | [ _ ] -> candidates
        | _ ->
            let best = candidates |> List.map score |> List.max
            candidates |> List.filter (fun candidate -> score candidate = best)

    let run measures candidates =
        measures
        |> List.fold
            (fun (candidates, steps) measure ->
                if List.length candidates <= 1 then
                    candidates, steps
                else
                    let survivors = narrowBy measure.Score candidates

                    let step =
                        { Label = measure.Label
                          Standing = candidates |> List.map (fun candidate -> candidate, measure.Shown candidate)
                          Survivors = survivors }

                    survivors, steps @ [ step ])
            (candidates, [])
