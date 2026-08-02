namespace TCModel

type GameStatus =
    | InProgress
    | Over of reason: string

/// A decision the active player owes before their turn can end.
type Pending =
    /// A stone has been drawn from the reserve; the player may now hand one back.
    | AwaitingReturn of drawn: StoneColor

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
      /// Set while the active player still owes a decision mid-turn.
      Pending: Pending option
      /// Turns spent negotiating, or skipped for want of stones, without a stone
      /// being played in between. Once every player has done so in a row, the game
      /// is over.
      Negotiations: int
      Turn: int
      /// Newest entry first.
      Log: string list
      Status: GameStatus }

/// Everything the game can be asked to do. The first four are the actions a player
/// may take on their turn.
type Msg =
    /// Place a stone from the bag into any region but the dead one.
    | Recruit of color: StoneColor * into: RegionId
    /// Place a stone in the Axe, then drive stones of other colours out of a region
    /// and back to the reserve, one for each stone there matching the placed colour.
    | Battle of color: StoneColor * target: RegionId * driven: StoneColor list
    /// Place a stone in the Flag, then move matching stones from one region into a
    /// region bordering it.
    | March of color: StoneColor * from: RegionId * into: RegionId * count: int
    /// Draw a stone from the reserve at random. The turn is not over until the draw
    /// is settled with `Settle`.
    | Negotiate
    /// Finish a negotiation: hand a stone back to the reserve, or keep the draw.
    | Settle of handBack: StoneColor option
    /// Abandon this game and deal a fresh one. Anything left unspecified is carried
    /// over from the game in progress.
    | Restart of players: int option * seed: uint64 option
    | Quit

module Model =

    let activePlayer model =
        model.Players |> List.find (fun player -> player.Id = model.Active)

    let tryRegion regionId model = model.Regions |> Map.tryFind regionId

    let withPlayer (player: Player) model =
        { model with
            Players = model.Players |> List.map (fun other -> if other.Id = player.Id then player else other) }

    let withRegion (region: Region) model =
        { model with Regions = model.Regions |> Map.add region.Id region }

    let returnToReserve stones model =
        { model with Reserve = Pile.merge stones model.Reserve }

    /// Regions in board order (Map keys sort by their underlying id).
    let regions model = model.Regions |> Map.toList |> List.map snd

    let regionsOfKind predicate model =
        regions model |> List.filter (fun region -> predicate region.Kind)

    let stonesIn regionId model =
        tryRegion regionId model
        |> Option.map (fun region -> region.Stones)
        |> Option.defaultValue Pile.empty

    /// Who rules the region, with the Axe and the Flag standing by to break ties.
    let ruleOver (region: Region) model =
        Ruling.decide (stonesIn Board.axe model) (stonesIn Board.flag model) region.Stones

    /// The cascade behind that verdict, written out.
    let explainRule (region: Region) model =
        Ruling.explain (stonesIn Board.axe model) (stonesIn Board.flag model) region.Stones

    /// Every region paired with who rules it, in board order.
    let rulings model =
        regions model |> List.map (fun region -> region, ruleOver region model)

    /// Ruling over ground only. The Flag and the Axe hold stones and can be ruled
    /// like anywhere else, but they are manoeuvres rather than land, so they are no
    /// part of how much of the map a faction holds.
    let landRulings model =
        rulings model |> List.filter (fun (region, _) -> not (Region.isIsolated region))

    /// How much land each colour rules, in canonical colour order.
    let standings model =
        let ruled =
            landRulings model
            |> List.choose (fun (_, rule) ->
                match rule with
                | Ruling.RuledBy color -> Some color
                | Ruling.Contested _
                | Ruling.Unclaimed -> None)

        StoneColor.all
        |> List.map (fun color -> color, ruled |> List.filter ((=) color) |> List.length)

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
