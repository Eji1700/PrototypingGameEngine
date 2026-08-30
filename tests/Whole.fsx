#load "Harness.fsx"
#load "Stack.fsx"

#load "../src/Games/Turncoats/Rules/Words.fs"
#load "../src/Games/Turncoats/Rules/Rival.fs"
#load "../src/Games/Turncoats/Reading/Ink.fs"
#load "../src/Games/Turncoats/Reading/Parse.fs"
#load "../src/Games/Turncoats/Reading/Render.fs"
#load "../src/Games/Turncoats/Reading/Rich.fs"
#load "../src/Games/Turncoats/Reading/Html.fs"
#load "../src/Games/Turncoats/Offer.fs"

open Prototyping.Table

let playing = Prototyping.Turncoats.Offer.playable

let standard = Playable.standard playing

let plain = Playable.plainest AtATerminal standard playing

let asPage = Playable.plainest InABrowser standard playing
