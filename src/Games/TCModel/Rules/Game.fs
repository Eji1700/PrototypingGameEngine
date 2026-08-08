namespace TCModel.Domain

open TCModel.Common

/// A game in progress: where the stones are, who is holding what, what is left to
/// draw, and the generator that will decide the next draw.
type Game =
    { Position: Position
      Table: Table
      Reserve: Pile
      Rng: Rng }

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

    // Who rules what is settled by `Ruling`, off the position alone. These are here
    // because a game is what most of the program is holding when it wants to ask.

    /// Who rules a region, with the Axe and the Flag standing by to break ties.
    let ruleOver regionId game = Ruling.over regionId game.Position

    /// The cascade behind that verdict.
    let weighRule regionId game = Ruling.weighing regionId game.Position

    /// Ruling over ground only. The Flag and the Axe hold stones and can be ruled
    /// like anywhere else, but they are manoeuvres rather than land.
    let landRulings game = Ruling.landRulings game.Position

    /// How much land each colour rules, in canonical colour order.
    let standings game = Ruling.standings game.Position

    /// The whole standing of the land at once.
    let landStanding game = Ruling.landStanding game.Position

    let allBagsEmpty game = Table.allEmptyHanded game.Table

    /// Every stone in the game, wherever it stands. Should always come to 63.
    let allStones game =
        players game
        |> List.fold (fun pile player -> Pile.merge player.Bag pile) (Position.total game.Position)
        |> Pile.merge game.Reserve
