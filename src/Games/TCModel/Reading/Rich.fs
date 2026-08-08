namespace TCModel.Domain

open System
open Spectre.Console
open Spectre.Console.Rendering
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing
// namespace, and both `Spectre.Console` and the command line's argument types carry
// names this game already uses - `Region`, `Open`, `View`.
open TCModel.Domain

/// The board built out of Spectre's own widgets: panels, tables and charts, rather than
/// one long block of text.
///
/// The honeycomb itself is kept. It is not decoration - a side shared between two regions
/// is a border and a corner touched is not, so the map is the only part of the screen that
/// says which regions a player may march between. A grid of tidy boxes would lose that, so
/// the map is `Render`'s, drawn once and coloured here. Everything standing around it is
/// built here from scratch.
///
/// Nothing in this file decides what a player may know. What is shown comes from
/// `Knowledge`, and what a notice says comes from `Render.wording`, the same as the plain
/// view - a second renderer is a second chance to leak, and the way not to take it is not
/// to write that reasoning down twice.
///
/// Nor does anything here decide what colour to use. Every one of them is read out of the
/// palette that comes in, which is the player's own, so two people at one table can be
/// drawn the same board in colours that look nothing alike.
module Rich =

    /// Characters to a region, and to half of one.
    ///
    /// `Board.layout` lies on a triangular lattice: a region is two half-columns wide and
    /// each row stands half a region across from the one above. Laid out that way the
    /// regions come out as brickwork, and a brick touches exactly six others - the two
    /// beside it and two on each of the rows above and below. Those six are its borders.
    /// So the offset is not decoration: drop it and the map stops saying where a player
    /// may march.
    let private across = 26
    let private half = across / 2

    /// The room inside a region's walls, once the border and its padding are taken off.
    let private inside = across - 4

    /// A title has less room than that, because it is written into the top wall and the
    /// wall wants a little of itself either side of it. A name given less room than it
    /// needs gives up its "The" rather than being cut short.
    let private titleRoom = across - 6

    /// How far the widest row of the map reaches, taken from the board rather than
    /// counted out here, so a board laid out differently still gets a screen that fits.
    let private mapAcross =
        Board.layout
        |> List.map (fun row -> (row |> List.map snd |> List.min) * half + List.length row * across)
        |> List.max

    /// Wide enough that the map is never folded, and wide enough besides that the two
    /// panels standing side by side have room for a bag laid out stone by stone.
    let private width = max (mapAcross + 4) 104

    /// How wide the chart of what is out of sight is drawn, and so how much room the two
    /// panels beside one another have. The note under it is wrapped to the same, because a
    /// paragraph broken narrower than the thing it explains reads as a column of scraps.
    let private breakdownAcross = 40

    let private esc (text: string) = Markup.Escape text

    let private markup (text: string) = Markup text :> IRenderable

    let private rows (all: IRenderable list) = Rows(all) :> IRenderable

    /// A panel is drawn in a quiet grey so that the stones inside it are what the eye goes
    /// to. Its title is not, and has to say so outright: a header takes the border's own
    /// style unless it is given a colour of its own, and a title in the same grey as the
    /// border is a title nobody reads. The spaces either side go inside the markup too -
    /// Spectre trims whatever is left outside it.
    let private titled title (content: IRenderable) =
        let panel = Panel(content)
        panel.Header <- PanelHeader $"[bold silver] {esc title} [/]"
        panel.Border <- BoxBorder.Rounded
        panel.BorderStyle <- Style(Color.Grey37)
        panel

    let private panel title content = titled title content :> IRenderable

    /// The same, but taking the whole width, so the blocks under one another line up.
    let private wide title content =
        let panel = titled title content
        panel.Expand <- true
        panel :> IRenderable

    /// Writing that is there to be read once: shown while the notes are on, and gone the
    /// moment they are turned off. What it says is `Render.Notes`, the same words the other
    /// two views show; the only thing decided here is how wide it is drawn.
    let private noted palette room (text: string) =
        markup ""
        :: (Render.wrap room text
            |> List.map (fun line -> markup (Tint.wrap (Ink.hidden palette) (esc line))))

    // --- stones -------------------------------------------------------------------------

    /// Stones laid out one by one, each in its own colour.
    let private laid palette pile =
        match Pile.toColors pile with
        | [] -> Tint.wrap (Ink.hidden palette) "-"
        | colors ->
            colors
            |> List.map (fun color -> Tint.wrap (Ink.ink palette color) (string (Words.glyph color)))
            |> String.concat " "

    /// The same, counted rather than laid out, for where there are too many to draw.
    let private counted palette pile =
        match Pile.toCounts pile with
        | [] -> Tint.wrap (Ink.hidden palette) "-"
        | counts ->
            counts
            |> List.map (fun (color, n) -> Tint.wrap (Ink.ink palette color) $"{Words.glyph color}x{n}")
            |> String.concat "  "

    /// Stones nobody can name, drawn as the stones they are. A closed bag of eight is
    /// eight of these: a player can see how much is in it and nothing else, which is
    /// exactly the state of affairs the game means them to be in.
    let private unnamed palette n =
        if n = 0 then
            Tint.wrap (Ink.hidden palette) "-"
        else
            Tint.wrap (Ink.hidden palette) (List.replicate n "?" |> String.concat " ")

    let private sighted palette =
        function
        | Open pile -> $"{laid palette pile}   ({Pile.total pile})"
        | Closed n -> $"{unnamed palette n}   ({n})"

    // --- the map ------------------------------------------------------------------------

    /// A region as a box of its own, bordered in the colour of whoever rules it - which
    /// is the one thing on a board worth seeing from across the room.
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

    /// One row of the map, shoved right by however many half-regions it stands in.
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

        // A blank column carrying the half-region offset, where there is one to carry.
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

    /// The Flag and the Axe, which border nothing and so stand outside the map. Here they
    /// are panels of their own rather than boxes drawn in text, which is what standing
    /// apart ought to look like.
    let private apart palette game =
        let held (region: Region) =
            // A panel has room to say who rules it in words, which the map has not - but
            // the words are `Words.rule`'s and the colouring is the same pass that colours
            // every other piece of prose here, so this is only where it is written down.
            let ruling = Ink.markup palette (Words.rule (Game.ruleOver region.Id game))

            // Padded to a width that clears the title, because a panel drawn no wider than
            // its own contents will cut its heading short to fit.
            let standing = laid palette (Game.stones region.Id game)
            let room = 16 - Pile.total (Game.stones region.Id game) * 2 |> max 1

            panel $"[{Words.number region.Id}] {region.Name}" (markup (standing + String(' ', room) + ruling))

        let standing = Board.apartRegions |> List.map held

        let grid = Grid()
        standing |> List.iter (fun _ -> grid.AddColumn() |> ignore)
        grid.AddRow(Array.ofList standing) |> ignore
        grid :> IRenderable

    // --- the players ----------------------------------------------------------------------

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

    // --- what is where --------------------------------------------------------------------

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

        // Every stone that is not on the map and not in the reader's own bag is somewhere
        // out there, and though nobody can say where, the colours are known exactly. That
        // is worth a picture rather than three numbers.
        let breakdown = BreakdownChart()
        breakdown.Width <- breakdownAcross

        for color, n in Pile.toCounts seen.Unseen do
            breakdown.AddItem(Words.color color, float n, Ink.color palette color) |> ignore

        rows [ table :> IRenderable; markup ""; breakdown :> IRenderable ]

    let private landRuled palette game =
        // How the land stands is the game's own reckoning, not this view's. A second
        // renderer counting it again for itself is a second chance to count it differently.
        let standing = Game.landStanding game

        let bars = BarChart()
        bars.Width <- width - 6

        for color in StoneColor.all do
            bars.AddItem(Words.color color, float (Map.find color standing.Ruled), Ink.color palette color)
            |> ignore

        bars.AddItem(Words.tied, float standing.Tied, Color.Grey37) |> ignore
        bars.AddItem(Words.unclaimed, float standing.Unclaimed, Color.Grey23) |> ignore
        bars :> IRenderable

    // --- everything the game has said ---------------------------------------------------

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

    // --- the whole screen ------------------------------------------------------------------

    let board palette notes (beholder: Player) model =
        let game = Playing.game model
        let active = Game.active game

        let seen =
            if Playing.isOver model then Knowledge.laidBare beholder game else Knowledge.seenBy beholder game

        let told = Render.wording beholder model

        let rule = Rule($"[bold]{esc (Render.heading beholder model)}[/]")
        rule.Justification <- Justify.Left
        rule.Style <- Style(Color.Grey37)

        /// Two panels side by side, which is room the map has already paid for.
        let beside left right =
            let grid = Grid()
            grid.AddColumn() |> ignore
            grid.AddColumn() |> ignore
            grid.AddRow(left, right) |> ignore
            grid :> IRenderable

        /// A note across the whole screen, and one inside a panel standing beside another.
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
            if notes then
                [ wide Render.Blocks.commands (plainly palette (Render.commands @ [ ""; "  " + Render.shorthand ])) ]
            else
                []

        rows (
            [ rule :> IRenderable
              wide Render.Blocks.map (rows ([ mapOf palette game ] @ mapNote))
              // Named but not boxed. Every other block here is a panel, and putting these two
              // inside one would be drawing a box around the thing that stands outside every
              // box on the board - so the name is written in a panel header's own style and
              // the Flag and the Axe are left standing on their own.
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
            @ [ wide Render.Blocks.log (log palette told model) ]
        )
        |> Tint.renderAt width

    // --- the rest of what a player reads --------------------------------------------------
    //
    // Every screen the game shows has to be answered here, not only the board. That is the
    // bargain of a wide seam: adding to the game means adding an endpoint and answering it
    // in every view, and the compiler says so if one is missed.

    /// The record of play, as a table: what was asked, by whom, and what came of it.
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

    /// The working behind who rules a region.
    let ruling palette regionId model =
        Tint.renderAt width (wide (Render.Blocks.region regionId) (plainly palette [ Render.explainRule regionId model ]))

    /// The rules and the commands, at length. The words are `Render`'s - they say what the
    /// game is, which is not a thing for a view to have an opinion about - laid out here.
    let rules palette =
        Tint.renderAt width (wide Render.Blocks.rules (plainly palette [ Render.help ]))

    /// A table still filling up, before there is any game to draw.
    let waiting palette (seats: Waiting list) =
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for seat in seats do
            let named = Words.seated seat.Yours seat.Player
            let who = if seat.Yours then Tint.wrap (Tint.yours palette) named else named

            // Somebody who has arrived is the good news on this screen, so they are the
            // one line on it drawn in a colour rather than held back in grey.
            let ink =
                if seat.Expected || seat.Away then Ink.hidden palette else Tint.yours palette

            table.AddRow(markup who, markup (Tint.wrap ink (esc (Render.Filling.standing seat))))
            |> ignore

        let footer = Tint.wrap (Ink.hidden palette) (esc (Render.Filling.stillToCome seats))

        Tint.renderAt width (panel Render.Filling.title (rows [ table :> IRenderable; markup ""; markup footer ]))
