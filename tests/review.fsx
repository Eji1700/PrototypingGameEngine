#load "Noughts.fsx"

open System.IO
open Prototyping.Engine
open Prototyping.Table
open Noughts

let report = Checks.report

let private msg line =
    match Playable.read noughts line with
    | Ok(Send msg) -> msg
    | _ -> failwith $"'{line}' is not a move at noughts and crosses"

// Second thoughts on purpose: an undo that crosses back over a turn, a move refused for a square
// already taken, and a redo with nothing to make again - each of them an entry that is not a mark.
let private lines =
    [ "place 1"
      "place 5"
      "undo"
      "place 6"
      "place 6"
      "place 2"
      "redo"
      "place 3"
      "place 9" ]

let private played =
    Update.replay noughts.Rules 2 0UL (List.map msg lines)
    |> Result.toOption
    |> Option.get

let private written = Transcript.write noughts [ Here; Here ] played.Journal

let private reading =
    Transcript.read noughts written |> Result.toOption |> Option.get

let private opened game reading =
    Review.opened game "a game.log" reading |> Result.toOption |> Option.get

let private review = opened noughts reading

let rec private walked steps review =
    if steps = 0 then review else walked (steps - 1) (Review.stepped 1 review)

/// Everything the table can read off a position, as in `Conforms`: a move is not something two of
/// which may be compared, so two models are the same when everything drawn and written of them is.
let private fingerprint (model: Model<_, _, _>) =
    let state = Model.state model

    [ yield $"turn {noughts.Rules.Turn state}"
      yield $"active {PlayerId.value (noughts.Rules.Active state)}"
      yield $"over {noughts.Rules.Over state}"
      yield!
          Journal.entries model.Journal
          |> List.map (fun entry -> noughts.Write entry.Asked)
      for place in 1..2 do
          yield Checks.uncoloured (plain.Board Margins.none (Seat.at place) model) ]


// --- the stops ----------------------------------------------------------------------------------

report
    "a review has a stop for the deal and one for every entry in the journal"
    (Journal.length played.Journal)
    ((Review.place review).Moves)

report "and opens at the deal, looking as everyone" (0, Eye.Everyone) ((Review.place review).Move, Review.eye review)

report
    "every stop is the game exactly as replaying that many lines leaves it"
    []
    [ for k in 0 .. Journal.length played.Journal do
          let replayed =
              Update.replay noughts.Rules 2 0UL (List.truncate k (List.map msg lines))
              |> Result.toOption
              |> Option.get

          if fingerprint (Review.model (walked k review)) <> fingerprint replayed then
              yield $"the stop at {k}" ]

// A game that neither takes square 9 nor says anything about it, which is how a clock beating over a
// board at rest behaves: the line is in the record, and nothing happened.
let private silentAtNine =
    { noughts with
        Rules =
            { noughts.Rules with
                Play = fun move state -> if noughts.Write(Make move) = "place 9" then None, [] else noughts.Rules.Play move state } }

report
    "a line that wrote nothing in the journal is never a stop"
    (2, [ "place 1"; "place 5" ])
    (let quiet =
        opened
            silentAtNine
            { reading with
                Moves = List.map msg [ "place 1"; "place 9"; "place 5" ] }

     let last = Review.stepped 9 quiet

     (Review.place last).Moves,
     Journal.entries (Review.model last).Journal
     |> List.map (fun entry -> noughts.Write entry.Asked))


// --- walking it ---------------------------------------------------------------------------------

let private landings step from =
    let rec landing review =
        let next = step review

        if (Review.place next).Move = (Review.place review).Move then
            []
        else
            (Review.place next).Move :: landing next

    landing from

// The journal, by stop: 1 turn 1, 2 turn 2, 3 the undo from turn 3, 4 turn 2 again, 5 and 6 turn 3
// (the refused mark and the one taken), 7 and 8 turn 4 (the empty redo and the mark), 9 turn 5.
report
    "a turn forward lands on the last stop of the next turn, an undo across a turn being a turn of its own"
    [ 1; 2; 3; 4; 6; 8; 9 ]
    (landings (Review.turned 1) review)

report
    "and a turn back lands on the last stop of the turn before, down to the deal"
    [ 8; 6; 4; 3; 2; 1; 0 ]
    (landings (Review.turned -1) (Review.stepped 99 review))

report
    "from part of the way through a turn, back lands on the end of the turn before it"
    4
    ((Review.place (Review.turned -1 (walked 5 review))).Move)

report
    "a move forward and back is one stop either way"
    ([ 1..9 ], [ 8..-1..0 ])
    (landings (Review.stepped 1) review, landings (Review.stepped -1) (Review.stepped 99 review))

report
    "the ends hold: nothing before the deal, nothing after the last stop"
    (0, 0, 9, 9)
    ((Review.place (Review.stepped -1 review)).Move,
     (Review.place (Review.turned -1 review)).Move,
     (Review.place (Review.stepped 1 (Review.stepped 99 review))).Move,
     (Review.place (Review.turned 1 (Review.stepped 99 review))).Move)

report
    "the turn is counted as the runs are, and the deal is none of them"
    [ 0, 7; 1, 7; 5, 7; 7, 7 ]
    ([ 0; 1; 6; 9 ]
     |> List.map (fun k -> walked k review |> Review.place |> (fun place -> place.Turn, place.Turns)))

report
    "turning the eye leaves the review where it stands, and walking leaves the eye where it is"
    (6, Eye.Seat(Seat.at 2))
    (let turned =
        walked 6 review
        |> Review.looking (Eye.Seat(Seat.at 2))
        |> Review.turned 1
        |> Review.turned -1

     (Review.place turned).Move, Review.eye turned)


// --- what is shown of each turn -----------------------------------------------------------------

report
    "at the deal there is nothing to list"
    "The deal\n  Nothing has been played yet."
    ((Review.thisTurn review).Replace("\r\n", "\n"))

report
    "as everyone, a turn lists every order in it and what came of each"
    [ "This turn (5)"
      "  X: place 6  -  Square 6 already has O in it."
      "  X: place 2  -  X takes square 2." ]
    ((Review.thisTurn (walked 6 review)).Replace("\r\n", "\n").Split '\n'
     |> List.ofArray)

report
    "and part of the way through one, only as far as the stop it stands on"
    [ "This turn (5)"; "  X: place 6  -  Square 6 already has O in it." ]
    ((Review.thisTurn (walked 5 review)).Replace("\r\n", "\n").Split '\n'
     |> List.ofArray)

// A game whose every order carries a word nobody but the seat that wrote it may see, and which tells
// nobody anything of anybody else's - so every order of another seat's that reaches the list is one
// the review let through itself.
let private secretive =
    { noughts with
        Write = fun msg -> $"{noughts.Write msg} (secret)"
        SeenBy = fun _ _ -> "" }

let private everyTurn eye =
    let looking = opened secretive reading |> Review.looking eye

    [ for k in 0 .. (Review.place looking).Moves -> Review.thisTurn (walked k looking) ]

// A seat's own order may carry its secret; nothing after it may, and no other line at all. The undo
// in this record is X's, and what it takes back is O's mark.
report
    "through a seat's eyes, no turn ever shows an order of anybody else's, nor a move taken back"
    []
    [ for seat in 1..2 do
          let own = $"  {noughts.Seat(Seat.at seat)}: "

          for told in everyTurn (Eye.Seat(Seat.at seat)) do
              for line in told.Replace("\r\n", "\n").Split '\n' do
                  let after =
                      match line.IndexOf "  -  " with
                      | -1 when line.StartsWith own -> ""
                      | -1 -> line
                      | at -> line.Substring at

                  if after.Contains "(secret)" then yield $"seat {seat} was shown '{line.Trim()}'" ]

report
    "but its own are there, and another seat's that it was told nothing of is said to be sealed"
    [ "This turn (3)"; "  X: undo (secret)"; "This turn (2)"; "  O: sealed" ]
    (let x = opened secretive reading |> Review.looking (Eye.Seat(Seat.at 1))

     [ walked 3 x; walked 2 x ]
     |> List.collect (fun at -> (Review.thisTurn at).Replace("\r\n", "\n").Split '\n' |> List.ofArray))

report
    "and the other seat sees that something was taken back, without being told what"
    [ "This turn (3)"; "  X: took something back" ]
    (opened secretive reading
     |> Review.looking (Eye.Seat(Seat.at 2))
     |> walked 3
     |> Review.thisTurn
     |> fun told -> told.Replace("\r\n", "\n").Split '\n' |> List.ofArray)

report
    "as everyone, every order is shown, the secrets in them as well"
    true
    (everyTurn Eye.Everyone
     |> List.exists (fun told -> told.Contains "O: place 5 (secret)"))


// --- what a review will not open ----------------------------------------------------------------

report
    "a record that deals again part of the way through is refused, being two games"
    true
    (match
        Review.opened
            noughts
            "twice.log"
            { reading with
                Moves = reading.Moves @ [ Restart(None, None) ] }
     with
     | Error problem -> problem.Contains "two games"
     | Ok _ -> false)

report
    "and one for a number of players the game does not take"
    true
    (match Review.opened noughts "three.log" { reading with Players = 3 } with
     | Error problem -> problem.Contains "number of players"
     | Ok _ -> false)

report
    "another game's record is turned away before anything is read, with the same hint taking one up gives"
    (Error "'logs/2026-08-03-193909-turncoats-2p-seed639214079490240407.log' is a game of turncoats, not of tictactoe. (hint)")
    (Transcript.reading (fun _ -> " (hint)") noughts "logs/2026-08-03-193909-turncoats-2p-seed639214079490240407.log"
     |> Result.map ignore)

report
    "and so is a record that is not there"
    (Error "There is no record at 'logs/nowhere.log'.")
    (Transcript.reading (fun _ -> "") noughts "logs/nowhere.log" |> Result.map ignore)


// --- nothing is played, and nothing is written ----------------------------------------------------

report
    "a record with the machine in a seat is reviewed as the lines in it, and the machine plays nothing"
    (Journal.length played.Journal, fingerprint played)
    (let seated =
        Transcript.write noughts [ Here; Machine "easy" ] played.Journal
        |> Transcript.read noughts
        |> Result.toOption
        |> Option.get

     let last = opened noughts seated |> Review.stepped 99
     (Review.place last).Moves, fingerprint (Review.model last))

let private everything () =
    Directory.EnumerateFiles "logs"
    |> Seq.sort
    |> Seq.map (fun path -> path, File.ReadAllBytes path |> System.Convert.ToBase64String)
    |> List.ofSeq

let private before = everything ()

let private kept =
    Transcript.reading (fun _ -> "") noughts "logs/2026-08-07-190000-tictactoe-2p-seed0.log"
    |> Result.bind (Review.opened noughts "2026-08-07-190000-tictactoe-2p-seed0.log")
    |> Result.toOption
    |> Option.get

// Every line a player might reach for to change a game, said to a review of a record in logs/, with
// the board drawn after each - which is everything the review loop at a keyboard does.
[ "end"
  "undo"
  "redo"
  "place 9"
  "resign"
  "restart"
  "players 2 7"
  "save"
  "back a turn"
  "eye 2"
  "history"
  "quit" ]
|> List.fold
    (fun review line ->
        Review.drawn plain review |> ignore

        match Review.said plain line review with
        | Said.Moved next
        | Said.Printed(next, _) -> next
        | Said.Looked _
        | Said.Left -> review)
    kept
|> ignore

report "nothing under logs/ is written, moved or changed by reviewing one of them" before (everything ())


// --- what a line typed at a review does ---------------------------------------------------------

let private saying line review =
    match Review.said plain line review with
    | Said.Moved next -> Ok next
    | Said.Printed(next, said) -> Error(next, said)
    | Said.Looked _ -> failwith $"'{line}' asked for another view"
    | Said.Left -> failwith $"'{line}' left"

let private landed line review =
    match saying line review with
    | Ok next -> (Review.place next).Move
    | Error(_, said) -> failwith $"'{line}' was answered '{said}'"

report
    "the words walk it as the steps do"
    [ 1; 0; 6; 4; 5; 4; 0; 9; 7; 9; 0 ]
    [ landed "next move" review
      landed "back a move" (walked 1 review)
      landed "next turn" (walked 4 review)
      landed "back a turn" (walked 5 review)
      landed "next" (walked 4 review)
      landed "previous" (walked 5 review)
      landed "start" (walked 5 review)
      landed "end" review
      landed "move 7" review
      landed "move 70" review
      landed "move -3" review ]

report
    "'eye' turns through everyone and each seat, and round again, leaving the review where it stands"
    [ Eye.Seat(Seat.at 1), 4
      Eye.Seat(Seat.at 2), 4
      Eye.Everyone, 4
      Eye.Seat(Seat.at 2), 4
      Eye.Everyone, 4 ]
    ([ "eye next", review
       "eye next", Review.looking (Eye.Seat(Seat.at 1)) review
       "eye next", Review.looking (Eye.Seat(Seat.at 2)) review
       "eye back", review
       "eye all", Review.looking (Eye.Seat(Seat.at 2)) review ]
     |> List.map (fun (line, looking) ->
         match saying line (walked 4 looking) with
         | Ok next -> Review.eye next, (Review.place next).Move
         | Error(_, said) -> failwith said))

report
    "and 'eye 2' looks as the second seat, while a seat the game does not have is refused in words"
    (Ok(Eye.Seat(Seat.at 2)), Error "There is no seat '3' at this game. Say 'eye all', or a seat from 1 to 2.")
    (saying "eye 2" review |> Result.map Review.eye |> Result.mapError snd,
     saying "eye 3" review |> Result.map Review.eye |> Result.mapError snd)

report
    "a line that would be a move is refused in one sentence, and the review stays where it was"
    []
    [ for line in [ "place 9"; "9"; "undo"; "redo"; "resign"; "restart"; "players 2 7" ] do
          match saying line (walked 4 review) with
          | Error(next, "A review cannot be played. 'quit' leaves.") when (Review.place next).Move = 4 -> ()
          | Error(_, said) -> yield $"'{line}' was answered '{said}'"
          | Ok _ -> yield $"'{line}' moved the review" ]

report
    "and 'save' is told that nothing is written"
    (Error "A review writes nothing down. The record is left as it was.")
    (saying "save" review |> Result.mapError snd |> Result.map ignore)

report
    "'quit', 'back' and 'q' leave"
    [ true; true; true; true ]
    ([ "quit"; "back"; "q"; "menu" ]
     |> List.map (fun line ->
         match Review.said plain line review with
         | Said.Left -> true
         | _ -> false))

report
    "'view' with a way of drawing the game hands it back, since the view is the console's"
    [ true; false ]
    ([ "view plain"; "view fancy" ]
     |> List.map (fun line ->
         match Review.said plain line review with
         | Said.Looked _ -> true
         | _ -> false))

report
    "'history' lists every turn so far through the eye, and nobody else's secrets through a seat's"
    (true, false, true)
    (let history eye =
        match saying "history" (opened secretive reading |> Review.looking eye |> walked 9) with
        | Error(_, said) -> said
        | Ok _ -> ""

     let x = history (Eye.Seat(Seat.at 1))
     let everyone = history Eye.Everyone
     x.Contains "Turn 7", x.Contains "O: place 5 (secret)", everyone.Contains "O: place 5 (secret)")

// A view that says how it was asked to draw, rather than drawing.
let private telling =
    { plain with
        Board = fun margins seat _ -> $"curtain {margins.Curtain}, seat {PlayerId.value seat}, notes {margins.Notes}" }

report
    "as everyone the board is drawn for the first seat with the curtain lifted, and through a seat's eyes for that seat with it hanging"
    [ true; true; true ]
    [ (Review.drawn telling review).Contains "curtain Lifted, seat 1"
      (Review.drawn telling (Review.looking (Eye.Seat(Seat.at 2)) review)).Contains "curtain Hanging, seat 2"
      (Review.drawn telling (Review.looking (Eye.Seat(Seat.at 1)) review)).Contains "curtain Hanging, seat 1" ]

report
    "the margins are this console's to change, as at a board"
    true
    (match saying "notes" review with
     | Ok next -> (Review.drawn telling next).Contains "notes False"
     | Error _ -> false)

report
    "the review is headed with the record, where it stands and through whose eyes"
    [ "Reviewing a game.log - the deal, with 7 turns and 9 moves to come - as everyone, with nothing hidden"
      "Reviewing a game.log - turn 5 of 7, move 6 of 9 - as O saw it" ]
    [ (Review.drawn plain review).Replace("\r\n", "\n").Split('\n')[0]
      (Review.drawn plain (walked 6 review |> Review.looking (Eye.Seat(Seat.at 2)))).Replace("\r\n", "\n").Split('\n')[0] ]

// Noughts and crosses has nothing to be asked, so this one is taught a question.
let private asked =
    { noughts with
        Read = fun line -> if line = "why" then Ok(Asking line) else noughts.Read line }

report
    "as everyone a question is answered as the first seat would be, and says so; through a seat's eyes, as that seat"
    (true, true, true)
    (let answered review =
        match Review.said plain "why" review with
        | Said.Printed(_, answer) -> answer
        | _ -> ""

     let question = opened asked reading

     (match saying "help" review with
      | Error(_, help) -> help.Contains "A review walks a record"
      | Ok _ -> false),
     (answered question).StartsWith "As X saw it:",
     answered (Review.looking (Eye.Seat(Seat.at 2)) question) = plain.Answer (Seat.at 2) "why" (Review.model question))


// --- the rows it is steered by --------------------------------------------------------------------

let private rowLines (steered: Keys.Steered) =
    steered.Screen.Rows
    |> List.collect (fun row ->
        (match row.Pick with
         | Keys.Sends line -> [ line ]
         | _ -> [])
        @ (match row.Turns with
           | Some turn -> [ turn -1; turn 1 ]
           | None -> []))

report
    "every row stands for a line the review reads, and none of them is a move"
    []
    [ for line in rowLines (Review.steered plain (walked 4 review)) do
          match Review.said plain line (walked 4 review) with
          | Said.Printed(_, said) -> yield $"'{line}' was answered '{said}'"
          | Said.Moved _
          | Said.Looked _
          | Said.Left -> () ]

report
    "at a game with no pages of its own there is no page to turn, nor any row for it, and nothing ever seals"
    (false, false, false)
    (let steered = Review.steered plain review
     steered.Pages.IsSome, steered.Screen.Rows |> List.exists (fun row -> row.Says = "Screen"), steered.Seals.IsSome)

// A game that turns between two screens of its own, by name, for whichever seat is drawn.
let private paged =
    { noughts with
        Steering =
            fun _ margins _ _ ->
                Some
                    { Keys.steered
                          { Title = ""
                            Prose = []
                            Rows = []
                            Note = []
                            Backs = None } with
                        Pages = Some(fun by -> if Margins.showing "board" margins = "board" then "rules" else "board")
                        Seals = Some "pass" } }

report
    "a game that turns its own pages has them turned from the review, onto the screen it names, and still nothing seals"
    (true, true, false, Some "rules")
    (let shown = opened paged reading
     let steered = Review.steered plain shown
     let turned = steered.Pages |> Option.map (fun turn -> turn 1)

     steered.Pages.IsSome, steered.Screen.Rows |> List.exists (fun row -> row.Says = "Screen"), steered.Seals.IsSome, turned)

Checks.finish ()
