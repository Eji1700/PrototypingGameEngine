// The rest of the program, for the checks that need a screen, a table or a wire.
//
// `Harness.fsx` stops at the game's rules, because that is all half of these scripts want.
// This carries on: everything from `Showing.fs` to `Offer.fs`, in the project's own compile
// order, ending at the one value that fills in both seams.
//
// One list rather than thirteen. Each script used to keep its own, naming only what it
// needed, and the moment a file moved they all drifted apart quietly - which is the same
// disease the reshape was for. What it costs is a few seconds per script compiling files it
// does not use; what it buys is that the order is written down once, beside the fsproj that
// has to agree with it.

// What the program's own project references, less the framework: `Browser`, `Server` and
// `Client` want ASP.NET and are the three files a script cannot load, which is also why
// nothing below them is here.
#r "nuget: Argu, 6.2.5"
#r "nuget: Falco.Datastar, 1.3.0"
#r "nuget: Falco.Markup, 1.4.0"
#r "nuget: Spectre.Console, 0.51.1"

#load "Harness.fsx"

#load "../src/Table/Parts/Showing.fs"
#load "../src/Table/Parts/Waiting.fs"
#load "../src/Table/Parts/Palette.fs"
#load "../src/Table/Parts/Reach.fs"
#load "../src/Table/Parts/Keys.fs"
#load "../src/Table/Parts/Commands.fs"
#load "../src/Table/Parts/View.fs"
#load "../src/Table/Parts/Seating.fs"
#load "../src/Table/Parts/Tint.fs"
#load "../src/Table/Parts/Page.fs"
#load "../src/Table/Parts/Screens.fs"
#load "../src/Table/Parts/Options.fs"
#load "../src/Table/Playable.fs"
#load "../src/Table/Playing/Solo.fs"
#load "../src/Table/Playing/Transcript.fs"
#load "../src/Table/Playing/Menu.fs"
#load "../src/Table/Playing/Launch.fs"
#load "../src/Net/Protocol.fs"
#load "../src/Net/Lobby.fs"

#load "../src/Games/Turncoats/Rules/Words.fs"
#load "../src/Games/Turncoats/Rules/Rival.fs"
#load "../src/Games/Turncoats/Reading/Ink.fs"
#load "../src/Games/Turncoats/Reading/Parse.fs"
#load "../src/Games/Turncoats/Reading/Render.fs"
#load "../src/Games/Turncoats/Reading/Rich.fs"
#load "../src/Games/Turncoats/Reading/Html.fs"
#load "../src/Games/Turncoats/Offer.fs"

open TCModel.Table

/// The game every script below is checking. Named once here, so a check reads as a check
/// on a table rather than on this game in particular - and so the day there is a second
/// game, what it takes to point one of these at it is this line.
let playing = TCModel.Turncoats.Offer.playable

/// The colours nobody has changed.
let standard = Playable.standard playing

/// The first way it can be drawn at a terminal, which is the plain one.
let plain = Playable.plainest AtATerminal standard playing

/// And the same in a browser.
let asPage = Playable.plainest InABrowser standard playing
