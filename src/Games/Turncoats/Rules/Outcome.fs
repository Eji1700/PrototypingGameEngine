namespace Prototyping.Turncoats

open Prototyping.Engine

open Prototyping.Common

type FactionMeasure =
    | LandRuled
    | AxeHeld
    | FlagHeld

type PlayerMeasure =
    | WinningStonesHeld
    | LosingStonesHeld
    | ClosestToActing

type DrawReason =
    | NoFactionSeparated of StoneColour list
    | EveryBagPlayedOut of StoneColour
    | NoPlayerSeparated of StoneColour

type Verdict =
    | Won of faction: StoneColour * player: PlayerId
    | Drawn of DrawReason

module Outcome =

    let private factionMeasures game =
        let ruled = Game.standings game
        let axe = Game.axeStones game
        let flag = Game.flagStones game

        [ Tiebreak.by LandRuled (fun colour -> ruled[colour])
          Tiebreak.by AxeHeld (fun colour -> Pile.count colour axe)
          Tiebreak.by FlagHeld (fun colour -> Pile.count colour flag) ]

    let weighFactions game =
        Tiebreak.run (factionMeasures game) StoneColour.all

    let private playerMeasures winning game =
        let ofWinning (player: Player) = Pile.count winning player.Bag

        let ofLosing (player: Player) =
            StoneColour.all
            |> List.filter (fun colour -> colour <> winning)
            |> List.sumBy (fun colour -> Pile.count colour player.Bag)

        // The last tie-break: whoever would have acted soonest. Counted from the seat after the one
        // that just played, so it settles every time rather than leaving two players level.
        let waiting =
            Table.fromNext game.Table
            |> List.mapi (fun place player -> player.Id, place)
            |> Map.ofList

        [ Tiebreak.by WinningStonesHeld ofWinning
          Tiebreak.byFewest LosingStonesHeld ofLosing
          Tiebreak.byFewest ClosestToActing (fun (player: Player) -> waiting[player.Id]) ]

    let weighPlayers winning game =
        Tiebreak.run (playerMeasures winning game) (Game.players game)

    let verdict game =
        match weighFactions game |> fst with
        | [ winning ] when Game.allBagsEmpty game -> Drawn(EveryBagPlayedOut winning)
        | [ winning ] ->
            match weighPlayers winning game |> fst with
            | [ player ] -> Won(winning, player.Id)
            | _ -> Drawn(NoPlayerSeparated winning)
        | tied -> Drawn(NoFactionSeparated tied)
