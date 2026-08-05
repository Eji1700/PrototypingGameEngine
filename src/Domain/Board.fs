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
    ///
    /// The order runs across the map rather than by kind, so that neighbours carry
    /// nearby numbers: no border joins regions more than three apart, and most
    /// regions border the one numbered either side of them. The mainland - homes,
    /// wilds and the dead region - takes 1 to 12; the Flag and the Axe, which are no
    /// part of the map, come last.
    let private table =
        [ "Nightfen", Home Green
          "Saltmarsh", Wild
          "Greymarket", Wild
          "Thornwood", Wild
          "Emberfall", Home Red
          "The Hollow Waste", Dead
          "Stonecradle", Wild
          "The Crossroads", Wild
          "Windgap", Wild
          "Tidewatch", Home Blue
          "Ironford", Wild
          "Dunmoor", Wild
          "The Flag", Special
          "The Axe", Special ]

    let regions =
        table
        |> List.mapi (fun index (name, kind) ->
            { Id = RegionId(index + 1)
              Name = name
              Kind = kind })

    let count = List.length regions

    let ids = regions |> List.map (fun region -> region.Id)

    let private byId =
        regions |> List.map (fun region -> region.Id, region) |> Map.ofList

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

    /// The other two: bought with stones, but no part of the map and no part of the
    /// land. Every view draws them clear of the map, so which they are is asked here
    /// rather than worked out again in each.
    let apartRegions =
        regions |> List.filter (fun region -> RegionKind.isIsolated region.Kind)

    /// Region numbers paired with the regions they border. Borders are symmetrised
    /// below, so a border only has to be named from one end: each region names only
    /// the neighbours numbered above it, which - given the numbering above - are
    /// never more than three away.
    let private declaredBorders =
        [ 1, [ 2; 4 ]
          2, [ 3; 4 ]
          3, [ 4; 5; 6 ]
          4, [ 6; 7 ]
          5, [ 6; 8 ]
          6, [ 7; 8; 9 ]
          7, [ 9; 10 ]
          8, [ 9; 11 ]
          9, [ 10; 11; 12 ]
          10, [ 12 ]
          11, [ 12 ] ]

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

    /// Where the regions lie, so the map can be drawn as a map. The mainland is a
    /// patch of a triangular lattice: rows run north to south, each row starting at
    /// the half-column given in front of it and its regions standing two half-columns
    /// apart, so that one row is offset half a step from the next.
    ///
    /// Two regions border each other exactly when they stand two half-columns apart in
    /// the same row, or one half-column apart in rows that touch. So every border can
    /// be drawn as a line and no line has to be drawn that is not a border - which is
    /// what `problems` checks the tables against each other for. The Flag and the Axe
    /// lie nowhere, being no part of the map.
    let private places =
        [ 2, [ 2; 1 ]; 1, [ 3; 4 ]; 0, [ 5; 6; 7 ]; 1, [ 8; 9; 10 ]; 2, [ 11; 12 ] ]

    let private placed =
        places
        |> List.map (fun (offset, row) -> row |> List.mapi (fun step n -> n, offset + 2 * step))

    /// The regions row by row, each with the half-column it stands in.
    let layout =
        placed
        |> List.map (List.choose (fun (n, at) -> tryId n |> Option.map (fun regionId -> regionId, at)))

    let private asPair one other = min one other, max one other

    /// The borders the layout puts side by side, which should be all of them.
    let private drawnBorders =
        Set.ofList
            [ for row in placed do
                  for one, here in row do
                      for other, there in row do
                          if one <> other && abs (here - there) = 2 then yield asPair one other

              for above, below in List.pairwise placed do
                  for one, here in above do
                      for other, there in below do
                          if abs (here - there) = 1 then yield asPair one other ]

    let private namedBorders =
        Set.ofList
            [ for from, borders in declaredBorders do
                  for other in borders do
                      yield asPair from other ]

    /// Every region reachable from the start by crossing borders, optionally
    /// treating some regions as impassable.
    let reachableFrom (blocked: Set<RegionId>) start =
        let rec walk seen frontier =
            match frontier with
            | [] -> seen
            | regionId :: rest when Set.contains regionId seen -> walk seen rest
            | regionId :: rest ->
                let next =
                    neighbours regionId
                    |> Set.filter (fun id -> not (Set.contains id blocked))
                    |> Set.toList

                walk (Set.add regionId seen) (next @ rest)

        walk Set.empty [ start ]

    /// Ways in which the declared map fails to make sense. A non-empty list is a bug
    /// in the tables above, not something a player can cause, so the game refuses to
    /// start rather than dealing onto a broken map.
    let problems =
        [ for from, borders in declaredBorders do
              if Option.isNone (tryId from) then yield $"Region {from} is not on the board."

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

          // The map as drawn has to be the map as declared, or the picture lies.
          for n, _ in List.concat placed do
              if Option.isNone (tryId n) then
                  yield $"The map lays out region {n}, which is not on the board."

          for region in regions do
              let times =
                  List.concat placed
                  |> List.filter (fst >> (=) (RegionId.value region.Id))
                  |> List.length

              match RegionKind.isIsolated region.Kind, times with
              | true, 0
              | false, 1 -> ()
              | true, _ -> yield $"{region.Name} stands apart from the map, but the map lays it out."
              | false, 0 -> yield $"{region.Name} is nowhere on the map."
              | false, n -> yield $"{region.Name} is laid out on the map {n} times."

          for one, other in Set.difference namedBorders drawnBorders do
              yield $"Regions {one} and {other} border each other, but the map does not lay them side by side."

          for one, other in Set.difference drawnBorders namedBorders do
              yield $"The map lays regions {one} and {other} side by side, but they share no border."

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
