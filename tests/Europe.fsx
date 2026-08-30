#load "Stack.fsx"

#load "../src/Games/Diplomacy/Rules/Powers.fs"
#load "../src/Games/Diplomacy/Rules/Atlas.fs"
#load "../src/Games/Diplomacy/Rules/Position.fs"
#load "../src/Games/Diplomacy/Rules/Orders.fs"
#load "../src/Games/Diplomacy/Rules/Adjudicate.fs"
#load "../src/Games/Diplomacy/Rules/Session.fs"
#load "../src/Games/Diplomacy/Rules/Turn.fs"
#load "../src/Games/Diplomacy/Rules/Words.fs"
#load "../src/Games/Diplomacy/Rules/Rival.fs"
#load "../src/Games/Diplomacy/Reading/Ink.fs"
#load "../src/Games/Diplomacy/Reading/Parse.fs"
#load "../src/Games/Diplomacy/Reading/Render.fs"
#load "../src/Games/Diplomacy/Offer.fs"

open Prototyping.Table

let diplomacy = Prototyping.Diplomacy.Offer.playable

let standard = Playable.standard diplomacy

let plain = Playable.plainest AtATerminal standard diplomacy

let asPage = Playable.plainest InABrowser standard diplomacy
