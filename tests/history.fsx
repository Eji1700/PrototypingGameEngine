#load "Whole.fsx"

open Prototyping.Turncoats
open Prototyping.Engine
open Prototyping.Table
open Harness
open Whole

let private at n = Board.tryId n |> Option.get

let private start () =
    Playing.start 2 42UL |> Result.toOption |> Option.get

let private held model =
    (Game.active (Playing.game model)).Bag |> Pile.toColours |> List.head

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

let private drawn model =
    match Playing.session model with
    | InPlay { Phase = AwaitingReturn drawn } -> Some drawn
    | _ -> None

let negotiated = Playing.update (Make Negotiate) (playOn 1 (start ()))

report "undo is time travel, not a re-roll" (drawn negotiated) (drawn (send [ Undo; Redo ] negotiated))


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

let branched = send [ Undo; Undo; nextMove (send [ Undo; Undo ] after) ] after

report "a new move clears what was taken back" 0 (Timeline.movesTakenBack branched.Timeline)


let private states model = Timeline.states model.Timeline

let walked = send [ Undo; Undo; Redo ] (playOn 9 (start ()))

let replayed =
    Playing.replay (Model.players walked) (Model.seed walked) (Journal.moves walked.Journal)
    |> Result.toOption
    |> Option.get

report "playing the record again ends where it left off" (Playing.session walked) (Playing.session replayed)

report "and passes through the same states on the way" (states walked) (states replayed)

report "and writes down the same record" (Journal.moves walked.Journal) (Journal.moves replayed.Journal)


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

// Read as a person would read the file rather than through the reader that already took it: past
// the comments and the two lines that say what was dealt, every line is one the prompt takes.
let private preamble (line: string) =
    line.StartsWith "format " || line.StartsWith "deal "

report
    "every line of a record is either a comment or a move a player could type"
    []
    (written.Split '\n'
     |> List.ofArray
     |> List.map (fun line -> line.Trim())
     |> List.filter (fun line -> line <> "" && not (line.StartsWith "#") && not (preamble line))
     |> List.filter (fun line ->
         match Playable.read playing line with
         | Ok(Send _) -> false
         | _ -> true))


report "and says who was in each seat" seating read.Sitters

report "so the machines sit back down where they were" [ None; Some "hard" ] (Seating.machines read.Sitters)


report
    "a game put down here can be taken up as a table others join"
    [ Elsewhere; Machine "hard"; Elsewhere ]
    (Seating.resuming Elsewhere [ Here; Machine "hard"; Here ])

report
    "and one put down there taken up here"
    [ Here; Machine "hard"; Here ]
    (Seating.resuming Here [ Elsewhere; Machine "hard"; Elsewhere ])


report
    "a record written before seatings were says everybody was a person"
    (Ok [ Here; Here ])
    (Transcript.read playing "deal 2 42" |> Result.map (fun read -> read.Sitters))

report
    "and a record that seats more than it deals is refused"
    true
    (match Transcript.read playing "deal 2 42 you you you" with
     | Error problem -> problem.Contains "deals 2" && problem.Contains "seats 3"
     | Ok _ -> false)


// The green faction was black, and written 'k', when the earliest records were played. Nothing
// writes either now, and a record is meant to replay for good.
let private readsAs line =
    match Playable.read playing line with
    | Ok(Send msg) -> Words.command msg
    | _ -> line

report
    "a record that says 'k' or 'black' for green still reads"
    [ "march g 3 2 1"; "recruit g 5" ]
    ([ "march k 3 2 1"; "recruit black 5" ] |> List.map readsAs)

let private earliest =
    System.IO.Path.Combine(__SOURCE_DIRECTORY__, "..", "logs", "2026-08-03-193909-turncoats-2p-seed639214079490240407.log")

report
    "and the record that marches a 'k' plays back whole"
    (Ok true)
    (Transcript.read playing (System.IO.File.ReadAllText earliest)
     |> Result.bind (fun read -> Playing.replay read.Players read.Seed read.Moves |> Result.map Playing.isOver))


let private filed = Transcript.path "2026-08-12-120000" walked.Journal

report "a record is filed under the stamp it was saved with" (Some "2026-08-12-120000") (Transcript.stampOf filed 2 42UL)

report "and one nobody can show is this game's is left alone" None (Transcript.stampOf "logs/somebody-renamed-this.log" 2 42UL)


// Saving writes a real record into logs/, the folder CI replays on every run. So it is stamped as
// the program stamps its own - the game's name in the middle, as a record the house would offer -
// at a moment no real game was played, and taken away again whatever happens in between.
let private stamp = Transcript.stamping playing.Name (System.DateTime(2000, 1, 1))

let private filedAt = Transcript.path stamp walked.Journal

let private savedTo journal =
    Transcript.save playing stamp seating journal |> ignore
    System.IO.File.ReadAllText filedAt

try
    savedTo (playOn 3 (start ())).Journal |> ignore

    report "a record saved again is the whole game and not two of them" written (savedTo walked.Journal)

    report "and nothing is left lying beside it" false (System.IO.File.Exists(filedAt + ".writing"))

    System.IO.File.AppendAllText(filedAt, "#   10  turn")

    report "a record torn off mid-save is written out whole the next time" written (savedTo walked.Journal)

    System.IO.File.WriteAllText(filedAt, "deal 2 42" + System.Environment.NewLine + "negotiate")

    report "and a file under this name that is some other game is not added to" written (savedTo walked.Journal)
finally
    for path in [ filedAt; filedAt + ".writing" ] do
        if System.IO.File.Exists path then System.IO.File.Delete path

report "and the record the checks wrote is taken away again" false (System.IO.File.Exists filedAt)


// === The shape of the file itself ===

// The engine is versioned apart from the games built on it, so a record written by one version
// will be read by another. These three are the whole of what that marker is for.

let private readBack (text: string) =
    Transcript.read playing text |> Result.map (fun read -> read.Players, read.Seed)

let private newline = System.Environment.NewLine

report "a record this build writes says which format it is in" true (written.Contains(newline + "format 1" + newline))

report "a record written before there was a marker is read as the format it is" (Ok(2, 42UL)) (readBack "deal 2 42\nnegotiate")

report
    "and one written by a later engine is refused rather than misread"
    (Error
        "That record is written in format 99, and this build reads up to 1. It was saved by a later version of the engine than this one.")
    (readBack "format 99\ndeal 2 42\nnegotiate")


finish ()
