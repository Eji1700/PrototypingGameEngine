// Checks the game's memory of itself: taking moves back, making them again, and the
// record that survives both.
//
//   dotnet fsi tests/history.fsx

#load "Whole.fsx"

open TCModel.Turncoats
open TCModel.Engine
open TCModel.Table
open Harness
open Whole

let private at n = Board.tryId n |> Option.get

let private start () =
    Playing.start 2 42UL |> Result.toOption |> Option.get

/// Some colour the player to act is holding.
let private held model =
    (Game.active (Playing.game model)).Bag |> Pile.toColors |> List.head

/// A move that is legal in the position in hand, so a run of them can be played without
/// knowing what was dealt. Recruiting and negotiating in turn keeps the game going: a
/// stone played breaks the run of negotiations, so the game never ends on its own.
let private nextMove model =
    match Playing.session model with
    | InPlay { Phase = AwaitingReturn drawn } -> Make(Settle drawn)
    | InPlay play when play.Turn % 2 = 0 -> Make Negotiate
    | InPlay _ -> Make(Recruit(held model, at 5))
    | Finished _ -> Undo

let rec private playOn n model =
    if n <= 0 then model else playOn (n - 1) (Playing.update (nextMove model) model)

let private send msgs model =
    msgs |> List.fold (fun model msg -> Playing.update msg model) model

// --- taking a move back -----------------------------------------------------

let before = playOn 6 (start ())
let move = nextMove before
let after = Playing.update move before

report "a move moves the game on" false (Playing.session before = Playing.session after)

report "taking it back puts the game back exactly" (Playing.session before) (Playing.session (Playing.update Undo after))

report "making it again puts it forward exactly" (Playing.session after) (Playing.session (send [ Undo; Redo ] after))

report "there is nothing to take back at the deal" (Playing.session (start ())) (Playing.session (Playing.update Undo (start ())))

report
    "walking all the way back reaches the deal"
    (Playing.session (start ()))
    (Playing.session (send (List.replicate 20 Undo) after))

// A negotiation carries the generator with it, so taking one back and making it again
// draws the same stone rather than rolling for a new one.
let private drawn model =
    match Playing.session model with
    | InPlay { Phase = AwaitingReturn drawn } -> Some drawn
    | _ -> None

let negotiated = Playing.update (Make Negotiate) (playOn 1 (start ()))

report "undo is time travel, not a re-roll" (drawn negotiated) (drawn (send [ Undo; Redo ] negotiated))

// --- what the record keeps --------------------------------------------------

let private recorded model = Journal.length model.Journal

report "the record grows with every move" 7 (recorded (playOn 7 (start ())))

report "taking a move back is itself written down" 8 (recorded (Playing.update Undo (playOn 7 (start ()))))

report
    "a refused move is written down too"
    9
    (recorded (Playing.update (Make(March(Red, at 13, at 14, 1))) (Playing.update Undo (playOn 7 (start ())))))

report
    "a refusal leaves the position alone"
    (Playing.session after)
    (Playing.session (Playing.update (Make(March(Red, at 13, at 14, 1))) after))

// A move made after taking one back closes off the road not taken.
let branched = send [ Undo; Undo; nextMove (send [ Undo; Undo ] after) ] after

report "a new move clears what was taken back" 0 (Timeline.movesTakenBack branched.Timeline)

// --- playing it again -------------------------------------------------------

/// Everything the game has been through, so two runs can be compared state by state and
/// not just at the end.
let private states model = Timeline.states model.Timeline

let walked = send [ Undo; Undo; Redo ] (playOn 9 (start ()))

let replayed =
    Playing.replay (Model.players walked) (Model.seed walked) (Journal.moves walked.Journal)
    |> Result.toOption
    |> Option.get

report "playing the record again ends where it left off" (Playing.session walked) (Playing.session replayed)

report "and passes through the same states on the way" (states walked) (states replayed)

report "and writes down the same record" (Journal.moves walked.Journal) (Journal.moves replayed.Journal)

// --- and again, through the file --------------------------------------------

let private seating = [ Here; Machine "hard" ]

let written = Transcript.write playing seating walked.Journal

let read = Transcript.read playing written |> Result.toOption |> Option.get

report "a written record says who was dealt in" (2, 42UL) (read.Players, read.Seed)

report "and carries every move, undo included" (Journal.moves walked.Journal) read.Moves

let fromFile =
    Playing.replay read.Players read.Seed read.Moves
    |> Result.toOption
    |> Option.get

report "a game read back from its file is the same game" (Playing.session walked) (Playing.session fromFile)

report "state for state" (states walked) (states fromFile)

report
    "every line of a record is either a comment or a move a player could type"
    true
    (Transcript.read playing written |> Result.isOk)

// --- and who was at it ---------------------------------------------------------
//
// A record has to say who was in each seat or it cannot be taken up again as the game it was.
// Everything else about a game comes back from the deal and the moves; a machine does not,
// because which seat it played and how well it played are decisions made before the deal and
// are written down nowhere else. A record without them replays into a table where every seat
// is suddenly yours.

report "and says who was in each seat" seating read.Sitters

report
    "so the machines sit back down where they were"
    [ None; Some "hard" ]
    (Seating.machines read.Sitters)

// And only the machines. Whether the people at a table are in this room or at their own
// machines is not a fact about the game - it is what `play`, `serve` and `host` mean - so a
// game put down at one keyboard can be taken up as a table others join, and the other way
// about, with the machines in the seats they were playing either way.

report
    "a game put down here can be taken up as a table others join"
    [ Elsewhere; Machine "hard"; Elsewhere ]
    (Seating.resuming Elsewhere [ Here; Machine "hard"; Here ])

report
    "and one put down there taken up here"
    [ Here; Machine "hard"; Here ]
    (Seating.resuming Here [ Elsewhere; Machine "hard"; Elsewhere ])

// Records were written before a seating was part of one, and they still play. The reading
// that cannot be wrong about them is that everybody at the table was a person: a machine put
// back at a seat it never played, or at a strength it never played, would be a different game.

report
    "a record written before seatings were says everybody was a person"
    (Ok [ Here; Here ])
    (Transcript.read playing "deal 2 42" |> Result.map (fun read -> read.Sitters))

// A seating a table would take, at a deal that does not match it. The count is said twice in
// a record - once as a number and once as a list of seats - so it can disagree with itself,
// and a record that does is not one to take a game up from.
report
    "and a record that seats more than it deals is refused"
    true
    (match Transcript.read playing "deal 2 42 you you you" with
     | Error problem -> problem.Contains "deals 2" && problem.Contains "seats 3"
     | Ok _ -> false)

// --- and the file it goes back into ----------------------------------------------
//
// One game, one record. A game taken up again goes on writing to the file it came out of
// rather than starting a second one beside it, and the way it knows which file that is, is
// the name it is already under - built in one place and taken apart in the same one.

let private filed = Transcript.path "2026-08-12-120000" walked.Journal

report
    "a record is filed under the stamp it was saved with"
    (Some "2026-08-12-120000")
    (Transcript.stampOf filed 2 42UL)

report
    "and one nobody can show is this game's is left alone"
    None
    (Transcript.stampOf "logs/somebody-renamed-this.log" 2 42UL)

finish ()
