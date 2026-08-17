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

        let moving =
            "One square a turn, and you may go any way but back into your own neck. Eating a piece of food adds a segment, and the next piece lands somewhere else at once."

    // --- the heading ---------------------------------------------------------------------

    let heading beholder session =
        match session with
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

            [ Scene.cell Tone.Yours (if seat = play.ToPlay && not (Session.isOver session) then "->" else "")
              Scene.cell (if yours then Tone.Yours else Tone.Slot(Ink.key seat)) (Words.seated yours seat)
              Scene.cell Tone.Quiet (Words.segments (Snake.length snake))
              Scene.cell Tone.Quiet $"ate {Words.eaten snake.Eaten}"
              Scene.cell Tone.Quiet standing ])
        |> Aligned

    /// The five things a player types, as controls. Each carries the line it would type, so a
    /// reader with buttons draws five buttons and one without writes out the five words.
    let private onwards =
        [ Scene.quietly "one square a turn"
          Does("north", "north", Tone.Plainly)
          Does("west", "west", Tone.Plainly)
          Does("east", "east", Tone.Plainly)
          Does("south", "south", Tone.Plainly)
          Does("go", "go", Tone.Plainly) ]

    // --- what a player may type ----------------------------------------------------------

    /// The commands, short. `help` has them at length, and both are written from this list so
    /// neither can quietly grow a command the other has never heard of.
    let private verbs =
        [ "north, n, up", "one square that way (and east, south, west the same)"
          "go", "straight on, the way you are already facing"
          "why east", "what is one square that way, before you commit to it"
          "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "resign", "stop your snake, but write the game down"
          "quit", "leave; the record is written and can be replayed" ]

    let commands = Scene.verbs verbs

    /// A paragraph as lines short enough for either terminal reader.
    let private wrapped text = Scene.paragraph 66 text

    let help =
        String.concat
            "\n"
            [ wrapped $"Snake, on a board of {Board.Width} by {Board.Height}, one square a turn."
              ""
              wrapped Notes.moving
              ""
              wrapped Notes.board
              ""
              wrapped
                  "A snake stops when its head meets the wall, another snake, or itself - and what is left of it lies where it fell, for everybody else to go round. At a table of one that is the end of the game and the score is what you ate. At a table of more, the last one moving has won."
              ""
              wrapped
                  "There is no 'wasd' here, and that is deliberate: 'w' is west on a board with compass points on it and up on a keyboard, and one key meaning two things at a game where one wrong move ends it is worse than one key more to reach for."
              ""
              "COMMANDS"
              commands ]

    // --- the log ---------------------------------------------------------------------------

    let wording = Told.inWords Words.said Words.command

    // --- the whole screen ---------------------------------------------------------------

    let board margins beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        Stack
            [ Heading(heading beholder session)
              Block(Blocks.board, [ grid (Session.play session); Scene.noted margins Notes.board ])
              Beside
                  [ Block(Blocks.snakes, [ snakes beholder session; Scene.noted margins Notes.moving ])
                    Block(Blocks.onwards, onwards) ]
              Scene.listing margins Blocks.commands commands
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

        match Parse.line asked with
        | Ok(Send(Make(Go direction))) ->
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
        | _ -> Block(Blocks.board, [ Scene.says $"'{asked}' is not a way to look. Say 'why east', or 'why up'." ])

    let rules = Scene.rules help

    // --- a table still filling up -----------------------------------------------------------

    let waiting = Scene.waiting Words.seated

    // --- what this game brings to a page -----------------------------------------------------

    /// This game's own rules of drawing, and no more than that - which here is none at all.
    /// The board is a grid of aligned rows, and the one thing that needs saying about one of
    /// those is said by the page itself.
    let shell =
        { Title = "Snake"
          Sheet = Page.tightRows
          Placeholder = "a way to go - 'north', 'e', 'up' - or 'go' to keep going, or 'help'" }
