namespace TCModel.TicTacToe

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.TicTacToe

/// Every screen this game has, described once.
///
/// There were three of these files. `Render` wrote the board out as text, `Rich` built the
/// same board out of Spectre's panels and tables, and `Html` built it a third time out of
/// elements - eighteen screens between them, for a game of nine squares, and the words on all
/// three had to be kept in step by hand.
///
/// What is here instead is a `Scene`: what each screen is *made of*, and nothing about what
/// it looks like. A block is a block, a square is a tile with a tone, a free square is a
/// control that types its own number. `Readers` turns that into text, into widgets and into
/// a page, and the three cannot disagree about what a block is called or which notes there
/// are, because there is one description and all three walk it.
///
/// The other game still writes its own three, and should: a honeycomb laid out by counting
/// characters into columns is exactly the screen no general reader would think of. That is
/// the trade this seam offers rather than imposes.
module Render =

    /// What each part of the screen is called. Named rather than written out, because the
    /// readers draw them and a block renamed in one place would look like a block that had
    /// gone missing.
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

    let nothingYet = "nothing yet"

    // --- the heading ---------------------------------------------------------------------

    /// Whose turn it is, or how it ended. Every screen opens with this line, so which of the
    /// two is being shown is never a question.
    let heading beholder session =
        match session with
        | InPlay play ->
            let yours = Session.seatOf play.ToPlay = beholder
            $"Turn {play.Turn} - {Words.seated yours (Session.seatOf play.ToPlay)} to play"
        | Finished(_, ending) -> $"The game is over: {Words.ending ending}"

    // --- the board ---------------------------------------------------------------------------

    /// One square.
    ///
    /// Taken, it is the mark in it, drawn large and in that mark's colour. Free, it is a
    /// control that types its own number - which is also the number printed on it, so what a
    /// player clicks and what a player could have typed are one thing said once. A reader
    /// with buttons draws a button and a reader without draws the number, and neither of them
    /// had to be told that the number is the move.
    let private square board n =
        match Board.at n board with
        | Some mark -> Tile(None, Tone.Slot(Ink.key mark), [ Big(Span.slot (Ink.key mark) (Words.mark mark)) ])
        | None -> Tile(None, Tone.Quiet, [ Does(string n, string n, Tone.Quiet) ])

    /// The board, worked out from the side rather than written out - so the one place the
    /// shape of this game is settled is `Squares`.
    let private grid board =
        // Nine across is what makes a square look square rather than a letter in a slot, at a
        // reader with the room for it. The one counting characters has not, and will give a
        // square the one character in it.
        Walled(9, Squares.rows |> List.map (fun row -> Scene.squared (row |> List.map (square board))))

    // --- who is playing -----------------------------------------------------------------------

    /// Both seats, with an arrow at whoever is to play and the reader's own marked, and the
    /// only count there is: how many squares each mark holds.
    let private players beholder session =
        let acting = Session.active session
        let board = Session.board session

        Mark.all
        |> List.map (fun mark ->
            let seat = Session.seatOf mark
            let yours = seat = beholder
            let held = Board.held mark board |> List.length

            [ Scene.cell Tone.Yours (if seat = acting && not (Session.isOver session) then "->" else "")
              Scene.cell (if yours then Tone.Yours else Tone.Slot(Ink.key mark)) (Words.seated yours seat)
              Scene.cell Tone.Quiet $"{held} of {Squares.Side * Squares.Side}" ])
        |> Aligned

    // --- what a player may type ----------------------------------------------------------

    /// The commands, short. `help` has them at length, and both are written from this list so
    /// neither can quietly grow a command the other has never heard of.
    let private verbs =
        [ "5", "take square 5 (or 'place 5')"
          "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide this and every note"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "resign", "give the game up, but write it down"
          "quit", "leave, saving first" ]

    let commands =
        verbs
        |> List.map (fun (verb, says) -> $"  %-12s{verb} %s{says}")
        |> String.concat "\n"

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

    // --- the log ---------------------------------------------------------------------------

    /// What the game has said lately, oldest first. Nothing here is a secret from anybody, so
    /// there is one wording and every seat gets it.
    let wording = Told.inWords Words.said Words.command

    let private log (model: Model<Move, Session, Notice>) =
        match model.Log with
        | [] -> [ Scene.quietly nothingYet ]
        | notices -> notices |> List.rev |> List.map (wording >> Scene.says)

    // --- the whole screen ---------------------------------------------------------------

    let board notes beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        Stack
            [ Heading(heading beholder session)
              // The board is narrow and the two seats would leave half a screen empty beside
              // it, so they are asked for side by side. A reader with no way to do that
              // stacks them and nothing is lost.
              Beside
                  [ Block(Blocks.board, [ grid (Session.board session); Scene.noted notes Notes.board ])
                    Block(Blocks.players, [ players beholder session; Scene.noted notes Notes.winning ]) ]
              Block(Blocks.commands, [ Written commands ])
              Block(Blocks.log, log model) ]

    // --- the rest of what a player reads --------------------------------------------------

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

    /// Nothing here needs explaining, and saying so is better than pretending there is a
    /// question. A game whose whole position is nine squares in plain sight has nowhere for
    /// working to hide.
    let nothingToExplain =
        "There is nothing here that needs working out: the board is nine squares in plain sight, and three in a row wins."

    let answer = Block("The board", [ Scene.says nothingToExplain ])

    let rules = Block("The rules", [ Written help ])

    // --- a table still filling up -----------------------------------------------------------
    //
    // Drawn from a list of who has arrived and nothing else, so there is no game in it to
    // differ about - and it is one line here rather than three screens. It had been written
    // out twice, once per game, and the two had already begun to drift: one said a console
    // had "gone" and the other that it had "dropped".

    let waiting = Scene.waiting Words.seated

    // --- what this game brings to a page -----------------------------------------------------------

    /// This game's own rules of drawing, and no more than that.
    ///
    /// One line, where it was fourteen. Every shape on the page - the walls round a cell, the
    /// grid they sit in, the glyph drawn large, two blocks beside each other - is a scene's
    /// and is styled once at `Page`; what is left is the one thing that is this game's, which
    /// is that nine squares want to be bigger than a cell in general has any reason to be.
    let private sheet =
        """
.grid { --cell: 4.6rem; }
"""

    let shell =
        { Title = "Noughts and crosses"
          Sheet = sheet
          Placeholder = "click a square, or type its number - 1 to 9, or 'help'" }
