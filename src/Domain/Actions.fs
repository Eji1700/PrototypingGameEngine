namespace TCModel.Domain

open TCModel.Common

/// Which stones a battle drives out of the region it targets.
type Casualties =
    /// As many as the rule allows. Only settled without asking where there is no
    /// choice to be made; a real choice between colours goes back to the attacker.
    | AsManyAsAllowed
    /// Exactly these. A battle must drive out at least one stone, so an empty list
    /// is refused rather than taken as declining the fight.
    | These of StoneColor list

/// The four things a player may do on their turn. Each is a pure transition:
/// a game and a choice in, a new game and what happened out - or why not.
module Actions =

    // -- vetting the pieces an action needs ---------------------------------

    /// A region a stone may enter: anything but the dead one.
    let private openRegion regionId =
        require (RegionKind.isOpen (Board.region regionId).Kind) (DeadGround regionId)

    /// A region an action may be aimed at: not dead, and not one of the two that
    /// stand apart from the map.
    let private contestedRegion regionId =
        result {
            do! openRegion regionId
            do! require (not (RegionKind.isIsolated (Board.region regionId).Kind)) (StandsApart regionId)
        }

    /// Take a stone out of the active player's bag.
    let private takeFromBag color game =
        let player = Game.active game

        match Pile.tryTake color 1 player.Bag with
        | None -> Error(NotInBag(player.Id, color))
        | Some bag -> Ok(Game.withActive { player with Bag = bag } game)

    /// Move a stone from the bag onto the map.
    let private placeFromBag color regionId game =
        takeFromBag color game
        |> Result.map (fun game ->
            { game with Position = Position.add color 1 regionId game.Position })

    /// Lift the named stones out of a region, objecting if any is not standing there.
    let private takeStones colors regionId game =
        colors
        |> List.fold
            (fun outcome color ->
                outcome
                |> Result.bind (fun pile ->
                    match Pile.tryTake color 1 pile with
                    | Some pile -> Ok pile
                    | None -> Error(NotStandingThere(regionId, color))))
            (Ok(Game.stones regionId game))

    // -- recruit ------------------------------------------------------------

    /// Place any stone from the bag on the map, anywhere but the dead region.
    let recruit color into game =
        result {
            let player = Game.active game
            do! openRegion into
            let! game = placeFromBag color into game
            return game, Recruited(player.Id, color, into)
        }

    // -- battle -------------------------------------------------------------

    /// The stones in a region that a battle of this colour could drive out.
    let private losingStones color regionId game =
        Game.stones regionId game
        |> Pile.toCounts
        |> List.filter (fun (other, _) -> other <> color)

    /// Work out what an unnamed battle drives out. Taking everything on offer is
    /// only assumed where it is the one thing the attacker could have meant. By the
    /// time this runs both counts are known to be at least one, so it never comes
    /// back empty.
    let private resolveCasualties color regionId allowed game =
        let losing = losingStones color regionId game
        let available = losing |> List.sumBy snd

        match losing with
        | _ when available <= allowed -> Ok(losing |> List.collect (fun (other, n) -> List.replicate n other))
        | [ (only, _) ] -> Ok(List.replicate allowed only)
        | _ -> Error(MustChooseCasualties(regionId, Pile.ofCounts losing, allowed))

    /// Place a stone in the Axe and name a region. For each stone there matching the
    /// placed colour, one stone of another colour is driven back to the reserve.
    let battle color target casualties game =
        result {
            let player = Game.active game
            do! contestedRegion target
            let matching = Pile.count color (Game.stones target game)
            let available = losingStones color target game |> List.sumBy snd

            // A battle has to be a real fight: something of yours to fight with,
            // and something of theirs to drive out.
            do! require (matching >= 1) (NothingToBattleWith(target, color))
            do! require (available >= 1) (NothingToDriveOut(target, color))

            let! driven =
                match casualties with
                | These named -> Ok named
                | AsManyAsAllowed -> resolveCasualties color target matching game

            do! require (not (List.isEmpty driven)) BattleMustDriveOutSomething
            do! require (driven |> List.forall (fun other -> other <> color)) (CannotDriveOutOwnColour color)
            do! require (List.length driven <= matching) (MoreDrivenThanAllowed(target, color, matching))

            let! held = takeStones driven target game
            let! game = placeFromBag color Board.axe game
            let spoils = Pile.ofColors driven

            let game =
                { game with
                    Position = Position.withStones target held game.Position
                    Reserve = Pile.merge spoils game.Reserve }

            return game, Battled(player.Id, color, target, spoils)
        }

    // -- march --------------------------------------------------------------

    /// Place a stone in the Flag and name a region. Stones there of the matching
    /// colour then march into a single region bordering it.
    let march color from into count game =
        result {
            let player = Game.active game
            do! contestedRegion from
            do! openRegion into
            do! require (count >= 1) MarchNeedsAStone

            let available = Pile.count color (Game.stones from game)
            do! require (available >= 1) (NothingToMarch(from, color))
            do! require (available >= count) (NotEnoughToMarch(from, color, available, count))
            do! require (Board.areAdjacent from into) (NotAdjacent(from, into))

            let! game = placeFromBag color Board.flag game

            let game =
                { game with
                    Position = game.Position |> Position.remove color count from |> Position.add color count into }

            return game, Marched(player.Id, color, from, into, count)
        }

    // -- negotiate ----------------------------------------------------------

    /// Draw a stone from the reserve at random. The turn is not over: a stone must
    /// then be handed back, which `settle` does.
    let negotiate game =
        result {
            let player = Game.active game
            do! require (not (Player.isEmptyHanded player)) (EmptyHandedCannotNegotiate player.Id)
            let drawn, rng = Pile.drawOne game.Reserve game.Rng

            match drawn with
            | None -> return! Error ReserveEmpty
            | Some(color, reserve) ->
                let game =
                    { game with Rng = rng; Reserve = reserve }
                    |> Game.withActive { player with Bag = Pile.add color 1 player.Bag }

                return game, color, Drew(player.Id, color)
        }

    /// Finish a negotiation by handing a stone back. One always goes back, so the
    /// bag ends the turn the size it began it.
    let settle color game =
        let player = Game.active game

        match Pile.tryTake color 1 player.Bag with
        | None -> Error(NotInBag(player.Id, color))
        | Some bag ->
            let game =
                { game with Reserve = Pile.add color 1 game.Reserve }
                |> Game.withActive { player with Bag = bag }

            Ok(game, HandedBack(player.Id, color))
