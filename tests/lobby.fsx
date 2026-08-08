// The rules a networked table adds to the game: who may sit, who may act, who may see,
// and what a player may ask for. None of them touch a wire - the lobby is a value, and
// what it hands back is a list of things to say.
//
//   dotnet fsi tests/lobby.fsx

#load "Whole.fsx"

open TCModel.Engine
open TCModel.Table
open TCModel.Net
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.Domain
open Harness
open Whole

let private dealt = Playing.start 2 42UL |> Result.toOption |> Option.get

/// A table of nothing but people, which is what this file is mostly about.
let private opened () = Lobby.opened playing dealt []

/// One console at the table, in the next empty seat.
let private sits console token lobby =
    Lobby.join console token None plain lobby

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
        | Seated(seat, token) -> $"seated at {seat} holding {token}"
        | Nudged -> "(nudged)")
    |> String.concat "\n"

/// Whether one console was told the turn had come round to it. Kept apart from `heard`
/// because a nudge carries no words at all - it is the one thing a table says that is not
/// something to read.
let private nudged console posts =
    posts |> List.exists (fun post -> post.To = console && post.Say = Nudged)

let private mentions (needle: string) (text: string) = text.Contains needle

let private movesMade lobby =
    Timeline.movesMade (Lobby.model lobby).Timeline

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

let _, resumed =
    dropped |> Lobby.join "one-again" "tok-fresh" (Some "tok-one") plain

report "a token brings a console back to the seat it left" true (heard "one-again" resumed |> mentions "seated at 1")

report "and back to the same stones" true (heard "one-again" resumed |> mentions "-> Player 1 (you)  bag: Rx3 Bx2 Gx3 (8)")

let _, stranger =
    full () |> Lobby.join "four" "tok-fresh" (Some "not-a-token") plain

report "a token that claimed no seat claims none now" true (heard "four" stranger |> mentions "That is not a seat at this table.")

// --- who may act ----------------------------------------------------------------------

let waited, waitedPosts = full () |> Lobby.said "two" "recruit r 3"

report "a player out of turn is told whose it is" true (heard "two" waitedPosts |> mentions "It is Player 1's turn.")

report "and the game does not move" 0 (movesMade waited)

let acted, _ = full () |> Lobby.said "one" "recruit r 3"

report "the player whose turn it is may move" 1 (movesMade acted)

// --- being told the turn has come round --------------------------------------------------
//
// The reason this table has it and one keyboard has no use for it: everybody here is at
// their own machine, waiting on somebody they cannot see, and the sensible thing to do
// while waiting is something else. So the turn arriving has to be able to reach a player
// who is not looking at it.
//
// Which makes the interesting checks the ones about when it stays quiet. A nudge that went
// out on every change to everybody would be a bell ringing all game, and a player would
// learn to ignore it inside two turns - so the whole of its worth is in being rare and
// meaning exactly one thing.

report "a move nudges the player it has come round to" true (nudged "two" (full () |> Lobby.said "one" "recruit r 3" |> snd))

report "and not the one who made it" false (nudged "one" (full () |> Lobby.said "one" "recruit r 3" |> snd))

report "a move refused for being out of turn nudges nobody" (false, false) (nudged "one" waitedPosts, nudged "two" waitedPosts)

report "and neither does a line that is not a move at all" false (nudged "two" (full () |> Lobby.said "one" "history" |> snd))

// Taking the last seat starts the game, and the player it starts with has been sitting
// there with nothing to watch - which is the same case a move makes and wants the same
// answer.
report "the last player to sit down nudges whoever the game begins with" true (nudged "one" seatedTwoPosts)

report "and not themselves" false (nudged "two" seatedTwoPosts)

report "a table still filling up nudges nobody" false (nudged "one" seatedOnePosts)

report "and a console coming back to a seat it already held starts nothing, so nudges nobody" false (nudged "one" resumed)

/// A two-player game played out the only way it can be in four lines: each player draws
/// and hands one back, and the game ends once everybody has negotiated in a row.
let private played =
    [ "one", "negotiate"; "one", "return r"; "two", "negotiate"; "two", "return r" ]
    |> List.fold (fun (lobby, _) (who, line) -> Lobby.said who line lobby) (full (), [])

report "those four lines end the game" true (Playing.isOver (Lobby.model (fst played)))

report "and a game that is over has come round to nobody" (false, false) (nudged "one" (snd played), nudged "two" (snd played))

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

let mixed, mixedPosts =
    sittingPlain
    |> Lobby.join
        "two"
        "tok-two"
        None
        (Playable.byName AtATerminal standard playing "rich"
         |> Result.toOption
         |> Option.get)

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

// --- a seat the machine plays ---------------------------------------------------------------
//
// A table opened over a network may have the program at some of its seats: somebody here,
// a friend two rooms away, and a machine between them. That seat was never empty, so nobody
// waits for it and nobody can sit down in it - and the machine plays it through the very loop
// the one-keyboard table uses, so a move it makes is a move like anybody else's.

/// Seat 1 is the machine's, which leaves one seat for one person - so a single arrival fills
/// a table of two, and the machine has already played by the time they are shown a board.
let private machineFirst () =
    Lobby.opened playing dealt (playing.Seating 42UL [ Some "easy"; None ] (Model.state dealt))

let alone, alonePosts = machineFirst () |> sits "one" "tok-one"

report
    "a machine's seat is not waited for, so one person fills a table of two"
    true
    (heard "one" alonePosts |> mentions "=== Turn")

report "and the seat handed out is the one the machine is not in" true (heard "one" alonePosts |> mentions "seated at 2")

report "the machine plays as the table fills, rather than after somebody has read a board" true (movesMade alone > 0)

report "so the board that arrives is already waiting on the person" true (heard "one" alonePosts |> mentions "Player 2 to play")

report
    "and whoever sits down is told which seat the machine has, the same as at one keyboard"
    true
    (heard "one" alonePosts |> mentions "Played by the machine: Player 1 (easy).")

let _, latecomerTurned = alone |> sits "two" "tok-two"

report
    "a seat the machine holds cannot be sat down in"
    true
    (heard "two" latecomerTurned |> mentions "Every seat at this table is taken.")

let answered, _ = alone |> Lobby.said "one" "recruit r 3"

report
    "and a move by the person is answered before the board comes back, as it is at one keyboard"
    true
    (movesMade answered >= movesMade alone + 2)

finish ()
