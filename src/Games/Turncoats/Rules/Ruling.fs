namespace TCModel.Turncoats

open TCModel.Common

type RulingMeasure =
    | StonesInRegion
    | StonesInAxe
    | StonesInFlag

type Rule =
    | RuledBy of StoneColor
    | Contested of StoneColor list
    | Unclaimed

type LandStanding =
    { Ruled: Map<StoneColor, int>
      Tied: int
      Unclaimed: int }

module Ruling =

    let private measures axe flag stones =
        [ Cascade.by StonesInRegion (fun color -> Pile.count color stones)
          Cascade.by StonesInAxe (fun color -> Pile.count color axe)
          Cascade.by StonesInFlag (fun color -> Pile.count color flag) ]

    let private contenders stones =
        StoneColor.all |> List.filter (fun color -> Pile.count color stones > 0)

    let weigh axe flag stones =
        Cascade.run (measures axe flag stones) (contenders stones)

    let decide axe flag stones =
        match weigh axe flag stones |> fst with
        | [] -> Unclaimed
        | [ color ] -> RuledBy color
        | tied -> Contested tied


    let over regionId position =
        decide (Position.stones Board.axe position) (Position.stones Board.flag position) (Position.stones regionId position)

    let weighing regionId position =
        weigh (Position.stones Board.axe position) (Position.stones Board.flag position) (Position.stones regionId position)

    let landRulings position =
        Board.landRegions |> List.map (fun region -> region, over region.Id position)

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
