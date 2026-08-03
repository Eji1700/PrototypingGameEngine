namespace TCModel.Domain

open TCModel.Common

/// Dealing a fresh game: 63 stones spread over the board, the players' bags and the
/// reserve.
module Setup =

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

    let private fullSupply =
        StoneColor.all |> List.map (fun color -> color, StonesPerColor) |> Pile.ofCounts

    /// Seed each home from the supply. Wild regions are dealt after, once every home
    /// has taken its own colour; the special regions start empty by rule, and the
    /// dead region because nothing may enter it.
    let private seedHomes supply =
        Board.regions
        |> List.fold
            (fun (position, supply) region ->
                match region.Kind with
                | Home color ->
                    position |> Position.add color HomeSeedStones region.Id,
                    Pile.remove color HomeSeedStones supply
                | Wild
                | Special
                | Dead -> position, supply)
            (Position.empty, supply)

    /// Deal each wild region two stones from what the homes left behind.
    let private dealWilds (position, reserve) rng =
        Board.regions
        |> List.filter (fun region -> region.Kind = Wild)
        |> List.fold
            (fun ((position, reserve), rng) region ->
                let (stones, reserve), rng = Pile.draw WildSeedStones reserve rng
                (Position.withStones region.Id stones position, reserve), rng)
            ((position, reserve), rng)

    /// Draw a bag for each player, in seating order.
    let private dealBags playerCount reserve rng =
        let (bags, reserve), rng =
            [ 1..playerCount ]
            |> List.fold
                (fun ((bags, reserve), rng) _ ->
                    let (bag, reserve), rng = Pile.draw BagSize reserve rng
                    (bag :: bags, reserve), rng)
                (([], reserve), rng)

        (List.rev bags, reserve), rng

    /// Deal a complete game for `playerCount` players from a seed. The player count
    /// is checked by the table, so a game that exists has a legal number of players.
    let deal playerCount (seed: uint64) =
        let position, supply = seedHomes fullSupply
        let (position, reserve), rng = dealWilds (position, supply) (Rng.ofSeed seed)
        let (bags, reserve), rng = dealBags playerCount reserve rng

        Table.trySeat bags
        |> Result.map (fun table ->
            { Position = position
              Table = table
              Reserve = reserve
              Rng = rng })
