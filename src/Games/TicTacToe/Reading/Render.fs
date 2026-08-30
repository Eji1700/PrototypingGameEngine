namespace Prototyping.TicTacToe

open Prototyping.Engine
open Prototyping.Table

module Render =

    let seated = Scene.seated Words.player

    module Blocks =
        let board = "The board"
        let players = "Players"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let board =
            "A number is a square nobody has taken. Say it to take it - '5', or 'place 5' if you prefer the long way round."

        let winning =
            "Three in a row wins: along a row, down a column, or corner to corner."


    let heading beholder session =
        match session with
        | InPlay play ->
            let yours = Session.seatOf play.ToPlay = beholder
            $"Turn {play.Turn} - {seated yours (Session.seatOf play.ToPlay)} to play"
        | Finished(_, ending) -> $"The game is over: {Words.ending ending}"


    let private square board n =
        match Board.at n board with
        | Some mark -> Tile(None, Tone.Slot(Ink.key mark), [ Big(Span.slot (Ink.key mark) (Words.mark mark)) ])
        | None -> Tile(None, Tone.Quiet, [ Does(string n, string n, Tone.Quiet) ])

    let private grid board =
        Walled(
            9,
            Squares.rows
            |> List.map (fun row -> Scene.squared (row |> List.map (square board)))
        )


    let private players beholder session =
        let acting = Session.active session
        let board = Session.board session

        Mark.all
        |> List.map (fun mark ->
            let seat = Session.seatOf mark
            let yours = seat = beholder
            let held = Board.held mark board |> List.length

            [ Scene.cell Tone.Yours (if seat = acting && not (Session.isOver session) then "->" else "")
              Scene.cell (if yours then Tone.Yours else Tone.Slot(Ink.key mark)) (seated yours seat)
              Scene.cell Tone.Quiet $"{held} of {Squares.Count}" ])
        |> Aligned


    let private verbs =
        [ "5", "take square 5 (or 'place 5')"; Commands.resign ] @ Commands.verbs

    let commands = Scene.verbs verbs

    let help =
        String.concat
            "\n"
            [ "Noughts and crosses, on a board of nine."
              ""
              Notes.winning
              Notes.board
              ""
              "Crosses go first. There is nothing dealt and nothing hidden: both players are"
              "looking at the whole game, which is the only kind of game this is."
              ""
              "COMMANDS"
              commands ]


    let wording = Told.inWords Words.said Words.command


    let board margins beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        Stack
            [ Heading(heading beholder session)
              Beside
                  [ Block(Blocks.board, [ grid (Session.board session); Scene.noted margins Notes.board ])
                    Block(Blocks.players, [ players beholder session; Scene.noted margins Notes.winning ]) ]
              Scene.listing margins Blocks.commands commands
              Scene.logged margins Blocks.log (Scene.log wording model) ]


    let history beholder (model: Model<Move, Session, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  turn {entry.Turn}"
              Scene.cell Tone.Plainly $"{Words.player entry.Actor}: {Words.command entry.Asked}"
              Scene.cell Tone.Plainly (entry.Told |> List.map wording |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading beholder (Model.state model))

    let nothingToExplain =
        "There is nothing here that needs working out: the board is nine squares in plain sight, and three in a row wins."

    let answer = Block(Blocks.board, [ Scene.says nothingToExplain ])

    let rules = Scene.rules help


    let waiting = Scene.waiting Words.player


    let private sheet =
        """
.grid { --cell: 4.6rem; }
"""

    let shell =
        { Title = "Noughts and crosses"
          Sheet = sheet
          Placeholder = "click a square, or type its number - 1 to 9, or 'help'"
          Keys = [] }
