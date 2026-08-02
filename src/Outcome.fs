/// Who won. Two cascades run when the game ends: first the faction that carried the
/// board, then the player who served that faction best. Both work the same way as
/// ruling a region - each measure only narrows the field the one before it left.
module TCModel.Outcome

type Result =
    | Won of faction: StoneColor * player: PlayerId
    | Drawn of why: string

// ---------------------------------------------------------------------------
// The winning faction
// ---------------------------------------------------------------------------

let private factionMeasures model =
    let ruled = Model.standings model |> Map.ofList
    let axe = Model.stonesIn Board.axe model
    let flag = Model.stonesIn Board.flag model

    let by label count =
        Cascade.measure label count (fun color -> $"{StoneColor.name color} {count color}")

    [ by "land ruled" (fun color -> ruled[color])
      by "stones in the Axe" (fun color -> Pile.count color axe)
      by "stones in the Flag" (fun color -> Pile.count color flag) ]

/// Every faction contends, including one ruling nothing: if no faction rules a
/// region at all they are level on the first measure and the Axe settles it.
let faction model =
    Cascade.run (factionMeasures model) StoneColor.all

// ---------------------------------------------------------------------------
// The winning player
// ---------------------------------------------------------------------------

let private playerMeasures winning model =
    let ofWinning (player: Player) = Pile.count winning player.Bag

    let ofLosing (player: Player) =
        StoneColor.all
        |> List.filter (fun color -> color <> winning)
        |> List.sumBy (fun color -> Pile.count color player.Bag)

    // How far round the table a player sits from whoever would act next.
    let seats = model.Players |> List.map (fun player -> player.Id)
    let next = Model.nextPlayer model.Active model
    let seat playerId = seats |> List.findIndex ((=) playerId)

    let waiting (player: Player) =
        (seat player.Id - seat next + List.length seats) % List.length seats

    [ Cascade.measure
          $"{StoneColor.name winning} stones held"
          ofWinning
          (fun player -> $"{Player.name player} {ofWinning player}")
      // Negated: the fewest stones of the losing factions wins.
      Cascade.measure
          "stones of the losing factions, fewest winning"
          (fun player -> -(ofLosing player))
          (fun player -> $"{Player.name player} {ofLosing player}")
      Cascade.measure
          "closest to taking the next turn"
          (fun player -> -(waiting player))
          (fun player -> $"{Player.name player} {waiting player} seat(s) away") ]

let private playedOut model =
    model.Players |> List.forall (fun player -> Pile.isEmpty player.Bag)

// ---------------------------------------------------------------------------

let result model =
    match faction model |> fst with
    | [ winning ] when playedOut model ->
        Drawn $"{StoneColor.name winning} carried the board, but every player has played out their bag"
    | [ winning ] ->
        match Cascade.run (playerMeasures winning model) model.Players |> fst with
        | [ player ] -> Won(winning, player.Id)
        | _ -> Drawn "no player could be separated"
    | tied ->
        let names = tied |> List.map StoneColor.name |> String.concat ", "
        Drawn $"{names} could not be separated, so no faction carried the board"

/// Both cascades written out, for showing the working behind the result.
let explain model =
    let factions, factionTrace = faction model
    let heading = [ "THE WINNING FACTION" ]
    let factionLines = Cascade.workings StoneColor.name factionTrace

    let verdict =
        match factions with
        | [ color ] -> [ $"  {StoneColor.name color} carries the board." ]
        | tied ->
            let names = tied |> List.map StoneColor.name |> String.concat ", "
            [ $"  {names} are level after every tie-breaker, so the game is a draw." ]

    let players =
        match factions with
        | [ winning ] when not (playedOut model) ->
            let _, trace = Cascade.run (playerMeasures winning model) model.Players

            [ ""; "THE WINNING PLAYER" ]
            @ Cascade.workings Player.name trace
            @ [ match result model with
                | Won(_, playerId) ->
                    let winner = model.Players |> List.find (fun player -> player.Id = playerId)
                    $"  {Player.name winner} wins."
                | Drawn why -> $"  {why}." ]
        | [ _ ] -> [ ""; "THE WINNING PLAYER"; "  Every player has played out their bag, so nobody wins." ]
        | _ -> []

    heading @ factionLines @ verdict @ players
