namespace TCModel.Domain

/// Identifies a region of the map. The case is private to this file, so the only
/// way to get one is from the board below - which means a `RegionId` always names a
/// region that exists, and looking one up never fails.
type RegionId = private RegionId of int

module RegionId =
    let value (RegionId n) = n

/// What a region is, which decides how it is seeded and who may enter it.
type RegionKind =
    /// The heartland of a colour, seeded with that colour's stones.
    | Home of StoneColor
    /// Contested ground, seeded at random.
    | Wild
    /// A manoeuvre rather than ground: nothing borders it, it starts empty, and it
    /// is no part of the land a faction holds.
    | Special
    /// Ground no stone may ever enter.
    | Dead

module RegionKind =

    /// Whether stones may be placed here at all.
    let isOpen kind =
        match kind with
        | Dead -> false
        | Home _
        | Wild
        | Special -> true

    /// Whether the region stands outside the map: bordering nothing, and not land.
    /// The dead region is deliberately not isolated - adjacency runs through it.
    let isIsolated kind =
        match kind with
        | Special -> true
        | Home _
        | Wild
        | Dead -> false

    /// Whether holding this region counts towards the land a faction rules.
    let isLand kind = not (isIsolated kind)

/// A region as the map defines it. Nothing here changes during play.
type Region =
    { Id: RegionId
      Name: string
      Kind: RegionKind }

/// The fixed shape of the map: which regions exist, and which of them border each other.
module Board =

    /// The regions in board order. A region's position in this list is its number.
    let private table =
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

    let regions =
        table
        |> List.mapi (fun index (name, kind) ->
            { Id = RegionId(index + 1)
              Name = name
              Kind = kind })

    let count = List.length regions

    let ids = regions |> List.map (fun region -> region.Id)

    let private byId = regions |> List.map (fun region -> region.Id, region) |> Map.ofList

    /// Total, because a RegionId can only have come from this board.
    let region regionId = byId |> Map.find regionId

    /// The only way in from the outside: a number a player typed.
    let tryId n =
        if n >= 1 && n <= count then Some(RegionId n) else None

    let private named name =
        regions
        |> List.tryFind (fun region -> region.Name = name)
        |> Option.map (fun region -> region.Id)
        // Guarded by `problems` below, which stops the game before a deal.
        |> Option.defaultValue (RegionId 0)

    /// The region a march is declared through.
    let flag = named "The Flag"

    /// The region a battle is declared through.
    let axe = named "The Axe"

    /// The regions that count as ground held, which is everything but the Flag and
    /// the Axe. The dead region is land nobody can ever take.
    let landRegions =
        regions |> List.filter (fun region -> RegionKind.isLand region.Kind)

    /// Region numbers paired with the regions they border. Borders are symmetrised
    /// below, so a border only has to be named from one end.
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

    /// Every declared border taken both ways round. Regions that border nothing
    /// still appear, mapped to the empty set.
    let private adjacency =
        let empty = ids |> List.map (fun regionId -> regionId, Set.empty) |> Map.ofList

        declaredBorders
        |> List.collect (fun (from, borders) -> borders |> List.collect (fun other -> [ from, other; other, from ]))
        |> List.distinct
        |> List.fold
            (fun map (from, other) ->
                match tryId from, tryId other with
                | Some from, Some other ->
                    let neighbours = map |> Map.tryFind from |> Option.defaultValue Set.empty
                    Map.add from (Set.add other neighbours) map
                | _ -> map)
            empty

    let neighbours regionId =
        adjacency |> Map.tryFind regionId |> Option.defaultValue Set.empty

    let areAdjacent one other = neighbours one |> Set.contains other

    /// Every region reachable from the start by crossing borders, optionally
    /// treating some regions as impassable.
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

    /// Ways in which the declared map fails to make sense. A non-empty list is a bug
    /// in the tables above, not something a player can cause, so the game refuses to
    /// start rather than dealing onto a broken map.
    let problems =
        [ for from, borders in declaredBorders do
              if Option.isNone (tryId from) then
                  yield $"Region {from} is not on the board."

              for other in borders do
                  if Option.isNone (tryId other) then
                      yield $"Region {from} borders {other}, which is not on the board."
                  elif other = from then
                      yield $"Region {from} borders itself."

          for region in regions do
              match RegionKind.isIsolated region.Kind, Set.isEmpty (neighbours region.Id) with
              | true, false -> yield $"{region.Name} is meant to stand alone but borders other regions."
              | false, true -> yield $"{region.Name} borders nothing, so no stone can ever reach it."
              | _ -> ()

          for label, regionId in [ "The Flag", flag; "The Axe", axe ] do
              match regions |> List.tryFind (fun region -> region.Id = regionId) with
              | None -> yield $"{label} is missing from the board, but actions are declared through it."
              | Some found ->
                  if not (RegionKind.isIsolated found.Kind) then
                      yield $"{label} must be a region that stands alone."

          match regions |> List.filter (fun region -> not (RegionKind.isIsolated region.Kind)) with
          | [] -> ()
          | first :: _ ->
              let reached = reachableFrom Set.empty first.Id

              for region in regions do
                  if not (RegionKind.isIsolated region.Kind) && not (Set.contains region.Id reached) then
                      yield $"{region.Name} is cut off from the rest of the map." ]
