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
        let choosing = "Waiting on an answer"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let draft =
            $"Twelve protocols, no duplicates, and the picks go 1-2-2-1: one to Player 1, two to Player 2, two back to Player 1, one back to Player 2. Say a protocol to take it - 'fire'."

        let arranging =
            $"Your {Protocol.Each} protocols go against the {Lines.Count} lines, one each, in the order you say them. Both players do it, and the lines they make are read across the table from each other."

        let field =
            "A line runs across the table: their stack, the two protocols that meet in the middle of it, and yours. A stack grows away from the line, so the card most recently played is the one nearest whoever played it - furthest up for them, furthest down for you."

        let control =
            $"The control component: at the start of your turn, leading {Field.LanesForControl} lanes - strictly, a tie is no lead - takes it, out of the middle or off the other player. Holding it costs you something every time you compile or refresh: your {Protocol.Each} protocols have to move first, into a different order. The stacks do not move with them, so a line built patiently for one protocol can end up compiling another."

        let text =
            "A card played face up does what is printed on it, one command at a time - and between any two of them the table is looked at again, because a command that turned a card face up has put that card's own text in front of whatever was waiting. A command with nothing to point at simply finds nothing to do, and the one after it still happens. A command with one thing to point at does it; with several, the game stops and asks - sometimes of the player whose turn it is not, and then nothing at all moves until they answer."

        let refreshing =
            $"Nothing is drawn at the end of a turn. 'refresh' puts your whole hand down and takes {Deck.HandSize} up, and it costs the turn - so {Deck.HandSize} cards is {Deck.HandSize} turns of tempo, and the turn you spend getting more is a turn they spend getting closer to {Stack.ToCompile}. With an empty hand it is the only thing left to do. A deck that runs out is shuffled from its own discard."

        let compiling =
            $"At the start of your turn, every line where you have {Stack.ToCompile} or more and strictly more than they do is compiled: the protocol facing it is turned over and the whole line goes, both players' cards alike. 'ready' beside a stack means it will compile next time that player's turn begins - which is one turn to answer it, and {Placed.FaceDownValue} face down is enough if it makes the scores level. Compile all {Protocol.Each} of your protocols and you win."

        let hand =
            $"Say a card and a line to play it - 'fire-3 2'. Face up it is worth the number printed on it and may only go where its protocol is - either player's. Face down - 'fire-3 2 down' - it is worth {Placed.FaceDownValue} and goes anywhere, so a card you cannot use is never a dead card."

    let nothingYet = "nothing yet"

    /// How much room a card's text is given, and how wide a cell holding one asks to be.
    ///
    /// The one measurement on this screen, because everything on it is a card: a stack in the
    /// field and a card in hand are the same thing printed in two places, and a board where
    /// they were two different widths would be two boards. The text is broken to `Room` here
    /// rather than left to whatever is drawing it - a reader that lays a screen out by counting
    /// characters gives a cell the width of its widest line, and one long sentence would push
    /// every column on the board out to the width of that sentence.
    [<Literal>]
    let private Room = 26

    [<Literal>]
    let private Across = 28

    /// How many cards stand side by side. Three, because there are three lines and the hand
    /// reads better under a field it lines up with.
    [<Literal>]
    let private PerRow = 3

    /// A card's text as lines that will fit a cell, quietly - it is what the card says rather
    /// than something the game is saying now.
    let private inSmall said =
        said |> List.collect (Scene.wrap Room) |> List.map Scene.quietly

    // --- the heading -------------------------------------------------------------------------

    /// Whose turn it is and what they are being asked for, or how it ended. Every screen opens
    /// with this line, and at a game of three stages it is carrying more than usual: which
    /// stage it is, is a thing a player has to be told rather than left to infer from what is
    /// on the board.
    let heading beholder session =
        let seat = Session.active session
        let yours = seat = beholder
        let who = Words.seated yours seat

        match Session.asking session, session.Stage with
        | Some asked, _ ->
            let whoAsks =
                match asked.Because with
                | Some source -> Card.name source.Saying
                | None -> "The control component"

            $"Turn {session.Turn} - {whoAsks} is waiting on {who}"
        | None, Drafting _ ->
            $"The draft, pick {Session.picksMade session + 1} of {Draft.Picks} - {who} to choose a protocol"
        | None, Arranging -> $"Protocols - {who} to set theirs against the lines"
        | None, Playing -> $"Turn {session.Turn} - {who} to play"
        | None, Done ending -> $"The game is over: {Words.ending ending}"

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

    /// One stack, with everything in it saying as much as it is still allowed to say.
    ///
    /// `reading` is how a card is put into words for whoever is looking, which is the only
    /// thing that differs between the two halves of the table: a card face down is a two to the
    /// player across from it and a card they played to the player who played it.
    ///
    /// `newestLast` is which way up the pile is drawn, and it has to be settled here rather
    /// than by whoever asks, because a stack is held newest first and that is what says which
    /// card is uncovered. A caller that reversed the list before handing it over would be
    /// handing over a pile whose top card is at the wrong end, and every card in it would then
    /// be printed with the wrong half of its text.
    let private stack tone reading newestLast cards =
        match cards with
        | [] -> Tile(None, Tone.Quiet, [ Scene.quietly "-" ])
        | cards ->
            let printed =
                cards
                |> List.mapi (fun depth placed -> reading placed, Words.saying (depth = 0) placed)

            let laid = if newestLast then List.rev printed else printed

            // A blank line between two cards where either of them is talking, and none at all
            // where the stack is a plain list of names. Without it a card's text reads as
            // though it belonged to whichever name it happens to sit under - which in a pile is
            // the card above, and is the one card it never belongs to.
            Tile(
                None,
                tone,
                laid
                |> List.mapi (fun depth (name, said) ->
                    [ if depth > 0 && not (List.isEmpty said && List.isEmpty (snd laid[depth - 1])) then
                          Scene.quietly " "
                      Say [ Span.toned tone name ]
                      yield! inSmall said ])
                |> List.concat
            )

    /// The middle of a line: the two protocols that meet there, theirs above yours, in the
    /// colours of whoever they belong to.
    ///
    /// Theirs is kept back until both are laid out. A card may be played face up against
    /// *either* protocol on a line, so an order seen before you have chosen your own is worth a
    /// great deal - which is why they go down face down and turn over together, and why this is
    /// the one thing on this board drawn differently for the two people reading it.
    let private middle field (them, theirs) (you, yours) theirTone yourTone shown line =
        let facing side =
            Side.protocolOn line side |> Option.map Protocol.name |> Option.defaultValue "not set"

        let theirFacing =
            if shown then facing theirs
            elif List.isEmpty theirs.Order then "not set"
            else Words.hidden

        // What each stack is worth, beside the protocol it is being played towards, and where
        // that leaves the line. The one piece of arithmetic in this game, so the board does it
        // rather than the player - and "ready" is the whole of the warning a stack at ten gives
        // the player who has to answer it.
        let against name tone seat side =
            let standing =
                if Field.won seat line field then "  ready"
                elif Side.protocolOn line side |> Option.exists (fun protocol -> Side.hasCompiled protocol side) then
                    "  done"
                else
                    ""

            Say
                [ Span.toned tone name
                  Span.quiet $"  {Field.valueOn seat line field}{standing}" ]

        Tile(
            Some $"Line {line}",
            Tone.Quiet,
            [ against theirFacing theirTone them theirs
              against (facing yours) yourTone you yours ]
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

        // Everything is on the table from the moment both orders are turned over, which is the
        // moment the settling ends.
        let shown = Session.doing session <> TheProtocols

        Walled(
            Across,
            [ Scene.squared (
                  Lines.all
                  |> List.map (fun line -> stack theirTone Words.faceless false (Side.stack line theirs))
              )
              Scene.squared (
                  Lines.all
                  |> List.map (middle session.Field (them, theirs) (beholder, yours) theirTone yourTone shown)
              )
              Scene.squared (
                  Lines.all
                  |> List.map (fun line -> stack yourTone Words.placed true (Side.stack line yours))
              ) ]
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
                  // The score, first, because it is the only count here that is the game
                  // rather than the housekeeping.
                  Scene.cell
                      (if Set.isEmpty side.Compiled then Tone.Quiet else Tone.Slot(Ink.key seat))
                      $"compiled {Set.count side.Compiled} of {Protocol.Each}"

                  Scene.cell Tone.Quiet $"deck {List.length side.Deck}"
                  Scene.cell Tone.Quiet $"discard {List.length side.Discard}"
                  Scene.cell Tone.Quiet $"hand {List.length side.Hand}"

              // Only in the game that has one, and only beside whoever is holding it - a column
              // of blanks at a game without the rule would be a screen drawn for a rule it does
              // not have.
              if Session.withControl session then
                  Scene.cell
                      (Tone.Slot(Ink.key seat))
                      (if session.Control = HeldBy seat then "control" else "")
              else
                  Scene.cell
                      Tone.Quiet
                      (match side.Drafted with
                       | [] -> "nothing drafted yet"
                       | drafted -> Words.order drafted) ])
        |> Aligned

    // --- the hand ----------------------------------------------------------------------------

    /// The reader's own cards, and only ever theirs. Each is a cell carrying every way that card
    /// could be laid, because a card, a line and a face together are one move and a control has
    /// to carry the whole of one.
    ///
    /// Face up is one button and not three: no protocol is drafted twice, so a card's protocol
    /// sits on exactly one line and there is exactly one place it can go face up. Face down is
    /// three, because face down goes anywhere. That asymmetry is the game's, and drawing it is
    /// most of what makes the choice legible without reading a rule.
    ///
    /// And each of them says what it does. A hand drawn as five coloured names was a hand a
    /// player had to have memorised to play from - the whole choice at this game is between the
    /// number on a card and the words under it, and one of the two was not on the screen.
    let private hand beholder session =
        let side = Session.side beholder session
        let tone = Tone.Slot(Ink.key beholder)

        let ways card =
            [ for line in Field.facingLines beholder card session.Field ->
                  Does($"up {line}", $"play {Card.key card} {line}", Tone.Plainly)
              for line in Lines.all -> Does($"down {line}", $"play {Card.key card} {line} down", Tone.Quiet) ]

        // Always offered, because refreshing is always an action - and when the hand is empty it
        // is the only one, which is when a player most needs to be told it exists.
        let again =
            Tile(
                Some "refresh",
                Tone.Quiet,
                [ Does($"put {List.length side.Hand} down", "refresh", Tone.Plainly)
                  Scene.quietly $"take {Deck.HandSize} up"
                  Scene.quietly "costs the turn" ]
            )

        // No marker on the cards that say something: **every one of the ninety does**, so a star
        // would be a star on all of them. What is printed is under the name instead, whole -
        // `what fire-3` still says it at length, and now it says the same thing twice rather
        // than being the only place to find it.
        let card' card =
            Tile(Some(Card.name card), tone, inSmall (Words.printed card) @ [ Scene.quietly " " ] @ ways card)

        match side.Hand |> List.sortBy (fun card -> Protocol.name card.Protocol, card.Value) with
        | [] -> Walled(Across, [ Scene.squared [ again ] ])
        | cards ->
            Walled(
                Across,
                (cards |> List.map card') @ [ again ]
                |> List.chunkBySize PerRow
                |> List.map Scene.squared
            )

    // --- a card waiting on somebody ------------------------------------------------------------

    /// What is being asked, and a control per card that would answer it.
    ///
    /// The one screen this game did not have until the pile went in, and the one it most needed:
    /// a game stopped mid-effect looks exactly like a game that has hung, unless it says what it
    /// is waiting for and who from.
    let private question beholder (asked: Question) =
        let yours = asked.Chooser = beholder

        /// Who is asking: a card, or the rules themselves when the control component has forced
        /// a rearrangement.
        let whoAsks =
            match asked.Because with
            | Some source -> Card.name source.Saying
            | None -> "The control component"

        let doing, choices =
            match asked.Wanting with
            | ACard(command, targets) ->
                let doing =
                    match command with
                    | Delete _ -> "pick a card to delete"
                    | Flip _ -> "pick a card to turn over"
                    | Discard -> "pick a card to discard"
                    | Return _ -> "pick a card to take back into hand"
                    | Shift _ -> "pick a card to move"
                    | Show _ -> "pick a card to reveal"
                    | PlayFromHand _ -> "pick a card to play"
                    | Give -> "pick a card to give away"
                    | Draw _
                    | Refreshing'
                    | FromDeck _
                    | TakeAtRandom
                    | StopTheirCompile
                    | RevealTheirHand
                    | Swap
                    | Reveal
                    | UnderThis _
                    | Times _
                    | OneOrMore _
                    | May _
                    | Every _
                    | InAChosenLine _
                    | InAChosenLineOf _
                    | InEachOtherLine _
                    | InEachLineHolding _
                    | IfYouDo _
                    | IfCovering _
                    | Rearrange _
                    | TakeTheirTop
                    | Either _
                    | Opposing _ -> "pick a card"

                let choices =
                    targets
                    |> List.map (fun target ->
                        let card = Target.card target

                        let where =
                            match target with
                            | OnTable(seat, line, placed) ->
                                let whose = if seat = beholder then "yours" else "theirs"
                                let way = if Placed.isFaceUp placed then "face up" else "face down"
                                $"{whose}, line {line}, {way}"
                            | InHand(_, _) -> "in hand"

                        Tile(
                            Some(Card.name card),
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ Scene.quietly where
                              if yours then
                                  Does("choose", $"choose {Card.key card}", Tone.Plainly) ]
                        ))

                doing, choices

            | AnOrder(_, offered) ->
                let choices =
                    offered
                    |> List.map (fun order ->
                        let caption = order |> List.map Protocol.name |> String.concat " / "
                        let typed = order |> List.map Protocol.key |> String.concat " "

                        Tile(
                            None,
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ if yours then
                                  Does(caption, $"arrange {typed}", Tone.Plainly)
                              else
                                  Scene.quietly caption ]
                        ))

                $"put the {Protocol.Each} protocols in a different order", choices

            | ALine(moving, offered) ->
                let choices =
                    offered
                    |> List.map (fun line ->
                        Tile(
                            Some $"Line {line}",
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ if yours then
                                  Does("move it here", $"choose line {line}", Tone.Plainly)
                              else
                                  Scene.quietly "" ]
                        ))

                $"say where {Card.name (Target.card moving)} goes", choices

            | ALineFor(command, offered) ->
                let choices =
                    offered
                    |> List.map (fun line ->
                        Tile(
                            Some $"Line {line}",
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ if yours then
                                  Does("here", $"choose line {line}", Tone.Plainly)
                              else
                                  Scene.quietly "" ]
                        ))

                $"say which line to {Words.printing command} in", choices

            | Whether inner ->
                let choices =
                    [ "yes", "yes", Tone.Plainly; "no", "no", Tone.Quiet ]
                    |> List.map (fun (caption, typed, tone) ->
                        Tile(
                            None,
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ if yours then Does(caption, typed, tone) else Scene.quietly caption ]
                        ))

                $"say whether to {Words.printing inner}", choices

            // Both halves are on the buttons rather than "first" and "second", because a player
            // choosing between two commands should be reading the commands.
            | OneOf(first, second) ->
                let choices =
                    [ Words.printing first, "first"; Words.printing second, "second" ]
                    |> List.map (fun (caption, typed) ->
                        Tile(
                            None,
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ if yours then
                                  Does(caption, typed, Tone.Plainly)
                              else
                                  Scene.quietly caption ]
                        ))

                "say which of the two", choices

        let says =
            if yours then
                $"{whoAsks} needs you to {doing}."
            else
                $"{whoAsks} needs {Words.player asked.Chooser} to {doing}. Nothing else can happen until they do."

        Stack
            [ Scene.says says
              Walled(20, choices |> List.chunkBySize 3 |> List.map Scene.squared) ]

    // --- what a player may type ----------------------------------------------------------------

    /// The commands, short. `help` has them at length, and both are written from this list so
    /// neither can quietly grow a command the other has never heard of.
    let private verbs =
        [ "fire", "draft the Fire protocol (or 'draft fire')"
          "water dark fire", "set your three against lines 1, 2 and 3 (or 'arrange ...')"
          "fire-3 2", "play Fire-3 face up to line 2 (or 'play fire-3 2')"
          "fire-3 2 down", $"play it face down instead, for {Placed.FaceDownValue}, on any line"
          "refresh", $"put your hand down and take {Deck.HandSize} up - instead of playing, not as well as"
          "fire-3", "answer a card that is waiting on you to pick one (or 'choose fire-3')"
          "what fire-3", "read what a card says, whether or not it is anywhere near the table"
          "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
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
              "REFRESHING"
              Notes.refreshing
              ""
              "WHAT A CARD SAYS"
              Notes.text
              ""
              "COMPILING, AND WINNING"
              Notes.compiling
              $"A protocol already compiled compiles again if you win its line again: no nearer winning, the line wiped just the same, and the top card of the other deck comes into your hand. It can be played face down like anything else, or face up on the line its protocol sits on - which is on their side of the board."
              ""
              "COMMANDS"
              commands ]

    // --- the log ------------------------------------------------------------------------------

    /// What the game has said lately, oldest first - and in the words that seat may hear it in.
    /// One line of this is a secret before the protocols are turned over, so the log is read
    /// per seat rather than written once, like everything else on this board.
    let wording = Told.inWords Words.said Words.command

    let private heardBy seat = Told.inWords (Words.saidTo seat) Words.command

    let private log beholder (model: Model<Move, Session, Notice>) =
        match model.Log with
        | [] -> [ Scene.quietly nothingYet ]
        | notices -> notices |> List.rev |> List.map (heardBy beholder >> Scene.says)

    // --- the whole screen -----------------------------------------------------------------------

    /// One screen, and which parts of it there are depends on the stage: a draft has no field
    /// worth drawing and a hand nobody has been dealt, and a screen padded out with empty
    /// blocks is a screen that has to be read past.
    let board margins beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        let stage =
            // A card waiting on somebody outranks the stage, because until it is answered
            // nothing else on this screen is something a player could act on.
            match Session.asking session, session.Stage with
            | Some asked, _ -> Block(Blocks.choosing, [ question beholder asked ])
            | None, Drafting left -> Block(Blocks.draft, [ pool left; Scene.noted margins Notes.draft ])
            | None, Arranging ->
                Block(Blocks.protocols, [ arranging beholder session; Scene.noted margins Notes.arranging ])
            | None, Playing
            | None, Done _ ->
                Block(
                    Blocks.hand,
                    [ hand beholder session
                      Scene.noted margins Notes.hand
                      Scene.noted margins Notes.refreshing
                      (if Session.withControl session then
                           Scene.noted margins Notes.control
                       else
                           Blank) ]
                )

        let table =
            match session.Stage with
            | Drafting _ -> Blank
            | Arranging
            | Playing
            | Done _ ->
                Block(
                    Blocks.field,
                    [ field beholder session
                      Scene.noted margins Notes.field
                      Scene.noted margins Notes.compiling ]
                )

        // The two seats used to stand beside the stage, and they were the wrong two things to
        // put side by side: the stage is the widest block on the screen now that a card says
        // what it does, so half the room went to five short counts and every one of them came
        // out broken over two lines. They are counts rather than a choice, so they go under the
        // thing being chosen from and get the whole width to say themselves in one line each.
        Stack
            [ Heading(heading beholder session)
              table
              stage
              Block(Blocks.players, [ players beholder session ])
              Scene.listing margins Blocks.commands commands
              Block(Blocks.log, log beholder model) ]

    // --- the rest of what a player reads --------------------------------------------------------

    /// What somebody asked for, as much of it as the reader may know.
    ///
    /// The record is the whole truth of the game and a player is not owed the whole truth while
    /// it is still being played. An order laid face down is nobody's business until both are
    /// turned over - and the record would otherwise hand it straight over, having been careful
    /// about it everywhere else.
    let private askedFor beholder session (entry: Entry<Move, Notice>) =
        match entry.Asked with
        | Make(Arrange _) when entry.Actor <> beholder && Session.doing session = TheProtocols ->
            "sets their protocols, face down"
        | msg -> Words.command msg

    let history beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  turn {entry.Turn}"
              Scene.cell Tone.Plainly $"{Words.player entry.Actor}: {askedFor beholder session entry}"
              Scene.cell Tone.Plainly (entry.Told |> List.map (Told.inWords (Words.saidTo beholder) Words.command) |> String.concat " ") ]

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
    let answer (asked: string) (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        // A card named is a card asked about. This is what `View.Answer` was put there for. The
        // board prints what a card says where the card is lying, but only as much of it as that
        // card is still allowed to say - so this stays the one place to read the whole of one,
        // including the half a covering card has silenced and every card not on the table at all.
        let aboutACard =
            Commands.words asked
            |> List.tryPick (fun word -> Card.byName (word.ToLowerInvariant()))

        match aboutACard with
        | Some card ->
            Block(
                Card.name card,
                Words.printed card |> List.map Scene.says
            )
        | None ->

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

    /// This game's own rules of drawing, and no more than that: a cell here is a card, with its
    /// name and what is printed under it, so it wants far more room than a board of squares
    /// would ask for - and the writing wants to start at the left edge every line rather than
    /// be centred like a mark in a square, because it is a paragraph and not a glyph.
    let private sheet =
        """
.grid { --cell: 15rem; }
.grid .tile { align-items: stretch; }
.grid .tile .said { text-align: left; }
"""

    let shell =
        { Title = "Compile"
          Sheet = sheet
          Placeholder = "a protocol to draft it, or a card and a line - 'fire-3 2', or 'help'" }
