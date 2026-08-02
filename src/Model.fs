namespace TCModel

type GameStatus =
    | InProgress
    | Over of reason: string

/// The single immutable value describing the whole game.
type Model =
    { Seed: uint64
      /// Generator state, threaded through updates so play stays reproducible.
      Rng: Rng
      Regions: Map<RegionId, Region>
      /// Which regions border which. Fixed for the whole game, but carried here so
      /// that the model describes the map on its own.
      Adjacency: Map<RegionId, Set<RegionId>>
      Players: Player list
      /// Stones that were never dealt out during setup.
      Reserve: Pile
      Active: PlayerId
      Turn: int
      /// Newest entry first.
      Log: string list
      Status: GameStatus }

/// Everything the game can be asked to do.
type Msg =
    /// Move a stone of the given colour from the active player's bag into a region.
    | Place of StoneColor * RegionId
    /// Give up the turn without placing.
    | Pass
    /// Abandon this game and deal a fresh one. Anything left unspecified is carried
    /// over from the game in progress.
    | Restart of players: int option * seed: uint64 option
    | Quit

module Model =

    let activePlayer model =
        model.Players |> List.find (fun player -> player.Id = model.Active)

    let tryRegion regionId model = model.Regions |> Map.tryFind regionId

    /// Regions in board order (Map keys sort by their underlying id).
    let regions model = model.Regions |> Map.toList |> List.map snd

    let regionsOfKind predicate model =
        regions model |> List.filter (fun region -> predicate region.Kind)

    /// The regions bordering the given one, by id.
    let neighbours regionId model =
        model.Adjacency |> Map.tryFind regionId |> Option.defaultValue Set.empty

    let areAdjacent one other model = neighbours one model |> Set.contains other

    /// The regions bordering the given one, in board order.
    let neighbouringRegions regionId model =
        neighbours regionId model |> Set.toList |> List.choose (fun id -> tryRegion id model)

    let stonesOnBoard model =
        regions model
        |> List.fold (fun pile region -> region.Stones |> Pile.toCounts |> List.fold (fun p (c, n) -> Pile.add c n p) pile) Pile.empty

    /// The player who acts after the given one, wrapping around the table.
    let nextPlayer playerId model =
        let order = model.Players |> List.map (fun player -> player.Id)
        let index = order |> List.findIndex (fun id -> id = playerId)
        order[(index + 1) % order.Length]

    let playerCount model = List.length model.Players

    let allBagsEmpty model =
        model.Players |> List.forall (fun player -> Pile.isEmpty player.Bag)
