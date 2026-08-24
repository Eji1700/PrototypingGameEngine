namespace Prototyping.MyGame

open Prototyping.Engine
open Prototyping.Table

module Render =

    module Blocks =
        let row = "The row"
        let players = "Players"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let row =
            $"Say how many to take - 1 to {Row.Most} of them. Whoever takes the last one wins."


    let heading beholder round =
        match round with
        | InPlay play ->
            let yours = Seat.at play.ToPlay = beholder
            $"Turn {play.Turn} - {Words.seated yours (Seat.at play.ToPlay)} to play"
        | Finished(_, ending) -> $"The game is over: {Words.ending ending}"


    let private tokens round =
        match Round.left round with
        | 0 -> Scene.quietly "the row is empty"
        | left -> Big(Span.slot Ink.Tokens (List.replicate left "*" |> String.concat " "))

    let private players beholder round =
        let acting = Round.active round

        [ for place in 1 .. Round.seats round ->
              let seat = Seat.at place
              let yours = seat = beholder

              [ Scene.cell Tone.Yours (if seat = acting && not (Round.isOver round) then "->" else "")
                Scene.cell (if yours then Tone.Yours else Tone.Plainly) (Words.seated yours seat) ] ]
        |> Aligned


    let private verbs =
        [ "2", "take two of them (or 'take 2')"
          "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide the writing that explains the row"
          "commands", "hide this box"
          "log", "hide what the game has been saying"
          "view <name>", "draw the row another way"
          "save", "write the record now"
          "help", "every command, at length"
          "resign", "give the game up, but write it down"
          "quit", "leave; the game is written down and 'replay' takes it up again" ]

    let commands = Scene.verbs verbs

    let help =
        String.concat
            "\n"
            [ $"A row of {Row.Dealt} tokens, and the people round it taking from it in turn."
              ""
              Notes.row
              ""
              "There is nothing dealt and nothing hidden: everybody is looking at the whole game,"
              "which makes this the smallest honest game there is to build on."
              ""
              "COMMANDS"
              commands ]


    let wording = Told.inWords Words.said Words.command


    let board margins beholder (model: Model<Move, Round, Notice>) =
        let round = Model.state model

        Stack
            [ Heading(heading beholder round)
              Beside
                  [ Block(Blocks.row, [ tokens round; Scene.noted margins Notes.row ])
                    Block(Blocks.players, [ players beholder round ]) ]
              Scene.listing margins Blocks.commands commands
              Scene.logged margins Blocks.log (Scene.log wording model) ]


    let history beholder (model: Model<Move, Round, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  turn {entry.Turn}"
              Scene.cell Tone.Plainly $"{Words.player entry.Actor}: {Words.command entry.Asked}"
              Scene.cell Tone.Plainly (entry.Told |> List.map wording |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading beholder (Model.state model))


    let answer =
        Block(
            Blocks.row,
            [ Scene.says
                  "There is nothing here that needs working out: the row is in plain sight, and whoever takes the last token wins." ]
        )

    let rules = Scene.rules help

    let waiting = Scene.waiting Words.seated


    let private sheet =
        """
.big { letter-spacing: 0.25ch; }
"""

    let shell =
        { Title = "MyGame"
          Sheet = sheet
          Placeholder = $"how many to take - 1 to {Row.Most}, or 'help'"
          Keys = [ "1", "take 1"; "2", "take 2"; "3", "take 3" ] }
