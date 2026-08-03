namespace TCModel.Console

open System
open System.Text
open TCModel.Common
open TCModel.Domain
open TCModel.App

/// The V of MVU: a pure projection from the model to console text.
module Render =

    let private borders regionId =
        match Board.neighbours regionId |> Set.toList with
        | [] -> "-"
        | ids -> ids |> List.map (Words.number >> string) |> String.concat ","

    let private regionLine game (region: Region) =
        sprintf
            "  [%2d] %-18s %-11s %-15s %-8s %s"
            (Words.number region.Id)
            region.Name
            (Words.kind region.Kind)
            (borders region.Id)
            (Game.ruleOver region.Id game |> Words.rule)
            (Game.stones region.Id game |> Words.stones)

    let private playerLine active (player: Player) =
        let marker = if player.Id = active then "->" else "  "
        sprintf "  %s %-9s bag: %s" marker (Words.player player.Id) (Words.tally player.Bag)

    let private section (sb: StringBuilder) (title: string) lines =
        sb.AppendLine(title) |> ignore
        lines |> List.iter (fun line -> sb.AppendLine(line: string) |> ignore)
        sb.AppendLine() |> ignore

    /// One cascade step, as "label: standing -> who survived".
    let private steps describeLabel describeCandidate (steps: Cascade.Step<'L, 'T> list) =
        steps
        |> List.map (fun step ->
            let standing =
                step.Standing
                |> List.map (fun (candidate, n) -> $"{describeCandidate candidate} {n}")
                |> String.concat ", "

            let outcome =
                match step.Survivors with
                | [ one ] -> $"{describeCandidate one} leads"
                | many -> (many |> List.map describeCandidate |> String.concat ", ") + " still level"

            $"  {describeLabel step.Label}: {standing} -> {outcome}")

    /// Show the working behind who rules a region.
    let explainRule regionId model =
        let game = Model.game model
        let survivors, trace = Game.weighRule regionId game

        let verdict =
            match survivors with
            | [] -> "  The region holds no stones, so no colour rules it."
            | [ color ] -> $"  {Words.color color} rules the region."
            | tied -> $"  {Words.colors tied} are level after every tie-breaker, so the region is tied and has no ruler."

        let heading = $"{Words.region regionId} holds {Words.pile (Game.stones regionId game)}."

        String.concat Environment.NewLine (heading :: steps Words.rulingMeasure Words.color trace @ [ verdict ])

    /// Both winning cascades, written out.
    let private result game =
        let factions, factionTrace = Outcome.weighFactions game

        let factionVerdict =
            match factions with
            | [ color ] -> [ $"  {Words.color color} carries the board." ]
            | tied -> [ $"  {Words.colors tied} are level after every tie-breaker, so the game is a draw." ]

        let players =
            match factions with
            | [ winning ] when Game.allBagsEmpty game ->
                [ ""; "THE WINNING PLAYER"; "  Every player has played out their bag, so nobody wins." ]
            | [ winning ] ->
                let _, trace = Outcome.weighPlayers winning game

                let verdict =
                    match Outcome.verdict game with
                    | Won(_, playerId) -> $"  {Words.player playerId} wins."
                    | Drawn(NoPlayerSeparated _) -> "  No player could be told apart, so nobody wins."
                    | Drawn(EveryBagPlayedOut _) -> "  Every player has played out their bag, so nobody wins."
                    | Drawn(NoFactionSeparated _) -> "  No faction carried the board."

                [ ""; "THE WINNING PLAYER" ]
                @ steps Words.playerMeasure (fun (p: Player) -> Words.player p.Id) trace
                @ [ verdict ]
            | _ -> []

        [ "THE WINNING FACTION" ]
        @ steps Words.factionMeasure Words.color factionTrace
        @ factionVerdict
        @ players

    /// The record of the game so far, as it stands and as it will be saved.
    let history model =
        let entry (entry: Entry) =
            let asked =
                sprintf "  %3d  turn %-4d %-9s %s" entry.Ordinal entry.Turn (Words.player entry.Actor) (Words.command entry.Asked)

            let told =
                entry.Told |> List.map (fun notice -> String.replicate 26 " " + Words.notice notice)

            asked :: told

        let standing =
            let made = Timeline.movesMade model.Timeline
            let back = Timeline.movesTakenBack model.Timeline

            match back with
            | 0 -> $"{made} move(s) stand between the deal and here."
            | _ -> $"{made} move(s) stand between the deal and here, with {back} taken back and waiting to be made again."

        match Journal.entries model.Journal with
        | [] -> "Nothing has happened yet."
        | entries ->
            String.concat Environment.NewLine ((entries |> List.collect entry) @ [ ""; "  " + standing ])

    /// Render the whole game as a block of text.
    let model model =
        let sb = StringBuilder()
        let game = Model.game model
        let active = Game.active game

        let heading =
            match Model.session model with
            | Finished over -> $"Game over after {over.Turn} turns - {Words.ending over.Ending}"
            | InPlay { Phase = AwaitingReturn drawn; Turn = turn } ->
                $"Turn {turn} - {Words.player active.Id} drew a {Words.color drawn} stone and must hand one back"
            | InPlay { Turn = turn } -> $"Turn {turn} - {Words.player active.Id} to play"

        sb.AppendLine().AppendLine($"=== {heading} ===").AppendLine() |> ignore

        sb.AppendLine(sprintf "  %-4s %-18s %-11s %-15s %-8s %s" "id" "region" "kind" "borders" "ruler" "stones")
        |> ignore

        let byKind predicate =
            Board.regions |> List.filter (fun region -> predicate region.Kind) |> List.map (regionLine game)

        section sb "HOMELANDS" (byKind (function Home _ -> true | _ -> false))
        section sb "WILDS" (byKind (function Wild -> true | _ -> false))
        section sb "SPECIAL (standing alone)" (byKind (function Special -> true | _ -> false))
        section sb "DEAD" (byKind (function Dead -> true | _ -> false))

        let run =
            match Model.session model with
            | InPlay play ->
                [ $"  negotiations in a row: {play.Negotiations} of {Game.playerCount game} - the game ends on the last" ]
            | Finished _ -> []

        section sb "PLAYERS" ((Game.players game |> List.map (playerLine active.Id)) @ run)

        let ruled =
            Game.standings game
            |> Map.toList
            |> List.sortBy (fun (color, _) -> List.findIndex ((=) color) StoneColor.all)
            |> List.map (fun (color, n) -> $"{Words.color color} {n}")
            |> String.concat "   "

        let counted predicate =
            Game.landRulings game |> List.filter (snd >> predicate) |> List.length

        section
            sb
            "LAND RULED"
            [ $"  {ruled}   tied {counted (function Contested _ -> true | _ -> false)}   unclaimed {counted (function Unclaimed -> true | _ -> false)}"
              "  (the Flag and the Axe are manoeuvres, not land, and do not count here)" ]

        section
            sb
            "SUPPLY"
            [ $"  on the board: {Words.tally (Position.total game.Position)}"
              $"  in reserve:   {Words.tally game.Reserve}" ]

        match Model.session model with
        | InPlay _ -> ()
        | Finished _ -> section sb "RESULT" (result game)

        section sb "LOG" (model.Log |> List.rev |> List.map (fun notice -> $"  {Words.notice notice}"))

        sb.ToString()

    let help =
        String.concat
            Environment.NewLine
            [ "Each turn, take one of the four actions:"
              "  recruit <colour> <region>              place a stone from your bag on the map (alias: r)"
              "  battle <colour> <region> [colours...]  place a stone in the Axe, then drive that many"
              "                                         stones of other colours out of the region (alias: b)"
              "                                         name no colours to drive out all you may"
              "  march <colour> <from> <to> [count]     place a stone in the Flag, then move matching"
              "                                         stones into a bordering region (alias: m)"
              "  negotiate                              draw a stone from the reserve (alias: n)"
              "    then: return <colour>                hand a stone back - one always must go back,"
              "                                         and it may be the one just drawn"
              ""
              "A battle needs a stone of its colour in the region and something else to drive"
              "out, and must drive out at least one. A march needs stones of its colour to move."
              "The game ends once every player has negotiated in a row. An empty-handed player"
              "has their turn skipped, and that counts as a negotiation."
              ""
              "Walking the game:"
              "  undo                      take the last move back, whoever made it"
              "  redo                      make again the move last taken back"
              "  history                   the whole record of the game so far"
              "  save                      write the record out now, without waiting"
              ""
              "Undo goes back in time rather than rolling again: a negotiation taken back"
              "and made again draws the same stone. Both are written into the record, so a"
              "saved game replays exactly as it was played, doubling back and all."
              ""
              "Other commands:"
              "  rule <region>             show the working behind who rules a region"
              "  restart [seed]            deal a fresh game to the same players"
              $"  players <n> [seed]        deal a fresh game to n players ({Table.MinPlayers}-{Table.MaxPlayers})"
              "  help                      show this list"
              "  quit                      leave, saving the record on the way out"
              ""
              "Colours: r/red, b/blue, k/black. Regions are numbered by the board above."
              "Battle and march cannot target the dead region, the Flag or the Axe." ]
