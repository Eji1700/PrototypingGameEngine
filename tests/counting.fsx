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

// The games that count things, as far as the words they count them in. Loaded here rather than
// through their own harnesses because those compile a copy of the engine apiece, and a check that
// holds several games against one another needs the one engine underneath all of them.
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
#load "../src/Games/Warband/Rules/Formation.fs"
#load "../src/Games/Warband/Rules/Kinds.fs"
#load "../src/Games/Warband/Rules/Squads.fs"
#load "../src/Games/Warband/Rules/Session.fs"
#load "../src/Games/Warband/Rules/Events.fs"
#load "../src/Games/Warband/Rules/Battle.fs"
#load "../src/Games/Warband/Rules/Turn.fs"
#load "../src/Games/Warband/Rules/Words.fs"

open System.Text.RegularExpressions
open Prototyping.Common
open Prototyping.Engine
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
    [ "Cascade cells", Prototyping.Cascade.Words.cells
      "Cascade turns", Prototyping.Cascade.Words.turns
      "Cascade touches", Prototyping.Cascade.Words.touches
      "Cascade waves", Prototyping.Cascade.Words.waves
      "Life cells", Prototyping.Life.Words.cells
      "Life generations", Prototyping.Life.Words.generations
      "Snake segments", Prototyping.Snake.Words.segments
      "Snake steps", Prototyping.Snake.Words.steps
      "Snake eaten", Prototyping.Snake.Words.eaten
      "Warband units", Prototyping.Warband.Words.units
      "Warband hexes", Prototyping.Warband.Words.hexes
      "Warband rounds", Prototyping.Warband.Words.rounds
      "Warband blows", Prototyping.Warband.Words.blows ]

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
      "protocols"
      "hexes"
      "rounds"
      "blows" ]

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
          "Cascade StillTurning", Prototyping.Cascade.Words.said (Prototyping.Cascade.Refused(Prototyping.Cascade.StillTurning n))
          "Cascade NoSuchSpeed", Prototyping.Cascade.Words.said (Prototyping.Cascade.Refused(Prototyping.Cascade.NoSuchSpeed n))
          "Life NoSuchRun", Prototyping.Life.Words.said (Prototyping.Life.Refused(Prototyping.Life.NoSuchRun n))
          "Life NothingWouldChange", Prototyping.Life.Words.said (Prototyping.Life.Refused(Prototyping.Life.NothingWouldChange n))
          "Snake NoSuchSpeed", Prototyping.Snake.Words.said (Prototyping.Snake.Refused(Prototyping.Snake.NoSuchSpeed n))

      "Cascade NoneLeft", Prototyping.Cascade.Words.said (Prototyping.Cascade.Refused Prototyping.Cascade.NoneLeft)
      "Life NothingLeft", Prototyping.Life.Words.said (Prototyping.Life.Refused Prototyping.Life.NothingLeft)

      for n in 0..2 do
          "Warband NoSuchGround", Prototyping.Warband.Words.said (Prototyping.Warband.Refused(Prototyping.Warband.NoSuchGround n))

      // Warband counts a kind in its own plural, so this one is read back once for every kind
      // there is rather than at nought, one and two.
      for kind in Prototyping.Warband.Kinds.all do
          $"Warband TooAlike {Prototyping.Warband.Kinds.name kind}",
          Prototyping.Warband.Words.said (Prototyping.Warband.Refused(Prototyping.Warband.TooAlike(1, kind))) ]

report
    "no refusal any of them makes puts a one against a plural"
    []
    (refusals
     |> List.filter (snd >> disagrees)
     |> List.map (fun (which, said) -> $"{which}: '{said}'"))

report "nor does any of them come out empty" [] (refusals |> List.filter (snd >> (=) "") |> List.map fst)


// --- nor the counts a game reads out as it goes ------------------------------------------------

let private tally n : Prototyping.Cascade.Tally =
    { Touches = n
      Rotations = n
      Lines = n
      Squares = n }

let private said =
    [ for n in 0..2 do
          Prototyping.Cascade.Words.said (Prototyping.Cascade.Happened(Prototyping.Cascade.GameEnded(tally n)))
          Prototyping.Cascade.Words.said (Prototyping.Cascade.Happened(Prototyping.Cascade.GaveIn n))
          Prototyping.Cascade.Words.said (Prototyping.Cascade.Happened(Prototyping.Cascade.CameUp(Prototyping.Cascade.Rank 1, n)))
          Prototyping.Life.Words.said (Prototyping.Life.Happened(Prototyping.Life.Ran(n, n, n)))
          Prototyping.Life.Words.said (Prototyping.Life.Happened(Prototyping.Life.Swept n))
          Prototyping.Snake.Words.said (Prototyping.Snake.Happened(Prototyping.Snake.Ate(Seat.at 1, n, n)))
          Prototyping.Warband.Words.atLength (Prototyping.Warband.Strikes(n, n, n))
          Prototyping.Warband.Words.atLength (Prototyping.Warband.Shoots(n, n, n))
          Prototyping.Warband.Words.ending (Prototyping.Warband.Outlasted n)
          Prototyping.Warband.Words.ground n
          Prototyping.Warband.Words.said (Prototyping.Warband.Happened(Prototyping.Warband.GroundSet n))

          Prototyping.Warband.Words.said (
              Prototyping.Warband.Happened(
                  Prototyping.Warband.Unreached(
                      1,
                      { Rank = Prototyping.Warband.Front
                        Step = 1 },
                      Prototyping.Warband.Footman,
                      n
                  )
              )
          ) ]

report "nor anything a game reads out as it plays" [] (said |> List.filter disagrees)


finish ()
