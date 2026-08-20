namespace TCModel.Turncoats

open TCModel.Common

module Setup =

    [<Literal>]
    let StonesPerColor = 21

    [<Literal>]
    let HomeSeedStones = 2

    [<Literal>]
    let WildSeedStones = 2

    [<Literal>]
    let BagSize = 8

    let private fullSupply =
        StoneColor.all |> List.map (fun color -> color, StonesPerColor) |> Pile.ofCounts

    let private seedHomes supply =
        Board.regions
        |> List.fold
            (fun (position, supply) region ->
                match region.Kind with
                | Home color -> position |> Position.add color HomeSeedStones region.Id, Pile.remove color HomeSeedStones supply
                | Wild
                | Special
                | Dead -> position, supply)
            (Position.empty, supply)

    let private dealWilds (position, reserve) rng =
        Board.regions
        |> List.filter (fun region -> region.Kind = Wild)
        |> List.fold
            (fun ((position, reserve), rng) region ->
                let (stones, reserve), rng = Pile.draw WildSeedStones reserve rng
                (Position.withStones region.Id stones position, reserve), rng)
            ((position, reserve), rng)

    let private dealBags playerCount reserve rng =
        let (bags, reserve), rng =
            [ 1..playerCount ]
            |> List.fold
                (fun ((bags, reserve), rng) _ ->
                    let (bag, reserve), rng = Pile.draw BagSize reserve rng
                    (bag :: bags, reserve), rng)
                (([], reserve), rng)

        (List.rev bags, reserve), rng

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
