#load "Stack.fsx"

#load "../src/Games/TicTacToe/Rules/Marks.fs"
#load "../src/Games/TicTacToe/Rules/Board.fs"
#load "../src/Games/TicTacToe/Rules/Session.fs"
#load "../src/Games/TicTacToe/Rules/Turn.fs"
#load "../src/Games/TicTacToe/Rules/Words.fs"
#load "../src/Games/TicTacToe/Rules/Rival.fs"
#load "../src/Games/TicTacToe/Reading/Ink.fs"
#load "../src/Games/TicTacToe/Reading/Parse.fs"
#load "../src/Games/TicTacToe/Reading/Render.fs"
#load "../src/Games/TicTacToe/Offer.fs"

open Prototyping.Table

let noughts = Prototyping.TicTacToe.Offer.playable

let standard = Playable.standard noughts

let plain = Playable.plainest AtATerminal standard noughts

let asPage = Playable.plainest InABrowser standard noughts
