namespace TCModel.Domain

open TCModel.Common

/// A game in progress: where the stones are, who is holding what, what is left to
/// draw, and the generator that will decide the next draw.
type Game =
    { Position: Position
      Table: Table
      Reserve: Pile
      Rng: Rng }

/// How the land stands: how much of it each colour rules, and how much is still going
/// spare. Dead ground is left out of the last two - it is unclaimed and always will be,
/// so counting it says nothing about how much is still to play for.
type LandStanding =
    { Ruled: Map<StoneColor, int>
      Tied: int
      Unclaimed: int }

module Game =

    let active game = Table.active game.Table

    let players game = Table.players game.Table

    let tryPlayer playerId game = Table.tryPlayer playerId game.Table

    let playerCount game = Table.count game.Table

    let stones regionId game = Position.stones regionId game.Position

    let axeStones game = stones Board.axe game

    let flagStones game = stones Board.flag game

    let withActive player game =
        { game with
            Table = Table.withActive player game.Table }

    /// Who rules a region, with the Axe and the Flag standing by to break ties.
    let ruleOver regionId game =
        Ruling.decide (axeStones game) (flagStones game) (stones regionId game)

    /// The cascade behind that verdict.
    let weighRule regionId game =
        Ruling.weigh (axeStones game) (flagStones game) (stones regionId game)

    /// Ruling over ground only. The Flag and the Axe hold stones and can be ruled
    /// like anywhere else, but they are manoeuvres rather than land.
    let landRulings game =
        Board.landRegions |> List.map (fun region -> region, ruleOver region.Id game)

    /// How much land each colour rules, in canonical colour order.
    let standings game =
        let ruled =
            landRulings game
            |> List.choose (fun (_, rule) ->
                match rule with
                | RuledBy color -> Some color
                | Contested _
                | Unclaimed -> None)

        StoneColor.all
        |> List.map (fun color -> color, ruled |> List.filter ((=) color) |> List.length)
        |> Map.ofList

    /// The whole standing of the land at once. Worked out here rather than wherever it
    /// happens to be shown, because it is a fact about the position and not about the
    /// drawing of it - and a second view counting it again for itself could count it
    /// differently.
    let landStanding game =
        let open' =
            landRulings game
            |> List.filter (fun (region, _) -> RegionKind.isOpen region.Kind)

        let counting predicate =
            open' |> List.filter (snd >> predicate) |> List.length

        { Ruled = standings game
          Tied =
            counting (function
                | Contested _ -> true
                | _ -> false)
          Unclaimed =
            counting (function
                | Unclaimed -> true
                | _ -> false) }

    let allBagsEmpty game = Table.allEmptyHanded game.Table

    /// Every stone in the game, wherever it stands. Should always come to 63.
    let allStones game =
        players game
        |> List.fold (fun pile player -> Pile.merge player.Bag pile) (Position.total game.Position)
        |> Pile.merge game.Reserve
