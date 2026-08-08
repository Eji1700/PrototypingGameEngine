namespace TCModel.Domain

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing
// namespace, and both `Spectre.Console` and the command line's argument types carry
// names this game already uses - `Region`, `Open`, `View`.
open TCModel.Domain

/// Putting the game into English. The domain reports what happened in its own terms;
/// everything a player actually reads is written here.
module Words =

    let color =
        function
        | Red -> "Red"
        | Blue -> "Blue"
        | Green -> "Green"

    let glyph =
        function
        | Red -> 'R'
        | Blue -> 'B'
        | Green -> 'G'

    let colors colors =
        colors |> List.map color |> String.concat ", "

    let region regionId = (Board.region regionId).Name

    let number regionId = RegionId.value regionId

    let player playerId = $"Player {PlayerId.value playerId}"

    /// A player as one screen names them, with the reader's own seat marked.
    ///
    /// Every view does this and the game is unreadable without it: over a network the seat
    /// to play is very often not the seat reading, so "Player 3" alone leaves somebody
    /// hunting for which one they are. Said here because it is the same mark on all of
    /// them - the board, and the table still filling up.
    let seated yours playerId =
        player playerId + (if yours then " (you)" else "")

    /// Who at a table is the machine, said to somebody as they sit down to it.
    ///
    /// Nothing on the board says so and nothing should: a machine's stones look like
    /// anybody's, and they are. But a game where you cannot tell who you are playing has a
    /// secret in it that is no part of the game, so it is said once, plainly, to whoever
    /// arrives - and nothing at all is said at a table of nothing but people.
    ///
    /// Here rather than at either table, because both tables seat machines and there is one
    /// sentence for it. A seat and the name of how it plays is the whole of what the sentence
    /// wants, so that is what it takes.
    let roster (machines: (PlayerId * string) list) =
        match machines with
        | [] -> None
        | machines ->
            machines
            |> List.map (fun (playerId, skill) -> $"{player playerId} ({skill})")
            |> String.concat ", "
            |> sprintf "Played by the machine: %s."
            |> Some

    /// Ground nobody holds, and ground nobody holds outright. Both are read as a chart's
    /// label and as the end of a sentence, so they are words rather than either.
    let unclaimed = "unclaimed"

    let tied = "tied"

    /// A count of one colour, said the way a person would: "a Red stone", "3 Red stones".
    /// Every sentence below that counts stones goes through here, because "1 stone(s)" is
    /// a placeholder somebody forgot to finish, and it is read mid-game.
    let stonesOf n c =
        if n = 1 then $"a {color c} stone" else $"{n} {color c} stones"

    /// The same for moves, for the one sentence that counts them.
    let moves n =
        if n = 1 then "1 move" else $"{n} moves"

    /// Reads as "2 Red and 1 Green"; empty piles read as "nothing".
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

    /// Stones that may be out of sight: an open pile is tallied as usual, a closed one
    /// gives up nothing but how many it holds.
    let sight =
        function
        | Open pile -> tally pile
        | Closed n -> $"closed ({n})"

    /// Who rules a region, said outright.
    ///
    /// `Render.standingIn` writes the short form - ">R", "=BG" - because a region drawn on
    /// a map has room for two characters and no more. This is the same thing for the views
    /// that give a region a box with room to say it in words, which so far is the Flag and
    /// the Axe standing on their own.
    let rule =
        function
        | RuledBy c -> color c
        | Contested level -> colors level + " level"
        | Unclaimed -> unclaimed

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
        | Marched(p, c, from, into, count) -> $"{player p} marches {stonesOf count c} from {region from} into {region into}."
        | Drew(p, c) -> $"{player p} draws a {color c} stone from the reserve, and must now hand one back."
        | HandedBack(p, c) -> $"{player p} hands a {color c} stone back to the reserve."
        | TurnSkipped p -> $"{player p} has no stones left, so the turn is skipped and counts as a negotiation."
        | GameEnded e -> $"The game is over: {ending e}."

    let rejection =
        function
        | NotInBag(p, c) -> $"{player p} has no {color c} stone in the bag."
        | DeadGround r -> $"{region r} is dead ground - no stone may enter."
        | StandsApart r -> $"{region r} stands apart from the map and cannot be chosen."
        | NothingToBattleWith(r, c) -> $"A {color c} battle needs a {color c} stone already in {region r}, and there is none."
        | NothingToDriveOut(r, c) -> $"{region r} holds nothing but {color c} stones, so there is nothing to drive out."
        | BattleMustDriveOutSomething -> "A battle must drive out at least one stone."
        | CannotDriveOutOwnColour c -> $"A battle drives out the other colours, so {color c} cannot be named."
        | MoreDrivenThanAllowed(r, c, allowed) ->
            $"{region r} holds only {stonesOf allowed c}, so no more than that may be driven out."
        | MustChooseCasualties(r, available, allowed) ->
            $"{region r} holds {pile available}, and {allowed} of them may be driven out - name which."
        | NotStandingThere(r, c) -> $"{region r} has no {color c} stone to drive out."
        | NothingToMarch(r, c) -> $"{region r} holds no {color c} stone, so there is nothing there to march."
        | NotEnoughToMarch(r, c, held, wanted) -> $"{region r} holds {stonesOf held c}, which is not enough to march {wanted}."
        | MarchNeedsAStone -> "A march moves at least one stone."
        | NotAdjacent(from, into) -> $"{region from} does not border {region into}."
        | ReserveEmpty -> "The reserve is empty - there is nothing to negotiate for."
        | EmptyHandedCannotNegotiate p -> $"{player p}'s bag is empty, and only a player with a stone to trade may negotiate."
        | MustSettleFirst drawn ->
            $"Settle the negotiation first: a stone must go back to the reserve, and the {color drawn} stone just drawn may be it."
        | NothingToSettle -> "There is no negotiation to settle."

    /// A message written the way a player types it. The record is kept in the same
    /// words the prompt takes, so a game can be read back and played again without a
    /// second language standing between the two.
    let command msg =
        let short color =
            (glyph color |> string).ToLowerInvariant()

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

    /// What this game itself said - and the whole of what a game has to say for itself.
    /// Everything the engine says about undo, redo and a line nobody could read is said once,
    /// in `Playable`, in words that suit any game.
    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    /// A whole notice as a screen reads it: this game's half in this game's words, the
    /// engine's half in the engine's. Written out once in `Told.inWords` rather than copied
    /// here, which is what it was before there was a second game to copy it into.
    let notice = Told.inWords said command

    // --- what a notice says to the player reading it ------------------------------
    //
    // The record above keeps the whole truth of what happened, because it is the record.
    // What follows is that same truth as it reaches one player at the table, which is
    // less: a stone drawn from the reserve goes straight into a closed bag, so only the
    // player who drew it ever sees its colour.

    let eventSeenBy beholder happening =
        match happening with
        | Drew(player', _) when player' <> beholder ->
            $"{player player'} draws a stone from the reserve, and must now hand one back."
        | _ -> event happening

    /// A refusal is public - asking is part of what happened at the table - but this one
    /// names the stone just drawn, and it stays on screen after the turn has moved on.
    /// Only the player who drew can be told it, and the heading gives them the colour
    /// anyway, so nothing is lost by leaving it out here.
    let private rejectionSeenBy refusal =
        match refusal with
        | MustSettleFirst _ ->
            "Settle the negotiation first: a stone must go back to the reserve, and the one just drawn may be it."
        | _ -> rejection refusal

    /// This game's half, as it reaches one seat.
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
