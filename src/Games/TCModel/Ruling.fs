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

/// How the land stands: how much of it each colour rules, and how much is still going
/// spare. Dead ground is left out of the last two - it is unclaimed and always will be,
/// so counting it says nothing about how much is still to play for.
type LandStanding =
    { Ruled: Map<StoneColor, int>
      Tied: int
      Unclaimed: int }

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

    // --- the same, read off a whole position ------------------------------------------
    //
    // Who rules what is a fact about where the stones are standing and nothing else: the
    // Axe and the Flag are two more regions, and a position always has all of them. So it
    // is settled here, from a position, rather than needing a game to be asked - which is
    // what lets somebody weighing a position they are only allowed to *see* weigh it by the
    // same reckoning the game itself uses, instead of keeping a second copy of it.

    let over regionId position =
        decide (Position.stones Board.axe position) (Position.stones Board.flag position) (Position.stones regionId position)

    /// The cascade behind that verdict.
    let weighing regionId position =
        weigh (Position.stones Board.axe position) (Position.stones Board.flag position) (Position.stones regionId position)

    /// Ruling over ground only. The Flag and the Axe hold stones and can be ruled like
    /// anywhere else, but they are manoeuvres rather than land.
    let landRulings position =
        Board.landRegions |> List.map (fun region -> region, over region.Id position)

    /// How much land each colour rules, in canonical colour order.
    let standings position =
        let ruled =
            landRulings position
            |> List.choose (fun (_, rule) ->
                match rule with
                | RuledBy color -> Some color
                | Contested _
                | Unclaimed -> None)

        StoneColor.all
        |> List.map (fun color -> color, ruled |> List.filter ((=) color) |> List.length)
        |> Map.ofList

    /// The whole standing of the land at once. Worked out here rather than wherever it
    /// happens to be shown, because it is a fact about the position and not about the
    /// drawing of it - and a second view counting it again for itself could count it
    /// differently.
    let landStanding position =
        let open' =
            landRulings position
            |> List.filter (fun (region, _) -> RegionKind.isOpen region.Kind)

        let counting predicate =
            open' |> List.filter (snd >> predicate) |> List.length

        { Ruled = standings position
          Tied =
            counting (function
                | Contested _ -> true
                | _ -> false)
          Unclaimed =
            counting (function
                | Unclaimed -> true
                | _ -> false) }
