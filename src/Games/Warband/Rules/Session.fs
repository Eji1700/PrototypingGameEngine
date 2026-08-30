namespace Prototyping.Warband

open Prototyping.Engine

type Ending =
    | Broke of winner: int * loser: int
    | Outlasted of winner: int

    /// Neither line could reach the other, so nothing was ever going to happen. `None` where there
    /// was nothing to choose between the two squads either.
    | Stood of winner: int option

    | Drawn
    | Walked of who: int

/// A round of the battle: which one it is, and the units still to act in it, in the order they act.
/// Emptying the list is what ends a round, so a unit felled before its turn simply never comes up.
type Fight =
    { Round: int
      Waiting: (int * Hex) list }

/// The two halves of the game, and the end of it. Mustering is played; fighting is watched.
type Phase =
    | Mustering of toPlace: int
    | Fighting of Fight
    | Ended of Ending

/// What the board is making a noise about after a move, and after no other. State rather than
/// something read out of the notices, for the reason Cascade keeps its own: a table reads it off
/// the position after every move, and after every undo and redo, so a sound left lying there
/// would be heard again after everything that followed it.
type Sounding =
    /// A muster placed, and the other squad waited on.
    | Waited

    /// The tenth placement: the muster is done and the lines are formed.
    | Formed

    | Blow
    | Settled

    /// Somebody walked away from the muster.
    | Abandoned

type Play =
    {
        Squads: Map<int, Squad>

        // `Stage` rather than `Phase`: a `Margins` has a `Phase`, and F# resolves a field on an
        // un-annotated value by name alone, so the clash would silently retype half of `Render`.
        Stage: Phase

        /// How many hexes of ground lie between the two front ranks. One is the lines touching,
        /// which is where a game is dealt and what every reach on the roster is enough for; wind it
        /// out and the roster thins from the far end - the spear and the charge go at two, the bow
        /// carries to four, and past that neither line can touch the other at all.
        ///
        /// The hexes in between are not drawn as hexes anybody stands on, and there is nothing in
        /// the rules that could put a unit there. They are ground.
        Engaged: int

        /// Whether the battle runs on its own. The clock beats either way; a battle that is stopped
        /// answers a beat with nothing, which the engine leaves out of the record.
        Running: bool

        Sounding: Sounding list
        Turn: int
    }

module Session =

    [<Literal>]
    let Seats = 2

    /// How long a battle may go on before it is settled on what is left standing. Two squads of
    /// five put each other down in two or three rounds, so this is a stop rather than a rule -
    /// but a pair of squads that are all menders and warders would otherwise stand there for ever.
    [<Literal>]
    let Rounds = 12

    /// The ground between the two lines, at its nearest and at its furthest. `Closest` is one
    /// because two lines cannot stand on the same hexes, and it is what a game is dealt at.
    /// `Furthest` is a single digit for no better reason than that a single digit is easy to type
    /// and easy to draw; it is one literal, and nothing else in the game reads it.
    [<Literal>]
    let Closest = 1

    [<Literal>]
    let Furthest = 9

    let places = [ 1..Seats ]

    let other place = if place = 1 then 2 else 1

    let dealt =
        { Squads = places |> List.map (fun place -> place, Squad.empty) |> Map.ofList
          Stage = Mustering 1
          Engaged = Closest
          Running = true
          Sounding = []
          Turn = 1 }

    let groundHolds hexes = hexes >= Closest && hexes <= Furthest

    let squadOf place play = Map.find place play.Squads

    let withSquad place squad play =
        { play with
            Squads = Map.add place squad play.Squads }

    let isOver play =
        match play.Stage with
        | Ended _ -> true
        | _ -> false

    let isMustering play =
        match play.Stage with
        | Mustering _ -> true
        | _ -> false

    let turn play = play.Turn

    let seats (_: Play) = Seats

    /// Whose turn it is - which through the battle is nobody's, since nobody is asked anything
    /// once both squads are on the field, so the first seat stands in. Naming the side about to
    /// swing was tried and taken out again: one keyboard draws the board for whoever is active, so
    /// the field turned over every blow. SEAM.md has the account, under what Warband found.
    let active play =
        match play.Stage with
        | Mustering place -> Seat.at place
        | Fighting _
        | Ended _ -> Seat.at 1

    /// Who places next: the other squad, unless it has its five already.
    let nextToPlace after play =
        match places |> List.filter (fun place -> not (Squad.full (squadOf place play))) with
        | [] -> None
        | left ->
            left
            |> List.tryFind ((<>) after)
            |> Option.defaultValue (List.head left)
            |> Some

    /// Whether a unit standing there has anything it could do this round. Mending and standing
    /// idle are always something - they happen inside your own formation - and a blow is only
    /// something if it will carry as far as the other line.
    let canAct engaged rank kind =
        match Kinds.stance rank kind with
        | Mends _
        | Idles -> true
        | stance -> Kinds.carries engaged stance

    /// How many of a squad are standing in a field with nothing they can do about the other one.
    /// The board says this; the log does not, which is the whole reason `order` leaves them out.
    let outranged place play =
        Squad.standing (squadOf place play)
        |> List.filter (fun (hex, unit) -> not (canAct play.Engaged hex.Rank unit.Kind))
        |> List.length

    /// The order the standing units act in this round: the quickest first, and a tie broken by
    /// which squad the round favours - the first on odd rounds and the second on even ones, so
    /// neither of them is always the one that swings first. Everything after that is the board
    /// itself: front rank before middle, and left to right within a rank.
    ///
    /// A unit whose blow will not reach the other line is left out. The rule that it cannot reach
    /// lives in `Battle`, where the blow does; leaving it out here is only so that winding the
    /// ground out does not fill the log with eight units a round saying nothing happened. What is
    /// standing there unable to help is on the board instead, where a standing fact belongs.
    let order round play =
        [ for place in places do
              for hex, unit in Squad.standing (squadOf place play) do
                  if canAct play.Engaged hex.Rank unit.Kind then yield place, hex, unit.Kind ]
        |> List.sortBy (fun (place, hex, kind) ->
            -Kinds.quick kind, (if place = (if round % 2 = 1 then 1 else 2) then 0 else 1), Formation.depth hex.Rank, hex.Step)
        |> List.map (fun (place, hex, _) -> place, hex)

    let standingAt place play =
        Squad.standing (squadOf place play) |> List.length

    /// Whether anybody on either side can still put a blow across the ground. Two lines wound far
    /// enough apart can neither of them touch the other, and a battle that would stand there for
    /// twelve rounds saying so is better said once.
    let anythingReaches play =
        places
        |> List.exists (fun place ->
            Squad.standing (squadOf place play)
            |> List.exists (fun (hex, unit) -> Kinds.carries play.Engaged (Kinds.stance hex.Rank unit.Kind)))
