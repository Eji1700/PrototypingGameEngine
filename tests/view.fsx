// The ways of showing the game, and the one rule they all keep: colour may be laid over
// a board, but nothing may move a character of it. The map is drawn by counting into
// columns, so a view that shifted anything sideways would take the map apart.
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
#load "../src/Console/Words.fs"
#load "../src/Console/Render.fs"
#load "../src/Console/Tint.fs"
#load "../src/Console/View.fs"

open System.Text.RegularExpressions
open TCModel.Domain
open TCModel.App
open TCModel.Console
open Harness

/// A whole board, notes and all: the map with its columns, the region numbers in their
/// square brackets, every tally, and the writing that explains them.
let private board =
    let model = Update.start 2 42UL |> Result.toOption |> Option.get
    Render.model true (Game.active (Model.game model)) model

/// Colour taken back off, leaving what a person would actually see.
let private seen text =
    Regex.Replace(text, "\\[[0-9;]*m", "")

// --- the rule every view keeps ---------------------------------------------------------

// Written as a sweep over `View.all` rather than one view at a time, so a view added
// later is held to the same rule without anybody remembering to come back here.
for view in View.all do
    report $"the {view.Name} view moves no character of the board" board (seen (view.Show board))

report "and every view answers to its own name" (View.all |> List.map (fun view -> view.Name)) [ "plain"; "rich" ]

// --- plain --------------------------------------------------------------------------

report "the plain view is the board itself, untouched" board (View.plain.Show board)

// --- rich ------------------------------------------------------------------------------

let private painted = View.rich.Show board

report "the rich view does colour something" true (painted.Contains "[")

report
    "a region's number keeps its square brackets"
    true
    (seen painted |> fun text -> text.Contains "[ 5] Emberfall")

/// The colour a run of text was painted in, if it was painted at all.
let private paintOf (needle: string) =
    let found = Regex.Match(painted, @"\[([0-9;]+)m" + Regex.Escape needle + @"\[0m")
    if found.Success then Some found.Groups[1].Value else None

report "a red stone is painted" (paintOf "R" |> Option.isSome) true

report
    "Blue and Black are told apart, though both begin with a B"
    true
    (paintOf "Blue" <> paintOf "Black" && paintOf "Blue" |> Option.isSome && paintOf "Black" |> Option.isSome)

report "a stone's glyph and its name are painted alike" (paintOf "R") (paintOf "Red")

// --- choosing one ------------------------------------------------------------------------

report "a view can be asked for by name" (Ok "rich") (View.byName "rich" |> Result.map (fun view -> view.Name))

report "and is not case-fussy about it" (Ok "plain") (View.byName "PLAIN" |> Result.map (fun view -> view.Name))

report
    "a name nobody answers to is refused, and says what there is"
    (Error "'fancy' is not a way of showing the game. There is plain, rich.")
    (View.byName "fancy" |> Result.map (fun view -> view.Name))

finish ()
