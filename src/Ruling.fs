/// Who rules a region. The colour holding the most stones there rules it; ties are
/// broken first by the Axe and then by the Flag. Each measure only narrows the field
/// left by the one before, so a colour knocked out never comes back.
module TCModel.Ruling

type Rule =
    /// One colour came through the cascade alone.
    | RuledBy of StoneColor
    /// Two or more colours stayed level all the way through, so nobody rules.
    | Contested of StoneColor list
    /// The region holds no stones at all.
    | Unclaimed

let private measures axe flag stones =
    let by label pile =
        Cascade.measure label (fun color -> Pile.count color pile) (fun color ->
            $"{StoneColor.name color} {Pile.count color pile}")

    [ by "stones in the region" stones
      by "stones in the Axe" axe
      by "stones in the Flag" flag ]

/// Only colours actually present contend: an empty region is ruled by nobody,
/// however the Axe and the Flag happen to stand.
let private contenders stones =
    StoneColor.all |> List.filter (fun color -> Pile.count color stones > 0)

let decide axe flag stones =
    match Cascade.run (measures axe flag stones) (contenders stones) |> fst with
    | [] -> Unclaimed
    | [ color ] -> RuledBy color
    | tied -> Contested tied

/// The cascade written out, for showing the working behind a close call.
let explain axe flag stones =
    let names colors =
        colors |> List.map StoneColor.name |> String.concat ", "

    let candidates, trace = Cascade.run (measures axe flag stones) (contenders stones)

    let verdict =
        match candidates with
        | [] -> "  The region holds no stones, so no colour rules it."
        | [ color ] -> $"  {StoneColor.name color} rules the region."
        | tied -> $"  {names tied} are level after every tie-breaker, so the region is tied and has no ruler."

    Cascade.workings StoneColor.name trace @ [ verdict ]

let describe rule =
    match rule with
    | RuledBy color -> StoneColor.name color
    | Contested tied -> "tied " + (tied |> List.map (StoneColor.glyph >> string) |> String.concat "")
    | Unclaimed -> "-"
