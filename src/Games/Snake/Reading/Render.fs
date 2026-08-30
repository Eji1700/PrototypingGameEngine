namespace Prototyping.Snake

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

module Render =

    let seated = Scene.seated Words.player

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
            function
            | Turns ->
                "One square a turn, and you may go any way but back into your own neck. Eating a piece of food adds a segment, and the next piece lands somewhere else at once."
            | Clock ->
                "The snakes move on their own, together, and quicken as they eat. A direction only turns a head - and never back into its own neck. Eating adds a segment, and the next piece lands somewhere else at once."


    let heading beholder session =
        match session with
        | InPlay play when play.Pace = Clock ->
            let mine =
                match play.Seats |> List.tryFind ((=) beholder) with
                | Some seat -> Some(seat, Session.snakeAt seat play)
                | None ->
                    Session.living play
                    |> List.tryHead
                    |> Option.map (fun seat -> seat, Session.snakeAt seat play)

            match mine with
            | Some(seat, snake) when Snake.isAlive snake ->
                $"Beat {play.Turn} - {seated (seat = beholder) seat}, {Words.segments (Snake.length snake)}, ate {Words.eaten snake.Eaten}"
            | Some(seat, snake) -> $"Beat {play.Turn} - {seated (seat = beholder) seat} {Words.fate (Option.get snake.Fate)}"
            | None -> $"Beat {play.Turn}"
        | InPlay play ->
            let yours = play.ToPlay = beholder
            let snake = Session.snakeAt play.ToPlay play

            $"Turn {play.Turn} - {seated yours play.ToPlay} to play, {Words.segments (Snake.length snake)} and facing {Words.direction snake.Facing}"
        | Finished(play, over) -> $"The game is over: {Words.scored play over}"


    let private standing play cell =
        match Session.snakes play |> List.tryFind (fun (_, snake) -> Snake.covers cell snake) with
        | Some(_, snake) when not (Snake.isAlive snake) -> Ink.Wreck, Tone.Quiet
        | Some(seat, snake) when Snake.head snake = cell -> Ink.head seat, Tone.Slot(Ink.key seat)
        | Some(seat, _) -> Ink.body seat, Tone.Slot(Ink.key seat)
        | None -> if play.Food = Some cell then Ink.Food, Tone.Slot Ink.food.Key else Ink.Empty, Tone.Quiet

    let private wall =
        Scene.cell Tone.Quiet ("+" + String.replicate Board.Width "-" + "+")

    let private grid play =
        let side = "|", Tone.Quiet

        Aligned(
            [ [ wall ] ]
            @ (Board.rows
               |> List.map (fun row -> [ Scene.runs ([ side ] @ (row |> List.map (standing play)) @ [ side ]) ]))
            @ [ [ wall ] ]
        )


    let private snakes beholder session =
        let play = Session.play session

        Session.snakes play
        |> List.map (fun (seat, snake) ->
            let yours = seat = beholder

            let standing =
                match snake.Fate with
                | Some fate -> Words.fate fate
                | None -> $"facing {Words.direction snake.Facing}"

            [ Scene.cell
                  Tone.Yours
                  (if play.Pace = Turns && seat = play.ToPlay && not (Session.isOver session) then "->" else "")
              Scene.cell (if yours then Tone.Yours else Tone.Slot(Ink.key seat)) (seated yours seat)
              Scene.cell Tone.Quiet (Words.segments (Snake.length snake))
              Scene.cell Tone.Quiet $"ate {Words.eaten snake.Eaten}"
              Scene.cell Tone.Quiet standing ])
        |> Aligned

    let private clock play =
        match play.Pace with
        | Turns -> Blank
        | Clock -> Scene.quietly $"clock at speed {play.Speed} of {Notch.Fastest} - 'faster' and 'slower', or + and -"

    let private onwards beholder session =
        let play = Session.play session

        match play.Pace with
        | Turns ->
            [ Scene.quietly "one square a turn"
              Does("north", "north", Tone.Plainly)
              Does("west", "west", Tone.Plainly)
              Does("east", "east", Tone.Plainly)
              Does("south", "south", Tone.Plainly)
              Does("go", "go", Tone.Plainly)
              Scene.quietly "and another board"
              Does("restart", "restart", Tone.Plainly) ]
        | Clock ->
            let mine =
                if List.contains beholder play.Seats then beholder else Session.foremost play

            let letter = Words.letter mine

            [ yield Scene.quietly $"turning {Words.player mine}"

              for way in [ North; West; East; South ] do
                  let line = $"{letter} {Words.direction way}"
                  yield Does(line, line, Tone.Plainly)

              yield Scene.quietly "and the clock"
              yield Does("slower", "slower", Tone.Plainly)
              yield Does("faster", "faster", Tone.Plainly)

              yield Scene.quietly "and another board"
              yield Does("restart", "restart", Tone.Plainly) ]


    let private verbs pace =
        [ match pace with
          | Turns ->
              yield "north, n, up", "one square that way (and east, south, west the same)"
              yield "go", "straight on, the way you are already facing"
          | Clock ->
              yield "arrows, wasd", "turn your snake - the arrows are A's, wasd are B's"
              yield "north, n, up", "the same, typed (and 'b north' for somebody else's)"
              yield "+ and -", $"wind the clock up or down ('faster', 'slower')"
              yield "speed 7", $"straight to a notch, from {Notch.Slowest} to {Notch.Fastest}"
              yield "space", "hold the clock while you think; space again to go on"

          yield "why east", "what is one square that way, before you commit to it"

          yield
              "restart",
              match pace with
              | Clock -> "another board - or 'r', once the clock has stopped"
              | Turns -> "another board, dealt fresh"

          yield
              "resign",
              (if pace = Turns then
                   "stop your snake, but write the game down"
               else
                   "give the game up, and write it down")

          yield! Commands.verbs ]

    let commands pace = Scene.verbs (verbs pace)

    let help pace =
        String.concat
            "\n"
            [ Scene.prose $"Snake, on a board of {Board.Width} by {Board.Height}."
              ""
              Scene.prose (Notes.moving pace)
              ""
              Scene.prose Notes.board
              ""
              Scene.prose
                  "A snake stops when its head meets the wall, another snake, or itself - and what is left of it lies where it fell, for everybody else to go round. At a table of one that is the end of the game and the score is what you ate. At a table of more, the last one moving has won."
              ""
              match pace with
              | Turns ->
                  Scene.prose
                      "Nothing happens here until you say so: a direction is a step, and the board waits between them. The other way of playing this game does not wait - see 'snake' rather than 'snake-turns'."
              | Clock ->
                  Scene.prose
                      "Nobody waits for anybody here. The clock moves every snake at once, quicker as they eat, and what you press only turns a head - so the wall arrives whether or not you had decided. Space holds the clock, Enter types a whole line, Esc puts the game down."

                  ""

                  Scene.prose
                      "Four snakes at one keyboard have a hand each: the arrows are Snake A, wasd is B, ijkl is C and the number pad is D. Typed, they say which snake they mean - 'b north' - and a bare direction is A's."

              ""
              "COMMANDS"
              commands pace ]


    let wording = Told.inWords Words.said Words.command

    let board margins beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model
        let pace = (Session.play session).Pace

        Stack
            [ Heading(heading beholder session)
              Block(Blocks.board, [ grid (Session.play session); Scene.noted margins Notes.board ])
              Beside
                  [ Block(
                        Blocks.snakes,
                        [ snakes beholder session
                          clock (Session.play session)
                          Scene.noted margins (Notes.moving pace) ]
                    )
                    Scene.offering margins Blocks.onwards (onwards beholder session) ]
              Scene.listing margins Blocks.commands (commands pace)
              Scene.logged margins Blocks.log (Scene.lately margins (Scene.log wording model)) ]


    let history beholder (model: Model<Move, Session, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  turn {entry.Turn}"
              Scene.cell Tone.Plainly $"{Words.player entry.Actor}: {Words.command entry.Asked}"
              Scene.cell Tone.Plainly (entry.Told |> List.map wording |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading beholder (Model.state model))

    let answer beholder asked (model: Model<Move, Session, Notice>) =
        let session = Model.state model
        let play = Session.play session

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


    let waiting = Scene.waiting Words.player


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
          "6", "d east"
          "+", "faster"
          "=", "faster"
          "-", "slower"
          "_", "slower" ]

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
