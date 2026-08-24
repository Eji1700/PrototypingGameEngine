namespace Prototyping.Table

open System
open System.Text.Json.Serialization
open Falco.Markup
open Falco.Datastar

type Shell =
    { Title: string
      Sheet: string
      Placeholder: string

      Keys: (string * string) list }

module Page =

    [<Literal>]
    let Screen = "screen"

    [<Literal>]
    let Told = "told"

    [<Literal>]
    let Client = "/datastar.js"

    [<Literal>]
    let Stream = "/stream"

    [<Literal>]
    let Say = "/say"

    [<Literal>]
    let Amiss = "/amiss"

    [<Literal>]
    let Notify = "notify"

    [<Literal>]
    let Nudge = "nudged()"

    [<Literal>]
    let Alive = "alive()"

    /// What the page is asked to do when the table makes a sound. A `Sound` is named for what
    /// happened rather than for what it sounds like, so this is where the one becomes the other
    /// and it is the only place in the program that has an opinion about pitch.
    let rang sound = $"rang('{Sound.word sound}')"

    type Signals =
        { [<JsonPropertyName("line")>]
          Line: string }

    let nothingTyped = { Line = "" }

    let tightRows =
        """
.rows .row > span { padding-bottom: 0; line-height: 1.15; }
"""


    let attr name (value: string) =
        Attr.create name (Net.WebUtility.HtmlEncode value)

    let valued =
        function
        | NonValueAttr name -> KeyValueAttr(name, "")
        | given -> given

    let quiet text =
        Elem.span [ Attr.class' "quiet" ] [ Text.enc text ]

    let note text =
        Elem.p [ Attr.class' "note" ] [ Text.enc text ]

    let block title content =
        Elem.section [ Attr.class' "block" ] (Elem.h2 [] [ Text.enc title ] :: content)

    let lines (text: string) = Elem.pre [] [ Text.enc text ]

    let types line caption =
        Elem.button
            [ Attr.class' "types"
              attr "title" line
              Ds.onClick (Ds.post $"{Say}?line={Uri.EscapeDataString line}") ]
            [ Text.enc caption ]


    let screen content =
        renderNode (Elem.main [ Attr.id Screen ] content)

    let aside content =
        renderNode (Elem.aside [ Attr.id Told ] content)

    let says (text: string) =
        aside [ Elem.div [ Attr.class' "said" ] [ Text.enc text ] ]


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

/* A field: many small cells rather than a few big ones, one glyph each, laid out so that the
   legend across the top sits over the column it names. The moods a game gives a cell arrive
   here as further classes on `.speck`, and it is the game's own sheet that says what they
   look like - nothing general could know what turning, or landing, or lit is. */
.field { display: inline-flex; flex-direction: column; margin: .3rem 0 .6rem; line-height: 1.25; }
.field .row { display: flex; white-space: pre; }
.field .label, .field .legend { color: var(--edge); }
.field .speck { display: inline-block; width: 1ch; text-align: center; }

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

    /// The table's one voice. Three sounds, made out of nothing the page had to fetch, because a
    /// board that could not be heard until a file arrived would be silent for the first move of
    /// every game. A browser will not let a page make a noise before somebody has touched it, so
    /// the first touch is what wakes it, and until then this quietly does nothing at all.
    let private sounding =
        String.concat
            ""
            [ "let horn=null;const wake=()=>{try{horn=horn||new (window.AudioContext||window.webkitAudioContext)();"
              "if(horn.state===`suspended`)horn.resume()}catch(e){}};"
              "addEventListener(`pointerdown`,wake);addEventListener(`keydown`,wake);"

              // Five recipes: the notes to play, how far apart, how loud, how long each rings and
              // what shape of wave it is. A tap is one low blip; a chime and a fanfare climb, the
              // fanfare further and brighter; ready is a single clear note that says the board is
              // yours again; a knell falls rather than climbs, lower and slower than any of them.
              "const ways={"
              "tap:[[392],0.00,0.045,0.13,`triangle`],"
              "chime:[[659.25,987.77],0.07,0.06,0.19,`triangle`],"
              "fanfare:[[523.25,659.25,783.99,1046.5],0.075,0.075,0.28,`triangle`],"
              "ready:[[587.33],0.00,0.06,0.30,`sine`],"
              "knell:[[329.63,220],0.16,0.07,0.55,`sine`]};"

              "window.rang=w=>{wake();if(!horn||horn.state!==`running`)return;"
              "const way=ways[w];if(!way)return;"
              "const[notes,apart,loud,ring,shape]=way;const at=horn.currentTime;"
              "notes.forEach((hz,i)=>{const o=horn.createOscillator(),g=horn.createGain();"
              "o.type=shape;o.frequency.value=hz;const from=at+i*apart;"
              "g.gain.setValueAtTime(0.0001,from);g.gain.exponentialRampToValueAtTime(loud,from+0.012);"
              "g.gain.exponentialRampToValueAtTime(0.0001,from+ring);"
              "o.connect(g);g.connect(horn.destination);o.start(from);o.stop(from+ring+0.02)})}" ]

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
              "const watch=()=>{clearTimeout(beat);beat=setTimeout(regain,90000)};"
              "window.alive=watch;watch();"
              "document.addEventListener('datastar-fetch',e=>{"
              "const doing=e.detail?e.detail.type:'';"
              "if(doing==='retries-failed')regain();else if(doing==='started')watch()});" ]

    let private answering =
        String.concat
            ""
            [ "const calm=document.title;let marked=false;"
              "const settle=()=>{if(marked){marked=false;document.title=calm}};"
              "addEventListener('focus',settle);"
              "addEventListener('visibilitychange',()=>{if(!document.hidden)settle()});"
              "window.nudged=()=>{if(document.hasFocus())return;"
              "marked=true;document.title='Your turn - '+calm;"
              "if(window.Notification"
              "?Notification.permission==='granted':false){"
              "const said=new Notification('Your turn',{body:calm,tag:'turn',renotify:true});"
              "said.onclick=()=>{window.focus();said.close()}}};"
              $"const asking=document.getElementById('{Notify}');"
              "if(window.Notification?Notification.permission==='default':false)"
              "asking.onclick=()=>Notification.requestPermission().then(()=>asking.remove());"
              "else asking.remove()" ]

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

    let private streaming =
        { RequestOptions.Defaults with
            OpenWhenHidden = true }

    let private steering (keys: (string * string) list) =
        match keys with
        | [] -> ""
        | keys ->
            let table =
                keys
                |> List.map (fun (key, line) -> $"'{key}':'{Uri.EscapeDataString line}'")
                |> String.concat ","

            String.concat
                ""
                [ $"const steer={{{table}}};"
                  "addEventListener('keydown',e=>{"
                  "const at=document.activeElement;"
                  "const typing=at?at.tagName==='INPUT'||at.tagName==='TEXTAREA':false;"
                  "if(typing)return;"
                  "const line=steer[e.key];if(!line)return;e.preventDefault();"
                  $"fetch('{Say}?line='+line,{{method:'POST'}}).catch(()=>{{}})}})" ]

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
                        Elem.script
                            []
                            [ Text.raw (
                                  "let left=8;const tell=w=>{if(left-->0)fetch('"
                                  + Amiss
                                  + "',{method:'POST',body:w}).catch(()=>{})};"
                                  + "addEventListener('error',e=>tell((e.message||'')+' at '+(e.filename||'')+':'+(e.lineno||0)));"
                                  + "addEventListener('unhandledrejection',e=>tell('unsettled: '+e.reason))"
                              ) ]
                        Elem.script [ attr "type" "module"; Attr.src Client ] [] ]
                  Elem.body
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
                                    Ds.onEvent ("keydown", $"evt.key === 'Enter' ? {Ds.post Say} : null")
                                    attr "autofocus" "autofocus"
                                    Attr.autocomplete "off"
                                    attr "placeholder" shell.Placeholder ]
                              Elem.button [ Attr.class' "types"; Ds.onClick (Ds.post Say) ] [ Text.raw "send" ] ]
                        Elem.script [] [ Text.raw sounding ]
                        Elem.script [] [ Text.raw answering ]
                        Elem.script [] [ Text.raw holding ]
                        Elem.script [] [ Text.raw (steering shell.Keys) ] ] ]
        )

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

    type Row =
        { Where: string
          Name: string
          Stage: string
          Seats: string
          Sitters: string
          Spare: bool }

    let house shell palette (opening: (int * string) list) (rows: Row list) =
        let table (row: Row) =
            Elem.li
                [ Attr.class' (if row.Spare then "spare" else "taken") ]
                [ Elem.a [ Attr.href row.Where; Attr.class' "types" ] [ Text.enc (if row.Spare then "sit down" else "look on") ]
                  Elem.span [] [ Text.enc $"{row.Stage} - {row.Seats}" ]
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
