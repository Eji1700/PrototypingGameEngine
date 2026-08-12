// The game at one keyboard: what a typed line does, and what it asks to be written down.
//
// These are the rules the local game has always had - the prompt has been keeping them
// since the beginning - but until `Solo` they lived inside a loop wrapped around
// `Console.ReadLine`, and a rule you cannot reach without a keyboard is a rule nobody
// checks. [lobby.fsx](lobby.fsx) does the same job for the networked table; this is the
// other half, and between them every rule that is about a *table* rather than about the
// game is now held to something.
//
// Two things are worth the trouble here. The first is that walking the game back and forth
// is allowed at one keyboard and refused at a networked table, which is the sharpest
// difference between the two and easy to lose. The second is the record: a value cannot
// write a file, so `Solo` says what it wants written and something else does it, and the
// case that matters is a restart - the record it asks for is the game that has just been
// swept off the table, not the one that replaced it.
//
//   dotnet fsi tests/solo.fsx

#load "Whole.fsx"

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.Turncoats
open Harness
open Whole

let private dealt = Playing.start 2 42UL |> Result.toOption |> Option.get

let private reading = { Notes = true; View = plain }

/// A game with one person at it, which is the ordinary case.
let private sitting =
    Solo.opened playing "first" dealt |> Solo.watching "keyboard" reading |> fst

/// Type a line and take what came of it. The stamp handed in is only ever used by a line
/// that deals a fresh game, so it is named for that here.
let private say line solo = Solo.said "afresh" "keyboard" line solo

let private turn solo = Solo.said "afresh" "keyboard" "" solo

let private next line solo =
    let solo, _, _ = say line solo
    solo

let private screens (posts: Post list) =
    posts
    |> List.choose (fun post ->
        match post.Say with
        | Screen text -> Some text
        | _ -> None)

let private saidTo (posts: Post list) =
    posts
    |> List.choose (fun post ->
        match post.Say with
        | Told text
        | TurnedAway text -> Some text
        | _ -> None)

// --- sitting down -------------------------------------------------------------------------

report
    "sitting down draws you a board"
    1
    (Solo.watching "one" reading (Solo.opened playing "s" dealt)
     |> snd
     |> screens
     |> List.length)

report
    "a line typed by somebody who is not watching is turned away"
    [ "You are not watching this game." ]
    (Solo.said "afresh" "stranger" "negotiate" (Solo.opened playing "s" dealt)
     |> fun (_, posts, _) -> saidTo posts)

// Two people can watch one hot seat - the same game open in two browsers - and a move
// made in either is drawn to both, because there is only the one game to look at.

let private watched = sitting |> Solo.watching "other" reading |> fst

report
    "a move is drawn to everybody watching the one hot seat"
    2
    (say "recruit r 1" watched |> fun (_, posts, _) -> screens posts |> List.length)

report
    "but changing how you read is drawn only to you"
    1
    (say "notes off" watched |> fun (_, posts, _) -> screens posts |> List.length)

// --- being told the turn has come round ------------------------------------------------
//
// Which at this table can only ever mean somebody else's browser, because the screen here
// belongs to whoever is to play: a watcher who did not type the line has had the turn
// handed to them without asking. At the ordinary table - one person, one keyboard - there
// is nobody to tell, and the check that matters is that nothing rings.

let private nudged console posts =
    posts |> List.exists (fun post -> post.To = console && post.Say = Nudged)

report
    "a move nudges the other console watching the hot seat"
    true
    (nudged "other" (say "recruit r 1" watched |> fun (_, posts, _) -> posts))

report "and not the one that made it" false (nudged "keyboard" (say "recruit r 1" watched |> fun (_, posts, _) -> posts))

report
    "at one keyboard nothing is nudged at all, there being nobody to interrupt but yourself"
    []
    (say "recruit r 1" sitting
     |> fun (_, posts, _) -> posts |> List.filter (fun post -> post.Say = Nudged))

report
    "a line that only changes how you read nudges nobody"
    false
    (nudged "other" (say "notes off" watched |> fun (_, posts, _) -> posts))

// --- the screen changes hands with the turn ---------------------------------------------------
//
// One keyboard, so the board belongs to whoever is to play. Over a network it belongs to
// whoever is reading it, and that is the whole of the difference between `Solo` and `Lobby`.

let private boardSays (solo: Solo<_, _, _>) (needle: string) =
    match Solo.board "keyboard" solo with
    | Some board -> board.Contains needle
    | None -> false

report "the board is drawn for whoever is to play" true (boardSays sitting "Player 1 (you)")

report "and turns over with the turn" true (boardSays (next "recruit r 1" sitting) "Player 2 (you)")

// --- walking the game back and forth -------------------------------------------------------
//
// Allowed here and refused at a networked table. There is nobody else at this one, so there
// is no bag being read over anybody's shoulder and nobody whose move is being taken back
// for them.

let private moved = sitting |> next "recruit r 1"

report "undo takes the last move back" (Playing.session dealt) (Playing.session (Solo.model (next "undo" moved)))

report
    "and redo makes it again"
    (Playing.session (Solo.model moved))
    (Playing.session (Solo.model (moved |> next "undo" |> next "redo")))

report "a restart deals a fresh game to the same players" true (Journal.isEmpty (Solo.model (next "restart" moved)).Journal)

// --- what the world is asked to do -------------------------------------------------------------
//
// The only thing a local game ever needs of the world is a file. `Solo` says whether, what,
// and under which name; something else does it. That split is what makes the case below
// checkable at all.

let private errandOf line solo =
    let _, _, doing = say line solo
    doing

report
    "an ordinary move asks for nothing"
    true
    (match errandOf "recruit r 1" sitting with
     | Carrying -> true
     | _ -> false)

report
    "'save' asks for the record, and to be told where it went"
    true
    (match errandOf "save" moved with
     | Keeping(_, "first", true) -> true
     | _ -> false)

// And leaves the game standing. A record is something to take up again, so a game still in
// play is written down as it stands rather than conceded on the way out - otherwise putting
// one down for the evening would be the same act as losing it. Conceding is `resign`.
report
    "'quit' asks for it too, and leaves the game where it stands"
    true
    (match errandOf "quit" moved with
     | Leaving(model, "first") -> not (Playing.isOver model)
     | _ -> false)

report
    "so a game put down can be taken up again"
    (Playing.session (Solo.model moved))
    (match errandOf "quit" moved with
     | Leaving(model, _) -> Playing.session model
     | _ -> failwith "quitting wrote nothing down")

// The one that would be easy to get wrong, and was worth pulling out of the loop to be able
// to ask: a restart writes the game it has just cleared away, under the name that game was
// being kept under - not the fresh one, and not under the fresh one's name.

report
    "a restart writes the game it swept away, under that game's own name"
    true
    (match errandOf "restart" moved with
     | Keeping(model, "first", false) -> Journal.entries model.Journal |> List.length = 1
     | _ -> false)

report "and the game that replaces it is kept under the new name" "afresh" (Solo.stamp (next "restart" moved))

// A game that ends on its own writes itself down without being asked, and without saying so.

let private resigning = sitting |> next "resign"

report
    "a game that has just ended writes itself down unasked"
    true
    (match errandOf "resign" sitting with
     | Keeping(model, "first", false) -> Playing.isOver model
     | _ -> false)

report
    "but a line typed after it is already over asks for nothing more"
    true
    (match errandOf "resign" resigning with
     | Carrying -> true
     | _ -> false)

// --- reading it your own way ---------------------------------------------------------------------

report
    "a view can be changed from the prompt"
    "rich"
    (let solo = next "view rich" sitting

     match Solo.board "keyboard" solo with
     | Some board when board.Contains "[" -> "rich"
     | _ -> "plain")

report
    "and a view this reader could not show is refused"
    true
    (say "view html" sitting
     |> fun (_, posts, _) ->
         saidTo posts
         |> List.exists (fun said -> said.Contains "is not a way of showing the game here"))

report "an empty line simply draws the board again" 1 (turn sitting |> fun (_, posts, _) -> screens posts |> List.length)

report
    "and a line the parser cannot read is answered, leaving the game alone"
    (Playing.session dealt)
    (let solo, posts, _ = say "frobnicate" sitting
     ignore (saidTo posts)
     Playing.session (Solo.model solo))

// --- said after the fact ---------------------------------------------------------------------------
//
// Where a record went is not something a value can know, so it is said by whoever wrote the
// file - but it still has to reach the player in the words their own view speaks. Said as
// bare text it would read fine at a terminal and go missing entirely in a browser, which is
// the sort of difference that only shows up in the one place nobody is looking.

let private inABrowser =
    Solo.opened playing "first" dealt
    |> Solo.watching "page" { Notes = true; View = asPage }
    |> fst

report
    "a word after the fact reaches a terminal as a line"
    [ "Record saved to somewhere.log" ]
    (Solo.saying "keyboard" "Record saved to somewhere.log" sitting |> saidTo)

report
    "and reaches a page as something a page can put somewhere"
    true
    (match Solo.saying "page" "Record saved to somewhere.log" inABrowser |> saidTo with
     | [ said ] ->
         said.StartsWith $"<aside id=\"{Page.Told}\""
         && said.Contains "Record saved to somewhere.log"
     | _ -> false)

report "and nobody who is not watching is told anything" [] (Solo.saying "stranger" "Record saved to somewhere.log" sitting)

// --- going --------------------------------------------------------------------------------------

report
    "somebody who stops watching stops being drawn to"
    0
    (Solo.gone "other" watched
     |> fst
     |> say "recruit r 1"
     |> fun (_, posts, _) -> posts |> List.filter (fun post -> post.To = "other") |> List.length)

// --- the seats nobody is sitting in --------------------------------------------------------
//
// A machine at a seat is not a second kind of table. It moves through `Playing.update` like
// anybody else and lands in the record like anybody else; the only thing this table adds is
// that after a person has spoken, the machines answer before the prompt comes back. What is
// worth holding it to is the two places that has an edge: what happens when nobody at the
// table is a person, and what `undo` means when half the moves were not yours.

let private facing sitting solo =
    Solo.against (playing.Seating 42UL sitting (Model.state (Solo.model solo))) solo

let private opposed =
    Solo.opened playing "first" dealt
    |> facing [ None; Some "medium" ]
    |> fst
    |> Solo.watching "keyboard" reading
    |> fst

report
    "the machine answers a move without being asked to, before the prompt comes back"
    2
    (let solo = next "recruit r 1" opposed
     Journal.length (Solo.model solo).Journal)

report
    "and it is Player 1's turn again when it does"
    1
    (let solo = next "recruit r 1" opposed
     PlayerId.value (Game.active (Playing.game (Solo.model solo))).Id)

// The record is the record. A machine's move is written into it in the same words a person's
// would be, so a game played against one replays like any other.

// Which move it picked is `Rival`'s business and [rival.fsx](rival.fsx)'s to check. What
// matters here is that it went into the record against the seat that made it, in the same
// words a person's would have - so a game played against a machine is a record like any
// other, and replays like one.

report
    "a machine's move goes into the record against its own seat, in the words a person types"
    [ 1, "recruit r 1"; 2, "battle r 1" ]
    (next "recruit r 1" opposed
     |> Solo.model
     |> fun model ->
         Journal.entries model.Journal
         |> List.map (fun entry -> PlayerId.value entry.Actor, Words.command entry.Asked))

// The one with an edge on it. Taking a move back has to take the machine's answer back with
// it, or `undo` would hand the turn straight back to the machine and nothing would ever be
// undone.

report
    "undo takes the machine's answer back with it, and stops where a person has to decide"
    (Playing.session dealt)
    (opposed |> next "recruit r 1" |> next "undo" |> Solo.model |> Playing.session)

report
    "and redo brings both of them along again"
    (Playing.session (Solo.model (next "recruit r 1" opposed)))
    (opposed
     |> next "recruit r 1"
     |> next "undo"
     |> next "redo"
     |> Solo.model
     |> Playing.session)

// A table where every seat is the machine's has no seat to stop at, so it plays itself out
// the moment the machines sit down - and asks for the record, there being no later moment to
// ask at. Asked for like any other seating: a seat is the machine's if the list says it is,
// and there is nothing about the first seat that keeps a person in it.

let private noPeople =
    Solo.opened playing "first" dealt
    |> facing (Game.players (Playing.game dealt) |> List.map (fun _ -> Some "easy"))

report "a table of nothing but machines plays itself out as it sits down" true (Playing.isOver (Solo.model (fst noPeople)))

report
    "and asks for the record on the way, there being no later moment to ask at"
    true
    (match snd noPeople with
     | Keeping(model, "first", false) -> Playing.isOver model
     | _ -> false)

report
    "while a table of people asks for nothing when it opens"
    true
    (match snd (facing [] (Solo.opened playing "first" dealt)) with
     | Carrying -> true
     | _ -> false)

// Who you are playing is not on the board - a machine's stones look like anybody's - so the
// table says it once, to whoever sits down, in the words their own view speaks.

report
    "sitting down at a table with a machine at it says which seat that is"
    [ "Played by the machine: Player 2 (medium)." ]
    (Solo.opened playing "first" dealt
     |> facing [ None; Some "medium" ]
     |> fst
     |> Solo.watching "keyboard" reading
     |> snd
     |> saidTo)

report
    "and a table of people says nothing at all"
    []
    (Solo.watching "keyboard" reading (Solo.opened playing "first" dealt)
     |> snd
     |> saidTo)

finish ()
