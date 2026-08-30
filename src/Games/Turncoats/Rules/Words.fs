namespace Prototyping.Turncoats

open Prototyping.Common
open Prototyping.Engine

module Words =

    let colour =
        function
        | Red -> "Red"
        | Blue -> "Blue"
        | Green -> "Green"

    let glyph =
        function
        | Red -> 'R'
        | Blue -> 'B'
        | Green -> 'G'

    /// The glyph as it is typed: what a command says for a colour, and what a page's controls type.
    let short colour =
        (glyph colour |> string).ToLowerInvariant()

    /// Read out the way a person would: "Red and Blue", "Red, Blue and Green".
    let colours colours =
        match colours |> List.map colour |> List.rev with
        | [] -> ""
        | [ one ] -> one
        | last :: rest -> String.concat ", " (List.rev rest) + " and " + last

    let region regionId = (Board.region regionId).Name

    let number regionId = RegionId.value regionId

    let player playerId = $"Player {PlayerId.value playerId}"

    let unclaimed = "unclaimed"

    let tied = "tied"

    let stonesOf n c =
        Counting.a $"{colour c} stone" $"{colour c} stones" n

    let moves = Counting.several "move" "moves"

    let turns = Counting.several "turn" "turns"

    let pile stones =
        match Pile.toCounts stones with
        | [] -> "nothing"
        | counts -> counts |> List.map (fun (c, n) -> $"{n} {colour c}") |> String.concat " and "

    let stones pile =
        if Pile.isEmpty pile then
            "-"
        else
            pile |> Pile.toColours |> List.map (glyph >> string) |> String.concat " "

    let counted pile =
        match Pile.toCounts pile with
        | [] -> "empty"
        | counts -> counts |> List.map (fun (c, n) -> $"{glyph c}x{n}") |> String.concat " "

    let tally pile = $"{counted pile} ({Pile.total pile})"

    let sight =
        function
        | Open pile -> tally pile
        | Closed n -> $"closed ({n})"

    let rule =
        function
        | RuledBy c -> colour c
        | Contested level -> colours level + " level"
        | Unclaimed -> unclaimed

    let ending =
        function
        | AllNegotiated -> "every player has negotiated in turn"
        | AllPlayedOut -> "every player has played out their bag"
        | Abandoned -> "the players walked away"

    let event =
        function
        | Recruited(p, c, into) -> $"{player p} recruits a {colour c} stone into {region into}."
        | Battled(p, c, target, driven) ->
            $"{player p} battles {region target} with a {colour c} stone, driving {pile driven} back to the reserve."
        | Marched(p, c, from, into, count) -> $"{player p} marches {stonesOf count c} from {region from} into {region into}."
        | Drew(p, c) -> $"{player p} draws a {colour c} stone from the reserve, and must now hand one back."
        | HandedBack(p, c) -> $"{player p} hands a {colour c} stone back to the reserve."
        | TurnSkipped p -> $"{player p} has no stones left, so the turn is skipped and counts as a negotiation."
        | GameEnded e -> $"The game is over: {ending e}."

    let rejection =
        function
        | NotInBag(p, c) -> $"{player p} has no {colour c} stone in the bag."
        | DeadGround r -> $"{region r} is dead ground - no stone may enter."
        | StandsApart r -> $"{region r} stands apart from the map and cannot be chosen."
        | NothingToBattleWith(r, c) -> $"A {colour c} battle needs a {colour c} stone already in {region r}, and there is none."
        | NothingToDriveOut(r, c) -> $"{region r} holds nothing but {colour c} stones, so there is nothing to drive out."
        | BattleMustDriveOutSomething -> "A battle must drive out at least one stone."
        | CannotDriveOutOwnColour c -> $"A battle drives out the other colours, so {colour c} cannot be named."
        | MoreDrivenThanAllowed(r, c, allowed) ->
            $"{region r} holds only {stonesOf allowed c}, so no more than that may be driven out."
        | MustChooseCasualties(r, available, allowed) ->
            $"{region r} holds {pile available}, and {allowed} of them may be driven out - name which."
        | NotStandingThere(r, c) -> $"{region r} has no {colour c} stone to drive out."
        | NothingToMarch(r, c) -> $"{region r} holds no {colour c} stone, so there is nothing there to march."
        | NotEnoughToMarch(r, c, held, wanted) -> $"{region r} holds {stonesOf held c}, which is not enough to march {wanted}."
        | MarchNeedsAStone -> "A march moves at least one stone."
        | NotAdjacent(from, into) -> $"{region from} does not border {region into}."
        | ReserveEmpty -> "The reserve is empty - there is nothing to negotiate for."
        | EmptyHandedCannotNegotiate p -> $"{player p}'s bag is empty, and only a player with a stone to trade may negotiate."
        | MustSettleFirst drawn ->
            $"Settle the negotiation first: a stone must go back to the reserve, and the {colour drawn} stone just drawn may be it."
        | NothingToSettle -> "There is no negotiation to settle."

    let command =
        Msg.written (function
            | Recruit(c, into) -> $"recruit {short c} {number into}"
            | Battle(c, target, AsManyAsAllowed) -> $"battle {short c} {number target}"
            | Battle(c, target, These []) -> $"battle {short c} {number target} none"
            | Battle(c, target, These driven) ->
                let driven = driven |> List.map short |> String.concat " "
                $"battle {short c} {number target} {driven}"
            | March(c, from, into, count) -> $"march {short c} {number from} {number into} {count}"
            | Negotiate -> "negotiate"
            | Settle c -> $"return {short c}"
            | Resign -> "resign")

    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    let notice = Told.inWords said command


    let private eventSeenBy beholder happening =
        match happening with
        | Drew(player', _) when player' <> beholder ->
            $"{player player'} draws a stone from the reserve, and must now hand one back."
        | _ -> event happening

    let private rejectionSeenBy refusal =
        match refusal with
        | MustSettleFirst _ ->
            "Settle the negotiation first: a stone must go back to the reserve, and the one just drawn may be it."
        | _ -> rejection refusal

    let saidTo beholder =
        function
        | Happened happening -> eventSeenBy beholder happening
        | Refused refusal -> rejectionSeenBy refusal

    let noticeSeenBy beholder = Told.inWords (saidTo beholder) command

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
