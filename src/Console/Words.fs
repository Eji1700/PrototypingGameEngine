namespace TCModel.Console

open TCModel.Domain
open TCModel.App

/// Putting the game into English. The domain reports what happened in its own terms;
/// everything a player actually reads is written here.
module Words =

    let color =
        function
        | Red -> "Red"
        | Blue -> "Blue"
        | Black -> "Black"

    let glyph =
        function
        | Red -> 'R'
        | Blue -> 'B'
        | Black -> 'K'

    let colors colors =
        colors |> List.map color |> String.concat ", "

    let region regionId = (Board.region regionId).Name

    let number regionId = RegionId.value regionId

    let player playerId = $"Player {PlayerId.value playerId}"

    /// Reads as "2 Red and 1 Black"; empty piles read as "nothing".
    let pile stones =
        match Pile.toCounts stones with
        | [] -> "nothing"
        | counts -> counts |> List.map (fun (c, n) -> $"{n} {color c}") |> String.concat " and "

    /// The stones themselves, laid out for the board display.
    let stones pile =
        if Pile.isEmpty pile then
            "-"
        else
            pile |> Pile.toColors |> List.map (glyph >> string) |> String.concat " "

    /// Stones counted rather than laid out, as "Rx4 Bx2", for where there are too many
    /// to draw one by one.
    let counted pile =
        match Pile.toCounts pile with
        | [] -> "empty"
        | counts -> counts |> List.map (fun (c, n) -> $"{glyph c}x{n}") |> String.concat " "

    /// A compact tally, as "Rx4 Bx2 (6)".
    let tally pile = $"{counted pile} ({Pile.total pile})"

    let rule =
        function
        | RuledBy c -> color c
        | Contested tied -> "tied " + (tied |> List.map (glyph >> string) |> String.concat "")
        | Unclaimed -> "-"

    let ending =
        function
        | AllNegotiated -> "every player negotiated in turn"
        | AllPlayedOut -> "every player has played out their bag"
        | Abandoned -> "the players walked away"

    let event =
        function
        | Recruited(p, c, into) -> $"{player p} recruits a {color c} stone into {region into}."
        | Battled(p, c, target, driven) ->
            $"{player p} battles {region target} with a {color c} stone, driving {pile driven} back to the reserve."
        | Marched(p, c, from, into, count) ->
            $"{player p} marches {count} {color c} stone(s) from {region from} into {region into}."
        | Drew(p, c) -> $"{player p} draws a {color c} stone from the reserve, and must now hand one back."
        | HandedBack(p, c) -> $"{player p} hands a {color c} stone back to the reserve."
        | TurnSkipped p -> $"{player p} has no stones left, so the turn is skipped and counts as a negotiation."
        | GameEnded e -> $"The game is over: {ending e}."

    let rejection =
        function
        | NotInBag(p, c) -> $"{player p} has no {color c} stone in the bag."
        | DeadGround r -> $"{region r} is dead ground - no stone may enter."
        | StandsApart r -> $"{region r} stands apart from the map and cannot be chosen."
        | NothingToBattleWith(r, c) -> $"{region r} holds no {color c} stone, so there is nothing there to battle with."
        | NothingToDriveOut(r, c) -> $"{region r} holds nothing but {color c} stones, so there is nothing to drive out."
        | BattleMustDriveOutSomething -> "A battle must drive out at least one stone."
        | CannotDriveOutOwnColour c -> $"The Axe drives out stones of other colours, not {color c} ones."
        | MoreDrivenThanAllowed(r, c, allowed) ->
            $"{region r} holds {allowed} {color c} stone(s), so no more than that many may be driven out."
        | MustChooseCasualties(r, available, allowed) ->
            $"{region r} holds {pile available}, and {allowed} of them may be driven out - name which."
        | NotStandingThere(r, c) -> $"{region r} has no {color c} stone to drive out."
        | NothingToMarch(r, c) -> $"{region r} holds no {color c} stone, so there is nothing there to march."
        | NotEnoughToMarch(r, c, held, wanted) ->
            $"{region r} holds {held} {color c} stone(s), which is not enough to march {wanted}."
        | MarchNeedsAStone -> "A march moves at least one stone."
        | NotAdjacent(from, into) -> $"{region from} does not border {region into}."
        | ReserveEmpty -> "The reserve is empty - there is nothing to negotiate for."
        | EmptyHandedCannotNegotiate p ->
            $"{player p} holds nothing, and only a player with a stone in the bag may negotiate."
        | MustSettleFirst drawn ->
            $"Settle the negotiation first: a stone must go back to the reserve, and the {color drawn} stone just drawn may be it."
        | NothingToSettle -> "There is no negotiation to settle."

    /// A message written the way a player types it. The record is kept in the same
    /// words the prompt takes, so a game can be read back and played again without a
    /// second language standing between the two.
    let command msg =
        let short color = (glyph color |> string).ToLowerInvariant()

        match msg with
        | Make(Recruit(c, into)) -> $"recruit {short c} {number into}"
        | Make(Battle(c, target, AsManyAsAllowed)) -> $"battle {short c} {number target}"
        | Make(Battle(c, target, These [])) -> $"battle {short c} {number target} none"
        | Make(Battle(c, target, These driven)) ->
            let driven = driven |> List.map short |> String.concat " "
            $"battle {short c} {number target} {driven}"
        | Make(March(c, from, into, count)) -> $"march {short c} {number from} {number into} {count}"
        | Make Negotiate -> "negotiate"
        | Make(Settle c) -> $"return {short c}"
        | Make Resign -> "resign"
        | Undo -> "undo"
        | Redo -> "redo"
        | Restart(None, None) -> "restart"
        | Restart(None, Some seed) -> $"restart {seed}"
        | Restart(Some players, None) -> $"players {players}"
        | Restart(Some players, Some seed) -> $"players {players} {seed}"

    let notice =
        function
        | Happened e -> event e
        | Refused r -> rejection r
        | TookBack msg -> $"Taken back: {command msg}."
        | MadeAgain msg -> $"Made again: {command msg}."
        | NothingToTakeBack -> "There is nothing left to take back - this is the deal itself."
        | NothingToMakeAgain -> "There is nothing to make again."
        | GameIsOver -> "The game is over. Take a move back to see it again, or restart."
        | Misunderstood text -> text

    let rulingMeasure =
        function
        | StonesInRegion -> "stones in the region"
        | StonesInAxe -> "stones in the Axe"
        | StonesInFlag -> "stones in the Flag"

    let factionMeasure =
        function
        | LandRuled -> "land ruled"
        | AxeHeld -> "stones in the Axe"
        | FlagHeld -> "stones in the Flag"

    let playerMeasure =
        function
        | WinningStonesHeld -> "stones of the winning faction held"
        | LosingStonesHeld -> "stones of the losing factions, fewest winning"
        | ClosestToActing -> "closest to taking the next turn"
