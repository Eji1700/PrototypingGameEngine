// Checks the game's memory of itself: taking moves back, making them again, and the
// record that survives both.
//
//   dotnet fsi tests/history.fsx

#load "Harness.fsx"
#load "../src/Console/Words.fs"
#load "../src/Console/Parse.fs"
#load "../src/Console/Transcript.fs"

open TCModel.Domain
open TCModel.Engine
open TCModel.Console
open Harness

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

let written = Transcript.write walked.Journal

let read = Transcript.read written |> Result.toOption |> Option.get

report "a written record says who was dealt in" (2, 42UL) (read.Players, read.Seed)

report "and carries every move, undo included" (Journal.moves walked.Journal) read.Moves

let fromFile =
    Playing.replay read.Players read.Seed read.Moves
    |> Result.toOption
    |> Option.get

report "a game read back from its file is the same game" (Playing.session walked) (Playing.session fromFile)

report "state for state" (states walked) (states fromFile)

report "every line of a record is either a comment or a move a player could type" true (Transcript.read written |> Result.isOk)

finish ()
