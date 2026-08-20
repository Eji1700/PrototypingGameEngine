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

#load "../src/Games/Compile/Rules/Protocols.fs"
#load "../src/Games/Compile/Rules/Cards.fs"
#load "../src/Games/Compile/Rules/Effects.fs"
#load "../src/Games/Compile/Rules/Printed.fs"
#load "../src/Games/Compile/Rules/Field.fs"
#load "../src/Games/Compile/Rules/Drafting.fs"
#load "../src/Games/Compile/Rules/Session.fs"
#load "../src/Games/Compile/Rules/Events.fs"
#load "../src/Games/Compile/Rules/Resolving.fs"
#load "../src/Games/Compile/Rules/Turn.fs"
#load "../src/Games/Compile/Rules/Words.fs"
#load "../src/Games/Compile/Rules/Rival.fs"
#load "../src/Games/Compile/Reading/Ink.fs"
#load "../src/Games/Compile/Reading/Parse.fs"
#load "../src/Games/Compile/Reading/Render.fs"
#load "../src/Games/Compile/Offer.fs"

open TCModel.Table

let compiled = TCModel.Compile.Offer.playable

let controlled = TCModel.Compile.Offer.withControl

let standard = Playable.standard compiled

let plain = Playable.plainest AtATerminal standard compiled

let asPage = Playable.plainest InABrowser standard compiled
