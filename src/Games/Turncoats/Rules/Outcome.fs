namespace TCModel.Turncoats

open TCModel.Engine

open TCModel.Common

type FactionMeasure =
    | LandRuled
    | AxeHeld
    | FlagHeld

type PlayerMeasure =
    | WinningStonesHeld
    | LosingStonesHeld
    | ClosestToActing

type DrawReason =
    | NoFactionSeparated of StoneColor list
    | EveryBagPlayedOut of StoneColor
    | NoPlayerSeparated of StoneColor

type Verdict =
    | Won of faction: StoneColor * player: PlayerId
    | Drawn of DrawReason

module Outcome =

    let private factionMeasures game =
        let ruled = Game.standings game
        let axe = Game.axeStones game
        let flag = Game.flagStones game

        [ Cascade.by LandRuled (fun color -> ruled[color])
          Cascade.by AxeHeld (fun color -> Pile.count color axe)
          Cascade.by FlagHeld (fun color -> Pile.count color flag) ]

    let weighFactions game =
        Cascade.run (factionMeasures game) StoneColor.all

    let private playerMeasures winning game =
        let ofWinning (player: Player) = Pile.count winning player.Bag

        let ofLosing (player: Player) =
            StoneColor.all
            |> List.filter (fun color -> color <> winning)
            |> List.sumBy (fun color -> Pile.count color player.Bag)

        // The last tie-break: whoever would have acted soonest. Counted from the seat after the one
        // that just played, so it settles every time rather than leaving two players level.
        let waiting =
            Table.fromNext game.Table
            |> List.mapi (fun place player -> player.Id, place)
            |> Map.ofList

        [ Cascade.by WinningStonesHeld ofWinning
          Cascade.byFewest LosingStonesHeld ofLosing
          Cascade.byFewest ClosestToActing (fun (player: Player) -> waiting[player.Id]) ]

    let weighPlayers winning game =
        Cascade.run (playerMeasures winning game) (Game.players game)

    let verdict game =
        match weighFactions game |> fst with
        | [ winning ] when Game.allBagsEmpty game -> Drawn(EveryBagPlayedOut winning)
        | [ winning ] ->
            match weighPlayers winning game |> fst with
            | [ player ] -> Won(winning, player.Id)
            | _ -> Drawn(NoPlayerSeparated winning)
        | tied -> Drawn(NoFactionSeparated tied)
