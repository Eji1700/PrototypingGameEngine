/// The U of MVU: a pure transition from a message and a model to the next model.
module TCModel.Update

/// Longest run of log entries kept in the model.
[<Literal>]
let private LogDepth = 12

let private log message model =
    { model with Log = message :: model.Log |> List.truncate LogDepth }

/// Record a message that came from outside the game proper, such as unparseable input.
let note message model = log message model

let private number (RegionId n) = n

// ---------------------------------------------------------------------------
// Vetting the pieces an action needs
// ---------------------------------------------------------------------------

let private findRegion regionId model =
    match Model.tryRegion regionId model with
    | Some region -> Ok region
    | None -> Error $"There is no region {number regionId}."

/// A region a stone may enter: anything but the dead one.
let private openRegion regionId model =
    result {
        let! region = findRegion regionId model
        do! require (Region.isOpen region) $"{region.Name} is dead ground - no stone may enter."
        return region
    }

/// A region an action may be aimed at: not dead, and not one of the two that stand alone.
let private contestedRegion regionId model =
    result {
        let! region = openRegion regionId model

        do! require
                (not (Region.isIsolated region))
                $"{region.Name} stands apart from the map and cannot be chosen."

        return region
    }

let private takeFromBag color model =
    let player = Model.activePlayer model

    match Pile.tryTake color 1 player.Bag with
    | None -> Error $"{Player.name player} has no {StoneColor.name color} stone in the bag."
    | Some bag -> Ok(Model.withPlayer { player with Bag = bag } model)

/// Move a stone out of the bag and into a region that has already been vetted.
let private placeFromBag color region model =
    takeFromBag color model |> Result.map (Model.withRegion (Region.addStone color region))

/// Lift the named stones out of a pile, objecting if any of them is not there.
let private takeStones colors regionName pile =
    colors
    |> List.fold
        (fun outcome color ->
            outcome
            |> Result.bind (fun pile ->
                match Pile.tryTake color 1 pile with
                | Some pile -> Ok pile
                | None -> Error $"{regionName} has no {StoneColor.name color} stone to drive out."))
        (Ok pile)

// ---------------------------------------------------------------------------
// Turn order
// ---------------------------------------------------------------------------

/// The game ends once every player in turn has negotiated rather than played a
/// stone, a skipped turn counting as a negotiation.
let private overIfAllNegotiated model =
    if model.Negotiations < Model.playerCount model then
        model
    else
        let reason =
            if model.Players |> List.forall (fun player -> Pile.isEmpty player.Bag) then
                "every player has played out their bag"
            else
                "every player negotiated in turn"

        { model with Status = Over reason } |> log $"The game is over: {reason}."

/// Close the turn and hand on. `negotiated` says whether this turn was spent
/// negotiating; playing a stone instead breaks the run and resets the count.
/// A player holding nothing has their turn skipped, which counts as a negotiation.
let rec private endTurn negotiated model =
    let model =
        { model with
            Pending = None
            Negotiations = if negotiated then model.Negotiations + 1 else 0 }

    match overIfAllNegotiated model with
    | { Status = Over _ } as over -> over
    | model ->
        let model =
            { model with
                Active = Model.nextPlayer model.Active model
                Turn = model.Turn + 1 }

        let player = Model.activePlayer model

        if Pile.isEmpty player.Bag then
            model
            |> log $"{Player.name player} has no stones left, so the turn is skipped and counts as a negotiation."
            |> endTurn true
        else
            model

// ---------------------------------------------------------------------------
// The four actions
// ---------------------------------------------------------------------------

/// Place any stone from the bag on the map, anywhere but the dead region.
let private recruit color into model =
    result {
        let player = Model.activePlayer model
        let! region = openRegion into model
        let! model = placeFromBag color region model

        return
            model
            |> log $"{Player.name player} recruits a {StoneColor.name color} stone into {region.Name}."
            |> endTurn false
    }

/// The stones in a region that a battle of this colour could drive out.
let private losingStones color (region: Region) =
    Pile.toCounts region.Stones |> List.filter (fun (other, _) -> other <> color)

/// Work out what an unnamed battle drives out. Taking everything on offer is only
/// assumed where it is the one thing the attacker could have meant; a real choice
/// between colours has to be made by the player. Both counts are known to be at
/// least one by the time this runs, so it never comes back empty.
let private resolveCasualties color region allowed =
    let losing = losingStones color region
    let available = losing |> List.sumBy snd

    match losing with
    | _ when available <= allowed -> Ok(losing |> List.collect (fun (other, n) -> List.replicate n other))
    | [ (only, _) ] -> Ok(List.replicate allowed only)
    | _ ->
        let holding = losing |> List.map (fun (other, n) -> $"{n} {StoneColor.name other}") |> String.concat " and "

        Error $"{region.Name} holds {holding}, and {allowed} of them may be driven out - name which."

/// Place a stone in the Axe and name a region. For each stone there matching the
/// placed colour, one stone of another colour may be driven back to the reserve.
let private battle color target driven model =
    result {
        let player = Model.activePlayer model
        let! region = contestedRegion target model
        let! axe = findRegion Board.axe model
        let matching = Pile.count color region.Stones
        let available = losingStones color region |> List.sumBy snd

        // A battle has to be a real fight: something of yours to fight with, and
        // something of theirs to drive out.
        do! require
                (matching >= 1)
                $"{region.Name} holds no {StoneColor.name color} stone, so there is nothing there to battle with."

        do! require
                (available >= 1)
                $"{region.Name} holds nothing but {StoneColor.name color} stones, so there is nothing to drive out."

        let! driven =
            match driven with
            | These named -> Ok named
            | AsManyAsAllowed -> resolveCasualties color region matching

        do! require (not (List.isEmpty driven)) "A battle must drive out at least one stone."

        do! require
                (driven |> List.forall (fun other -> other <> color))
                $"The Axe drives out stones of other colours, not {StoneColor.name color} ones."

        do! require
                (List.length driven <= matching)
                $"{region.Name} holds {matching} {StoneColor.name color} stone(s), so no more than that many may be driven out."

        let! held = takeStones driven region.Name region.Stones
        let! model = placeFromBag color axe model
        let spoils = Pile.ofColors driven

        let telling =
            $"{Player.name player} battles {region.Name} with a {StoneColor.name color} stone, driving {Pile.describe spoils} back to the reserve."

        return
            model
            |> Model.withRegion { region with Stones = held }
            |> Model.returnToReserve spoils
            |> log telling
            |> endTurn false
    }

/// Place a stone in the Flag and name a region. Stones there of the matching colour
/// may then march into a region bordering it.
let private march color from into count model =
    result {
        let player = Model.activePlayer model
        let! source = contestedRegion from model
        let! destination = openRegion into model
        let! flag = findRegion Board.flag model
        do! require (count >= 1) "A march moves at least one stone."

        let available = Pile.count color source.Stones

        do! require
                (available >= 1)
                $"{source.Name} holds no {StoneColor.name color} stone, so there is nothing there to march."

        do! require
                (available >= count)
                $"{source.Name} holds {available} {StoneColor.name color} stone(s), which is not enough to march {count}."

        do! require
                (Model.areAdjacent source.Id destination.Id model)
                $"{source.Name} does not border {destination.Name}."

        let! model = placeFromBag color flag model

        return
            model
            |> Model.withRegion { source with Stones = Pile.remove color count source.Stones }
            |> Model.withRegion { destination with Stones = Pile.add color count destination.Stones }
            |> log
                $"{Player.name player} marches {count} {StoneColor.name color} stone(s) from {source.Name} into {destination.Name}."
            |> endTurn false
    }

/// Draw a stone from the reserve at random. The player then owes a decision about
/// which stone, if any, to hand back, so the turn stays open.
let private negotiate model =
    result {
        let player = Model.activePlayer model

        do! require
                (not (Pile.isEmpty player.Bag))
                $"{Player.name player} holds nothing, and only a player with a stone in the bag may negotiate."

        let drawn, rng = Pile.drawOne model.Reserve model.Rng

        match drawn with
        | None -> return! Error "The reserve is empty - there is nothing to negotiate for."
        | Some(color, reserve) ->
            return
                { model with
                    Rng = rng
                    Reserve = reserve
                    Pending = Some(AwaitingReturn color) }
                |> Model.withPlayer { player with Bag = Pile.add color 1 player.Bag }
                |> log
                    $"{Player.name player} draws a {StoneColor.name color} stone from the reserve, and may hand one back."
    }

/// Finish a negotiation by handing a stone back, or by keeping the draw.
let private settle handBack model =
    let player = Model.activePlayer model

    match handBack with
    | None -> Ok(model |> log $"{Player.name player} keeps the draw." |> endTurn true)
    | Some color ->
        match Pile.tryTake color 1 player.Bag with
        | None -> Error $"{Player.name player} has no {StoneColor.name color} stone to hand back."
        | Some bag ->
            Ok(
                model
                |> Model.withPlayer { player with Bag = bag }
                |> Model.returnToReserve (Pile.ofCounts [ color, 1 ])
                |> log $"{Player.name player} hands a {StoneColor.name color} stone back to the reserve."
                |> endTurn true
            )

// ---------------------------------------------------------------------------

/// Deal a fresh game, falling back to the current table size and drawing an unnamed
/// seed from the generator in play so that even a restart stays reproducible.
let private restart players seed model =
    let players = players |> Option.defaultValue (Model.playerCount model)

    let seed =
        match seed with
        | Some seed -> seed
        | None -> Rng.next model.Rng |> fst

    Setup.init players seed

/// An action that fails leaves the game untouched and says why.
let private attempt outcome model =
    match outcome with
    | Ok model -> model
    | Error objection -> log objection model

let update msg model =
    match model.Status, model.Pending, msg with
    | _, _, Restart(players, seed) -> restart players seed model
    | Over _, _, _ -> model
    | InProgress, _, Quit -> { model with Status = Over "the players walked away" }

    // A draw from the reserve must be settled before the turn can move on.
    | InProgress, Some _, Settle handBack -> model |> attempt (settle handBack model)
    | InProgress, Some(AwaitingReturn drawn), _ ->
        model
        |> log
            $"Settle the negotiation first: hand a stone back, or keep the {StoneColor.name drawn} stone just drawn."

    | InProgress, None, Recruit(color, into) -> model |> attempt (recruit color into model)
    | InProgress, None, Battle(color, target, driven) -> model |> attempt (battle color target driven model)
    | InProgress, None, March(color, from, into, count) -> model |> attempt (march color from into count model)
    | InProgress, None, Negotiate -> model |> attempt (negotiate model)
    | InProgress, None, Settle _ -> model |> log "There is no negotiation to settle."
