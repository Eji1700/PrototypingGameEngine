namespace TCModel.Turncoats

/// The stones standing on the map. Every board region has an entry, so asking what
/// is in a region always answers.
type Position = private Position of Map<RegionId, Pile>

module Position =

    let empty =
        Board.ids
        |> List.map (fun regionId -> regionId, Pile.empty)
        |> Map.ofList
        |> Position

    let stones regionId (Position piles) = piles |> Map.find regionId

    let withStones regionId pile (Position piles) =
        Position(piles |> Map.add regionId pile)

    let add color n regionId position =
        position |> withStones regionId (Pile.add color n (stones regionId position))

    let remove color n regionId position =
        position |> withStones regionId (Pile.remove color n (stones regionId position))

    /// Every region paired with what stands in it, in board order.
    let all position =
        Board.regions |> List.map (fun region -> region, stones region.Id position)

    /// Every stone on the map, of any colour.
    let total position =
        Board.ids
        |> List.fold (fun pile regionId -> Pile.merge (stones regionId position) pile) Pile.empty
