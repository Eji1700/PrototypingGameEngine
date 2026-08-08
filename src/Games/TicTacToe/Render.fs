namespace TCModel.TicTacToe

open System
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.TicTacToe

/// The game as plain text, and the words every other way of drawing it borrows.
///
/// The same split the other game keeps, and for the same reason: what a screen *says* -
/// the headings, the notes, the rules, the commands - is settled once here, and a view that
/// lays it out differently lays out these words rather than words of its own. Two screens
/// that disagree about what a block is called are two screens somebody has to read twice.
module Render =

    /// What each part of the screen is called. Named rather than written out, because three
    /// views draw them and a block that was renamed on one of them would look like a block
    /// that had gone missing.
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

    /// A line of prose broken to fit a given room. Here rather than left to whatever is
    /// showing it, because a view that lays a note out inside a panel has to know where the
    /// breaks are before it can draw the walls.
    let wrap room (text: string) =
        let put (lines, line) word =
            if line = "" then lines, word
            elif String.length line + 1 + String.length word <= room then lines, line + " " + word
            else lines @ [ line ], word

        let lines, last = text.Split ' ' |> Array.fold put ([], "")
        lines @ [ last ]

    // --- the heading ---------------------------------------------------------------------

    /// Whose turn it is, or how it ended. Every view opens with this line, so which of the
    /// two a screen is showing is never a question.
    let heading beholder session =
        match session with
        | InPlay play ->
            let yours = Session.seatOf play.ToPlay = beholder
            $"Turn {play.Turn} - {Words.seated yours (Session.seatOf play.ToPlay)} to play"
        | Finished(_, ending) -> $"The game is over: {Words.ending ending}"

    // --- the board -----------------------------------------------------------------------

    /// One square as it is drawn: the mark in it, or the number to type if it is free.
    let square board n =
        match Board.at n board with
        | Some mark -> Words.mark mark
        | None -> string n

    /// The board as a grid, worked out from the side rather than written out - so the one
    /// place the shape of this game is settled is `Squares`.
    let grid board =
        let row line =
            "     " + (line |> List.map (square board) |> String.concat " | ")

        let between = "    " + String.concat "+" (List.replicate Squares.Side "---")

        Squares.rows
        |> List.map row
        |> String.concat (Environment.NewLine + between + Environment.NewLine)

    // --- who is playing --------------------------------------------------------------------

    /// Both seats, with an arrow at whoever is to play and the reader's own marked. Two
    /// lines, and they carry the only count there is: how many squares each mark holds.
    let players beholder session =
        let acting = Session.active session
        let board = Session.board session

        Mark.all
        |> List.map (fun mark ->
            let seat = Session.seatOf mark
            let marker = if seat = acting && not (Session.isOver session) then "->" else "  "
            let held = Board.held mark board |> List.length
            let squares = if held = 1 then "1 square" else $"{held} squares"

            $"  {marker} {Words.seated (seat = beholder) seat, -8} {squares}")
        |> String.concat Environment.NewLine

    // --- what a player may type ----------------------------------------------------------

    /// The commands, short. `help` has them at length, and both are written from this list
    /// so neither can quietly grow a command the other has never heard of.
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
        |> String.concat Environment.NewLine

    let help =
        String.concat
            Environment.NewLine
            ([ "Noughts and crosses, on a board of nine."
               ""
               Notes.winning
               Notes.board
               ""
               "Crosses go first. There is nothing dealt and nothing hidden: both players are"
               "looking at the whole game, which is the only kind of game this is."
               ""
               "COMMANDS"
               commands ])

    // --- the log ---------------------------------------------------------------------------

    /// What the game has said lately, oldest first. Nothing here is a secret from anybody,
    /// so there is one wording and every seat gets it.
    let wording = Told.inWords Words.said Words.command

    let private log (model: Model<Move, Session, Notice>) =
        match model.Log with
        | [] -> [ $"  {nothingYet}" ]
        | notices -> notices |> List.rev |> List.map (fun notice -> $"  {wording notice}")

    // --- the whole screen ---------------------------------------------------------------

    let private block name lines =
        (name.ToString().ToUpperInvariant()) :: lines

    let model notes beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        let noted text =
            if notes then [ ""; $"  {text}" ] else []

        String.concat
            Environment.NewLine
            ([ $"=== {heading beholder session} ==="; "" ]
             @ block Blocks.board ([ ""; grid (Session.board session) ] @ noted Notes.board)
             @ [ "" ]
             @ block Blocks.players [ players beholder session ]
             @ [ "" ]
             @ block Blocks.commands [ commands ]
             @ noted Notes.winning
             @ [ "" ]
             @ block Blocks.log (log model)
             @ [ "" ])

    // --- the rest of what a player reads --------------------------------------------------

    let history beholder (model: Model<Move, Session, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            let told = entry.Told |> List.map wording |> String.concat " "

            $"  {entry.Ordinal, 3}  turn {entry.Turn}  {Words.player entry.Actor}: {Words.command entry.Asked}  {told}"

        String.concat
            Environment.NewLine
            (match Journal.entries model.Journal with
             | [] -> [ "RECORD"; $"  {nothingYet}" ]
             | entries ->
                 [ "RECORD" ]
                 @ (entries |> List.map entry)
                 @ [ ""; $"  {heading beholder (Model.state model)}" ])

    /// Nothing here needs explaining, and saying so is better than pretending there is a
    /// question. A game whose whole position is nine squares in plain sight has nowhere for
    /// working to hide.
    let nothingToExplain =
        "There is nothing here that needs working out: the board is nine squares in plain sight, and three in a row wins."

    // --- a table still filling up -----------------------------------------------------------

    module Filling =
        let title = "Waiting for the table to fill"

        let standing (seat: Waiting) =
            match seat.Expected, seat.Away with
            | true, _ -> "still to arrive"
            | _, true -> "here, but their console has gone"
            | _, _ -> "here"

        let stillToCome (seats: Waiting list) =
            match seats |> List.filter (fun seat -> seat.Expected) |> List.length with
            | 0 -> "Everybody is here."
            | 1 -> "1 more to come."
            | more -> $"{more} more to come."

    let waiting (seats: Waiting list) =
        let seat (seat: Waiting) =
            $"  {Words.seated seat.Yours seat.Player, -8} {Filling.standing seat}"

        String.concat
            Environment.NewLine
            ([ $"=== {Filling.title} ==="; "" ]
             @ (seats |> List.map seat)
             @ [ ""; $"  {Filling.stillToCome seats}"; "" ])
