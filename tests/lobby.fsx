#load "Whole.fsx"

open TCModel.Engine
open TCModel.Table
open TCModel.Net
open TCModel.Turncoats
open Harness
open Whole

let private dealt = Playing.start 2 42UL |> Result.toOption |> Option.get

let private opened () = Lobby.opened playing dealt []

let private sits console token lobby =
    Lobby.join console token None plain lobby

let private full () =
    let lobby, _ = opened () |> sits "one" "tok-one"
    let lobby, _ = lobby |> sits "two" "tok-two"
    lobby

let private heard console posts =
    posts
    |> List.filter (fun post -> post.To = console)
    |> List.map (fun post ->
        match post.Say with
        | Told text
        | Screen text
        | TurnedAway text
        | GotUp text -> text
        | Seated(seat, token) -> $"seated at {seat} holding {token}"
        | Nudged -> "(nudged)")
    |> String.concat "\n"

let private nudged console posts =
    posts |> List.exists (fun post -> post.To = console && post.Say = Nudged)

let private mentions (needle: string) (text: string) = text.Contains needle

let private movesMade lobby =
    Timeline.movesMade (Lobby.model lobby).Timeline


let seatedOne, seatedOnePosts = opened () |> sits "one" "tok-one"

report "the first to arrive is given the first seat" true (heard "one" seatedOnePosts |> mentions "seated at 1")

let seatedTwo, seatedTwoPosts = seatedOne |> sits "two" "tok-two"

report "the next is given the next seat" true (heard "two" seatedTwoPosts |> mentions "seated at 2")

let _, latecomer = seatedTwo |> sits "three" "tok-three"

report
    "a table that is full turns the next one away"
    true
    (heard "three" latecomer |> mentions "Every seat at this table is taken.")


report
    "one player alone is shown the lobby, not a board"
    true
    (heard "one" seatedOnePosts |> mentions "Waiting for the table to fill")

report "and is told how many are still to come" true (heard "one" seatedOnePosts |> mentions "1 more to come")

report
    "once the table fills, everyone is shown a board"
    true
    (heard "one" seatedTwoPosts |> mentions "=== Turn 1 - Player 1 to play ===")


let dropped, _ = full () |> Lobby.left "one"

let _, resumed =
    dropped |> Lobby.join "one-again" "tok-fresh" (Some "tok-one") plain

report "a token brings a console back to the seat it left" true (heard "one-again" resumed |> mentions "seated at 1")

report "and back to the same stones" true (heard "one-again" resumed |> mentions "-> Player 1 (you)  bag: Rx3 Bx2 Gx3 (8)")

let _, stranger =
    full () |> Lobby.join "four" "tok-fresh" (Some "not-a-token") plain

report "a token that claimed no seat claims none now" true (heard "four" stranger |> mentions "That is not a seat at this table.")


let private upFrom lobby = lobby |> Lobby.said "one" "quit"

let stood, stoodPosts = full () |> upFrom

report "a console that gets up is told it has" true (heard "one" stoodPosts |> mentions "You are up from the table")

report "and told the seat is kept" true (heard "one" stoodPosts |> mentions "Your seat is kept")

report
    "and it is said as getting up rather than as anything else"
    true
    (stoodPosts
     |> List.exists (fun post ->
         post.To = "one"
         && match post.Say with
            | GotUp _ -> true
            | _ -> false))

report
    "the table lets go of the console"
    true
    (heard "one" (stood |> Lobby.said "one" "history" |> snd)
     |> mentions "You are not sitting at this table.")

report
    "but keeps the seat, so the token still brings them back"
    true
    (heard "one-again" (stood |> Lobby.join "one-again" "tok-fresh" (Some "tok-one") plain |> snd)
     |> mentions "seated at 1")

report
    "getting up works at a table still filling up"
    true
    (heard "one" (seatedOne |> upFrom |> snd) |> mentions "You are up from the table")


let waited, waitedPosts = full () |> Lobby.said "two" "recruit r 3"

report "a player out of turn is told whose it is" true (heard "two" waitedPosts |> mentions "It is Player 1's turn.")

report "and the game does not move" 0 (movesMade waited)

let acted, _ = full () |> Lobby.said "one" "recruit r 3"

report "the player whose turn it is may move" 1 (movesMade acted)


report "a move nudges the player it has come round to" true (nudged "two" (full () |> Lobby.said "one" "recruit r 3" |> snd))

report "and not the one who made it" false (nudged "one" (full () |> Lobby.said "one" "recruit r 3" |> snd))

report "a move refused for being out of turn nudges nobody" (false, false) (nudged "one" waitedPosts, nudged "two" waitedPosts)

report "and neither does a line that is not a move at all" false (nudged "two" (full () |> Lobby.said "one" "history" |> snd))

report "the last player to sit down nudges whoever the game begins with" true (nudged "one" seatedTwoPosts)

report "and not themselves" false (nudged "two" seatedTwoPosts)

report "a table still filling up nudges nobody" false (nudged "one" seatedOnePosts)

report "and a console coming back to a seat it already held starts nothing, so nudges nobody" false (nudged "one" resumed)

let private played =
    [ "one", "negotiate"; "one", "return r"; "two", "negotiate"; "two", "return r" ]
    |> List.fold (fun (lobby, _) (who, line) -> Lobby.said who line lobby) (full (), [])

report "those four lines end the game" true (Playing.isOver (Lobby.model (fst played)))

report "and a game that is over has come round to nobody" (false, false) (nudged "one" (snd played), nudged "two" (snd played))

report
    "a player may get up from a game that has finished"
    true
    (heard "one" (fst played |> upFrom |> snd)
     |> mentions "You are up from the table")


let private refuses typed sentence =
    let after, posts = full () |> Lobby.said "one" typed
    report $"'{typed}' is refused" true (heard "one" posts |> mentions sentence)
    report $"'{typed}' leaves the game where it was" 0 (movesMade after)

refuses "undo" "Undo is not played over a network"
refuses "redo" "Undo is not played over a network"
refuses "restart" "A networked table plays the one game it was dealt"
refuses "players 3" "How many are playing is settled when the table is opened"


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


let private described lobby = Lobby.described lobby

report
    "a table nobody has sat at is filling up, with every seat going spare"
    (Lobby.Filling, 2, 0, 0, 0)
    (let door = described (opened ())
     door.Stage, door.Places, door.Machines, door.Sat, door.Reading)

report
    "one arrival is counted, and the table is still filling"
    (Lobby.Filling, 1, 1)
    (let door = described seatedOne
     door.Stage, door.Sat, door.Reading)

report
    "a full table is under way"
    (Lobby.Underway, 2, 2)
    (let door = described (full ())
     door.Stage, door.Sat, door.Reading)

report
    "a player who drops still holds their seat, so the table is full and one console short"
    (Lobby.Underway, 2, 1)
    (let door = described dropped
     door.Stage, door.Sat, door.Reading)

report
    "a seat the machine plays is counted as its own thing and never as one going spare"
    (Lobby.Underway, 2, 1, 1)
    (let door = described alone
     door.Stage, door.Places, door.Machines, door.Sat)

report
    "and the roster says who is at each seat in the game's own words for them"
    [ "Player 1 (the machine)"; "Player 2 (here)" ]
    (described alone).Sitters

report
    "a seat somebody walked away from says so, rather than reading as empty"
    [ "Player 1 (away)"; "Player 2 (here)" ]
    (described dropped).Sitters

finish ()
