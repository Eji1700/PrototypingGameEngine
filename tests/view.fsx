// The ways of showing the game.
//
// A view may lay the board out however it likes - `plain` writes one block of text and
// `rich` builds panels, tables and charts - but no view may show a player anything the
// game means them not to see. A second renderer is a second chance to leak, so every view
// there is gets held to that here, whether or not anybody remembered to come back and add
// it to this file.
//
//   dotnet fsi tests/view.fsx

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
#load "../src/Console/Tint.fs"
#load "../src/Console/Rich.fs"
#load "../src/Console/View.fs"

open System.Text.RegularExpressions
open TCModel.Domain
open TCModel.App
open TCModel.Console
open Harness

let private dealt = Update.start 2 42UL |> Result.toOption |> Option.get

let private seats = Game.players (Model.game dealt)

/// Colour taken back off, leaving what a person would actually see. The escape itself
/// has to go with the codes; stripping only the codes leaves it sitting between the
/// letters, and a check for "R R R" would then never find one.
let private seen text = Regex.Replace(text, "\u001b\\[[0-9;]*m", "")

/// A bag stone by stone, which is how `rich` draws one that is open.
let private laidOut (player: Player) =
    player.Bag |> Pile.toColors |> List.map (Words.glyph >> string) |> String.concat " "

/// The same bag counted, which is how `plain` draws it. Between them these are every way
/// a bag is written anywhere in the program, and so every shape a leak could take.
let private tallied (player: Player) = Words.counted player.Bag

let private mentions (needle: string) (text: string) = text.Contains needle

// --- the rule every view keeps ----------------------------------------------------------

// A sweep, so a view added later is held to this without anybody remembering to come back.
for view in View.all do
    for beholder in seats do
        let board = seen (view.Board true beholder dealt)

        report
            $"the {view.Name} view shows {Words.player beholder.Id} their own bag"
            true
            (board |> mentions (laidOut beholder) || board |> mentions (tallied beholder))

        for other in seats |> List.filter (fun p -> p.Id <> beholder.Id) do
            report
                $"the {view.Name} view keeps {Words.player other.Id}'s bag from {Words.player beholder.Id}"
                false
                (board |> mentions (laidOut other) || board |> mentions (tallied other))

// --- a stone drawn stays with the player who drew it ---------------------------------------

let private drawn =
    dealt |> Update.update (Make Negotiate)

let private drewColor =
    match Model.session drawn with
    | InPlay { Phase = AwaitingReturn color } -> Words.color color
    | _ -> failwith "the negotiation did not leave a stone to hand back"

for view in View.all do
    let drawer, other = seats[0], seats[1]

    report
        $"the {view.Name} view names the drawn stone to the player who drew it"
        true
        (seen (view.Board true drawer drawn) |> mentions $"drew a {drewColor} stone")

    report
        $"the {view.Name} view does not name it to anybody else"
        false
        (seen (view.Board true other drawn) |> mentions $"drew a {drewColor} stone")

// --- prose ---------------------------------------------------------------------------------

report "the plain view leaves what the game says exactly as it said it" Render.help (View.plain.Says Render.help)

report
    "the rich view colours prose without moving a character of it"
    Render.help
    (seen (View.rich.Says Render.help))

report "and does colour it" true (View.rich.Says Render.help |> mentions "[")

// --- choosing one -----------------------------------------------------------------------------

report "every view answers to its own name" [ "plain"; "rich" ] (View.all |> List.map (fun view -> view.Name))

report "a view can be asked for by name" (Ok "rich") (View.byName "rich" |> Result.map (fun view -> view.Name))

report "and is not case-fussy about it" (Ok "plain") (View.byName "PLAIN" |> Result.map (fun view -> view.Name))

report
    "a name nobody answers to is refused, and says what there is"
    (Error "'fancy' is not a way of showing the game. There is plain, rich.")
    (View.byName "fancy" |> Result.map (fun view -> view.Name))

finish ()
