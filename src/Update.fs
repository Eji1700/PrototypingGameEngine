/// The U of MVU: a pure transition from a message and a model to the next model.
module TCModel.Update

/// Longest run of log entries kept in the model.
[<Literal>]
let private LogDepth = 12

let private log message model =
    { model with Log = message :: model.Log |> List.truncate LogDepth }

/// Record a message that came from outside the game proper, such as unparseable input.
let note message model = log message model

let private endTurn model =
    let model =
        { model with
            Active = Model.nextPlayer model.Active model
            Turn = model.Turn + 1 }

    if Model.allBagsEmpty model then
        { model with Status = Over "every bag is empty" } |> log "Every bag is empty - the game is over."
    else
        model

let private place color regionId model =
    let player = Model.activePlayer model

    match Model.tryRegion regionId model with
    | None ->
        let (RegionId n) = regionId
        log $"There is no region {n}." model
    | Some region when not (Region.isOpen region) -> log $"{region.Name} is dead ground - no stone may enter." model
    | Some region ->
        match Pile.tryTake color 1 player.Bag with
        | None -> log $"{Player.name player} has no {StoneColor.name color} stone left in the bag." model
        | Some bag ->
            let player = { player with Bag = bag }
            let region = Region.addStone color region

            { model with
                Players = model.Players |> List.map (fun p -> if p.Id = player.Id then player else p)
                Regions = model.Regions |> Map.add region.Id region }
            |> log $"{Player.name player} places a {StoneColor.name color} stone in {region.Name}."
            |> endTurn

/// Deal a fresh game, falling back to the current table size and drawing an unnamed
/// seed from the generator in play so that even a restart stays reproducible.
let private restart players seed model =
    let players = players |> Option.defaultValue (Model.playerCount model)

    let seed =
        match seed with
        | Some seed -> seed
        | None -> Rng.next model.Rng |> fst

    Setup.init players seed

let update msg model =
    match model.Status, msg with
    | _, Restart(players, seed) -> restart players seed model
    | Over _, _ -> model
    | InProgress, Place(color, regionId) -> place color regionId model
    | InProgress, Pass ->
        let player = Model.activePlayer model
        model |> log $"{Player.name player} passes." |> endTurn
    | InProgress, Quit -> { model with Status = Over "the players walked away" }
