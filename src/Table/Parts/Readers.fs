namespace TCModel.Table

open System
open TCModel.Engine

/// The three ways of reading a `Scene`, written once for every game there will ever be.
///
/// This is the other half of the bargain `Scene` offers. A game says what a screen is made of
/// and these say what it looks like: `Plain` counts characters into columns, `Panels` builds
/// Spectre's widgets, and `Pages` builds elements. None of them knows what game it is drawing
/// and none of them ever will - a scene has no stones in it, no squares, and no rules.
///
/// What that buys is the thing the sweep in `view.fsx` was written to catch after the fact. A
/// block cannot be shown by two readers and quietly missing from the third, because there is
/// one description and all three walk it. A note cannot be worded one way here and another
/// way there. A control cannot offer a line the parser would refuse, because the line is in
/// the description and the caption is drawn from it.
///
/// A game is not obliged to take any of this. `Turncoats` draws its map by counting characters
/// into a honeycomb and will go on doing so; the `View` seam is untouched and a game that
/// wants a screen no reader here would think of writes one. What is on offer is that a game
/// which does not want to should not have to.
module Readers =

    /// The quiet colour: what a count beside a name, a free square's number and the writing
    /// under a board are drawn in.
    ///
    /// Not a slot, and deliberately. Every game wants one of these and no two agree what it
    /// is *for* - one game's is the colour of what is held back from a reader, another's is
    /// the colour of whichever piece is not being talked about - so it is the readers' rather
    /// than the palette's, and a game with an opinion about it says so with a slot of its own.
    let private hush = Palette.slate

    // --- a grid that is a map -------------------------------------------------------------------

    /// A grid of patches, laid out character by character.
    ///
    /// A grid of tiles is a table: rows, columns, a wall between every pair of cells, and both
    /// of the readers that draw at a terminal have something that draws tables. A grid of
    /// patches is not a table at all. A region takes as many cells as its borders need, the
    /// walls inside it are not walls, and the wall round it is drawn in whatever the region is
    /// drawn in - so a province somebody owns is a shape outlined in their colour rather than
    /// four or five boxes that happen to share a name.
    ///
    /// Neither reader was handed anything that does that, so it is done here once and each of
    /// them hands it an alphabet: box characters for a terminal that can show them, and the
    /// dashes and bars of a board drawn on a typewriter for one that would rather not. What is
    /// where is worked out in exactly one place, which is the only way the two can be looked at
    /// side by side and be the same map.
    module private Comb =

        /// One cell, once the scene in it has been read: whether there is anything there at
        /// all, which region it belongs to, what it is drawn in, and its lines.
        type Facet =
            { Here: bool
              Shape: string option
              Tone: Tone
              Lines: Line list }

        /// Whatever is inside a cell, as lines. A cell of a map holds a line or two of text and
        /// nothing that has to be laid out - which is the whole reason a map can be drawn by
        /// counting characters at all.
        let rec private lines scene : Line list =
            match scene with
            | Blank -> []
            | Say line -> [ line ]
            | Big span -> [ [ span ] ]
            | Note text -> [ [ Span.quiet text ] ]
            | Heading text -> [ [ Span.plainly text ] ]
            | Does(caption, _, tone) -> [ [ Span.toned tone caption ] ]
            | Written text ->
                text.Replace("\r\n", "\n").Split '\n'
                |> List.ofArray
                |> List.map (fun line -> [ Span.plainly line ])
            | Block(_, body)
            | Stack body
            | Beside body
            | Tile(_, _, body)
            | Patch(_, _, body) -> body |> List.collect lines
            | Aligned rows -> rows |> List.map List.concat
            | Walled _ -> []

        let private facet scene : Facet =
            match scene with
            | Blank ->
                { Here = false
                  Shape = None
                  Tone = Tone.Quiet
                  Lines = [] }
            | Patch(shape, tone, body) ->
                { Here = true
                  Shape = Some shape
                  Tone = tone
                  Lines = body |> List.collect lines }
            | Tile(_, tone, body) ->
                { Here = true
                  Shape = None
                  Tone = tone
                  Lines = body |> List.collect lines }
            | other ->
                { Here = true
                  Shape = None
                  Tone = Tone.Quiet
                  Lines = lines other }

        /// Whether this grid is a map at all. One with no patch in it is a table, and is left
        /// to whatever the reader draws tables with - which is what keeps every board that was
        /// drawn before any of this exactly as it was.
        let isMap (rows: Course list) =
            rows
            |> List.exists (fun row ->
                row.Cells
                |> List.exists (function
                    | Patch _ -> true
                    | _ -> false))

        /// Box characters, with the rounded corners the panels round everything else use.
        let box up down left right =
            match up, down, left, right with
            | true, true, true, true -> '┼'
            | true, true, true, false -> '┤'
            | true, true, false, true -> '├'
            | true, true, false, false -> '│'
            | true, false, true, true -> '┴'
            | true, false, true, false -> '╯'
            | true, false, false, true -> '╰'
            | true, false, false, false -> '│'
            | false, true, true, true -> '┬'
            | false, true, true, false -> '╮'
            | false, true, false, true -> '╭'
            | false, true, false, false -> '│'
            | false, false, false, false -> ' '
            | false, false, _, _ -> '─'

        /// The same map with nothing in it a terminal has to understand.
        let bare up down left right =
            match up, down, left, right with
            | false, false, false, false -> ' '
            | false, false, _, _ -> '-'
            | _, _, false, false -> '|'
            | _ -> '+'

        /// The map, as lines of spans - one span to a run of characters drawn the same.
        let lay glyph across (rows: Course list) : Line list =
            let laid =
                rows
                |> List.map (fun row -> row.Shift, row.Cells |> List.map facet |> Array.ofList)

            let every = laid |> List.collect (fun (_, cells) -> List.ofArray cells)

            let tall =
                every |> List.fold (fun tall facet -> max tall (List.length facet.Lines)) 0 |> max 1

            let widest =
                every
                |> List.collect (fun facet -> facet.Lines)
                |> List.fold (fun room line -> max room (String.length (Scene.plainText line))) 0

            // A cell and the wall beside it come to an even number of characters, so that a row
            // sitting half a cell across from the one above lands on a character rather than
            // between two. That is the one thing this arithmetic has to get right: everything
            // else here is only true if a half-step is a whole number of columns.
            let inner =
                let asked = max across widest |> max 3
                if (asked + 1) % 2 = 0 then asked else asked + 1

            let step = inner + 1

            let placed = laid |> List.map (fun (shift, cells) -> shift * step / 2, cells)

            let width =
                placed
                |> List.fold (fun width (start, cells) -> max width (start + Array.length cells * step + 1)) 1

            let deep = List.length placed

            // --- what is where -------------------------------------------------------------

            let there (facet: Facet option) =
                match facet with
                | Some facet -> facet.Here
                | None -> false

            /// Two cells are one region when they are both there and both carry the same shape.
            /// Not the same tone: two provinces held by one power are two provinces and the
            /// border between them is real.
            let joined (one: Facet option) (other: Facet option) =
                match one, other with
                | Some one, Some other when one.Here && other.Here ->
                    match one.Shape, other.Shape with
                    | Some shape, Some another -> shape = another
                    | _ -> false
                | _ -> false

            /// A wall stands between two cells unless there is nothing on either side of it, or
            /// unless the two are one region - in which case it is the inside of that region
            /// and is not drawn at all.
            let wall one other = (there one || there other) && not (joined one other)

            let at (cells: Facet[]) index =
                if index >= 0 && index < Array.length cells then Some cells[index] else None

            let indexed (start, cells: Facet[]) column =
                let index, offset = (column - start) / step, (column - start) % step
                index, offset, cells

            /// Which cell covers this column: the one it is inside, or - where the column is a
            /// wall that is not there - the region lying on both sides of it.
            let covering row column =
                if column < fst row then
                    None
                else
                    let index, offset, cells = indexed row column

                    if offset <> 0 then
                        at cells index
                    else
                        let one, other = at cells (index - 1), at cells index
                        if joined one other then one else None

            /// The vertical wall standing at this column, if there is one.
            let standing row column =
                if column < fst row then
                    false
                else
                    let index, offset, cells = indexed row column
                    offset = 0 && wall (at cells (index - 1)) (at cells index)

            /// The cells a wall column has on either side of it, for deciding what to draw it
            /// in - a corner touches up to four regions and takes the colour of the first that
            /// has one.
            let flanking row column =
                if column < fst row then
                    []
                else
                    let index, offset, cells = indexed row column
                    if offset = 0 then [ at cells (index - 1); at cells index ] else []

            /// What a wall is drawn in: the colour of whichever side of it has one. A region
            /// somebody owns is outlined in their colour, and the sea it borders has no opinion.
            let inked sides =
                sides
                |> List.tryPick (fun (facet: Facet option) ->
                    match facet with
                    | Some facet when facet.Here ->
                        match facet.Tone with
                        | Tone.Slot _
                        | Tone.Yours -> Some facet.Tone
                        | _ -> None
                    | _ -> None)
                |> Option.defaultValue Tone.Quiet

            // --- and the picture of it -------------------------------------------------------

            let picture =
                Array.init (deep * (tall + 1) + 1) (fun _ -> Array.create width (' ', Tone.Quiet))

            // The line between two rows, which is the bottom of one and the top of the next at
            // once. Every wall on it belongs to a cell above or a cell below, and the corners
            // are always tees rather than crosses: rows sit half a cell apart, so no wall above
            // ever lines up with a wall below.
            for gap in 0..deep do
                let above = if gap = 0 then None else Some placed[gap - 1]
                let below = if gap = deep then None else Some placed[gap]
                let line = gap * (tall + 1)

                let over =
                    Array.init width (fun column -> above |> Option.bind (fun row -> covering row column))

                let under =
                    Array.init width (fun column -> below |> Option.bind (fun row -> covering row column))

                let along = Array.init width (fun column -> wall over[column] under[column])

                for column in 0 .. width - 1 do
                    let up =
                        above |> Option.map (fun row -> standing row column) |> Option.defaultValue false

                    let down =
                        below |> Option.map (fun row -> standing row column) |> Option.defaultValue false

                    if up || down || along[column] then
                        let sides =
                            [ over[column]; under[column] ]
                            @ (above |> Option.map (fun row -> flanking row column) |> Option.defaultValue [])
                            @ (below |> Option.map (fun row -> flanking row column) |> Option.defaultValue [])

                        // A corner takes its arms from the columns beside it rather than from
                        // its own, so a wall that ends where a region carries on downwards
                        // turns a corner instead of leaving a stub pointing at nothing.
                        let drawn =
                            if up || down then
                                glyph
                                    up
                                    down
                                    (column > 0 && along[column - 1])
                                    (column < width - 1 && along[column + 1])
                            else
                                glyph false false true true

                        picture[line][column] <- drawn, inked sides

            // Then the rows themselves: the walls down the sides of each cell, and what is
            // written in it.
            for row in 0 .. deep - 1 do
                let start, cells = placed[row]
                let top = row * (tall + 1) + 1

                for edge in 0 .. Array.length cells do
                    let one, other = at cells (edge - 1), at cells edge

                    if wall one other then
                        for line in 0 .. tall - 1 do
                            picture[top + line][start + edge * step] <- glyph true true false false, inked [ one; other ]

                for index in 0 .. Array.length cells - 1 do
                    let facet = cells[index]

                    if facet.Here then
                        let left = start + index * step + 1

                        facet.Lines
                        |> List.truncate tall
                        |> List.iteri (fun line spans ->
                            let spare = max 0 (inner - String.length (Scene.plainText spans))
                            let mutable column = left + spare / 2

                            for span in spans do
                                for letter in span.Text do
                                    if column >= 0 && column < width then
                                        picture[top + line][column] <- letter, span.Tone

                                    column <- column + 1)

            // Runs of the same tone gathered into one span each, and the padding at the end of
            // a line thrown away: a map padded to the width of its widest row would push
            // whatever is drawn beside it sideways for no reason at all.
            picture
            |> Array.toList
            |> List.map (fun line ->
                let last =
                    line |> Array.tryFindIndexBack (fun (letter, _) -> letter <> ' ')

                match last with
                | None -> []
                | Some last ->
                    Array.sub line 0 (last + 1)
                    |> Array.fold
                        (fun spans (letter, tone) ->
                            match spans with
                            | (run: Span) :: rest when run.Tone = tone ->
                                { run with Text = run.Text + string letter } :: rest
                            | _ -> { Text = string letter; Tone = tone } :: spans)
                        []
                    |> List.rev)

    // --- blocks of text ---------------------------------------------------------------------

    /// The board written out as text, with nothing in it a terminal has to understand.
    ///
    /// Colour is not this reader's business at all: it lays a screen out by counting
    /// characters into columns, and an escape code inserted mid-layout pushes everything after
    /// it sideways. Tones are read and dropped.
    module Plain =

        [<Literal>]
        let private Room = 76

        let private indent n (lines: string list) =
            let pad = String.replicate n " "
            lines |> List.map (fun line -> if line = "" then "" else pad + line)

        let private broken (text: string) =
            text.Replace("\r\n", "\n").Split '\n' |> List.ofArray

        /// A cell's text in the middle of the room it is given, which is what makes a grid of
        /// them look like a board rather than a list.
        let private centred room (text: string) =
            let spare = max 0 (room - String.length text)
            String.replicate (spare / 2) " " + text + String.replicate (spare - spare / 2) " "

        /// Cells with the columns lined up and no walls between them.
        let private aligned (rows: Line list list) =
            let texts = rows |> List.map (List.map Scene.plainText)
            let columns = texts |> List.fold (fun most row -> max most (List.length row)) 0

            let widths =
                [ for column in 0 .. columns - 1 ->
                      texts
                      |> List.fold
                          (fun room row ->
                              if column < List.length row then max room (String.length row[column]) else room)
                          0 ]

            texts
            |> List.map (fun row ->
                row
                |> List.mapi (fun column text -> text.PadRight widths[column])
                |> String.concat "  "
                |> fun line -> line.TrimEnd())

        let rec private draw scene : string list =
            match scene with
            | Blank -> []
            | Heading text -> [ $"=== {text} ==="; "" ]
            | Say line -> [ Scene.plainText line ]
            | Note text -> "" :: Scene.wrap Room text
            | Written text -> broken text
            | Block(title, body) -> (title.ToUpperInvariant() :: indent 2 (body |> List.collect draw)) @ [ "" ]
            // Two things beside each other are two things one after another here. Nothing is
            // lost but the width, which a reader that lays out by counting characters has
            // less of than anybody.
            | Stack parts
            | Beside parts -> parts |> List.collect draw
            | Aligned rows -> aligned rows
            // A map is laid out in one place for both readers that draw at a terminal, and
            // this one asks for it in dashes and bars. What it gets back is the same map the
            // colour screen draws, character for character.
            | Walled(across, rows) when Comb.isMap rows ->
                Comb.lay Comb.bare across rows |> List.map Scene.plainText
            | Walled(across, rows) -> grid across rows
            | Tile(title, _, body) ->
                (match title with
                 | Some title -> [ title ]
                 | None -> [])
                @ (body |> List.collect draw)
            | Patch(_, _, body) -> body |> List.collect draw
            | Big span -> [ span.Text ]
            // A control is the line a player would have typed, and at a terminal that is
            // exactly what it is: the caption is what to type, so it is written out and the
            // line behind it is already the same words.
            | Does(caption, _, _) -> [ caption ]

        // `across` is not read here, and that is the one place a reader is right to ignore
        // what a scene asked for. It is how much room a cell *wants*, which the two readers
        // with room to spare honour - and this one has the least room of anybody, laying a
        // screen out by counting characters into columns with a terminal's width to fit it
        // all in. So a cell gets exactly what is in it and no padding it did not earn.
        and private grid _ (rows: Course list) =
            let drawn = rows |> List.map (fun row -> row.Shift, row.Cells |> List.map draw)

            let inner =
                drawn
                |> List.collect snd
                |> List.collect id
                |> List.fold (fun room line -> max room (String.length line)) 0
                |> max 1

            let wall = String.replicate (inner + 2) "-"

            let laid (shift, cells: string list list) =
                let tall = cells |> List.fold (fun tall cell -> max tall (List.length cell)) 0

                let padded =
                    cells
                    |> List.map (fun cell -> cell @ List.replicate (tall - List.length cell) "")

                // Half a cell across per step, walls counted in, which is the whole of what a
                // shift means. A row that sits square under the one above shifts by nothing
                // and this comes to no spaces at all.
                let across = String.replicate (shift * (inner + 3) / 2) " "

                [ for line in 0 .. tall - 1 ->
                      across
                      + (padded
                         |> List.map (fun cell -> " " + centred inner cell[line] + " ")
                         |> String.concat "|") ]

            let between =
                (List.replicate (drawn |> List.fold (fun most (_, cells) -> max most (List.length cells)) 0) wall
                 |> String.concat "+")

            drawn |> List.map laid |> List.reduce (fun above below -> above @ [ between ] @ below)

        let screen scene =
            draw scene |> String.concat Environment.NewLine

    // --- panels, tables and walls -------------------------------------------------------------

    /// The same screen built out of Spectre's own widgets: blocks get panels, grids get walls,
    /// and everything gets the colour the scene said it was drawn in.
    ///
    /// `paint` is how the game colours its own prose. A scene says what most things are drawn
    /// in outright, but prose arrives already written - a line of the log, a page of rules -
    /// and the only thing that knows a faction's name is a colour is the game. So `Plainly`
    /// means "however the game paints prose" here, and means nothing at all to the other two.
    module Panels =

        open Spectre.Console
        open Spectre.Console.Rendering

        let private esc (text: string) = Markup.Escape text

        let private markup (text: string) = Markup text :> IRenderable

        let private ink palette tone =
            match tone with
            | Tone.Plainly -> None
            | Tone.Quiet -> Some(Palette.ink hush)
            | Tone.Yours -> Some(Tint.yours palette)
            | Tone.Slot key -> Some(Palette.inkOf key palette)

        let private span paint palette (span: Span) =
            match ink palette span.Tone with
            | Some style -> Tint.wrap style (esc span.Text)
            | None -> paint palette span.Text

        let private line paint palette (line: Line) =
            line |> List.map (span paint palette) |> String.concat ""

        let private quietly (text: string) =
            Tint.wrap (Palette.ink hush) (esc text)

        let private walled palette tone title (content: IRenderable) wide =
            let panel = Panel(content)

            match title with
            | Some title -> panel.Header <- PanelHeader $"[bold silver] {esc title} [/]"
            | None -> ()

            panel.Border <- BoxBorder.Rounded

            // The wall takes the tone's own colour where there is one, and the quiet grey
            // where there is not. Read back through `Style.Parse` because a tone comes out of
            // the palette as the word Spectre knows the colour by, and Spectre is the one
            // that knows what each of those means.
            panel.BorderStyle <-
                match ink palette tone with
                | Some style -> Style.Parse style
                | None -> Style(Color.Grey37)

            panel.Expand <- wide
            panel :> IRenderable

        let rec private render paint palette room wide scene : IRenderable list =
            match scene with
            | Blank -> []
            | Heading text ->
                let rule = Rule($"[bold]{esc text}[/]")
                rule.Justification <- Justify.Left
                rule.Style <- Style(Color.Grey37)
                [ rule :> IRenderable ]
            | Say text -> [ markup (line paint palette text) ]
            | Note text ->
                markup ""
                :: (Scene.wrap (room - 6) text |> List.map (quietly >> markup))
            | Written text -> [ markup (paint palette text) ]
            | Block(title, body) ->
                [ walled palette Tone.Plainly (Some title) (stacked paint palette room body) wide ]
            | Stack parts -> [ stacked paint palette room parts ]
            | Beside parts ->
                // Each gets its share of the room, which is what the notes inside them are
                // broken to. A note wrapped to the screen and then again to the box holding it
                // comes out as a column of scraps.
                let share = max 20 (room / max 1 (List.length parts))
                let grid = Spectre.Console.Grid()

                for _ in parts do
                    grid.AddColumn() |> ignore

                grid.AddRow(parts |> List.map (fun part -> stacked paint palette share [ part ]) |> Array.ofList)
                |> ignore

                [ grid :> IRenderable ]
            | Aligned rows ->
                let table = Table()
                table.Border <- TableBorder.None
                table.ShowHeaders <- false

                let columns = rows |> List.fold (fun most row -> max most (List.length row)) 0

                for _ in 1..columns do
                    table.AddColumn(TableColumn "") |> ignore

                for row in rows do
                    let cells =
                        row @ List.replicate (columns - List.length row) []
                        |> List.map (line paint palette >> markup)

                    table.AddRow(Array.ofList cells) |> ignore

                [ table :> IRenderable ]
            // A map is drawn rather than tabled. Spectre's tables have one wall in one colour
            // and no way to leave a wall out, which is exactly the two things a map needs -
            // so the characters are counted out in `Comb` and coloured here, and every region
            // comes out as one shape in the colour of whoever owns it.
            | Walled(across, rows) when Comb.isMap rows ->
                [ Comb.lay Comb.box across rows
                  |> List.map (line paint palette)
                  |> String.concat "\n"
                  |> markup ]
            | Walled(across, rows) -> [ grid paint palette room across rows ]
            | Tile(title, tone, body) ->
                match title with
                | Some _ -> [ walled palette tone title (stacked paint palette room body) false ]
                // A cell with nothing to call it is drawn without walls of its own: it is
                // already sitting inside a grid, and a wall inside a wall is two walls.
                | None -> [ stacked paint palette room body ]
            // Only ever reached by a patch drawn on its own, outside the map it belongs to.
            | Patch(_, _, body) -> [ stacked paint palette room body ]
            // A terminal cannot make a letter bigger, so what it can do instead is make it
            // stand out. The room round it comes from the grid rather than from here - a
            // glyph that padded itself would leave a row holding one three lines tall and the
            // row beneath it one, which is a board that has stopped being square.
            | Big text -> [ markup (Tint.wrap "bold" (span paint palette text)) ]
            | Does(caption, _, tone) -> [ markup (span paint palette { Text = caption; Tone = tone }) ]

        and private stacked paint palette room parts =
            Spectre.Console.Rows(parts |> List.collect (render paint palette room false)) :> IRenderable

        and private grid paint palette room across rows =
            let inner = max across 5

            /// A cell with a line of room above and below it, which is what makes a grid of
            /// them look like a board rather than three columns of letters. It is the grid's
            /// job and not the cell's: done in the cell, a row holding a glyph would stand
            /// three lines tall beside a row of bare numbers standing one.
            // A space rather than nothing, because nothing is what Spectre folds away: an
            // empty markup renders no segment at all and the room asked for never arrives.
            let roomy cell =
                Spectre.Console.Rows([ markup " " ] @ render paint palette inner false cell @ [ markup " " ])
                :> IRenderable

            let walls (row: Course) =
                let table = Table()
                table.Border <- TableBorder.Rounded
                table.BorderStyle <- Style(Color.Grey37)
                table.ShowHeaders <- false

                for _ in row.Cells do
                    table.AddColumn(TableColumn("").Width(inner).Centered()) |> ignore

                table.AddRow(
                    row.Cells
                    |> List.map roomy
                    |> Array.ofList
                )
                |> ignore

                table :> IRenderable

            // Where every row sits square under the one above there is one table and the walls
            // join up, which is what a board should look like. Where the rows are shifted -
            // a map on a lattice, half a cell across per row - they cannot be one table, so
            // each is drawn and moved across on its own.
            if rows |> List.forall (fun row -> row.Shift = 0) then
                let table = Table()
                table.Border <- TableBorder.Rounded
                table.BorderStyle <- Style(Color.Grey37)
                table.ShowHeaders <- false

                let columns = rows |> List.fold (fun most row -> max most (List.length row.Cells)) 0

                for _ in 1..columns do
                    table.AddColumn(TableColumn("").Width(inner).Centered()) |> ignore

                for row in rows do
                    let cells =
                        row.Cells @ List.replicate (columns - List.length row.Cells) Blank
                        |> List.map roomy

                    table.AddRow(Array.ofList cells) |> ignore

                table :> IRenderable
            else
                Spectre.Console.Rows(
                    rows
                    |> List.map (fun row ->
                        Padder(walls row).Padding(Padding(row.Shift * (inner + 2) / 2, 0, 0, 0)) :> IRenderable)
                )
                :> IRenderable

        let screen paint width palette scene =
            render paint palette width true scene
            |> Spectre.Console.Rows
            |> Tint.renderAt width

    // --- a page ---------------------------------------------------------------------------------

    /// The same screen built out of elements, for a player reading in a browser.
    ///
    /// The colours are not written in. Every tone comes out as one of the custom properties
    /// `Page` writes into the document's own head, so one board is built however many people
    /// are reading it and each of them reads it in colours of their own.
    module Pages =

        open Falco.Markup

        let private toned tone =
            match tone with
            | Tone.Plainly -> []
            | Tone.Quiet -> [ Attr.class' "quiet" ]
            | Tone.Yours -> [ Page.attr "style" "color: var(--yours)" ]
            | Tone.Slot key -> [ Page.attr "style" $"color: var(--{key})" ]

        let private span (span: Span) =
            Elem.span (toned span.Tone) [ Text.enc span.Text ]

        let rec private draw scene : XmlNode list =
            match scene with
            | Blank -> []
            | Heading text -> [ Elem.h1 [] [ Text.enc text ] ]
            | Say line -> [ Elem.div [ Attr.class' "said" ] (line |> List.map span) ]
            | Note text -> [ Page.note text ]
            | Written text -> [ Page.lines text ]
            | Block(title, body) -> [ Page.block title (body |> List.collect draw) ]
            | Stack parts -> [ Elem.div [] (parts |> List.collect draw) ]
            | Beside parts ->
                [ Elem.div
                      [ Attr.class' "beside" ]
                      (parts |> List.map (fun part -> Elem.div [] (draw part))) ]
            | Aligned rows ->
                [ Elem.div
                      [ Attr.class' "rows" ]
                      (rows
                       |> List.map (fun row -> Elem.div [ Attr.class' "row" ] (row |> List.map (fun cell -> Elem.span [] (cell |> List.map span))))) ]
            | Walled(_, rows) ->
                [ Elem.div
                      [ Attr.class' "grid" ]
                      (rows
                       |> List.map (fun row ->
                           Elem.div
                               (Attr.class' "row"
                                :: (if row.Shift = 0 then
                                        []
                                    else
                                        // Half a cell across per step, said in the page's own
                                        // arithmetic so that the cell can be whatever size the
                                        // sheet makes it and the offset still lands right.
                                        [ Page.attr "style" $"margin-left: calc(var(--cell) * {row.Shift} / 2)" ]))
                               (row.Cells |> List.collect draw)))
                  ]
            | Tile(title, tone, body) ->
                let walls =
                    match tone with
                    | Tone.Plainly -> []
                    | Tone.Quiet -> [ Page.attr "style" "border-color: var(--edge)" ]
                    | Tone.Yours -> [ Page.attr "style" "border-color: var(--yours)" ]
                    | Tone.Slot key -> [ Page.attr "style" $"border-color: var(--{key})" ]

                [ Elem.div
                      (Attr.class' "tile" :: walls)
                      ((match title with
                        | Some title -> [ Elem.h3 [] [ Text.enc title ] ]
                        | None -> [])
                       @ (body |> List.collect draw)) ]
            // A patch is a tile here, drawn in its region's colour, and the shape it is part
            // of is not drawn at all. The cells of a page's grid do not touch - they are laid
            // out with a gap between them and corners rounded off - so there is no wall
            // between two of them to leave out. What a browser gets is every cell of a region
            // outlined in the colour of whoever owns it, which is the half of this that
            // survives being drawn in boxes with room round them.
            | Patch(_, tone, body) -> draw (Tile(None, tone, body))
            | Big text -> [ Elem.span (Attr.class' "big" :: toned text.Tone) [ Text.enc text.Text ] ]
            | Does(caption, line, _) -> [ Page.types line caption ]

        let screen scene = Page.screen (draw scene)

        let aside scene = Page.aside (draw scene)

    // --- the three of them, as a game is asked for them --------------------------------------

    /// Every screen a game describes, in scenes rather than in text.
    ///
    /// The same five endpoints `View` has, less the two that are not scenes: `Says` is one
    /// line of prose the game has already written, and `Palette` is not a screen at all.
    [<NoComparison; NoEquality>]
    type Scenes<'Move, 'State, 'Notice> =
        {
            Board: Margins -> PlayerId -> Model<'Move, 'State, 'Notice> -> Scene
            History: PlayerId -> Model<'Move, 'State, 'Notice> -> Scene
            Answer: string -> Model<'Move, 'State, 'Notice> -> Scene
            Rules: Scene
            Waiting: Waiting list -> Scene

            /// What this game colours in prose, which is the one thing no scene can say. A
            /// scene names the tone of everything it lays out, but a line of the log arrives
            /// already written and only the game knows that a word in it is a faction.
            Marking: Marking

            /// How wide a board is drawn at a terminal. The one number a reader cannot work
            /// out for itself: nothing here knows what console the text is going to, and it
            /// may be going down a wire to one nobody here can ask about.
            Width: int
        }

    /// The three ways of drawing a game that describes its screens rather than writing them.
    ///
    /// A game says `Views = Readers.views scenes` and has all three. Which is the whole of
    /// what this is for: adding a way of showing the game is now a fourth entry here rather
    /// than a fourth function in every game there is.
    let views (scenes: Scenes<'Move, 'State, 'Notice>) palette : View<'Move, 'State, 'Notice> list =
        // Built once and kept, because a game's patterns are compiled and a board is painted
        // every turn. Asking for this per board would recompile the lot on each one.
        let paint = Tint.painter scenes.Marking

        let inPanels = Panels.screen (Tint.markup scenes.Marking) scenes.Width palette

        [ { Name = "plain"
            Describe = "plain text, and nothing this terminal has to understand"
            Shown = AtATerminal
            Palette = palette
            Board = fun margins seat model -> Plain.screen (scenes.Board margins seat model)
            History = fun seat model -> Plain.screen (scenes.History seat model)
            Answer = fun asked model -> Plain.screen (scenes.Answer asked model)
            Rules = Plain.screen scenes.Rules
            Says = id
            Waiting = fun seats -> Plain.screen (scenes.Waiting seats) }

          { Name = "rich"
            Describe = "panels, walls and colour, for a terminal that can show them"
            Shown = AtATerminal
            Palette = palette
            Board = fun margins seat model -> inPanels (scenes.Board margins seat model)
            History = fun seat model -> inPanels (scenes.History seat model)
            Answer = fun asked model -> inPanels (scenes.Answer asked model)
            Rules = inPanels scenes.Rules
            Says = paint palette
            Waiting = fun seats -> inPanels (scenes.Waiting seats) }

          // This one takes no palette into its endpoints, and it is the only one that does
          // not. A page carries its colours in its own head and every fragment draws in those,
          // so one board serves however many people are reading it.
          { Name = "html"
            Describe = "a page, for a player reading in a browser"
            Shown = InABrowser
            Palette = palette
            Board = fun margins seat model -> Pages.screen (scenes.Board margins seat model)
            History = fun seat model -> Pages.aside (scenes.History seat model)
            Answer = fun asked model -> Pages.aside (scenes.Answer asked model)
            Rules = Pages.aside scenes.Rules
            Says = Page.says
            Waiting = fun seats -> Pages.screen (scenes.Waiting seats) } ]
