namespace TCModel

/// Settling a contest by measures applied in order. Each measure only narrows the
/// field the one before it left, so a candidate knocked out early never comes back
/// on a later measure.
module Cascade =

    [<NoComparison; NoEquality>]
    type Measure<'T> =
        { Label: string
          /// The higher the score, the better. Negate a count to make fewest win.
          Score: 'T -> int
          /// How a candidate's standing reads when the working is shown.
          Reading: 'T -> string }

    let measure label score reading =
        { Label = label
          Score = score
          Reading = reading }

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
    /// who is left, along with a record of the measures that were actually needed.
    let run measures candidates =
        measures
        |> List.fold
            (fun (candidates, trace) measure ->
                if List.length candidates <= 1 then
                    candidates, trace
                else
                    let narrowed = narrowBy measure.Score candidates
                    let readings = candidates |> List.map measure.Reading
                    narrowed, trace @ [ (measure.Label, readings, narrowed) ])
            (candidates, [])

    /// The working, one line for each measure that was needed.
    let workings describe trace =
        trace
        |> List.map (fun (label, readings, narrowed) ->
            let outcome =
                match narrowed with
                | [ one ] -> $"{describe one} leads"
                | many -> (many |> List.map describe |> String.concat ", ") + " still level"

            let standing = String.concat ", " readings
            $"  {label}: {standing} -> {outcome}")
