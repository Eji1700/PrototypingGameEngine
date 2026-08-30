#load "Whole.fsx"

open Prototyping.Engine
open Prototyping.Table
open Prototyping.Turncoats
open Checks
open Whole

let private dealt = Playing.start 2 42UL |> Result.toOption |> Option.get

let private reading =
    { Margins = Margins.all
      Hushed = false
      View = plain }

let private sitting =
    Solo.opened playing "first" dealt |> Solo.watching "keyboard" reading |> fst

let private say line solo =
    Solo.said (fun () -> "afresh") "keyboard" line solo

let private turn solo =
    Solo.said (fun () -> "afresh") "keyboard" "" solo

let private next line solo =
    let solo, _, _ = say line solo
    solo

let private screens (posts: Post list) =
    posts
    |> List.choose (fun post ->
        match post.Say with
        | ToPlayer.Screen text -> Some text
        | _ -> None)

let private saidTo (posts: Post list) =
    posts
    |> List.choose (fun post ->
        match post.Say with
        | ToPlayer.Told text
        | ToPlayer.TurnedAway text -> Some text
        | _ -> None)


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
    (Solo.said (fun () -> "afresh") "stranger" "negotiate" (Solo.opened playing "s" dealt)
     |> fun (_, posts, _) -> saidTo posts)


let private watched = sitting |> Solo.watching "other" reading |> fst

report
    "a move is drawn to everybody watching the one hot seat"
    2
    (say "recruit r 1" watched |> fun (_, posts, _) -> screens posts |> List.length)

report
    "but changing how you read is drawn only to you"
    1
    (say "notes off" watched |> fun (_, posts, _) -> screens posts |> List.length)


let private drawnAfter lines =
    lines
    |> List.fold (fun solo line -> next line solo) sitting
    |> Solo.board "keyboard"
    |> Option.get

let private says (needle: string) (text: string) = (flat text).Contains(flat needle)

let private aCommand = List.head Render.commands

report "the commands are on the board to begin with" true (drawnAfter [] |> says aCommand)

report "'commands' takes them off" false (drawnAfter [ "commands" ] |> says aCommand)

report "and 'commands' again puts them back" true (drawnAfter [ "commands"; "commands" ] |> says aCommand)

report "turning the notes off leaves the commands where they are" true (drawnAfter [ "notes off" ] |> says aCommand)

report
    "and turning the commands off leaves the notes where they are"
    true
    (drawnAfter [ "commands off" ] |> says Render.Notes.landRuled)


let private nudged console posts =
    posts
    |> List.exists (fun post -> post.To = console && post.Say = ToPlayer.Nudged)

report
    "a move nudges the other console watching the hot seat"
    true
    (nudged "other" (say "recruit r 1" watched |> fun (_, posts, _) -> posts))

report "and not the one that made it" false (nudged "keyboard" (say "recruit r 1" watched |> fun (_, posts, _) -> posts))

report
    "at one keyboard nothing is nudged at all, there being nobody to interrupt but yourself"
    []
    (say "recruit r 1" sitting
     |> fun (_, posts, _) -> posts |> List.filter (fun post -> post.Say = ToPlayer.Nudged))

report
    "a line that only changes how you read nudges nobody"
    false
    (nudged "other" (say "notes off" watched |> fun (_, posts, _) -> posts))


let private boardSays (solo: Solo<_, _, _>) (needle: string) =
    match Solo.board "keyboard" solo with
    | Some board -> board.Contains needle
    | None -> false

report "the board is drawn for whoever is to play" true (boardSays sitting "Player 1 (you)")

report "and turns over with the turn" true (boardSays (next "recruit r 1" sitting) "Player 2 (you)")


let private moved = sitting |> next "recruit r 1"

report "undo takes the last move back" (Playing.session dealt) (Playing.session (Solo.model (next "undo" moved)))

report
    "and redo makes it again"
    (Playing.session (Solo.model moved))
    (Playing.session (Solo.model (moved |> next "undo" |> next "redo")))

report "a restart deals a fresh game to the same players" true (Journal.isEmpty (Solo.model (next "restart" moved)).Journal)


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

report
    "'quit' asks for it too, and leaves the game where it stands"
    true
    (match errandOf "quit" moved with
     | Leaving(Some model, "first") -> not (Playing.isOver model)
     | _ -> false)

report
    "so a game put down can be taken up again"
    (Playing.session (Solo.model moved))
    (match errandOf "quit" moved with
     | Leaving(Some model, _) -> Playing.session model
     | _ -> failwith "quitting wrote nothing down")

report
    "a game taken up and put straight down again writes nothing"
    true
    (match errandOf "quit" sitting with
     | Leaving(None, "first") -> true
     | _ -> false)


report
    "a restart writes the game it swept away, under that game's own name"
    true
    (match errandOf "restart" moved with
     | Keeping(model, "first", false) -> Journal.entries model.Journal |> List.length = 1
     | _ -> false)

report "and the game that replaces it is kept under the new name" "afresh" (Solo.stamp (next "restart" moved))


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
    "a line the parser cannot read is answered, in words"
    true
    (say "frobnicate" sitting
     |> fun (_, posts, _) -> saidTo posts |> List.exists (fun said -> said <> ""))

report
    "and it leaves the game alone"
    (Playing.session dealt)
    (let solo, _, _ = say "frobnicate" sitting
     Playing.session (Solo.model solo))


let private inABrowser =
    Solo.opened playing "first" dealt
    |> Solo.watching
        "page"
        { Margins = Margins.all
          Hushed = false
          View = asPage }
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


report
    "somebody who stops watching stops being drawn to"
    0
    (Solo.gone "other" watched
     |> fst
     |> say "recruit r 1"
     |> fun (_, posts, _) -> posts |> List.filter (fun post -> post.To = "other") |> List.length)


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


report
    "a machine's move goes into the record against its own seat, in the words a person types"
    [ 1, "recruit r 1"; 2, "battle r 1" ]
    (next "recruit r 1" opposed
     |> Solo.model
     |> fun model ->
         Journal.entries model.Journal
         |> List.map (fun entry -> PlayerId.value entry.Actor, Words.command entry.Asked))


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
