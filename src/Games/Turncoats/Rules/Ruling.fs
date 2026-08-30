namespace Prototyping.Turncoats

open Prototyping.Common

type RulingMeasure =
    | StonesInRegion
    | StonesInAxe
    | StonesInFlag

type Rule =
    | RuledBy of StoneColour
    | Contested of StoneColour list
    | Unclaimed

type LandStanding =
    { Ruled: Map<StoneColour, int>
      Tied: int
      Vacant: int }

module Ruling =

    let private measures axe flag stones =
        [ Tiebreak.by StonesInRegion (fun colour -> Pile.count colour stones)
          Tiebreak.by StonesInAxe (fun colour -> Pile.count colour axe)
          Tiebreak.by StonesInFlag (fun colour -> Pile.count colour flag) ]

    let private contenders stones =
        StoneColour.all |> List.filter (fun colour -> Pile.count colour stones > 0)

    let private weigh axe flag stones =
        Tiebreak.run (measures axe flag stones) (contenders stones)

    let decide axe flag stones =
        match weigh axe flag stones |> fst with
        | [] -> Unclaimed
        | [ colour ] -> RuledBy colour
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
                | RuledBy colour -> Some colour
                | Contested _
                | Unclaimed -> None)

        StoneColour.all
        |> List.map (fun colour -> colour, ruled |> List.filter ((=) colour) |> List.length)
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
          Vacant =
            counting (function
                | Unclaimed -> true
                | _ -> false) }
