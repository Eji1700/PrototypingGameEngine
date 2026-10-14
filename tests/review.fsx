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

kept
|> Review.stepped 99
|> Review.turned -2
|> Review.looking (Eye.Seat(Seat.at 2))
|> Review.thisTurn
|> ignore

report "nothing under logs/ is written, moved or changed by reviewing one of them" before (everything ())

Checks.finish ()
