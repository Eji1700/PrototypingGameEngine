namespace TCModel.Turncoats

type RegionId = private RegionId of int

module RegionId =
    let value (RegionId n) = n

type RegionKind =
    | Home of StoneColor
    | Wild
    | Special
    | Dead

module RegionKind =

    let isOpen kind =
        match kind with
        | Dead -> false
        | Home _
        | Wild
        | Special -> true

    let isIsolated kind =
        match kind with
        | Special -> true
        | Home _
        | Wild
        | Dead -> false

    let isLand kind = not (isIsolated kind)

type Region =
    { Id: RegionId
      Name: string
      Kind: RegionKind }

module Board =

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

    let region regionId = byId |> Map.find regionId

    let tryId n =
        if n >= 1 && n <= count then Some(RegionId n) else None

    let private named name =
        regions
        |> List.tryFind (fun region -> region.Name = name)
        |> Option.map (fun region -> region.Id)
        |> Option.defaultValue (RegionId 0)

    let flag = named "The Flag"

    let axe = named "The Axe"

    let landRegions =
        regions |> List.filter (fun region -> RegionKind.isLand region.Kind)

    let apartRegions =
        regions |> List.filter (fun region -> RegionKind.isIsolated region.Kind)

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

    // How the map is drawn: each row starts at an offset and its regions sit two apart, so a
    // column is measured in halves of a cell and a row indented by an odd number of them sits
    // between the regions above it. That is what makes the layout read as a honeycomb.
    let private places =
        [ 2, [ 2; 1 ]; 1, [ 3; 4 ]; 0, [ 5; 6; 7 ]; 1, [ 8; 9; 10 ]; 2, [ 11; 12 ] ]

    let private placed =
        places
        |> List.map (fun (offset, row) -> row |> List.mapi (fun step n -> n, offset + 2 * step))

    let layout =
        placed
        |> List.map (List.choose (fun (n, at) -> tryId n |> Option.map (fun regionId -> regionId, at)))

    let private asPair one other = min one other, max one other

    // Which regions the drawing puts side by side: two apart along a row, or one apart between
    // rows. `problems` checks this against the borders actually declared, so a map that draws two
    // regions touching when they do not is caught rather than left to mislead a player.
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
