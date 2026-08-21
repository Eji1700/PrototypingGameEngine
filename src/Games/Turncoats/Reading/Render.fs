namespace TCModel.Turncoats

open System
open System.Text
open TCModel.Common
open TCModel.Engine
open TCModel.Table
open TCModel.Turncoats

module Render =


    let private step = 11

    let private cell = 2 * step - 1
    let private written = cell - 2

    let private column halfColumn = halfColumn * step

    let private sides row =
        match row with
        | [] -> []
        | _ -> (row |> List.map (snd >> column)) @ [ column (List.last row |> snd) + 2 * step ]

    let private spans row =
        row |> List.map (fun (_, at) -> column at, column at + 2 * step)

    let private mapWidth = (Board.layout |> List.collect sides |> List.max) + 1

    let regionTitle room (region: Region) =
        let titled name =
            match region.Kind with
            | Home color -> sprintf "[%2d] %s (%c)" (Words.number region.Id) name (Words.glyph color)
            | Wild
            | Special
            | Dead -> sprintf "[%2d] %s" (Words.number region.Id) name

        let full = titled region.Name

        if String.length full <= room || not (region.Name.StartsWith "The ") then
            full
        else
            titled (region.Name.Substring 4)

    let private mapTitle region = regionTitle written region

    let standingIn wall game (region: Region) =
        let ruler =
            match Game.ruleOver region.Id game with
            | RuledBy color -> $">{Words.glyph color}"
            | Contested tied -> "=" + (tied |> List.map (Words.glyph >> string) |> String.concat "")
            | Unclaimed -> ""

        let room = wall - String.length ruler - 1
        let pile = Game.stones region.Id game

        let standing =
            match region.Kind with
            | Dead -> "dead"
            | Home _
            | Wild
            | Special ->
                let laid = Words.stones pile
                if String.length laid <= room then laid else Words.counted pile

        let standing =
            if String.length standing > room then standing.Substring(0, room) else standing

        standing.PadRight(wall - String.length ruler) + ruler

    let private mapStanding game region = standingIn written game region

    let private paint (line: char array) at (text: string) =
        text |> String.iteri (fun i c -> if at + i < line.Length then line[at + i] <- c)

    let private mapRow game row =
        let line () =
            let blank = Array.create mapWidth ' '
            sides row |> List.iter (fun at -> blank[at] <- '|')
            blank

        let titles, standing = line (), line ()

        for regionId, at in row do
            let region = Board.region regionId
            paint titles (column at + 2) (mapTitle region)
            paint standing (column at + 2) (mapStanding game region)

        [ String(titles).TrimEnd(); String(standing).TrimEnd() ]

    let private mapBetween above below =
        let ground = spans above @ spans below
        let line = Array.create mapWidth ' '
        let middle (left, right) = (left + right) / 2

        let covered at =
            ground |> List.exists (fun (left, right) -> left <= at && at <= right)

        for left, right in ground do
            for at in left..right do
                line[at] <- '_'

        let point at (before, after) =
            if covered (at - 1) then line[at - 1] <- before
            if covered (at + 1) then line[at + 1] <- after

        let dip = '\\', '/'
        let peak = '/', '\\'

        for span in spans above do
            point (middle span) dip

        for at in sides below do
            point at dip

        for span in spans below do
            line[middle span] <- ' '
            point (middle span) peak

        for at in sides above do
            line[at] <- ' '
            point at peak

        String(line).TrimEnd()

    let private mapLines game =
        let rec draw above rows =
            match rows with
            | [] -> [ mapBetween above [] ]
            | row :: rest -> mapBetween above row :: mapRow game row @ draw row rest

        draw [] Board.layout

    let private apartLines game regions =
        let across piece =
            regions |> List.map piece |> String.concat "   "

        let inside (text: string) = "| " + text.PadRight written + " |"
        let half = String.replicate (step - 1) "_"

        [ across (fun _ -> half + "/ \\" + half)
          across (mapTitle >> inside)
          across (fun region -> inside (mapStanding game region))
          across (fun _ -> half + "\\_/" + half) ]


    let shorthand = "Colours are r, b and g; regions are numbered on the map above."

    let recordStanding model =
        let made = Timeline.movesMade model.Timeline
        let back = Timeline.movesTakenBack model.Timeline

        match made, back with
        | 0, 0 -> "The game stands where it was dealt."
        | _, 0 -> $"The game stands {Words.moves made} from the deal."
        | _ ->
            $"The game stands {Words.moves made} from the deal, with {Words.moves back} taken back and waiting to be made again."

    let negotiationRun play game =
        let seats = Game.playerCount game
        $"negotiations in a row: {play.Negotiations} of {seats} - the game ends at {seats}"

    module Notes =

        let map =
            "Two regions border each other where they share a side, and nowhere else: two that meet only at a point do not. A home has its own colour after its name, '>' marks the colour ruling a region and '=' the colours level in it, and the dead region says so where its stones would be."

        let bordered = "A region is drawn in the colour of whoever rules it."

        let apart =
            "The Flag and the Axe hold the stones that battles and marches are paid for with. They border nothing, so nothing can be marched into them, and neither is land - what they settle is ties."

        let landRuled =
            "Only land is counted here: not the Flag or the Axe, which stand outside the map, and not the dead region, which nobody may ever hold."

        let supply =
            "Every bag but your own is closed, and so is the reserve, so those are counted rather than read. But every stone is somewhere: whatever is neither on the map nor in your bag is out of sight, and its colours are exact. Where it is, is what you cannot know."

    module Blocks =
        let map = "The map"
        let apart = "Standing apart"
        let players = "Players"
        let supply = "Supply"
        let landRuled = "Land ruled"
        let result = "Result"
        let commands = "Commands"
        let log = "Log"
        let record = "The record"
        let rules = "How the game goes"
        let region regionId = $"Region {Words.number regionId}"

    module Supply =
        let onTheBoard = "on the board"
        let inReserve = "in reserve"
        let outOfSight = "out of sight"

    module Filling =
        let title = "Waiting for the table to fill"

        let standing (seat: Waiting) =
            if seat.Expected then "still to arrive"
            elif seat.Away then "here, but their console has dropped"
            else "here"

        let stillToCome (seats: Waiting list) =
            let expected = seats |> List.filter (fun seat -> seat.Expected) |> List.length
            $"{expected} more to come. The game begins once every seat is taken."

    let wrap room (text: string) =
        let put (lines, line) word =
            if line = "" then lines, word
            elif String.length line + 1 + String.length word <= room then lines, line + " " + word
            else lines @ [ line ], word

        let lines, last = text.Split ' ' |> Array.fold put ([], "")
        lines @ [ last ]

    let nothingYet = "Nothing has happened yet."

    let commands =
        [ ("r b 5", "recruit a Blue stone into 5"), ("n", "negotiate for a stone")
          ("b r 8", "battle in 8 with a Red one"), ("return g", "hand a Green one back")
          ("m g 8 5 2", "march 2 Green from 8 into 5"), ("undo, redo", "walk the game back")
          ("rule 8", "show why 8 is ruled as it is"), ("history", "the record so far")
          ("notes", "hide every note"), ("commands", "hide this box")
          ("log", "hide what has been said"), ("view <name>", "draw the board another way")
          ("save", "write the record now"), ("help", "every command, at length")
          ("quit", "leave; 'replay' takes the game up again"), ("", "") ]
        // Two to a line, and an odd one out takes the line to itself rather than padding out a
        // second column that has nothing in it.
        |> List.map (fun ((typed, does), (alsoTyped, alsoDoes)) ->
            if alsoTyped = "" then
                sprintf "  %-13s%s" typed does
            else
                sprintf "  %-13s%-30s%-12s%s" typed does alsoTyped alsoDoes)

    let private playerLine active beholder (playerId, bag) =
        let marker = if playerId = active then "->" else "  "
        let name = Words.seated (playerId = beholder) playerId
        sprintf "  %s %-15s bag: %s" marker name (Words.sight bag)

    let private section (sb: StringBuilder) (title: string) lines =
        sb.AppendLine(title) |> ignore
        lines |> List.iter (fun line -> sb.AppendLine(line: string) |> ignore)
        sb.AppendLine() |> ignore

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

    let explainRule regionId model =
        let game = Playing.game model
        let survivors, trace = Game.weighRule regionId game

        let verdict =
            match survivors with
            | [] -> "  The region holds no stones, so no colour rules it."
            | [ color ] -> $"  {Words.color color} rules the region."
            | tied -> $"  {Words.colors tied} are level after every tie-breaker, so the region is tied and has no ruler."

        let heading =
            $"{Words.region regionId} holds {Words.pile (Game.stones regionId game)}."

        String.concat Environment.NewLine (heading :: steps Words.rulingMeasure Words.color trace @ [ verdict ])

    let result game =
        let factions, factionTrace = Outcome.weighFactions game

        let factionVerdict =
            match factions with
            | [ color ] -> [ $"  {Words.color color} carries the board." ]
            | tied -> [ $"  {Words.colors tied} are level after every tie-breaker, so the game is a draw." ]

        let players =
            match factions with
            | [ winning ] when Game.allBagsEmpty game ->
                [ ""
                  "THE WINNING PLAYER"
                  "  Every player has played out their bag, so nobody wins." ]
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

    let heading (beholder: Player) model =
        let active = Game.active (Playing.game model)

        match Playing.session model with
        | Finished over -> $"Game over after {over.Turn} turns - {Words.ending over.Ending}"
        | InPlay { Phase = AwaitingReturn drawn
                   Turn = turn } ->
            let stone =
                if active.Id = beholder.Id then $"a {Words.color drawn} stone" else "a stone"

            $"Turn {turn} - {Words.player active.Id} drew {stone} and must hand one back"
        | InPlay { Turn = turn } -> $"Turn {turn} - {Words.player active.Id} to play"

    let wording (beholder: Player) model =
        if Playing.isOver model then Words.notice else Words.noticeSeenBy beholder.Id

    let history beholder model =
        let told = wording beholder model

        let entry (entry: Entry) =
            let asked =
                sprintf "  %3d  turn %-4d %-9s %s" entry.Ordinal entry.Turn (Words.player entry.Actor) (Words.command entry.Asked)

            asked
            :: (entry.Told |> List.map (fun notice -> String.replicate 26 " " + told notice))

        match Journal.entries model.Journal with
        | [] -> nothingYet
        | entries -> String.concat Environment.NewLine ((entries |> List.collect entry) @ [ ""; "  " + recordStanding model ])

    let model (margins: Margins) (beholder: Player) model =
        let notes = margins.Notes
        let sb = StringBuilder()
        let game = Playing.game model
        let active = Game.active game

        let seen =
            if Playing.isOver model then Knowledge.laidBare beholder game else Knowledge.seenBy beholder game

        let told = wording beholder model

        let noted note =
            if notes then "" :: (wrap (mapWidth - 6) note |> List.map (fun line -> "  " + line)) else []

        let notedWhileHidden note =
            if Playing.isOver model then [] else noted note

        sb.AppendLine().AppendLine($"=== {heading beholder model} ===").AppendLine()
        |> ignore

        let block sb name lines =
            section sb ((name: string).ToUpperInvariant()) lines

        block sb Blocks.map (mapLines game @ noted Notes.map)

        block sb Blocks.apart (apartLines game Board.apartRegions @ noted Notes.apart)

        let run =
            match Playing.session model with
            | InPlay play -> [ "  " + negotiationRun play game ]
            | Finished _ -> []

        block sb Blocks.players ((seen.Bags |> List.map (playerLine active.Id seen.Beholder)) @ run)

        let standing = Game.landStanding game

        let ruled =
            StoneColor.all
            |> List.map (fun color -> $"{Words.color color} {Map.find color standing.Ruled}")
            |> String.concat "   "

        block
            sb
            Blocks.landRuled
            ([ $"  {ruled}   {Words.tied} {standing.Tied}   {Words.unclaimed} {standing.Unclaimed}" ]
             @ noted Notes.landRuled)

        let supplied label what = sprintf "  %-13s %s" (label + ":") what

        block
            sb
            Blocks.supply
            ([ supplied Supply.onTheBoard (Words.tally (Position.total seen.Position))
               supplied Supply.inReserve (Words.sight seen.Reserve)
               supplied Supply.outOfSight (Words.tally seen.Unseen) ]
             @ notedWhileHidden Notes.supply)

        match Playing.session model with
        | InPlay _ -> ()
        | Finished _ -> block sb Blocks.result (result game)

        if margins.Commands then
            block sb Blocks.commands (commands @ [ ""; "  " + shorthand ])

        if margins.Logged then
            block sb Blocks.log (model.Log |> List.rev |> List.map (fun notice -> $"  {told notice}"))

        sb.ToString()

    let waiting (seats: Waiting list) =
        let standing (seat: Waiting) =
            sprintf "    %-15s %s" (Words.seated seat.Yours seat.Player) (Filling.standing seat)

        String.concat
            Environment.NewLine
            ([ ""; $"=== {Filling.title} ==="; "" ]
             @ (seats |> List.map standing)
             @ [ ""; "  " + Filling.stillToCome seats; "" ])

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
              "  notes [on|off]            show or hide the writing that explains the board"
              "  commands [on|off]         show or hide the box listing what can be typed"
              "  restart [seed]            deal a fresh game to the same players"
              $"  players <n> [seed]        deal a fresh game to n players ({Table.MinPlayers}-{Table.MaxPlayers})"
              "  help                      show this list"
              "  quit                      leave, saving the record on the way out"
              ""
              "Colours: r/red, b/blue, g/green. Regions are numbered by the board above."
              "Battle and march cannot target the dead region, the Flag or the Axe." ]
