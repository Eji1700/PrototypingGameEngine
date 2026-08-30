namespace Prototyping.Turncoats

open System
open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

module Render =

    let seated = Scene.seated Words.player


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
            | Home colour -> sprintf "[%2d] %s (%c)" (Words.number region.Id) name (Words.glyph colour)
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
            | RuledBy colour -> $">{Words.glyph colour}"
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

        // The page's own, for the controls it draws under a region and no terminal does.
        let recruiting =
            "The letters under a region recruit a stone into it, and '?' shows why it is ruled as it is."

        let acting =
            "Where a region holds stones there is a second row: '×R' battles with a Red one and drives out all it may, and 'R→8' marches a Red one into 8."

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
        let thisTurn = "This turn"
        let table = "The table"
        let region regionId = $"Region {Words.number regionId}"

    module Supply =
        let onTheBoard = "on the board"
        let inReserve = "in reserve"
        let outOfSight = "out of sight"

    let private verbs =
        [ "r b 5", "recruit a Blue stone into 5"
          "n", "negotiate for a stone"
          "b r 8", "battle in 8 with a Red one"
          "return g", "hand a Green one back"
          "m g 8 5 2", "march 2 Green from 8 into 5"
          "rule 8", "show why 8 is ruled as it is"
          Commands.restart
          "players 3", $"deal a fresh game to three; the game takes {Table.MinPlayers} to {Table.MaxPlayers}"
          Commands.resign ]
        @ Commands.verbs

    let commands = (Scene.verbs verbs).Split '\n' |> List.ofArray

    let private playerLine active beholder (playerId, bag) =
        let marker = if playerId = active then "->" else "  "
        let name = seated (playerId = beholder) playerId
        sprintf "  %s %-15s bag: %s" marker name (Words.sight bag)

    let private steps describeLabel describeCandidate (steps: Tiebreak.Step<'L, 'T> list) =
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
            | [ colour ] -> $"  {Words.colour colour} rules the region."
            | tied -> $"  {Words.colours tied} are level after every tie-breaker, so the region is tied and has no ruler."

        let heading =
            $"{Words.region regionId} holds {Words.pile (Game.stones regionId game)}."

        String.concat Environment.NewLine (heading :: steps Words.rulingMeasure Words.colour trace @ [ verdict ])

    // The verdict is read off the same two cascades the working is written from, so what is
    // shown settling it and what settled it cannot come apart.
    let result game =
        let factions, factionTrace = Outcome.weighFactions game

        let factionVerdict =
            match factions with
            | [ colour ] -> $"  {Words.colour colour} carries the board."
            | tied -> $"  {Words.colours tied} are level after every tie-breaker, so the game is a draw."

        let players =
            match factions with
            | [ _ ] when Game.allBagsEmpty game ->
                [ ""
                  "THE WINNING PLAYER"
                  "  Every player has played out their bag, so nobody wins." ]
            | [ winning ] ->
                let survivors, trace = Outcome.weighPlayers winning game

                let verdict =
                    match survivors with
                    | [ player ] -> $"  {Words.player player.Id} wins."
                    | _ -> "  No player could be told apart, so nobody wins."

                [ ""; "THE WINNING PLAYER" ]
                @ steps Words.playerMeasure (fun (p: Player) -> Words.player p.Id) trace
                @ [ verdict ]
            | _ -> []

        [ "THE WINNING FACTION" ]
        @ steps Words.factionMeasure Words.colour factionTrace
        @ [ factionVerdict ]
        @ players

    let heading (beholder: Player) model =
        let active = Game.active (Playing.game model)

        match Playing.session model with
        | Finished over -> $"Game over after {Words.turns over.Turn} - {Words.ending over.Ending}"
        | InPlay { Phase = AwaitingReturn drawn
                   Turn = turn } ->
            let stone =
                if active.Id = beholder.Id then $"a {Words.colour drawn} stone" else "a stone"

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
        | [] -> Scene.NothingYet
        | entries -> String.concat Environment.NewLine ((entries |> List.collect entry) @ [ ""; "  " + recordStanding model ])

    let model (margins: Margins) (beholder: Player) model =
        let game = Playing.game model
        let active = Game.active game
        let over = Playing.isOver model

        let seen =
            if over then Knowledge.laidBare beholder game else Knowledge.seenBy beholder game

        let told = wording beholder model

        let noted note =
            if margins.Notes then
                "" :: (Scene.wrap (mapWidth - 6) note |> List.map (fun line -> "  " + line))
            else
                []

        let block (name: string) lines =
            name.ToUpperInvariant() :: (lines @ [ "" ])

        let run =
            match Playing.session model with
            | InPlay play -> [ "  " + negotiationRun play game ]
            | Finished _ -> []

        let standing = Game.landStanding game

        let ruled =
            StoneColour.all
            |> List.map (fun colour -> $"{Words.colour colour} {Map.find colour standing.Ruled}")
            |> String.concat "   "

        let supplied label what = sprintf "  %-13s %s" (label + ":") what

        [ yield ""
          yield $"=== {heading beholder model} ==="
          yield ""
          yield! block Blocks.map (mapLines game @ noted Notes.map)
          yield! block Blocks.apart (apartLines game Board.apartRegions @ noted Notes.apart)
          yield! block Blocks.players ((seen.Bags |> List.map (playerLine active.Id seen.Beholder)) @ run)

          yield!
              block
                  Blocks.landRuled
                  ([ $"  {ruled}   {Words.tied} {standing.Tied}   {Words.unclaimed} {standing.Vacant}" ]
                   @ noted Notes.landRuled)

          yield!
              block
                  Blocks.supply
                  ([ supplied Supply.onTheBoard (Words.tally (Position.total seen.Position))
                     supplied Supply.inReserve (Words.sight seen.Reserve)
                     supplied Supply.outOfSight (Words.tally seen.Unseen) ]
                   @ (if over then [] else noted Notes.supply))

          if over then yield! block Blocks.result (result game)

          if margins.Commands then
              yield! block Blocks.commands (commands @ [ ""; "  " + shorthand ])

          if margins.Logged then
              yield! block Blocks.log (model.Log |> List.rev |> List.map (fun notice -> $"  {told notice}")) ]
        |> List.map (fun line -> line + Environment.NewLine)
        |> String.concat ""

    let waiting (seats: Waiting list) =
        let standing (seat: Waiting) =
            sprintf "    %-15s %s" (seated seat.Yours seat.Player) (Scene.Filling.standing seat)

        String.concat
            Environment.NewLine
            ([ ""; $"=== {Scene.Filling.title} ==="; "" ]
             @ (seats |> List.map standing)
             @ [ ""; "  " + Scene.Filling.stillToCome seats; "" ])

    // The four actions at length, then the same box the board shows: one list of what can be
    // typed, so that help and the board cannot drift apart.
    let help =
        String.concat
            Environment.NewLine
            ([ "Each turn, take one of the four actions:"
               "  recruit <colour> <region>              place a stone from your bag into any region"
               "                                         but the dead one (alias: r)"
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
               "Undo goes back in time rather than rolling again: a negotiation taken back"
               "and made again draws the same stone. Both are written into the record, so a"
               "saved game replays exactly as it was played, doubling back and all."
               ""
               "COMMANDS" ]
             @ commands
             @ [ ""
                 "Colours: r/red, b/blue, g/green - and k/black, which the earliest records wrote"
                 "for green. Regions are numbered by the board above. Battle and march cannot"
                 "target the dead region, the Flag or the Axe." ])
