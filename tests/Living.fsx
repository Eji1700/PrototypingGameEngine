// The whole program with Life in it, for that game's own checks.
//
// The same list as `Noughts.fsx` and `Whole.fsx` with a different game on the end, and a file
// of its own for the reason those two are two files: `dotnet fsi` names a loaded file by its
// basename, and this game has a `Turn.fs` and a `Words.fs` like the rest of them - which is a
// fact about scripts and not about the program, where all five sit side by side in one
// solution and always have.
//
// Everything above the last nine lines is the same list, in the same order, and none of it
// knows which game is coming - which is the whole claim this game was written to test.

#r "nuget: Argu, 6.2.5"
#r "nuget: Falco.Datastar, 1.3.0"
#r "nuget: Falco.Markup, 1.4.0"
#r "nuget: Spectre.Console, 0.51.1"

#load "Checks.fsx"

#load "../src/Common/Result.fs"
#load "../src/Common/Cascade.fs"
#load "../src/Common/Random.fs"
#load "../src/Engine/Seats.fs"
#load "../src/Engine/Messages.fs"
#load "../src/Engine/Told.fs"
#load "../src/Engine/Rules.fs"
#load "../src/Engine/Timeline.fs"
#load "../src/Engine/Journal.fs"
#load "../src/Engine/Model.fs"
#load "../src/Engine/Update.fs"
#load "../src/Engine/Machines.fs"
#load "../src/Table/Parts/Invoked.fs"
#load "../src/Table/Parts/Showing.fs"
#load "../src/Table/Parts/Waiting.fs"
#load "../src/Table/Parts/Scene.fs"
#load "../src/Table/Parts/Palette.fs"
#load "../src/Table/Parts/Settings.fs"
#load "../src/Table/Parts/Reach.fs"
#load "../src/Table/Parts/Keys.fs"
#load "../src/Table/Parts/Commands.fs"
#load "../src/Table/Parts/View.fs"
#load "../src/Table/Parts/Seating.fs"
#load "../src/Table/Parts/Tint.fs"
#load "../src/Table/Parts/Page.fs"
#load "../src/Table/Parts/Screens.fs"
#load "../src/Table/Parts/Options.fs"
#load "../src/Table/Parts/Readers.fs"
#load "../src/Table/Playable.fs"
#load "../src/Table/Playing/Solo.fs"
#load "../src/Table/Playing/Transcript.fs"
#load "../src/Table/Playing/Menu.fs"
#load "../src/Table/Playing/Launch.fs"
#load "../src/Net/Protocol.fs"
#load "../src/Net/Lobby.fs"
#load "../src/Net/Tables.fs"
#load "../src/Net/House.fs"

#load "../src/Games/Life/Rules/Grid.fs"
#load "../src/Games/Life/Rules/World.fs"
#load "../src/Games/Life/Rules/Turn.fs"
#load "../src/Games/Life/Rules/Words.fs"
#load "../src/Games/Life/Reading/Ink.fs"
#load "../src/Games/Life/Reading/Parse.fs"
#load "../src/Games/Life/Reading/Render.fs"
#load "../src/Games/Life/Offer.fs"

open TCModel.Table

/// The game these checks are about.
let life = TCModel.Life.Offer.playable

let standard = Playable.standard life

/// The first way it can be drawn at a terminal.
let plain = Playable.plainest AtATerminal standard life

/// And in a browser.
let asPage = Playable.plainest InABrowser standard life
