namespace TCModel.Turncoats

open TCModel.Common

type Casualties =
    | AsManyAsAllowed
    | These of StoneColor list

module Actions =


    let private openRegion regionId =
        require (RegionKind.isOpen (Board.region regionId).Kind) (DeadGround regionId)

    let private contestedRegion regionId =
        result {
            do! openRegion regionId
            do! require (not (RegionKind.isIsolated (Board.region regionId).Kind)) (StandsApart regionId)
        }

    let private takeFromBag color game =
        let player = Game.active game

        match Pile.tryTake color 1 player.Bag with
        | None -> Error(NotInBag(player.Id, color))
        | Some bag -> Ok(Game.withActive { player with Bag = bag } game)

    let private placeFromBag color regionId game =
        takeFromBag color game
        |> Result.map (fun game ->
            { game with
                Position = Position.add color 1 regionId game.Position })

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


    let recruit color into game =
        result {
            let player = Game.active game
            do! openRegion into
            let! game = placeFromBag color into game
            return game, Recruited(player.Id, color, into)
        }


    let private losingStones color regionId game =
        Game.stones regionId game
        |> Pile.toCounts
        |> List.filter (fun (other, _) -> other <> color)

    let private resolveCasualties color regionId allowed game =
        let losing = losingStones color regionId game
        let available = losing |> List.sumBy snd

        match losing with
        | _ when available <= allowed -> Ok(losing |> List.collect (fun (other, n) -> List.replicate n other))
        | [ (only, _) ] -> Ok(List.replicate allowed only)
        | _ -> Error(MustChooseCasualties(regionId, Pile.ofCounts losing, allowed))

    let battle color target casualties game =
        result {
            let player = Game.active game
            do! contestedRegion target
            let matching = Pile.count color (Game.stones target game)
            let available = losingStones color target game |> List.sumBy snd

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
                    Position =
                        game.Position
                        |> Position.remove color count from
                        |> Position.add color count into }

            return game, Marched(player.Id, color, from, into, count)
        }


    let negotiate game =
        result {
            let player = Game.active game
            do! require (not (Player.isEmptyHanded player)) (EmptyHandedCannotNegotiate player.Id)
            let drawn, rng = Pile.drawOne game.Reserve game.Rng

            match drawn with
            | None -> return! Error ReserveEmpty
            | Some(color, reserve) ->
                let game =
                    { game with
                        Rng = rng
                        Reserve = reserve }
                    |> Game.withActive
                        { player with
                            Bag = Pile.add color 1 player.Bag }

                return game, color, Drew(player.Id, color)
        }

    let settle color game =
        let player = Game.active game

        match Pile.tryTake color 1 player.Bag with
        | None -> Error(NotInBag(player.Id, color))
        | Some bag ->
            let game =
                { game with
                    Reserve = Pile.add color 1 game.Reserve }
                |> Game.withActive { player with Bag = bag }

            Ok(game, HandedBack(player.Id, color))
