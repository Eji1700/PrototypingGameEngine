namespace TCModel.Warband

open TCModel.Engine

type Ending =
    | Broke of winner: int * loser: int
    | Outlasted of winner: int
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

type Play =
    {
        Squads: Map<int, Squad>

        // `Stage` rather than `Phase`: a `Margins` has a `Phase`, and F# resolves a field on an
        // un-annotated value by name alone, so the clash would silently retype half of `Render`.
        Stage: Phase
        /// Whether the battle runs on its own. The clock beats either way; a battle that is stopped
        /// answers a beat with nothing, which the engine leaves out of the record.
        Running: bool
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

    let places = [ 1..Seats ]

    let other place = if place = 1 then 2 else 1

    let dealt =
        { Squads = places |> List.map (fun place -> place, Squad.empty) |> Map.ofList
          Stage = Mustering 1
          Running = true
          Turn = 0 }

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
    /// once both squads are on the field. The first seat stands in for nobody there, as it does in
    /// the two other games that reach a point with nothing owed to anybody.
    ///
    /// Naming the side whose unit swings next would read better in the history and would let
    /// either squad give up mid-battle. It was tried, and it is wrong: one keyboard draws the
    /// board for whoever is active, so the field turned over every single blow and the log changed
    /// sides under the person reading it. Nothing is given up mid-battle now either - `Turn`
    /// refuses it in words, because a battle already settled is not a thing anybody can concede.
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

    /// The order the standing units act in this round: the quickest first, and a tie broken by
    /// which squad the round favours - the first on odd rounds and the second on even ones, so
    /// neither of them is always the one that swings first. Everything after that is the board
    /// itself: front rank before middle, and left to right within a rank.
    let order round play =
        [ for place in places do
              for hex, unit in Squad.standing (squadOf place play) -> place, hex, unit.Kind ]
        |> List.sortBy (fun (place, hex, kind) ->
            -Kinds.quick kind, (if place = (if round % 2 = 1 then 1 else 2) then 0 else 1), Formation.depth hex.Rank, hex.Step)
        |> List.map (fun (place, hex, _) -> place, hex)

    let standingAt place play =
        Squad.standing (squadOf place play) |> List.length
