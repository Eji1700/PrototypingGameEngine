namespace Prototyping.Common

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

    // A measure always reads as "higher wins", so a count that should win when it is lowest
    // is negated for the comparison. Shown keeps the plain count, which is what a standing
    // is written out as.
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

    // Each measure only narrows the field the one before it left, so a candidate knocked out
    // early never comes back on a later one. Once a single candidate leads nothing further
    // is recorded, which is how a caller can tell which measures actually settled it.
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
