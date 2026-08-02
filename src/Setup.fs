/// Deals a fresh game: 63 stones spread over the board, the players' bags and the reserve.
module TCModel.Setup

/// Stones available of each colour.
[<Literal>]
let StonesPerColor = 21

/// Stones a home region starts with, of its own colour.
[<Literal>]
let HomeSeedStones = 2

/// Stones a wild region starts with, drawn at random.
[<Literal>]
let WildSeedStones = 2

/// Stones each player starts with in their bag.
[<Literal>]
let BagSize = 8

[<Literal>]
let MinPlayers = 2

[<Literal>]
let MaxPlayers = 5

let private fullSupply =
    StoneColor.all |> List.map (fun color -> color, StonesPerColor) |> Pile.ofCounts

/// Lay out the regions, seeding each home from the supply. Wild regions are dealt
/// later, once every home has taken its own colour. The special regions start empty
/// by rule, and the dead region stays empty because nothing may enter it.
let private layOutBoard supply =
    Board.regions
    |> List.mapi (fun index (name, kind) -> RegionId(index + 1), name, kind)
    |> List.mapFold
        (fun supply (id, name, kind) ->
            let stones, supply =
                match kind with
                | Home color -> Pile.ofCounts [ color, HomeSeedStones ], Pile.remove color HomeSeedStones supply
                | Wild
                | Special
                | Dead -> Pile.empty, supply

            { Id = id; Name = name; Kind = kind; Stones = stones }, supply)
        supply

let private dealWildRegions reserve regions =
    (reserve, regions)
    ||> Rand.mapFold (fun reserve region ->
        match region.Kind with
        | Wild ->
            Pile.draw WildSeedStones reserve
            |> Rand.map (fun (stones, reserve) -> { region with Stones = stones }, reserve)
        | Home _
        | Special
        | Dead -> Rand.retn (region, reserve))

let private dealBags playerCount reserve =
    (reserve, [ 1..playerCount ])
    ||> Rand.mapFold (fun reserve seat ->
        Pile.draw BagSize reserve
        |> Rand.map (fun (bag, reserve) -> { Id = PlayerId seat; Bag = bag }, reserve))

/// Deal a complete game for `playerCount` players from a seed.
let init playerCount (seed: uint64) : Model =
    if playerCount < MinPlayers || playerCount > MaxPlayers then
        invalidArg (nameof playerCount) $"The game takes {MinPlayers} to {MaxPlayers} players."

    let deal =
        rand {
            let regions, supply = layOutBoard fullSupply
            let! regions, reserve = dealWildRegions supply regions
            let! players, reserve = dealBags playerCount reserve
            return regions, players, reserve
        }

    let (regions, players, reserve), rng = deal (Rng.ofSeed seed)

    { Seed = seed
      Rng = rng
      Regions = regions |> List.map (fun region -> region.Id, region) |> Map.ofList
      Adjacency = Board.adjacency
      Players = players
      Reserve = reserve
      Active = (List.head players).Id
      Pending = None
      Turn = 1
      Log = [ $"A new game for {playerCount} players is dealt from seed {seed}." ]
      Status = InProgress }
