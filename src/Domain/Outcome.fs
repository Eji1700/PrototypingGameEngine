namespace TCModel.Domain

open TCModel.Common

/// The measures that decide which faction carried the board.
type FactionMeasure =
    | LandRuled
    | AxeHeld
    | FlagHeld

/// The measures that decide which player carried the faction.
type PlayerMeasure =
    | WinningStonesHeld
    | LosingStonesHeld
    | ClosestToActing

type DrawReason =
    /// No faction could be told apart, even after both tie-breakers.
    | NoFactionSeparated of StoneColor list
    /// A faction carried the board, but nobody holds a stone to win with.
    | EveryBagPlayedOut of StoneColor
    | NoPlayerSeparated of StoneColor

type Verdict =
    | Won of faction: StoneColor * player: PlayerId
    | Drawn of DrawReason

/// Who won. Two cascades run when the game ends: the faction that carried the board,
/// then the player who served that faction best.
module Outcome =

    let private factionMeasures game =
        let ruled = Game.standings game
        let axe = Game.axeStones game
        let flag = Game.flagStones game

        [ Cascade.by LandRuled (fun color -> ruled[color])
          Cascade.by AxeHeld (fun color -> Pile.count color axe)
          Cascade.by FlagHeld (fun color -> Pile.count color flag) ]

    /// Every faction contends, including one ruling nothing: if no faction rules a
    /// region at all they are level on nought and the Axe settles it.
    let weighFactions game =
        Cascade.run (factionMeasures game) StoneColor.all

    let private playerMeasures winning game =
        let ofWinning (player: Player) = Pile.count winning player.Bag

        let ofLosing (player: Player) =
            StoneColor.all
            |> List.filter (fun color -> color <> winning)
            |> List.sumBy (fun color -> Pile.count color player.Bag)

        // A player's place in the turn order from here: nought is next to act.
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
