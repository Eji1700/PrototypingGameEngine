// The rules a networked table adds to the game: who may sit, who may act, who may see,
// and what a player may ask for. None of them touch a wire - the lobby is a value, and
// what it hands back is a list of things to say.
//
//   dotnet fsi tests/lobby.fsx

#r "nuget: Spectre.Console, 0.51.1"

#load "Harness.fsx"
#load "../src/App/Messages.fs"
#load "../src/App/Session.fs"
#load "../src/App/Timeline.fs"
#load "../src/App/Journal.fs"
#load "../src/App/Model.fs"
#load "../src/App/Update.fs"
#load "../src/Console/Waiting.fs"
#load "../src/Console/Words.fs"
#load "../src/Console/Render.fs"
#load "../src/Console/Parse.fs"
#load "../src/Console/Palette.fs"
#load "../src/Console/Tint.fs"
#load "../src/Console/Rich.fs"
#load "../src/Console/View.fs"
#load "../src/Net/Protocol.fs"
#load "../src/Net/Lobby.fs"

open TCModel.App
open TCModel.Console
open TCModel.Net
open Harness

let private dealt = Update.start 2 42UL |> Result.toOption |> Option.get

let private opened () = Lobby.opened dealt

/// One console at the table, in the next empty seat.
let private sits console token lobby = Lobby.join console token None (View.plain Palette.standard) lobby

/// Both seats taken, which is the only state a game is played in.
let private full () =
    let lobby, _ = opened () |> sits "one" "tok-one"
    let lobby, _ = lobby |> sits "two" "tok-two"
    lobby

/// Everything one console was told, run together, so a check can look for the sentence
/// it cares about without minding what else arrived alongside it.
let private heard console posts =
    posts
    |> List.filter (fun post -> post.To = console)
    |> List.map (fun post ->
        match post.Say with
        | Told text
        | Screen text
        | TurnedAway text -> text
        | Seated(seat, token) -> $"seated at {seat} holding {token}")
    |> String.concat "\n"

let private mentions (needle: string) (text: string) = text.Contains needle

let private movesMade lobby = Timeline.movesMade (Lobby.model lobby).Timeline

// --- taking a seat ------------------------------------------------------------------

let seatedOne, seatedOnePosts = opened () |> sits "one" "tok-one"

report "the first to arrive is given the first seat" true (heard "one" seatedOnePosts |> mentions "seated at 1")

let seatedTwo, seatedTwoPosts = seatedOne |> sits "two" "tok-two"

report "the next is given the next seat" true (heard "two" seatedTwoPosts |> mentions "seated at 2")

let _, latecomer = seatedTwo |> sits "three" "tok-three"

report
    "a table that is full turns the next one away"
    true
    (heard "three" latecomer |> mentions "Every seat at this table is taken.")

// --- nobody plays until everyone is here ----------------------------------------------

report
    "one player alone is shown the lobby, not a board"
    true
    (heard "one" seatedOnePosts |> mentions "Waiting for the table to fill")

report "and is told how many are still to come" true (heard "one" seatedOnePosts |> mentions "1 more to come")

report
    "once the table fills, everyone is shown a board"
    true
    (heard "one" seatedTwoPosts |> mentions "=== Turn 1 - Player 1 to play ===")

// --- coming back --------------------------------------------------------------------

let dropped, _ = full () |> Lobby.left "one"
let _, resumed = dropped |> Lobby.join "one-again" "tok-fresh" (Some "tok-one") (View.plain Palette.standard)

report "a token brings a console back to the seat it left" true (heard "one-again" resumed |> mentions "seated at 1")

report
    "and back to the same stones"
    true
    (heard "one-again" resumed |> mentions "-> Player 1 (you)  bag: Rx3 Bx2 Gx3 (8)")

let _, stranger = full () |> Lobby.join "four" "tok-fresh" (Some "not-a-token") (View.plain Palette.standard)

report
    "a token that claimed no seat claims none now"
    true
    (heard "four" stranger |> mentions "That is not a seat at this table.")

// --- who may act ----------------------------------------------------------------------

let waited, waitedPosts = full () |> Lobby.said "two" "recruit r 3"

report "a player out of turn is told whose it is" true (heard "two" waitedPosts |> mentions "It is Player 1's turn.")

report "and the game does not move" 0 (movesMade waited)

let acted, _ = full () |> Lobby.said "one" "recruit r 3"

report "the player whose turn it is may move" 1 (movesMade acted)

// --- what a player may not ask for -----------------------------------------------------

let private refuses typed sentence =
    let after, posts = full () |> Lobby.said "one" typed
    report $"'{typed}' is refused" true (heard "one" posts |> mentions sentence)
    report $"'{typed}' leaves the game where it was" 0 (movesMade after)

refuses "undo" "Undo is not played over a network"
refuses "redo" "Undo is not played over a network"
refuses "restart" "A networked table plays the one game it was dealt"
refuses "players 3" "How many are playing is settled when the table is opened"

// --- who may see what -------------------------------------------------------------------

let _, drawn = full () |> Lobby.said "one" "negotiate"

report
    "the player who drew is told the colour"
    true
    (heard "one" drawn |> mentions "Player 1 draws a Green stone from the reserve")

report
    "and nobody else is"
    true
    (heard "two" drawn |> mentions "Player 1 draws a stone from the reserve"
     && not (heard "two" drawn |> mentions "Green stone from the reserve"))

report
    "each console is drawn its own bag"
    true
    (heard "one" seatedTwoPosts |> mentions "-> Player 1 (you)  bag: Rx3 Bx2 Gx3 (8)"
     && heard "two" seatedTwoPosts |> mentions "Player 2 (you)  bag: Rx3 Bx3 Gx2 (8)")

report
    "and everyone else's closed"
    true
    (heard "one" seatedTwoPosts |> mentions "Player 2        bag: closed (8)"
     && heard "two" seatedTwoPosts |> mentions "Player 1        bag: closed (8)")

// --- how each console reads it ------------------------------------------------------------
//
// A view lays a whole screen out and so needs the game to do it, and the game is here at
// the table. So the board is drawn per seat, and two people at one table can be sent two
// boards that look nothing alike from the one position.

let sittingPlain, _ = opened () |> sits "one" "tok-one"
let mixed, mixedPosts = sittingPlain |> Lobby.join "two" "tok-two" None (View.rich Palette.standard)

/// Panels are drawn with box characters; nothing in the plain board has one.
let private panelled = mentions "╭"

report "a console that asked to read richly is sent a board with panels" true (heard "two" mixedPosts |> panelled)

report "and the one beside it, reading plainly, is not" false (heard "one" mixedPosts |> panelled)

let looked, lookedPosts = mixed |> Lobby.said "one" "view rich"

report "a player may change how they read once they are sitting down" true (heard "one" lookedPosts |> panelled)

report "and that is not news to anybody else, so nobody else is drawn again" 1 (List.length lookedPosts)

let refusedView, refusedViewPosts = mixed |> Lobby.said "one" "view fancy"

report
    "a view nobody has is refused, and the seat keeps the one it had"
    true
    (heard "one" refusedViewPosts |> mentions "is not a way of showing the game")

report "and the game does not move for it" 0 (movesMade refusedView)

finish ()
