#load "Cascading.fsx"

open System
open System.Net
open System.Text.RegularExpressions
open System.Xml
open TCModel.Engine
open TCModel.Table
open TCModel.Cascade
open Checks
open Cascading

let private rules = cascade.Rules

let private at (name: string) = Board.read name |> Option.get

let private mentions (needle: string) (text: string) = text.Contains needle

/// A command as the line that would have made it. A `Command` is not something two of which may
/// be compared - it carries a move, and nothing about a game says a move can be.
let private reads typed =
    match Playable.read cascade typed with
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

let private sends move = Ok(Words.command (Make move))


// === The board it is played on ===

report "the board hangs together" [] cascade.Faults

report "it is two hundred and fifty-six cells" 256 (List.length Board.all)

report
    "a cell whose name does not read back"
    []
    (Board.all |> List.filter (fun cell -> Board.read (Board.name cell) <> Some cell))

report "the columns run a to p" "abcdefghijklmnop" Board.letters

report "and nothing off the board is on it" false (Board.holds (at "a17") || Board.holds { Row = 1; Column = 0 })

report
    "four quarter turns bring every facing back"
    []
    (Facing.all
     |> List.filter (fun f -> f |> Facing.turned |> Facing.turned |> Facing.turned |> Facing.turned <> f))

report "and no single one does" [] (Facing.all |> List.filter (fun f -> Facing.turned f = f))

report "every facing is two arms" [ 2; 2; 2; 2 ] (Facing.all |> List.map (Facing.arms >> List.length))

report
    "and every way out of a cell is an arm of exactly two of the four - which is the odds a cascade runs on"
    [ 2; 2; 2; 2 ]
    (Way.all
     |> List.map (fun way -> Facing.all |> List.filter (Facing.reaches way) |> List.length))

report
    "the shapes watched for are sixteen rows, sixteen columns and two hundred and twenty-five squares"
    257
    (List.length Shape.all)

report
    "and not one of them stands off the board"
    []
    (Shape.all
     |> List.filter (fun shape -> Shape.cells shape |> List.exists (Board.holds >> not)))

report "a rank is a whole row" 16 (List.length (Shape.cells (Rank 3)))

report
    "a square is four cells"
    [ at "c3"; at "d3"; at "c4"; at "d4" ]
    (Shape.cells (Square(at "c3")) |> List.sortBy (fun c -> c.Row, c.Column))


// === The rule, on a board laid out by hand ===
//
// A board where every cell faces the same way is the whole rule in one picture. Every cell here
// is up-and-right, so a cell that turns becomes right-and-down; its arm east finds a cell facing
// up-and-right, which has no arm west and is not reaching back, and its arm south finds one whose
// arm north is. So a touch marches straight down its column, one cell a wave, and stops at the
// bottom edge because an arm pointing off the board reaches nothing.

let private laid facing =
    let cells =
        Board.all
        |> List.fold
            (fun cells cell ->
                Map.add
                    cell
                    { Facing = facing
                      Turned = 0
                      Landed = 0 }
                    cells)
            Map.empty

    InPlay
        { Session.play (Session.dealt 0UL) with
            Cells = cells }

let private facing name what session =
    let play = Session.play session

    InPlay
        { play with
            Cells =
                Map.add
                    (at name)
                    { Facing = what
                      Turned = 0
                      Landed = 0 }
                    play.Cells }

let private asked move session =
    Turn.asked move session |> fst |> Option.defaultValue session

let private told move session = Turn.asked move session |> snd

let rec private toRest guard session =
    if Session.atRest (Session.play session) || guard > Session.MostWaves + 5 then
        session
    else
        toRest (guard + 1) (asked Beat session)

let private run session =
    (Session.play session).Run |> Option.defaultValue (Session.opened (at "a1"))

let private uniform = laid UpRight

let private marched = uniform |> asked (Touch(at "a1")) |> toRest 0

report "a touch on a board facing all one way marches down its column" 16 (run marched).Rotations

report "a wave a cell, and no two at once" 16 (run marched).Waves

report "and every cell in that column turned" 16 (Set.count (run marched).Rotated)

report "which is a whole column, and it is counted as one" [ File 1 ] (run marched).Made

report "a column is a line rather than a square" (1, 0) (Session.lines (run marched).Made, Session.squares (run marched).Made)

report "the cell touched has turned a quarter to the right" RightDown (Session.facing (at "a1") (Session.play marched))

report "and so has the one at the bottom the cascade reached" RightDown (Session.facing (at "a16") (Session.play marched))

report "while the column beside it was never touched" UpRight (Session.facing (at "b1") (Session.play marched))

report
    "a touch further down marches a shorter way, and completes nothing"
    (9, [])
    (let short = uniform |> asked (Touch(at "h8")) |> toRest 0
     (run short).Rotations, (run short).Made)


// === Two at once, and a cell set off a second time ===
//
// One cell of the uniform board is turned round to face down-and-left, which gives it an arm west
// for the cell beside it to find. Touching that cell now sets off two at once - the one below it
// and the one beside it - and what the one beside it lands facing reaches back to where it came
// from, so the cell that started the whole thing is set turning again.

let private forked = uniform |> facing "b1" DownLeft

let private wave n session =
    let rec beating count session =
        if count >= n then session else beating (count + 1) (asked Beat session)

    beating 0 session

let private started = forked |> asked (Touch(at "a1"))

report "the touch sets one cell turning" (set [ at "a1" ]) (Session.play started).Turning

report "and nothing has turned yet - a touch is not a turn" 0 (run started).Rotations

report
    "the first wave lands it and sets off two, which turn together"
    (set [ at "a2"; at "b1" ])
    (Session.play (wave 1 started)).Turning

report "both of them land on the same beat" 3 (run (wave 2 started)).Rotations

report
    "and the one beside it now reaches back, so the cell that started it turns again"
    true
    (Set.contains (at "a1") (Session.play (wave 2 started)).Turning)

report
    "a cell may go round more than once, and the count says how many turns rather than how many cells"
    true
    (let settled = forked |> asked (Touch(at "a1")) |> toRest 0
     (run settled).Rotations > Set.count (run settled).Rotated)

report
    "a cell that has been round twice has been counted twice"
    2
    (Session.standing (at "a1") (Session.play (wave 3 started))).Turned


// === What may be touched, and when ===

report "nothing may be touched while anything is turning" [ Refused(StillTurning 1) ] (told (Touch(at "p16")) started)

report
    "and the board does not move for the asking"
    (Session.play started).Cells
    (Session.play (asked (Touch(at "p16")) started)).Cells

report
    "once it comes to rest it may be touched again"
    true
    (match told (Touch(at "p16")) (toRest 0 started) with
     | [ Happened(Touched _) ] -> true
     | _ -> false)

report
    "a cell off the board is refused rather than reached for"
    [ Refused(NoSuchCell { Row = 40; Column = 1 }) ]
    (told (Touch { Row = 40; Column = 1 }) uniform)

report "a board is worth twelve touches" 12 Session.Touches

report
    "and when they are spent the game is over"
    true
    (let rec spending session =
        let play = Session.play session

        if Session.isOver session then true
        elif play.Left = 0 && Session.atRest play then false
        else spending (session |> asked (Touch(at "h8")) |> toRest 0)

     spending uniform)

report
    "the last touch is what ends it, not the beat after"
    true
    (let rec spending count session =
        if count = 0 then
            Session.isOver session
        else
            spending (count - 1) (session |> asked (Touch(at "a1")) |> toRest 0)

     spending Session.Touches uniform)

report "a touch too many is refused" [ Refused NoneLeft ] (told (Touch(at "a1")) (InPlay { Session.play uniform with Left = 0 }))

report
    "giving up spends what is left and says so"
    (true, Some GaveUp)
    (let given = uniform |> asked Resign
     Session.isOver given, Session.ending given)


// === The clock ===

report "a beat over a board with nothing turning takes nothing and says nothing" (None, []) (Turn.asked Beat uniform)

report "the notch a board is dealt on is the half second the rules are written in" 500 (Session.quarter Session.Ordinary)

report "the fastest notch is a tenth of that" 100 (Session.quarter Session.Fastest)

report "winding it up says what a quarter turn now takes" [ Happened(Wound 6) ] (told Faster uniform)

report
    "and winding past the end does nothing at all"
    (None, [])
    (Turn.asked
        Faster
        (InPlay
            { Session.play uniform with
                Speed = Session.Fastest }))

report "a speed the notches do not run to is refused" [ Refused(NoSuchSpeed 12) ] (told (Speed 12) uniform)

report
    "a cascade that will not stop is stopped"
    (true, true)
    (let held =
        InPlay
            { Session.play uniform with
                Turning = set [ at "a1" ]
                Run =
                    Some
                        { Session.opened (at "a1") with
                            Waves = Session.MostWaves - 1 } }

     let beaten = asked Beat held
     (run beaten).Halted, Session.atRest (Session.play beaten))


// === Through the engine: the record, and taking it back ===

let private dealt = Update.start rules 1 0UL |> Result.toOption |> Option.get

let private played moves =
    moves
    |> List.fold (fun model move -> Update.update rules (Make move) model) dealt

report "the deal is the first touch" 1 (rules.Turn(Model.state dealt))

report "the same seed is the same board" (Session.play (Session.dealt 42UL)).Cells (Session.play (Session.dealt 42UL)).Cells

report
    "and a different seed is a different one"
    false
    ((Session.play (Session.dealt 42UL)).Cells = (Session.play (Session.dealt 43UL)).Cells)

report "a board nobody has touched has an empty record" true (Journal.isEmpty dealt.Journal)

report
    "and beating a hundred times over one leaves it empty, because none of those beats happened"
    true
    (Journal.isEmpty (played (List.replicate 100 Beat)).Journal)

report "a touch is written down" 1 (Journal.length (played [ Touch(at "h8") ]).Journal)

report "and so is every beat that landed a wave" 3 (Journal.length (played [ Touch(at "h8"); Beat; Beat ]).Journal)

report
    "but a refused touch is written down too, because the game had something to say about it"
    3
    (Journal.length (played [ Touch(at "h8"); Beat; Touch(at "a1") ]).Journal)

report
    "undo walks back a wave at a time"
    1
    (let two = played [ Touch(at "a1"); Beat; Beat ]
     let back = Update.update rules Undo two
     Timeline.movesMade two.Timeline - Timeline.movesMade back.Timeline)

report
    "and a game replays off its record exactly"
    true
    (let game = played [ Touch(at "a1"); Beat; Beat; Faster; Touch(at "p16") ]

     match Update.replay rules 1 0UL (Journal.moves game.Journal) with
     | Ok again -> Model.state again = Model.state game
     | Error _ -> false)

report "there is one seat" 1 (rules.Seats(Model.state dealt))

report "and the game is always standing at it" (Seat.at 1) (rules.Active(Model.state dealt))

report "and there is no machine to sit in it" [] cascade.Skills


// === What it sounds like ===

report "a board at rest makes no sound" [] (cascade.Rings uniform)

report "a wave landing taps" [ Tap ] (cascade.Rings(wave 1 started))

report "a cascade coming to rest chimes" [ Chime ] (cascade.Rings(toRest 0 (uniform |> asked (Touch(at "h8")))))

report "a shape coming up is the rare one" [ Fanfare ] (cascade.Rings(toRest 0 (uniform |> asked (Touch(at "a1")))))

report "and a touch itself is silent - nothing has moved yet" [] (cascade.Rings started)


// === How it is drawn ===

let private seen (text: string) =
    Regex.Replace(text, @"\x1b\[[0-9;]*m", "")

let private turning = wave 1 started

let private drawn margins session =
    plain.Board
        margins
        (Seat.at 1)
        (Update.start rules 1 0UL
         |> Result.toOption
         |> Option.get
         |> fun model ->
             { model with
                 Timeline = Timeline.ofDeal session })

report "the board is drawn with a legend across the top" true (drawn Margins.all uniform |> mentions Board.letters)

report
    "and every row of it"
    true
    ([ 1..16 ]
     |> List.forall (fun row -> drawn Margins.all uniform |> mentions $"{row} "))

report "a cell as it was dealt is drawn light" true (drawn Margins.all uniform |> mentions "└└└")

report
    "a cell that has been round five times is drawn heavier, which is what a terminal with no colour has"
    true
    (let worn =
        InPlay
            { Session.play uniform with
                Cells =
                    Map.add
                        (at "a1")
                        { Facing = UpRight
                          Turned = 5
                          Landed = 0 }
                        (Session.play uniform).Cells }

     drawn Margins.all worn |> mentions "╰")

report
    "a turning cell is drawn three ways across a beat, and the phase is what picks between them"
    3
    ([ 0.0; 0.4; 0.8 ]
     |> List.map (fun phase -> drawn (Margins.through phase Margins.all) turning)
     |> List.distinct
     |> List.length)

report
    "a board with nothing turning on it is drawn the same however far through a beat it is"
    1
    ([ 0.0; 0.4; 0.8 ]
     |> List.map (fun phase -> drawn (Margins.through phase Margins.all) uniform)
     |> List.distinct
     |> List.length)

report "the notes can be turned off" false (drawn Margins.none uniform |> mentions Render.Notes.board)

report "and the count is drawn either way" true (drawn Margins.none uniform |> mentions "Turns")

for view in cascade.Views standard do
    report
        $"the {view.Name} view draws a board, a count and a log"
        true
        (let board =
            seen (view.Board Margins.all (Seat.at 1) (Update.start rules 1 0UL |> Result.toOption |> Option.get))

         [ Render.Blocks.board; Render.Blocks.count; Render.Blocks.log ]
         |> List.forall (fun block -> board.ToLowerInvariant() |> mentions (block.ToLowerInvariant())))


// === The page ===

let private page = Page.page cascade.Page standard

let private model session =
    Update.start rules 1 0UL
    |> Result.toOption
    |> Option.get
    |> fun m ->
        { m with
            Timeline = Timeline.ofDeal session }

let private paged margins session =
    asPage.Board margins (Seat.at 1) (model session)

let private fragments =
    [ "board", Page.Screen, paged Margins.all uniform
      "board with a cascade running", Page.Screen, paged Margins.all turning
      "board with the notes off", Page.Screen, paged Margins.none uniform
      "a line the game said", Page.Told, asPage.Says "a1 begins turning."
      "the record", Page.Told, asPage.History (Seat.at 1) (played [ Touch(at "a1"); Beat ])
      "an answer", Page.Told, asPage.Answer (Seat.at 1) "a1" (model uniform)
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

report "the page carries a stylesheet that knows how to turn a cell" true (page |> mentions "@keyframes turning")

report "and one that runs a light along a shape" true (page |> mentions "@keyframes lit")

report
    "and a notch for every speed the game has"
    true
    ([ Session.Slowest .. Session.Fastest ]
     |> List.forall (fun n -> page |> mentions $".speck.pace-{n} "))

report "and it stops moving for a reader who asked for that" true (page |> mentions "prefers-reduced-motion")

report
    "a turning cell on the page carries the mood the sheet animates"
    true
    (paged Margins.all turning |> mentions "class=\"speck turning")

report
    "and the notch it is turning at, so winding the clock winds the animation"
    true
    (paged Margins.all turning |> mentions $"pace-{Session.Ordinary}")

report "a board at rest carries no turning cell at all" false (paged Margins.all uniform |> mentions "speck turning")

report "a cell that has just landed says so, for the page to flash" true (paged Margins.all (wave 2 started) |> mentions "landed")

report
    "and a shape that has come up lights its cells in order, so the light travels"
    true
    (let lit = toRest 0 (uniform |> asked (Touch(at "a1")))
     let drawn = paged Margins.all lit
     drawn |> mentions "lit-0" && drawn |> mentions "speck lit")

let private posted (markup: string) =
    Regex.Matches(WebUtility.HtmlDecode markup, @"@post\('/say\?line=([^']*)'\)")
    |> Seq.map (fun found -> Uri.UnescapeDataString found.Groups[1].Value)
    |> List.ofSeq

let private buttons = posted (paged Margins.all uniform)

report
    "the board offers buttons for a touch, a question, the clock and another deal"
    [ "f7"; "why f7"; "faster"; "slower"; "undo"; "restart" ]
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

report
    "every key the page sends is a line this game reads"
    []
    (cascade.Page.Keys
     |> List.map snd
     |> List.filter (fun line -> Result.isError (reads line)))


// === What it says ===

report "a bare cell is a touch" (sends (Touch(at "f7"))) (reads "f7")

report "and so is one said the long way" (sends (Touch(at "f7"))) (reads "touch f7")

report "a question about a cell is a question" (Ok "asking f7") (reads "why f7")

report "the clock is wound by name" (sends Faster) (reads "faster")

report "and by sign" (sends Slower) (reads "-")

report "a notch is said outright" (sends (Speed 7)) (reads "speed 7")

report "a word that is not a cell is turned away" true (Result.isError (reads "zz"))

report
    "every move the game has is written as a line the game reads back"
    []
    ([ Touch(at "f7"); Beat; Faster; Slower; Speed 7; Resign ]
     |> List.map (fun move -> move, Words.command (Make move))
     |> List.filter (fun (move, line) -> reads line <> Ok(Words.command (Make move))))

report
    "and every notice the game can make has words"
    []
    ([ Happened(Touched(at "a1"))
       Happened(CameUp(Rank 3, 40))
       Happened(CameUp(Square(at "c3"), 40))
       Happened(Settled(Session.opened (at "a1")))
       Happened(Halted(Session.opened (at "a1")))
       Happened(Wound 7)
       Happened(GaveIn 3)
       Happened(GameEnded Session.fresh)
       Refused(StillTurning 4)
       Refused NoneLeft
       Refused(NoSuchCell(at "a1"))
       Refused(NoSuchSpeed 12) ]
     |> List.filter (fun notice -> Words.said notice = ""))


// === The clock a terminal keeps ===
//
// A frame is a redrawing rather than a move, and this is the arithmetic that decides when the
// next one is due. It is on the seam rather than in the loop that uses it, so that it can be
// asked the question here without a terminal, a keyboard or half a second of waiting.

let private opened = DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)

let private next = opened + TimeSpan.FromMilliseconds 600.0

let private waking frames after =
    Pulse.waking frames opened next (opened + TimeSpan.FromMilliseconds(float (after: int)))

report "a game that asks for no frames is woken by the beat and nothing else" next (waking 0 0)

report "and so is one that asks for a single frame, which is the beat itself" next (waking 1 300)

report "six frames over six hundred milliseconds fall a hundred apart" (opened + TimeSpan.FromMilliseconds 100.0) (waking 6 0)

report
    "and what is asked for is the next boundary after now, not the next one in order"
    (opened + TimeSpan.FromMilliseconds 400.0)
    (waking 6 340)

report
    "so a frame that came late does not push the ones behind it late as well"
    (opened + TimeSpan.FromMilliseconds 500.0)
    (waking 6 410)

report "the last frame of a beat is the beat" next (waking 6 540)

report "and a beat already overshot is due at once" next (waking 6 900)

report "the phase is nought at the beat" 0.0 (Pulse.phase opened next opened)

report "half way through, half" 0.5 (Pulse.phase opened next (opened + TimeSpan.FromMilliseconds 300.0))

report
    "and it never reaches one, so the last frame is the last picture of this turn rather than the first of the next"
    (Render.Pictures - 1)
    (Margins.frame Render.Pictures (Margins.through (Pulse.phase opened next next) Margins.all))

report "a beat with no width to it is all beat and no frame" 0.0 (Pulse.phase opened opened next)

report
    "and every one of the six frames a cascade asks for falls in one of the three pictures"
    [ 0; 0; 1; 1; 2; 2 ]
    ([ 0..5 ]
     |> List.map (fun frame ->
         let now = opened + TimeSpan.FromMilliseconds(100.0 * float frame)
         Margins.frame Render.Pictures (Margins.through (Pulse.phase opened next now) Margins.all)))


// === At a table ===

let private sitting =
    Solo.opened cascade "stamp" (model uniform)
    |> Solo.watching "keyboard" { Margins = Margins.all; View = plain }
    |> fst

report
    "a table beaten over a board at rest draws nobody, because nothing moved"
    []
    (let _, posts, _ = Solo.beaten sitting
     posts)

report
    "a table beaten over a cascade draws everybody watching, and rings"
    (1, 1)
    (let touched, _, _ = Solo.said "stamp" "keyboard" "a1" sitting
     let _, posts, _ = Solo.beaten touched

     posts
     |> List.filter (fun post ->
         match post.Say with
         | Screen _ -> true
         | _ -> false)
     |> List.length,
     posts
     |> List.filter (fun post ->
         match post.Say with
         | Rang _ -> true
         | _ -> false)
     |> List.length)


finish ()
