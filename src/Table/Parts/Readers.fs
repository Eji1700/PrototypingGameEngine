namespace TCModel.Table

open System
open TCModel.Engine

module Readers =

    let private hush = Palette.slate


    // Drawing a `Walled` grid as a honeycomb rather than as a table.
    //
    // Spectre can draw a table, but not one whose rows are offset by half a cell and whose cells
    // join up where they belong to the same region. So this lays the whole thing out as a grid of
    // characters, works out every join from what is on either side of it, and hands back rows of
    // spans - which both the coloured and the plain readers can then print.
    module private Comb =

        type Facet =
            { Here: bool
              Shape: string option
              Tone: Tone
              Lines: Line list }

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

        let isMap (rows: Course list) =
            rows
            |> List.exists (fun row ->
                row.Cells
                |> List.exists (function
                    | Patch _ -> true
                    | _ -> false))

        // The line-drawing character for a join, from which of the four ways a wall runs out of it.
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

        // The same, for a terminal that is only promised ASCII.
        let bare up down left right =
            match up, down, left, right with
            | false, false, false, false -> ' '
            | false, false, _, _ -> '-'
            | _, _, false, false -> '|'
            | _ -> '+'

        /// Lay the rows out and draw them.
        ///
        /// Every cell is the same size, and a row's `Shift` is counted in halves of one - which is
        /// what lets a row sit between the two above it. So the cell width is forced odd, making
        /// the width with its right-hand wall even, and half of that a whole number of characters.
        let lay glyph across (rows: Course list) : Line list =
            let laid =
                rows
                |> List.map (fun row -> row.Shift, row.Cells |> List.map facet |> Array.ofList)

            let every = laid |> List.collect (fun (_, cells) -> List.ofArray cells)

            let tall =
                every
                |> List.fold (fun tall facet -> max tall (List.length facet.Lines)) 0
                |> max 1

            let widest =
                every
                |> List.collect (fun facet -> facet.Lines)
                |> List.fold (fun room line -> max room (String.length (Scene.plainText line))) 0

            let inner =
                let asked = max across widest |> max 3
                if (asked + 1) % 2 = 0 then asked else asked + 1

            let step = inner + 1

            let placed = laid |> List.map (fun (shift, cells) -> shift * step / 2, cells)

            let width =
                placed
                |> List.fold (fun width (start, cells) -> max width (start + Array.length cells * step + 1)) 1

            let deep = List.length placed


            let there (facet: Facet option) =
                match facet with
                | Some facet -> facet.Here
                | None -> false

            // Two cells of the same shape are one region drawn across several cells, so no wall is
            // drawn between them and text laid in one may run through the other.
            let joined (one: Facet option) (other: Facet option) =
                match one, other with
                | Some one, Some other when one.Here && other.Here ->
                    match one.Shape, other.Shape with
                    | Some shape, Some another -> shape = another
                    | _ -> false
                | _ -> false

            let wall one other =
                (there one || there other) && not (joined one other)

            let at (cells: Facet[]) index =
                if index >= 0 && index < Array.length cells then Some cells[index] else None

            let indexed (start, cells: Facet[]) column =
                let index, offset = (column - start) / step, (column - start) % step
                index, offset, cells

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

            let standing row column =
                if column < fst row then
                    false
                else
                    let index, offset, cells = indexed row column
                    offset = 0 && wall (at cells (index - 1)) (at cells index)

            let flanking row column =
                if column < fst row then
                    []
                else
                    let index, offset, cells = indexed row column
                    if offset = 0 then [ at cells (index - 1); at cells index ] else []

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


            // The whole drawing as characters and their tones: a wall line above each row of cells,
            // and one more under the last of them.
            let picture =
                Array.init (deep * (tall + 1) + 1) (fun _ -> Array.create width (' ', Tone.Quiet))

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
                        above
                        |> Option.map (fun row -> standing row column)
                        |> Option.defaultValue false

                    let down =
                        below
                        |> Option.map (fun row -> standing row column)
                        |> Option.defaultValue false

                    if up || down || along[column] then
                        let sides =
                            [ over[column]; under[column] ]
                            @ (above |> Option.map (fun row -> flanking row column) |> Option.defaultValue [])
                            @ (below |> Option.map (fun row -> flanking row column) |> Option.defaultValue [])

                        let drawn =
                            if up || down then
                                glyph up down (column > 0 && along[column - 1]) (column < width - 1 && along[column + 1])
                            else
                                glyph false false true true

                        picture[line][column] <- drawn, inked sides

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

            // Back from characters to spans: trailing blanks are dropped and runs of one tone are
            // gathered up, so a line comes out as a handful of spans rather than one span a letter.
            picture
            |> Array.toList
            |> List.map (fun line ->
                let last = line |> Array.tryFindIndexBack (fun (letter, _) -> letter <> ' ')

                match last with
                | None -> []
                | Some last ->
                    Array.sub line 0 (last + 1)
                    |> Array.fold
                        (fun spans (letter, tone) ->
                            match spans with
                            | (run: Span) :: rest when run.Tone = tone ->
                                { run with
                                    Text = run.Text + string letter }
                                :: rest
                            | _ -> { Text = string letter; Tone = tone } :: spans)
                        []
                    |> List.rev)


    module Plain =

        [<Literal>]
        let private Room = 76

        let private indent n (lines: string list) =
            let pad = String.replicate n " "
            lines |> List.map (fun line -> if line = "" then "" else pad + line)

        let private broken (text: string) =
            text.Replace("\r\n", "\n").Split '\n' |> List.ofArray

        let private centred room (text: string) =
            let spare = max 0 (room - String.length text)

            String.replicate (spare / 2) " "
            + text
            + String.replicate (spare - spare / 2) " "

        let private aligned (rows: Line list list) =
            let texts = rows |> List.map (List.map Scene.plainText)
            let columns = texts |> List.fold (fun most row -> max most (List.length row)) 0

            let widths =
                [ for column in 0 .. columns - 1 ->
                      texts
                      |> List.fold
                          (fun room row -> if column < List.length row then max room (String.length row[column]) else room)
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
            | Stack parts
            | Beside parts -> parts |> List.collect draw
            | Aligned rows -> aligned rows
            | Walled(across, rows) when Comb.isMap rows -> Comb.lay Comb.bare across rows |> List.map Scene.plainText
            | Walled(across, rows) -> grid across rows
            | Tile(title, _, body) ->
                (match title with
                 | Some title -> [ title ]
                 | None -> [])
                @ (body |> List.collect draw)
            | Patch(_, _, body) -> body |> List.collect draw
            | Big span -> [ span.Text ]
            | Does(caption, _, _) -> [ caption ]

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

                let across = String.replicate (shift * (inner + 3) / 2) " "

                [ for line in 0 .. tall - 1 ->
                      across
                      + (padded
                         |> List.map (fun cell -> " " + centred inner cell[line] + " ")
                         |> String.concat "|") ]

            let between =
                (List.replicate (drawn |> List.fold (fun most (_, cells) -> max most (List.length cells)) 0) wall
                 |> String.concat "+")

            drawn
            |> List.map laid
            |> List.reduce (fun above below -> above @ [ between ] @ below)

        let screen scene =
            draw scene |> String.concat Environment.NewLine


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

        let private quietly (text: string) = Tint.wrap (Palette.ink hush) (esc text)

        let private walled palette tone title (content: IRenderable) wide =
            let panel = Panel(content)

            match title with
            | Some title -> panel.Header <- PanelHeader $"[bold silver] {esc title} [/]"
            | None -> ()

            panel.Border <- BoxBorder.Rounded

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
            | Note text -> markup "" :: (Scene.wrap (room - 6) text |> List.map (quietly >> markup))
            | Written text -> [ markup (paint palette text) ]
            | Block(title, body) -> [ walled palette Tone.Plainly (Some title) (stacked paint palette room body) wide ]
            | Stack parts -> [ stacked paint palette room parts ]
            | Beside parts ->
                let share = max 20 (room / max 1 (List.length parts))
                let grid = Spectre.Console.Grid()

                for _ in parts do
                    grid.AddColumn() |> ignore

                grid.AddRow(
                    parts
                    |> List.map (fun part -> stacked paint palette share [ part ])
                    |> Array.ofList
                )
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
            | Walled(across, rows) when Comb.isMap rows ->
                [ Comb.lay Comb.box across rows
                  |> List.map (line paint palette)
                  |> String.concat "\n"
                  |> markup ]
            | Walled(across, rows) -> [ grid paint palette room across rows ]
            | Tile(title, tone, body) ->
                match title with
                | Some _ -> [ walled palette tone title (spread paint palette room wide body) wide ]
                | None -> [ spread paint palette room wide body ]
            | Patch(_, _, body) -> [ stacked paint palette room body ]
            | Big text -> [ markup (Tint.wrap "bold" (span paint palette text)) ]
            | Does(caption, _, tone) -> [ markup (span paint palette { Text = caption; Tone = tone }) ]

        and private spread paint palette room wide parts =
            Spectre.Console.Rows(parts |> List.collect (render paint palette room wide)) :> IRenderable

        and private stacked paint palette room parts = spread paint palette room false parts

        and private grid paint palette room across rows =
            let inner = max across 5

            let roomy cell =
                Spectre.Console.Rows([ markup " " ] @ render paint palette inner true cell @ [ markup " " ]) :> IRenderable

            let walls (row: Course) =
                let table = Table()
                table.Border <- TableBorder.Rounded
                table.BorderStyle <- Style(Color.Grey37)
                table.ShowHeaders <- false

                for _ in row.Cells do
                    table.AddColumn(TableColumn("").Width(inner).Centered()) |> ignore

                table.AddRow(row.Cells |> List.map roomy |> Array.ofList) |> ignore

                table :> IRenderable

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
            | Beside parts -> [ Elem.div [ Attr.class' "beside" ] (parts |> List.map (fun part -> Elem.div [] (draw part))) ]
            | Aligned rows ->
                [ Elem.div
                      [ Attr.class' "rows" ]
                      (rows
                       |> List.map (fun row ->
                           Elem.div [ Attr.class' "row" ] (row |> List.map (fun cell -> Elem.span [] (cell |> List.map span))))) ]
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
                                        [ Page.attr "style" $"margin-left: calc(var(--cell) * {row.Shift} / 2)" ]))
                               (row.Cells |> List.collect draw))) ]
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
            | Patch(_, tone, body) -> draw (Tile(None, tone, body))
            | Big text -> [ Elem.span (Attr.class' "big" :: toned text.Tone) [ Text.enc text.Text ] ]
            | Does(caption, line, _) -> [ Page.types line caption ]

        let screen scene = Page.screen (draw scene)

        let aside scene = Page.aside (draw scene)


    [<NoComparison; NoEquality>]
    type Scenes<'Move, 'State, 'Notice> =
        { Board: Margins -> PlayerId -> Model<'Move, 'State, 'Notice> -> Scene
          History: PlayerId -> Model<'Move, 'State, 'Notice> -> Scene
          Answer: PlayerId -> string -> Model<'Move, 'State, 'Notice> -> Scene
          Rules: Scene
          Waiting: Waiting list -> Scene

          Marking: Marking

          Width: int }

    let views (scenes: Scenes<'Move, 'State, 'Notice>) palette : View<'Move, 'State, 'Notice> list =
        let paint = Tint.painter scenes.Marking

        let inPanels = Panels.screen (Tint.markup scenes.Marking) scenes.Width palette

        [ { Name = "plain"
            Describe = "plain text, and nothing this terminal has to understand"
            Shown = AtATerminal
            Palette = palette
            Board = fun margins seat model -> Plain.screen (scenes.Board margins seat model)
            History = fun seat model -> Plain.screen (scenes.History seat model)
            Answer = fun seat asked model -> Plain.screen (scenes.Answer seat asked model)
            Rules = Plain.screen scenes.Rules
            Says = id
            Waiting = fun seats -> Plain.screen (scenes.Waiting seats) }

          { Name = "rich"
            Describe = "panels, walls and colour, for a terminal that can show them"
            Shown = AtATerminal
            Palette = palette
            Board = fun margins seat model -> inPanels (scenes.Board margins seat model)
            History = fun seat model -> inPanels (scenes.History seat model)
            Answer = fun seat asked model -> inPanels (scenes.Answer seat asked model)
            Rules = inPanels scenes.Rules
            Says = paint palette
            Waiting = fun seats -> inPanels (scenes.Waiting seats) }

          { Name = "html"
            Describe = "a page, for a player reading in a browser"
            Shown = InABrowser
            Palette = palette
            Board = fun margins seat model -> Pages.screen (scenes.Board margins seat model)
            History = fun seat model -> Pages.aside (scenes.History seat model)
            Answer = fun seat asked model -> Pages.aside (scenes.Answer seat asked model)
            Rules = Pages.aside scenes.Rules
            Says = Page.says
            Waiting = fun seats -> Pages.screen (scenes.Waiting seats) } ]
