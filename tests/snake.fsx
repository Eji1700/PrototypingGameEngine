// The sixth game: the arcade one, a square at a time.
//
// Half of this is the ordinary thing: a snake moves, grows, and dies of three things, and the
// rules should say so. The other half is the two seams again - and this game leans on parts of
// them the others do not. It is the first whose table is any size from one to four with the
// same rules either way, so "how many are playing" is really the game's answer and not two of
// somebody else's; it is the first whose generator is used all through a game rather than only
// at the deal, so undo has to take a draw back with the move; and its machine is the first here
// that cannot see the end of the game and has to judge a position instead.
//
//   dotnet fsi tests/snake.fsx

#load "Slither.fsx"

open System
open System.Net
open System.Text.RegularExpressions
open System.Xml
open TCModel.Engine
open TCModel.Table
open TCModel.Snake
open Checks
open Slither

let private rules = snake.Rules

let private mentions (needle: string) (text: string) = text.Contains needle

let private seat n = Seat.at n

/// A game of this many, from a fixed seed. Every check below starts from one of these, so
/// nothing here depends on a clock or on which order the checks are run in.
let private dealt players =
    Update.start rules players 0UL |> Result.toOption |> Option.get

let private standing model = Model.state model

let private play model = Session.play (standing model)

let private played moves model =
    moves
    |> List.fold (fun model move -> Update.update rules (Make move) model) model

let private snakeOf n model = Session.snakeAt (seat n) (play model)

/// The board with the food put somewhere on purpose. The state is a record of plain values, so
/// a check can say "the food is *there*" instead of walking a snake across the board hoping to
/// meet it - and where the next one lands is still the game's own business.
let private feeding cell model =
    let put session =
        match session with
        | InPlay play -> InPlay { play with Food = Some cell }
        | Finished(play, over) -> Finished({ play with Food = Some cell }, over)

    { model with
        Timeline = Timeline.ofDeal (put (standing model)) }

// --- what the game says is wrong with itself ---------------------------------------------
//
// Where a snake starts is worked out from its seat and the size of the table rather than
// written down, so this is the check that the arithmetic came out - at every table there is.

report "the board hangs together, at every size of table" [] snake.Faults

report "a table of one to four" (1, 4) (snake.Fewest, snake.Most)

report
    "and a table of five is refused, in words a person can read"
    true
    (match rules.Deal 5 0UL with
     | Error problem -> problem |> mentions "Snake takes 1 to 4"
     | Ok _ -> false)

report "a table of none is refused too" true (Result.isError (rules.Deal 0 0UL))

// --- the board ------------------------------------------------------------------------------
//
// The edges are walls, which is the whole difference between this board and Life's. A step is
// allowed to leave it - that is what makes the wall hittable - and `holds` is what says so.

report "a step east is one column across" { Row = 3; Column = 5 } (Board.along East { Row = 3; Column = 4 })

report "and a step north is one row up" { Row = 2; Column = 4 } (Board.along North { Row = 3; Column = 4 })

report "the board does not join at the edges" false (Board.holds (Board.along West { Row = 1; Column = 1 }))

report
    "every direction has an opposite, and nothing is its own"
    []
    (Direction.all |> List.filter (fun way -> Direction.opposite way = way))

// --- a turn -------------------------------------------------------------------------------

let private solo = dealt 1

report "one snake is dealt for one" 1 (List.length (play solo).Seats)

report "three segments long" 3 (Snake.length (snakeOf 1 solo))

report "facing east" East (snakeOf 1 solo).Facing

report "and there is food on the board" true ((play solo).Food |> Option.forall Board.holds)

let private stepped = played [ Go East ] solo

report "a step moves the head" (Board.along East (Snake.head (snakeOf 1 solo))) (Snake.head (snakeOf 1 stepped))

report "and the tail follows, so the snake is the same length" 3 (Snake.length (snakeOf 1 stepped))

report "which is turn two" 2 (rules.Turn(standing stepped))

report "'go' is the way it is already facing" (Snake.head (snakeOf 1 stepped)) (Snake.head (snakeOf 1 (played [ Onward ] solo)))

report "and turning is turning" South (snakeOf 1 (played [ Go South ] solo)).Facing

// --- and what it will not take --------------------------------------------------------------

/// Everything the rules said about a move, whether or not it carried.
let private toldBy model =
    model.Log |> List.rev |> List.map (Playable.told snake)

let private turnedBack = played [ Go West ] solo

report
    "a snake cannot turn back into its own neck"
    true
    (toldBy turnedBack |> List.exists (mentions "cannot turn back into its own neck"))

report "and the game does not move" (standing solo) (standing turnedBack)

report "but a refused move is written down all the same" 1 (Journal.length turnedBack.Journal)

// --- eating -----------------------------------------------------------------------------------
//
// A snake grows by keeping its tail rather than by gaining a head, which is why eating and
// lengthening are one step apart. Both steps are checked, because a game that added the segment
// at once would look right in the log and wrong on the board.

let private hungry = feeding (Board.along East (Snake.head (snakeOf 1 solo))) solo

let private ate = played [ Go East ] hungry

report "eating is counted at once" 1 (snakeOf 1 ate).Eaten

report "and says so" true (toldBy ate |> List.exists (mentions "Snake A eats"))

report "the segment arrives on the next step, not this one" 3 (Snake.length (snakeOf 1 ate))

report "and then it is there" 4 (Snake.length (snakeOf 1 (played [ Go East ] ate)))

report "and stays there" 4 (Snake.length (snakeOf 1 (played [ Go East; Go East ] ate)))

report
    "the next piece of food is on the board and somewhere else"
    true
    ((play ate).Food
     |> Option.forall (fun cell -> Board.holds cell && cell <> Snake.head (snakeOf 1 ate)))

// The generator is in the state and moves with every piece eaten, which is what makes this game
// replayable at all: where the next piece lands is part of the position rather than a draw made
// on the side.

report
    "the same board eaten from twice puts the next piece in the same place"
    (play ate).Food
    (play (played [ Go East ] hungry)).Food

report "and taking the move back takes the draw back with it" (play hungry).Food (play (Update.update rules Undo ate)).Food

// --- the three ways it stops -------------------------------------------------------------------

/// A board with one snake laid out by hand, head first. What a snake *is* is a public record of
/// plain values, so a check can put one where it needs it rather than driving it there.
let private laid body facing model =
    let put session =
        match session with
        | InPlay play ->
            InPlay
                { play with
                    Snakes =
                        play.Snakes
                        |> Map.add
                            (seat 1)
                            { Session.snakeAt (seat 1) play with
                                Body = body
                                Facing = facing } }
        | finished -> finished

    { model with
        Timeline = Timeline.ofDeal (put (standing model)) }

let private at row column = { Row = row; Column = column }

let private wall = played (List.replicate 7 (Go North)) solo

report "a snake that runs into the wall stops" (Some HitWall) (snakeOf 1 wall).Fate

report "and says so" true (toldBy wall |> List.exists (mentions "ran into the wall"))

// A snake long enough to meet itself, coiled so that one square of it is neither the neck nor
// the tail - which is the only kind of square a snake can actually bite.
//
//   . a a .      six segments: the head at the bottom left of the coil, facing west, with the
//   . a . .      middle of its own body one square north of it
//   A a . .
//
let private coiled =
    laid [ at 6 5; at 6 6; at 5 6; at 5 5; at 4 5; at 4 6 ] West solo

let private bitten = played [ Go North ] coiled

report "a snake that runs into itself stops" (Some HitItself) (snakeOf 1 bitten).Fate

// The one place the rule is subtle: the square your own tail is standing on this moment is a
// square you may move into, because the tail leaves as the head arrives. A game that refused it
// would kill every snake that turned tightly, and one that allowed it while growing would let a
// snake eat itself.

let private following = laid [ at 5 5; at 5 6; at 6 6; at 6 5 ] West solo

report "a snake may move into the square its own tail is leaving" None (snakeOf 1 (played [ Go South ] following)).Fate

report
    "but not while it is growing, because the tail is staying where it is"
    (Some HitItself)
    (snakeOf 1 (played [ Go South ] (feeding (at 6 5) following))).Fate

// --- a table of more than one -------------------------------------------------------------------

let private four = dealt 4

report "four snakes are dealt for four" 4 (List.length (play four).Seats)

report
    "on squares nobody shares"
    true
    (let bodies =
        Session.snakes (play four) |> List.collect (fun (_, snake) -> snake.Body) in

     List.distinct bodies = bodies)

report "the first seat plays first" (seat 1) (rules.Active(standing four))

report "then the second" (seat 2) (rules.Active(standing (played [ Go East ] four)))

report "and the turn goes up when the round comes back round" 2 (rules.Turn(standing (played (List.replicate 4 Onward) four)))

// A snake that has stopped is skipped from that moment on, and is still lying there - which is
// most of what makes a table of four different from four tables of one.

let private resigned = played [ Resign ] four

report "a seat that gives up is passed over" (seat 2) (rules.Active(standing resigned))

report "and comes round to the living ones only" (seat 3) (rules.Active(standing (played [ Onward ] resigned)))

report "what is left of it is still on the board" 3 (Snake.length (snakeOf 1 resigned))

report
    "and is something to run into"
    true
    (let wreck = Snake.head (snakeOf 1 resigned)
     let hunting = laid [ Board.along West wreck ] West resigned
     // Seat one is the one laid out; it has stopped, so this only asks what `ahead` says of that
     // square rather than playing it.
     match Turn.ahead (seat 2) East (play resigned) with
     | Into _
     | Wall
     | Food
     | Clear -> Board.holds wreck && Snake.length (snakeOf 1 hunting) = 1)

// --- how it ends ---------------------------------------------------------------------------------

report "at a table of one, the game is over when the snake stops" true (rules.Over(standing wall))

report "and the ending is that nothing is left moving" (Some NobodyMoving) (Session.ending (standing wall))

report
    "which the screen says with the score, because a game of one ends with one"
    true
    (Render.heading (seat 1) (standing wall) |> mentions "ran into the wall")

let private two = dealt 2

let private lastOne = played [ Resign ] two

report
    "at a table of two, one giving up leaves the other the last one moving"
    (Some(LastMoving(seat 2)))
    (Session.ending (standing lastOne))

report "and the game is over" true (rules.Over(standing lastOne))

report
    "a move asked for after it is over is answered by the engine, not by the game"
    true
    (toldBy (played [ Go North ] lastOne)
     |> List.exists (mentions "The game is over, so there is nothing left to play"))

// --- everything the machinery brings with it ------------------------------------------------

let private walked = played [ Go East; Go North; Go East ] solo

report "a move can be taken back" (standing (played [ Go East; Go North ] solo)) (standing (Update.update rules Undo walked))

report "and made again" (standing walked) (standing (walked |> Update.update rules Undo |> Update.update rules Redo))

report "and there is nothing to take back at the deal" (standing solo) (standing (Update.update rules Undo solo))

// The record, which is written in the words the prompt takes and read back by the same parser.

let private record =
    Transcript.write snake [ Here; Machine "hard" ] (played [ Go East; Go South ] two).Journal

report "a record says how the game was dealt, and who was at it" true (record |> mentions "deal 2 0 you hard")

report "and names the game it is a record of" true (record |> mentions snake.Title)

let private readBack = Transcript.read snake record |> Result.toOption |> Option.get

report "every move comes back off it" [ Make(Go East); Make(Go South) ] readBack.Moves

report
    "and playing them again arrives at the same board"
    (standing (played [ Go East; Go South ] two))
    (standing (
        Update.replay rules readBack.Players readBack.Seed readBack.Moves
        |> Result.toOption
        |> Option.get
    ))

// The words every game knows, read once for all of them - so this game's own reader never sees
// `undo`, and could not redefine it if it tried.

let private reads typed =
    match Playable.read snake typed with
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

report "'resign' plays what this game says stopping is" (Ok "resign") (reads "resign")

for word in [ "north"; "n"; "up" ] do
    report $"'{word}' is north" (Ok "north") (reads word)

for word in [ "east"; "e"; "right" ] do
    report $"'{word}' is east" (Ok "east") (reads word)

report "'go' is straight on" (Ok "go") (reads "go")

report "and a record is written in the compass whichever was typed" (reads "north") (reads "up")

// The one key everybody's fingers want and this game will not take, because 'w' is west here and
// up on a keyboard. It is refused with the four words rather than quietly read as one of them.

report
    "'a' is not a direction, and says what the directions are"
    true
    (match reads "a" with
     | Error problem -> problem |> mentions "'north', 'east', 'south', 'west'"
     | Ok _ -> false)

report "and 'w' is west, as the compass says" (Ok "west") (reads "w")

report "a table of five is refused by the shared verbs too" (Error "5 players? The game takes 1 to 4.") (reads "players 5")

// --- the screen ------------------------------------------------------------------------------

let private view = Playable.plainest AtATerminal standard snake

let private board = view.Board Margins.all (seat 1) (played [ Go East ] two)

report "the board is drawn with the walls round it" true (board |> mentions ("+" + String.replicate Board.Width "-" + "+"))

report "with a head in capitals and a body in small" true (board |> mentions "aaA")

report "and the other snake on it too" true (board |> mentions "Bbb")

report "it says whose turn it is" true (board |> mentions "Snake B" && board |> mentions "to play")

report "and marks the seat belonging to whoever is reading" true (board |> mentions "Snake A (you)")

report "the notes can be turned off" false (view.Board Margins.none (seat 1) two |> mentions Render.Notes.moving)

report "what the game said is on the screen" true (board |> mentions "Snake A goes east.")

report "the record reads back through the view too" true (view.History (seat 1) walked |> mentions "Snake A: east")

// The one thing worth asking at this game, and the one thing the board cannot quite show: what
// the *rules* will make of the square you are looking at.

report "a square can be asked about" true (view.Answer (seat 1) "north" solo |> mentions "open board")

report
    "and the wall is named as what it is"
    true
    (view.Answer (seat 1) "north" (played (List.replicate 6 (Go North)) solo)
     |> mentions "the wall")

report "and the food when it is there" true (view.Answer (seat 1) "east" hungry |> mentions "the food")

report
    "a way nobody could look is answered rather than ignored"
    true
    (view.Answer (seat 1) "sideways" solo |> mentions "is not a way to look")

// --- all three ways of drawing it ---------------------------------------------------------

let private views = snake.Views standard

/// What a person would actually see, whatever a view wrote it in: colour taken back off - the
/// escape with the code that follows it, because this board is a picture made of characters -
/// and markup with it.
let private seen text =
    let uncoloured = Regex.Replace(text, string (char 27) + @"?\[[0-9;]*m", "")
    Regex.Replace(uncoloured, "<[^>]*>", "")

report "there are three of them" [ "plain"; "rich"; "html" ] (views |> List.map (fun view -> view.Name))

let private arriving =
    [ { Player = seat 1
        Expected = false
        Away = false
        Yours = true }
      { Player = seat 2
        Expected = true
        Away = false
        Yours = false } ]

for view in views do
    let drawn = seen (view.Board Margins.all (seat 1) (played [ Go East ] two))

    report $"the {view.Name} view draws both snakes, head and all" true (drawn |> mentions "aaA" && drawn |> mentions "Bbb")

    report $"the {view.Name} view draws the walls" true (drawn |> mentions "+---")

    for block in
        [ Render.Blocks.board
          Render.Blocks.snakes
          Render.Blocks.onwards
          Render.Blocks.log ] do
        report
            $"the {view.Name} view has a block for {block}"
            true
            (drawn.ToLowerInvariant() |> mentions (block.ToLowerInvariant()))

    report $"the {view.Name} view shows what the game has said" true (drawn |> mentions "goes east")

    report
        $"and the {view.Name} view's notes can be turned off"
        false
        (seen (view.Board Margins.none (seat 1) two) |> mentions Render.Notes.moving)

    report
        $"the {view.Name} view answers a table still filling up"
        true
        (seen (view.Waiting arriving) |> mentions Scene.Filling.title)

// --- the page ---------------------------------------------------------------------------------

let private page = Page.page snake.Page standard

let private fragments =
    [ "board", Page.Screen, asPage.Board Margins.all (seat 1) (played [ Go East ] two)
      "board with the notes off", Page.Screen, asPage.Board Margins.none (seat 1) two
      "a board that has ended", Page.Screen, asPage.Board Margins.all (seat 1) wall
      "waiting", Page.Screen, asPage.Waiting arriving
      "a line the game said", Page.Told, asPage.Says "Snake A goes east."
      "the record", Page.Told, asPage.History (seat 1) walked
      "an answer", Page.Told, asPage.Answer (seat 1) "north" solo
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

let private inTeal =
    Palette.set "a" "teal" standard |> Result.toOption |> Option.get

report
    "the page is drawn in the colours it is given"
    true
    (Page.page snake.Page inTeal
     |> mentions (Palette.paint (Palette.shadeOf "a" inTeal)))

report "the game's own stylesheet reaches the page" true (page |> mentions "line-height: 1.15")

/// What a control on the page would send.
let private posted (markup: string) =
    Regex.Matches(WebUtility.HtmlDecode markup, @"@post\('/say\?line=([^']*)'\)")
    |> Seq.map (fun found -> Uri.UnescapeDataString found.Groups[1].Value)
    |> List.ofSeq

let private buttons = posted (asPage.Board Margins.all (seat 1) two)

report "the board offers a button for each way to go" [ "north"; "west"; "east"; "south"; "go" ] buttons

report
    "and every one of them types a line the program takes"
    []
    (buttons
     |> List.filter (fun line ->
         match reads line with
         | Ok "nothing"
         | Error _ -> true
         | Ok _ -> false))

// --- one description, three readers ---------------------------------------------------------

let rec private controls scene =
    match scene with
    | Does(caption, line, _) -> [ (caption, line) ]
    | Block(_, body)
    | Stack body
    | Beside body
    | Tile(_, _, body) -> body |> List.collect controls
    | Walled(_, rows) -> rows |> List.collect (fun row -> row.Cells |> List.collect controls)
    | _ -> []

let private described = controls (Render.board Margins.all (seat 1) two)

report "every control types the line it is captioned with" [] (described |> List.filter (fun (caption, line) -> caption <> line))

report "and the page's buttons are exactly the controls the game described" (described |> List.map snd) buttons

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
    [ Render.Notes.board; Render.Notes.moving ]
    (notes (Render.board Margins.all (seat 1) two))

report "and not one of them survives turning them off" [] (notes (Render.board Margins.none (seat 1) two))

// --- the seat the program plays --------------------------------------------------------------
//
// A machine here is the first in this program that cannot see the end of the game. Noughts and
// crosses is small enough to solve outright; a snake on an open board has no end to walk to, so
// what `hard` promises is not "cannot be beaten" but "does not shut itself in" - and that is a
// thing that can be measured: it is how long it lives and how much it eats.

report "three machines are offered, worst to best" [ "easy"; "medium"; "hard" ] (snake.Skills |> List.map fst)

/// A whole game played out by machines, the way any table plays one: the engine asks, they
/// answer, and nothing in the asking knows how they chose.
let private playedOut skills seed =
    let start =
        Update.start rules (List.length skills) seed |> Result.toOption |> Option.get

    let seated = snake.Seating seed (skills |> List.map Some) (standing start)
    Machines.answering rules Playable.plays seated start |> fst

let private alone skill seed = playedOut [ skill ] seed

let private lived model =
    Model.state model
    |> Session.play
    |> Session.snakes
    |> List.sumBy (fun (_, snake) -> snake.Eaten)

// Fixed seeds throughout: there is nothing to be flaky here, and a run that came out differently
// would mean the machine had changed rather than the dice. Played once and kept, rather than a
// function called four times over: a careful machine plays a long game, and there is no sense in
// playing the same six of them again to ask a second question about them.
let private runs skill =
    [ 1UL .. 6UL ] |> List.map (alone skill)

let private careful = runs "hard"

let private hasty = runs "easy"

report "a table of machines plays the game out to its end" true (careful |> List.forall (fun model -> rules.Over(standing model)))

report
    "and nothing any of them asked for over a run of games was refused"
    []
    (careful
     |> List.collect (fun model -> Journal.entries model.Journal)
     |> List.collect (fun entry -> entry.Told)
     |> List.choose (function
         | Said(Refused refusal) -> Some(Words.rejection refusal)
         | _ -> None))

// The word `hard` is a promise to a player, so it owes them something. What it can honestly
// promise at this game is that counting the room a step leaves you in is worth more than eating.

let private eatenBy = List.sumBy lived

report "counting the room it leaves beats going straight for the food" true (eatenBy careful > eatenBy hasty)

report "and beats it by a good deal rather than by a nose" true (eatenBy careful > 3 * eatenBy hasty)

// And it is a fold, like everything else here: the generator travels inside the machine the way
// the game's own travels inside the game.

report
    "the same machines at the same deal play the same game twice"
    (Journal.moves (playedOut [ "hard"; "easy" ] 7UL).Journal)
    (Journal.moves (playedOut [ "hard"; "easy" ] 7UL).Journal)

report
    "and a different seed is a different game"
    false
    (Journal.moves (playedOut [ "easy"; "easy" ] 7UL).Journal = Journal.moves (playedOut [ "easy"; "easy" ] 8UL).Journal)

finish ()
