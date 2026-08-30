#load "Stack.fsx"

#load "../src/Games/Life/Rules/Torus.fs"
#load "../src/Games/Life/Rules/World.fs"
#load "../src/Games/Life/Rules/Turn.fs"
#load "../src/Games/Life/Rules/Words.fs"
#load "../src/Games/Life/Reading/Ink.fs"
#load "../src/Games/Life/Reading/Parse.fs"
#load "../src/Games/Life/Reading/Render.fs"
#load "../src/Games/Life/Offer.fs"

open Prototyping.Table

let life = Prototyping.Life.Offer.playable

let standard = Playable.standard life

let plain = Playable.plainest AtATerminal standard life

let asPage = Playable.plainest InABrowser standard life
