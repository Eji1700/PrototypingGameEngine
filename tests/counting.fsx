#load "Checks.fsx"

#load "../src/Common/Result.fs"
#load "../src/Common/Cascade.fs"
#load "../src/Common/Random.fs"
#load "../src/Common/Counting.fs"
#load "../src/Engine/Seats.fs"
#load "../src/Engine/Messages.fs"
#load "../src/Engine/Told.fs"
#load "../src/Engine/Rules.fs"
#load "../src/Engine/Timeline.fs"
#load "../src/Engine/Journal.fs"
#load "../src/Engine/Model.fs"
#load "../src/Engine/Update.fs"
#load "../src/Engine/Machines.fs"

// The three games that count things, as far as the words they count them in. Loaded here rather
// than through their own harnesses because those compile a copy of the engine apiece, and a check
// that holds three games against one another needs the one engine underneath all of them.
#load "../src/Games/Cascade/Rules/Board.fs"
#load "../src/Games/Cascade/Rules/Session.fs"
#load "../src/Games/Cascade/Rules/Turn.fs"
#load "../src/Games/Cascade/Rules/Words.fs"
#load "../src/Games/Life/Rules/Grid.fs"
#load "../src/Games/Life/Rules/World.fs"
#load "../src/Games/Life/Rules/Turn.fs"
#load "../src/Games/Life/Rules/Words.fs"
#load "../src/Games/Snake/Rules/Board.fs"
#load "../src/Games/Snake/Rules/Snakes.fs"
#load "../src/Games/Snake/Rules/Session.fs"
#load "../src/Games/Snake/Rules/Turn.fs"
#load "../src/Games/Snake/Rules/Words.fs"

open System.Text.RegularExpressions
open TCModel.Common
open TCModel.Engine
open Checks


// A count and the noun beside it have to agree, and the two ends of the range are where they stop
// agreeing: nought and one. Every game used to write its own "1 turn"/"3 turns", and what that
// bought was "1 cell are still turning", "leaves 1 seat(s)" and "has turned 1 turn". The counters
// are shared now, and this is what says so - at the boundaries, in every game that counts.


// --- the shared counter itself ---------------------------------------------------------------

let private turns = Counting.several "turn" "turns"

report "one of a thing is singular" "1 turn" (turns 1)
report "and none of it is plural" "0 turns" (turns 0)
report "as is two" "2 turns" (turns 2)

// A count is read as its size. Diplomacy carries what a power owes as a negative when it is short,
// and a shortfall of two units is still "2 units".
report "a count is read as its size" "2 turns" (turns -2)
report "which does not cost the singular" "1 turn" (turns -1)

let private touches = Counting.orNone "no touches" "touch" "touches"

report "a nothing of its own is used where there is one" "no touches" (touches 0)
report "and the ordinary words either side of it" "1 touch" (touches 1)
report "and above" "3 touches" (touches 3)

let private stones = Counting.a "stone" "stones"

report "one worth naming rather than counting is named" "a stone" (stones 1)
report "and more than one is counted" "4 stones" (stones 4)
report "and none of it is counted rather than named" "0 stones" (stones 0)


// --- and every counter the games kept for themselves -------------------------------------------

let private counters =
    [ "Cascade cells", TCModel.Cascade.Words.cells
      "Cascade turns", TCModel.Cascade.Words.turns
      "Cascade touches", TCModel.Cascade.Words.touches
      "Cascade waves", TCModel.Cascade.Words.waves
      "Life cells", TCModel.Life.Words.cells
      "Life generations", TCModel.Life.Words.generations
      "Snake segments", TCModel.Snake.Words.segments
      "Snake steps", TCModel.Snake.Words.steps
      "Snake eaten", TCModel.Snake.Words.eaten ]

/// A one standing against a plural, which is what every one of these bugs has been. The nouns are
/// named rather than matched as "any word ending in s", because "1 this" and "1 has" are neither
/// of them a count. Add to it when a game starts counting something new.
let private counted =
    [ "cells"
      "turns"
      "touches"
      "waves"
      "generations"
      "segments"
      "steps"
      "pieces"
      "squares"
      "rows"
      "columns"
      "moves"
      "players"
      "seats"
      "stones"
      "cards"
      "lines"
      "units"
      "builds"
      "games"
      "tables"
      "centres"
      "protocols" ]

let private disagrees (text: string) =
    Regex.IsMatch(text, @"\b1 (" + String.concat "|" counted + @")\b")

let private amiss said =
    [ for name, counter in counters do
          for n in 0..3 do
              if said (counter n) then $"{name} at {n}: '{counter n}'" ]

report "no counter says one of a plural" [] (amiss disagrees)

report "and none of them says nothing at all" [] (amiss (fun text -> text = ""))


// --- every line the counting games refuse in, at the boundaries --------------------------------

/// Every refusal the three of them can make that carries a count, at nought, one and two. Written
/// out by hand rather than generated, so a refusal added later has to be added here too - which is
/// the point, since a refusal nobody read back is exactly how "1 cell are still turning" got in.
let private refusals =
    [ for n in 0..2 do
          "Cascade StillTurning", TCModel.Cascade.Words.said (TCModel.Cascade.Refused(TCModel.Cascade.StillTurning n))
          "Cascade NoSuchSpeed", TCModel.Cascade.Words.said (TCModel.Cascade.Refused(TCModel.Cascade.NoSuchSpeed n))
          "Life NoSuchRun", TCModel.Life.Words.said (TCModel.Life.Refused(TCModel.Life.NoSuchRun n))
          "Life NothingWouldChange", TCModel.Life.Words.said (TCModel.Life.Refused(TCModel.Life.NothingWouldChange n))
          "Snake NoSuchSpeed", TCModel.Snake.Words.said (TCModel.Snake.Refused(TCModel.Snake.NoSuchSpeed n))

      "Cascade NoneLeft", TCModel.Cascade.Words.said (TCModel.Cascade.Refused TCModel.Cascade.NoneLeft)
      "Life NothingLeft", TCModel.Life.Words.said (TCModel.Life.Refused TCModel.Life.NothingLeft) ]

report
    "no refusal any of them makes puts a one against a plural"
    []
    (refusals
     |> List.filter (snd >> disagrees)
     |> List.map (fun (which, said) -> $"{which}: '{said}'"))

report "nor does any of them come out empty" [] (refusals |> List.filter (snd >> (=) "") |> List.map fst)


// --- nor the counts a game reads out as it goes ------------------------------------------------

let private tally n : TCModel.Cascade.Tally =
    { Touches = n
      Rotations = n
      Lines = n
      Squares = n }

let private said =
    [ for n in 0..2 do
          TCModel.Cascade.Words.said (TCModel.Cascade.Happened(TCModel.Cascade.GameEnded(tally n)))
          TCModel.Cascade.Words.said (TCModel.Cascade.Happened(TCModel.Cascade.GaveIn n))
          TCModel.Cascade.Words.said (TCModel.Cascade.Happened(TCModel.Cascade.CameUp(TCModel.Cascade.Rank 1, n)))
          TCModel.Life.Words.said (TCModel.Life.Happened(TCModel.Life.Ran(n, n, n)))
          TCModel.Life.Words.said (TCModel.Life.Happened(TCModel.Life.Swept n))
          TCModel.Snake.Words.said (TCModel.Snake.Happened(TCModel.Snake.Ate(Seat.at 1, n, n))) ]

report "nor anything a game reads out as it plays" [] (said |> List.filter disagrees)


finish ()
