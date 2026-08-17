namespace TCModel.Table

open System
open System.Text.Json.Serialization
open Falco.Markup
open Falco.Datastar

/// What one game brings to a page, and the whole of it.
///
/// Everything else about the page - the stream, the prompt, the colour chooser, the two
/// places a fragment can land, the script that answers a knock and the one that heals a
/// dead stream - is the same at every game and is written once below. A game supplies what
/// the page is called, how its own pieces are drawn, and what the prompt suggests typing.
type Shell =
    {
        /// The name on the tab, and what a notification says it is about.
        Title: string
        /// The game's own rules of drawing, added after the page's. Only what the game's
        /// fragments use: everything the page itself lays out is already styled.
        Sheet: string
        /// What the empty prompt suggests. A page is often the first way somebody meets a
        /// game, and an empty box says nothing about what may be typed into it.
        Placeholder: string
    }

/// The page a browser is served, less the game.
///
/// A page is a console like any other: it says the line a player typed and is sent back the
/// screen they would have been looking at. What is here is the shell that arrives before any
/// of that - two empty slots, a stream, and a box to type into - and the small change every
/// fragment is built out of.
///
/// It is here rather than with a game because none of it is one. Which is worth saying
/// twice: the stream, the reconnection, the knock, the colour form and the door are between
/// this program and a browser, and a second game that had to write them again would be a
/// second chance to get them subtly wrong.
module Page =

    /// The two places on the page anything ever lands: the board, and whatever the game
    /// last said that had no board with it. Said once here because the page builds them,
    /// the fragments are named for them, and the server patches them.
    [<Literal>]
    let Screen = "screen"

    [<Literal>]
    let Told = "told"

    /// Where the page fetches its client and where it posts what a player types. Named
    /// here rather than written into the markup, because the server has to serve exactly
    /// these.
    [<Literal>]
    let Client = "/datastar.js"

    [<Literal>]
    let Stream = "/stream"

    [<Literal>]
    let Say = "/say"

    /// Where a page says it has gone wrong.
    ///
    /// The person hosting a game cannot see the console of a browser two rooms away, and
    /// the person in that browser has no reason to know there is one. So anything the page
    /// throws is carried back here and printed where the game was started - which is the
    /// only screen anybody is actually watching.
    [<Literal>]
    let Amiss = "/amiss"

    /// The one control on the page that is not a move and not a colour: the button that
    /// asks the browser for leave to say so out loud. Named here because the markup writes
    /// it and the page's own script goes looking for it, and a name that drifted between
    /// those two would leave a button on the page that did nothing whatsoever.
    [<Literal>]
    let Notify = "notify"

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

    /// And what is sent down it when there is nothing to say, which over a long wire is the
    /// most important thing on it.
    ///
    /// Two failures it answers, and neither is the game's. A stream with no traffic on it
    /// looks idle to everything it passes through, and the usual arrangement out on the
    /// internet - a tunnel, a proxy, a load balancer - closes an idle connection after a
    /// minute or so; a turn-based game between people who are thinking is idle by nature.
    /// And a connection that has silently stopped arriving looks exactly like one where
    /// nobody has moved yet, so a page that is not told anything cannot tell the two apart.
    ///
    /// So the table says something harmless on a timer, and the page below counts the
    /// silence between. It is a script rather than a comment for the second reason: a
    /// comment keeps the wire warm but the page cannot see one, and being seen is half of
    /// what this is for.
    [<Literal>]
    let Alive = "alive()"

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

    /// A `Shell.Sheet` for a game whose board is drawn as rows of aligned cells rather than
    /// as walled ones.
    ///
    /// Rows of aligned cells are laid out for a table of counts beside names, which wants air
    /// between its lines. A board drawn that way is a picture, and air between the lines of a
    /// picture pulls it into a column of stripes. The page is already in a face where every
    /// character is the same width, so the other half of it is had for nothing.
    ///
    /// Offered here because it is a fact about the page's own layout rather than about any
    /// board: two games had found the same line by hand, and a third drawing its board the
    /// same way would have found it again.
    let tightRows =
        """
.rows .row > span { padding-bottom: 0; line-height: 1.15; }
"""

    // --- the small change ------------------------------------------------------------

    /// An attribute, with its value escaped.
    ///
    /// `Falco.Markup` escapes what goes between the tags and leaves what goes inside the
    /// quotes exactly as handed to it, which is a sharp edge worth blunting in one place
    /// rather than remembering at a dozen. Every attribute in the program whose value is
    /// built rather than written out goes through here.
    ///
    /// It matters most for the controls. What a button does is a scrap of the client's own
    /// language sitting in an attribute, and that language uses quotes and ampersands the
    /// way any language does - `evt.key === 'Enter' && ...` is four characters HTML would
    /// rather read as markup. The browser hands the value back decoded, so what the client
    /// finally reads is what was written here.
    let attr name (value: string) =
        Attr.create name (Net.WebUtility.HtmlEncode value)

    /// An attribute of the client's that stands for itself, given an empty value instead.
    ///
    /// `data-bind:line` and `data-bind:line=""` say the same thing to the client - it takes
    /// the key and treats an empty value as no value at all - and only the second is
    /// well-formed markup, which is what the checks hold every screen to. So the name still
    /// comes from `Falco.Datastar`; only how it is written down is settled here.
    ///
    /// The value is deliberately not touched. Some of what the library writes is escaped
    /// already and some is not, and running an escaper over the lot would turn a `&quot;`
    /// into an `&amp;quot;` and break the one thing that was right.
    let valued =
        function
        | NonValueAttr name -> KeyValueAttr(name, "")
        | given -> given

    let quiet text =
        Elem.span [ Attr.class' "quiet" ] [ Text.enc text ]

    /// Writing that is there to be read once, and gone the moment the notes are off.
    let note text =
        Elem.p [ Attr.class' "note" ] [ Text.enc text ]

    let block title content =
        Elem.section [ Attr.class' "block" ] (Elem.h2 [] [ Text.enc title ] :: content)

    let lines (text: string) = Elem.pre [] [ Text.enc text ]

    /// A control that types a line for the player.
    ///
    /// The line goes in the address rather than in a signal, so a button carries the whole
    /// of what it does and needs nothing on the page to be in any particular state for it
    /// to mean that. Escaped once, which is also what keeps it out of trouble inside the
    /// attribute: what comes out has no quote, space or bracket left in it.
    ///
    /// This is the page's whole bargain with the parser, and it is why it is written once:
    /// every button on every game's page posts the words a player would have typed at the
    /// prompt, so a button cannot ask for something the parser would not take.
    let types line caption =
        Elem.button
            [ Attr.class' "types"
              attr "title" line
              Ds.onClick (Ds.post $"{Say}?line={Uri.EscapeDataString line}") ]
            [ Text.enc caption ]

    // --- the two slots a fragment lands in ---------------------------------------------

    let screen content =
        renderNode (Elem.main [ Attr.id Screen ] content)

    let aside content =
        renderNode (Elem.aside [ Attr.id Told ] content)

    /// One line with no board to go with it. Every game says these and says them the same
    /// way, so a game's own view need do no more than point here.
    let says (text: string) =
        aside [ Elem.div [ Attr.class' "said" ] [ Text.enc text ] ]

    // --- the page the fragments land in --------------------------------------------------

    /// The rules of how the page is laid out, less the colours, which are a player's own and
    /// are written in above this as custom properties.
    ///
    /// The page and nothing of any game: the chrome, the prompt, the door, the corner, and
    /// the few row shapes a fragment is built out of. What a game's own pieces look like is
    /// the game's, and arrives with the shell.
    let private sheet =
        """
* { box-sizing: border-box; }
body {
  --edge: #6a6a72; --ground: #14141a; --raised: #1d1d25; --ink: #d8d8e0;
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

.types {
  font: inherit; color: var(--edge); background: transparent; cursor: pointer;
  border: 1px solid var(--edge); border-radius: .25rem; padding: 0 .4rem;
}
.types:hover { color: var(--ground); background: var(--yours); border-color: var(--yours); }

.player, .entry { display: flex; gap: .6rem; align-items: baseline; }
.player.yours .who { color: var(--yours); }
.marker { color: var(--yours); width: 1.2rem; }
.who { min-width: 12ch; }
.said { max-width: 100ch; }

/* The shapes a described screen is built out of - a `Scene`, drawn by `Readers.Pages`.
   They are here rather than with a game because not one of them is one: a block beside
   another block, cells in rows, a wall round a cell and a glyph drawn large are what any
   game's screen is made of, and a game that had to write these again would be a game
   with an opinion about what a row is. `--cell` is the one measurement, because a grid's
   rows are shifted in halves of it. */
.beside { display: flex; gap: 1.4rem; flex-wrap: wrap; align-items: flex-start; }
.rows { display: table; border-spacing: .6rem 0; margin-left: -.6rem; }
.rows .row { display: table-row; }
.rows .row > span { display: table-cell; padding-bottom: .2rem; white-space: pre; }

.grid { --cell: 4.5rem; display: inline-flex; flex-direction: column; gap: 4px; margin: .2rem 0; }
.grid .row { display: flex; gap: 4px; }
.tile {
  min-width: var(--cell); min-height: var(--cell);
  display: flex; flex-direction: column; align-items: center; justify-content: center;
  border: 1px solid var(--edge); border-radius: .4rem; background: var(--raised);
}
.tile h3 { font-size: .7rem; font-weight: 600; margin: 0 0 .2rem; color: var(--edge); }
.tile .types { width: 100%; height: 100%; border: none; background: transparent; }
.tile:hover { border-color: var(--yours); }
.big { font-size: 2.2rem; font-weight: 700; line-height: 1; }

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

.door { display: flex; gap: .5rem; max-width: 44ch; }
.door input {
  flex: 1; font: inherit; color: var(--ink); background: var(--ground);
  border: 1px solid var(--edge); border-radius: .25rem; padding: .35rem .6rem;
}
.door input:focus { outline: none; border-color: var(--yours); }

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

    /// What the page does when the table stops answering.
    ///
    /// The client reopens a stream that fails, and keeps at it for a good while - so this is
    /// what happens *after* that has run out, which over a long wire it eventually does: a
    /// laptop shut for an hour, a phone that changed networks, a tunnel restarted at the
    /// other end. What is wanted then is the thing a person would do, which is to try the
    /// address again every few seconds and come back to the game when it answers.
    ///
    /// A reload rather than anything cleverer, because a page here holds nothing worth
    /// keeping: the board is drawn at the table, the seat is remembered by a cookie, and
    /// what comes back is the game exactly where it was left. The one thing lost is a line
    /// half-typed into the box, which is a fair price for a page that heals itself.
    ///
    /// It is asked for two ways because the stream fails two ways. One is the client giving
    /// up, which it says out loud on the document; the other is a connection that stops
    /// arriving without ever failing - which nothing announces, and which is why the table
    /// says something harmless on a timer for this to count the gaps between.
    let private holding =
        String.concat
            ""
            [ "let beat=0,going=false;"
              "const regain=()=>{if(going)return;going=true;"
              $"const said=document.getElementById('{Told}');"
              "if(said)said.textContent='The table stopped answering. Trying to reach it again...';"
              "const again=()=>fetch(location.href,{method:'HEAD',cache:'no-store'})"
              ".then(()=>location.reload()).catch(()=>setTimeout(again,3000));"
              "setTimeout(again,1000)};"
              // Four missed beats before anything is done about it, so that a tab left in
              // the background - where a browser is free to run a timer late - is not
              // reloaded out from under somebody for being patient.
              "const watch=()=>{clearTimeout(beat);beat=setTimeout(regain,90000)};"
              "window.alive=watch;watch();"
              "document.addEventListener('datastar-fetch',e=>{"
              "const doing=e.detail?e.detail.type:'';"
              "if(doing==='retries-failed')regain();else if(doing==='started')watch()});" ]

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
              "const said=new Notification('Your turn',{body:calm,tag:'turn',renotify:true});"
              "said.onclick=()=>{window.focus();said.close()}}};"
              $"const asking=document.getElementById('{Notify}');"
              "if(window.Notification?Notification.permission==='default':false)"
              "asking.onclick=()=>Notification.requestPermission().then(()=>asking.remove());"
              "else asking.remove()" ]

    /// Which colour is drawn for what, offered the way a page offers anything: a form that
    /// asks for the page again, saying what to draw it in this time.
    ///
    /// It needs no client at all - it is a form, and a browser has known what to do with
    /// one of those since long before any of this. Every choice is written as the words a
    /// console would have typed at the colour screen and sent down the wire, and read at
    /// the other end by the same `Palette.read` a console's are, so there is one spelling
    /// of a palette in the program and not a second one for the web.
    ///
    /// What there is to colour is the game's, and comes off the palette rather than being
    /// listed here: a game with two pieces offers two choosers and one with five offers
    /// five, from the same markup.
    let private colours palette =
        let choosing (slot: Slot) =
            let holding = Palette.inSlot slot palette

            Elem.label
                []
                [ Text.enc slot.Key
                  Elem.select
                      [ attr "name" "colours" ]
                      (Palette.shades
                       |> List.map (fun shade ->
                           Elem.option
                               ([ attr "value" $"{slot.Key}={shade.Name}" ]
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
                  ((Palette.slots palette |> List.map choosing)
                   @ [ Elem.button [ Attr.class' "types" ] [ Text.raw "redraw" ] ]) ]

    let private styles shell palette =
        let named =
            Palette.slots palette
            |> List.map (fun slot -> $"  --{slot.Key}: {Palette.paint (Palette.inSlot slot palette)};")

        String.concat Environment.NewLine ([ ":root {" ] @ named @ [ "}"; sheet; shell.Sheet ])

    /// How the page holds its stream open.
    ///
    /// One change from what the client would do left alone, and it is about a game rather
    /// than a dashboard: the stream stays open while the tab is hidden. Being elsewhere is
    /// the state this game is played in - a stream that closed itself the moment somebody
    /// looked at another tab could not knock when their turn came round, which is the one
    /// thing it is there for.
    ///
    /// What is deliberately *not* set here is how hard the client tries to reopen a stream
    /// that failed. Two of those knobs are spelt differently by the library and by the
    /// client carried in `assets` - they version separately, which is what
    /// [html.fsx](tests/html.fsx) exists to notice - and none of them covers a stream that
    /// ended tidily or one that stopped arriving without saying so. So the page keeps its
    /// own watch instead, above, and this asks for the one thing that is unambiguous.
    let private streaming =
        { RequestOptions.Defaults with
            OpenWhenHidden = true }

    /// The page itself: a shell with two empty slots in it, and a line of typing at the
    /// bottom.
    ///
    /// Nothing of the game is in here. The page opens the stream, the table answers it
    /// with a board, and every board after that arrives the same way - so what a browser
    /// is first sent and what it is sent on the fiftieth move are built by the same code,
    /// and a page that renders can be trusted to keep rendering.
    ///
    /// The palette is spent here and nowhere else. Every fragment a game draws works in
    /// custom properties rather than in colours of its own, which is what lets two people at
    /// one table be sent the same board and read it in colours that have nothing to do with
    /// each other.
    let page shell palette =
        let asked = Uri.EscapeDataString(Palette.write palette)

        renderHtml (
            Elem.html
                [ Attr.lang "en" ]
                [ Elem.head
                      []
                      [ Elem.meta [ Attr.create "charset" "utf-8" ]
                        Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
                        Elem.title [] [ Text.enc shell.Title ]
                        Elem.style [] [ Text.raw (styles shell palette) ]
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
                      [ Ds.signals nothingTyped
                        Ds.onInit (Ds.get ($"{Stream}?colours={asked}", streaming)) ]
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
                                    attr "placeholder" shell.Placeholder ]
                              Elem.button [ Attr.class' "types"; Ds.onClick (Ds.post Say) ] [ Text.raw "send" ] ]
                        // Last, and not in the head with the other one, because they wire
                        // themselves to things further up the page and have to find them
                        // there: a button, and the corner the table's news lands in.
                        Elem.script [] [ Text.raw answering ]
                        Elem.script [] [ Text.raw holding ] ] ]
        )

    /// The door, for somebody who arrived at a table without the word for it.
    ///
    /// A page rather than a bare refusal, because the person on the other side of this is a
    /// player who was sent an address, and "403" is not an instruction anybody can act on.
    /// It is a plain form: no client, no stream, nothing of the game - the table has said
    /// nothing to this browser yet and will not until it comes back with the word.
    ///
    /// What it asks for is written into the address, which is where the word is read from
    /// anyway - so somebody who was given the whole address in the first place never sees
    /// this page, and somebody who was given the two halves separately can put them
    /// together here.
    let locked shell palette (again: bool) =
        renderHtml (
            Elem.html
                [ Attr.lang "en" ]
                [ Elem.head
                      []
                      [ Elem.meta [ Attr.create "charset" "utf-8" ]
                        Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
                        Elem.title [] [ Text.enc shell.Title ]
                        Elem.style [] [ Text.raw (styles shell palette) ] ]
                  Elem.body
                      []
                      [ Elem.main
                            [ Attr.id Screen ]
                            [ Elem.h1 [] [ Text.raw "This table has a word at the door" ]
                              block
                                  "The word"
                                  [ Elem.form
                                        [ attr "method" "get"; attr "action" "/"; Attr.class' "door" ]
                                        [ Elem.input
                                              [ attr "name" Reach.Asked
                                                attr "autofocus" "autofocus"
                                                Attr.autocomplete "off"
                                                attr "placeholder" "the word whoever opened the table read out" ]
                                          Elem.button [ Attr.class' "types" ] [ Text.raw "sit down" ] ]
                                    note (
                                        if again then
                                            "That is not the word for this table. Whoever opened it can read it out again."
                                        else
                                            "Whoever opened this table was shown a word for it. It goes here, or on the end of the address."
                                    ) ] ] ] ]
        )

    /// One table as a house's front page lists it: where it is, how far along it is, and
    /// whether there is a seat going spare.
    ///
    /// Plain strings and a number, because this file has never met a house and should not
    /// start now. What a stage is called and how a seat count reads are the house's answers;
    /// what a row looks like is this one's.
    type Row =
        {
            Where: string
            /// What the table is called, which a person at a terminal has to be able to read and
            /// type. A browser follows the link and never sees it; a console is told `--table`
            /// and this is the word that goes after it.
            Name: string
            Stage: string
            Seats: string
            Sitters: string
            Spare: bool
        }

    /// A house's front page: the tables there are, and the sizes one can be opened at.
    ///
    /// No stream and no client script. A house does not change under anybody the way a board
    /// does - a table opened while you were reading is a table you find by looking again - so
    /// this is a page that is fetched, read and left, and the whole of its machinery is a
    /// handful of links.
    ///
    /// The board pages it links to are the ones every other way of serving this game already
    /// draws, which is the point: a house adds a front door and changes nothing behind it.
    let house shell palette (opening: (int * string) list) (rows: Row list) =
        let table (row: Row) =
            Elem.li
                [ Attr.class' (if row.Spare then "spare" else "taken") ]
                [ Elem.a [ Attr.href row.Where; Attr.class' "types" ] [ Text.enc (if row.Spare then "sit down" else "look on") ]
                  Elem.span [] [ Text.enc $"{row.Stage} - {row.Seats}" ]
                  // The name in plain sight, because it is the whole of what somebody at a
                  // terminal needs and the link they cannot click carries it invisibly.
                  Elem.span [ Attr.class' "quiet" ] [ Text.enc row.Name ]
                  note row.Sitters ]

        renderHtml (
            Elem.html
                [ Attr.lang "en" ]
                [ Elem.head
                      []
                      [ Elem.meta [ Attr.create "charset" "utf-8" ]
                        Elem.meta [ Attr.name "viewport"; Attr.content "width=device-width, initial-scale=1" ]
                        Elem.title [] [ Text.enc shell.Title ]
                        Elem.style [] [ Text.raw (styles shell palette) ] ]
                  Elem.body
                      []
                      [ Elem.main
                            [ Attr.id Screen ]
                            [ Elem.h1 [] [ Text.enc shell.Title ]
                              block
                                  "Open a table"
                                  [ Elem.p
                                        [ Attr.class' "opening" ]
                                        [ for players, where in opening do
                                              Elem.a [ Attr.href where; Attr.class' "types" ] [ Text.enc $"for {players}" ] ] ]
                              block
                                  "Tables"
                                  [ match rows with
                                    | [] -> note "None yet. Open one, and read its address out to whoever is playing."
                                    | rows -> Elem.ul [ Attr.class' "tables" ] [ for row in rows -> table row ] ] ] ] ]
        )
