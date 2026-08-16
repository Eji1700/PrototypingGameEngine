namespace TCModel.Diplomacy

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses - `Open`.
open TCModel.Diplomacy

/// Every screen this game has, described once.
///
/// **The map is drawn, a province takes as many hexes as it needs, and the picture *is* the
/// border table.**
///
/// It went through four answers. First there was no map, on the argument that a board which
/// cannot be drawn faithfully is better not drawn: the other game of maps here prints a
/// honeycomb because its borders really are a patch of a triangular lattice, so its picture *is*
/// its border table. Then there was a map of one hex a province - and that argument turned out
/// to be about completeness rather than honesty, which are not the same thing.
///
/// The third answer is what fixed it. One hex gives a province six sides; provinces on this board
/// have up to eleven neighbours, and that ceiling is what held the first map to about half its
/// borders. A region three or four hexes across has sides to spare.
///
/// What that cost was a word in the description. A cell of this map is a `Patch` and says which
/// region it is part of, so the readers draw a province as one shape rather than four boxes
/// sharing a name - and the wall round the shape takes the colour of whoever holds its centre,
/// which is the one thing on this board that says at a glance whose is whose.
///
/// The fourth was going back over the places the layout had settled for less, which took the map
/// from nine borders in ten to all two hundred and six - so `Atlas.problems` now refuses a game
/// in both directions: **every side the picture draws between two provinces is a real border, and
/// every border is drawn.** The picture *is* the border table. A side between two hexes of the
/// same province is not a border but the inside of a country, and is no part of either half.
///
/// The note under the board says what the shapes mean, because a player who cannot tell the
/// inside of a country from a border has not been told what they are looking at.
module Render =

    module Blocks =
        let powers = "The powers"
        let orders = "Your orders"
        let board = "The board"
        let sea = "At sea"
        let last = "Last time round"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let board =
            "A name between tildes - ~nth~ - is open water, where only a fleet can go. On one hex of every other province: the letter of whoever holds its supply centre, or a star where nobody does, and under it whatever is standing there - A for an army, F for a fleet, and the letter of the power it belongs to. A province whose centre is held is outlined in that power's colour."

        let orders =
            "Write an order for each of your units, then 'commit'. Nobody sees what you have written until every power has committed, and then everybody sees all of it at once."

        let borders =
            "A province takes as many hexes as it needs and is drawn as one shape with its name written across it. Two provinces share a side exactly when they border each other, and two drawn apart do not - the picture is the whole of it, so what you can see is what a piece can do. The one hole in it is Switzerland, which nothing may enter. 'borders vie' spells one province out in words."

        let press =
            "'press france ...' sends a line to one power and to nobody else. 'press all ...' sends it to the table. Nothing in the rules makes anybody keep their word."

    let nothingYet = "nothing yet"

    let private dash = "-"

    // --- who is playing -------------------------------------------------------------------------

    let heading beholder session =
        match session with
        | Finished(_, ending) -> $"The game is over: {Words.ending ending}"
        | InPlay play ->
            match Session.awaited play with
            | power :: _ ->
                let seat = Power.seatOf power
                $"{Words.phase play.Stage play.Year} - {Words.seated (seat = beholder) seat} to write"
            | [] -> Words.phase play.Stage play.Year

    /// Every power, with an arrow at whoever is to write and the reader's own marked: what it
    /// holds, what it has on the board, and where it has got to this phase.
    let private powers beholder session =
        let play = Session.play session
        let acting = Session.awaited play |> List.tryHead

        Power.all
        |> List.map (fun power ->
            let seat = Power.seatOf power
            let yours = seat = beholder
            let centres, units = Position.counts power play.Board

            let standing =
                if Position.isOut power play.Board then "out of the game"
                elif Set.contains power play.Adrift then "walked away"
                elif Session.isOver session then ""
                elif not (Session.hasSomethingToDo power play) then "nothing to do"
                elif Set.contains power play.Sealed then "committed"
                else "still writing"

            [ Scene.cell Tone.Yours (if Some power = acting && not (Session.isOver session) then ">" else "")
              Scene.cell (if yours then Tone.Yours else Tone.Slot(Ink.key power)) (Words.seated yours seat)
              Scene.cell Tone.Quiet $"{centres} centres"
              Scene.cell Tone.Quiet $"{units} units"
              Scene.cell Tone.Quiet standing ])
        |> Aligned

    // --- the board ------------------------------------------------------------------------------------

    let private regionName =
        function
        | TheIsles -> "The British Isles"
        | Iberia -> "Iberia"
        | TheLowCountries -> "The Low Countries"
        | FranceAnd -> "France"
        | GermanyAnd -> "Germany"
        | Scandinavia -> "Scandinavia"
        | RussiaAnd -> "Russia"
        | AustriaAnd -> "Austria and its marches"
        | ItalyAnd -> "Italy"
        | TheBalkans -> "The Balkans"
        | TurkeyAnd -> "Turkey"
        | Africa -> "Africa"
        | Waters -> Blocks.sea

    // --- the board drawn as a board ------------------------------------------------------------------

    /// Which hex of each province carries what is standing on it: the first one, in reading
    /// order.
    ///
    /// A province takes as many hexes as it needs its sides, so most of them take several - and
    /// a unit drawn in every hex of a province would look like four armies rather than one. So
    /// the name is written in every hex, which is what shows the region's shape, and everything
    /// else is written once.
    let private seatOfEach =
        Atlas.layout
        |> List.mapi (fun row (_, cells) -> cells |> List.mapi (fun step cell -> (row, step), cell))
        |> List.collect id
        |> List.fold
            (fun (met, seats) (where, cell) ->
                match cell with
                | Some province when not (Set.contains province met) -> Set.add province met, Set.add where seats
                | _ -> met, seats)
            (Set.empty, Set.empty)
        |> snd

    /// One province as a cell of the map: its three letters, and - on the one hex that carries
    /// them - who holds the centre and what is standing there.
    ///
    /// A `Patch` rather than a `Tile`, and the difference is the whole shape of the board. A
    /// tile is a cell with its own four walls; a patch is a piece of something bigger and says
    /// which - so the hexes of one province are drawn as one region with no walls inside it, and
    /// the wall round it takes the colour of whoever holds its centre. A province is a shape in
    /// somebody's colour rather than four boxes that happen to share a name.
    ///
    /// The shape is the province's code and not its owner's: Vienna and Budapest are both
    /// Austria's and the border between them is perfectly real.
    ///
    /// Two lines whether or not there is a unit, so that every cell is the same height and a row
    /// of them looks like a row rather than a broken comb.
    let private cell position carries province =
        let owner = Position.ownerOf province position
        let isCentre = Atlas.isCentre province
        let afloat = Atlas.terrainOf province = Sea

        let held =
            match owner with
            | Some power when isCentre -> Tone.Slot(Ink.key power)
            | _ -> Tone.Quiet

        let named =
            match owner, isCentre with
            | _ when afloat -> Tone.Slot Ink.Sea
            | Some power, true -> Tone.Slot(Ink.key power)
            | None, true -> Tone.Plainly
            | _ -> Tone.Quiet

        // The three letters, and after them who holds the centre - their own letter, or a star
        // where nobody does, or nothing at all where there is no centre to hold. Said in a
        // character rather than left to the colour, because the plainest reader drops every tone
        // it is given and a board that only says who owns what in colour says it to nobody there.
        //
        // Open water is written between tildes instead, on every hex of it rather than one: half
        // of what a player may do turns on whether a province is wet - a fleet lives out there
        // and an army may not go near it - and that is too much to leave to a colour, which the
        // plainest reader has none of. Nothing else can wear them: no sea on this board holds a
        // supply centre, so the character after a name is never asked to mean two things at once.
        let head =
            if afloat then
                "~" + Atlas.code province + "~"
            else
                Atlas.code province
                + (if not carries then
                       ""
                   else
                       match owner, isCentre with
                       | Some power, true -> Power.letter power
                       | None, true -> "*"
                       | _ -> "")

        let standing =
            match (if carries then Position.at province position else None) with
            | Some piece ->
                Say [ Span.toned (Tone.Slot(Ink.key piece.Power)) $"{Kind.letter piece.Kind} {Power.letter piece.Power}" ]
            | None -> Say [ Span.quiet "" ]

        Patch(Atlas.code province, held, [ Say [ Span.toned named head ]; standing ])

    /// The whole board as a honeycomb. Each row sits half a cell across from the one above, which
    /// is what gives a cell six sides to share; `Atlas.layout` says where everything lies and
    /// `Atlas.problems` refuses to deal a game if any side it draws between two provinces is not
    /// a real border.
    let private honeycomb position =
        Walled(
            4,
            Atlas.layout
            |> List.mapi (fun row (shift, cells) ->
                { Shift = shift
                  Cells =
                    cells
                    |> List.mapi (fun step ->
                        function
                        | Some province -> cell position (Set.contains (row, step) seatOfEach) province
                        // Nothing at all, which is what keeps two provinces that do not border
                        // from being drawn side by side. A hole in the map has no walls of its
                        // own: what is drawn round it is the coastline of whatever it is a hole
                        // in.
                        | None -> Blank) })
        )

    // --- what this power has been asked for ------------------------------------------------------------

    /// A control that types a line, captioned with the line it types. The page draws a button
    /// and a terminal draws the words, and both of them are showing the same order.
    let private types line = Does(line, line, Tone.Quiet)

    let private tile piece contents =
        Tile(Some(Words.piece piece), Tone.Slot(Ink.key piece.Power), contents)

    /// What this power still has to do, in the shape the phase asks for it.
    ///
    /// Three phases, three different things, and this is where the difference shows up on a
    /// screen. In a movement it is every unit and what it has been told; in a retreat it is the
    /// beaten and where they may go; in a winter it is a home centre with room in it or a unit
    /// that has to be given up.
    let private chores margins beholder play =
        let power = Power.atSeat beholder

        let written =
            play.Written
            |> Map.toList
            |> List.filter (fun (province, says) ->
                match says with
                | Builds _ -> Position.ownerOf province play.Board = power
                | _ ->
                    (Position.at province play.Board |> Option.map (fun piece -> piece.Power)) = power
                    || play.Beaten
                       |> List.exists (fun beaten -> beaten.From = province && Some beaten.Piece.Power = power))

        let orderFor province =
            written
            |> List.tryPick (fun (other, says) -> if other = province then Some says else None)

        match power with
        | None -> Blank
        | Some power ->

        let mine = Position.unitsOf power play.Board

        let laid rows =
            match rows with
            | [] -> Blank
            // Beside rather than a walled grid: the map below is the one grid on this screen, and
            // a cell wide enough to hold `bud s vie - tri` would blow it out to a page across.
            | rows -> Beside rows

        match play.Stage with
        | Moving _ ->
            let listed =
                mine
                |> List.map (fun piece ->
                    [ Scene.cell (Tone.Slot(Ink.key power)) (Words.piece piece)
                      Scene.cell
                          (match orderFor piece.Where.At with
                           | Some _ -> Tone.Plainly
                           | None -> Tone.Quiet)
                          (match orderFor piece.Where.At with
                           | Some says -> Words.saying says
                           | None -> dash) ])

            let waiting =
                mine
                |> List.filter (fun piece -> (orderFor piece.Where.At).IsNone)
                |> List.map (fun piece ->
                    let where = Atlas.code piece.Where.At

                    tile
                        piece
                        (types $"{where} hold"
                         :: (Atlas.reach piece.Kind piece.Where
                             |> List.map (fun into -> types $"{where} - {Words.spot into}"))
                         // Last, and not an order at all: the board draws no map, so the
                         // question "what can this actually reach" needs to be one press away
                         // rather than something a player has to know to type.
                         @ [ types $"borders {where}" ]))

            Stack
                [ (if List.isEmpty listed then Scene.quietly "nothing on the board" else Aligned listed)
                  laid waiting
                  Does("commit", "commit", Tone.Yours)
                  Scene.noted margins Notes.orders ]

        | Falling _ ->
            let beaten = play.Beaten |> List.filter (fun beaten -> beaten.Piece.Power = power)

            let waiting =
                beaten
                |> List.filter (fun beaten -> (orderFor beaten.From).IsNone)
                |> List.map (fun beaten ->
                    let where = Atlas.code beaten.From

                    tile
                        beaten.Piece
                        ((beaten.Options |> List.map (fun into -> types $"{where} - {Words.spot into}"))
                         @ [ types $"disband {where}" ]))

            Stack
                [ Aligned(
                      beaten
                      |> List.map (fun beaten ->
                          [ Scene.cell (Tone.Slot(Ink.key power)) (Words.piece beaten.Piece)
                            Scene.cell
                                Tone.Quiet
                                (match orderFor beaten.From with
                                 | Some says -> Words.saying says
                                 | None -> $"beaten out of {Words.province beaten.From}") ])
                  )
                  laid waiting
                  Does("commit", "commit", Tone.Yours) ]

        | Building ->
            let owing = Session.owed power play.Board

            let already =
                written
                |> List.map (fun (province, says) ->
                    [ Scene.cell (Tone.Slot(Ink.key power)) (Atlas.nameOf province)
                      Scene.cell Tone.Plainly (Words.saying says) ])

            let waiting =
                if owing > 0 then
                    Orders.buildable power play.Board
                    |> List.filter (fun home -> (orderFor home).IsNone)
                    |> List.map (fun home ->
                        let where = Atlas.code home

                        let ports =
                            if Atlas.terrainOf home <> Coastal then
                                []
                            else
                                match Atlas.coastsOf home with
                                | [] -> [ types $"build f {where}" ]
                                | coasts -> coasts |> List.map (fun coast -> types $"build f {where}/{Coast.code coast}")

                        Tile(Some(Atlas.nameOf home), Tone.Slot(Ink.key power), types $"build a {where}" :: ports))
                elif owing < 0 then
                    Position.unitsOf power play.Board
                    |> List.filter (fun piece -> (orderFor piece.Where.At).IsNone)
                    |> List.map (fun piece -> tile piece [ types $"disband {Atlas.code piece.Where.At}" ])
                else
                    []

            let says =
                if owing > 0 then $"{owing} to build"
                elif owing < 0 then $"{-owing} to give up"
                else "nothing owed"

            Stack
                [ Scene.quietly says
                  (if List.isEmpty already then Blank else Aligned already)
                  laid waiting
                  Does("commit", "commit", Tone.Yours) ]

    // --- what happened last time everybody moved ---------------------------------------------------------

    let private passing (was: Passing) =
        let orders =
            was.Reports
            |> List.map (fun entry ->
                let said, came = Words.report entry

                [ Scene.cell (Tone.Slot(Ink.key entry.Piece.Power)) (Power.letter entry.Piece.Power)
                  Scene.cell Tone.Plainly said
                  Scene.cell Tone.Quiet came ])

        let asides =
            [ for piece, into in was.Retreated -> Scene.cell Tone.Quiet $"{Words.piece piece} retreats to {Words.spot into}"
              for piece in was.Scattered -> Scene.cell Tone.Quiet $"{Words.piece piece} is disbanded"
              for piece in was.Built -> Scene.cell Tone.Quiet $"{Words.named piece} is raised"
              for piece in was.Removed -> Scene.cell Tone.Quiet $"{Words.piece piece} is given up"
              for centre, owner, _ in was.Changed -> Scene.cell Tone.Quiet $"{Words.province centre} to {Power.name owner}" ]

        Stack
            [ Scene.quietly (Words.phase was.Was was.Year)
              (if List.isEmpty orders then Blank else Aligned orders)
              (if List.isEmpty asides then Blank else Aligned(asides |> List.map List.singleton)) ]

    let private lastTime play =
        match play.Last with
        | [] -> Blank
        | passings -> Block(Blocks.last, passings |> List.map passing)

    // --- what a player may type -------------------------------------------------------------------------

    let private verbs =
        [ "vie - tri", "move; 'vie - stp/sc' names a coast"
          "bud s vie - tri", "support that move"
          "bud s vie", "support Vienna where it stands"
          "nth c lon - bel", "convoy an army over the water"
          "vie hold", "stand still"
          "cancel vie", "take that order back"
          "commit", "these orders are final"
          "build a vie, disband vie", "in a winter"
          "press france ...", "a word to one power, and to nobody else"
          "borders vie, where vie", "what a piece there can reach, and what is there"
          "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "resign", "walk away; your units stand and are worn down"
          "quit", "leave; the game is written down and 'replay' takes it up again" ]

    let commands =
        verbs
        |> List.map (fun (verb, says) -> $"  %-24s{verb} %s{says}")
        |> String.concat "\n"

    let help =
        String.concat
            "\n"
            [ "Diplomacy: seven powers, thirty-four supply centres, and no dice at all."
              ""
              "Hold eighteen centres and you have won. Every power writes its orders in secret and"
              "they are all carried out at once, so nothing on this board is decided by luck and"
              "almost nothing is decided alone: one unit beats one unit, and the way anything is"
              "ever taken is a second unit standing behind the first."
              ""
              "THE YEAR"
              "  Spring, then autumn, and the centres are counted after the autumn. A season where"
              "  something was pushed out of its province is followed by retreats; a year that"
              "  changed hands is followed by a winter of builds. Both are skipped when there is"
              "  nothing in them."
              ""
              "THE ORDERS"
              "  A unit may hold, move to a province beside it, support another unit holding or"
              "  moving, or - a fleet at sea - convoy an army from one coast to another."
              "  A support is cut by any attack on the unit giving it. A power can never push its"
              "  own unit out of a province, and cannot help anybody else do it either."
              ""
              "  Armies walk the land. Fleets sail the seas and the coasts, and cannot cross a land"
              "  border: a fleet in Rome may sail to Naples but not to Venice. Spain, Bulgaria and"
              "  St Petersburg have two coasts each, and a fleet has to say which."
              ""
              "TALKING"
              "  'press france ...' is read by France and by nobody else; 'press all ...' by the"
              "  table. Everybody is told that a message went, and nobody else is told what was in"
              "  it. Nothing in the rules makes anybody keep a promise, and that is the game."
              ""
              "COMMANDS"
              commands ]

    // --- the log -------------------------------------------------------------------------------------------

    let private wordsFor beholder =
        Told.inWords (Words.saidTo beholder) Words.command

    let private log beholder (model: Model<Move, Session, Notice>) =
        match model.Log with
        | [] -> [ Scene.quietly nothingYet ]
        | notices -> notices |> List.rev |> List.map (wordsFor beholder >> Scene.says)

    // --- the whole screen ------------------------------------------------------------------------------------

    let board margins beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model
        let play = Session.play session
        let position = play.Board

        Stack
            [ Heading(heading beholder session)
              Beside
                  [ Block(Blocks.powers, [ powers beholder session ])
                    Block(Blocks.orders, [ chores margins beholder play ]) ]
              Block(
                  Blocks.board,
                  [ honeycomb position
                    Scene.noted margins Notes.board
                    Scene.noted margins Notes.borders ]
              )
              lastTime play
              Scene.listing margins Blocks.commands commands
              Block(Blocks.log, log beholder model) ]

    // --- the record ----------------------------------------------------------------------------------------

    /// One line of the record, as much of it as this seat may read.
    ///
    /// The record is the whole truth of the game and a player is not owed the whole truth while
    /// it is still being played. An order written this phase is nobody's business until the
    /// phase resolves, and a word sent to one power is sent to one power for good - so the
    /// record shows that a thing was done and, where it may not say what, says that too.
    let private askedFor beholder current (entry: Entry<Move, Notice>) =
        match entry.Asked with
        | Make(Whisper(Some heard, _)) when entry.Actor <> beholder && Power.seatOf heard <> beholder ->
            $"press {Power.key heard} ..."
        | Make(Give(at, _)) when entry.Actor <> beholder && entry.Turn = current -> $"an order for {Atlas.code at}"
        | msg -> Words.command msg

    let history beholder (model: Model<Move, Session, Notice>) =
        let current = Session.turn (Model.state model)

        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  turn {entry.Turn}"
              Scene.cell (Tone.Slot(Ink.key (Power.atSeat entry.Actor |> Option.defaultValue Austria))) (Words.player entry.Actor)
              Scene.cell Tone.Plainly (askedFor beholder current entry)
              Scene.cell Tone.Quiet (entry.Told |> List.map (wordsFor beholder) |> String.concat " ") ]

        match Journal.entries model.Journal with
        | [] -> Block("The record", [ Scene.quietly nothingYet ])
        | entries ->
            Block(
                "The record",
                [ Aligned(entries |> List.map entry)
                  Scene.quietly (heading beholder (Model.state model)) ]
            )

    // --- the one thing this game can be asked -------------------------------------------------------------------

    /// `borders vie`, `where vie`, `orders`.
    ///
    /// This is what `View.Answer` was put there for, and this game fills it the way the game it
    /// was written for does. The board deliberately shows no picture of the map, so the question
    /// "what can this piece actually reach" has to have an answer - and it comes out of the same
    /// table the adjudicator walks, so it cannot be out of date and cannot be wrong.
    let answer (asked: string) (model: Model<Move, Session, Notice>) =
        let play = Session.play (Model.state model)

        let named word =
            match Atlas.byWord word with
            | Some province -> Ok province
            | None -> Error $"'{word}' is not a province."

        // Written out rather than said line by line, and for the same reason the other game's
        // answer is: this lands beside the board rather than on it, and it is a column of
        // related lines that have to stay in the order and the shape they were written in.
        let written title lines =
            Block(title, [ Written(String.concat "\n" lines) ])

        let borders province =
            let byLand = Atlas.armyReach province |> List.map Atlas.nameOf |> List.sort

            let bySea =
                match Atlas.coastsOf province with
                | [] -> [ ("", Atlas.fleetReach { At = province; Coast = None }) ]
                | coasts ->
                    coasts
                    |> List.map (fun coast -> Coast.name coast, Atlas.fleetReach { At = province; Coast = Some coast })

            written
                (Atlas.nameOf province)
                [ $"{Atlas.code province} - {Words.province province}"
                  (match Atlas.terrainOf province with
                   | Inland -> "Landlocked: no fleet ever stands here."
                   | Coastal -> "A coast: an army or a fleet may stand here."
                   | Sea -> "Open sea: fleets only.")
                  ""
                  (match byLand with
                   | [] -> "An army can reach nowhere from here."
                   | places -> "An army can reach " + String.concat ", " places + ".")
                  ""
                  for coast, reach in bySea do
                      // Named in full rather than in the three letters an order uses. This is
                      // the screen somebody reads when they are not sure, and "Gulf of Lyon"
                      // answers that where "gol" only repeats the question.
                      match reach |> List.map Words.place |> List.sort with
                      | [] -> "A fleet can reach nowhere from here."
                      | places ->
                          let from = if coast = "" then "" else $" from the {coast}"
                          $"""A fleet can reach{from} {String.concat ", " places}.""" ]

        let standing province =
            written
                (Atlas.nameOf province)
                [ $"{Atlas.code province} - {Words.province province}, in {regionName (Atlas.regionOf province)}."
                  ""
                  (match Position.at province play.Board with
                   | Some piece -> $"{Words.named piece} stands in {Words.province province}."
                   | None -> $"Nothing stands in {Words.province province}.")
                  ""
                  (match Atlas.centreOf province, Position.ownerOf province play.Board with
                   | NotACentre, _ -> "It is not a supply centre."
                   | Home power, Some owner when owner = power -> $"A home centre of {Power.name power}, still held by it."
                   | Home power, Some owner -> $"A home centre of {Power.name power}, held by {Power.name owner}."
                   | Home power, None -> $"A home centre of {Power.name power}, held by nobody."
                   | Neutral, Some owner -> $"A supply centre, held by {Power.name owner}."
                   | Neutral, None -> "A supply centre nobody holds.") ]

        let lost why =
            written
                Blocks.board
                [ why
                  ""
                  "Ask 'borders vie' for what a piece in Vienna could reach, or 'where vie' for what is standing there." ]

        match Commands.words (asked.ToLowerInvariant()) with
        | [ "borders"; word ] ->
            match named word with
            | Ok province -> borders province
            | Error why -> lost why
        | [ "where"; word ] ->
            match named word with
            | Ok province -> standing province
            | Error why -> lost why
        | _ -> lost "That is not a question this game knows."

    let rules = Block("The rules", [ Written help ])

    // --- a table still filling up -------------------------------------------------------------------------------

    let waiting = Scene.waiting Words.seated

    // --- and what this game brings to a page --------------------------------------------------------------------

    /// This game's own rules of drawing, and no more than that.
    ///
    /// Two sizes, because this game has two kinds of cell. The map is sixteen columns of three
    /// letters and wants them small; a unit's orders are whole lines like `bud s vie - tri` and
    /// want room. `--cell` belongs to the grid, and the map is the only grid here, so the order
    /// tiles sit in a row of their own and are sized by what is in them.
    let private sheet =
        """
.grid { --cell: 3.4rem; }
.grid .tile { padding: .15rem .2rem; font-size: .78rem; line-height: 1.15; }
.beside .tile { min-width: 9rem; }
.tile h3 { font-size: 0.95rem; }
.beside { align-items: flex-start; }
"""

    let shell =
        { Title = "Diplomacy"
          Sheet = sheet
          Placeholder = "an order - 'vie - tri', 'bud s vie - tri' - then 'commit'. Or 'help'." }
