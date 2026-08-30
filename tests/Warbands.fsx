#load "Stack.fsx"

#load "../src/Games/Warband/Rules/Formation.fs"
#load "../src/Games/Warband/Rules/Kinds.fs"
#load "../src/Games/Warband/Rules/Squads.fs"
#load "../src/Games/Warband/Rules/Session.fs"
#load "../src/Games/Warband/Rules/Events.fs"
#load "../src/Games/Warband/Rules/Battle.fs"
#load "../src/Games/Warband/Rules/Turn.fs"
#load "../src/Games/Warband/Rules/Words.fs"
#load "../src/Games/Warband/Rules/Rival.fs"
#load "../src/Games/Warband/Reading/Ink.fs"
#load "../src/Games/Warband/Reading/Parse.fs"
#load "../src/Games/Warband/Reading/Render.fs"
#load "../src/Games/Warband/Offer.fs"

open Prototyping.Table

let warband = Prototyping.Warband.Offer.playable

let standard = Playable.standard warband

let plain = Playable.plainest AtATerminal standard warband

let asPage = Playable.plainest InABrowser standard warband
