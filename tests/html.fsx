// The page: that it is a page at all, that it lands where it is aimed, and that every
// button on it types something the game would take.
//
// `view.fsx` holds this view to the rule every view keeps - that no player is shown what
// the game means them not to see - and sweeps it up with the other two. What is here is
// what only a page can get wrong.
//
// Three things, and the third is the one that matters. A fragment has to be well-formed,
// because a browser handed broken markup does not complain, it quietly draws something
// else. It has to carry the id it will be patched by, because that is the whole of how the
// table knows where to put a board. And every control on it has to be a line the game's own
// parser accepts - which is the record's bargain, that moves are written in the words the
// prompt takes, held to by a view that has buttons instead of a prompt. A button that could
// send something `Parse` will not read would be a second language on the page, and the
// program has spent a great deal of effort not having one of those.
//
//   dotnet fsi tests/html.fsx

#r "nuget: Falco.Datastar, 1.3.0"
#r "nuget: Falco.Markup, 1.4.0"
#r "nuget: Spectre.Console, 0.51.1"

#load "Harness.fsx"
#load "../src/App/Messages.fs"
#load "../src/App/Session.fs"
#load "../src/App/Timeline.fs"
#load "../src/App/Journal.fs"
#load "../src/App/Model.fs"
#load "../src/App/Update.fs"
#load "../src/Console/Waiting.fs"
#load "../src/Console/Words.fs"
#load "../src/Console/Render.fs"
#load "../src/Console/Parse.fs"
#load "../src/Console/Palette.fs"
#load "../src/Console/Tint.fs"
#load "../src/Console/Rich.fs"
#load "../src/Console/Html.fs"
#load "../src/Console/View.fs"

open System
open System.Net
open System.Text.RegularExpressions
open System.Xml
open TCModel.Domain
open TCModel.App
open TCModel.Console
open Harness

let private dealt = Update.start 3 42UL |> Result.toOption |> Option.get

let private beholder = Game.active (Model.game dealt)

/// A game stopped in the middle of a negotiation, so that the controls a table offers only
/// while a stone is owed are drawn at least once here.
let private owing = dealt |> Update.update (Make Negotiate)

let private page = Html.page Palette.standard

let private view = View.html Palette.standard

/// Every screen this view draws, and the slot each is aimed at. A page has two places
/// anything ever lands - the board, and whatever the game last said - and which of the two
/// a screen goes to is settled by the fragment itself, because the fragment is the only
/// thing that knows what kind of screen it is.
let private screens =
    [ "board", Html.Screen, view.Board true beholder dealt
      "board, mid-negotiation", Html.Screen, view.Board true (Game.active (Model.game owing)) owing
      "board with the notes off", Html.Screen, view.Board false beholder dealt
      "waiting",
      Html.Screen,
      view.Waiting
          [ { Player = beholder.Id
              Expected = false
              Away = false
              Yours = true } ]
      "a line the game said", Html.Told, view.Says "It is Player 2's turn."
      "the record", Html.Told, view.History beholder dealt
      "the working behind a ruling", Html.Told, view.Ruling (List.head Board.regions).Id dealt
      "the rules", Html.Told, view.Rules ]

// --- markup that is markup --------------------------------------------------------------
//
// A browser handed something broken does not say so. It guesses, draws whatever it made of
// the mess, and leaves nobody any the wiser - so being well-formed is checked here, where
// it can still fail out loud.

/// Read a screen as a document, so that being well-formed is checked rather than assumed:
/// tags balanced, attributes quoted, entities real.
///
/// Namespace-unaware on purpose. `data-on:click` is exactly how the client wants an event
/// written and a perfectly good HTML attribute name, and to anything reading strict XML a
/// colon means a namespace prefix that was never declared. The question here is whether a
/// browser can make sense of this, not whether an XML parser can.
let private read (markup: string) =
    let document = XmlDocument()
    use reader = new XmlTextReader(new IO.StringReader(markup), Namespaces = false)
    document.Load reader
    document

let private parses (markup: string) =
    try
        read markup |> ignore
        true
    with _ ->
        false

for name, _, markup in screens do
    report $"the {name} is well-formed markup" true (parses markup)

// The page is held to the same standard, which costs it two spellings: an attribute that
// stands for itself in HTML - `selected`, `autofocus` - has to be written out in full. Both
// forms are HTML, only one is also well-formed, and the price of picking that one is a word
// each in two places.

report "and so is the page itself" true (parses page)

// --- and lands where it is aimed ------------------------------------------------------------

for name, slot, markup in screens do
    report
        $"the {name} is one element, carrying the id it will be patched by"
        slot
        ((read markup).DocumentElement.GetAttribute "id")

report "the page has a place for a board" true (page.Contains $"id=\"{Html.Screen}\"")

report "and a place for what the game says without one" true (page.Contains $"id=\"{Html.Told}\"")

report "and carries the client rather than sending anybody off to fetch it" true (page.Contains Html.Client)

// --- attributes the carried client has actually heard of --------------------------------------
//
// This is the check that was missing, and the bug it would have caught cost an afternoon.
//
// An attribute that takes a key separates the key from the plugin's name with a colon:
// `data-on:click`. Written `data-on-click` the client looks for a plugin called `on-click`,
// does not find one, and says nothing whatsoever - so the page renders, the stream opens,
// the board draws, and not one button on it does anything. No error, no warning, nothing in
// the console. A page can be entirely inert and look completely well.
//
// So the vocabulary is not taken on trust here. The client is a file in this repository;
// what it registers can be read out of it, and every attribute the page emits is held
// against that, split the way the client itself splits them.

let private client =
    IO.File.ReadAllText(IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "assets", "datastar.js"))

/// The attribute plugins the carried client registers, read out of the client.
let private known =
    Regex.Matches(client, @"p\(\{name:""([a-z-]+)""")
    |> Seq.map (fun found -> found.Groups[1].Value)
    |> Set.ofSeq

// If the client is ever rebuilt in a shape this cannot read, the checks below would all
// pass by finding nothing to complain about. So the reading itself is checked first.

report "the carried client's own vocabulary can be read out of it" true (Set.count known > 8 && known.Contains "on")

/// A `data-` attribute as the client parses it: modifiers off the end after `__`, then the
/// plugin's name off the front at the first colon.
let private pluginOf (attribute: string) =
    let withoutData = attribute.Substring 5
    let beforeMods = (withoutData.Split "__")[0]
    (beforeMods.Split(':', 2))[0]

let private attributes (markup: string) =
    Regex.Matches(markup, @"\sdata-([a-zA-Z:._-]+)=")
    |> Seq.map (fun found -> "data-" + found.Groups[1].Value)
    |> List.ofSeq

let private everywhere =
    page :: (screens |> List.map (fun (_, _, markup) -> markup))
    |> List.collect attributes

report "the page and its screens do carry client attributes at all" true (List.length everywhere > 3)

report
    "and every one of them names a plugin the carried client registers"
    []
    (everywhere
     |> List.map pluginOf
     |> List.distinct
     |> List.filter (known.Contains >> not))

// --- every button types something the game would take ------------------------------------------

/// What a control on the page would send. The address is written into the markup escaped
/// twice over - once for the client's own language and once for HTML - so it comes back the
/// same way, and what is left after both is the line a player would have typed.
let private posted (markup: string) =
    Regex.Matches(WebUtility.HtmlDecode markup, @"@post\('/say\?line=([^']*)'\)")
    |> Seq.map (fun found -> Uri.UnescapeDataString found.Groups[1].Value)
    |> List.ofSeq

let private lines = screens |> List.collect (fun (_, _, markup) -> posted markup)

report "the board has controls on it at all" true (List.length lines > 20)

let private refused =
    lines
    |> List.distinct
    |> List.filter (fun line ->
        match Parse.line line with
        | Ok Parse.Nothing
        | Error _ -> true
        | Ok _ -> false)

report "and every one of them types a line the game's own parser takes" [] refused

// The one control that is drawn only while a stone is owed, and so the one that would go
// unnoticed if the checks above only ever looked at a board with nothing owing on it.

report
    "a table waiting on a stone offers the move that hands one back"
    true
    (posted (view.Board true (Game.active (Model.game owing)) owing)
     |> List.exists (fun line -> line.StartsWith "return "))

// --- the colours ---------------------------------------------------------------------------
//
// A page carries its colours in its own head and every fragment draws in those, which is
// what lets one board be built and read by however many people in however many palettes.
// So the palette has to reach the page, and has to reach nothing else.

let private redIsTeal =
    Palette.set "red" "teal" Palette.standard |> Result.toOption |> Option.get

let private mentions (needle: string) (text: string) = text.Contains needle

report "the page is drawn in the palette it is given" true (Html.page redIsTeal |> mentions (Palette.paint redIsTeal.Red))

report "and not in the one it was not" false (Html.page redIsTeal |> mentions (Palette.paint Palette.standard.Red))

report
    "the board itself is the same board whatever the colours"
    (view.Board true beholder dealt)
    ((View.html redIsTeal).Board true beholder dealt)

report
    "the colours are offered as the same words a console types for them"
    true
    (Html.page redIsTeal |> mentions "value=\"red=teal\"")

// --- being told the turn has come round ------------------------------------------------
//
// The one thing a table says to a browser that is not a piece of the page. It goes down the
// stream as a line of the page's own script, so both ends of it are in `Html` and both are
// held here: a name that drifted between the knock and the page would leave the browser
// running something nobody had written, and the way that shows is a bell that never rings.

/// The knock is a call, so what the page has to define is the name in front of the brackets.
let private knocked = Html.Nudge.TrimEnd('(', ')')

report "what the stream knocks on is a name the page defines" true (page |> mentions $"window.{knocked}=")

// Every other control on this page is a line of typing. This one cannot be: a browser only
// takes the question from a click it has just seen, and a typed line has been to the table
// and back before anything on the page could ask. So it is a button, and the page's own
// script goes looking for it by name.

report "the page carries the button that asks the browser's permission" true (page |> mentions $"id=\"{Html.Notify}\"")

report "and the page's own script goes looking for it by that name" true (page |> mentions $"getElementById('{Html.Notify}')")

finish ()
