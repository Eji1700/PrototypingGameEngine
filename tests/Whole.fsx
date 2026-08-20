#r "nuget: Argu, 6.2.5"
#r "nuget: Falco.Datastar, 1.3.0"
#r "nuget: Falco.Markup, 1.4.0"
#r "nuget: Spectre.Console, 0.51.1"

#load "Harness.fsx"

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

#load "../src/Games/Turncoats/Rules/Words.fs"
#load "../src/Games/Turncoats/Rules/Rival.fs"
#load "../src/Games/Turncoats/Reading/Ink.fs"
#load "../src/Games/Turncoats/Reading/Parse.fs"
#load "../src/Games/Turncoats/Reading/Render.fs"
#load "../src/Games/Turncoats/Reading/Rich.fs"
#load "../src/Games/Turncoats/Reading/Html.fs"
#load "../src/Games/Turncoats/Offer.fs"

open TCModel.Table

let playing = TCModel.Turncoats.Offer.playable

let standard = Playable.standard playing

let plain = Playable.plainest AtATerminal standard playing

let asPage = Playable.plainest InABrowser standard playing
