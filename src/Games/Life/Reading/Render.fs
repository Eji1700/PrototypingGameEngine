namespace TCModel.Life

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Life

/// Every screen this game has, described once - and the three ways of reading one come back
/// from `Readers` already written.
///
/// A `Scene` says what a screen is *made of* and nothing about what it looks like, and that is
/// what makes a board of four hundred cells possible here at all. Written out three times over
/// - as text, as Spectre's widgets, as elements - a grid this size would be three layouts to
/// keep in step, and two of them would be wrong within a week.
module Render =

    /// What each part of the screen is called. Named rather than written out, because the
    /// readers draw them and a block renamed in one place would look like a block that had
    /// gone missing.
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

    // --- the heading ---------------------------------------------------------------------
    //
    // Whose turn it is, at a game where it is always the same one, is not worth a line. What
    // is worth one is the thing a single frame of a board cannot show: whether it is still
    // going anywhere.

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

    // --- the board ---------------------------------------------------------------------------

    /// One row, as spans - and runs of the same tone gathered into one span each by
    /// `Scene.runs`, because a board of four hundred and sixteen cells drawn afresh every time
    /// anybody looks at it is not four hundred and sixteen spans.
    let private row world cells =
        cells
        |> List.map (fun cell -> if World.alive cell world then Ink.Living, Tone.Slot Ink.Key else Ink.Empty, Tone.Quiet)
        |> Scene.runs

    /// The board: a column of rows, each with its number beside it, under a line of column
    /// letters.
    ///
    /// Rows lined up rather than cells walled off, and that is a decision about this game
    /// rather than a shortcut. A `Walled` grid is what the game of nine squares wants - a cell
    /// with room in it, a wall round it, a button on it - and at four hundred cells every
    /// reader here would draw something unreadable: a table four hundred columns of walls
    /// wide, or a page of four hundred boxes. What a cell of *this* board is, is one character
    /// in a shape made of its neighbours, and the shape is the whole point.
    let private grid world =
        Aligned(
            [ Scene.cell Tone.Quiet ""; Scene.cell Tone.Quiet Grid.letters ]
            :: (Grid.rows
                |> List.mapi (fun index cells -> [ Scene.cell Tone.Quiet (string (index + 1)); row world cells ]))
        )

    // --- where the run stands -----------------------------------------------------------------

    let private standing world =
        [ Scene.says $"Generation {world.Generation}."
          Scene.says $"{Words.cells (World.living world)} alive, of {Grid.Width * Grid.Height} squares."
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

    /// The four things a player does over and over, as controls.
    ///
    /// Each carries the line it would type, which is the whole of what a control is for: a
    /// reader with buttons draws four buttons and one without writes out the four words, and
    /// neither of them had to be told what a button means. Two of these are the engine's own
    /// words rather than this game's - which is exactly why they can be offered here without
    /// this game having an opinion about undo.
    let private onwards =
        // The line above the buttons is here to be read, and it is also holding the box open:
        // a block of nothing but short captions comes out of the reader that builds panels
        // narrower than its own name, and a panel too narrow for its header is drawn without
        // one. Which is worth a sentence rather than a shrug - the block would still be there,
        // and nobody looking at it would know what it was called.
        [ Scene.quietly "each of these is a line you could type"
          Does("step", "step", Tone.Plainly)
          Does("step 10", "step 10", Tone.Plainly)
          Does("step 50", "step 50", Tone.Plainly)
          Does("undo", "undo", Tone.Plainly)
          Does("clear", "clear", Tone.Plainly)
          Does("restart", "restart", Tone.Plainly) ]

    // --- what a player may type ----------------------------------------------------------

    /// The commands, short. `help` has them at length, and both are written from this list so
    /// neither can quietly grow a command the other has never heard of.
    let private verbs =
        [ "f7", "turn cell f7 on, or off (or 'toggle f7')"
          "step, step 10", "let the rule run, once or ten times"
          "why f7", "what the rule will do with that cell, and why"
          "undo, redo", "walk the run back and forward"
          "clear", "sweep the board, to draw on it from nothing"
          "restart", "deal another soup; 'restart 42' deals that one"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "quit", "leave; the record is written and can be replayed" ]

    let commands = Scene.verbs verbs

    /// A paragraph as lines short enough for either terminal reader.
    let private wrapped text = Scene.paragraph 66 text

    let help =
        String.concat
            "\n"
            [ wrapped $"Conway's Game of Life, on a board of {Grid.Width} by {Grid.Height} with its edges joined."
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

    // --- the log ---------------------------------------------------------------------------

    let wording = Told.inWords Words.said Words.command

    // --- the whole screen ---------------------------------------------------------------

    let board margins _ (model: Model<Move, World, Notice>) =
        let world = Model.state model

        Stack
            [ Heading(heading world)
              Block(Blocks.board, [ grid world; Scene.noted margins Notes.board ])
              // The two narrow blocks side by side under the board, because the board wants the
              // whole width and neither of these wants half of it. A reader with no way to put
              // two things beside each other stacks them and nothing is lost.
              Beside
                  [ Block(Blocks.run, standing world @ [ Scene.noted margins Notes.rule ])
                    Block(Blocks.onwards, onwards) ]
              Scene.listing margins Blocks.commands commands
              Block(Blocks.log, Scene.log wording model) ]

    // --- the rest of what a player reads --------------------------------------------------

    let history _ (model: Model<Move, World, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  gen {entry.Turn}"
              Scene.cell Tone.Plainly (Words.command entry.Asked)
              Scene.cell Tone.Plainly (entry.Told |> List.map wording |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading (Model.state model))

    /// What the rule is about to do with one cell, and why.
    ///
    /// The one thing at this game that is worth asking and cannot be read off the board: the
    /// board shows what is alive, and the rule is about what is alive *around* a square. Which
    /// is the whole of Life - so a game whose every position is in plain sight still has
    /// something to explain, and this is it.
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

    // --- a table still filling up -----------------------------------------------------------
    //
    // Drawn from a list of who has arrived and nothing else, so there is no game in it to
    // differ about - and it is one line here rather than a screen.

    let waiting = Scene.waiting Words.seated

    // --- what this game brings to a page -----------------------------------------------------

    /// This game's own rules of drawing, and no more than that - which here is none at all.
    /// The board is a grid of aligned rows and everything else on the page - the blocks, the
    /// buttons, the notes - is a scene's, so all of it is styled at `Page`.
    let shell =
        { Title = "Life"
          Sheet = Page.tightRows
          Placeholder = "a cell to turn it on - 'f7' - or 'step 10' to run it on, or 'help'" }
