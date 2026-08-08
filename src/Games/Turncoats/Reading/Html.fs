namespace TCModel.Turncoats

open System
open System.Text.Json.Serialization
open Falco.Markup
open Falco.Datastar
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing
// namespace, and both `Spectre.Console` and the command line's argument types carry
// names this game already uses - `Region`, `Open`, `View`.
open TCModel.Turncoats

/// The board as a page, for a player reading in a browser rather than at a terminal.
///
/// This is the third way of showing the game and it is shown the same way as the other
/// two: every endpoint is handed the model and gives back text. The text happens to be
/// HTML, which a terminal cannot show and a browser can, and that is the whole of the
/// difference the rest of the program has to know about.
///
/// Nothing here decides what a player may know. What is shown comes from `Knowledge` and
/// what a notice says comes from `Render.wording`, the same as the other two views - a
/// third renderer is a third chance to leak, and the way not to take it is not to write
/// that reasoning down a third time. Nor does anything here decide what a screen *says*:
/// the region titles, the cascades, the rules and the commands are all `Render`'s words,
/// laid out differently.
///
/// Two things are this view's own. The first is that a screen is a *fragment* rather than
/// a page: everything below is one element with a known id, so the same text serves both
/// for building the page the first time and for patching it afterwards, and there is only
/// one way of drawing a board rather than one for each. The second is that a control is a
/// line of typing. Every button here posts the words a player would have typed at the
/// prompt, which is the record's own bargain - moves are written in the words the prompt
/// takes - held to by a view that has buttons. A button cannot ask for something the
/// parser would not take, because there is nothing else for it to send.
module Html =

    // --- the small change ------------------------------------------------------------
    //
    // All of it the page's rather than the game's, and named here only so that the drawing
    // below reads as drawing. What each one is, and why it is written the way it is, is at
    // `Page`.

    let private attr = Page.attr

    let private types = Page.types

    let private block = Page.block

    let private note = Page.note

    let private lines = Page.lines

    let private screen = Page.screen

    let private aside = Page.aside

    /// A colour's name in lower case, which serves as a CSS class and as the name of the
    /// custom property holding it. Taken from `Words` so that a faction renamed is
    /// renamed here too.
    let private shade color = (Words.color color).ToLowerInvariant()

    let private quiet text =
        Elem.span [ Attr.class' "quiet" ] [ Text.enc text ]

    let private stone color =
        Elem.span [ Attr.class' $"stone {shade color}" ] [ Text.raw (string (Words.glyph color)) ]

    /// Stones laid out one by one, each in its own colour - the same picture the rich view
    /// draws, which is the one a player learns to read at a glance.
    let private laid pile =
        match Pile.toColors pile with
        | [] -> [ quiet "-" ]
        | colors -> colors |> List.map stone

    /// The same, counted rather than laid out, for where there are too many to draw.
    let private counted pile =
        match Pile.toCounts pile with
        | [] -> [ quiet "-" ]
        | counts ->
            counts
            |> List.map (fun (color, n) ->
                Elem.span [ Attr.class' $"stone {shade color}" ] [ Text.raw $"{Words.glyph color}x{n}" ])

    /// Stones nobody can name, drawn as the stones they are: a closed bag of eight is
    /// eight of these, which is exactly the state of affairs the game means a player to
    /// be in.
    let private unnamed n =
        if n = 0 then [ quiet "-" ] else List.replicate n (quiet "?")

    let private sighted =
        function
        | Open pile -> laid pile @ [ quiet $" ({Pile.total pile})" ]
        | Closed n -> unnamed n @ [ quiet $" ({n})" ]

    // --- the map ----------------------------------------------------------------------
    //
    // `Board.layout` lies on a triangular lattice: a region is two half-columns wide and
    // each row stands half a region across from the one above. Laid out that way the
    // regions come out as brickwork, and a brick touches exactly six others - the two
    // beside it and two on each of the rows above and below. Those six are its borders. So
    // the offset is not decoration: drop it and the map stops saying where a player may
    // march.

    let private rulerBadge ruling =
        match ruling with
        | RuledBy color -> [ Elem.span [ Attr.class' $"rules {shade color}" ] [ Text.raw $">{Words.glyph color}" ] ]
        | Contested tied -> [ Elem.span [ Attr.class' "tied" ] (Text.raw "=" :: (tied |> List.map stone)) ]
        | Unclaimed -> []

    /// A region as a box of its own, bordered in the colour of whoever rules it - which is
    /// the one thing on a board worth seeing from across the room.
    let private regionCell game (region: Region) =
        let ruling = Game.ruleOver region.Id game

        let border =
            match region.Kind, ruling with
            | Dead, _ -> "var(--hidden)"
            | _, RuledBy color -> $"var(--{shade color})"
            | _, (Contested _ | Unclaimed) -> "var(--edge)"

        let standing =
            match region.Kind with
            | Dead -> [ quiet "dead" ]
            | Home _
            | Wild
            | Special -> laid (Game.stones region.Id game)

        // Recruiting is the one action that needs nothing but a colour and a region, so it
        // is the one that fits on a region. A battle or a march needs saying, and gets
        // said at the prompt.
        let acts =
            match region.Kind with
            | Dead -> []
            | Home _
            | Wild
            | Special ->
                StoneColor.all
                |> List.map (fun color ->
                    types
                        $"recruit {(Words.glyph color |> string).ToLowerInvariant()} {Words.number region.Id}"
                        (string (Words.glyph color)))

        Elem.div
            [ Attr.class' "region"; attr "style" $"border-color: {border}" ]
            [ Elem.div [ Attr.class' "title" ] [ Text.enc (Render.regionTitle 24 region) ]
              Elem.div [ Attr.class' "standing" ] (standing @ rulerBadge ruling)
              Elem.div [ Attr.class' "acts" ] (acts @ [ types $"rule {Words.number region.Id}" "?" ]) ]

    let private mapOf game =
        Board.layout
        |> List.map (fun row ->
            let offset = row |> List.map snd |> List.min

            Elem.div
                [ Attr.class' "row"
                  attr "style" $"margin-left: calc(%d{offset} * var(--half))" ]
                (row |> List.map (fst >> Board.region >> regionCell game)))
        |> Elem.div [ Attr.class' "map" ]

    /// The Flag and the Axe, which border nothing and so stand outside the map.
    let private apart game =
        Board.apartRegions
        |> List.map (fun region ->
            let ruling = Game.ruleOver region.Id game

            Elem.div
                [ Attr.class' "region apart" ]
                [ Elem.div [ Attr.class' "title" ] [ Text.enc (Render.regionTitle 24 region) ]
                  Elem.div [ Attr.class' "standing" ] (laid (Game.stones region.Id game) @ rulerBadge ruling) ])
        |> Elem.div [ Attr.class' "row" ]

    // --- the blocks standing around it ------------------------------------------------

    let private players (seen: Knowledge) active model =
        let row (playerId, bag) =
            let yours = playerId = seen.Beholder

            Elem.div
                [ Attr.class' (if yours then "player yours" else "player") ]
                [ Elem.span [ Attr.class' "marker" ] [ Text.raw (if playerId = active then "-&gt;" else " ") ]
                  Elem.span [ Attr.class' "who" ] [ Text.enc (Words.seated yours playerId) ]
                  Elem.span [ Attr.class' "bag" ] (sighted bag) ]

        let run =
            match Playing.session model with
            | InPlay play -> [ quiet (Render.negotiationRun play (Playing.game model)) ]
            | Finished _ -> []

        (seen.Bags |> List.map row) @ run

    let private supply notes over (seen: Knowledge) =
        let line label what =
            Elem.div [ Attr.class' "supply" ] (quiet label :: what)

        [ line Render.Supply.onTheBoard (counted (Position.total seen.Position))
          line
              Render.Supply.inReserve
              (match seen.Reserve with
               | Open pile -> counted pile
               | Closed n -> [ quiet $"?x{n}" ])
          line Render.Supply.outOfSight (counted seen.Unseen) ]
        @ (if notes && not over then [ note Render.Notes.supply ] else [])

    /// How the land stands is the game's own reckoning, not this view's. A third renderer
    /// counting it again for itself is a third chance to count it differently.
    let private landRuled notes game =
        let standing = Game.landStanding game

        let total =
            max
                1
                (List.sum (
                    standing.Tied
                    :: standing.Unclaimed
                    :: (StoneColor.all |> List.map (fun c -> Map.find c standing.Ruled))
                ))

        let bar name colour n =
            Elem.div
                [ Attr.class' "bar" ]
                [ Elem.span [ Attr.class' "who" ] [ Text.enc name ]
                  Elem.span
                      [ Attr.class' "fill"
                        attr "style" $"width: calc(%d{n} * 100%% / %d{total}); background: {colour}" ]
                      []
                  Elem.span [ Attr.class' "count" ] [ Text.enc (string n) ] ]

        (StoneColor.all
         |> List.map (fun color -> bar (Words.color color) $"var(--{shade color})" (Map.find color standing.Ruled)))
        @ [ bar Words.tied "var(--edge)" standing.Tied
            bar Words.unclaimed "var(--hidden)" standing.Unclaimed ]
        @ (if notes then [ note Render.Notes.landRuled ] else [])

    let private log told (model: Model) =
        match model.Log with
        | [] -> [ quiet Render.nothingYet ]
        | notices ->
            notices
            |> List.rev
            |> List.map (fun notice -> Elem.div [ Attr.class' "said" ] [ Text.enc (told notice) ])

    // --- the whole screen ---------------------------------------------------------------

    /// The board, drawn for one player. `notes` says whether the writing that explains it
    /// comes with it; the controls stay either way, because a player who knows how to read
    /// a board still has to move on it.
    let board notes (beholder: Player) model =
        let game = Playing.game model
        let active = Game.active game
        let over = Playing.isOver model

        let seen =
            if over then Knowledge.laidBare beholder game else Knowledge.seenBy beholder game

        let told = Render.wording beholder model

        /// A note, if the reader still wants them. A page wraps its own paragraphs, so
        /// unlike the two terminal views this hands the whole thing over as it stands.
        let noted text = if notes then [ note text ] else []

        // What may be done without saying anything more than the word itself. The rest of
        // the moves take arguments, and take them at the prompt.
        let toHand =
            match Playing.session model with
            | InPlay { Phase = AwaitingReturn _ } ->
                StoneColor.all
                |> List.map (fun color ->
                    types $"return {(Words.glyph color |> string).ToLowerInvariant()}" $"return {Words.color color}")
            | InPlay _ -> [ types "negotiate" "negotiate" ]
            | Finished _ -> []

        let result =
            if over then
                [ block Render.Blocks.result [ lines (String.concat Environment.NewLine (Render.result game)) ] ]
            else
                []

        let commands =
            if notes then
                [ block
                      Render.Blocks.commands
                      [ lines (String.concat Environment.NewLine (Render.commands @ [ ""; "  " + Render.shorthand ])) ] ]
            else
                []

        screen (
            [ Elem.h1 [] [ Text.enc (Render.heading beholder model) ]
              block
                  Render.Blocks.map
                  ([ mapOf game ]
                   @ noted (
                       String.concat
                           " "
                           [ Render.Notes.map
                             Render.Notes.bordered
                             // The one thing here no other view has: a region on this board can be
                             // typed on by clicking it.
                             "The letters under a region recruit a stone into it, and '?' shows why it is ruled as it is." ]
                   ))
              block Render.Blocks.apart ([ apart game ] @ noted Render.Notes.apart)
              // The one block no other view has: the moves that need nothing said beyond
              // the word itself, which a terminal simply types.
              block "This turn" [ Elem.div [ Attr.class' "acts wide" ] toHand ]
              block Render.Blocks.players (players seen active.Id model)
              block Render.Blocks.supply (supply notes over seen)
              block Render.Blocks.landRuled (landRuled notes game) ]
            @ result
            @ commands
            @ [ block Render.Blocks.log (log told model) ]
        )

    /// A table still filling up. There is no game to draw yet, so this is the one screen
    /// drawn from a list of who has arrived rather than from a position.
    let waiting (seats: Waiting list) =
        let standing (seat: Waiting) =
            Elem.div
                [ Attr.class' (if seat.Yours then "player yours" else "player") ]
                [ Elem.span [ Attr.class' "who" ] [ Text.enc (Words.seated seat.Yours seat.Player) ]
                  quiet (Render.Filling.standing seat) ]

        screen
            [ Elem.h1 [] [ Text.enc Render.Filling.title ]
              block "The table" ((seats |> List.map standing) @ [ quiet (Render.Filling.stillToCome seats) ]) ]

    // --- the rest of what a player reads -----------------------------------------------
    //
    // Everything below lands in the aside rather than on the board, because that is what
    // the table means by telling a console something: news with no board to go with it.
    // The board stays where it was while it is read, which is more than a terminal manages.

    /// One line with no board to go with it. The page's, because every game says these and
    /// says them the same way.
    let says = Page.says

    let history (beholder: Player) model =
        let told = Render.wording beholder model

        match Journal.entries model.Journal with
        | [] -> aside [ Elem.h2 [] [ Text.enc Render.Blocks.record ]; quiet Render.nothingYet ]
        | entries ->
            let row (entry: Entry) =
                Elem.div
                    [ Attr.class' "entry" ]
                    [ quiet $"{entry.Ordinal}  turn {entry.Turn}"
                      Elem.span [ Attr.class' "asked" ] [ Text.enc $"{Words.player entry.Actor}: {Words.command entry.Asked}" ]
                      Elem.span [ Attr.class' "outcome" ] (entry.Told |> List.map (fun notice -> quiet (told notice))) ]

            aside (
                [ Elem.h2 [] [ Text.enc Render.Blocks.record ] ]
                @ (entries |> List.map row)
                @ [ quiet (Render.recordStanding model) ]
            )

    let ruling regionId model =
        aside
            [ Elem.h2 [] [ Text.enc (Render.Blocks.region regionId) ]
              lines (Render.explainRule regionId model) ]

    let rules = aside [ Elem.h2 [] [ Text.enc Render.Blocks.rules ]; lines Render.help ]
    // --- what this game looks like on a page ----------------------------------------------

    /// The rules of how *this game's* pieces are drawn, and no more than that. The page
    /// itself - the chrome, the prompt, the door, the corner - is styled at `Page`, along
    /// with the row shapes every game builds a fragment out of.
    ///
    /// The colours are not here. They are a player's own and are written in above this as
    /// custom properties, which is what lets two people at one table be sent the same board
    /// and read it in colours that have nothing to do with each other.
    let private sheet =
        """
body { --half: 7.5rem; }

.map, .row { display: flex; flex-wrap: nowrap; }
.map { flex-direction: column; overflow-x: auto; padding-bottom: .5rem; }
.region {
  width: calc(2 * var(--half)); flex: none; padding: .35rem .5rem;
  border: 1px solid var(--edge); border-radius: .4rem; background: var(--raised); margin: 2px;
}
.region.apart { width: calc(3 * var(--half)); }
.region .title { color: var(--ink); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.region .standing { min-height: 1.5rem; }
.stone { font-weight: 700; }
.rules { font-weight: 700; float: right; }
.tied { color: var(--edge); float: right; }
.red { color: var(--red); } .blue { color: var(--blue); } .green { color: var(--green); }

.acts { display: flex; gap: .25rem; margin-top: .25rem; }
.acts.wide { flex-wrap: wrap; }

.supply, .bar { display: flex; gap: .6rem; align-items: baseline; }
.supply .quiet:first-child { min-width: 14ch; }
.bar .fill { height: .8rem; border-radius: .2rem; min-width: 1px; }
.entry .asked { min-width: 30ch; }
"""

    /// What this game brings to a browser, and the whole of it. Everything else about the
    /// page is the same at every game and is written once, at `Page`.
    let shell =
        { Title = "Turncoats"
          Sheet = sheet
          Placeholder = "type a move - r b 5, b r 8, m g 8 5 2, help" }
