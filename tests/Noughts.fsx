// The whole program with noughts and crosses in it, for that game's own checks.
//
// `Whole.fsx` is the same list with the other game at the end of it. Two files rather than
// one because `dotnet fsi` names a loaded file by its basename, and both games have a
// `Board.fs` - which is a fact about scripts and not about the program, where the two sit
// side by side in one project and always have.
//
// Which is worth reading as the point rather than the inconvenience: everything above the
// last nine lines is the same list, in the same order, and none of it knows which game is
// coming.

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

#load "../src/Games/TicTacToe/Rules/Marks.fs"
#load "../src/Games/TicTacToe/Rules/Board.fs"
#load "../src/Games/TicTacToe/Rules/Session.fs"
#load "../src/Games/TicTacToe/Rules/Turn.fs"
#load "../src/Games/TicTacToe/Rules/Words.fs"
#load "../src/Games/TicTacToe/Rules/Rival.fs"
#load "../src/Games/TicTacToe/Reading/Ink.fs"
#load "../src/Games/TicTacToe/Reading/Parse.fs"
#load "../src/Games/TicTacToe/Reading/Render.fs"
#load "../src/Games/TicTacToe/Offer.fs"

open TCModel.Table

/// The game these checks are about.
let noughts = TCModel.TicTacToe.Offer.playable

let standard = Playable.standard noughts

/// The first way it can be drawn at a terminal.
let plain = Playable.plainest AtATerminal standard noughts

/// And in a browser.
let asPage = Playable.plainest InABrowser standard noughts
