namespace Prototyping.Turncoats

open System
open Falco.Markup
open Prototyping.Engine
open Prototyping.Table
// After the table, so that a Sight is this game's and not the command line's.
open Prototyping.Turncoats

module Html =

    let private quiet text =
        Elem.span [ Attr.class' "quiet" ] [ Text.enc text ]

    let private stone colour =
        Elem.span [ Attr.class' $"stone {Ink.key colour}" ] [ Text.raw (string (Words.glyph colour)) ]

    let private laid pile =
        match Pile.toColours pile with
        | [] -> [ quiet "-" ]
        | colours -> colours |> List.map stone

    let private counted pile =
        match Pile.toCounts pile with
        | [] -> [ quiet "-" ]
        | counts ->
            counts
            |> List.map (fun (colour, n) ->
                Elem.span [ Attr.class' $"stone {Ink.key colour}" ] [ Text.raw $"{Words.glyph colour}x{n}" ])

    let private unnamed n =
        if n = 0 then [ quiet "-" ] else List.replicate n (quiet "?")

    let private sighted =
        function
        | Open pile -> laid pile @ [ quiet $" ({Pile.total pile})" ]
        | Closed n -> unnamed n @ [ quiet $" ({n})" ]


    let private rulerBadge ruling =
        match ruling with
        | RuledBy colour -> [ Elem.span [ Attr.class' $"rules {Ink.key colour}" ] [ Text.raw $">{Words.glyph colour}" ] ]
        | Contested tied -> [ Elem.span [ Attr.class' "tied" ] (Text.raw "=" :: (tied |> List.map stone)) ]
        | Unclaimed -> []

    let private regionCell game (region: Region) =
        let ruling = Game.ruleOver region.Id game

        let border =
            match region.Kind, ruling with
            | Dead, _ -> "var(--hidden)"
            | _, RuledBy colour -> $"var(--{Ink.key colour})"
            | _, (Contested _ | Unclaimed) -> "var(--edge)"

        let standing =
            match region.Kind with
            | Dead -> [ quiet "dead" ]
            | Home _
            | Wild
            | Special -> laid (Game.stones region.Id game)

        let recruiting =
            match region.Kind with
            | Dead -> []
            | Home _
            | Wild
            | Special ->
                StoneColour.all
                |> List.map (fun colour ->
                    Page.types $"recruit {Words.short colour} {Words.number region.Id}" (string (Words.glyph colour)))

        let withWhatIsHere =
            let here = Game.stones region.Id game

            let marching colour =
                Board.neighbours region.Id
                |> Set.toList
                |> List.filter (fun other -> RegionKind.isOpen (Board.region other).Kind)
                |> List.map (fun other ->
                    Page.types
                        $"march {Words.short colour} {Words.number region.Id} {Words.number other}"
                        $"{Words.glyph colour}→{Words.number other}")

            match region.Kind with
            | Dead -> []
            | Home _
            | Wild
            | Special ->
                StoneColour.all
                |> List.filter (fun colour -> Pile.count colour here > 0)
                |> List.collect (fun colour ->
                    Page.types $"battle {Words.short colour} {Words.number region.Id}" $"×{Words.glyph colour}"
                    :: marching colour)

        Elem.div
            [ Attr.class' "region"; Page.attr "style" $"border-color: {border}" ]
            ([ Elem.div [ Attr.class' "title" ] [ Text.enc (Render.regionTitle 24 region) ]
               Elem.div [ Attr.class' "standing" ] (standing @ rulerBadge ruling)
               Elem.div [ Attr.class' "acts" ] (recruiting @ [ Page.types $"rule {Words.number region.Id}" "?" ]) ]
             @ (match withWhatIsHere with
                | [] -> []
                | acts -> [ Elem.div [ Attr.class' "acts wide" ] acts ]))

    let private mapOf game =
        Board.layout
        |> List.map (fun row ->
            let offset = row |> List.map snd |> List.min

            Elem.div
                [ Attr.class' "row"
                  Page.attr "style" $"margin-left: calc(%d{offset} * var(--half))" ]
                (row |> List.map (fst >> Board.region >> regionCell game)))
        |> Elem.div [ Attr.class' "map" ]

    let private apart game =
        Board.apartRegions
        |> List.map (fun region ->
            let ruling = Game.ruleOver region.Id game

            Elem.div
                [ Attr.class' "region apart" ]
                [ Elem.div [ Attr.class' "title" ] [ Text.enc (Render.regionTitle 24 region) ]
                  Elem.div [ Attr.class' "standing" ] (laid (Game.stones region.Id game) @ rulerBadge ruling) ])
        |> Elem.div [ Attr.class' "row" ]


    let private players (seen: Knowledge) active model =
        let row (playerId, bag) =
            let yours = playerId = seen.Beholder

            Elem.div
                [ Attr.class' (if yours then "player yours" else "player") ]
                [ Elem.span [ Attr.class' "marker" ] [ Text.raw (if playerId = active then "-&gt;" else " ") ]
                  Elem.span [ Attr.class' "who" ] [ Text.enc (Render.seated yours playerId) ]
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
        @ (if notes && not over then [ Page.note Render.Notes.supply ] else [])

    let private landRuled notes game =
        let standing = Game.landStanding game

        let total =
            max
                1
                (List.sum (
                    standing.Tied
                    :: standing.Vacant
                    :: (StoneColour.all |> List.map (fun c -> Map.find c standing.Ruled))
                ))

        let bar name colour n =
            Elem.div
                [ Attr.class' "bar" ]
                [ Elem.span [ Attr.class' "who" ] [ Text.enc name ]
                  Elem.span
                      [ Attr.class' "fill"
                        Page.attr "style" $"width: calc(%d{n} * 100%% / %d{total}); background: {colour}" ]
                      []
                  Elem.span [ Attr.class' "count" ] [ Text.enc (string n) ] ]

        (StoneColour.all
         |> List.map (fun colour -> bar (Words.colour colour) $"var(--{Ink.key colour})" (Map.find colour standing.Ruled)))
        @ [ bar Words.tied "var(--edge)" standing.Tied
            bar Words.unclaimed "var(--hidden)" standing.Vacant ]
        @ (if notes then [ Page.note Render.Notes.landRuled ] else [])

    let private log told (model: Model) =
        match model.Log with
        | [] -> [ quiet Scene.NothingYet ]
        | notices ->
            notices
            |> List.rev
            |> List.map (fun notice -> Elem.div [ Attr.class' "said" ] [ Text.enc (told notice) ])


    let board (margins: Margins) (beholder: Player) model =
        let notes = margins.Notes
        let game = Playing.game model
        let active = Game.active game
        let over = Playing.isOver model

        let seen =
            if over then Knowledge.laidBare beholder game else Knowledge.seenBy beholder game

        let told = Render.wording beholder model

        let noted text =
            if notes then [ Page.note text ] else []

        let toHand =
            match Playing.session model with
            | InPlay { Phase = AwaitingReturn _ } ->
                StoneColour.all
                |> List.map (fun colour -> Page.types $"return {Words.short colour}" $"return {Words.colour colour}")
            | InPlay _ -> [ Page.types "negotiate" "negotiate" ]
            | Finished _ -> []

        let result =
            if over then
                [ Page.block Render.Blocks.result [ Page.lines (String.concat Environment.NewLine (Render.result game)) ] ]
            else
                []

        let commands =
            if margins.Commands then
                [ Page.block
                      Render.Blocks.commands
                      [ Page.lines (String.concat Environment.NewLine (Render.commands @ [ ""; "  " + Render.shorthand ])) ] ]
            else
                []

        Page.screen (
            [ Elem.h1 [] [ Text.enc (Render.heading beholder model) ]
              Page.block
                  Render.Blocks.map
                  ([ mapOf game ]
                   @ noted (
                       String.concat
                           " "
                           [ Render.Notes.map
                             Render.Notes.bordered
                             Render.Notes.recruiting
                             Render.Notes.acting ]
                   ))
              Page.block Render.Blocks.apart ([ apart game ] @ noted Render.Notes.apart)
              Page.block Render.Blocks.thisTurn [ Elem.div [ Attr.class' "acts wide" ] toHand ]
              Page.block Render.Blocks.players (players seen active.Id model)
              Page.block Render.Blocks.supply (supply notes over seen)
              Page.block Render.Blocks.landRuled (landRuled notes game) ]
            @ result
            @ commands
            @ (if margins.Logged then [ Page.block Render.Blocks.log (log told model) ] else [])
        )

    let waiting (seats: Waiting list) =
        let standing (seat: Waiting) =
            Elem.div
                [ Attr.class' (if seat.Yours then "player yours" else "player") ]
                [ Elem.span [ Attr.class' "who" ] [ Text.enc (Render.seated seat.Yours seat.Player) ]
                  quiet (Scene.Filling.standing seat) ]

        Page.screen
            [ Elem.h1 [] [ Text.enc Scene.Filling.title ]
              Page.block Render.Blocks.table ((seats |> List.map standing) @ [ quiet (Scene.Filling.stillToCome seats) ]) ]


    let says = Page.says

    let history (beholder: Player) model =
        let told = Render.wording beholder model

        match Journal.entries model.Journal with
        | [] -> Page.aside [ Elem.h2 [] [ Text.enc Render.Blocks.record ]; quiet Scene.NothingYet ]
        | entries ->
            let row (entry: Entry) =
                Elem.div
                    [ Attr.class' "entry" ]
                    [ quiet $"{entry.Ordinal}  turn {entry.Turn}"
                      Elem.span [ Attr.class' "asked" ] [ Text.enc $"{Words.player entry.Actor}: {Words.command entry.Asked}" ]
                      Elem.span [ Attr.class' "outcome" ] (entry.Told |> List.map (fun notice -> quiet (told notice))) ]

            Page.aside (
                [ Elem.h2 [] [ Text.enc Render.Blocks.record ] ]
                @ (entries |> List.map row)
                @ [ quiet (Render.recordStanding model) ]
            )

    let ruling regionId model =
        Page.aside
            [ Elem.h2 [] [ Text.enc (Render.Blocks.region regionId) ]
              Page.lines (Render.explainRule regionId model) ]

    let rules =
        Page.aside [ Elem.h2 [] [ Text.enc Render.Blocks.rules ]; Page.lines Render.help ]

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

    let shell =
        { Title = "Turncoats"
          Sheet = sheet
          Placeholder = "type a move - r b 5, b r 8, m g 8 5 2, help"
          Keys = [] }
