namespace TCModel.TicTacToe

open System
open Spectre.Console
open Spectre.Console.Rendering
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and both `Spectre.Console` and the command line's argument types carry names this game
// already uses - `Open`.
open TCModel.TicTacToe

/// The board built out of Spectre's own widgets rather than one block of text.
///
/// A board of nine squares is exactly what a table of three columns is for, so this one
/// really is a table: the squares get walls, the marks get room, and a free square shows the
/// number to type in the quiet colour so the eye passes over it.
///
/// Nothing here decides what a screen *says*. The headings, the notes, the commands and the
/// rules are all `Render`'s words, laid out differently - a second renderer is a second
/// chance for two screens to disagree about what the game is called.
module Rich =

    /// Wide enough for the board, the two seats beside it and a line of the log, and no
    /// wider: this board is small, and a screen padded out to fill a terminal is a screen
    /// with a lot of nothing in the middle of it.
    let private width = 72

    /// Characters across one square, which is what makes the board look like a board rather
    /// than three columns of letters.
    let private across = 9

    let private esc (text: string) = Markup.Escape text

    let private markup (text: string) = Markup text :> IRenderable

    let private rows (all: IRenderable list) = Rows(all) :> IRenderable

    let private titled title (content: IRenderable) =
        let panel = Panel(content)
        panel.Header <- PanelHeader $"[bold silver] {esc title} [/]"
        panel.Border <- BoxBorder.Rounded
        panel.BorderStyle <- Style(Color.Grey37)
        panel

    let private panel title content = titled title content :> IRenderable

    let private wide title content =
        let panel = titled title content
        panel.Expand <- true
        panel :> IRenderable

    /// Writing that is there to be read once, in the quiet colour, and gone the moment the
    /// notes are turned off.
    let private noted palette room (text: string) =
        markup ""
        :: (Render.wrap room text
            |> List.map (fun line -> markup (Tint.wrap (Ink.hidden palette) (esc line))))

    // --- the board ------------------------------------------------------------------------

    /// One square: the mark in it in that mark's colour, or the number to type in the quiet
    /// one. A free square is a thing to do rather than a thing to read, which is why it is
    /// drawn as the line a player would type for it.
    let private cell palette board square =
        let text =
            match Board.at square board with
            | Some mark -> Tint.wrap (Ink.ink palette mark) (Words.mark mark)
            | None -> Tint.wrap (Ink.hidden palette) (string square)

        // A blank line either side, so a square is square rather than a letter in a slot.
        // Where it sits across is the column's business, which is why the column is centred
        // and nothing is counted here.
        markup ("\n" + text + "\n")

    let private grid palette board =
        let table = Table()
        table.Border <- TableBorder.Rounded
        table.BorderStyle <- Style(Color.Grey37)
        table.ShowHeaders <- false

        for _ in 1 .. Squares.Side do
            table.AddColumn(TableColumn("").Width(across).Centered()) |> ignore

        for row in Squares.rows do
            table.AddRow(row |> List.map (cell palette board) |> Array.ofList) |> ignore

        table :> IRenderable

    // --- who is playing ---------------------------------------------------------------------

    /// Both seats, the arrow on whoever is to play and the reader's own marked - the same
    /// two facts the plain view lays out in two lines, in a table so the columns line up.
    let private players palette beholder session =
        let acting = Session.active session
        let board = Session.board session
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for mark in Mark.all do
            let seat = Session.seatOf mark

            let marker =
                if seat = acting && not (Session.isOver session) then Tint.wrap (Tint.yours palette) "->" else "  "

            let named =
                let words = Words.seated (seat = beholder) seat
                if seat = beholder then Tint.wrap (Tint.yours palette) (esc words) else Ink.markup palette words

            let held = Board.held mark board |> List.length

            table.AddRow(markup marker, markup named, markup (Tint.wrap (Ink.hidden palette) $"{held} of 9"))
            |> ignore

        table :> IRenderable

    // --- what the game has said ----------------------------------------------------------

    let private log palette (model: Model<Move, Session, Notice>) =
        match model.Log with
        | [] -> markup (Tint.wrap (Ink.hidden palette) Render.nothingYet)
        | notices ->
            notices
            |> List.rev
            |> List.map (Render.wording >> Ink.markup palette)
            |> String.concat Environment.NewLine
            |> markup

    let private plainly palette (lines: string list) =
        lines |> String.concat Environment.NewLine |> Ink.markup palette |> markup

    // --- the whole screen -------------------------------------------------------------------

    let board palette notes beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model

        let rule = Rule($"[bold]{esc (Render.heading beholder session)}[/]")
        rule.Justification <- Justify.Left
        rule.Style <- Style(Color.Grey37)

        /// Two panels side by side: the board is narrow, and the seats standing under it
        /// would leave the right-hand half of the screen empty.
        let beside left right =
            let table = Grid()
            table.AddColumn() |> ignore
            table.AddColumn() |> ignore
            table.AddRow(left, right) |> ignore
            table :> IRenderable

        // Wrapped to the panel it is going inside rather than to the screen: a note broken
        // wider than the box holding it is broken twice, once here and once by Spectre, and
        // comes out as a column of scraps.
        let noted text =
            if notes then noted palette (width / 2 - 6) text else []

        let commands =
            if notes then [ wide Render.Blocks.commands (plainly palette [ Render.commands ]) ] else []

        rows (
            [ rule :> IRenderable
              beside
                  (panel Render.Blocks.board (grid palette (Session.board session)))
                  (panel Render.Blocks.players (rows (players palette beholder session :: noted Render.Notes.winning))) ]
            @ (if notes then [ markup (Tint.wrap (Ink.hidden palette) (esc Render.Notes.board)) ] else [])
            @ commands
            @ [ wide Render.Blocks.log (log palette model) ]
        )
        |> Tint.renderAt width

    // --- the rest of what a player reads ------------------------------------------------------

    let history palette beholder (model: Model<Move, Session, Notice>) =
        match Journal.entries model.Journal with
        | [] -> Tint.renderAt width (panel "The record" (markup (Tint.wrap (Ink.hidden palette) Render.nothingYet)))
        | entries ->

        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for entry in entries do
            let outcome =
                entry.Told
                |> List.map (Render.wording >> Ink.markup palette)
                |> String.concat Environment.NewLine

            table.AddRow(
                markup (Tint.wrap (Ink.hidden palette) (esc $"{entry.Ordinal}  turn {entry.Turn}")),
                markup (Ink.markup palette $"{Words.player entry.Actor}: {Words.command entry.Asked}"),
                markup outcome
            )
            |> ignore

        Tint.renderAt
            width
            (wide
                "The record"
                (rows
                    [ table :> IRenderable
                      markup ""
                      markup (Tint.wrap (Ink.hidden palette) (esc (Render.heading beholder (Model.state model)))) ]))

    let rules palette =
        Tint.renderAt width (wide "The rules" (plainly palette [ Render.help ]))

    let answer palette =
        Tint.renderAt width (wide "The board" (plainly palette [ Render.nothingToExplain ]))

    let waiting palette (seats: Waiting list) =
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for seat in seats do
            let named = Words.seated seat.Yours seat.Player

            let who =
                if seat.Yours then Tint.wrap (Tint.yours palette) (esc named) else Ink.markup palette named

            table.AddRow(markup who, markup (Tint.wrap (Ink.hidden palette) (esc (Render.Filling.standing seat))))
            |> ignore

        Tint.renderAt
            width
            (wide
                Render.Filling.title
                (rows
                    [ table :> IRenderable
                      markup ""
                      markup (Tint.wrap (Ink.hidden palette) (esc (Render.Filling.stillToCome seats))) ]))
