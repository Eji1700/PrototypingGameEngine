// The second game, and what having one is actually for.
//
// Half of this is the ordinary thing: noughts and crosses has rules, and the rules should be
// right. The other half is the reason it exists at all. `Engine` and `Table` were extracted
// on the claim that they are generic, and a claim like that cannot be tested by the game it
// was extracted from - it can only be tested by a second game going through the same seams
// and getting the same machinery for nothing.
//
// So every check below that is not about three in a row is really about that: the timeline,
// the record on disk, the replay, the shared verbs and the screen all work here, and not one
// line of them was written with noughts and crosses in mind.
//
//   dotnet fsi tests/tictactoe.fsx

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

/// Play a run of squares in turn, from the deal.
let private played squares =
    squares
    |> List.fold (fun model square -> Update.update rules (Make(Place square)) model) dealt

let private standing model = Model.state model

let private mentions (needle: string) (text: string) = text.Contains needle

// --- what the game says is wrong with itself ---------------------------------------------
//
// The board is worked out from its own side rather than written down, so this is the check
// that the arithmetic came out - and the one thing `Faults` is for.

report "the board hangs together" [] noughts.Faults

report "there are eight ways to win" 8 (List.length Squares.lines)

report
    "and every square is on at least one of them"
    []
    (Squares.all
     |> List.filter (fun square -> Squares.lines |> List.forall (fun line -> not (List.contains square line))))

// --- a turn -------------------------------------------------------------------------------

report "crosses go first" (Seat.at 1) (rules.Active(standing dealt))

report "and the board starts empty" 0 (Board.marks (Session.board (standing dealt)))

let private opened = played [ 5 ]

report "a mark lands where it was put" (Some Cross) (Board.at 5 (Session.board (standing opened)))

report "and the turn passes" (Seat.at 2) (rules.Active(standing opened))

report "which is turn two" 2 (rules.Turn(standing opened))

// --- and what it will not take --------------------------------------------------------------

/// Everything the rules said about a move, whether or not it carried.
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

// A refusal is still something that was asked for, so the record keeps it. That is the
// engine's rule rather than this game's, and it holds here without a line being written.

report "but a refused move is written down all the same" 2 (Journal.length (refused 5 opened).Journal)

// --- three in a row ---------------------------------------------------------------------
//
// Every line, rather than one of them: the lines are generated, so a check on one row says
// nothing about the diagonals.

let private winner model =
    match Session.ending (standing model) with
    | Some(Won(mark, line)) -> Some(mark, line)
    | Some _
    | None -> None

/// The shortest game that wins on a given line: crosses take it, noughts take whatever is
/// left over in between.
let private takes line =
    let others =
        Squares.all |> List.filter (fun square -> not (List.contains square line))

    List.zip line (others |> List.truncate (List.length line))
    |> List.collect (fun (mine, theirs) -> [ mine; theirs ])
    // The last of the three wins the game before the reply is played.
    |> List.truncate (2 * List.length line - 1)

for line in Squares.lines do
    let said = line |> List.map string |> String.concat ""

    report $"three in a row on {said} wins it" (Some(Cross, line)) (winner (played (takes line)))

report "and the game is over" true (rules.Over(standing (played (takes [ 1; 5; 9 ]))))

// A board with no line on it and nowhere left to put one.
//
//   X | O | X
//   X | O | O
//   O | X | X

let private drawnGame = played [ 1; 2; 3; 6; 4; 5; 8; 7; 9 ]

report "a full board with no line is a draw" (Some Drawn) (Session.ending (standing drawnGame))

report "and it is over too" true (rules.Over(standing drawnGame))

report "with every square taken" 9 (Board.marks (Session.board (standing drawnGame)))

// --- putting it down -----------------------------------------------------------------------

let private resigned = Update.update rules (Make Resign) opened

report "resigning ends the game, and says who walked away" (Some(Abandoned Nought)) (Session.ending (standing resigned))

report
    "and a move asked for after it is over is answered by the engine, not by the game"
    true
    (toldBy (refused 1 resigned)
     |> List.exists (mentions "The game is over, so there is nothing left to play"))

// --- everything the machinery brings with it ------------------------------------------------
//
// None of what follows was written for this game. It is here because a second game through
// the same seams is the only honest way to find out whether that is true.

// Walking the game about.

let private walked = played [ 1; 2; 3 ]

report "a move can be taken back" (standing (played [ 1; 2 ])) (standing (Update.update rules Undo walked))

report "and made again" (standing walked) (standing (walked |> Update.update rules Undo |> Update.update rules Redo))

report "and there is nothing to take back at the deal" (standing dealt) (standing (Update.update rules Undo dealt))

// The record, which is written in the words the prompt takes and read back by the same
// parser - so a game plays again from its own file without a second language in between.

let private record = Transcript.write noughts walked.Journal

report "a record says how the game was dealt" true (record |> mentions "deal 2 0")

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

// The words every game knows, read once for all of them - so this game's own reader never
// sees `undo`, and could not redefine it if it tried.

/// What a typed line came to, said in a word. A `Command` carries no equality of its own -
/// it holds a move, and a game's move need not be comparable - so what a check compares is
/// what the command says it is.
let private reads typed =
    match Playable.read noughts typed with
    | Ok(Send msg) -> Ok(Words.command msg)
    | Ok Help -> Ok "help"
    | Ok(Notes wanted) -> Ok $"notes {wanted}"
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

// --- the screen ------------------------------------------------------------------------------

let private view = Playable.plainest AtATerminal (Playable.standard noughts) noughts

let private board = view.Board true (Seat.at 1) walked

report "the board is drawn with the marks on it" true (board |> mentions "X | O | X")

report "and the free squares showing what to type" true (board |> mentions "7 | 8 | 9")

report "it says whose turn it is" true (board |> mentions "Turn 4 - O to play")

report "and marks the seat belonging to whoever is reading" true (board |> mentions "X (you)")

report "the other seat is not marked as theirs" false (board |> mentions "O (you)")

report "the notes can be turned off" false (view.Board false (Seat.at 1) walked |> mentions Render.Notes.winning)

report "what the game said is on the screen" true (board |> mentions "X takes square 3.")

report "the record reads back through the view too" true (view.History (Seat.at 1) walked |> mentions "X: place 1")

report
    "and there is an answer for a game with nothing to explain"
    true
    (view.Answer "anything at all" walked
     |> mentions "nothing here that needs working out")

// The record's bargain, held to by the screen: every command the board offers is a line the
// program will actually take. `html.fsx` asks this of the other game's buttons; here there
// are no buttons yet, so it is asked of the words the board prints.

/// The word in the left-hand column of every row the board offers, `undo, redo` counting as
/// two. Rows that need something after them - `view <name>` - are left out: what they take
/// is checked above, one at a time, in the words a person would really type.
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

// --- and one thing that is only true here -----------------------------------------------------
//
// The other game hides a bag from everybody but its owner, and three renderers get held to
// that in `view.fsx`. This one hides nothing, which is worth saying out loud rather than
// leaving as an absence: it is the contrast that shows `Knowledge` is a game's own idea and
// not something the screens require.

for seat in [ Seat.at 1; Seat.at 2 ] do
    report
        $"{noughts.Seat seat} is told everything the game said"
        (walked.Log |> List.map (Playable.told noughts))
        (walked.Log |> List.map (Playable.toldSeenBy noughts seat))

// --- all three ways of drawing it ---------------------------------------------------------
//
// One decision and three layouts: `plain` writes text, `rich` builds walled squares out of
// Spectre's widgets, and `html` builds nine buttons. A block that got added to two of them
// and quietly missed off the third is the way this goes wrong, so the sweep asks all of
// them - and a fourth view added later is held to it without anybody remembering to come
// back here.

let private views = noughts.Views standard

/// What a person would actually see, whatever a view wrote it in: colour taken back off,
/// and markup with it.
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
    let drawn = seen (view.Board true (Seat.at 1) walked)

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
        (seen (view.Board false (Seat.at 1) walked) |> mentions Render.Notes.winning)

    report
        $"the {view.Name} view answers a table still filling up"
        true
        // In `Scene`'s words rather than this game's. A table still filling up is drawn from
        // a list of who has arrived and nothing else, so there is no game in it to differ
        // about - and it had been written out once per game, in wordings that had already
        // begun to drift apart.
        (seen (view.Waiting arriving) |> mentions Scene.Filling.title)

// --- the page, which can go wrong in ways nothing else can ------------------------------------
//
// A browser handed broken markup does not complain: it guesses, draws whatever it made of
// the mess, and leaves nobody any the wiser. So being well-formed is checked here, where it
// can still fail out loud - and so is the one thing that makes a board a board rather than a
// picture of one, which is that every button on it types a line the parser takes.

let private page = Page.page noughts.Page standard

let private fragments =
    [ "board", Page.Screen, asPage.Board true (Seat.at 1) walked
      "board with the notes off", Page.Screen, asPage.Board false (Seat.at 1) walked
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

// A page carries its colours in its own head and every fragment draws in those, which is
// what lets one board be built and read by however many people in however many palettes.

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

// A game describing its screens gets every shape on the page from `Page`'s own sheet - the
// walls round a cell, the grid they sit in, a glyph drawn large - so what is left of this
// game's stylesheet is the one thing that is this game's, which is how big nine squares want
// to be. That it still reaches the page is what is being asked here: the seam is the same
// one, and a game with a whole sheet of its own would arrive by it just as this does.
report "the game's own stylesheet reaches the page" true (page |> mentions "--cell")

report
    "and the board itself is the same board whatever the colours"
    (asPage.Board true (Seat.at 1) walked)
    ((Playable.plainest InABrowser crossesAreTeal noughts).Board true (Seat.at 1) walked)

/// What a control on the page would send. The address is written into the markup escaped
/// twice over - once for the client's own language and once for HTML - so it comes back the
/// same way, and what is left after both is the line a player would have typed.
let private posted (markup: string) =
    Regex.Matches(WebUtility.HtmlDecode markup, @"@post\('/say\?line=([^']*)'\)")
    |> Seq.map (fun found -> Uri.UnescapeDataString found.Groups[1].Value)
    |> List.ofSeq

let private buttons = posted (asPage.Board true (Seat.at 1) walked)

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

// --- one description, three readers -------------------------------------------------------
//
// The sweep above asks each view the same questions and holds the answers up against each
// other, which is how this was checked for as long as there were three views to write. There
// is one now: this game says what a screen is made of and `Readers` draws it three ways.
//
// So the questions worth asking have changed shape. Not "do the three agree" - they cannot
// disagree, there being one description - but whether the description says what it should,
// and whether each reader is really drawing what it was handed.

/// Every control in a described screen: what it is captioned, and the line it types.
let rec private controls scene =
    match scene with
    | Does(caption, line, _) -> [ caption, line ]
    | Block(_, body)
    | Stack body
    | Beside body
    | Tile(_, _, body) -> body |> List.collect controls
    | Walled(_, rows) -> rows |> List.collect (fun row -> row.Cells |> List.collect controls)
    | _ -> []

let private described = controls (Render.board true (Seat.at 1) walked)

// The one thing a `Does` is for. A control carries the line it would type, so what a player
// at a terminal is told to type and what a browser posts when the same control is clicked are
// not two strings that have to be kept in step - they are one string, read twice.

report "every control types the line it is captioned with" [] (described |> List.filter (fun (caption, line) -> caption <> line))

report "and the page's buttons are exactly the controls the game described" (described |> List.map snd) buttons

report
    "and the terminal is told to type the very same lines"
    true
    (described |> List.forall (fun (caption, _) -> plain.Board true (Seat.at 1) walked |> mentions caption))

// A note is the game's decision and not a reader's, so it is off in the description before it
// is off on any screen - which is what makes turning it off in one place enough.

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
    (notes (Render.board true (Seat.at 1) walked))

report "and not one of them survives turning them off" [] (notes (Render.board false (Seat.at 1) walked))

// --- the seat the program plays --------------------------------------------------------------
//
// A machine at a table is the one thing here that can be wrong quietly: one that picked a
// move nobody would has picked a legal move, drawn a perfectly good board and said nothing
// at all. This game is small enough to be solved outright, so what `hard` promises is not
// "plays well" but "cannot be beaten" - which is a thing that can actually be checked.

report "three machines are offered, worst to best" [ "easy"; "medium"; "hard" ] (noughts.Skills |> List.map fst)

/// A whole game between two machines, played the way any table plays one: the engine asks,
/// they answer, and nothing in the asking knows how they chose.
let private playedOut skills seed =
    let seated = noughts.Seating seed (skills |> List.map Some) (standing dealt)
    Machines.answering rules Playable.plays seated dealt |> fst

let private outcome model =
    match Session.ending (standing model) with
    | Some(Won(mark, _)) -> Some mark
    | Some _
    | None -> None

// Twelve deals, each played both ways round, so going first is not what is being measured.
// Fixed seeds throughout: there is nothing to be flaky here, and a run that came out
// differently would mean the machine had changed rather than the dice.
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

// The word `hard` is a promise to a player, so it owes them something. At a game this size
// it can keep the strongest form of that promise there is.

report "hard never loses to easy, either way round" 0 (lost hardVsEasy)

report "and beats it often enough to be worth the name" true (won hardVsEasy > 6)

report "and two of them can only draw, this game being a draw when neither errs" 0 (won hardVsHard + lost hardVsHard)

// And it is a fold, like everything else here: the generator travels inside the machine the
// way the game's own travels inside the game.

report
    "the same machines at the same deal play the same game twice"
    (Journal.moves (playedOut [ "hard"; "easy" ] 7UL).Journal)
    (Journal.moves (playedOut [ "hard"; "easy" ] 7UL).Journal)

report
    "and a different seed is a different game"
    false
    (Journal.moves (playedOut [ "easy"; "easy" ] 7UL).Journal = Journal.moves (playedOut [ "easy"; "easy" ] 8UL).Journal)

finish ()
