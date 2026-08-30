#load "Stack.fsx"

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

open Prototyping.Table

let compiled = Prototyping.Compile.Offer.playable

let controlled = Prototyping.Compile.Offer.withControl

let standard = Playable.standard compiled

let plain = Playable.plainest AtATerminal standard compiled

let asPage = Playable.plainest InABrowser standard compiled
