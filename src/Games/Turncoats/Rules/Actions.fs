namespace Prototyping.Turncoats

open Prototyping.Common

type Casualties =
    | AsManyAsAllowed
    | These of StoneColour list

module Actions =


    let private openRegion regionId =
        require (RegionKind.isOpen (Board.region regionId).Kind) (DeadGround regionId)

    let private contestedRegion regionId =
        result {
            do! openRegion regionId
            do! require (not (RegionKind.isIsolated (Board.region regionId).Kind)) (StandsApart regionId)
        }

    let private takeFromBag colour game =
        let player = Game.active game

        match Pile.tryTake colour 1 player.Bag with
        | None -> Error(NotInBag(player.Id, colour))
        | Some bag -> Ok(Game.withActive { player with Bag = bag } game)

    let private placeFromBag colour regionId game =
        takeFromBag colour game
        |> Result.map (fun game ->
            { game with
                Position = Position.add colour 1 regionId game.Position })

    let private takeStones colours regionId game =
        colours
        |> List.fold
            (fun outcome colour ->
                outcome
                |> Result.bind (fun pile ->
                    match Pile.tryTake colour 1 pile with
                    | Some pile -> Ok pile
                    | None -> Error(NotStandingThere(regionId, colour))))
            (Ok(Game.stones regionId game))


    let recruit colour into game =
        result {
            let player = Game.active game
            do! openRegion into
            let! game = placeFromBag colour into game
            return game, Recruited(player.Id, colour, into)
        }


    /// What a battle fought in `colour` may drive out of what stands there: every stone of another.
    let losingStones colour standing =
        Pile.toCounts standing |> List.filter (fun (other, _) -> other <> colour)

    let private resolveCasualties colour regionId allowed game =
        let losing = losingStones colour (Game.stones regionId game)
        let available = losing |> List.sumBy snd

        match losing with
        | _ when available <= allowed -> Ok(losing |> List.collect (fun (other, n) -> List.replicate n other))
        | [ (only, _) ] -> Ok(List.replicate allowed only)
        | _ -> Error(MustChooseCasualties(regionId, Pile.ofCounts losing, allowed))

    let battle colour target casualties game =
        result {
            let player = Game.active game
            do! contestedRegion target
            let matching = Pile.count colour (Game.stones target game)
            let available = losingStones colour (Game.stones target game) |> List.sumBy snd

            do! require (matching >= 1) (NothingToBattleWith(target, colour))
            do! require (available >= 1) (NothingToDriveOut(target, colour))

            let! driven =
                match casualties with
                | These named -> Ok named
                | AsManyAsAllowed -> resolveCasualties colour target matching game

            do! require (not (List.isEmpty driven)) BattleMustDriveOutSomething
            do! require (driven |> List.forall (fun other -> other <> colour)) (CannotDriveOutOwnColour colour)
            do! require (List.length driven <= matching) (MoreDrivenThanAllowed(target, colour, matching))

            let! held = takeStones driven target game
            let! game = placeFromBag colour Board.axe game
            let spoils = Pile.ofColours driven

            let game =
                { game with
                    Position = Position.withStones target held game.Position
                    Reserve = Pile.merge spoils game.Reserve }

            return game, Battled(player.Id, colour, target, spoils)
        }


    let march colour from into count game =
        result {
            let player = Game.active game
            do! contestedRegion from
            do! openRegion into
            do! require (count >= 1) MarchNeedsAStone

            let available = Pile.count colour (Game.stones from game)
            do! require (available >= 1) (NothingToMarch(from, colour))
            do! require (available >= count) (NotEnoughToMarch(from, colour, available, count))
            do! require (Board.areAdjacent from into) (NotAdjacent(from, into))

            let! game = placeFromBag colour Board.flag game

            let game =
                { game with
                    Position =
                        game.Position
                        |> Position.remove colour count from
                        |> Position.add colour count into }

            return game, Marched(player.Id, colour, from, into, count)
        }


    let negotiate game =
        result {
            let player = Game.active game
            do! require (not (Player.isEmptyHanded player)) (EmptyHandedCannotNegotiate player.Id)
            let drawn, rng = Pile.drawOne game.Reserve game.Rng

            match drawn with
            | None -> return! Error ReserveEmpty
            | Some(colour, reserve) ->
                let game =
                    { game with
                        Rng = rng
                        Reserve = reserve }
                    |> Game.withActive
                        { player with
                            Bag = Pile.add colour 1 player.Bag }

                return game, colour, Drew(player.Id, colour)
        }

    let settle colour game =
        let player = Game.active game

        match Pile.tryTake colour 1 player.Bag with
        | None -> Error(NotInBag(player.Id, colour))
        | Some bag ->
            let game =
                { game with
                    Reserve = Pile.add colour 1 game.Reserve }
                |> Game.withActive { player with Bag = bag }

            Ok(game, HandedBack(player.Id, colour))
