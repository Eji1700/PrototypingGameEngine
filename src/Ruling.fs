/// Who rules a region. The colour holding the most stones there rules it; ties are
/// broken first by the Axe and then by the Flag. Each measure only narrows the field
/// left by the one before, so a colour knocked out early never returns.
module TCModel.Ruling

type Rule =
    /// One colour came through the cascade alone.
    | RuledBy of StoneColor
    /// Two or more colours stayed level all the way through, so nobody rules.
    | Contested of StoneColor list
    /// The region holds no stones at all.
    | Unclaimed

/// Keep only the candidates that lead on this measure. A field of one is already
/// settled and passes through untouched.
let private narrowBy measure candidates =
    match candidates with
    | []
    | [ _ ] -> candidates
    | _ ->
        let best = candidates |> List.map measure |> List.max
        candidates |> List.filter (fun color -> measure color = best)

/// Run the cascade, keeping a record of each measure that was actually needed.
let private cascade axe flag stones =
    let stages =
        [ "stones in the region", (fun color -> Pile.count color stones)
          "stones in the Axe", (fun color -> Pile.count color axe)
          "stones in the Flag", (fun color -> Pile.count color flag) ]

    // Only colours actually present contend: an empty region is ruled by nobody,
    // however the Axe and the Flag happen to stand.
    let present =
        StoneColor.all |> List.filter (fun color -> Pile.count color stones > 0)

    stages
    |> List.fold
        (fun (candidates, trace) (label, measure) ->
            if List.length candidates <= 1 then
                candidates, trace
            else
                let narrowed = narrowBy measure candidates
                let tallies = candidates |> List.map (fun color -> color, measure color)
                narrowed, trace @ [ (label, tallies, narrowed) ])
        (present, [])

let decide axe flag stones =
    match cascade axe flag stones |> fst with
    | [] -> Unclaimed
    | [ color ] -> RuledBy color
    | tied -> Contested tied

/// The cascade written out, for showing the working behind a close call.
let explain axe flag stones =
    let names colors =
        colors |> List.map StoneColor.name |> String.concat ", "

    let counts pairs =
        pairs |> List.map (fun (color, n) -> $"{StoneColor.name color} {n}") |> String.concat ", "

    let candidates, trace = cascade axe flag stones

    let steps =
        trace
        |> List.map (fun (label, tallies, narrowed) ->
            match narrowed with
            | [ color ] -> $"  {label}: {counts tallies} -> {StoneColor.name color} leads"
            | _ -> $"  {label}: {counts tallies} -> {names narrowed} still level")

    let verdict =
        match candidates with
        | [] -> "  The region holds no stones, so no colour rules it."
        | [ color ] -> $"  {StoneColor.name color} rules the region."
        | tied -> $"  {names tied} are level after every tie-breaker, so the region is tied and has no ruler."

    steps @ [ verdict ]

let describe rule =
    match rule with
    | RuledBy color -> StoneColor.name color
    | Contested tied -> "tied " + (tied |> List.map (StoneColor.glyph >> string) |> String.concat "")
    | Unclaimed -> "-"
