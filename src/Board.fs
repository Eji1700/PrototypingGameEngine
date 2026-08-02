/// The fixed shape of the map: which regions exist, and which of them border each other.
/// Nothing here changes during play.
module TCModel.Board

/// The regions in board order. A region's position in this list is its number.
let regions =
    [ "Emberfall", Home Red
      "Tidewatch", Home Blue
      "Nightfen", Home Black
      "The Crossroads", Wild
      "Greymarket", Wild
      "Saltmarsh", Wild
      "Thornwood", Wild
      "Ironford", Wild
      "Windgap", Wild
      "Stonecradle", Wild
      "Dunmoor", Wild
      "The Flag", Special
      "The Axe", Special
      "The Hollow Waste", Dead ]

let count = List.length regions

/// Region numbers paired with the regions they border. Every border here is named
/// from both ends, and `adjacency` symmetrises anyway, so a border added later only
/// has to be named once.
let private declaredBorders =
    [ 1, [ 4; 5; 14 ]
      5, [ 1; 14; 7; 6 ]
      6, [ 3; 7; 5 ]
      4, [ 1; 14; 9; 8 ]
      14, [ 4; 1; 5; 7; 10; 9 ]
      7, [ 14; 5; 6; 3; 10 ]
      3, [ 6; 7 ]
      8, [ 4; 9; 11 ]
      9, [ 8; 4; 14; 10; 2; 11 ]
      10, [ 9; 14; 7; 2 ]
      11, [ 8; 9; 2 ]
      2, [ 11; 9; 10 ] ]

/// Every declared border, taken both ways round, as a lookup from region to neighbours.
/// Regions that border nothing still appear, mapped to the empty set.
let adjacency: Map<RegionId, Set<RegionId>> =
    let empty = [ for n in 1..count -> RegionId n, Set.empty ] |> Map.ofList

    declaredBorders
    |> List.collect (fun (from, borders) -> borders |> List.collect (fun other -> [ from, other; other, from ]))
    |> List.distinct
    |> List.fold
        (fun map (from, other) ->
            let from = RegionId from
            let neighbours = map |> Map.tryFind from |> Option.defaultValue Set.empty
            Map.add from (Set.add (RegionId other) neighbours) map)
        empty

/// The regions bordering the given one; empty for regions that stand alone.
let neighbours regionId =
    adjacency |> Map.tryFind regionId |> Option.defaultValue Set.empty

/// Every region reachable from the start by crossing borders, optionally treating
/// some regions as impassable.
let reachableFrom (blocked: Set<RegionId>) start =
    let rec walk seen frontier =
        match frontier with
        | [] -> seen
        | regionId :: rest when Set.contains regionId seen -> walk seen rest
        | regionId :: rest ->
            let next =
                neighbours regionId |> Set.filter (fun id -> not (Set.contains id blocked)) |> Set.toList

            walk (Set.add regionId seen) (next @ rest)

    walk Set.empty [ start ]

/// Ways in which the declared map fails to make sense. A non-empty list is a bug in
/// the table above, not something a player can cause, so the game refuses to start.
let problems =
    let onBoard n = n >= 1 && n <= count
    let named = regions |> List.mapi (fun index (name, kind) -> RegionId(index + 1), name, kind)

    let mainland =
        named |> List.filter (fun (_, _, kind) -> not (RegionKind.isIsolated kind))

    [ for from, borders in declaredBorders do
          if not (onBoard from) then
              yield $"Region {from} is not on the board."

          for other in borders do
              if not (onBoard other) then
                  yield $"Region {from} borders {other}, which is not on the board."
              elif other = from then
                  yield $"Region {from} borders itself."

      for regionId, name, kind in named do
          match RegionKind.isIsolated kind, Set.isEmpty (neighbours regionId) with
          | true, false -> yield $"{name} is meant to stand alone but borders other regions."
          | false, true -> yield $"{name} borders nothing, so no stone can ever reach it."
          | _ -> ()

      match mainland with
      | [] -> ()
      | (start, _, _) :: _ ->
          let reached = reachableFrom Set.empty start

          for regionId, name, _ in mainland do
              if not (Set.contains regionId reached) then
                  yield $"{name} is cut off from the rest of the map." ]
