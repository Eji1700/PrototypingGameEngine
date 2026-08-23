#load "Slither.fsx"
#load "Conforms.fsx"

open System
open System.Net
open System.Text.RegularExpressions
open System.Xml
open TCModel.Engine
open TCModel.Table
open TCModel.Snake
open Checks
open Slither

let private mentions (needle: string) (text: string) = text.Contains needle

let private seat n = Seat.at n

let private at row column = { Row = row; Column = column }

let private turning = turns.Rules

let private racing = snake.Rules

let private dealt rules players =
    Update.start rules players 0UL |> Result.toOption |> Option.get

let private standing model = Model.state model

let private play model = Session.play (standing model)

let private played rules moves model =
    moves
    |> List.fold (fun model move -> Update.update rules (Make move) model) model

let private snakeOf n model = Session.snakeAt (seat n) (play model)

let private headOf n model = Snake.head (snakeOf n model)

let private toldBy game model =
    model.Log |> List.rev |> List.map (Playable.told game)

let private feeding cell model =
    let put session =
        match session with
        | InPlay play -> InPlay { play with Food = Some cell }
        | Finished(play, over) -> Finished({ play with Food = Some cell }, over)

    { model with
        Timeline = Timeline.ofDeal (put (standing model)) }

let private laid bodies model =
    let put session =
        match session with
        | InPlay play ->
            InPlay
                { play with
                    Snakes =
                        bodies
                        |> List.fold
                            (fun snakes (place, body, facing) ->
                                snakes
                                |> Map.add
                                    (seat place)
                                    { Session.snakeAt (seat place) play with
                                        Body = body
                                        Facing = facing })
                            play.Snakes }
        | finished -> finished

    { model with
        Timeline = Timeline.ofDeal (put (standing model)) }


report "the board hangs together, at every size of table" [] snake.Faults

report "and says the same of both ways of playing it" turns.Faults snake.Faults

report "a table of one to four" (1, 4) (snake.Fewest, snake.Most)

report
    "and a table of five is refused, in words a person can read"
    true
    (match racing.Deal 5 0UL with
     | Error problem -> problem |> mentions "Snake takes 1 to 4"
     | Ok _ -> false)

report "a table of none is refused too" true (Result.isError (racing.Deal 0 0UL))


report "a step east is one column across" (at 3 5) (Board.along East (at 3 4))

report "and a step north is one row up" (at 2 4) (Board.along North (at 3 4))

report "the board does not join at the edges" false (Board.holds (Board.along West (at 1 1)))

report
    "every direction has an opposite, and nothing is its own"
    []
    (Direction.all |> List.filter (fun way -> Direction.opposite way = way))


let private solo = dealt turning 1

report "one snake is dealt for one" 1 (List.length (play solo).Seats)

report "three segments long" 3 (Snake.length (snakeOf 1 solo))

report "facing east" East (snakeOf 1 solo).Facing

report "and there is food on the board" true ((play solo).Food |> Option.forall Board.holds)

let private stepped = played turning [ Go East ] solo

report "a step moves the head" (Board.along East (headOf 1 solo)) (headOf 1 stepped)

report "and the tail follows, so the snake is the same length" 3 (Snake.length (snakeOf 1 stepped))

report "which is turn two" 2 (turning.Turn(standing stepped))

report "'go' is the way it is already facing" (headOf 1 stepped) (headOf 1 (played turning [ Onward ] solo))

report "and turning is turning" South (snakeOf 1 (played turning [ Go South ] solo)).Facing

let private turnedBack = played turning [ Go West ] solo

report
    "a snake cannot turn back into its own neck"
    true
    (toldBy turns turnedBack
     |> List.exists (mentions "cannot turn back into its own neck"))

report "and the game does not move" (standing solo) (standing turnedBack)

report "but a refused move is written down all the same" 1 (Journal.length turnedBack.Journal)


let private hungry = feeding (Board.along East (headOf 1 solo)) solo

let private ate = played turning [ Go East ] hungry

report "eating is counted at once" 1 (snakeOf 1 ate).Eaten

report "and says so" true (toldBy turns ate |> List.exists (mentions "Snake A eats"))

report "the segment arrives on the next step, not this one" 3 (Snake.length (snakeOf 1 ate))

report "and then it is there" 4 (Snake.length (snakeOf 1 (played turning [ Go East ] ate)))

report "and stays there" 4 (Snake.length (snakeOf 1 (played turning [ Go East; Go East ] ate)))

report
    "the next piece of food is on the board and somewhere else"
    true
    ((play ate).Food
     |> Option.forall (fun cell -> Board.holds cell && cell <> headOf 1 ate))


report
    "the same board eaten from twice puts the next piece in the same place"
    (play ate).Food
    (play (played turning [ Go East ] hungry)).Food

report "and taking the move back takes the draw back with it" (play hungry).Food (play (Update.update turning Undo ate)).Food


let private wall = played turning (List.replicate 7 (Go North)) solo

report "a snake that runs into the wall stops" (Some HitWall) (snakeOf 1 wall).Fate

report "and says so" true (toldBy turns wall |> List.exists (mentions "ran into the wall"))

let private coiled =
    laid [ (1, [ at 6 5; at 6 6; at 5 6; at 5 5; at 4 5; at 4 6 ], West) ] solo

report "a snake that runs into itself stops" (Some HitItself) (snakeOf 1 (played turning [ Go North ] coiled)).Fate


let private following = laid [ (1, [ at 5 5; at 5 6; at 6 6; at 6 5 ], West) ] solo

report "a snake may move into the square its own tail is leaving" None (snakeOf 1 (played turning [ Go South ] following)).Fate

report
    "but not while it is growing, because the tail is staying where it is"
    (Some HitItself)
    (snakeOf 1 (played turning [ Go South ] (feeding (at 6 5) following))).Fate


let private four = dealt turning 4

report "four snakes are dealt for four" 4 (List.length (play four).Seats)

report
    "on squares nobody shares"
    true
    (let bodies =
        Session.snakes (play four) |> List.collect (fun (_, snake) -> snake.Body) in

     List.distinct bodies = bodies)

report "the first seat plays first" (seat 1) (turning.Active(standing four))

report "then the second" (seat 2) (turning.Active(standing (played turning [ Go East ] four)))

report
    "and the turn goes up when the round comes back round"
    2
    (turning.Turn(standing (played turning (List.replicate 4 Onward) four)))

let private resigned = played turning [ Resign ] four

report "a seat that gives up is passed over" (seat 2) (turning.Active(standing resigned))

report "and comes round to the living ones only" (seat 3) (turning.Active(standing (played turning [ Onward ] resigned)))

report "what is left of it is still on the board" 3 (Snake.length (snakeOf 1 resigned))


report "at a table of one, the game is over when the snake stops" true (turning.Over(standing wall))

report "and the ending is that nothing is left moving" (Some NobodyMoving) (Session.ending (standing wall))

report
    "which the screen says with the score, because a game of one ends with one"
    true
    (Render.heading (seat 1) (standing wall) |> mentions "ran into the wall")

let private two = dealt turning 2

let private lastOne = played turning [ Resign ] two

report
    "at a table of two, one giving up leaves the other the last one moving"
    (Some(LastMoving(seat 2)))
    (Session.ending (standing lastOne))

report "and the game is over" true (turning.Over(standing lastOne))

report
    "a move asked for after it is over is answered by the engine, not by the game"
    true
    (toldBy turns (played turning [ Go North ] lastOne)
     |> List.exists (mentions "The game is over, so there is nothing left to play"))


let private ticking = dealt racing 1

let private ticked = played racing [ Beat ] ticking

report "a beat moves the snake a square" (Board.along East (headOf 1 ticking)) (headOf 1 ticked)

report "and counts as a beat rather than a turn taken" 2 (racing.Turn(standing ticked))

report "steering turns the head" North (snakeOf 1 (played racing [ Steer(seat 1, North) ] ticking)).Facing

report
    "and does not move it, which is the whole difference between the two paces"
    (headOf 1 ticking)
    (headOf 1 (played racing [ Steer(seat 1, North) ] ticking))

report
    "so a steer and a beat come to a step that way"
    (Board.along North (headOf 1 ticking))
    (headOf 1 (played racing [ Steer(seat 1, North); Beat ] ticking))


let private leaning =
    played racing [ Steer(seat 1, East); Steer(seat 1, East) ] ticking

report "steering where it already points changes nothing" (standing ticking) (standing leaning)

report "and says nothing" [] (toldBy snake leaning)

report
    "and leaves the history where it was"
    0
    (Timeline.movesMade (
        standing leaning |> ignore
        leaning.Timeline
    ))

report
    "turning back is refused here too"
    true
    (toldBy snake (played racing [ Steer(seat 1, West) ] ticking)
     |> List.exists (mentions "cannot turn back into its own neck"))

report
    "a snake nobody is at is refused"
    true
    (toldBy snake (played racing [ Steer(seat 3, North) ] ticking)
     |> List.exists (mentions "There is no Snake C"))


let private turnedTwice =
    played racing [ Steer(seat 1, North); Steer(seat 1, West) ] ticking

report
    "the second of two quick turns cannot point the head at its own neck"
    true
    (toldBy snake turnedTwice |> List.exists (mentions "where this one's neck is"))

report "so the beat after two quick turns finds the snake alive" None (snakeOf 1 (played racing [ Beat ] turnedTwice)).Fate

report "and going the way it was pointed" (Board.along North (headOf 1 ticking)) (headOf 1 (played racing [ Beat ] turnedTwice))

report
    "two quick turns that are not into the neck are both taken"
    South
    (snakeOf 1 (played racing [ Steer(seat 1, North); Steer(seat 1, South) ] ticking)).Facing

report
    "and the same pair a beat apart, where the neck has moved, is fine either way"
    None
    (snakeOf 1 (played racing [ Steer(seat 1, North); Beat; Steer(seat 1, West); Beat ] ticking)).Fate


let private pair = dealt racing 2

let private beaten n model =
    played racing (List.replicate n Beat) model

report "one beat moves every snake" 2 (Session.living (play (beaten 1 pair)) |> List.length)

report
    "each of them by a square"
    [ Board.along East (headOf 1 pair); Board.along West (headOf 2 pair) ]
    [ headOf 1 (beaten 1 pair); headOf 2 (beaten 1 pair) ]

let private meeting =
    laid [ (1, [ at 5 4; at 5 3; at 5 2 ], East); (2, [ at 5 6; at 5 7; at 5 8 ], West) ] pair

report
    "two heads that pick the same square both stop"
    [ Some(HitAnother(seat 2)); Some(HitAnother(seat 1)) ]
    [ (snakeOf 1 (beaten 1 meeting)).Fate; (snakeOf 2 (beaten 1 meeting)).Fate ]

report
    "and that is the end of the game, with nobody left moving"
    (Some NobodyMoving)
    (Session.ending (standing (beaten 1 meeting)))

let private chasing =
    laid [ (1, [ at 8 5; at 8 4; at 8 3 ], East); (2, [ at 8 8; at 8 7; at 8 6 ], East) ] pair

report
    "a snake may take the square another's tail is leaving on the same beat"
    [ None; None ]
    [ (snakeOf 1 (beaten 1 chasing)).Fate; (snakeOf 2 (beaten 1 chasing)).Fate ]

report
    "and they are still both there three beats later"
    [ None; None ]
    [ (snakeOf 1 (beaten 3 chasing)).Fate; (snakeOf 2 (beaten 3 chasing)).Fate ]

let private strewn =
    laid
        [ (1, [ at 1 5; at 2 5; at 3 5 ], North)
          (2, [ at 5 5; at 5 6; at 5 7 ], West) ]
        pair

report "a snake that runs into the wall stops on the beat" (Some HitWall) (snakeOf 1 (beaten 1 strewn)).Fate

report "and the one still moving wins it" (Some(LastMoving(seat 2))) (Session.ending (standing (beaten 1 strewn)))


let private feasting = feeding (Board.along East (headOf 1 ticking)) ticking

report "a beat onto the food eats it" 1 (snakeOf 1 (beaten 1 feasting)).Eaten

report "and the segment arrives the beat after" 4 (Snake.length (snakeOf 1 (beaten 2 feasting)))

report "and there is a fresh piece somewhere else" true ((play (beaten 1 feasting)).Food |> Option.forall Board.holds)


report
    "a step is refused at a table that keeps its own time"
    true
    (toldBy snake (played racing [ Go North ] ticking)
     |> List.exists (mentions "Not in this way of playing"))

report
    "and a beat is refused at a table that waits"
    true
    (toldBy turns (played turning [ Beat ] solo)
     |> List.exists (mentions "Not in this way of playing"))

report "giving up on a clock stops every snake" true (racing.Over(standing (played racing [ Resign ] pair)))

report "and at a game of turns it stops only yours" false (turning.Over(standing (played turning [ Resign ] four)))


let private pulse = snake.Pulse |> Option.get

report "the game of turns keeps no time" true turns.Pulse.IsNone

report "and the arcade one does" true snake.Pulse.IsSome

report "its beat is the move the checks above have been folding by hand" Beat pulse.Beat


let private wound moves = played racing moves ticking

let private beat model =
    (pulse.Every(standing model)).TotalMilliseconds

report "a fresh game opens in the middle of the range" 5 (play ticking).Speed

report "which is about a fifth of a second" true (beat ticking > 200.0 && beat ticking < 240.0)

report "winding it up shortens the beat" true (beat (wound [ Faster ]) < beat ticking)

report "and down lengthens it" true (beat (wound [ Slower ]) > beat ticking)

report "a notch can be asked for outright" 9 (play (wound [ Speed 9 ])).Speed

report "and the quickest is quick" true (beat (wound [ Speed 9 ]) < 100.0)

report "and the slowest is not" true (beat (wound [ Speed 1 ]) > 350.0)

report
    "a speed nobody has is refused, and says what there is"
    true
    (toldBy snake (wound [ Speed 12 ])
     |> List.exists (mentions "The clock winds from 1 to 9"))


report "asking for the speed it is already at changes nothing" (standing ticking) (standing (wound [ Speed 5 ]))

report "and says nothing" [] (toldBy snake (wound [ Speed 5 ]))

report "nor does winding past the end of the range" (standing (wound [ Speed 9 ])) (standing (wound [ Speed 9; Faster ]))

report
    "the eating quickens it too, on top of the notch"
    true
    (beat (wound [ Speed 5 ]) > beat (feeding (Board.along East (headOf 1 ticking)) (wound [ Speed 5 ]) |> beaten 1))

report "but never past the floor" true (beat (wound [ Speed 9 ]) >= 50.0)

report
    "and winding the clock is a move like any other, so it can be taken back"
    5
    (play (Update.update racing Undo (wound [ Speed 9 ]))).Speed

let private fed pieces =
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
                                Eaten = pieces } }
        | finished -> finished

    put (standing ticking)

report "and quickens as the snake eats" true (pulse.Every(fed 10) < pulse.Every(fed 0))

report
    "but not past the floor, which is as fast as anybody is still steering"
    true
    (pulse.Every(fed 500) >= TimeSpan.FromMilliseconds 50.0)


let private keyed key =
    pulse.Pressed(ConsoleKeyInfo(' ', key, false, false, false))

let private reads (game: Playable<_, _, _>) typed =
    match Playable.read game typed with
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

report "the arrows turn the first snake" (Some "a north") (keyed ConsoleKey.UpArrow)

report "wasd turns the second" (Some "b west") (keyed ConsoleKey.A)

report
    "ijkl the third, and the number pad the fourth"
    (Some "c south", Some "d east")
    (keyed ConsoleKey.K, keyed ConsoleKey.NumPad6)

report "a key the game has no use for is left to the table" None (keyed ConsoleKey.F7)

report
    "and every key there is types a line this game reads"
    []
    ([ ConsoleKey.UpArrow
       ConsoleKey.DownArrow
       ConsoleKey.LeftArrow
       ConsoleKey.RightArrow
       ConsoleKey.W
       ConsoleKey.A
       ConsoleKey.S
       ConsoleKey.D
       ConsoleKey.I
       ConsoleKey.J
       ConsoleKey.K
       ConsoleKey.L
       ConsoleKey.NumPad8
       ConsoleKey.NumPad4
       ConsoleKey.NumPad5
       ConsoleKey.NumPad6
       ConsoleKey.OemPlus
       ConsoleKey.Add
       ConsoleKey.OemMinus
       ConsoleKey.Subtract ]
     |> List.choose keyed
     |> List.filter (fun line -> Result.isError (reads snake line)))


let private sitting =
    Solo.opened snake "stamp" (dealt racing 1)
    |> Solo.watching
        "keyboard"
        { Margins = Margins.all
          Hushed = false
          View = plain }
    |> fst

let private tocked, posts, _ = Solo.beaten sitting

report "a table beaten once has moved the game" 2 (racing.Turn(Model.state (Solo.model tocked)))

report "and drawn everybody watching" 1 (List.length posts)

report
    "a table of a game that waits is not beaten at all"
    0
    (let waiting =
        Solo.opened turns "stamp" (dealt turning 1)
        |> Solo.watching
            "keyboard"
            { Margins = Margins.all
              Hushed = false
              View = Playable.plainest AtATerminal standard turns }
        |> fst

     let _, posts, _ = Solo.beaten waiting
     List.length posts)

report
    "a table beaten until the snake stops writes the record itself"
    true
    (let rec beating count table =
        let next, _, doing = Solo.beaten table

        match doing with
        | Keeping _ -> true
        | _ when count > 60 -> false
        | _ -> beating (count + 1) next

     beating 0 sitting)


let private walked =
    played racing [ Steer(seat 1, North); Beat; Steer(seat 1, East); Beat ] ticking

report
    "a move can be taken back"
    (standing (played racing [ Steer(seat 1, North); Beat; Steer(seat 1, East) ] ticking))
    (standing (Update.update racing Undo walked))

report "and made again" (standing walked) (standing (walked |> Update.update racing Undo |> Update.update racing Redo))


let private record = Transcript.write snake [ Here ] walked.Journal

report "a record says how the game was dealt, and who was at it" true (record |> mentions "deal 1 0 you")

report "and is written in beats and steers" true (record |> mentions "a north" && record |> mentions "go")

let private readBack = Transcript.read snake record |> Result.toOption |> Option.get

report
    "every move comes back off it"
    [ Make(Steer(seat 1, North)); Make Beat; Make(Steer(seat 1, East)); Make Beat ]
    readBack.Moves

report
    "and playing them again arrives at the same board, with no clock anywhere near it"
    (standing walked)
    (standing (
        Update.replay racing readBack.Players readBack.Seed readBack.Moves
        |> Result.toOption
        |> Option.get
    ))


report "'undo' is not this game's business" (Ok "undo") (reads snake "undo")

report "nor is 'save'" (Ok "save") (reads snake "save")

report "'resign' plays what this game says stopping is" (Ok "resign") (reads snake "resign")

for word in [ "north"; "n"; "up" ] do
    report $"'{word}' steers the first snake north" (Ok "a north") (reads snake word)

report "and 'b west' somebody else's" (Ok "b west") (reads snake "b west")

report "'go' is the beat" (Ok "go") (reads snake "go")

report
    "at a game of turns the same word is a step, and a bare direction is one too"
    (Ok "north", Ok "go")
    (reads turns "north", reads turns "go")

report
    "'a' is not a direction, and says what the directions are"
    true
    (match reads snake "a" with
     | Error problem -> problem |> mentions "'north', 'east', 'south', 'west'"
     | Ok _ -> false)

report "a table of five is refused by the shared verbs too" (Error "5 players? The game takes 1 to 4.") (reads snake "players 5")


let private view = Playable.plainest AtATerminal standard snake

let private board = view.Board Margins.all (seat 1) (beaten 1 pair)

report "the board is drawn with the walls round it" true (board |> mentions ("+" + String.replicate Board.Width "-" + "+"))

report "with a head in capitals and a body in small" true (board |> mentions "aaA")

report "and the other snake on it too" true (board |> mentions "Bbb")

report
    "it says how long this has been going and how you are doing"
    true
    (board |> mentions "Beat 2" && board |> mentions "Snake A (you)")

report "and nobody is to play, so nothing is pointed at" false (board |> mentions "->")

report
    "at a game of turns, somebody is"
    true
    ((Playable.plainest AtATerminal standard turns).Board Margins.all (seat 1) two
     |> mentions "->")

report "the notes can be turned off" false (view.Board Margins.none (seat 1) pair |> mentions (Render.Notes.moving Clock))

report "the record reads back through the view too" true (view.History (seat 1) walked |> mentions "a north")


report "a square can be asked about" true (view.Answer (seat 1) "north" ticking |> mentions "open board")

report
    "and the wall is named as what it is"
    true
    (view.Answer (seat 1) "north" (laid [ (1, [ at 1 5; at 1 4; at 1 3 ], East) ] ticking)
     |> mentions "the wall")

report "and the food when it is there" true (view.Answer (seat 1) "east" feasting |> mentions "the food")

report
    "a way nobody could look is answered rather than ignored"
    true
    (view.Answer (seat 1) "sideways" ticking |> mentions "is not a way to look")


let private views = snake.Views standard

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
    let drawn = seen (view.Board Margins.all (seat 1) (beaten 1 pair))

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

    report
        $"and the {view.Name} view's notes can be turned off"
        false
        (seen (view.Board Margins.none (seat 1) pair)
         |> mentions (Render.Notes.moving Clock))

    report
        $"the {view.Name} view answers a table still filling up"
        true
        (seen (view.Waiting arriving) |> mentions Scene.Filling.title)


let private page = Page.page snake.Page standard

let private fragments =
    [ "board", Page.Screen, asPage.Board Margins.all (seat 1) (beaten 1 pair)
      "board with the notes off", Page.Screen, asPage.Board Margins.none (seat 1) pair
      "a board that has ended", Page.Screen, asPage.Board Margins.all (seat 1) (beaten 1 meeting)
      "waiting", Page.Screen, asPage.Waiting arriving
      "a line the game said", Page.Told, asPage.Says "Snake A turns north."
      "the record", Page.Told, asPage.History (seat 1) walked
      "an answer", Page.Told, asPage.Answer (seat 1) "north" ticking
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


report "the page carries the keys, because this game does not wait" true (page |> mentions "ArrowUp")

report "the page of a game that waits carries none" false (Page.page turns.Page standard |> mentions "ArrowUp")

report
    "and every key the page sends is a line this game reads"
    []
    (snake.Page.Keys
     |> List.map snd
     |> List.filter (fun line -> Result.isError (reads snake line)))

report
    "the keys a page has and the keys a terminal has are the same keys"
    (snake.Page.Keys |> List.map snd |> List.sort)
    ([ ConsoleKey.UpArrow
       ConsoleKey.DownArrow
       ConsoleKey.LeftArrow
       ConsoleKey.RightArrow
       ConsoleKey.W
       ConsoleKey.A
       ConsoleKey.S
       ConsoleKey.D
       ConsoleKey.I
       ConsoleKey.J
       ConsoleKey.K
       ConsoleKey.L
       ConsoleKey.NumPad8
       ConsoleKey.NumPad4
       ConsoleKey.NumPad5
       ConsoleKey.NumPad6
       ConsoleKey.OemPlus
       ConsoleKey.Add
       ConsoleKey.OemMinus
       ConsoleKey.Subtract ]
     |> List.choose keyed
     |> List.sort)

let private posted (markup: string) =
    Regex.Matches(WebUtility.HtmlDecode markup, @"@post\('/say\?line=([^']*)'\)")
    |> Seq.map (fun found -> Uri.UnescapeDataString found.Groups[1].Value)
    |> List.ofSeq

let private buttons = posted (asPage.Board Margins.all (seat 1) pair)

report
    "the board offers a button for each way to turn your own snake, two for the clock, and one to deal another"
    [ "a north"; "a west"; "a east"; "a south"; "slower"; "faster"; "restart" ]
    buttons

report
    "and the second player's board offers theirs"
    [ "b north"; "b west"; "b east"; "b south"; "slower"; "faster"; "restart" ]
    (posted (asPage.Board Margins.all (seat 2) pair))


report "and the one that is the engine's word is read like anybody's" (Ok "restart") (reads snake "restart")

report
    "and every one of them types a line the program takes"
    []
    (buttons
     |> List.filter (fun line ->
         match reads snake line with
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

let private described = controls (Render.board Margins.all (seat 1) pair)

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
    [ Render.Notes.board; Render.Notes.moving Clock ]
    (notes (Render.board Margins.all (seat 1) pair))

report "and not one of them survives turning them off" [] (notes (Render.board Margins.none (seat 1) pair))


report "three machines are offered at the pace that has turns" [ "easy"; "medium"; "hard" ] (turns.Skills |> List.map fst)

report "and none at all on a clock, where there is no turn to take one in" [] snake.Skills

let private playedOut skills seed =
    let start =
        Update.start turning (List.length skills) seed |> Result.toOption |> Option.get

    let seated = turns.Seating seed (skills |> List.map Some) (standing start)
    Machines.answering turning Playable.plays seated start |> fst

let private alone skill seed = playedOut [ skill ] seed

let private lived model =
    Model.state model
    |> Session.play
    |> Session.snakes
    |> List.sumBy (fun (_, snake) -> snake.Eaten)

let private runs skill =
    [ 1UL .. 6UL ] |> List.map (alone skill)

let private careful = runs "hard"

let private hasty = runs "easy"

report
    "a table of machines plays the game out to its end"
    true
    (careful |> List.forall (fun model -> turning.Over(standing model)))

report
    "and nothing any of them asked for over a run of games was refused"
    []
    (careful
     |> List.collect (fun model -> Journal.entries model.Journal)
     |> List.collect (fun entry -> entry.Told)
     |> List.choose (function
         | Said(Refused refusal) -> Some(Words.rejection refusal)
         | _ -> None))

let private eatenBy = List.sumBy lived

report "counting the room it leaves beats going straight for the food" true (eatenBy careful > eatenBy hasty)

report "and beats it by a good deal rather than by a nose" true (eatenBy careful > 3 * eatenBy hasty)

report
    "the same machines at the same deal play the same game twice"
    (Journal.moves (playedOut [ "hard"; "easy" ] 7UL).Journal)
    (Journal.moves (playedOut [ "hard"; "easy" ] 7UL).Journal)

report
    "and a different seed is a different game"
    false
    (Journal.moves (playedOut [ "easy"; "easy" ] 7UL).Journal = Journal.moves (playedOut [ "easy"; "easy" ] 8UL).Journal)


// === The seam every game fills in ===

Conforms.against snake 2 [ "beat"; "faster"; "slower"; "b north" ]

Conforms.against turns 2 [ "go"; "north"; "go" ]

finish ()
