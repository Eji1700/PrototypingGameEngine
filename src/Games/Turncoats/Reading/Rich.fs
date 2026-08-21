namespace TCModel.Turncoats

open System
open Spectre.Console
open Spectre.Console.Rendering
open TCModel.Engine
open TCModel.Table
open TCModel.Turncoats

module Rich =

    let private across = 26
    let private half = across / 2

    let private inside = across - 4

    let private titleRoom = across - 6

    let private mapAcross =
        Board.layout
        |> List.map (fun row -> (row |> List.map snd |> List.min) * half + List.length row * across)
        |> List.max

    let private width = max (mapAcross + 4) 104

    let private breakdownAcross = 40

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

    let private noted palette room (text: string) =
        markup ""
        :: (Render.wrap room text
            |> List.map (fun line -> markup (Tint.wrap (Ink.hidden palette) (esc line))))


    let private laid palette pile =
        match Pile.toColors pile with
        | [] -> Tint.wrap (Ink.hidden palette) "-"
        | colors ->
            colors
            |> List.map (fun color -> Tint.wrap (Ink.ink palette color) (string (Words.glyph color)))
            |> String.concat " "

    let private counted palette pile =
        match Pile.toCounts pile with
        | [] -> Tint.wrap (Ink.hidden palette) "-"
        | counts ->
            counts
            |> List.map (fun (color, n) -> Tint.wrap (Ink.ink palette color) $"{Words.glyph color}x{n}")
            |> String.concat "  "

    let private unnamed palette n =
        if n = 0 then
            Tint.wrap (Ink.hidden palette) "-"
        else
            Tint.wrap (Ink.hidden palette) (List.replicate n "?" |> String.concat " ")

    let private sighted palette =
        function
        | Open pile -> $"{laid palette pile}   ({Pile.total pile})"
        | Closed n -> $"{unnamed palette n}   ({n})"


    let private regionPanel palette game (region: Region) =
        let border =
            match region.Kind, Game.ruleOver region.Id game with
            | Dead, _ -> Color.Grey23
            | _, RuledBy color -> Ink.color palette color
            | _, (Contested _ | Unclaimed) -> Color.Grey37

        let panel =
            Panel(markup (Ink.markup palette (Render.standingIn inside game region)))

        panel.Header <- PanelHeader $"[bold silver] {esc (Render.regionTitle titleRoom region)} [/]"
        panel.Border <- BoxBorder.Rounded
        panel.BorderStyle <- Style(border)
        panel.Padding <- Padding(1, 0, 1, 0)
        panel.Width <- Nullable across
        panel :> IRenderable

    let private mapRow palette game (cells: (RegionId * int) list) =
        let grid = Grid()
        grid.Expand <- false

        let column width =
            let sized = GridColumn()
            sized.Width <- Nullable width
            sized.Padding <- Padding(0, 0, 0, 0)
            sized.NoWrap <- true
            grid.AddColumn sized |> ignore

        let offset = cells |> List.map snd |> List.min

        let lead =
            if offset > 0 then
                column (offset * half)
                [ markup " " ]
            else
                []

        cells |> List.iter (fun _ -> column across)

        let panels = cells |> List.map (fst >> Board.region >> regionPanel palette game)

        grid.AddRow(Array.ofList (lead @ panels)) |> ignore
        grid :> IRenderable

    let private mapOf palette game =
        Board.layout |> List.map (mapRow palette game) |> rows

    let private apart palette game =
        let held (region: Region) =
            let ruling = Ink.markup palette (Words.rule (Game.ruleOver region.Id game))

            let standing = laid palette (Game.stones region.Id game)
            let room = 16 - Pile.total (Game.stones region.Id game) * 2 |> max 1

            panel $"[{Words.number region.Id}] {region.Name}" (markup (standing + String(' ', room) + ruling))

        let standing = Board.apartRegions |> List.map held

        let grid = Grid()
        standing |> List.iter (fun _ -> grid.AddColumn() |> ignore)
        grid.AddRow(Array.ofList standing) |> ignore
        grid :> IRenderable


    let private players palette (seen: Knowledge) active model =
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for playerId, bag in seen.Bags do
            let marker = if playerId = active then Tint.wrap (Tint.yours palette) "->" else " "

            let yours = playerId = seen.Beholder
            let named = Words.seated yours playerId
            let name = if yours then Tint.wrap (Tint.yours palette) named else named

            table.AddRow(markup marker, markup name, markup (sighted palette bag)) |> ignore

        let run =
            match Playing.session model with
            | InPlay play ->
                let said = Render.negotiationRun play (Playing.game model)
                [ markup (Tint.wrap (Ink.hidden palette) (esc said)) ]
            | Finished _ -> []

        rows ([ table :> IRenderable ] @ run)


    let private supply palette (seen: Knowledge) =
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        let line label what =
            table.AddRow(markup (Tint.wrap (Ink.hidden palette) (esc label)), markup what)
            |> ignore

        line Render.Supply.onTheBoard (counted palette (Position.total seen.Position))

        line
            Render.Supply.inReserve
            (match seen.Reserve with
             | Open pile -> counted palette pile
             | Closed n -> Tint.wrap (Ink.hidden palette) $"?x{n}")

        line Render.Supply.outOfSight (counted palette seen.Unseen)

        let breakdown = BreakdownChart()
        breakdown.Width <- breakdownAcross

        for color, n in Pile.toCounts seen.Unseen do
            breakdown.AddItem(Words.color color, float n, Ink.color palette color) |> ignore

        rows [ table :> IRenderable; markup ""; breakdown :> IRenderable ]

    let private landRuled palette game =
        let standing = Game.landStanding game

        let bars = BarChart()
        bars.Width <- width - 6

        for color in StoneColor.all do
            bars.AddItem(Words.color color, float (Map.find color standing.Ruled), Ink.color palette color)
            |> ignore

        bars.AddItem(Words.tied, float standing.Tied, Color.Grey37) |> ignore
        bars.AddItem(Words.unclaimed, float standing.Unclaimed, Color.Grey23) |> ignore
        bars :> IRenderable


    let private log palette told (model: Model) =
        match model.Log with
        | [] -> markup (Tint.wrap (Ink.hidden palette) Render.nothingYet)
        | notices ->
            notices
            |> List.rev
            |> List.map (told >> Ink.markup palette)
            |> String.concat Environment.NewLine
            |> markup

    let private plainly palette (lines: string list) =
        lines |> String.concat Environment.NewLine |> Ink.markup palette |> markup


    let board palette (margins: Margins) (beholder: Player) model =
        let notes = margins.Notes
        let game = Playing.game model
        let active = Game.active game

        let seen =
            if Playing.isOver model then Knowledge.laidBare beholder game else Knowledge.seenBy beholder game

        let told = Render.wording beholder model

        let rule = Rule($"[bold]{esc (Render.heading beholder model)}[/]")
        rule.Justification <- Justify.Left
        rule.Style <- Style(Color.Grey37)

        let beside left right =
            let grid = Grid()
            grid.AddColumn() |> ignore
            grid.AddColumn() |> ignore
            grid.AddRow(left, right) |> ignore
            grid :> IRenderable

        let noted room text =
            if notes then noted palette room text else []

        let wideNote = noted (width - 6)
        let besideNote = noted breakdownAcross

        let mapNote = wideNote (Render.Notes.map + " " + Render.Notes.bordered)

        let hiddenNote = if Playing.isOver model then [] else besideNote Render.Notes.supply

        let result =
            if Playing.isOver model then
                [ wide Render.Blocks.result (plainly palette (Render.result game)) ]
            else
                []

        let commands =
            if margins.Commands then
                [ wide Render.Blocks.commands (plainly palette (Render.commands @ [ ""; "  " + Render.shorthand ])) ]
            else
                []

        rows (
            [ rule :> IRenderable
              wide Render.Blocks.map (rows ([ mapOf palette game ] @ mapNote))
              rows (
                  markup (Tint.wrap "bold silver" (esc Render.Blocks.apart))
                  :: apart palette game
                  :: wideNote Render.Notes.apart
              )
              beside
                  (panel Render.Blocks.players (players palette seen active.Id model))
                  (panel Render.Blocks.supply (rows ([ supply palette seen ] @ hiddenNote)))
              wide Render.Blocks.landRuled (rows (landRuled palette game :: wideNote Render.Notes.landRuled)) ]
            @ result
            @ commands
            @ (if margins.Logged then [ wide Render.Blocks.log (log palette told model) ] else [])
        )
        |> Tint.renderAt width


    let history palette (beholder: Player) model =
        let told = Render.wording beholder model

        match Journal.entries model.Journal with
        | [] -> Tint.renderAt width (panel Render.Blocks.record (markup (Tint.wrap (Ink.hidden palette) Render.nothingYet)))
        | entries ->

        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for entry in entries do
            let asked = Words.command entry.Asked

            let outcome =
                match entry.Told with
                | [] -> ""
                | notices ->
                    notices
                    |> List.map (told >> Ink.markup palette)
                    |> String.concat Environment.NewLine

            table.AddRow(
                markup (Tint.wrap (Ink.hidden palette) (esc $"{entry.Ordinal}  turn {entry.Turn}")),
                markup (Ink.markup palette $"{Words.player entry.Actor}: {asked}"),
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
                      markup (Tint.wrap (Ink.hidden palette) (esc (Render.recordStanding model))) ]))

    let ruling palette regionId model =
        Tint.renderAt width (wide (Render.Blocks.region regionId) (plainly palette [ Render.explainRule regionId model ]))

    let rules palette =
        Tint.renderAt width (wide Render.Blocks.rules (plainly palette [ Render.help ]))

    let waiting palette (seats: Waiting list) =
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for seat in seats do
            let named = Words.seated seat.Yours seat.Player
            let who = if seat.Yours then Tint.wrap (Tint.yours palette) named else named

            let ink =
                if seat.Expected || seat.Away then Ink.hidden palette else Tint.yours palette

            table.AddRow(markup who, markup (Tint.wrap ink (esc (Render.Filling.standing seat))))
            |> ignore

        let footer = Tint.wrap (Ink.hidden palette) (esc (Render.Filling.stillToCome seats))

        Tint.renderAt width (panel Render.Filling.title (rows [ table :> IRenderable; markup ""; markup footer ]))
