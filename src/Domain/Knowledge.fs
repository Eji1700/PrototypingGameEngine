namespace TCModel.Domain

/// Stones as they look from where someone is sitting: either open, and counted out
/// colour by colour, or closed, and giving up nothing but how many there are.
type Sight =
    | Open of Pile
    | Closed of int

/// What one player can see of a game.
///
/// A bag is held closed and so is the reserve, so a player knows their own stones and
/// everything standing on the map, and of the rest only how many are where. The colours
/// are not altogether lost though: every stone is somewhere, so whatever is neither on
/// the map nor in the beholder's own bag must be out there in the reserve or in another
/// player's bag - and that much can be worked out exactly.
type Knowledge =
    { /// Whose view this is.
      Beholder: PlayerId
      /// The map, which is open to everyone.
      Position: Position
      /// Every player and their bag in seating order, the beholder's own open.
      Bags: (PlayerId * Sight) list
      Reserve: Sight
      /// Stones that must be out of sight somewhere: every stone in the game, less the
      /// ones on the map and the ones the beholder holds.
      Unseen: Pile }

module Knowledge =

    /// Everything in the game that the beholder cannot point at. Taken from what is
    /// really there rather than from the size of the deal, so it stays true however the
    /// stones were dealt out.
    // `Game` and `Knowledge` both carry a `Position`, and the later declaration wins
    // when a field is looked up, so the game is named by type wherever it is opened up.
    let private unseen bag (game: Game) =
        Game.allStones game
        |> Pile.without (Position.total game.Position)
        |> Pile.without bag

    /// What `beholder` can see: their own bag laid out, the map, and nothing but counts
    /// for every bag and the reserve besides.
    let seenBy (beholder: Player) (game: Game) =
        { Beholder = beholder.Id
          Position = game.Position
          Bags =
            Game.players game
            |> List.map (fun player ->
                let bag =
                    if player.Id = beholder.Id then
                        Open player.Bag
                    else
                        Closed(Pile.total player.Bag)

                player.Id, bag)
          Reserve = Closed(Pile.total game.Reserve)
          Unseen = unseen beholder.Bag game }

    /// Everything, for when the game is over and there is nothing left to hold back.
    /// The view still belongs to whoever was to play, so the table reads the same way.
    let laidBare (game: Game) =
        let beholder = Game.active game

        { Beholder = beholder.Id
          Position = game.Position
          Bags = Game.players game |> List.map (fun player -> player.Id, Open player.Bag)
          Reserve = Open game.Reserve
          Unseen = unseen beholder.Bag game }
