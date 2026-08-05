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
open System.Xml.Linq
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

let private parses (markup: string) =
    try
        XElement.Parse markup |> ignore
        true
    with _ ->
        false

for name, _, markup in screens do
    report $"the {name} is well-formed markup" true (parses markup)

// The page is held to the same standard, which costs it two spellings: an attribute that
// stands for itself in HTML - `selected`, `autofocus` - has to be written out in full. Both
// forms are HTML, only one is also well-formed, and the price of picking that one is a word
// each in two places.

report
    "and so is the page itself"
    true
    (try
        XDocument.Parse page |> ignore
        true
     with _ ->
         false)

// --- and lands where it is aimed ------------------------------------------------------------

for name, slot, markup in screens do
    report
        $"the {name} is one element, carrying the id it will be patched by"
        slot
        (XElement.Parse(markup).Attribute(XName.Get "id").Value)

report "the page has a place for a board" true (page.Contains $"id=\"{Html.Screen}\"")

report "and a place for what the game says without one" true (page.Contains $"id=\"{Html.Told}\"")

report "and carries the client rather than sending anybody off to fetch it" true (page.Contains $"src=\"{Html.Client}\"")

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

finish ()
