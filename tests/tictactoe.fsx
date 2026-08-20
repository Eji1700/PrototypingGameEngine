#load "Noughts.fsx"

open System
open System.Net
open System.Text.RegularExpressions
open System.Xml
open TCModel.Engine
open TCModel.Table
open TCModel.TicTacToe
open Checks
open Noughts

let private rules = noughts.Rules

let private dealt = Update.start rules 2 0UL |> Result.toOption |> Option.get

let private played squares =
    squares
    |> List.fold (fun model square -> Update.update rules (Make(Place square)) model) dealt

let private standing model = Model.state model

let private mentions (needle: string) (text: string) = text.Contains needle


report "the board hangs together" [] noughts.Faults

report "there are eight ways to win" 8 (List.length Squares.lines)

report
    "and every square is on at least one of them"
    []
    (Squares.all
     |> List.filter (fun square -> Squares.lines |> List.forall (fun line -> not (List.contains square line))))


report "crosses go first" (Seat.at 1) (rules.Active(standing dealt))

report "and the board starts empty" 0 (Board.marks (Session.board (standing dealt)))

let private opened = played [ 5 ]

report "a mark lands where it was put" (Some Cross) (Board.at 5 (Session.board (standing opened)))

report "and the turn passes" (Seat.at 2) (rules.Active(standing opened))

report "which is turn two" 2 (rules.Turn(standing opened))


let private toldBy model =
    model.Log |> List.rev |> List.map (Playable.told noughts)

let private refused square from =
    Update.update rules (Make(Place square)) from

report
    "a square nobody has is refused, and says which there are"
    true
    (toldBy (refused 10 opened) |> List.exists (mentions "There is no square 10"))

report "and the game does not move" (standing opened) (standing (refused 10 opened))

report
    "a square already taken is refused, and says what is in it"
    true
    (toldBy (refused 5 opened)
     |> List.exists (mentions "Square 5 already has X in it"))

report "and that game does not move either" (standing opened) (standing (refused 5 opened))


report "but a refused move is written down all the same" 2 (Journal.length (refused 5 opened).Journal)


let private winner model =
    match Session.ending (standing model) with
    | Some(Won(mark, line)) -> Some(mark, line)
    | Some _
    | None -> None

let private takes line =
    let others =
        Squares.all |> List.filter (fun square -> not (List.contains square line))

    List.zip line (others |> List.truncate (List.length line))
    |> List.collect (fun (mine, theirs) -> [ mine; theirs ])
    |> List.truncate (2 * List.length line - 1)

for line in Squares.lines do
    let said = line |> List.map string |> String.concat ""

    report $"three in a row on {said} wins it" (Some(Cross, line)) (winner (played (takes line)))

report "and the game is over" true (rules.Over(standing (played (takes [ 1; 5; 9 ]))))


let private drawnGame = played [ 1; 2; 3; 6; 4; 5; 8; 7; 9 ]

report "a full board with no line is a draw" (Some Drawn) (Session.ending (standing drawnGame))

report "and it is over too" true (rules.Over(standing drawnGame))

report "with every square taken" 9 (Board.marks (Session.board (standing drawnGame)))


let private resigned = Update.update rules (Make Resign) opened

report "resigning ends the game, and says who walked away" (Some(Abandoned Nought)) (Session.ending (standing resigned))

report
    "and a move asked for after it is over is answered by the engine, not by the game"
    true
    (toldBy (refused 1 resigned)
     |> List.exists (mentions "The game is over, so there is nothing left to play"))


let private walked = played [ 1; 2; 3 ]

report "a move can be taken back" (standing (played [ 1; 2 ])) (standing (Update.update rules Undo walked))

report "and made again" (standing walked) (standing (walked |> Update.update rules Undo |> Update.update rules Redo))

report "and there is nothing to take back at the deal" (standing dealt) (standing (Update.update rules Undo dealt))


let private record =
    Transcript.write noughts [ Here; Machine "hard" ] walked.Journal

report "a record says how the game was dealt, and who was at it" true (record |> mentions "deal 2 0 you hard")

report "and names the game it is a record of" true (record |> mentions noughts.Title)

let private readBack =
    Transcript.read noughts record |> Result.toOption |> Option.get

report "every move comes back off it" [ Make(Place 1); Make(Place 2); Make(Place 3) ] readBack.Moves

report
    "and playing them again arrives at the same game"
    (standing walked)
    (standing (
        Update.replay rules readBack.Players readBack.Seed readBack.Moves
        |> Result.toOption
        |> Option.get
    ))


let private reads typed =
    match Playable.read noughts typed with
    | Ok(Send msg) -> Ok(Words.command msg)
    | Ok Help -> Ok "help"
    | Ok(Notes wanted) -> Ok $"notes {wanted}"
    | Ok(Listing wanted) -> Ok $"commands {wanted}"
    | Ok(Looking name) -> Ok $"view {name}"
    | Ok(Asking question) -> Ok $"asking {question}"
    | Ok Recount -> Ok "history"
    | Ok Keep -> Ok "save"
    | Ok Leave -> Ok "quit"
    | Ok Nothing -> Ok "nothing"
    | Error problem -> Error problem

report "'undo' is not this game's business" (Ok "undo") (reads "undo")

report "nor is 'save'" (Ok "save") (reads "save")

report "nor 'view rich'" (Ok "view rich") (reads "view rich")

report "'resign' plays what this game says resigning is" (Ok "resign") (reads "resign")

report "a bare number is a move, because on this board naming a square is the whole move" (Ok "place 7") (reads "7")

report "and so is the long way round" (Ok "place 7") (reads "place 7")

report
    "a square nobody could type is refused where it was typed"
    true
    (match reads "place seven" with
     | Error problem -> problem |> mentions "'seven' is not a square"
     | Ok _ -> false)

report "a table of three is refused, in words a person can read" (Error "3 players? The game takes 2.") (reads "players 3")

report
    "and so is a deal of three, by the rules themselves"
    true
    (match rules.Deal 3 0UL with
     | Error problem -> problem |> mentions "Noughts and crosses takes 2"
     | Ok _ -> false)


let private view = Playable.plainest AtATerminal (Playable.standard noughts) noughts

let private board = view.Board Margins.all (Seat.at 1) walked

report "the board is drawn with the marks on it" true (board |> mentions "X | O | X")

report "and the free squares showing what to type" true (board |> mentions "7 | 8 | 9")

report "it says whose turn it is" true (board |> mentions "Turn 4 - O to play")

report "and marks the seat belonging to whoever is reading" true (board |> mentions "X (you)")

report "the other seat is not marked as theirs" false (board |> mentions "O (you)")

report "the notes can be turned off" false (view.Board Margins.none (Seat.at 1) walked |> mentions Render.Notes.winning)

report "what the game said is on the screen" true (board |> mentions "X takes square 3.")

report "the record reads back through the view too" true (view.History (Seat.at 1) walked |> mentions "X: place 1")

report
    "and there is an answer for a game with nothing to explain"
    true
    (view.Answer (Seat.at 1) "anything at all" walked
     |> mentions "nothing here that needs working out")


let private offered =
    Render.commands.Split '\n'
    |> Array.choose (fun row ->
        match row.Split("  ", System.StringSplitOptions.RemoveEmptyEntries) with
        | [||] -> None
        | parts -> Some(parts[0].Trim()))
    |> Array.collect (fun verb -> verb.Split ',')
    |> Array.map (fun verb -> verb.Trim())
    |> Array.filter (fun verb -> verb <> "" && not (verb.Contains ' '))
    |> List.ofArray
    |> List.distinct

report "the board does offer some commands" true (List.length offered > 4)

report
    "and every one of them is a line the program takes"
    []
    (offered
     |> List.filter (fun verb ->
         match reads verb with
         | Ok "nothing"
         | Error _ -> true
         | Ok _ -> false))


for seat in [ Seat.at 1; Seat.at 2 ] do
    report
        $"{noughts.Seat seat} is told everything the game said"
        (walked.Log |> List.map (Playable.told noughts))
        (walked.Log |> List.map (Playable.toldSeenBy noughts seat))


let private views = noughts.Views standard

let private seen text =
    let uncoloured = Regex.Replace(text, "\\[[0-9;]*m", "")
    Regex.Replace(uncoloured, "<[^>]*>", "")

report "there are three of them" [ "plain"; "rich"; "html" ] (views |> List.map (fun view -> view.Name))

let private arriving =
    [ { Player = Seat.at 1
        Expected = false
        Away = false
        Yours = true }
      { Player = Seat.at 2
        Expected = true
        Away = false
        Yours = false } ]

for view in views do
    let drawn = seen (view.Board Margins.all (Seat.at 1) walked)

    report $"the {view.Name} view says whose turn it is" true (drawn |> mentions "Turn 4")

    report $"the {view.Name} view draws the marks that have been played" true (drawn |> mentions "X" && drawn |> mentions "O")

    report
        $"the {view.Name} view offers the squares nobody has taken"
        true
        ([ 4; 5; 6; 7; 8; 9 ]
         |> List.forall (fun square -> drawn |> mentions (string square)))

    report $"the {view.Name} view marks the seat belonging to whoever is reading" true (drawn |> mentions "X (you)")

    for block in [ Render.Blocks.board; Render.Blocks.players; Render.Blocks.log ] do
        report
            $"the {view.Name} view has a block for {block}"
            true
            (drawn.ToLowerInvariant() |> mentions (block.ToLowerInvariant()))

    report $"the {view.Name} view shows what the game has said" true (drawn |> mentions "takes square 3")

    report
        $"and the {view.Name} view's notes can be turned off"
        false
        (seen (view.Board Margins.none (Seat.at 1) walked)
         |> mentions Render.Notes.winning)

    report
        $"the {view.Name} view answers a table still filling up"
        true
        (seen (view.Waiting arriving) |> mentions Scene.Filling.title)


let private page = Page.page noughts.Page standard

let private fragments =
    [ "board", Page.Screen, asPage.Board Margins.all (Seat.at 1) walked
      "board with the notes off", Page.Screen, asPage.Board Margins.none (Seat.at 1) walked
      "waiting", Page.Screen, asPage.Waiting arriving
      "a line the game said", Page.Told, asPage.Says "It is O's turn."
      "the record", Page.Told, asPage.History (Seat.at 1) walked
      "the rules", Page.Told, asPage.Rules ]

let private read (markup: string) =
    let document = XmlDocument()
    use reader = new XmlTextReader(new IO.StringReader(markup), Namespaces = false)
    document.Load reader
    document

let private parses (markup: string) =
    try
        read markup |> ignore
        true
    with _ ->
        false

for name, _, markup in fragments do
    report $"the {name} is well-formed markup" true (parses markup)

report "and so is the page itself" true (parses page)

for name, slot, markup in fragments do
    report
        $"the {name} is one element, carrying the id it will be patched by"
        slot
        ((read markup).DocumentElement.GetAttribute "id")


let private crossesAreTeal =
    Palette.set "x" "teal" standard |> Result.toOption |> Option.get

report
    "the page is drawn in the colours it is given"
    true
    (Page.page noughts.Page crossesAreTeal
     |> mentions (Palette.paint (Palette.shadeOf "x" crossesAreTeal)))

report
    "and not in the ones it was not"
    false
    (Page.page noughts.Page crossesAreTeal
     |> mentions (Palette.paint Palette.crimson))

report "the game's own stylesheet reaches the page" true (page |> mentions "--cell")

report
    "and the board itself is the same board whatever the colours"
    (asPage.Board Margins.all (Seat.at 1) walked)
    ((Playable.plainest InABrowser crossesAreTeal noughts).Board Margins.all (Seat.at 1) walked)

let private posted (markup: string) =
    Regex.Matches(WebUtility.HtmlDecode markup, @"@post\('/say\?line=([^']*)'\)")
    |> Seq.map (fun found -> Uri.UnescapeDataString found.Groups[1].Value)
    |> List.ofSeq

let private buttons = posted (asPage.Board Margins.all (Seat.at 1) walked)

report "the board has a button for every square nobody has taken" [ "4"; "5"; "6"; "7"; "8"; "9" ] buttons

report
    "and every one of them types a line the game's own parser takes"
    []
    (buttons
     |> List.filter (fun line ->
         match reads line with
         | Ok "nothing"
         | Error _ -> true
         | Ok _ -> false))

report "a square already taken is not a button" [] (buttons |> List.filter (fun line -> [ "1"; "2"; "3" ] |> List.contains line))


let rec private controls scene =
    match scene with
    | Does(caption, line, _) -> [ caption, line ]
    | Block(_, body)
    | Stack body
    | Beside body
    | Tile(_, _, body) -> body |> List.collect controls
    | Walled(_, rows) -> rows |> List.collect (fun row -> row.Cells |> List.collect controls)
    | _ -> []

let private described = controls (Render.board Margins.all (Seat.at 1) walked)


report "every control types the line it is captioned with" [] (described |> List.filter (fun (caption, line) -> caption <> line))

report "and the page's buttons are exactly the controls the game described" (described |> List.map snd) buttons

report
    "and the terminal is told to type the very same lines"
    true
    (described
     |> List.forall (fun (caption, _) -> plain.Board Margins.all (Seat.at 1) walked |> mentions caption))


let rec private notes scene =
    match scene with
    | Note text -> [ text ]
    | Block(_, body)
    | Stack body
    | Beside body
    | Tile(_, _, body) -> body |> List.collect notes
    | Walled(_, rows) -> rows |> List.collect (fun row -> row.Cells |> List.collect notes)
    | _ -> []

report
    "the notes the game explains its board with"
    [ Render.Notes.board; Render.Notes.winning ]
    (notes (Render.board Margins.all (Seat.at 1) walked))

report "and not one of them survives turning them off" [] (notes (Render.board Margins.none (Seat.at 1) walked))


report "three machines are offered, worst to best" [ "easy"; "medium"; "hard" ] (noughts.Skills |> List.map fst)

let private playedOut skills seed =
    let seated = noughts.Seating seed (skills |> List.map Some) (standing dealt)
    Machines.answering rules Playable.plays seated dealt |> fst

let private outcome model =
    match Session.ending (standing model) with
    | Some(Won(mark, _)) -> Some mark
    | Some _
    | None -> None

let private duel one other =
    [ 1UL .. 12UL ]
    |> List.collect (fun seed -> [ playedOut [ one; other ] seed, Cross; playedOut [ other; one ] seed, Nought ])

let private lost pairs =
    pairs
    |> List.filter (fun (model, mine) -> outcome model = Some(Mark.other mine))
    |> List.length

let private won pairs =
    pairs
    |> List.filter (fun (model, mine) -> outcome model = Some mine)
    |> List.length

let private hardVsEasy = duel "hard" "easy"

let private hardVsHard = duel "hard" "hard"

report
    "a table of machines plays the game out to its end"
    true
    (hardVsHard |> List.forall (fun (model, _) -> rules.Over(standing model)))

report
    "and nothing either of them asked for over a run of games was refused"
    []
    (hardVsHard
     |> List.collect (fun (model, _) -> model.Log)
     |> List.choose (function
         | Said(Refused refusal) -> Some(Words.rejection refusal)
         | _ -> None))


report "hard never loses to easy, either way round" 0 (lost hardVsEasy)

report "and beats it often enough to be worth the name" true (won hardVsEasy > 6)

report "and two of them can only draw, this game being a draw when neither errs" 0 (won hardVsHard + lost hardVsHard)


report
    "the same machines at the same deal play the same game twice"
    (Journal.moves (playedOut [ "hard"; "easy" ] 7UL).Journal)
    (Journal.moves (playedOut [ "hard"; "easy" ] 7UL).Journal)

report
    "and a different seed is a different game"
    false
    (Journal.moves (playedOut [ "easy"; "easy" ] 7UL).Journal = Journal.moves (playedOut [ "easy"; "easy" ] 8UL).Journal)

finish ()
