namespace TCModel.Console

open System
open System.Text.Json.Serialization
open Falco.Markup
open Falco.Datastar
open TCModel.Domain
open TCModel.App

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

    /// The two places on the page anything ever lands: the board, and whatever the game
    /// last said that had no board with it. Said once here because the page builds them,
    /// the fragments are named for them, and the server patches them.
    [<Literal>]
    let Screen = "screen"

    [<Literal>]
    let Told = "told"

    /// Where the page fetches its client and where it posts what a player types. Named
    /// here rather than written into the markup, because `Web` has to serve exactly these.
    [<Literal>]
    let Client = "/datastar.js"

    [<Literal>]
    let Stream = "/stream"

    [<Literal>]
    let Say = "/say"

    /// What the page holds on the client's behalf, and the whole of it: the line waiting to
    /// be sent.
    ///
    /// One declaration rather than two. The page starts this off, the box at the bottom is
    /// bound to it, and the table reads it back off a request - and a name that drifted
    /// between those would leave a browser sending something nobody was listening for,
    /// silently, which is this seam's whole way of going wrong.
    ///
    /// Named twice over on purpose: `Line` for F# to read and `line` for the wire, because
    /// the wire's spelling is the one the client binds to and it is not F#'s to choose.
    type Signals =
        { [<JsonPropertyName("line")>]
          Line: string }

    /// A page holding nothing typed yet, which is how one arrives and how it is left after
    /// every line it sends.
    let nothingTyped = { Line = "" }

    /// What is sent down a page's stream to say the turn has come round to whoever is
    /// reading it. Not a piece of the page: there is nothing on the board this could
    /// replace, because being interrupted is something a browser does rather than something
    /// it shows.
    ///
    /// The whole of the deciding is at the far end, in the page's own script below, which is
    /// the only thing in the program that can see whether anybody is looking. So this is a
    /// knock and not an instruction - named here beside the script that answers it, so the
    /// two cannot drift apart.
    [<Literal>]
    let Nudge = "nudged()"

    /// The one control on the page that is not a move and not a colour: the button that
    /// asks the browser for leave to say so out loud. Named here because the markup writes
    /// it and the page's own script goes looking for it, and a name that drifted between
    /// those two would leave a button on the page that did nothing whatsoever.
    [<Literal>]
    let Notify = "notify"

    /// Where a page says it has gone wrong.
    ///
    /// The person hosting a game cannot see the console of a browser two rooms away, and
    /// the person in that browser has no reason to know there is one. So anything the page
    /// throws is carried back here and printed where the game was started - which is the
    /// only screen anybody is actually watching.
    [<Literal>]
    let Amiss = "/amiss"

    // --- the small change ------------------------------------------------------------

    /// An attribute, with its value escaped.
    ///
    /// `Falco.Markup` escapes what goes between the tags and leaves what goes inside the
    /// quotes exactly as handed to it, which is a sharp edge worth blunting in one place
    /// rather than remembering at a dozen. Every attribute below whose value is built
    /// rather than written out goes through here.
    ///
    /// It matters most for the controls. What a button does is a scrap of the client's own
    /// language sitting in an attribute, and that language uses quotes and ampersands the
    /// way any language does - `evt.key === 'Enter' && ...` is four characters HTML would
    /// rather read as markup. The browser hands the value back decoded, so what the client
    /// finally reads is what was written here.
    let private attr name (value: string) =
        Attr.create name (Net.WebUtility.HtmlEncode value)

    /// An attribute of the client's that stands for itself, given an empty value instead.
    ///
    /// `data-bind:line` and `data-bind:line=""` say the same thing to the client - it takes
    /// the key and treats an empty value as no value at all - and only the second is
    /// well-formed markup, which is what `html.fsx` holds every screen to. So the name
    /// still comes from `Falco.Datastar`; only how it is written down is settled here.
    ///
    /// The value is deliberately not touched. Some of what the library writes is escaped
    /// already and some is not, and running an escaper over the lot would turn a `&quot;`
    /// into an `&amp;quot;` and break the one thing that was right.
    let private valued =
        function
        | NonValueAttr name -> KeyValueAttr(name, "")
        | given -> given

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

    /// A control that types a line for the player.
    ///
    /// The line goes in the address rather than in a signal, so a button carries the whole
    /// of what it does and needs nothing on the page to be in any particular state for it
    /// to mean that. Escaped once, which is also what keeps it out of trouble inside the
    /// attribute: what comes out has no quote, space or bracket left in it.
    let private types line caption =
        Elem.button
            [ Attr.class' "types"
              attr "title" line
              Ds.onClick (Ds.post $"{Say}?line={Uri.EscapeDataString line}") ]
            [ Text.enc caption ]

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

    let private block title content =
        Elem.section [ Attr.class' "block" ] (Elem.h2 [] [ Text.enc title ] :: content)

    /// Writing that is there to be read once, and gone the moment the notes are off.
    let private note text =
        Elem.p [ Attr.class' "note" ] [ Text.enc text ]

    let private players (seen: Knowledge) active model =
        let row (playerId, bag) =
            let yours = playerId = seen.Beholder

            Elem.div
                [ Attr.class' (if yours then "player yours" else "player") ]
                [ Elem.span [ Attr.class' "marker" ] [ Text.raw (if playerId = active then "-&gt;" else " ") ]
                  Elem.span [ Attr.class' "who" ] [ Text.enc (Words.player playerId + (if yours then " (you)" else "")) ]
                  Elem.span [ Attr.class' "bag" ] (sighted bag) ]

        let run =
            match Model.session model with
            | InPlay play ->
                let seats = Game.playerCount (Model.game model)
                [ quiet $"negotiations in a row: {play.Negotiations} of {seats} - the game ends on the last" ]
            | Finished _ -> []

        (seen.Bags |> List.map row) @ run

    let private supply notes over (seen: Knowledge) =
        let line label what =
            Elem.div [ Attr.class' "supply" ] (quiet label :: what)

        [ line "on the board" (counted (Position.total seen.Position))
          line
              "in reserve"
              (match seen.Reserve with
               | Open pile -> counted pile
               | Closed n -> [ quiet $"?x{n}" ])
          line "out of sight" (counted seen.Unseen) ]
        @ (if notes && not over then
               [ note
                     "Every bag but your own is closed, and so is the reserve. But every stone is somewhere: what is neither on the map nor in your bag must be out of sight, and that much is exact." ]
           else
               [])

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
        @ [ bar "tied" "var(--edge)" standing.Tied
            bar "unclaimed" "var(--hidden)" standing.Unclaimed ]
        @ (if notes then
               [ note
                     "The Flag and the Axe are manoeuvres rather than land, and the dead region is nobody's to take, so none of the three are counted here." ]
           else
               [])

    let private lines (text: string) = Elem.pre [] [ Text.enc text ]

    let private log told (model: Model) =
        match model.Log with
        | [] -> [ quiet Render.nothingYet ]
        | notices ->
            notices
            |> List.rev
            |> List.map (fun notice -> Elem.div [ Attr.class' "said" ] [ Text.enc (told notice) ])

    // --- the whole screen ---------------------------------------------------------------

    let private screen content =
        renderNode (Elem.main [ Attr.id Screen ] content)

    let private aside content =
        renderNode (Elem.aside [ Attr.id Told ] content)

    /// The board, drawn for one player. `notes` says whether the writing that explains it
    /// comes with it; the controls stay either way, because a player who knows how to read
    /// a board still has to move on it.
    let board notes (beholder: Player) model =
        let game = Model.game model
        let active = Game.active game
        let over = Model.isOver model

        let seen =
            if over then Knowledge.laidBare beholder game else Knowledge.seenBy beholder game

        let told = Render.wording beholder model

        // What may be done without saying anything more than the word itself. The rest of
        // the moves take arguments, and take them at the prompt.
        let toHand =
            match Model.session model with
            | InPlay { Phase = AwaitingReturn _ } ->
                StoneColor.all
                |> List.map (fun color ->
                    types $"return {(Words.glyph color |> string).ToLowerInvariant()}" $"return {Words.color color}")
            | InPlay _ -> [ types "negotiate" "negotiate" ]
            | Finished _ -> []

        let result =
            if over then
                [ block "Result" [ lines (String.concat Environment.NewLine (Render.result game)) ] ]
            else
                []

        let commands =
            if notes then
                [ block
                      "Commands"
                      [ lines (String.concat Environment.NewLine (Render.commands @ [ ""; "  " + Render.shorthand ])) ] ]
            else
                []

        screen (
            [ Elem.h1 [] [ Text.enc (Render.heading beholder model) ]
              block
                  "The map"
                  ([ mapOf game ]
                   @ (if notes then
                          [ note
                                "Two regions border each other where they share a wall, and nowhere else - each one touches the two beside it and two on the row above and below. A region is bordered in the colour of whoever rules it; '>' says the same, and '=' marks who is level in it. A home carries its own colour. The letters under a region recruit into it, and '?' says why it is ruled as it is." ]
                      else
                          []))
              block "Standing apart" [ apart game ]
              block "This turn" [ Elem.div [ Attr.class' "acts wide" ] toHand ]
              block "Players" (players seen active.Id model)
              block "Supply" (supply notes over seen)
              block "Land ruled" (landRuled notes game) ]
            @ result
            @ commands
            @ [ block "Log" (log told model) ]
        )

    /// A table still filling up. There is no game to draw yet, so this is the one screen
    /// drawn from a list of who has arrived rather than from a position.
    let waiting (seats: Waiting list) =
        let standing (seat: Waiting) =
            let holding =
                if seat.Expected then "still to arrive"
                elif seat.Away then "here, but their console has dropped"
                else "here"

            Elem.div
                [ Attr.class' (if seat.Yours then "player yours" else "player") ]
                [ Elem.span [ Attr.class' "who" ] [ Text.enc (Words.player seat.Player + (if seat.Yours then " (you)" else "")) ]
                  quiet holding ]

        let expected = seats |> List.filter (fun seat -> seat.Expected) |> List.length

        screen
            [ Elem.h1 [] [ Text.raw "Waiting for the table to fill" ]
              block
                  "The table"
                  ((seats |> List.map standing)
                   @ [ quiet $"{expected} more to come. The game begins once every seat is taken." ]) ]

    // --- the rest of what a player reads -----------------------------------------------
    //
    // Everything below lands in the aside rather than on the board, because that is what
    // the table means by telling a console something: news with no board to go with it.
    // The board stays where it was while it is read, which is more than a terminal manages.

    let says (text: string) =
        aside [ Elem.div [ Attr.class' "said" ] [ Text.enc text ] ]

    let history (beholder: Player) model =
        let told = Render.wording beholder model

        match Journal.entries model.Journal with
        | [] -> aside [ Elem.h2 [] [ Text.raw "The record" ]; quiet Render.nothingYet ]
        | entries ->
            let row (entry: Entry) =
                Elem.div
                    [ Attr.class' "entry" ]
                    [ quiet $"{entry.Ordinal}  turn {entry.Turn}"
                      Elem.span [ Attr.class' "asked" ] [ Text.enc $"{Words.player entry.Actor}: {Words.command entry.Asked}" ]
                      Elem.span [ Attr.class' "outcome" ] (entry.Told |> List.map (fun notice -> quiet (told notice))) ]

            aside (
                [ Elem.h2 [] [ Text.raw "The record" ] ]
                @ (entries |> List.map row)
                @ [ quiet (Render.recordStanding model) ]
            )

    let ruling regionId model =
        aside
            [ Elem.h2 [] [ Text.enc $"Region {Words.number regionId}" ]
              lines (Render.explainRule regionId model) ]

    let rules = aside [ Elem.h2 [] [ Text.raw "How the game goes" ]; lines Render.help ]

    // --- the page the fragments land in --------------------------------------------------

    /// The rules of how the board is drawn, less the five colours, which are a player's
    /// own and are written in above this as custom properties.
    let private sheet =
        """
* { box-sizing: border-box; }
body {
  --half: 7.5rem; --edge: #6a6a72; --ground: #14141a; --raised: #1d1d25; --ink: #d8d8e0;
  margin: 0; padding: 1rem 1rem 6rem; background: var(--ground); color: var(--ink);
  font: 14px/1.5 ui-monospace, "Cascadia Mono", Consolas, monospace;
}
h1 { font-size: 1.1rem; font-weight: 600; margin: 0 0 1rem; }
h2 { font-size: .8rem; font-weight: 600; letter-spacing: .1em; text-transform: uppercase;
     color: var(--edge); margin: 0 0 .6rem; }
.block { margin: 0 0 1.4rem; }
.quiet { color: var(--edge); }
.note { color: var(--edge); max-width: 70ch; margin: .6rem 0 0; }
pre { margin: 0; white-space: pre-wrap; overflow-x: auto; }

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
.types {
  font: inherit; color: var(--edge); background: transparent; cursor: pointer;
  border: 1px solid var(--edge); border-radius: .25rem; padding: 0 .4rem;
}
.types:hover { color: var(--ground); background: var(--yours); border-color: var(--yours); }

.player, .supply, .bar, .entry { display: flex; gap: .6rem; align-items: baseline; }
.player.yours .who { color: var(--yours); }
.marker { color: var(--yours); width: 1.2rem; }
.who { min-width: 12ch; }
.supply .quiet:first-child { min-width: 14ch; }
.bar .fill { height: .8rem; border-radius: .2rem; min-width: 1px; }
.said { max-width: 100ch; }
.entry .asked { min-width: 30ch; }

#told {
  position: fixed; right: 1rem; bottom: 4.5rem; max-height: 60vh; width: min(60ch, 45vw);
  overflow: auto; padding: .8rem; border: 1px solid var(--edge); border-radius: .4rem;
  background: var(--raised); box-shadow: 0 .5rem 1.5rem #0008;
}
#told:empty { display: none; }

.prompt {
  position: fixed; left: 0; right: 0; bottom: 0; display: flex; gap: .5rem;
  padding: .7rem 1rem; background: var(--raised); border-top: 1px solid var(--edge);
}
.prompt input {
  flex: 1; font: inherit; color: var(--ink); background: var(--ground);
  border: 1px solid var(--edge); border-radius: .25rem; padding: .35rem .6rem;
}
.prompt input:focus { outline: none; border-color: var(--yours); }

.corner { position: fixed; top: .6rem; right: 1rem; z-index: 1;
          display: flex; gap: .6rem; align-items: flex-start; }
.colours summary { color: var(--edge); cursor: pointer; list-style: none; }
.colours form {
  display: grid; grid-template-columns: auto auto; gap: .4rem .6rem; align-items: center;
  margin-top: .5rem; padding: .8rem; border: 1px solid var(--edge); border-radius: .4rem;
  background: var(--raised);
}
.colours label { display: contents; color: var(--edge); }
.colours select {
  font: inherit; color: var(--ink); background: var(--ground);
  border: 1px solid var(--edge); border-radius: .25rem; padding: .1rem .3rem;
}
.colours button { grid-column: 1 / -1; }
"""

    /// Which colour is drawn for what, offered the way a page offers anything: a form that
    /// asks for the page again, saying what to draw it in this time.
    ///
    /// It needs no client at all - it is a form, and a browser has known what to do with
    /// one of those since long before any of this. Every choice is written as the words a
    /// console would have typed at the colour screen and sent down the wire, and read at
    /// the other end by the same `Palette.read` a console's are, so there is one spelling
    /// of a palette in the program and not a second one for the web.
    /// What the page does about being nudged, and the one control that is not a move.
    ///
    /// This is the second and last hand-written script on the page, and like the first it
    /// says nothing about the game. It is here because the decision it makes cannot be made
    /// anywhere else: the table knows the turn has come round unasked, and only the browser
    /// knows whether the player is sitting in front of it. So the table knocks and this
    /// answers, and a player who is looking at the board is left alone - the board says
    /// whose turn it is at the top of it, and it has just been redrawn.
    ///
    /// Two ways of answering, because they fail in opposite places. The title is written on
    /// the tab and on the taskbar and needs nobody's permission, and it is no use at all to
    /// somebody whose browser is behind three other windows. A notification is exactly what
    /// reaches that player, and no browser will show one unasked - so the asking is a button
    /// the player presses, which is also why it cannot be a typed line like everything else:
    /// a browser only takes this question from a click it has just seen, and a line typed at
    /// the prompt has been to the table and back before anything here could ask.
    ///
    /// The button is written for the case it is good for and takes itself off the page
    /// otherwise, which is the only state a browser will let it read.
    let private answering =
        String.concat
            ""
            [ "const calm=document.title;let marked=false;"
              "const settle=()=>{if(marked){marked=false;document.title=calm}};"
              "addEventListener('focus',settle);"
              "addEventListener('visibilitychange',()=>{if(!document.hidden)settle()});"
              // Nobody is nudged for a turn they are watching arrive.
              "window.nudged=()=>{if(document.hasFocus())return;"
              "marked=true;document.title='Your turn - '+calm;"
              "if(window.Notification"
              // Written as a question rather than with an `and`, which is two characters
              // markup would rather read as the start of something escaped.
              "?Notification.permission==='granted':false){"
              "const said=new Notification('Your turn',{body:calm,tag:'stones',renotify:true});"
              "said.onclick=()=>{window.focus();said.close()}}};"
              $"const asking=document.getElementById('{Notify}');"
              "if(window.Notification?Notification.permission==='default':false)"
              "asking.onclick=()=>Notification.requestPermission().then(()=>asking.remove());"
              "else asking.remove()" ]

    let private colours palette =
        let choosing (slot: Slot) =
            let holding = slot.Of palette

            Elem.label
                []
                [ Text.enc slot.Says
                  Elem.select
                      [ attr "name" "colours" ]
                      (Palette.shades
                       |> List.map (fun shade ->
                           Elem.option
                               ([ attr "value" $"{slot.Says}={shade.Name}" ]
                                // Written out in full rather than standing on its own.
                                // Both are HTML, and only this one is also well-formed
                                // markup - which is what the checks hold this file to,
                                // because a browser handed something broken draws
                                // something else rather than saying so.
                                @ (if shade.Name = holding.Name then [ attr "selected" "selected" ] else []))
                               [ Text.enc shade.Name ])) ]

        Elem.details
            [ Attr.class' "colours" ]
            [ Elem.summary [] [ Text.raw "colours" ]
              Elem.form
                  [ attr "method" "get"; attr "action" "/" ]
                  ((Palette.slots |> List.map choosing)
                   @ [ Elem.button [ Attr.class' "types" ] [ Text.raw "redraw" ] ]) ]

    let private styles palette =
        let named =
            Palette.slots
            |> List.map (fun slot -> $"  --{slot.Says}: {Palette.paint (slot.Of palette)};")

        String.concat Environment.NewLine ([ ":root {" ] @ named @ [ "}"; sheet ])

    /// The page itself: a shell with two empty slots in it, and a line of typing at the
    /// bottom.
    ///
    /// Nothing of the game is in here. The page opens the stream, the table answers it
    /// with a board, and every board after that arrives the same way - so what a browser
    /// is first sent and what it is sent on the fiftieth move are built by the same code,
    /// and a page that renders can be trusted to keep rendering.
    ///
    /// The palette is spent here and nowhere else. Every fragment above draws in custom
    /// properties rather than in colours of its own, which is what lets two people at one
    /// table be sent the same board and read it in colours that have nothing to do with
    /// each other.
    let page palette =
        let asked = Uri.EscapeDataString(Palette.write palette)

        renderHtml (
            Elem.html
                [ Attr.lang "en" ]
                [ Elem.head
                      []
                      [ Elem.meta [ Attr.create "charset" "utf-8" ]
                        Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
                        Elem.title [] [ Text.raw "A game of stones" ]
                        Elem.style [] [ Text.raw (styles palette) ]
                        // Plain script rather than a module, and before the client, so that
                        // it is listening by the time anything can go wrong. It is the only
                        // hand-written script on the page and it says nothing about the
                        // game: it carries what broke back to whoever is hosting.
                        Elem.script
                            []
                            [ Text.raw (
                                  // Two things stop this feeding itself, and it needs both.
                                  // Saying so fails like anything else, and a failure while
                                  // reporting a failure is another failure to report; and a
                                  // client that keeps retrying something broken has plenty
                                  // to say. So it swallows its own trouble and stops after
                                  // a handful - by which point whoever is hosting knows.
                                  "let left=8;const tell=w=>{if(left-->0)fetch('"
                                  + Amiss
                                  + "',{method:'POST',body:w}).catch(()=>{})};"
                                  + "addEventListener('error',e=>tell((e.message||'')+' at '+(e.filename||'')+':'+(e.lineno||0)));"
                                  + "addEventListener('unhandledrejection',e=>tell('unsettled: '+e.reason))"
                              ) ]
                        Elem.script [ attr "type" "module"; Attr.src Client ] [] ]
                  Elem.body
                      // The signals are the line waiting to be typed and nothing else. Said
                      // as a value rather than as a scrap of JSON, so that what the page
                      // starts holding and what the table reads back are one declaration.
                      [ Ds.signals nothingTyped; Ds.onInit (Ds.get $"{Stream}?colours={asked}") ]
                      [ Elem.div
                            [ Attr.class' "corner" ]
                            [ Elem.button
                                  [ Attr.class' "types"
                                    Attr.id Notify
                                    attr "title" "say so out loud when the turn comes round and you are looking elsewhere" ]
                                  [ Text.raw Notify ]
                              colours palette ]
                        Elem.main [ Attr.id Screen ] [ Elem.h1 [] [ Text.raw "Sitting down..." ] ]
                        Elem.aside [ Attr.id Told ] []
                        Elem.div
                            [ Attr.class' "prompt" ]
                            [ Elem.input
                                  [ valued (Ds.bind "line")
                                    // Asked as a question rather than with an `&&`, which
                                    // is two characters markup would rather read as the
                                    // start of something escaped.
                                    Ds.onEvent ("keydown", $"evt.key === 'Enter' ? {Ds.post Say} : null")
                                    attr "autofocus" "autofocus"
                                    Attr.autocomplete "off"
                                    attr "placeholder" "type a move - r b 5, b r 8, m g 8 5 2, help" ]
                              Elem.button [ Attr.class' "types"; Ds.onClick (Ds.post Say) ] [ Text.raw "send" ] ]
                        // Last, and not in the head with the other one, because it wires
                        // itself to a control further up the page and has to find it there.
                        Elem.script [] [ Text.raw answering ] ] ]
        )
