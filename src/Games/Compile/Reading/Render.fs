namespace TCModel.Compile

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Compile

/// Every screen this game has, described once.
///
/// The board is the one thing here that is really this game's: two players sit opposite each
/// other, so a line is read from the far side of the table inwards - their stack, the two
/// protocols meeting in the middle, and then yours. A `Scene` says that much and nothing about
/// what it looks like; `Readers` turns it into text, into panels and into a page, and the
/// three cannot come to disagree about which way round the table is.
module Render =

    /// What each part of the screen is called. Named rather than written out, because the
    /// readers draw them and a block renamed in one place would look like a block that had
    /// gone missing.
    module Blocks =
        let draft = "The draft"
        let protocols = "Your protocols"
        let field = "The field"
        let players = "Players"
        let hand = "Your hand"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let draft =
            $"Twelve protocols, no duplicates, and the picks go 1-2-2-1: one to Player 1, two to Player 2, two back to Player 1, one back to Player 2. Say a protocol to take it - 'fire'."

        let arranging =
            $"Your {Protocol.Each} protocols go against the {Lines.Count} lines, one each, in the order you say them. Both players do it, and the lines they make are read across the table from each other."

        let field =
            "A line runs across the table: their stack, the two protocols that meet in the middle of it, and yours. A stack grows away from the line, so the card most recently played is the one nearest whoever played it - furthest up for them, furthest down for you."

        let hand =
            "Say a card and a line to play it - 'fire-3 2'. Cards are written as a protocol, a dash, and the number on the card."

    let nothingYet = "nothing yet"

    // --- the heading -------------------------------------------------------------------------

    /// Whose turn it is and what they are being asked for, or how it ended. Every screen opens
    /// with this line, and at a game of three stages it is carrying more than usual: which
    /// stage it is, is a thing a player has to be told rather than left to infer from what is
    /// on the board.
    let heading beholder session =
        let seat = Session.active session
        let yours = seat = beholder
        let who = Words.seated yours seat

        match session.Stage with
        | Drafting _ -> $"The draft, pick {Session.picksMade session + 1} of {Draft.Picks} - {who} to choose a protocol"
        | Arranging -> $"Protocols - {who} to set theirs against the lines"
        | Playing -> $"Turn {session.Turn} - {who} to play"
        | Done ending -> $"The game is over: {Words.ending ending}"

    // --- the draft ---------------------------------------------------------------------------

    /// What is left of the twelve, each one a control that types its own name - so what a
    /// player clicks and what a player could have typed are one thing said once.
    let private pool protocols =
        match protocols with
        | [] -> Scene.quietly "none left"
        | protocols ->
            Walled(
                12,
                protocols
                |> List.chunkBySize 4
                |> List.map (fun row ->
                    Scene.squared (
                        row
                        |> List.map (fun protocol ->
                            Tile(None, Tone.Quiet, [ Does(Protocol.name protocol, $"draft {Protocol.key protocol}", Tone.Plainly) ]))
                    ))
            )

    // --- setting the protocols against the lines ---------------------------------------------

    /// Every order three protocols can be put in. Six of them, which is few enough to offer
    /// whole rather than asking somebody to think of one.
    let private orders protocols =
        let rec walk items =
            match items with
            | [] -> [ [] ]
            | _ ->
                items
                |> List.collect (fun one -> walk (items |> List.filter ((<>) one)) |> List.map (fun rest -> one :: rest))

        walk protocols

    let private arranging beholder session =
        let side = Session.side beholder session

        if Session.active session <> beholder || not (List.isEmpty side.Order) then
            Scene.quietly (
                match side.Order with
                | [] -> "waiting for the other player"
                | order -> $"yours are set: {Words.order order}"
            )
        else
            Walled(
                26,
                orders side.Drafted
                |> List.chunkBySize 2
                |> List.map (fun row ->
                    Scene.squared (
                        row
                        |> List.map (fun order ->
                            let caption = order |> List.map Protocol.name |> String.concat " / "
                            let typed = order |> List.map Protocol.key |> String.concat " "
                            Tile(None, Tone.Quiet, [ Does(caption, $"arrange {typed}", Tone.Plainly) ]))
                    ))
            )

    // --- the field ---------------------------------------------------------------------------

    let private stack tone cards =
        match cards with
        | [] -> Tile(None, Tone.Quiet, [ Scene.quietly "-" ])
        | cards -> Tile(None, tone, cards |> List.map (fun card -> Say [ Span.toned tone (Card.name card) ]))

    /// The middle of a line: the two protocols that meet there, theirs above yours, in the
    /// colours of whoever they belong to.
    let private middle theirs yours theirTone yourTone line =
        let facing side =
            Side.protocolOn line side |> Option.map Protocol.name |> Option.defaultValue "not set"

        Tile(
            Some $"Line {line}",
            Tone.Quiet,
            [ Say [ Span.toned theirTone (facing theirs) ]
              Say [ Span.toned yourTone (facing yours) ] ]
        )

    /// The table as the reader is sitting at it: the other player's half across from them,
    /// the protocols meeting in the middle, and their own half nearest.
    ///
    /// The two halves are read in opposite directions, and that is not a flourish. A stack is
    /// a real pile on a real table growing away from the line it was played to, so the newest
    /// card is the one nearest whoever played it - which on a screen with one player above the
    /// line and the other below means the far stack is listed newest first and the near one
    /// newest last. Drawing both the same way round would put one player's freshest card at
    /// the other end of the table from them.
    let private field beholder session =
        let them = Session.other beholder
        let theirs = Session.side them session
        let yours = Session.side beholder session
        let theirTone = Tone.Slot(Ink.key them)
        let yourTone = Tone.Slot(Ink.key beholder)

        Walled(
            18,
            [ Scene.squared (Lines.all |> List.map (fun line -> stack theirTone (Side.stack line theirs)))
              Scene.squared (Lines.all |> List.map (middle theirs yours theirTone yourTone))
              Scene.squared (Lines.all |> List.map (fun line -> stack yourTone (Side.stack line yours |> List.rev))) ]
        )

    // --- who is playing, and what they are holding --------------------------------------------

    /// Both seats, with an arrow at whoever is to act, and the three counts that are the whole
    /// of what one player may know about the other's cards: how many are left, how many are
    /// gone, and how many are in hand. What those cards *are* is not here, and that is the
    /// point of counting them.
    ///
    /// Before the deal there are no cards to count, so it says what each has taken instead. A
    /// row of three zeroes is not a fact about the game - it is a screen drawn for a stage it
    /// is not in.
    let private players beholder session =
        let acting = Session.active session

        // Whether there are cards at all, asked of the cards rather than of the stage: a game
        // given up during the draft is `Done` with nothing dealt, and counting that as play
        // would draw three zeroes under a game that never had a deck.
        let dealt =
            Session.seats
            |> List.exists (fun seat ->
                let side = Session.side seat session
                not (List.isEmpty side.Deck && List.isEmpty side.Hand && List.isEmpty side.Discard))

        Session.seats
        |> List.map (fun seat ->
            let side = Session.side seat session
            let yours = seat = beholder

            [ Scene.cell Tone.Yours (if seat = acting && not (Session.isOver session) then "->" else "")
              Scene.cell (if yours then Tone.Yours else Tone.Slot(Ink.key seat)) (Words.seated yours seat)

              if dealt then
                  Scene.cell Tone.Quiet $"deck {List.length side.Deck}"
                  Scene.cell Tone.Quiet $"discard {List.length side.Discard}"
                  Scene.cell Tone.Quiet $"hand {List.length side.Hand}"
              else
                  Scene.cell
                      Tone.Quiet
                      (match side.Drafted with
                       | [] -> "nothing drafted yet"
                       | drafted -> Words.order drafted) ])
        |> Aligned

    // --- the hand ----------------------------------------------------------------------------

    /// The reader's own cards, and only ever theirs. Each is a cell carrying the three lines
    /// it could go to, because a card and a line together are one move and a control has to
    /// carry the whole of one.
    let private hand beholder session =
        let side = Session.side beholder session
        let tone = Tone.Slot(Ink.key beholder)

        match side.Hand |> List.sortBy (fun card -> Protocol.name card.Protocol, card.Value) with
        | [] -> Scene.quietly "nothing in hand"
        | cards ->
            Walled(
                14,
                cards
                |> List.chunkBySize 5
                |> List.map (fun row ->
                    Scene.squared (
                        row
                        |> List.map (fun card ->
                            Tile(
                                Some(Card.name card),
                                tone,
                                Lines.all
                                |> List.map (fun line -> Does($"-> {line}", $"play {Card.key card} {line}", Tone.Plainly))
                            ))
                    ))
            )

    // --- what a player may type ----------------------------------------------------------------

    /// The commands, short. `help` has them at length, and both are written from this list so
    /// neither can quietly grow a command the other has never heard of.
    let private verbs =
        [ "fire", "draft the Fire protocol (or 'draft fire')"
          "water dark fire", "set your three against lines 1, 2 and 3 (or 'arrange ...')"
          "fire-3 2", "play the card Fire-3 to line 2 (or 'play fire-3 2')"
          "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide this and every note"
          "view <name>", "draw the field another way"
          "save", "write the record now"
          "help", "every command, at length"
          "resign", "give the game up, but write it down"
          "quit", "leave; the game is written down and 'replay' takes it up again" ]

    let commands =
        verbs
        |> List.map (fun (verb, says) -> $"  %-18s{verb} %s{says}")
        |> String.concat "\n"

    let help =
        String.concat
            "\n"
            [ "Compile, for two players sitting opposite each other."
              ""
              "THE DRAFT"
              Notes.draft
              ""
              "THE PROTOCOLS"
              Notes.arranging
              ""
              "THE FIELD"
              Notes.field
              $"Each protocol has {Card.PerProtocol} cards, so a deck is {Deck.Size}, and {Deck.HandSize} are drawn to open."
              Notes.hand
              ""
              "COMMANDS"
              commands ]

    // --- the log ------------------------------------------------------------------------------

    /// What the game has said lately, oldest first.
    let wording = Told.inWords Words.said Words.command

    let private log (model: Model<Move, Session, Notice>) =
        match model.Log with
        | [] -> [ Scene.quietly nothingYet ]
        | notices -> notices |> List.rev |> List.map (wording >> Scene.says)

    // --- the whole screen -----------------------------------------------------------------------

    /// One screen, and which parts of it there are depends on the stage: a draft has no field
    /// worth drawing and a hand nobody has been dealt, and a screen padded out with empty
    /// blocks is a screen that has to be read past.
    let board notes beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        let stage =
            match session.Stage with
            | Drafting left -> Block(Blocks.draft, [ pool left; Scene.noted notes Notes.draft ])
            | Arranging -> Block(Blocks.protocols, [ arranging beholder session; Scene.noted notes Notes.arranging ])
            | Playing
            | Done _ -> Block(Blocks.hand, [ hand beholder session; Scene.noted notes Notes.hand ])

        let table =
            match session.Stage with
            | Drafting _ -> Blank
            | Arranging
            | Playing
            | Done _ -> Block(Blocks.field, [ field beholder session; Scene.noted notes Notes.field ])

        Stack
            [ Heading(heading beholder session)
              table
              Beside [ stage; Block(Blocks.players, [ players beholder session ]) ]
              Block(Blocks.commands, [ Written commands ])
              Block(Blocks.log, log model) ]

    // --- the rest of what a player reads --------------------------------------------------------

    let history beholder (model: Model<Move, Session, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  turn {entry.Turn}"
              Scene.cell Tone.Plainly $"{Words.player entry.Actor}: {Words.command entry.Asked}"
              Scene.cell Tone.Plainly (entry.Told |> List.map wording |> String.concat " ") ]

        match Journal.entries model.Journal with
        | [] -> Block("The record", [ Scene.quietly nothingYet ])
        | entries ->
            Block(
                "The record",
                [ Aligned(entries |> List.map entry)
                  Scene.quietly (heading beholder (Model.state model)) ]
            )

    /// What is being asked for right now, and why. A game of three stages is the one place
    /// this question has a real answer: the same board takes a protocol at one moment and a
    /// card at the next, and nothing on it says which.
    let answer _ (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        let said =
            match session.Stage with
            | Drafting left ->
                let names = left |> List.map Protocol.key |> String.concat ", "

                [ Notes.draft
                  $"{Draft.Picks - Session.picksMade session} of the {Draft.Picks} picks are still to come, out of these: {names}." ]
            | Arranging -> [ Notes.arranging ]
            | Playing -> [ Notes.field; Notes.hand ]
            | Done ending -> [ $"Nothing: {Words.ending ending}." ]

        Block("What is being asked", said |> List.map Scene.says)

    let rules = Block("The rules", [ Written help ])

    // --- a table still filling up -----------------------------------------------------------

    let waiting = Scene.waiting Words.seated

    // --- what this game brings to a page -----------------------------------------------------

    /// This game's own rules of drawing, and no more than that: a stack is a column of card
    /// names rather than a single glyph, so its cells want to be wider and shorter than a
    /// board of squares would ask for.
    let private sheet =
        """
.grid { --cell: 9rem; }
"""

    let shell =
        { Title = "Compile"
          Sheet = sheet
          Placeholder = "a protocol to draft it, or a card and a line - 'fire-3 2', or 'help'" }
