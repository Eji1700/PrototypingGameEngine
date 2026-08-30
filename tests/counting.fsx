#load "Checks.fsx"

#load "../src/Common/Result.fs"
#load "../src/Common/Tiebreak.fs"
#load "../src/Common/Random.fs"
#load "../src/Common/Counting.fs"
#load "../src/Common/Grid.fs"
#load "../src/Common/Notch.fs"
#load "../src/Engine/Seats.fs"
#load "../src/Engine/Messages.fs"
#load "../src/Engine/Told.fs"
#load "../src/Engine/Rules.fs"
#load "../src/Engine/Timeline.fs"
#load "../src/Engine/Journal.fs"
#load "../src/Engine/Model.fs"
#load "../src/Engine/Update.fs"
#load "../src/Engine/Machines.fs"

// The table's own words, as far as the two parts of it that count in them: the verbs every game
// answers to, and the screen a table draws while it fills.
#load "../src/Table/Parts/Waiting.fs"
#load "../src/Table/Parts/Scene.fs"
#load "../src/Table/Parts/Commands.fs"

// The games that count things, as far as the words they count them in - each in the order its
// project compiles it. Loaded here rather than through their own harnesses because those compile a
// copy of the engine apiece, and a check that holds several games against one another needs the
// one engine underneath all of them.
#load "../src/Games/Cascade/Rules/Board.fs"
#load "../src/Games/Cascade/Rules/Session.fs"
#load "../src/Games/Cascade/Rules/Turn.fs"
#load "../src/Games/Cascade/Rules/Words.fs"
#load "../src/Games/Life/Rules/Torus.fs"
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
#load "../src/Games/Compile/Rules/Protocols.fs"
#load "../src/Games/Compile/Rules/Cards.fs"
#load "../src/Games/Compile/Rules/Effects.fs"
#load "../src/Games/Compile/Rules/Printed.fs"
#load "../src/Games/Compile/Rules/Field.fs"
#load "../src/Games/Compile/Rules/Drafting.fs"
#load "../src/Games/Compile/Rules/Session.fs"
#load "../src/Games/Compile/Rules/Events.fs"
#load "../src/Games/Compile/Rules/Resolving.fs"
#load "../src/Games/Compile/Rules/Turn.fs"
#load "../src/Games/Compile/Rules/Words.fs"
#load "../src/Games/Diplomacy/Rules/Powers.fs"
#load "../src/Games/Diplomacy/Rules/Atlas.fs"
#load "../src/Games/Diplomacy/Rules/Position.fs"
#load "../src/Games/Diplomacy/Rules/Orders.fs"
#load "../src/Games/Diplomacy/Rules/Adjudicate.fs"
#load "../src/Games/Diplomacy/Rules/Session.fs"
#load "../src/Games/Diplomacy/Rules/Turn.fs"
#load "../src/Games/Diplomacy/Rules/Words.fs"
#load "../src/Games/Turncoats/Rules/Stones.fs"
#load "../src/Games/Turncoats/Rules/Board.fs"
#load "../src/Games/Turncoats/Rules/Players.fs"
#load "../src/Games/Turncoats/Rules/Position.fs"
#load "../src/Games/Turncoats/Rules/Ruling.fs"
#load "../src/Games/Turncoats/Rules/Game.fs"
#load "../src/Games/Turncoats/Rules/Knowledge.fs"
#load "../src/Games/Turncoats/Rules/Events.fs"
#load "../src/Games/Turncoats/Rules/Actions.fs"
#load "../src/Games/Turncoats/Rules/Outcome.fs"
#load "../src/Games/Turncoats/Rules/Setup.fs"
#load "../src/Games/Turncoats/Rules/Turn.fs"
#load "../src/Games/Turncoats/Rules/Playing.fs"
#load "../src/Games/Turncoats/Rules/Words.fs"

open Prototyping.Common
open Prototyping.Engine
open Checks


// A count and the noun beside it have to agree, and the two ends of the range are where they stop
// agreeing: nought and one. Every game used to write its own "1 turn"/"3 turns", and what that
// bought was "1 cell are still turning", "leaves 1 seat(s)" and "has turned 1 turn". The counters
// are shared now, and this is what says so - at the boundaries, in every game that counts, and at
// the table itself.


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


// --- and every counter a game, or the table, keeps for itself ------------------------------------

let private counters =
    [ "Cascade cells", Prototyping.Cascade.Words.cells
      "Cascade turns", Prototyping.Cascade.Words.turns
      "Cascade touches", Prototyping.Cascade.Words.touches
      "Cascade waves", Prototyping.Cascade.Words.waves
      "Cascade squares", Prototyping.Cascade.Words.squares
      "Cascade whole rows or columns", Prototyping.Cascade.Words.wholeLines
      "Life cells", Prototyping.Life.Words.cells
      "Life generations", Prototyping.Life.Words.generations
      "Snake segments", Prototyping.Snake.Words.segments
      "Snake steps", Prototyping.Snake.Words.steps
      "Snake eaten", Prototyping.Snake.Words.eaten
      "Warband units", Prototyping.Warband.Words.units
      "Warband hexes", Prototyping.Warband.Words.hexes
      "Warband rounds", Prototyping.Warband.Words.rounds
      "Warband blows", Prototyping.Warband.Words.blows
      "Diplomacy centres", Prototyping.Diplomacy.Words.centresOf
      "Diplomacy units", Prototyping.Diplomacy.Words.unitsOf
      "Turncoats moves", Prototyping.Turncoats.Words.moves
      "Turncoats turns", Prototyping.Turncoats.Words.turns
      "Turncoats stones", (fun n -> Prototyping.Turncoats.Words.stonesOf n Prototyping.Turncoats.Red)
      "the table's players", Prototyping.Table.Commands.players ]

let private amiss said =
    [ for name, counter in counters do
          for n in 0..3 do
              if said (counter n) then $"{name} at {n}: '{counter n}'" ]

report "no counter says one of a plural" [] (amiss disagrees)

report "and none of them says nothing at all" [] (amiss (fun text -> text = ""))


// --- every line the games refuse in, at the boundaries ------------------------------------------

let private one = Seat.at 1

let private region n =
    Prototyping.Turncoats.Board.tryId n |> Option.get

/// Every refusal a game makes that carries a count, at nought, one and two. Written out by hand
/// rather than generated, so a refusal added later has to be added here too - which is the point,
/// since a refusal nobody read back is exactly how "1 cell are still turning" got in.
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
          Prototyping.Warband.Words.said (Prototyping.Warband.Refused(Prototyping.Warband.TooAlike(1, kind)))

      // Diplomacy carries what a power owes as a negative when it is short, so both signs.
      for n in -2 .. 2 do
          "Diplomacy ThatIsEnough",
          Prototyping.Diplomacy.Words.rejection (Prototyping.Diplomacy.ThatIsEnough(Prototyping.Diplomacy.Austria, n))

      for n in 0..2 do
          "Turncoats MoreDrivenThanAllowed",
          Prototyping.Turncoats.Words.rejection (
              Prototyping.Turncoats.MoreDrivenThanAllowed(region 3, Prototyping.Turncoats.Red, n)
          )

          "Turncoats NotEnoughToMarch",
          Prototyping.Turncoats.Words.rejection (
              Prototyping.Turncoats.NotEnoughToMarch(region 3, Prototyping.Turncoats.Red, n, n + 1)
          )

      // A table too small, refused by the game and by the table's own verbs: the two ways a
      // player count is turned away, at nought and one.
      for n in 0..1 do
          "Turncoats TooFewPlayers",
          (match Prototyping.Turncoats.Playing.start n 1UL with
           | Error said -> said
           | Ok _ -> $"a table of {n} was dealt")

          "the table's players",
          (match Prototyping.Table.Commands.tryPlayerCount (3, 5) (string n) with
           | Error said -> said
           | Ok _ -> $"a table of {n} was taken") ]

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

/// A table still filling up with `n` seats to come, for the line the table draws under it.
let private expected n : Prototyping.Table.Waiting list =
    [ for _ in 1..n ->
          { Player = one
            Expected = true
            Away = false
            Yours = false } ]

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
          )

          // Compile keeps its counters to itself, so they are held through the lines they come
          // out in: what a player draws, refreshes and holds over, and what a card's text asks for.
          Prototyping.Compile.Words.said (Prototyping.Compile.Happened(Prototyping.Compile.Drew(one, n)))
          Prototyping.Compile.Words.said (Prototyping.Compile.Happened(Prototyping.Compile.Refreshed(one, n, n)))
          Prototyping.Compile.Words.said (Prototyping.Compile.Happened(Prototyping.Compile.OverTheLimit(one, n)))
          Prototyping.Compile.Words.printing (Prototyping.Compile.Draw(Prototyping.Compile.Just n))
          Prototyping.Compile.Words.printing (Prototyping.Compile.Times(Prototyping.Compile.Just n, Prototyping.Compile.Discard))

          Prototyping.Diplomacy.Words.ending (Prototyping.Diplomacy.Solo(Prototyping.Diplomacy.Austria, n))

          Prototyping.Turncoats.Words.event (Prototyping.Turncoats.Marched(one, Prototyping.Turncoats.Red, region 3, region 2, n))

          Prototyping.Table.Scene.Filling.stillToCome (expected n) ]

report "nor anything a game, or the table, reads out as it plays" [] (said |> List.filter disagrees)

report "and none of that comes out empty either" [] (said |> List.filter ((=) ""))


finish ()
