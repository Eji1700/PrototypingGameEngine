#load "Living.fsx"
#load "Conforms.fsx"

open System
open System.Net
open System.Text.RegularExpressions
open System.Xml
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Life
open Checks
open Living

let private rules = life.Rules

let private dealt = Update.start rules 1 0UL |> Result.toOption |> Option.get

let private standing model = Model.state model

let private mentions (needle: string) (text: string) = text.Contains needle

let private at word = Grid.read word |> Option.get

let private played moves =
    moves
    |> List.fold (fun model move -> Update.update rules (Make move) model) dealt

let private drawn cells =
    played (Clear :: (cells |> List.map (at >> Toggle)))

let private living model = (standing model).Cells

let private shape cells = Set.ofList (cells |> List.map at)


report "the board hangs together" [] life.Faults

report "it is four hundred and sixteen squares" (Grid.Width * Grid.Height) (List.length Grid.all)

report
    "every cell has eight neighbours"
    []
    (Grid.all
     |> List.filter (fun cell -> List.length (List.distinct (Grid.neighbours cell)) <> 8))


report "the corner touches the far corner" true (Grid.neighbours (at "a1") |> List.contains (at "z16"))

report "and the far side of its own row" true (Grid.neighbours (at "a1") |> List.contains (at "z1"))

report "a cell whose name does not read back" [] (Grid.all |> List.filter (fun cell -> Grid.read (Grid.name cell) <> Some cell))


report "a lone cell dies" Set.empty (living (drawn [ "m8" ] |> fun model -> Update.update rules (Make(Step 1)) model))

let private block = [ "j10"; "j11"; "k10"; "k11" ]

report "a block stands still" (shape block) (living (played (Clear :: (block |> List.map (at >> Toggle)) @ [ Step 1 ])))

let private blinker = [ "b2"; "b3"; "b4" ]

report
    "a blinker turns over"
    (shape [ "a3"; "b3"; "c3" ])
    (living (played (Clear :: (blinker |> List.map (at >> Toggle)) @ [ Step 1 ])))

report
    "and comes back two generations later"
    (shape blinker)
    (living (played (Clear :: (blinker |> List.map (at >> Toggle)) @ [ Step 2 ])))


let private glider = [ "f7"; "g8"; "h6"; "h7"; "h8" ]

let private gliding steps =
    played (Clear :: (glider |> List.map (at >> Toggle)) @ [ Step steps ])

report "a glider moves one square diagonally in four generations" (shape [ "g8"; "h9"; "i7"; "i8"; "i9" ]) (living (gliding 4))

report "and is still five cells after forty" 5 (Set.count (living (gliding 40)))

let private moved (down, across) cells =
    cells
    |> Set.map (fun cell ->
        { Row = (cell.Row - 1 + down) % Grid.Height + 1
          Column = (cell.Column - 1 + across) % Grid.Width + 1 })

report "and has gone ten squares diagonally by then, edges and all" (moved (10, 10) (shape glider)) (living (gliding 40))


report "the deal is generation nought" 0 (rules.Turn(standing dealt))

report "and is a soup rather than an empty board" true (World.living (standing dealt) > 0)

report "the same seed is the same soup" (World.dealt 42UL).Cells (World.dealt 42UL).Cells

report "and a different seed is a different one" false ((World.dealt 42UL).Cells = (World.dealt 43UL).Cells)

report "a step is one generation" 1 (rules.Turn(standing (played [ Step 1 ])))

report "and a run is as many as it was asked for" 25 (rules.Turn(standing (played [ Step 25 ])))

report
    "which is the same as asking for them one at a time"
    (living (played [ Step 25 ]))
    (living (played (List.replicate 25 (Step 1))))


let private toldBy model =
    model.Log |> List.rev |> List.map (Playable.told life)

report
    "a cell off the board is refused, and says where the board ends"
    true
    (toldBy (played [ Toggle(at "a40") ])
     |> List.exists (mentions "There is no cell a40"))

report "and the game does not move" (standing dealt) (standing (played [ Toggle(at "a40") ]))

report
    "a run of nought is refused, and says what a run may be"
    true
    (toldBy (played [ Step 0 ])
     |> List.exists (mentions $"Say a number of generations from 1 to {Turn.Longest}"))

report
    "and so is a run longer than the cap"
    true
    (toldBy (played [ Step(Turn.Longest + 1) ])
     |> List.exists (mentions "A run of 101"))


report "but a refused move is written down all the same" 1 (Journal.length (played [ Step 0 ]).Journal)


let private settled = drawn block

report "a still life says so" true (World.settled (standing settled))

report
    "and asking it to run is refused rather than answered with the same board"
    true
    (toldBy (played (Clear :: (block |> List.map (at >> Toggle)) @ [ Step 5 ]))
     |> List.exists (mentions "this board is a still life"))

report "the game is not over all the same" false (rules.Over(standing settled))

let private died = played [ Clear; Toggle(at "m8"); Step 1 ]

report "a board the rule empties says the last of them died" true (toldBy died |> List.exists (mentions "The last of them died"))

report "and it is still not over" false (rules.Over(standing died))

report
    "a cell can be turned on again afterwards"
    1
    (World.living (standing (played [ Clear; Toggle(at "m8"); Step 1; Toggle(at "c3") ])))

report
    "this game is never over, which is the honest answer for one that cannot be won"
    [ false; false; false ]
    ([ dealt; settled; died ] |> List.map (standing >> rules.Over))


report
    "a blinker is beating, two generations in"
    true
    (World.beating (standing (played (Clear :: (blinker |> List.map (at >> Toggle)) @ [ Step 2 ]))))

report "a block is not - it has settled instead" false (World.beating (standing settled))

report "and a board just drawn on knows nothing about beating" false (World.beating (standing (drawn blinker)))


report "there is one seat" 1 (rules.Seats(standing dealt))

report "and the game is always standing at it" (Seat.at 1) (rules.Active(standing dealt))

report
    "a table of two is refused by the rules themselves, in words a person can read"
    true
    (match rules.Deal 2 0UL with
     | Error problem -> problem |> mentions "there is one seat at it"
     | Ok _ -> false)

report "and there is no machine to sit in the one there is" [] life.Skills

report "so a seating asks for none" [] (life.Seating 0UL [ None ] (standing dealt) |> List.map fst)


let private walked = played [ Step 3; Toggle(at "c4") ]

report "a move can be taken back" (living (played [ Step 3 ])) (living (Update.update rules Undo walked))

report "and made again" (living walked) (living (walked |> Update.update rules Undo |> Update.update rules Redo))

report "and there is nothing to take back at the deal" (standing dealt) (standing (Update.update rules Undo dealt))


let private record = Transcript.write life [ Here ] walked.Journal

report "a record says how the game was dealt, and who was at it" true (record |> mentions "deal 1 0 you")

report "and names the game it is a record of" true (record |> mentions life.Title)

let private readBack = Transcript.read life record |> Result.toOption |> Option.get

report "every move comes back off it" [ Make(Step 3); Make(Toggle(at "c4")) ] readBack.Moves

report
    "and playing them again arrives at the same board"
    (living walked)
    (living (
        Update.replay rules readBack.Players readBack.Seed readBack.Moves
        |> Result.toOption
        |> Option.get
    ))


let private reads typed =
    match Playable.read life typed with
    | Ok(Send msg) -> Ok(Words.command msg)
    | Ok Help -> Ok "help"
    | Ok(Notes wanted) -> Ok $"notes {wanted}"
    | Ok(Listing wanted) -> Ok $"commands {wanted}"
    | Ok(Logging wanted) -> Ok(sprintf "log %A" wanted)
    | Ok(Hushing hushed) -> Ok $"sound {hushed}"
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

report
    "there is no resigning from a game with nobody to resign to"
    true
    (match reads "resign" with
     | Error problem -> problem |> mentions "no resigning"
     | Ok _ -> false)

report "a bare cell is a move, because naming a cell is the whole of what to do with one" (Ok "toggle f7") (reads "f7")

report "and the long way round writes the same line" (Ok "toggle f7") (reads "toggle f7")

report "a bare number is a run, which a cell can never be mistaken for" (Ok "step 10") (reads "10")

report "'step' on its own is one generation" (Ok "step") (reads "step")

report "and a question is a question rather than a move" (Ok "asking f7") (reads "why f7")

report
    "a cell nobody could type is refused where it was typed"
    true
    (match reads "seven" with
     | Error problem -> problem |> mentions "'seven' is not a cell"
     | Ok _ -> false)

report "a table of two is refused by the shared verbs too" (Error "2 players? The game takes 1.") (reads "players 2")


let private view = Playable.plainest AtATerminal standard life

let private board = view.Board Margins.all (Seat.at 1) (gliding 4)

report "the board is drawn with the living cells on it" true (board |> mentions "#")

report "under the letters its columns are named by" true (board |> mentions Grid.letters)

report "it says which generation this is, and how many are alive" true (board |> mentions "Generation 4 - 5 cells alive")

report "a settled board says so on its heading instead" true (view.Board Margins.all (Seat.at 1) settled |> mentions "settled")

report
    "and a beating one says that"
    true
    (view.Board Margins.all (Seat.at 1) (played (Clear :: (blinker |> List.map (at >> Toggle)) @ [ Step 2 ]))
     |> mentions "beating")

report "the notes can be turned off" false (view.Board Margins.none (Seat.at 1) (gliding 4) |> mentions Render.Notes.rule)

report "what the game said is on the screen" true (board |> mentions "Ran 4 generations")

report "the record reads back through the view too" true (view.History (Seat.at 1) walked |> mentions "toggle c4")


let private asked = view.Answer (Seat.at 1) "h9" (gliding 4)

report "a cell can be asked about" true (asked |> mentions "h9 is alive")

report "and is told what the rule will do with it" true (asked |> mentions "lives on")

report "and which cells it is counting" true (asked |> mentions "Round it:")

report
    "a cell nobody could ask about is answered rather than ignored"
    true
    (view.Answer (Seat.at 1) "nowhere" (gliding 4) |> mentions "is not a cell")


let private views = life.Views standard

let private seen text =
    let uncoloured = Regex.Replace(text, string (char 27) + @"?\[[0-9;]*m", "")
    Regex.Replace(uncoloured, "<[^>]*>", "")

report "there are three of them" [ "plain"; "rich"; "html" ] (views |> List.map (fun view -> view.Name))

let private ninthRow =
    String.replicate 7 Ink.Empty
    + String.replicate 2 Ink.Living
    + String.replicate (Grid.Width - 9) Ink.Empty

let private arriving =
    [ { Player = Seat.at 1
        Expected = false
        Away = false
        Yours = true } ]

for view in views do
    let drawn = seen (view.Board Margins.all (Seat.at 1) (gliding 4))

    report $"the {view.Name} view says which generation it is" true (drawn |> mentions "Generation 4")

    report $"the {view.Name} view draws that board's ninth row, cell for cell" true (drawn |> mentions ninthRow)

    report $"the {view.Name} view names the rows and columns" true (drawn |> mentions Grid.letters)

    for block in
        [ Render.Blocks.board
          Render.Blocks.run
          Render.Blocks.onwards
          Render.Blocks.log ] do
        report
            $"the {view.Name} view has a block for {block}"
            true
            (drawn.ToLowerInvariant() |> mentions (block.ToLowerInvariant()))

    report $"the {view.Name} view shows what the game has said" true (drawn |> mentions "Ran 4 generations")

    report
        $"and the {view.Name} view's notes can be turned off"
        false
        (seen (view.Board Margins.none (Seat.at 1) (gliding 4))
         |> mentions Render.Notes.rule)

    report
        $"the {view.Name} view answers a table still filling up"
        true
        (seen (view.Waiting arriving) |> mentions Scene.Filling.title)


let private page = Page.page life.Page standard

let private fragments =
    [ "board", Page.Screen, asPage.Board Margins.all (Seat.at 1) (gliding 4)
      "board with the notes off", Page.Screen, asPage.Board Margins.none (Seat.at 1) (gliding 4)
      "empty board", Page.Screen, asPage.Board Margins.all (Seat.at 1) died
      "waiting", Page.Screen, asPage.Waiting arriving
      "a line the game said", Page.Told, asPage.Says "f7 comes alive."
      "the record", Page.Told, asPage.History (Seat.at 1) walked
      "an answer", Page.Told, asPage.Answer (Seat.at 1) "h9" (gliding 4)
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


let private lifeIsTeal =
    Palette.set Ink.Key "teal" standard |> Result.toOption |> Option.get

report
    "the page is drawn in the colours it is given"
    true
    (Page.page life.Page lifeIsTeal
     |> mentions (Palette.paint (Palette.shadeOf Ink.Key lifeIsTeal)))

report "the game's own stylesheet reaches the page" true (page |> mentions "line-height: 1.15")

let private posted (markup: string) =
    Regex.Matches(WebUtility.HtmlDecode markup, @"@post\('/say\?line=([^']*)'\)")
    |> Seq.map (fun found -> Uri.UnescapeDataString found.Groups[1].Value)
    |> List.ofSeq

let private buttons = posted (asPage.Board Margins.all (Seat.at 1) (gliding 4))

report
    "the board offers the things a person does over and over, the clock first"
    [ "stop"; "step"; "step 10"; "slower"; "faster"; "undo"; "clear"; "restart" ]
    buttons

report
    "and every one of them types a line the program takes"
    []
    (buttons
     |> List.filter (fun line ->
         match reads line with
         | Ok "nothing"
         | Error _ -> true
         | Ok _ -> false))


let rec private controls scene =
    match scene with
    | Does(caption, line, _) -> [ (caption, line) ]
    | Block(_, body)
    | Stack body
    | Beside body
    | Tile(_, _, body) -> body |> List.collect controls
    | Walled(_, rows) -> rows |> List.collect (fun row -> row.Cells |> List.collect controls)
    | _ -> []

let private described = controls (Render.board Margins.all (Seat.at 1) (gliding 4))

report "every control types the line it is captioned with" [] (described |> List.filter (fun (caption, line) -> caption <> line))

report "and the page's buttons are exactly the controls the game described" (described |> List.map snd) buttons

report
    "and the terminal is told to type the very same lines"
    true
    (described
     |> List.forall (fun (caption, _) -> plain.Board Margins.all (Seat.at 1) (gliding 4) |> mentions caption))


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
    [ Render.Notes.board; Render.Notes.rule ]
    (notes (Render.board Margins.all (Seat.at 1) (gliding 4)))

report "and not one of them survives turning them off" [] (notes (Render.board Margins.none (Seat.at 1) (gliding 4)))

// --- the clock ------------------------------------------------------------------------------
//
// The rule runs on its own now, which is a table keeping time and a game answering a beat. None
// of what follows needs a clock: a beat is a move, and every one of these asks for it by hand.

let private pulse = life.Pulse |> Option.get

let private beating moves model =
    moves
    |> List.fold (fun model move -> Update.update rules (Make move) model) model

report "a fresh soup is dealt running" true (standing dealt).Running

report "at a notch in the middle of the range" 5 (standing dealt).Speed

report "which is about three generations a second" true (pulse.Every(standing dealt) < TimeSpan.FromMilliseconds 400.0)

report "a beat is one generation" 1 (rules.Turn(standing (beating [ Beat ] dealt)))

report "and says nothing, because the board is already saying it" [] (toldBy (beating [ Beat ] dealt))

// The whole point of the toggle: a board nobody has started costs nothing at all, however long
// the clock goes on beating over it.

let private stopped = beating [ Running None ] dealt

report "'run' turns it the other way" false (standing stopped).Running

report "and says so" true (toldBy stopped |> List.exists (mentions "Stopped at generation"))

report "a beat at a stopped board does nothing" (standing stopped) (standing (beating (List.replicate 20 Beat) stopped))

report
    "and writes no record at all, twenty beats or none"
    (Journal.length stopped.Journal)
    (Journal.length (beating (List.replicate 20 Beat) stopped).Journal)

report "nor says anything" (toldBy stopped) (toldBy (beating (List.replicate 20 Beat) stopped))

report "and 'run' again starts it" true (standing (beating [ Running None ] stopped)).Running

report
    "'stop' says which outright, and twice over is still stopped"
    false
    (standing (beating [ Running(Some false); Running(Some false) ] dealt)).Running

report "'start' likewise" true (standing (beating [ Running(Some false); Running(Some true) ] dealt)).Running

// A board with nothing left to do is beaten just the same and answers the same way, so a settled
// board is not a clock filling a record with refusals.

let private stillLife =
    played
        [ Clear
          Toggle(at "j10")
          Toggle(at "j11")
          Toggle(at "k10")
          Toggle(at "k11") ]

report
    "a settled board takes a beat and does nothing with it"
    (standing stillLife)
    (standing (beating (List.replicate 10 Beat) stillLife))

report "and an empty one the same" (standing died) (standing (beating (List.replicate 10 Beat) died))

// Winding it, which is the same ladder Snake has and a slower one.

report "winding it up shortens the beat" true (pulse.Every(standing (beating [ Faster ] dealt)) < pulse.Every(standing dealt))

report "and down lengthens it" true (pulse.Every(standing (beating [ Slower ] dealt)) > pulse.Every(standing dealt))

report "a notch can be asked for outright" 9 (standing (beating [ Speed 9 ] dealt)).Speed

report
    "the quickest is about nine a second"
    true
    (pulse.Every(standing (beating [ Speed 9 ] dealt)) < TimeSpan.FromMilliseconds 120.0)

report "and the slowest about two" true (pulse.Every(standing (beating [ Speed 1 ] dealt)) > TimeSpan.FromMilliseconds 500.0)

report
    "a speed nobody has is refused, and says what there is"
    true
    (toldBy (beating [ Speed 12 ] dealt)
     |> List.exists (mentions "The clock winds from 1 to 9"))

report
    "winding past the end of the range does nothing"
    (standing (beating [ Speed 9 ] dealt))
    (standing (beating [ Speed 9; Faster ] dealt))

// The keys, held to the rule every control here is held to: a key stands for a line the game
// itself reads.

let private keyed key =
    pulse.Pressed(ConsoleKeyInfo(' ', key, false, false, false))

report "'p' starts and stops it" (Some "run") (keyed ConsoleKey.P)

report "a full stop steps it once" (Some "step") (keyed ConsoleKey.OemPeriod)

report "and a key this game has no use for is left to the table" None (keyed ConsoleKey.F7)

report
    "every key there is types a line this game reads"
    []
    ([ ConsoleKey.P
       ConsoleKey.OemPeriod
       ConsoleKey.C
       ConsoleKey.OemPlus
       ConsoleKey.OemMinus ]
     |> List.choose keyed
     |> List.filter (fun line -> Result.isError (reads line)))

report
    "and so does every key the page sends"
    []
    (life.Page.Keys
     |> List.map snd
     |> List.filter (fun line -> Result.isError (reads line)))

report
    "space is one of them, because a page has it going spare"
    true
    (life.Page.Keys |> List.exists (fun (key, line) -> key = " " && line = "run"))

// And it is still a fold: a record of beats replays to the board it was saved from, with no
// clock anywhere near it.

let private clocked =
    beating [ Beat; Beat; Running None; Toggle(at "c3"); Running None; Beat ] dealt

let private clockedRecord = Transcript.write life [ Here ] clocked.Journal

report
    "a record of a running board is written in beats and runs"
    true
    (clockedRecord |> mentions "beat" && clockedRecord |> mentions "run")

report
    "and replays to the same board"
    (standing clocked)
    (let readBack = Transcript.read life clockedRecord |> Result.toOption |> Option.get

     standing (
         Update.replay rules readBack.Players readBack.Seed readBack.Moves
         |> Result.toOption
         |> Option.get
     ))


// === The seam every game fills in ===

Conforms.against life 1 [ "a1"; "b2"; "step"; "run"; "clear"; "faster" ]

finish ()
