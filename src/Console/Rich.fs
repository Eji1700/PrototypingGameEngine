namespace TCModel.Console

open System
open Spectre.Console
open Spectre.Console.Rendering
open TCModel.Domain
open TCModel.App

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
    /// moment they are turned off.
    let private note (text: string) =
        markup (Tint.wrap Tint.Hidden (esc text))

    // --- stones -------------------------------------------------------------------------

    /// Stones laid out one by one, each in its own colour.
    let private laid pile =
        match Pile.toColors pile with
        | [] -> Tint.wrap Tint.Hidden "-"
        | colors ->
            colors
            |> List.map (fun color -> Tint.wrap (Tint.ink color) (string (Words.glyph color)))
            |> String.concat " "

    /// The same, counted rather than laid out, for where there are too many to draw.
    let private counted pile =
        match Pile.toCounts pile with
        | [] -> Tint.wrap Tint.Hidden "-"
        | counts ->
            counts
            |> List.map (fun (color, n) -> Tint.wrap (Tint.ink color) $"{Words.glyph color}x{n}")
            |> String.concat "  "

    /// Stones nobody can name, drawn as the stones they are. A closed bag of eight is
    /// eight of these: a player can see how much is in it and nothing else, which is
    /// exactly the state of affairs the game means them to be in.
    let private unnamed n =
        if n = 0 then
            Tint.wrap Tint.Hidden "-"
        else
            Tint.wrap Tint.Hidden (List.replicate n "?" |> String.concat " ")

    let private sighted =
        function
        | Open pile -> $"{laid pile}   ({Pile.total pile})"
        | Closed n -> $"{unnamed n}   ({n})"

    // --- the map ------------------------------------------------------------------------

    /// A region as a box of its own, bordered in the colour of whoever rules it - which
    /// is the one thing on a board worth seeing from across the room.
    let private regionPanel game (region: Region) =
        let border =
            match region.Kind, Game.ruleOver region.Id game with
            | Dead, _ -> Color.Grey23
            | _, RuledBy color -> Tint.color color
            | _, (Contested _ | Unclaimed) -> Color.Grey37

        let panel = Panel(markup (Tint.markup (Render.standingIn inside game region)))
        panel.Header <- PanelHeader $"[bold silver] {esc (Render.regionTitle titleRoom region)} [/]"
        panel.Border <- BoxBorder.Rounded
        panel.BorderStyle <- Style(border)
        panel.Padding <- Padding(1, 0, 1, 0)
        panel.Width <- Nullable across
        panel :> IRenderable

    /// One row of the map, shoved right by however many half-regions it stands in.
    let private mapRow game (cells: (RegionId * int) list) =
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

        let panels =
            cells |> List.map (fst >> Board.region >> regionPanel game)

        grid.AddRow(Array.ofList (lead @ panels)) |> ignore
        grid :> IRenderable

    let private mapOf game =
        Board.layout |> List.map (mapRow game) |> rows

    /// The Flag and the Axe, which border nothing and so stand outside the map. Here they
    /// are panels of their own rather than boxes drawn in text, which is what standing
    /// apart ought to look like.
    let private apart game =
        let held (region: Region) =
            let ruling =
                match Game.ruleOver region.Id game with
                | RuledBy color -> Tint.wrap $"bold {Tint.ink color}" (Words.color color)
                | Contested tied ->
                    (tied |> List.map (fun c -> Tint.wrap (Tint.ink c) (Words.color c)) |> String.concat ", ")
                    + " level"
                | Unclaimed -> Tint.wrap Tint.Hidden "unclaimed"

            // Padded to a width that clears the title, because a panel drawn no wider than
            // its own contents will cut its heading short to fit.
            let standing = laid (Game.stones region.Id game)
            let room = 16 - Pile.total (Game.stones region.Id game) * 2 |> max 1

            panel $"[{Words.number region.Id}] {region.Name}" (markup (standing + String(' ', room) + ruling))

        let standing =
            Board.regions
            |> List.filter (fun region -> RegionKind.isIsolated region.Kind)
            |> List.map held

        let grid = Grid()
        standing |> List.iter (fun _ -> grid.AddColumn() |> ignore)
        grid.AddRow(Array.ofList standing) |> ignore
        grid :> IRenderable

    // --- the players ----------------------------------------------------------------------

    let private players (seen: Knowledge) active model =
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for playerId, bag in seen.Bags do
            let marker = if playerId = active then Tint.wrap Tint.Yours "->" else " "

            let name =
                if playerId = seen.Beholder then
                    Tint.wrap Tint.Yours $"{Words.player playerId} (you)"
                else
                    Words.player playerId

            table.AddRow(markup marker, markup name, markup (sighted bag)) |> ignore

        let run =
            match Model.session model with
            | InPlay play ->
                let seats = Game.playerCount (Model.game model)

                [ markup (Tint.wrap Tint.Hidden (esc $"negotiations in a row: {play.Negotiations} of {seats}")) ]
            | Finished _ -> []

        rows ([ table :> IRenderable ] @ run)

    // --- what is where --------------------------------------------------------------------

    let private supply (seen: Knowledge) =
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        let line label what =
            table.AddRow(markup (Tint.wrap Tint.Hidden (esc label)), markup what) |> ignore

        line "on the board" (counted (Position.total seen.Position))

        line
            "in reserve"
            (match seen.Reserve with
             | Open pile -> counted pile
             | Closed n -> Tint.wrap Tint.Hidden $"?x{n}")

        line "out of sight" (counted seen.Unseen)

        // Every stone that is not on the map and not in the reader's own bag is somewhere
        // out there, and though nobody can say where, the colours are known exactly. That
        // is worth a picture rather than three numbers.
        let breakdown = BreakdownChart()
        breakdown.Width <- 40

        for color, n in Pile.toCounts seen.Unseen do
            breakdown.AddItem(Words.color color, float n, Tint.color color) |> ignore

        rows [ table :> IRenderable; markup ""; breakdown :> IRenderable ]

    let private landRuled game =
        // How the land stands is the game's own reckoning, not this view's. A second
        // renderer counting it again for itself is a second chance to count it differently.
        let standing = Game.landStanding game

        let bars = BarChart()
        bars.Width <- width - 6

        for color in StoneColor.all do
            bars.AddItem(Words.color color, float (Map.find color standing.Ruled), Tint.color color)
            |> ignore

        bars.AddItem("tied", float standing.Tied, Color.Grey37) |> ignore
        bars.AddItem("unclaimed", float standing.Unclaimed, Color.Grey23) |> ignore
        bars :> IRenderable

    // --- everything the game has said ---------------------------------------------------

    let private log told (model: Model) =
        match model.Log with
        | [] -> markup (Tint.wrap Tint.Hidden "nothing yet")
        | notices ->
            notices
            |> List.rev
            |> List.map (told >> Tint.markup)
            |> String.concat Environment.NewLine
            |> markup

    let private plainly (lines: string list) =
        lines |> String.concat Environment.NewLine |> Tint.markup |> markup

    // --- the whole screen ------------------------------------------------------------------

    let board notes (beholder: Player) model =
        let game = Model.game model
        let active = Game.active game

        let seen =
            if Model.isOver model then
                Knowledge.laidBare beholder game
            else
                Knowledge.seenBy beholder game

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

        let mapNotes =
            if notes then
                [ markup ""
                  note "Two regions border each other where they share a wall, and nowhere else -"
                  note "each one touches the two beside it and two on the row above and below. A"
                  note "region is bordered in the colour of whoever rules it; '>' says the same,"
                  note "and '=' marks who is level in it. A home carries its own colour." ]
            else
                []

        let hiddenNote =
            if notes && not (Model.isOver model) then
                [ markup ""
                  note "Every bag but your own is closed, and so is the reserve. But every stone"
                  note "is somewhere: what is neither on the map nor in your bag must be out of"
                  note "sight, and that much is exact." ]
            else
                []

        let result =
            if Model.isOver model then
                [ wide "Result" (plainly (Render.result game)) ]
            else
                []

        let commands =
            if notes then
                [ wide
                      "Commands"
                      (plainly (Render.commands @ [ ""; "  Colours are r, b and g; regions are numbered on the map." ])) ]
            else
                []

        rows (
            [ rule :> IRenderable
              wide "The map" (rows ([ mapOf game ] @ mapNotes))
              apart game
              beside
                  (panel "Players" (players seen active.Id model))
                  (panel "Supply" (rows ([ supply seen ] @ hiddenNote)))
              wide "Land ruled" (landRuled game) ]
            @ result
            @ commands
            @ [ wide "Log" (log told model) ]
        )
        |> Tint.renderAt width

    // --- the rest of what a player reads --------------------------------------------------
    //
    // Every screen the game shows has to be answered here, not only the board. That is the
    // bargain of a wide seam: adding to the game means adding an endpoint and answering it
    // in every view, and the compiler says so if one is missed.

    /// The record of play, as a table: what was asked, by whom, and what came of it.
    let history (beholder: Player) model =
        let told = Render.wording beholder model

        match Journal.entries model.Journal with
        | [] -> Tint.renderAt width (panel "The record" (markup (Tint.wrap Tint.Hidden "Nothing has happened yet.")))
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
                | notices -> notices |> List.map (told >> Tint.markup) |> String.concat Environment.NewLine

            table.AddRow(
                markup (Tint.wrap Tint.Hidden (esc $"{entry.Ordinal}  turn {entry.Turn}")),
                markup (Tint.markup $"{Words.player entry.Actor}: {asked}"),
                markup outcome
            )
            |> ignore

        let made = Timeline.movesMade model.Timeline
        let back = Timeline.movesTakenBack model.Timeline

        let standing =
            match back with
            | 0 -> $"{made} move(s) stand between the deal and here."
            | _ -> $"{made} move(s) stand between the deal and here, with {back} taken back."

        Tint.renderAt
            width
            (wide "The record" (rows [ table :> IRenderable; markup ""; markup (Tint.wrap Tint.Hidden (esc standing)) ]))

    /// The working behind who rules a region.
    let ruling regionId model =
        Tint.renderAt width (wide $"Region {Words.number regionId}" (plainly [ Render.explainRule regionId model ]))

    /// The rules and the commands, at length. The words are `Render`'s - they say what the
    /// game is, which is not a thing for a view to have an opinion about - laid out here.
    let rules =
        Tint.renderAt width (wide "How the game goes" (plainly [ Render.help ]))

    /// A table still filling up, before there is any game to draw.
    let waiting (seats: Waiting list) =
        let table = Table()
        table.Border <- TableBorder.None
        table.ShowHeaders <- false
        table.AddColumn(TableColumn "") |> ignore
        table.AddColumn(TableColumn "") |> ignore

        for seat in seats do
            let who =
                if seat.Yours then
                    Tint.wrap Tint.Yours $"{Words.player seat.Player} (you)"
                else
                    Words.player seat.Player

            let holding =
                if seat.Expected then Tint.wrap Tint.Hidden "still to arrive"
                elif seat.Away then Tint.wrap Tint.Hidden "here, but their console has dropped"
                else Tint.wrap Tint.Yours "here"

            table.AddRow(markup who, markup holding) |> ignore

        let expected = seats |> List.filter (fun seat -> seat.Expected) |> List.length

        let footer =
            Tint.wrap Tint.Hidden (esc $"{expected} more to come. The game begins once every seat is taken.")

        Tint.renderAt
            width
            (panel "Waiting for the table to fill" (rows [ table :> IRenderable; markup ""; markup footer ]))
