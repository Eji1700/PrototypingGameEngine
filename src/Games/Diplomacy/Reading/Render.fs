namespace TCModel.Diplomacy

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses - `Open`.
open TCModel.Diplomacy

/// Every screen this game has, described once.
///
/// **There is no picture of the map, and that is a decision rather than an omission.** The
/// other game of maps here draws one, and can: its borders are a patch of a triangular lattice,
/// so the honeycomb it prints *is* the border table and cannot disagree with it. This board is
/// not a lattice and never was. Any grid of seventy-five provinces would put pairs side by side
/// that share no border and pull apart pairs that do, and a player would read the picture -
/// there is no other reason to draw one. A board that lies about where the armies can walk is
/// worse than no board.
///
/// So what is drawn is what is actually true: every supply centre and who holds it, every unit
/// and where it stands, grouped the way people who play this talk about the map. The borders
/// are one question away - `borders vie` - and that answer comes straight out of the table the
/// rules use.
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
            "A letter beside a province is the power that owns the centre; a dot is a centre nobody holds. The letter under it is the unit standing there - A for an army, F for a fleet."

        let orders =
            "Write an order for each of your units, then 'commit'. Nobody sees what you have written until every power has committed, and then everybody sees all of it at once."

        let borders =
            "Provinces are grouped by where they are, not by what borders what. 'borders vie' says what a piece in Vienna can reach."

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

    /// One province: what it is called, who owns it if it is worth owning, and what is standing
    /// in it. A province that is neither a centre nor occupied is not drawn at all - there are
    /// forty-one of those and nothing is happening in any of them.
    let private row (position: Position) (province: ProvinceId) =
        let owner =
            match Atlas.centreOf province with
            | NotACentre -> Scene.cell Tone.Quiet ""
            | _ ->
                match Position.ownerOf province position with
                | Some power -> Scene.cell (Tone.Slot(Ink.key power)) (Power.letter power)
                | None -> Scene.cell Tone.Quiet "."

        let unit =
            match Position.at province position with
            | Some piece ->
                Scene.cell
                    (Tone.Slot(Ink.key piece.Power))
                    $"{Kind.letter piece.Kind} {Power.letter piece.Power}"
            | None -> Scene.cell Tone.Quiet ""

        [ Scene.cell Tone.Plainly (Atlas.nameOf province); owner; unit ]

    let private worthDrawing position province =
        Atlas.isCentre province || Position.occupied province position

    let private regionBlock position region =
        let places =
            Atlas.all
            |> List.filter (fun province -> province.Region = region && worthDrawing position province.Id)
            |> List.map (fun province -> province.Id)

        match places with
        | [] -> Blank
        | places -> Block(regionName region, [ Aligned(places |> List.map (row position)) ])

    /// The land, in three columns of regions. Which region a province is in is nothing the rules
    /// know about; it is how everybody who plays this talks about the board, and a board grouped
    /// the way it is talked about is a board that can be read at a glance.
    let private mainland position =
        let column regions =
            Stack(regions |> List.map (regionBlock position))

        Beside
            [ column [ TheIsles; FranceAnd; Iberia; TheLowCountries ]
              column [ GermanyAnd; Scandinavia; RussiaAnd; Africa ]
              column [ AustriaAnd; ItalyAnd; TheBalkans; TurkeyAnd ] ]

    /// The waters, and only the ones with something in them. Nineteen empty seas drawn every
    /// turn would be nineteen lines of nothing.
    let private seas position =
        match Atlas.all |> List.filter (fun province -> province.Terrain = Sea && Position.occupied province.Id position) with
        | [] -> Blank
        | busy ->
            Block(
                Blocks.sea,
                [ Aligned(busy |> List.map (fun province -> row position province.Id)) ]
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
    let private chores notes beholder play =
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
            written |> List.tryPick (fun (other, says) -> if other = province then Some says else None)

        match power with
        | None -> Blank
        | Some power ->

        let mine = Position.unitsOf power play.Board

        let laid rows =
            match rows with
            | [] -> Blank
            | rows -> Walled(20, [ Scene.squared rows ])

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
                  Scene.noted notes Notes.orders ]

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

                        Tile(
                            Some(Atlas.nameOf home),
                            Tone.Slot(Ink.key power),
                            types $"build a {where}" :: ports
                        ))
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
            [ for piece, into in was.Retreated ->
                  Scene.cell Tone.Quiet $"{Words.piece piece} retreats to {Words.spot into}"
              for piece in was.Scattered -> Scene.cell Tone.Quiet $"{Words.piece piece} is disbanded"
              for piece in was.Built -> Scene.cell Tone.Quiet $"{Words.named piece} is raised"
              for piece in was.Removed -> Scene.cell Tone.Quiet $"{Words.piece piece} is given up"
              for centre, owner, _ in was.Changed ->
                  Scene.cell Tone.Quiet $"{Words.province centre} to {Power.name owner}" ]

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
          "notes", "hide this and every note"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "resign", "walk away; your units stand and are worn down"
          "quit", "leave, saving first" ]

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

    let private wordsFor beholder = Told.inWords (Words.saidTo beholder) Words.command

    let private log beholder (model: Model<Move, Session, Notice>) =
        match model.Log with
        | [] -> [ Scene.quietly nothingYet ]
        | notices -> notices |> List.rev |> List.map (wordsFor beholder >> Scene.says)

    // --- the whole screen ------------------------------------------------------------------------------------

    let board notes beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model
        let play = Session.play session
        let position = play.Board

        Stack
            [ Heading(heading beholder session)
              Beside
                  [ Block(Blocks.powers, [ powers beholder session ])
                    Block(Blocks.orders, [ chores notes beholder play ]) ]
              Block(Blocks.board, [ mainland position; Scene.noted notes Notes.board; Scene.noted notes Notes.borders ])
              seas position
              lastTime play
              Block(Blocks.commands, [ Written commands ])
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
        let written title lines = Block(title, [ Written(String.concat "\n" lines) ])

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
                [ (match Position.at province play.Board with
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
            written Blocks.board [ why; ""; "Ask 'borders vie' for what a piece in Vienna could reach, or 'where vie' for what is standing there." ]

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

    /// This game's own rules of drawing, and no more than that. Cells hold a whole order rather
    /// than a glyph, so they want to be wide and short where the game of nine squares wanted
    /// them square.
    let private sheet =
        """
.grid { --cell: 11rem; }
.tile h3 { font-size: 0.95rem; }
.beside { align-items: flex-start; }
"""

    let shell =
        { Title = "Diplomacy"
          Sheet = sheet
          Placeholder = "an order - 'vie - tri', 'bud s vie - tri' - then 'commit'. Or 'help'." }
