#load "Whole.fsx"

open System
open System.Net
open System.Text.RegularExpressions
open System.Xml
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Turncoats
open Harness
open Whole

let private dealt = Playing.start 3 42UL |> Result.toOption |> Option.get

let private beholder = (Game.active (Playing.game dealt)).Id

let private owing = dealt |> Playing.update (Make Negotiate)

let private page = Page.page playing.Page standard

let private view = asPage

let private pageIn palette = Page.page playing.Page palette

let private viewIn palette =
    Playable.plainest InABrowser palette playing

let private screens =
    [ "board", Page.Screen, view.Board Margins.all beholder dealt
      "board, mid-negotiation", Page.Screen, view.Board Margins.all (Game.active (Playing.game owing)).Id owing
      "board with the notes off", Page.Screen, view.Board Margins.none beholder dealt
      "waiting",
      Page.Screen,
      view.Waiting
          [ { Player = beholder
              Expected = false
              Away = false
              Yours = true } ]
      "a line the game said", Page.Told, view.Says "It is Player 2's turn."
      "the record", Page.Told, view.History beholder dealt
      "the working behind a ruling", Page.Told, view.Answer beholder $"rule {Words.number (List.head Board.regions).Id}" dealt
      "the rules", Page.Told, view.Rules ]


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


report "and so is the page itself" true (parses page)


for name, slot, markup in screens do
    report
        $"the {name} is one element, carrying the id it will be patched by"
        slot
        ((read markup).DocumentElement.GetAttribute "id")

report "the page has a place for a board" true (page.Contains $"id=\"{Page.Screen}\"")

report "and a place for what the game says without one" true (page.Contains $"id=\"{Page.Told}\"")

report "and carries the client rather than sending anybody off to fetch it" true (page.Contains Page.Client)


let private client =
    IO.File.ReadAllText(IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "assets", "datastar.js"))

let private known =
    Regex.Matches(client, @"p\(\{name:""([a-z-]+)""")
    |> Seq.map (fun found -> found.Groups[1].Value)
    |> Set.ofSeq


report "the carried client's own vocabulary can be read out of it" true (Set.count known > 8 && known.Contains "on")

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
        match Playable.read playing line with
        | Ok Nothing
        | Error _ -> true
        | Ok _ -> false)

report "and every one of them types a line the game's own parser takes" [] refused


report
    "a table waiting on a stone offers the move that hands one back"
    true
    (posted (view.Board Margins.all (Game.active (Playing.game owing)).Id owing)
     |> List.exists (fun line -> line.StartsWith "return "))


let private onTheMap =
    Board.layout |> List.collect (List.map fst) |> List.map Board.region

let private short colour =
    (Words.glyph colour |> string).ToLowerInvariant()

let private allowed =
    [ for region in onTheMap do
          if region.Kind <> Dead then
              let here = Game.stones region.Id (Playing.game dealt)

              for colour in StoneColor.all do
                  if Pile.count colour here > 0 then
                      yield $"battle {short colour} {Words.number region.Id}"

                      for other in Board.neighbours region.Id do
                          if (Board.region other).Kind <> Dead then
                              yield $"march {short colour} {Words.number region.Id} {Words.number other}" ]

let private offered =
    posted (view.Board Margins.all beholder dealt)
    |> List.filter (fun line -> line.StartsWith "battle " || line.StartsWith "march ")

report "the board does offer battles and marches at all" true (List.length allowed > 20)

report
    "and exactly the ones the position allows: one per colour standing there, to each region it borders"
    (List.sort allowed)
    (List.sort offered)


report
    "no march is offered between two regions that do not border each other"
    []
    (offered
     |> List.filter (fun line -> line.StartsWith "march ")
     |> List.filter (fun line ->
         match Commands.words line with
         | [ _; _; from; into ] ->
             match Board.tryId (int from), Board.tryId (int into) with
             | Some from, Some into -> not (Board.areAdjacent from into)
             | _ -> true
         | _ -> true))


let private bare =
    onTheMap
    |> List.filter (fun region -> Pile.isEmpty (Game.stones region.Id (Playing.game dealt)))

report "there is a region with nothing standing on it" true (not (List.isEmpty bare))

report
    "and it offers nothing to do with what is not there"
    []
    (bare
     |> List.collect (fun region -> offered |> List.filter (fun line -> line.EndsWith $" {Words.number region.Id}")))

let private redIsTeal =
    Palette.set "red" "teal" standard |> Result.toOption |> Option.get

let private mentions (needle: string) (text: string) = text.Contains needle

report
    "the page is drawn in the palette it is given"
    true
    (pageIn redIsTeal |> mentions (Palette.paint (Palette.shadeOf "red" redIsTeal)))

report "and not in the one it was not" false (pageIn redIsTeal |> mentions (Palette.paint (Palette.shadeOf "red" standard)))

report
    "the board itself is the same board whatever the colours"
    (view.Board Margins.all beholder dealt)
    ((viewIn redIsTeal).Board Margins.all beholder dealt)

report
    "the colours are offered as the same words a console types for them"
    true
    (pageIn redIsTeal |> mentions "value=\"red=teal\"")


let private knocked = Page.Nudge.TrimEnd('(', ')')

report "what the stream knocks on is a name the page defines" true (page |> mentions $"window.{knocked}=")


report "the page carries the button that asks the browser's permission" true (page |> mentions $"id=\"{Page.Notify}\"")

report "and the page's own script goes looking for it by that name" true (page |> mentions $"getElementById('{Page.Notify}')")

finish ()
