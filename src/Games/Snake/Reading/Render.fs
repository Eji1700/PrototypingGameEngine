namespace TCModel.Snake

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Snake

/// Every screen this game has, described once - and the three ways of reading one come back
/// from `Readers` already written.
module Render =

    /// What each part of the screen is called. Named rather than written out, because the
    /// readers draw them and a block renamed in one place would look like a block that had
    /// gone missing.
    module Blocks =
        let board = "The board"
        let snakes = "The snakes"
        let onwards = "Which way"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let board =
            "Your snake is its own letter, small along the body and capital at the head. The food is a star, and the wall is the edge - there is nothing on the other side of it."

        /// Two, because there are two paces and the difference between them is exactly what a
        /// note is for: at one the board waits for you, and at the other it does not.
        let moving =
            function
            | Turns ->
                "One square a turn, and you may go any way but back into your own neck. Eating a piece of food adds a segment, and the next piece lands somewhere else at once."
            | Clock ->
                "The snakes move on their own, together, and quicken as they eat. A direction only turns a head - and never back into its own neck. Eating adds a segment, and the next piece lands somewhere else at once."

    // --- the heading ---------------------------------------------------------------------

    let heading beholder session =
        match session with
        | InPlay play when play.Pace = Clock ->
            // Nobody is to play, so what the line above the board is for is the two things a
            // player glances up at: how long this has been going, and how they are doing.
            let mine =
                match play.Seats |> List.tryFind ((=) beholder) with
                | Some seat -> Some(seat, Session.snakeAt seat play)
                | None ->
                    Session.living play
                    |> List.tryHead
                    |> Option.map (fun seat -> seat, Session.snakeAt seat play)

            match mine with
            | Some(seat, snake) when Snake.isAlive snake ->
                $"Beat {play.Turn} - {Words.seated (seat = beholder) seat}, {Words.segments (Snake.length snake)}, ate {Words.eaten snake.Eaten}"
            | Some(seat, snake) ->
                $"Beat {play.Turn} - {Words.seated (seat = beholder) seat} {Words.fate (Option.get snake.Fate)}"
            | None -> $"Beat {play.Turn}"
        | InPlay play ->
            let yours = play.ToPlay = beholder
            let snake = Session.snakeAt play.ToPlay play

            $"Turn {play.Turn} - {Words.seated yours play.ToPlay} to play, {Words.segments (Snake.length snake)} and facing {Words.direction snake.Facing}"
        | Finished(play, over) -> $"The game is over: {Words.scored play over}"

    // --- the board -----------------------------------------------------------------------

    /// What is standing on one square, as a glyph and the colour it is drawn in.
    ///
    /// The order is the one thing here that is a rule rather than a picture: a snake is asked
    /// about before the food, because a snake lying across the food is a thing that can happen
    /// and a board that drew the star through it would be a board that lied about where the
    /// food is.
    let private standing play cell =
        match Session.snakes play |> List.tryFind (fun (_, snake) -> Snake.covers cell snake) with
        | Some(_, snake) when not (Snake.isAlive snake) -> Ink.Wreck, Tone.Quiet
        | Some(seat, snake) when Snake.head snake = cell -> Ink.head seat, Tone.Slot(Ink.key seat)
        | Some(seat, _) -> Ink.body seat, Tone.Slot(Ink.key seat)
        | None -> if play.Food = Some cell then Ink.Food, Tone.Slot Ink.food.Key else Ink.Empty, Tone.Quiet

    let private wall =
        Scene.cell Tone.Quiet ("+" + String.replicate Board.Width "-" + "+")

    /// The board, walls and all.
    ///
    /// The walls are drawn because they are part of the game rather than part of the frame: a
    /// square outside them is a death, and a board that stopped at its own edge would leave a
    /// player to work out where the edge was by dying at it.
    let private grid play =
        let side = "|", Tone.Quiet

        Aligned(
            [ [ wall ] ]
            @ (Board.rows
               |> List.map (fun row -> [ Scene.runs ([ side ] @ (row |> List.map (standing play)) @ [ side ]) ]))
            @ [ [ wall ] ]
        )

    // --- who is playing --------------------------------------------------------------------

    /// Every seat, with an arrow at whoever is to play and the reader's own marked: how long
    /// each snake is, what it has eaten, and how it stopped if it has.
    let private snakes beholder session =
        let play = Session.play session

        Session.snakes play
        |> List.map (fun (seat, snake) ->
            let yours = seat = beholder

            let standing =
                match snake.Fate with
                | Some fate -> Words.fate fate
                | None -> $"facing {Words.direction snake.Facing}"

            [ // An arrow at whoever is to play, and none at all on a clock: there, everybody is
              // to play at once and an arrow would be pointing at nothing.
              Scene.cell
                  Tone.Yours
                  (if play.Pace = Turns && seat = play.ToPlay && not (Session.isOver session) then "->" else "")
              Scene.cell (if yours then Tone.Yours else Tone.Slot(Ink.key seat)) (Words.seated yours seat)
              Scene.cell Tone.Quiet (Words.segments (Snake.length snake))
              Scene.cell Tone.Quiet $"ate {Words.eaten snake.Eaten}"
              Scene.cell Tone.Quiet standing ])
        |> Aligned

    /// What a player does over and over, as controls. Each carries the line it would type, so
    /// a reader with buttons draws buttons and one without writes out the words.
    ///
    /// On a clock they are the reader's own snake's - `b north` and not `north` - because at a
    /// table where nobody is waiting for anybody, a control that steered whoever happened to be
    /// first would steer the wrong snake for three players out of four. The board knows who is
    /// reading it; the parser cannot, and should not have to.
    let private onwards beholder session =
        let play = Session.play session

        match play.Pace with
        | Turns ->
            [ Scene.quietly "one square a turn"
              Does("north", "north", Tone.Plainly)
              Does("west", "west", Tone.Plainly)
              Does("east", "east", Tone.Plainly)
              Does("south", "south", Tone.Plainly)
              Does("go", "go", Tone.Plainly) ]
        | Clock ->
            let mine =
                if List.contains beholder play.Seats then beholder else Session.foremost play

            let letter = Words.letter mine

            Scene.quietly $"turning {Words.player mine}"
            :: [ for way in [ North; West; East; South ] ->
                     let line = $"{letter} {Words.direction way}"
                     Does(line, line, Tone.Plainly) ]

    // --- what a player may type ----------------------------------------------------------

    /// The commands, short. `help` has them at length, and both are written from this list so
    /// neither can quietly grow a command the other has never heard of.
    let private verbs pace =
        [ match pace with
          | Turns ->
              yield "north, n, up", "one square that way (and east, south, west the same)"
              yield "go", "straight on, the way you are already facing"
          | Clock ->
              yield "arrows, wasd", "turn your snake - the arrows are A's, wasd are B's"
              yield "north, n, up", "the same, typed (and 'b north' for somebody else's)"
              yield "space", "hold the clock while you think; space again to go on"

          yield "why east", "what is one square that way, before you commit to it"
          yield "undo, redo", "walk the game back and forward"
          yield "history", "the record so far"
          yield "notes", "hide the writing that explains the board"
          yield "commands", "hide this box"
          yield "view <name>", "draw the board another way"
          yield "save", "write the record now"
          yield "help", "every command, at length"
          yield
              "resign",
              (if pace = Turns then
                   "stop your snake, but write the game down"
               else
                   "give the game up, and write it down")
          yield "quit", "leave; the record is written and can be replayed" ]

    let commands pace = Scene.verbs (verbs pace)

    /// A paragraph as lines short enough for either terminal reader.
    let private wrapped text = Scene.paragraph 66 text

    let help pace =
        String.concat
            "\n"
            [ wrapped $"Snake, on a board of {Board.Width} by {Board.Height}."
              ""
              wrapped (Notes.moving pace)
              ""
              wrapped Notes.board
              ""
              wrapped
                  "A snake stops when its head meets the wall, another snake, or itself - and what is left of it lies where it fell, for everybody else to go round. At a table of one that is the end of the game and the score is what you ate. At a table of more, the last one moving has won."
              ""
              match pace with
              | Turns ->
                  wrapped
                      "Nothing happens here until you say so: a direction is a step, and the board waits between them. The other way of playing this game does not wait - see 'snake' rather than 'snake-turns'."
              | Clock ->
                  wrapped
                      "Nobody waits for anybody here. The clock moves every snake at once, quicker as they eat, and what you press only turns a head - so the wall arrives whether or not you had decided. Space holds the clock, Enter types a whole line, Esc puts the game down."

                  ""

                  wrapped
                      "Four snakes at one keyboard have a hand each: the arrows are Snake A, wasd is B, ijkl is C and the number pad is D. Typed, they say which snake they mean - 'b north' - and a bare direction is A's."

              ""
              "COMMANDS"
              commands pace ]

    // --- the log ---------------------------------------------------------------------------

    let wording = Told.inWords Words.said Words.command

    // --- the whole screen ---------------------------------------------------------------

    let board margins beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model
        let pace = (Session.play session).Pace

        Stack
            [ Heading(heading beholder session)
              Block(Blocks.board, [ grid (Session.play session); Scene.noted margins Notes.board ])
              Beside
                  [ Block(Blocks.snakes, [ snakes beholder session; Scene.noted margins (Notes.moving pace) ])
                    Block(Blocks.onwards, onwards beholder session) ]
              Scene.listing margins Blocks.commands (commands pace)
              Block(Blocks.log, Scene.log wording model) ]

    // --- the rest of what a player reads --------------------------------------------------

    let history beholder (model: Model<Move, Session, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  turn {entry.Turn}"
              Scene.cell Tone.Plainly $"{Words.player entry.Actor}: {Words.command entry.Asked}"
              Scene.cell Tone.Plainly (entry.Told |> List.map wording |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading beholder (Model.state model))

    /// What is one square that way, asked before committing to it.
    ///
    /// The one thing at this game worth asking and the one thing a board cannot quite show: a
    /// player can see the square, and what they want to know is what the *rules* will make of
    /// it - which is the difference between a wall and a tail that will have moved by the time
    /// they get there.
    let answer beholder asked (model: Model<Move, Session, Notice>) =
        let session = Model.state model
        let play = Session.play session

        // Asked of the seat reading rather than the seat to play, because a player waiting on
        // three other snakes is entitled to look at their own board while they wait.
        let seat = if List.contains beholder play.Seats then beholder else play.ToPlay

        let snake = Session.snakeAt seat play

        match Parse.direction asked with
        | Some direction ->
            let there = Turn.ahead seat direction play
            let target = Board.along direction (Snake.head snake)

            let says =
                match there with
                | Wall -> "the wall. Going that way stops the snake."
                | Into other when other = seat -> "its own body. Going that way stops the snake."
                | Into other when Snake.isAlive (Session.snakeAt other play) ->
                    $"{Words.player other}. Going that way stops the snake."
                | Into other -> $"what is left of {Words.player other}. Going that way stops the snake."
                | Food -> "the food. Going that way eats it."
                | Clear -> "open board."

            let far =
                match play.Food with
                | Some food when there <> Food -> $"The food is {Words.steps (Board.apart target food)} from there."
                | _ -> ""

            Block(
                $"{Words.direction direction} of {Words.player seat}",
                [ Scene.says $"One square {Words.direction direction} - {Words.towards direction} on the screen - is {says}"
                  if far <> "" then Scene.quietly far
                  if direction = Direction.opposite snake.Facing then
                      Scene.quietly (Words.rejection (CannotTurnBack direction)) ]
            )
        | None -> Block(Blocks.board, [ Scene.says $"'{asked}' is not a way to look. Say 'why east', or 'why up'." ])

    let rules pace = Scene.rules (help pace)

    // --- a table still filling up -----------------------------------------------------------

    let waiting = Scene.waiting Words.seated

    // --- what this game brings to a page -----------------------------------------------------

    /// This game's own rules of drawing, and no more than that - which here is none at all.
    /// The board is a grid of aligned rows, and the one thing that needs saying about one of
    /// those is said by the page itself.
    ///
    /// The keys are the other half of what a browser needs at a pace that does not wait. Four
    /// hands' worth, the same four the terminal has, each saying which snake it turns - so a
    /// page and a console are steered the same way and neither of them can send a line the
    /// parser would refuse. A game of turns hands over none, and the browser leaves the keys to
    /// whoever is typing.
    let private hands =
        [ "ArrowUp", "a north"
          "ArrowLeft", "a west"
          "ArrowDown", "a south"
          "ArrowRight", "a east"
          "w", "b north"
          "a", "b west"
          "s", "b south"
          "d", "b east"
          "i", "c north"
          "j", "c west"
          "k", "c south"
          "l", "c east"
          "8", "d north"
          "4", "d west"
          "5", "d south"
          "6", "d east" ]

    let shell pace =
        { Title = "Snake"
          Sheet = Page.tightRows
          Placeholder =
            match pace with
            | Turns -> "a way to go - 'north', 'e', 'up' - or 'go' to keep going, or 'help'"
            | Clock -> "the arrows steer - or type a way to go, 'b north' for somebody else's snake, or 'help'"
          Keys =
            match pace with
            | Turns -> []
            | Clock -> hands }
