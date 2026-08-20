namespace TCModel.Turncoats

open TCModel.Common

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


    let ruleOver regionId game = Ruling.over regionId game.Position

    let weighRule regionId game = Ruling.weighing regionId game.Position

    let landRulings game = Ruling.landRulings game.Position

    let standings game = Ruling.standings game.Position

    let landStanding game = Ruling.landStanding game.Position

    let allBagsEmpty game = Table.allEmptyHanded game.Table

    let allStones game =
        players game
        |> List.fold (fun pile player -> Pile.merge player.Bag pile) (Position.total game.Position)
        |> Pile.merge game.Reserve
