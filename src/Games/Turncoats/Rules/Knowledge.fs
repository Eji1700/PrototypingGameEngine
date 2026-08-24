namespace Prototyping.Turncoats

open Prototyping.Engine

type Sight =
    | Open of Pile
    | Closed of int

type Knowledge =
    { Beholder: PlayerId
      Position: Position
      Bags: (PlayerId * Sight) list
      Reserve: Sight
      Unseen: Pile }

module Knowledge =

    let private unseen bag (game: Game) =
        Game.allStones game
        |> Pile.without (Position.total game.Position)
        |> Pile.without bag

    let seenBy (beholder: Player) (game: Game) =
        { Beholder = beholder.Id
          Position = game.Position
          Bags =
            Game.players game
            |> List.map (fun player ->
                let bag =
                    if player.Id = beholder.Id then Open player.Bag else Closed(Pile.total player.Bag)

                player.Id, bag)
          Reserve = Closed(Pile.total game.Reserve)
          Unseen = unseen beholder.Bag game }

    let laidBare (beholder: Player) (game: Game) =
        { Beholder = beholder.Id
          Position = game.Position
          Bags = Game.players game |> List.map (fun player -> player.Id, Open player.Bag)
          Reserve = Open game.Reserve
          Unseen = unseen beholder.Bag game }
