namespace TCModel.Compile

open TCModel.Engine
open TCModel.Table
open TCModel.Compile

module Render =

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
            $"{List.length Protocol.all} protocols, no duplicates, and the {Draft.Picks} picks go 1-2-2-1: one to Player 1, two to Player 2, two back to Player 1, one back to Player 2. Say a protocol to take it - 'fire'."

        let arranging =
            $"Your {Protocol.Each} protocols go against the {Lines.Count} lines, one each, in the order you say them. Both players do it, and the lines they make are read across the table from each other."

        let field =
            "A line runs across the table: their stack, the two protocols that meet in the middle of it, and yours. A stack grows away from the line, so the card most recently played is the one nearest whoever played it - furthest up for them, furthest down for you."

        let control =
            $"The control component: at the start of your turn, leading {Field.LanesForControl} lanes - strictly, a tie is no lead - takes it, out of the middle or off the other player. Holding it costs you something every time you compile or refresh: your {Protocol.Each} protocols have to move first, into a different order. The stacks do not move with them, so a line built patiently for one protocol can end up compiling another."

        let text =
            "A card played face up does what is printed on it, one command at a time - and between any two of them the table is looked at again, because a command that turned a card face up has put that card's own text in front of whatever was waiting. A command with nothing to point at simply finds nothing to do, and the one after it still happens. A command with one thing to point at does it; with several, the game stops and asks - sometimes of the player whose turn it is not, and then nothing at all moves until they answer."

        let boxes =
            "A card you can still play on is drawn as its three boxes, empty ones and all: the top is a standing rule and whatever the card is listening for, the middle is what fires when the card is turned face up or uncovered again, and the bottom is a standing rule too. Playing anything on a line - face up or face down, it makes no difference - covers the middle and the bottom of whatever was there, so a covered card is drawn without its boxes and what is left under its name is its top box and the whole of what it still says. A card lying face down says nothing at all, whatever is printed on it."

        let refreshing =
            $"Nothing is drawn at the end of a turn. 'refresh' puts your whole hand down and takes {Deck.HandSize} up, and it costs the turn - so {Deck.HandSize} cards is {Deck.HandSize} turns of tempo, and the turn you spend getting more is a turn they spend getting closer to {Stack.ToCompile}. With an empty hand it is the only thing left to do. A deck that runs out is shuffled from its own discard."

        let compiling =
            $"At the start of your turn, every line where you have {Stack.ToCompile} or more and strictly more than they do is compiled: the protocol facing it is turned over and the whole line goes, both players' cards alike. 'ready' beside a stack means it will compile next time that player's turn begins - which is one turn to answer it, and {Placed.FaceDownValue} face down is enough if it makes the scores level. Compile all {Protocol.Each} of your protocols and you win."

        let hand =
            $"Say a card and a line to play it - 'fire-3 2'. Face up it is worth the number printed on it and may only go where its protocol is - either player's. Face down - 'fire-3 2 down' - it is worth {Placed.FaceDownValue} and goes anywhere, so a card you cannot use is never a dead card."

    let private whoAsks =
        function
        | ACardSaying source -> Card.name source.Saying
        | TheControlComponent -> "The control component"
        | TheCacheCheck -> "The check cache phase"


    [<Literal>]
    let private Across = 28

    [<Literal>]
    let private Room = 20

    [<Literal>]
    let private PerRow = 3

    let private inSmall said =
        said |> List.collect (Scene.wrap Room) |> List.map Scene.quietly

    let private boxed (top, middle, bottom) =
        [ "top", top; "middle", middle; "bottom", bottom ]
        |> List.map (fun (which, said) ->
            Tile(Some which, Tone.Quiet, (if List.isEmpty said then [ Scene.quietly " " ] else inSmall said)))


    let heading beholder session =
        let seat = Session.active session
        let yours = seat = beholder
        let who = Words.seated yours seat

        match Session.asking session, session.Stage with
        | Some asked, _ -> $"Turn {session.Turn} - {whoAsks asked.Because} is waiting on {who}"
        | None, Drafting _ -> $"The draft, pick {Session.picksMade session + 1} of {Draft.Picks} - {who} to choose a protocol"
        | None, Arranging -> $"Protocols - {who} to set theirs against the lines"
        | None, Playing -> $"Turn {session.Turn} - {who} to play"
        | None, Done ending -> $"The game is over: {Words.ending ending}"


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
                            Tile(
                                None,
                                Tone.Quiet,
                                [ Does(Protocol.name protocol, $"draft {Protocol.key protocol}", Tone.Plainly) ]
                            ))
                    ))
            )


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


    /// One line's stack of cards. Depth 0 is the uncovered one, which is the only card that reads its
    /// whole text - the rest are drawn small, since all that is still doing anything is their top box.
    /// `newestLast` turns the drawing over for the side of the board that is read from the far edge in.
    let private stack tone reading newestLast cards =
        match cards with
        | [] -> Tile(None, Tone.Quiet, [ Scene.quietly "-" ])
        | cards ->
            let drawn depth placed =
                let top, _, _ = Words.saying (depth = 0) placed

                [ Say [ Span.toned tone (reading placed) ]
                  if depth = 0 && Placed.isFaceUp placed then
                      yield! boxed (Words.saying true placed)
                  else
                      yield! inSmall top ]

            let laid = cards |> List.mapi drawn

            (if newestLast then List.rev laid else laid)
            |> List.concat
            |> fun body -> Tile(None, tone, body)

    let private middle field (them, theirs) (you, yours) theirTone yourTone shown line =
        let facing side =
            Side.protocolOn line side
            |> Option.map Protocol.name
            |> Option.defaultValue "not set"

        let theirFacing =
            if shown then facing theirs
            elif List.isEmpty theirs.Order then "not set"
            else Words.hidden

        let against name tone seat side =
            let standing =
                if Field.won seat line field then
                    "  ready"
                elif
                    Side.protocolOn line side
                    |> Option.exists (fun protocol -> Side.hasCompiled protocol side)
                then
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

    let private field beholder session =
        let them = Session.other beholder
        let theirs = Session.side them session
        let yours = Session.side beholder session
        let theirTone = Tone.Slot(Ink.key them)
        let yourTone = Tone.Slot(Ink.key beholder)

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


    let private players beholder session =
        let acting = Session.active session

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
                  Scene.cell
                      (if Set.isEmpty side.Compiled then Tone.Quiet else Tone.Slot(Ink.key seat))
                      $"compiled {Set.count side.Compiled} of {Protocol.Each}"

                  Scene.cell Tone.Quiet $"deck {List.length side.Deck}"
                  Scene.cell Tone.Quiet $"discard {List.length side.Discard}"
                  Scene.cell Tone.Quiet $"hand {List.length side.Hand}"

              if Session.withControl session then
                  Scene.cell (Tone.Slot(Ink.key seat)) (if session.Control = HeldBy seat then "control" else "")
              else
                  Scene.cell
                      Tone.Quiet
                      (match side.Drafted with
                       | [] -> "nothing drafted yet"
                       | drafted -> Words.order drafted) ])
        |> Aligned


    let private hand beholder session =
        let side = Session.side beholder session
        let tone = Tone.Slot(Ink.key beholder)

        let ways card =
            [ for line in Field.facingLines beholder card session.Field ->
                  Does($"play face up {line}", $"play {Card.key card} {line}", Tone.Plainly) ]

        let again =
            Tile(
                Some "refresh",
                Tone.Quiet,
                [ Does($"put {List.length side.Hand} down", "refresh", Tone.Plainly)
                  Scene.quietly $"take {Deck.HandSize} up"
                  Scene.quietly "costs the turn" ]
            )

        let card' card =
            Tile(Some(Card.name card), tone, boxed (Words.boxes card) @ ways card)

        match side.Hand |> List.sortBy (fun card -> Protocol.name card.Protocol, card.Value) with
        | [] -> Walled(Across, [ Scene.squared [ again ] ])
        | cards ->
            Walled(
                Across,
                (cards |> List.map card') @ [ again ]
                |> List.chunkBySize PerRow
                |> List.map Scene.squared
            )


    let private question beholder (asked: Question) =
        let yours = asked.Chooser = beholder

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

                        match target with
                        | InHand(whose, _) when whose <> beholder ->
                            Tile(None, Tone.Quiet, [ Scene.quietly "a card in their hand" ])
                        | target ->
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
                                  if yours then Does("choose", $"choose {Card.key card}", Tone.Plainly) ]
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
                            [ if yours then Does(caption, $"arrange {typed}", Tone.Plainly) else Scene.quietly caption ]
                        ))

                $"put the {Protocol.Each} protocols in a different order", choices

            | ALine(moving, offered) ->
                let choices =
                    offered
                    |> List.map (fun line ->
                        Tile(
                            Some $"Line {line}",
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ if yours then Does("move it here", $"choose line {line}", Tone.Plainly) else Scene.quietly "" ]
                        ))

                $"say where {Card.name (Target.card moving)} goes", choices

            | ALineFor(command, offered) ->
                let choices =
                    offered
                    |> List.map (fun line ->
                        Tile(
                            Some $"Line {line}",
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ if yours then Does("here", $"choose line {line}", Tone.Plainly) else Scene.quietly "" ]
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

            | OneOf(first, second) ->
                let choices =
                    [ Words.printing first, "first"; Words.printing second, "second" ]
                    |> List.map (fun (caption, typed) ->
                        Tile(
                            None,
                            (if yours then Tone.Yours else Tone.Quiet),
                            [ if yours then Does(caption, typed, Tone.Plainly) else Scene.quietly caption ]
                        ))

                "say which of the two", choices

        let says =
            if yours then
                $"{whoAsks asked.Because} needs you to {doing}."
            else
                $"{whoAsks asked.Because} needs {Words.player asked.Chooser} to {doing}. Nothing else can happen until they do."

        Stack
            [ Scene.says says
              Walled(20, choices |> List.chunkBySize 3 |> List.map Scene.squared) ]


    let private verbs =
        [ "fire", "draft the Fire protocol (or 'draft fire')"
          "water dark fire", "set your three against lines 1, 2 and 3 (or 'arrange ...')"
          "fire-3 2", "play Fire-3 face up to line 2 (or 'play fire-3 2')"
          "fire-3 2 down", $"play it face down instead, for {Placed.FaceDownValue}, on any line"
          "refresh", $"put your hand down and take {Deck.HandSize} up - instead of playing, not as well as"
          "fire-3", "answer a card that is waiting on you to pick one (or 'choose fire-3')"
          "what fire-3", "read what a card says, whether or not it is anywhere near the table"
          "peek", "read your own cards lying face down ('peek all' for every one you know)"
          "pile", "what the game still has to do, in the order it will do it"
          "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
          "log", "hide what the game has been saying"
          "view <name>", "draw the field another way"
          "save", "write the record now"
          "help", "every command, at length"
          "resign", "give the game up, but write it down"
          "quit", "leave; the game is written down and 'replay' takes it up again" ]

    let commands = Scene.verbs verbs

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
              Notes.boxes
              ""
              "COMPILING, AND WINNING"
              Notes.compiling
              $"A protocol already compiled compiles again if you win its line again: no nearer winning, the line wiped just the same, and the top card of the other deck comes into your hand. It can be played face down like anything else, or face up on the line its protocol sits on - which is on their side of the board."
              ""
              "COMMANDS"
              commands ]


    let wording = Told.inWords Words.said Words.command

    let private heardBy seat =
        Told.inWords (Words.saidTo seat) Words.command


    let board margins beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        let stage =
            match Session.asking session, session.Stage with
            | Some asked, _ -> Block(Blocks.choosing, [ question beholder asked ])
            | None, Drafting left -> Block(Blocks.draft, [ pool left; Scene.noted margins Notes.draft ])
            | None, Arranging -> Block(Blocks.protocols, [ arranging beholder session; Scene.noted margins Notes.arranging ])
            | None, Playing
            | None, Done _ ->
                Block(
                    Blocks.hand,
                    [ hand beholder session
                      Scene.noted margins Notes.hand
                      Scene.noted margins Notes.refreshing
                      (if Session.withControl session then Scene.noted margins Notes.control else Blank) ]
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
                      Scene.noted margins Notes.boxes
                      Scene.noted margins Notes.compiling ]
                )

        Stack
            [ Heading(heading beholder session)
              table
              stage
              Block(Blocks.players, [ players beholder session ])
              Scene.listing margins Blocks.commands commands
              Scene.logged margins Blocks.log (Scene.log (heardBy beholder) model) ]


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
              Scene.cell Tone.Plainly (entry.Told |> List.map (heardBy beholder) |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading beholder (Model.state model))


    let private peeking beholder rest session =
        let them = Session.other beholder

        let mine yours =
            [ for seat in (if yours then [ beholder ] else Session.seats) do
                  for line in Lines.all do
                      for placed in Side.stack line (Session.side seat session) do
                          if not (Placed.isFaceUp placed) && Placed.readableBy (seat = beholder) placed then
                              let whose = if seat = beholder then "yours" else "theirs"

                              Tile(
                                  Some(Card.name placed.Card),
                                  Tone.Slot(Ink.key seat),
                                  Scene.quietly $"{whose}, line {line}" :: boxed (Words.boxes placed.Card)
                              ) ]

        let across =
            match rest with
            | []
            | [ "mine" ]
            | [ "yours" ] -> Some false
            | [ "all" ]
            | [ "table" ] -> Some true
            | _ -> None

        match across with
        | None ->
            Block(
                "Peek",
                [ Scene.says
                      "Say 'peek' to read your own cards lying face down, or 'peek all' to read every face-down card on the table you already know." ]
            )
        | Some everywhere ->
            let found = mine (not everywhere)

            let nothing =
                if everywhere then
                    $"There is nothing face down on the table you know the face of. {Words.player them}'s cards are theirs until one of them has been face up here."
                else
                    "You have nothing lying face down."

            Block(
                (if everywhere then "Peek - the whole table" else "Peek - your own"),
                [ if List.isEmpty found then
                      Scene.quietly nothing
                  else
                      Walled(Across, found |> List.chunkBySize PerRow |> List.map Scene.squared) ]
            )

    let private piling beholder session =
        match session.Pile with
        | [] -> Block("The pile", [ Scene.quietly "nothing waiting - the game is not in the middle of anything" ])
        | pile ->
            Block(
                "The pile",
                [ Scene.says "What is still to happen, in the order it will happen."
                  Aligned(
                      pile
                      |> List.mapi (fun step waiting ->
                          [ Scene.cell Tone.Quiet $"{step + 1}."
                            Scene.cell
                                (match waiting with
                                 | Ask _ -> Tone.Yours
                                 | _ -> Tone.Plainly)
                                (Words.waiting beholder waiting) ])
                  ) ]
            )

    let answer beholder (asked: string) (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        match Commands.lowered asked with
        | "peek" :: rest -> peeking beholder rest session
        | "pile" :: _ -> piling beholder session
        | _ ->

        let aboutACard =
            Commands.words asked
            |> List.tryPick (fun word -> Card.byName (word.ToLowerInvariant()))

        match aboutACard with
        | Some card -> Block(Card.name card, boxed (Words.boxes card))
        | None ->

        let puzzled =
            match Commands.words asked with
            | []
            | "what" :: _
            | "says" :: _ -> []
            | _ -> [ $"I do not know how to '{asked}'." ]

        let seat = Session.active session
        let side = Session.side seat session
        let yours = seat = beholder

        let playable =
            side.Hand
            |> List.collect (fun card ->
                Field.facingLines seat card session.Field
                |> List.map (fun line -> $"'{Card.key card} {line}'"))

        let inPlay =
            if not yours then
                [ $"{Words.player seat} is to play, and what they are holding is theirs."
                  Notes.field ]
            elif List.isEmpty side.Hand then
                [ $"Your hand is empty, so 'refresh' is the only thing left this turn - put nothing down and take {Deck.HandSize} up." ]
            else
                [ $"It is your turn to play a card. In hand: {Words.choices side.Hand}."
                  (match playable with
                   | [] ->
                       $"None of them has its protocol facing a line just now, so they can only go face down - '{Card.key (List.head side.Hand)} 1 down', worth {Placed.FaceDownValue}, on any line."
                   | lines ->
                       let up = String.concat ", " lines
                       $"Face up, where its protocol is: {up}. Face down, on any line, for {Placed.FaceDownValue}: '{Card.key (List.head side.Hand)} 1 down'.")
                  $"Or 'refresh' - your whole hand down and {Deck.HandSize} up, instead of playing, not as well as." ]

        let said =
            match Session.asking session, session.Stage with
            | Some question, _ ->
                [ if question.Chooser = beholder then
                      $"{whoAsks question.Because} is waiting on you: {Words.wanting question.Wanting}."
                  else
                      $"{whoAsks question.Because} is waiting on {Words.player question.Chooser}, and nothing at all moves until they answer." ]
            | None, Drafting left ->
                let names = left |> List.map Protocol.key |> String.concat ", "

                [ if Session.active session = beholder then
                      $"It is your pick. Say one of these to take it: {names}."
                  else
                      $"It is {Words.player (Session.active session)}'s pick, out of these: {names}."
                  $"{Draft.Picks - Session.picksMade session} of the {Draft.Picks} picks are still to come."
                  Notes.draft ]
            | None, Arranging ->
                let anOrder = side.Drafted |> List.map Protocol.key |> String.concat " "

                [ if List.isEmpty side.Order && yours then
                      $"Say your {Protocol.Each} in the order you want them against lines 1 to {Lines.Count} - '{anOrder}', or any other order of the same {Protocol.Each}."
                  else
                      "Yours are set. Nothing else happens until both are, and then both are turned over at once."
                  Notes.arranging ]
            | None, Playing -> inPlay
            | None, Done ending -> [ $"Nothing: {Words.ending ending}." ]

        Block("What is being asked", puzzled @ said |> List.map Scene.says)

    let rules = Scene.rules help


    let waiting = Scene.waiting Words.seated


    let private sheet =
        """
.grid { --cell: 15rem; }
.grid .tile { align-items: stretch; justify-content: flex-start; }
.grid .tile .said { text-align: left; }
.grid .tile .tile { min-height: 0; min-width: 0; }
"""

    let shell =
        { Title = "Compile"
          Sheet = sheet
          Placeholder = "a protocol to draft it, or a card and a line - 'fire-3 2', or 'help'"
          Keys = [] }
