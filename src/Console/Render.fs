namespace TCModel.Console

open System
open System.Text
open TCModel.Common
open TCModel.Domain
open TCModel.App

/// The V of MVU: a pure projection from the model to console text.
module Render =

    // --- the map ----------------------------------------------------------------
    //
    // `Board.layout` lies on a triangular lattice, which is drawn here as a honeycomb:
    // every region is a hex two half-columns wide, and each row is laid half a region
    // across from the one above, so a region has an upright side to its left and right
    // and a cut corner running to the two regions above and the two below. Those six
    // are exactly its neighbours - a side shared between two regions is a border, and
    // regions that meet only at a point are not neighbours. So no border has to be
    // drawn as a line into open ground, and none can be drawn wrong: `Board.problems`
    // checks the layout against the borders before a game is ever dealt.

    /// Characters to a half-column, so a region is two of them wide, wall to wall.
    let private step = 11

    /// The room between a region's walls, and the room its writing takes inside that,
    /// which is the same less a margin either side.
    let private cell = 2 * step - 1
    let private written = cell - 2

    let private column halfColumn = halfColumn * step

    /// Where a row's upright sides stand: one at each region's left, and one closing
    /// the row.
    let private sides row =
        match row with
        | [] -> []
        | _ -> (row |> List.map (snd >> column)) @ [ column (List.last row |> snd) + 2 * step ]

    /// Each region in a row as the two columns it stands between.
    let private spans row =
        row |> List.map (fun (_, at) -> column at, column at + 2 * step)

    let private mapWidth =
        (Board.layout |> List.collect sides |> List.max) + 1

    /// A region as it is titled on the map: its number and name, and for a home the
    /// colour that holds it. A name with no room to spare gives up its "The".
    ///
    /// Takes the room it has, because every view draws a region into a space of its own
    /// size. What it says is the same in all of them; only how much fits differs.
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

    /// What stands in a region, with who rules it held against the right-hand edge.
    /// A full region falls back to the tally rather than run into its neighbour.
    ///
    /// Public and taking its room for the same reason as the title: what a region says is
    /// one decision, and where it is written is another. A view that draws a region as a
    /// box of its own asks for it at whatever width that box has.
    let standingIn wall game (region: Region) =
        let ruler =
            match Game.ruleOver region.Id game with
            | RuledBy color -> $">{Words.glyph color}"
            | Contested tied -> "=" + (tied |> List.map (Words.glyph >> string) |> String.concat "")
            | Unclaimed -> ""

        // A space between the stones and the ruler keeps the two apart when a region
        // fills up.
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

    /// The map is laid out by column rather than written left to right, so its lines
    /// are painted onto a blank one.
    let private paint (line: char array) at (text: string) =
        text |> String.iteri (fun i c -> if at + i < line.Length then line[at + i] <- c)

    /// A row of regions: what each one is, and what stands in it, between the upright
    /// sides it shares with its neighbours either side.
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

    /// The line where one row meets the next, and so where the borders between them
    /// run. Each region's flat side comes down to it, and the corners cut from either
    /// end fall into the point of the region below and rise to the point of the one
    /// above - a run of valleys and peaks, half a region apart.
    let private mapBetween above below =
        let ground = spans above @ spans below
        let line = Array.create mapWidth ' '
        let middle (left, right) = (left + right) / 2
        let covered at = ground |> List.exists (fun (left, right) -> left <= at && at <= right)

        // Each sloping side runs from a region's upright side to the point of the region
        // beyond it, which is the ground between the two.
        for left, right in ground do
            for at in left .. right do
                line[at] <- '_'

        // Cut the ground away either side of a point, leaving the point itself and any
        // ground the map does not reach.
        let point at (before, after) =
            if covered (at - 1) then line[at - 1] <- before
            if covered (at + 1) then line[at + 1] <- after

        let dip = '\\', '/'
        let peak = '/', '\\'

        // A region above comes to a point below its middle, and a region below rises to
        // one above its own, so the line dips and peaks by turns half a region apart.
        // Where a row runs out, the row beyond it does the same about its upright sides,
        // which is what closes the honeycomb along the edges of the map.
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

    /// The map, one line at a time: the honeycomb this view draws it as.
    ///
    /// What a region *says* is shared with every other view - `regionTitle` and
    /// `standingIn` above - but where the regions are put is this view's own business,
    /// and `rich` puts them somewhere else entirely.
    let private mapLines game =
        let rec draw above rows =
            match rows with
            | [] -> [ mapBetween above [] ]
            | row :: rest -> mapBetween above row :: mapRow game row @ draw row rest

        draw [] Board.layout

    /// The Flag and the Axe, drawn in the same hand as the map but standing clear of it
    /// and of each other: sharing no wall with anything, they border nothing.
    let private apartLines game regions =
        let across piece =
            regions |> List.map piece |> String.concat "   "

        let inside (text: string) = "| " + text.PadRight written + " |"
        let half = String.replicate (step - 1) "_"

        [ across (fun _ -> half + "/ \\" + half)
          across (mapTitle >> inside)
          across (fun region -> inside (mapStanding game region))
          across (fun _ -> half + "\\_/" + half) ]

    /// The commands in brief, each shown as it would be typed. This sits with the board
    /// because that is where a player is looking; `Render.help` says all of it at length.
    let commands =
        [ ("r b 5", "recruit a Blue stone into 5"), ("n", "negotiate for a stone")
          ("b r 8", "battle in 8 with a Red one"), ("return g", "hand a Green one back")
          ("m g 8 5 2", "march 2 Green from 8 into 5"), ("undo, redo", "walk the game back")
          ("rule 8", "show why 8 is ruled as it is"), ("history", "the record so far")
          ("notes", "hide this and every note"), ("save", "write the record now")
          ("help", "every command, at length"), ("quit", "leave, saving first") ]
        |> List.map (fun ((typed, does), (alsoTyped, alsoDoes)) ->
            sprintf "  %-13s%-30s%-12s%s" typed does alsoTyped alsoDoes)

    /// A player and their bag as the reader sees it - their own laid out, everyone
    /// else's closed. The arrow marks whoever is to play, which over a network is not
    /// always the one reading, so the reader's own seat is named as well.
    let private playerLine active beholder (playerId, bag) =
        let marker = if playerId = active then "->" else "  "
        let name = Words.player playerId + (if playerId = beholder then " (you)" else "")
        sprintf "  %s %-15s bag: %s" marker name (Words.sight bag)

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
    let result game =
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

    /// How a notice reads to the player at this screen: while the game runs they are
    /// told only what they could know, and once it is over there is nothing left to
    /// hold back. Around one keyboard the beholder is whoever is to play; over a
    /// network every console has a beholder of its own and they are all different.
    /// What a screen says it is: whose turn, and what they owe. Public, and used by every
    /// view rather than written out again in each, because the middle case is the one
    /// place the drawn stone is named outright - and over a network that heading is read
    /// by people who did not draw it.
    let heading (beholder: Player) model =
        let active = Game.active (Model.game model)

        match Model.session model with
        | Finished over -> $"Game over after {over.Turn} turns - {Words.ending over.Ending}"
        | InPlay { Phase = AwaitingReturn drawn; Turn = turn } ->
            let stone =
                if active.Id = beholder.Id then $"a {Words.color drawn} stone" else "a stone"

            $"Turn {turn} - {Words.player active.Id} drew {stone} and must hand one back"
        | InPlay { Turn = turn } -> $"Turn {turn} - {Words.player active.Id} to play"

    let wording (beholder: Player) model =
        if Model.isOver model then
            Words.notice
        else
            Words.noticeSeenBy beholder.Id

    /// The record of the game so far, as the player reading it may know it. The journal
    /// itself keeps the whole of what happened, and `Transcript.write` saves it that way.
    let history beholder model =
        let told = wording beholder model

        let entry (entry: Entry) =
            let asked =
                sprintf "  %3d  turn %-4d %-9s %s" entry.Ordinal entry.Turn (Words.player entry.Actor) (Words.command entry.Asked)

            asked :: (entry.Told |> List.map (fun notice -> String.replicate 26 " " + told notice))

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

    /// Render the whole game as a block of text for one player to read. `notes` says
    /// whether the writing that explains the board comes with it: turned off, what is
    /// left is the position and nothing else, for a player who already knows how to
    /// read it.
    ///
    /// `beholder` is whose screen this is. Around one keyboard that is always the
    /// player to act; over a network it is one of several, each being drawn a board of
    /// their own from the same game.
    let model notes (beholder: Player) model =
        let sb = StringBuilder()
        let game = Model.game model
        let active = Game.active game

        // Everything below is drawn from what the beholder can see rather than from the
        // game itself - until the game is over, when the table is turned face up.
        let seen =
            if Model.isOver model then
                Knowledge.laidBare beholder game
            else
                Knowledge.seenBy beholder game

        let told = wording beholder model

        /// Lines that are there to be read once, and only while they are still wanted.
        let noted lines = if notes then lines else []

        /// Notes about what is being held back, which are only true while it still is.
        let notedWhileHidden lines =
            if Model.isOver model then [] else noted lines

        sb.AppendLine().AppendLine($"=== {heading beholder model} ===").AppendLine() |> ignore

        section
            sb
            "THE MAP"
            (mapLines game
             @ noted
                 [ ""
                   "  Two regions border each other where they share a side, and nowhere else -"
                   "  regions that meet only at a point do not. A home carries its colour, '>'"
                   "  marks who rules a region and '=' who is level in it, and the rest are wild." ])

        section
            sb
            "STANDING APART"
            (apartLines game (Board.regions |> List.filter (fun region -> RegionKind.isIsolated region.Kind))
             @ noted [ ""; "  Bought with stones, but no part of the map and no part of the land." ])

        let run =
            match Model.session model with
            | InPlay play ->
                [ $"  negotiations in a row: {play.Negotiations} of {Game.playerCount game} - the game ends on the last" ]
            | Finished _ -> []

        section sb "PLAYERS" ((seen.Bags |> List.map (playerLine active.Id seen.Beholder)) @ run)

        let ruled =
            Game.standings game
            |> Map.toList
            |> List.sortBy (fun (color, _) -> List.findIndex ((=) color) StoneColor.all)
            |> List.map (fun (color, n) -> $"{Words.color color} {n}")
            |> String.concat "   "

        // Dead ground is unclaimed and always will be, so counting it says nothing
        // about how much of the map is still going spare.
        let contested =
            Game.landRulings game |> List.filter (fun (region, _) -> RegionKind.isOpen region.Kind)

        let counted predicate =
            contested |> List.filter (snd >> predicate) |> List.length

        section
            sb
            "LAND RULED"
            ([ $"  {ruled}   tied {counted (function Contested _ -> true | _ -> false)}   unclaimed {counted (function Unclaimed -> true | _ -> false)}" ]
             @ noted
                 [ "  (the Flag and the Axe are manoeuvres rather than land, and the dead region"
                   "  is nobody's to take, so none of the three are counted here)" ])

        section
            sb
            "SUPPLY"
            ([ $"  on the board: {Words.tally (Position.total seen.Position)}"
               $"  in reserve:   {Words.sight seen.Reserve}"
               $"  out of sight: {Words.tally seen.Unseen}" ]
             @ notedWhileHidden
                 [ "  (every bag but your own is closed, and so is the reserve, so those are"
                   "  counted rather than read. But every stone is somewhere: what is neither"
                   "  on the map nor in your bag must be out of sight, and that much is exact)" ])

        match Model.session model with
        | InPlay _ -> ()
        | Finished _ -> section sb "RESULT" (result game)

        // The commands go with the notes: a player who has turned them off has turned
        // this off too, and `help` still says all of it.
        if notes then
            section sb "COMMANDS" (commands @ [ ""; "  Colours are r, b and g; regions are numbered on the map above." ])

        section sb "LOG" (model.Log |> List.rev |> List.map (fun notice -> $"  {told notice}"))

        sb.ToString()

    /// A table still filling up. There is no game to draw yet, so this is the one screen
    /// drawn from a list of who has arrived rather than from a position.
    let waiting (seats: Waiting list) =
        let standing seat =
            let who = Words.player seat.Player + (if seat.Yours then " (you)" else "")

            let holding =
                if seat.Expected then "still to arrive"
                elif seat.Away then "here, but their console has dropped"
                else "here"

            sprintf "    %-15s %s" who holding

        let expected = seats |> List.filter (fun seat -> seat.Expected) |> List.length

        String.concat
            Environment.NewLine
            ([ ""; "=== Waiting for the table to fill ==="; "" ]
             @ (seats |> List.map standing)
             @ [ ""
                 $"  {expected} more to come. The game begins once every seat is taken."
                 "" ])

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
              "  notes [on|off]            show or hide the writing that explains the board,"
              "                            and the list of commands that goes with it"
              "  restart [seed]            deal a fresh game to the same players"
              $"  players <n> [seed]        deal a fresh game to n players ({Table.MinPlayers}-{Table.MaxPlayers})"
              "  help                      show this list"
              "  quit                      leave, saving the record on the way out"
              ""
              "Colours: r/red, b/blue, g/green. Regions are numbered by the board above."
              "Battle and march cannot target the dead region, the Flag or the Axe." ]
