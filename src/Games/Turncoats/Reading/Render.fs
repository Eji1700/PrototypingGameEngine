namespace TCModel.Turncoats

open System
open System.Text
open TCModel.Common
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing
// namespace, and both `Spectre.Console` and the command line's argument types carry
// names this game already uses - `Region`, `Open`, `View`.
open TCModel.Turncoats

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

    let private mapWidth = (Board.layout |> List.collect sides |> List.max) + 1

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

        let covered at =
            ground |> List.exists (fun (left, right) -> left <= at && at <= right)

        // Each sloping side runs from a region's upright side to the point of the region
        // beyond it, which is the ground between the two.
        for left, right in ground do
            for at in left..right do
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

    // --- what both views say ------------------------------------------------------------
    //
    // A view lays a screen out as it likes, but what the screen says about the game is one
    // decision and not one per view. Anything below is written once and read by both, so
    // two boards can look nothing alike and still not disagree.

    /// How a stone and a region are named at the prompt, for a player looking at a board
    /// rather than at the help.
    let shorthand = "Colours are r, b and g; regions are numbered on the map above."

    /// Where the game stands between the deal and here, once moves have been taken back.
    let recordStanding model =
        let made = Timeline.movesMade model.Timeline
        let back = Timeline.movesTakenBack model.Timeline

        match made, back with
        | 0, 0 -> "The game stands where it was dealt."
        | _, 0 -> $"The game stands {Words.moves made} from the deal."
        | _ ->
            $"The game stands {Words.moves made} from the deal, with {Words.moves back} taken back and waiting to be made again."

    /// How close the game is to ending. Not a note - it is part of the position, and stays
    /// on screen with the notes turned off.
    let negotiationRun play game =
        let seats = Game.playerCount game
        $"negotiations in a row: {play.Negotiations} of {seats} - the game ends at {seats}"

    // --- the notes ------------------------------------------------------------------
    //
    // Writing that explains the board rather than states it, which `notes off` takes away.
    // It is written here rather than in each view because three renderers each phrasing the
    // same explanation is three explanations, and these three had already drifted: the map
    // was read as sharing a "side" in one view and a "wall" in another, one view called the
    // dead region wild, and two of the four notes were shown by one view and not the others.
    // What a note says is one decision, like what a region says; how wide it is drawn is
    // each view's own business.
    module Notes =

        /// How to read the map. What each view adds to this is how *it* draws the map,
        /// which is the one part that really is its own.
        let map =
            "Two regions border each other where they share a side, and nowhere else: two that meet only at a point do not. A home has its own colour after its name, '>' marks the colour ruling a region and '=' the colours level in it, and the dead region says so where its stones would be."

        /// For the views that draw a region as a box of its own.
        let bordered = "A region is drawn in the colour of whoever rules it."

        let apart =
            "The Flag and the Axe hold the stones that battles and marches are paid for with. They border nothing, so nothing can be marched into them, and neither is land - what they settle is ties."

        let landRuled =
            "Only land is counted here: not the Flag or the Axe, which stand outside the map, and not the dead region, which nobody may ever hold."

        let supply =
            "Every bag but your own is closed, and so is the reserve, so those are counted rather than read. But every stone is somewhere: whatever is neither on the map nor in your bag is out of sight, and its colours are exact. Where it is, is what you cannot know."

    /// What the blocks of a screen are called.
    ///
    /// Which blocks there are and what they are named is one decision; how each view draws
    /// a heading is three. `plain` shouts them, `rich` writes them into the top wall of a
    /// panel and `html` gives them a heading element, and all three take the name from
    /// here - so a block renamed is renamed everywhere, and one added cannot be added to
    /// two screens out of three.
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

    /// The three lines of the supply block, which every view lists in this order.
    module Supply =
        let onTheBoard = "on the board"
        let inReserve = "in reserve"
        let outOfSight = "out of sight"

    /// A table that has not filled up yet, as the people waiting are told it.
    ///
    /// There is no game to draw here - it is the one screen built from a list of who has
    /// arrived rather than from a position - and all three views build it. What it says is
    /// therefore here, and only how it is laid out is theirs.
    module Filling =
        let title = "Waiting for the table to fill"

        let standing (seat: Waiting) =
            if seat.Expected then "still to arrive"
            elif seat.Away then "here, but their console has dropped"
            else "here"

        let stillToCome (seats: Waiting list) =
            let expected = seats |> List.filter (fun seat -> seat.Expected) |> List.length
            $"{expected} more to come. The game begins once every seat is taken."

    /// A paragraph as lines of at most `room` characters, for a view that has to fit one.
    /// Words are never broken.
    let wrap room (text: string) =
        let put (lines, line) word =
            if line = "" then lines, word
            elif String.length line + 1 + String.length word <= room then lines, line + " " + word
            else lines @ [ line ], word

        let lines, last = text.Split ' ' |> Array.fold put ([], "")
        lines @ [ last ]

    /// A record with nothing in it yet.
    let nothingYet = "Nothing has happened yet."

    /// The commands in brief, each shown as it would be typed. This sits with the board
    /// because that is where a player is looking; `Render.help` says all of it at length.
    let commands =
        [ ("r b 5", "recruit a Blue stone into 5"), ("n", "negotiate for a stone")
          ("b r 8", "battle in 8 with a Red one"), ("return g", "hand a Green one back")
          ("m g 8 5 2", "march 2 Green from 8 into 5"), ("undo, redo", "walk the game back")
          ("rule 8", "show why 8 is ruled as it is"), ("history", "the record so far")
          ("notes", "hide this and every note"), ("save", "write the record now")
          ("help", "every command, at length"), ("quit", "leave, saving first") ]
        |> List.map (fun ((typed, does), (alsoTyped, alsoDoes)) -> sprintf "  %-13s%-30s%-12s%s" typed does alsoTyped alsoDoes)

    /// A player and their bag as the reader sees it - their own laid out, everyone
    /// else's closed. The arrow marks whoever is to play, which over a network is not
    /// always the one reading, so the reader's own seat is named as well.
    let private playerLine active beholder (playerId, bag) =
        let marker = if playerId = active then "->" else "  "
        let name = Words.seated (playerId = beholder) playerId
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

    /// How a notice reads to the player at this screen: while the game runs they are
    /// told only what they could know, and once it is over there is nothing left to
    /// hold back. Around one keyboard the beholder is whoever is to play; over a
    /// network every console has a beholder of its own and they are all different.
    /// What a screen says it is: whose turn, and what they owe. Public, and used by every
    /// view rather than written out again in each, because the middle case is the one
    /// place the drawn stone is named outright - and over a network that heading is read
    /// by people who did not draw it.
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

    /// The record of the game so far, as the player reading it may know it. The journal
    /// itself keeps the whole of what happened, and `Transcript.write` saves it that way.
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
        let game = Playing.game model
        let active = Game.active game

        // Everything below is drawn from what the beholder can see rather than from the
        // game itself - until the game is over, when the table is turned face up.
        let seen =
            if Playing.isOver model then Knowledge.laidBare beholder game else Knowledge.seenBy beholder game

        let told = wording beholder model

        /// A note, laid out for this view: a blank line, then the paragraph broken to the
        /// width of the map it sits under and indented to match everything else in a block.
        let noted note =
            if notes then "" :: (wrap (mapWidth - 6) note |> List.map (fun line -> "  " + line)) else []

        /// A note that is only true while something is still being held back.
        let notedWhileHidden note =
            if Playing.isOver model then [] else noted note

        sb.AppendLine().AppendLine($"=== {heading beholder model} ===").AppendLine()
        |> ignore

        /// This view shouts a block's name, which the other two do not - so the name comes
        /// from `Blocks` and only the shouting is decided here.
        let block sb name lines =
            section sb ((name: string).ToUpperInvariant()) lines

        block sb Blocks.map (mapLines game @ noted Notes.map)

        block sb Blocks.apart (apartLines game Board.apartRegions @ noted Notes.apart)

        let run =
            match Playing.session model with
            | InPlay play -> [ "  " + negotiationRun play game ]
            | Finished _ -> []

        block sb Blocks.players ((seen.Bags |> List.map (playerLine active.Id seen.Beholder)) @ run)

        // How the land stands is the game's own reckoning, not this view's - dead ground
        // is unclaimed and always will be, and which regions count at all is a question
        // about the board rather than about how it is written down.
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

        // The commands go with the notes: a player who has turned them off has turned
        // this off too, and `help` still says all of it.
        if notes then block sb Blocks.commands (commands @ [ ""; "  " + shorthand ])

        block sb Blocks.log (model.Log |> List.rev |> List.map (fun notice -> $"  {told notice}"))

        sb.ToString()

    /// A table still filling up. There is no game to draw yet, so this is the one screen
    /// drawn from a list of who has arrived rather than from a position.
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
              "  notes [on|off]            show or hide the writing that explains the board,"
              "                            and the list of commands that goes with it"
              "  restart [seed]            deal a fresh game to the same players"
              $"  players <n> [seed]        deal a fresh game to n players ({Table.MinPlayers}-{Table.MaxPlayers})"
              "  help                      show this list"
              "  quit                      leave, saving the record on the way out"
              ""
              "Colours: r/red, b/blue, g/green. Regions are numbered by the board above."
              "Battle and march cannot target the dead region, the Flag or the Axe." ]
