namespace TCModel.Life

open TCModel.Engine
open TCModel.Table
open TCModel.Life

module Render =

    module Blocks =
        let board = "The board"
        let run = "The run"
        let onwards = "What next"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let board =
            "A letter and a number name a cell - 'f7' is column f, row 7. Say one to turn it on, or again to turn it off. 'why f7' says what the rule will do with it."

        let rule =
            "A living cell with two or three neighbours lives on. An empty one with exactly three comes alive. Everything else is empty next time round. The edges are joined, so what leaves one side arrives at the other."


    let heading world =
        let where = $"Generation {world.Generation}"

        if World.isEmpty world then
            $"{where} - nothing is left alive"
        elif World.settled world then
            $"{where} - settled: {Words.cells (World.living world)} that will not change again"
        elif World.beating world then
            $"{where} - {Words.cells (World.living world)}, beating between two shapes"
        else
            $"{where} - {Words.cells (World.living world)} alive"


    let private row world cells =
        cells
        |> List.map (fun cell -> if World.alive cell world then Ink.Living, Tone.Slot Ink.Key else Ink.Empty, Tone.Quiet)
        |> Scene.runs

    let private grid world =
        Aligned(
            [ Scene.cell Tone.Quiet ""; Scene.cell Tone.Quiet Grid.letters ]
            :: (Grid.rows
                |> List.mapi (fun index cells -> [ Scene.cell Tone.Quiet (string (index + 1)); row world cells ]))
        )


    let private standing world =
        [ Scene.says $"Generation {world.Generation}."
          Scene.says $"{Words.cells (World.living world)} alive, of {Grid.Width * Grid.Height} squares."
          // Stays on the screen while the board is moving, where the notes and the box of
          // commands do not: somebody watching it run is entitled to find out how to stop it
          // without stopping it to read.
          Scene.quietly (
              if world.Running then
                  $"running at speed {world.Speed} of {World.Fastest} - 'p' stops it, + and - wind the clock"
              else
                  $"stopped at speed {world.Speed} of {World.Fastest} - 'p' starts it, 'step' goes one generation"
          )
          Scene.quietly (
              if World.isEmpty world then
                  "Nothing is left for the rule to work on. Turn cells on to draw something, or restart for another soup."
              elif World.settled world then
                  "It has settled. Nothing the rule does will change it again."
              elif World.beating world then
                  "It is back where it was two generations ago, and will go on beating between the two."
              else
                  "It is still going."
          ) ]

    let private onwards world =
        [ Scene.quietly "each of these is a line you could type"
          Does((if world.Running then "stop" else "run"), (if world.Running then "stop" else "run"), Tone.Plainly)
          Does("step", "step", Tone.Plainly)
          Does("step 10", "step 10", Tone.Plainly)
          Scene.quietly "and the clock"
          Does("slower", "slower", Tone.Plainly)
          Does("faster", "faster", Tone.Plainly)
          Scene.quietly "and the board"
          Does("undo", "undo", Tone.Plainly)
          Does("clear", "clear", Tone.Plainly)
          Does("restart", "restart", Tone.Plainly) ]


    let private verbs =
        [ "run, p", "start the rule, and stop it again"
          "f7", "turn cell f7 on, or off (or 'toggle f7')"
          "step, step 10", "one generation, or ten, while it is stopped"
          "+ and -", "wind the clock up or down ('faster', 'slower')"
          "speed 7", "straight to a notch, from 1 to 9"
          "why f7", "what the rule will do with that cell, and why"
          "undo, redo", "walk the run back and forward"
          "clear", "sweep the board, to draw on it from nothing"
          "restart", "deal another soup; 'restart 42' deals that one"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
          "log", "hide what the game has been saying"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "quit", "leave; the record is written and can be replayed" ]

    let commands = Scene.verbs verbs

    let private wrapped text = Scene.paragraph 66 text

    let help =
        String.concat
            "\n"
            [ wrapped $"Conway's Game of Life, on a board of {Grid.Width} by {Grid.Height} with its edges joined."
              ""
              wrapped
                  "The rule runs on a clock: 'run' - or 'p' at a terminal, space in a browser - starts it and stops it again, and + and - wind the clock between two and nine generations a second. Stopped, 'step' goes one generation at a time."
              ""
              wrapped Notes.rule
              ""
              wrapped Notes.board
              ""
              wrapped
                  "Nobody is opposed here and there is nothing to win. The deal is a soup drawn from the seed, and what happens to it is settled the moment it is dealt - so a seed is a pattern, and the same seed is the same pattern every time."
              ""
              wrapped
                  "It never ends. What it does instead is arrive somewhere the rule has nothing more to do: a board that has settled, or one with nothing left alive. Both are said plainly and neither takes the board away - turn a cell on, sweep it and draw something of your own, or walk the run back with 'undo'."
              ""
              "COMMANDS"
              commands ]


    let wording = Told.inWords Words.said Words.command


    let board margins _ (model: Model<Move, World, Notice>) =
        let world = Model.state model

        Stack
            [ Heading(heading world)
              Block(Blocks.board, [ grid world; Scene.noted margins Notes.board ])
              Beside
                  [ Block(Blocks.run, standing world @ [ Scene.noted margins Notes.rule ])
                    Scene.offering margins Blocks.onwards (onwards world) ]
              Scene.listing margins Blocks.commands commands
              Scene.logged margins Blocks.log (Scene.log wording model) ]


    let history _ (model: Model<Move, World, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  gen {entry.Turn}"
              Scene.cell Tone.Plainly (Words.command entry.Asked)
              Scene.cell Tone.Plainly (entry.Told |> List.map wording |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading (Model.state model))

    let answer asked (model: Model<Move, World, Notice>) =
        let world = Model.state model

        match Grid.read asked with
        | Some cell when Grid.holds cell ->
            let neighbours =
                Grid.neighbours cell |> List.filter (fun other -> World.alive other world)

            let around = List.length neighbours
            let alive = World.alive cell world
            let next = Set.contains cell (Grid.step world.Cells)

            let standing =
                if alive then
                    $"{Words.cell cell} is alive, with {Words.cells around} round it."
                else
                    $"{Words.cell cell} is empty, with {Words.cells around} round it."

            let coming =
                match alive, next with
                | true, true -> "Two or three keep a cell, so it lives on."
                | true, false when around < 2 -> "Fewer than two, so it dies."
                | true, false -> "More than three, so it dies."
                | false, true -> "Exactly three make a cell, so it comes alive."
                | false, false -> "It takes exactly three to make a cell, so it stays empty."

            Block(
                $"Cell {Words.cell cell}",
                [ Scene.says standing
                  Scene.says coming
                  Scene.quietly (
                      match neighbours with
                      | [] -> "Nothing is near it."
                      | living -> "Round it: " + (living |> List.map Words.cell |> String.concat ", ") + "."
                  ) ]
            )
        | Some cell -> Block(Blocks.board, [ Scene.says (Words.rejection (NoSuchCell cell)) ])
        | None -> Block(Blocks.board, [ Scene.says $"'{asked}' is not a cell. Ask about one by name - 'why f7'." ])

    let rules = Scene.rules help


    let waiting = Scene.waiting Words.seated


    let shell =
        { Title = "Life"
          Sheet = Page.tightRows
          Placeholder = "'run' starts and stops the rule - or a cell to turn it on, 'f7', or 'help'"

          // Space is the obvious key for starting and stopping a thing that runs, and on a page
          // it is free - the table's own hold is a terminal's.
          Keys =
            [ " ", "run"
              "p", "run"
              ".", "step"
              "+", "faster"
              "=", "faster"
              "-", "slower"
              "_", "slower" ] }
