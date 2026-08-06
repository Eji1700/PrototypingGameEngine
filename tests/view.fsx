// The ways of showing the game, and the colours they are shown in.
//
// A view may lay the board out however it likes - `plain` writes one block of text, `rich`
// builds panels, tables and charts, and `html` builds a page - but no view may show a
// player anything the game means them not to see. A third renderer is a third chance to
// leak, so every view there is gets held to that here, whether or not anybody remembered to
// come back and add it to this file.
//
//   dotnet fsi tests/view.fsx

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
#load "../src/Console/Options.fs"

open System.Text.RegularExpressions
open TCModel.Domain
open TCModel.App
open TCModel.Console
open Harness

let private dealt = Update.start 2 42UL |> Result.toOption |> Option.get

let private seats = Game.players (Model.game dealt)

/// What a person would actually see, whatever the view wrote it in: colour taken back off,
/// and markup with it. The escape itself has to go with the codes; stripping only the codes
/// leaves it sitting between the letters, and a check for "R R R" would then never find one.
let private uncoloured text =
    Regex.Replace(text, "\u001b\\[[0-9;]*m", "")

/// Markup taken off too, so that what is left of a page is what somebody reading one would
/// have in front of them.
///
/// Only for boards. The game's own prose says things like `recruit <colour> <region>`, and
/// to a rule this blunt an angle bracket is an angle bracket - so anything checking what
/// the game *says* uses `uncoloured` above and stops there.
let private seen text =
    Regex.Replace(uncoloured text, "<[^>]*>", "")

/// Every way a bag is written anywhere in the program, and so every shape a leak could
/// take: laid out stone by stone, which is how `rich` draws one that is open; counted,
/// which is how `plain` draws it; and laid out with nothing at all between the stones,
/// which is what `html` comes to once its tags are off, each stone being an element of
/// its own.
let private spellings (player: Player) =
    let stones = player.Bag |> Pile.toColors |> List.map (Words.glyph >> string)

    [ String.concat " " stones; String.concat "" stones; Words.counted player.Bag ]

let private mentions (needle: string) (text: string) = text.Contains needle

let private spells (player: Player) (board: string) =
    spellings player |> List.exists (fun spelling -> board |> mentions spelling)

/// Every view, in the colours the game is drawn in unless somebody says otherwise. What a
/// player may see does not depend on what colour it is drawn in, and the checks below say
/// so in the one palette; the ones at the foot of this file are where colour is the point.
let private views = View.all Palette.standard

// --- the rule every view keeps ----------------------------------------------------------

// A sweep, so a view added later is held to this without anybody remembering to come back.
for view in views do
    for beholder in seats do
        let board = seen (view.Board true beholder dealt)

        report $"the {view.Name} view shows {Words.player beholder.Id} their own bag" true (board |> spells beholder)

        for other in seats |> List.filter (fun p -> p.Id <> beholder.Id) do
            report
                $"the {view.Name} view keeps {Words.player other.Id}'s bag from {Words.player beholder.Id}"
                false
                (board |> spells other)

// --- a stone drawn stays with the player who drew it ---------------------------------------

let private drawn = dealt |> Update.update (Make Negotiate)

let private drewColor =
    match Model.session drawn with
    | InPlay { Phase = AwaitingReturn color } -> Words.color color
    | _ -> failwith "the negotiation did not leave a stone to hand back"

for view in views do
    let drawer, other = seats[0], seats[1]

    report
        $"the {view.Name} view names the drawn stone to the player who drew it"
        true
        (seen (view.Board true drawer drawn) |> mentions $"drew a {drewColor} stone")

    report
        $"the {view.Name} view does not name it to anybody else"
        false
        (seen (view.Board true other drawn) |> mentions $"drew a {drewColor} stone")

// --- the notes -----------------------------------------------------------------------------
//
// The writing that explains the board is `Render.Notes`, and every view shows all of it. Each
// wraps it to whatever width it has, which is the only part that is a view's own business.
//
// Written out per view they drifted, which is what this is here to stop: one view said two
// regions share a "side" and another a "wall", one called the dead region wild, and two of the
// four notes were shown by one view and quietly missing from the others. A note nobody is
// shown reads exactly like a note nobody needed.

/// A note as it reaches a reader, with everything that is only how it was drawn taken back
/// off: colour and markup, the walls of whatever box it was written inside - a note in a
/// Spectre panel has one down each side of every line - and the wrapping, run back together.
/// A page's entities are read back too, the map note having an apostrophe and an angle
/// bracket in it and a page writing both the long way round.
let private unwrapped text =
    let walls = Regex.Replace(seen text, "[─-╿]", "")
    let flat = Regex.Replace(walls, @"\s+", " ")

    flat.Replace("&#39;", "'").Replace("&gt;", ">").Replace("&lt;", "<")

let private notes =
    [ "the map", Render.Notes.map
      "what stands apart from it", Render.Notes.apart
      "how the land is counted", Render.Notes.landRuled
      "what is out of sight", Render.Notes.supply ]

for view in views do
    let shown = unwrapped (view.Board true seats[0] dealt)
    let hidden = unwrapped (view.Board false seats[0] dealt)

    for what, note in notes do
        let note = Regex.Replace(note, @"\s+", " ")

        report $"the {view.Name} view explains {what} in the words every view uses" true (shown |> mentions note)

        report $"and with the notes off says nothing of {what}" false (hidden |> mentions note)

// --- prose ---------------------------------------------------------------------------------

let private plain = View.plain Palette.standard

let private rich = View.rich Palette.standard

report "the plain view leaves what the game says exactly as it said it" Render.help (plain.Says Render.help)

report "the rich view colours prose without moving a character of it" Render.help (uncoloured (rich.Says Render.help))

report "and does colour it" true (rich.Says Render.help |> mentions "[")

// --- choosing one -----------------------------------------------------------------------------

report "every view answers to its own name" [ "plain"; "rich"; "html" ] (views |> List.map (fun view -> view.Name))

report
    "a view can be asked for by name"
    (Ok "rich")
    (View.byName AtATerminal Palette.standard "rich"
     |> Result.map (fun view -> view.Name))

report
    "and is not case-fussy about it"
    (Ok "plain")
    (View.byName AtATerminal Palette.standard "PLAIN"
     |> Result.map (fun view -> view.Name))

report
    "a name nobody answers to is refused, and says what there is"
    (Error "'fancy' is not a way of showing the game here. There is plain, rich.")
    (View.byName AtATerminal Palette.standard "fancy"
     |> Result.map (fun view -> view.Name))

// Which views there are is one question and which of them a reader could show is another.
// A terminal handed a page reads angle brackets and a browser handed the rich board reads
// escape codes, so neither is offered the other's - and the refusal says what there is
// rather than that the name is unknown, because the name is not the problem.

report
    "a terminal is not offered the page"
    (Error "'html' is not a way of showing the game here. There is plain, rich.")
    (View.byName AtATerminal Palette.standard "html"
     |> Result.map (fun view -> view.Name))

report
    "and a browser is not offered the terminal's boards"
    (Error "'rich' is not a way of showing the game here. There is html.")
    (View.byName InABrowser Palette.standard "rich"
     |> Result.map (fun view -> view.Name))

// --- the colours it is drawn in -----------------------------------------------------------------
//
// A palette is how the game is read rather than how it is played, so the whole of what it
// may do is change which escape codes come out. Nothing it does may move a character, and
// nothing it does may show a player one thing more than they could see before.

let private beholder = seats[0]

/// The standard palette with one thing moved: Red drawn in teal rather than crimson.
let private redIsTeal =
    Palette.set "red" "teal" Palette.standard |> Result.toOption |> Option.get

/// Whether a piece of writing has been coloured with a given eight-bit colour. 45 is teal
/// and 196 is crimson, which are the two this file moves a colour between. The escape
/// itself is written as its number: put in as the character it is, it would be a byte
/// nobody reading this file could see.
let private escape = string (char 0x1b)

let private inked code text =
    text |> mentions $"{escape}[38;5;{code}m"

report
    "a colour is changed by the words a person types"
    (Ok "teal")
    (Palette.set "blue" "teal" Palette.standard
     |> Result.map (fun palette -> palette.Blue.Name))

report
    "and only that one"
    (Ok [ "crimson"; "moss"; "gold"; "slate" ])
    (Palette.set "blue" "teal" Palette.standard
     |> Result.map (fun palette ->
         [ palette.Red.Name
           palette.Green.Name
           palette.Yours.Name
           palette.Hidden.Name ]))

report
    "a colour nobody has is refused, and says what there is"
    true
    (match Palette.set "blue" "beige" Palette.standard with
     | Error problem -> problem |> mentions "'beige' is not a colour I have. There is crimson,"
     | Ok _ -> false)

report
    "and so is a thing nobody draws"
    (Error "There is nothing called 'walls' to colour. There is red, blue, green, yours, hidden.")
    (Palette.set "walls" "teal" Palette.standard |> Result.map (fun _ -> ()))

report "the rich board is drawn in the palette it is given" true ((View.rich redIsTeal).Board true beholder dealt |> inked 45)

report "and not in the one it was not" false ((View.rich redIsTeal).Board true beholder dealt |> inked 196)

report
    "colouring moves not one character of the board"
    (uncoloured (rich.Board true beholder dealt))
    (uncoloured ((View.rich redIsTeal).Board true beholder dealt))

report
    "the plain view is left plain by any of it"
    (plain.Board true beholder dealt)
    ((View.plain redIsTeal).Board true beholder dealt)

// A palette crosses a wire as the words a person would have typed, and is read back by the
// same function that reads them, so there is no second spelling of one to keep in step.

report
    "a palette comes back off the wire as it went on"
    [ "teal"; "azure"; "moss"; "gold"; "slate" ]
    (let there = Palette.read (Palette.write redIsTeal)

     [ there.Red.Name
       there.Blue.Name
       there.Green.Name
       there.Yours.Name
       there.Hidden.Name ])

report "and a word the far end does not know leaves that one as it was" "crimson" (Palette.read "red=beige blue=teal").Red.Name

// --- the screen that offers them ------------------------------------------------------------

report
    "two words are a colour for something"
    (Ok "teal")
    (match Options.choose Palette.standard "blue teal" with
     | Ok(Options.Changed palette) -> Ok palette.Blue.Name
     | Ok _ -> Error "no change"
     | Error problem -> Error problem)

report
    "'reset' puts them all back"
    "crimson"
    (match Options.choose redIsTeal "reset" with
     | Ok(Options.Changed palette) -> palette.Red.Name
     | _ -> "nothing")

report
    "'done' goes back to the menu"
    true
    (match Options.choose redIsTeal "done" with
     | Ok Options.Done -> true
     | _ -> false)

report
    "and an empty line simply asks again"
    true
    (match Options.choose redIsTeal "" with
     | Ok Options.Same -> true
     | _ -> false)

// The screen is written in the board's own words so that showing it through a view colours
// the samples on it - which is what makes it a look at the colours rather than a list of
// names for them.

// It is always shown through the view built in the very palette it is offering, which is
// what makes the samples on it a look at the colours rather than a list of names for them.

let private offering = View.rich redIsTeal

report "the colour screen shows what it is offering" true (offering.Says(Options.screen redIsTeal) |> inked 45)

finish ()
