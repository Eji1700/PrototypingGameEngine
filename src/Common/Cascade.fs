namespace TCModel.Common

/// Settling a contest by measures applied in order. Each measure only narrows the
/// field the one before it left, so a candidate knocked out early never comes back
/// on a later measure.
///
/// Measures are labelled with whatever the caller likes, so nothing here has to
/// know how a contest reads to a player - that belongs to the presentation.
module Cascade =

    [<NoComparison; NoEquality>]
    type Measure<'Label, 'T> =
        { Label: 'Label
          /// The higher the score, the better. Negate a count to make fewest win.
          Score: 'T -> int
          /// The standing to report, which for a negated score is the plain count.
          Shown: 'T -> int }

    /// A measure where the reported standing is the score itself.
    let by label score =
        { Label = label; Score = score; Shown = score }

    /// A measure where the fewest wins, reported as the plain count.
    let byFewest label count =
        { Label = label
          Score = count >> (~-)
          Shown = count }

    /// One step of the cascade: what the field stood at, and who survived it.
    type Step<'Label, 'T> =
        { Label: 'Label
          Standing: ('T * int) list
          Survivors: 'T list }

    /// Keep only the candidates that lead on this measure. A field of one is already
    /// settled and passes through untouched.
    let narrowBy score candidates =
        match candidates with
        | []
        | [ _ ] -> candidates
        | _ ->
            let best = candidates |> List.map score |> List.max
            candidates |> List.filter (fun candidate -> score candidate = best)

    /// Apply the measures in order, stopping once a single candidate leads. Returns
    /// who is left, along with the steps that were actually needed.
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
