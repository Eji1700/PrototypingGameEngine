#load "Stack.fsx"

#load "../src/Games/Cascade/Rules/Board.fs"
#load "../src/Games/Cascade/Rules/Session.fs"
#load "../src/Games/Cascade/Rules/Turn.fs"
#load "../src/Games/Cascade/Rules/Words.fs"
#load "../src/Games/Cascade/Reading/Ink.fs"
#load "../src/Games/Cascade/Reading/Parse.fs"
#load "../src/Games/Cascade/Reading/Render.fs"
#load "../src/Games/Cascade/Offer.fs"

open Prototyping.Table

let cascade = Prototyping.Cascade.Offer.playable

let standard = Playable.standard cascade

let plain = Playable.plainest AtATerminal standard cascade

let asPage = Playable.plainest InABrowser standard cascade
