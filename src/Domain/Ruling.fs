namespace TCModel.Domain

open TCModel.Common

/// The measures that decide who rules a region, in the order they are applied.
type RulingMeasure =
    | StonesInRegion
    | StonesInAxe
    | StonesInFlag

type Rule =
    /// One colour came through the cascade alone.
    | RuledBy of StoneColor
    /// Two or more colours stayed level all the way through, so nobody rules.
    | Contested of StoneColor list
    /// The region holds no stones at all.
    | Unclaimed

/// Who rules a region. The colour holding the most stones there rules it; ties are
/// broken first by the Axe and then by the Flag.
module Ruling =

    let private measures axe flag stones =
        [ Cascade.by StonesInRegion (fun color -> Pile.count color stones)
          Cascade.by StonesInAxe (fun color -> Pile.count color axe)
          Cascade.by StonesInFlag (fun color -> Pile.count color flag) ]

    /// Only colours actually present contend: an empty region is ruled by nobody,
    /// however the Axe and the Flag happen to stand.
    let private contenders stones =
        StoneColor.all |> List.filter (fun color -> Pile.count color stones > 0)

    /// Who is left after the cascade, and the steps it took to get there.
    let weigh axe flag stones =
        Cascade.run (measures axe flag stones) (contenders stones)

    let decide axe flag stones =
        match weigh axe flag stones |> fst with
        | [] -> Unclaimed
        | [ color ] -> RuledBy color
        | tied -> Contested tied
