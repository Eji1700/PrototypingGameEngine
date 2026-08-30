#load "Stack.fsx"

#load "../src/Games/Snake/Rules/Board.fs"
#load "../src/Games/Snake/Rules/Snakes.fs"
#load "../src/Games/Snake/Rules/Session.fs"
#load "../src/Games/Snake/Rules/Turn.fs"
#load "../src/Games/Snake/Rules/Words.fs"
#load "../src/Games/Snake/Rules/Rival.fs"
#load "../src/Games/Snake/Reading/Ink.fs"
#load "../src/Games/Snake/Reading/Parse.fs"
#load "../src/Games/Snake/Reading/Render.fs"
#load "../src/Games/Snake/Offer.fs"

open Prototyping.Table

let snake = Prototyping.Snake.Offer.playable

let turns = Prototyping.Snake.Offer.ways |> List.item 1

let standard = Playable.standard snake

let plain = Playable.plainest AtATerminal standard snake

let asPage = Playable.plainest InABrowser standard snake
