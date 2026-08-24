namespace Prototyping.Diplomacy

open Prototyping.Engine

type Season =
    | Spring
    | Autumn

type Stage =
    | Moving of Season
    | Falling of Season
    | Building

type Ending =
    | Solo of Power * centres: int
    | LastStanding of Power
    | Deserted

type Passing =
    { Was: Stage
      Year: int
      Reports: Report list
      Retreated: (Piece * Location) list
      Scattered: Piece list
      Built: Piece list
      Removed: Piece list
      Changed: (ProvinceId * Power * Power option) list
      Eliminated: Power list }

type Play =
    { Year: int
      Stage: Stage
      Board: Position

      Written: Map<ProvinceId, Instruction>

      Sealed: Set<Power>

      Beaten: Retreating list

      Contested: Set<ProvinceId>

      Adrift: Set<Power>

      Last: Passing list

      Turn: int }

type Session =
    | InPlay of Play
    | Finished of Play * Ending

module Session =

    [<Literal>]
    let Victory = 18

    [<Literal>]
    let private FirstYear = 1901


    let play =
        function
        | InPlay play -> play
        | Finished(play, _) -> play

    let board session = (play session).Board

    let turn session = (play session).Turn

    let isOver =
        function
        | InPlay _ -> false
        | Finished _ -> true

    let ending =
        function
        | InPlay _ -> None
        | Finished(_, ending) -> Some ending

    let seatOf = Power.seatOf

    let powerAt = Power.atSeat

    let owed power position =
        let centres, units = Position.counts power position
        centres - units

    let hasSomethingToDo power play =
        if Set.contains power play.Adrift || Position.isOut power play.Board then
            false
        else
            match play.Stage with
            | Moving _ -> Position.unitsOf power play.Board |> List.isEmpty |> not
            | Falling _ -> play.Beaten |> List.exists (fun beaten -> beaten.Piece.Power = power)
            | Building ->
                match owed power play.Board with
                | 0 -> false
                | owing when owing < 0 -> true
                | _ -> Orders.buildable power play.Board |> List.isEmpty |> not

    let awaited play =
        Power.all
        |> List.filter (fun power -> hasSomethingToDo power play && not (Set.contains power play.Sealed))

    let active session =
        match session with
        | Finished _ -> Seat.at 1
        | InPlay play ->
            match awaited play with
            | power :: _ -> seatOf power
            | [] -> Seat.at 1


    /// How many steps a province is from the nearest of a power's home centres, walked over land and
    /// sea alike. Only used to order units for removal, so somewhere unreachable answering 99 is
    /// exactly right: it goes first.
    let private fromHome power province =
        let homes = Atlas.homesOf power |> Set.ofList

        let rec walk seen edge steps =
            match edge with
            | [] -> 99
            | _ when edge |> List.exists (fun place -> Set.contains place homes) -> steps
            | _ ->
                let next =
                    edge
                    |> List.collect (fun place ->
                        Atlas.armyReach place
                        @ (Atlas.fleetReach { At = place; Coast = None }
                           |> List.map (fun there -> there.At)))
                    |> List.distinct
                    |> List.filter (fun place -> not (Set.contains place seen))

                match next with
                | [] -> 99
                | _ -> walk (List.fold (fun seen place -> Set.add place seen) seen next) next (steps + 1)

        walk (Set.singleton province) [ province ] 0

    /// Which units go when a power owes removals and has not said which. Furthest from home first,
    /// fleets before armies at the same distance, and the province code to settle the rest - so it
    /// is the same list every time and a replay comes out the same.
    let private givenUp power position howMany =
        Position.unitsOf power position
        |> List.sortBy (fun piece ->
            -(fromHome power piece.Where.At),
            (match piece.Kind with
             | Fleet -> 0
             | Army -> 1),
            Atlas.code piece.Where.At)
        |> List.truncate howMany


    let private nothingHappened stage year =
        { Was = stage
          Year = year
          Reports = []
          Retreated = []
          Scattered = []
          Built = []
          Removed = []
          Changed = []
          Eliminated = [] }

    let private harvested play =
        let before = play.Board
        let after = Position.harvest before

        let changed =
            after.Owners
            |> Map.toList
            |> List.choose (fun (province, owner) ->
                match Position.ownerOf province before with
                | Some was when was = owner -> None
                | was -> Some(province, owner, was))
            |> List.sortBy (fun (province, _, _) -> Atlas.code province)

        let goneOut =
            Power.all
            |> List.filter (fun power -> not (Position.isOut power before) && Position.isOut power after)

        { play with Board = after }, changed, goneOut

    let private decided play =
        let standing =
            Position.stillIn play.Board
            |> List.filter (fun power -> not (Set.contains power play.Adrift))

        match
            Power.all
            |> List.tryFind (fun power -> List.length (Position.centresOf power play.Board) >= Victory)
        with
        | Some winner -> Some(Solo(winner, List.length (Position.centresOf winner play.Board)))
        | None ->
            match standing with
            | [] -> Some Deserted
            | [ only ] -> Some(LastStanding only)
            | _ -> None

    /// Opening a stage, with every power that has nothing to do in it already sealed. A power with no
    /// retreat to make is not waited on, and a stage nobody is waited on for passes straight through.
    let private entering stage play =
        let play =
            { play with
                Stage = stage
                Written = Map.empty
                Sealed = Set.empty
                Turn = play.Turn + 1 }

        { play with
            Sealed =
                Power.all
                |> List.filter (fun power -> not (hasSomethingToDo power play))
                |> Set.ofList }

    let private nextAfter season =
        if season = Spring then Moving Autumn else Building

    /// Working the game forward from a stage everybody has sealed. It loops rather than settling one
    /// stage and stopping, because settling one often opens another that nobody has anything to say
    /// in - a season with no dislodgements skips the retreats, a winter where every power is square
    /// skips the building - and the game should come back to the players at the next stage that
    /// actually wants them.
    let rec private through play (told: Passing list) =
        match decided play with
        | Some finish -> Finished(play, finish), List.rev told
        | None ->

        let resolved, passing, next =
            match play.Stage with
            | Moving season ->
                let resolution = Adjudicate.outcome play.Board play.Written

                { play with
                    Board = resolution.Position
                    Beaten = resolution.Retreats
                    Contested = resolution.Contested },
                { nothingHappened (Moving season) play.Year with
                    Reports = resolution.Reports },
                (if List.isEmpty resolution.Retreats then nextAfter season else Falling season)

            | Falling season ->
                let board, survivors, scattered =
                    Adjudicate.retreat play.Board play.Beaten play.Written

                { play with Board = board; Beaten = [] },
                { nothingHappened (Falling season) play.Year with
                    Retreated = survivors |> List.map (fun (who, into) -> who.Piece, into)
                    Scattered = scattered |> List.map (fun who -> who.Piece) },
                nextAfter season

            | Building ->
                let built =
                    play.Written
                    |> Map.toList
                    |> List.choose (fun (province, says) ->
                        match says with
                        | Builds(kind, coast) ->
                            Position.ownerOf province play.Board
                            |> Option.map (fun power ->
                                { Power = power
                                  Kind = kind
                                  Where = Atlas.standing kind province coast })
                        | _ -> None)

                let said =
                    play.Written
                    |> Map.toList
                    |> List.choose (fun (province, says) ->
                        match says with
                        | Disbands -> Position.at province play.Board
                        | _ -> None)

                let short =
                    Power.all
                    |> List.collect (fun power ->
                        let owing = -(owed power play.Board)
                        let named = said |> List.filter (fun piece -> piece.Power = power) |> List.length

                        if owing > named then
                            givenUp power play.Board owing
                            |> List.filter (fun piece -> not (List.contains piece said))
                            |> List.truncate (owing - named)
                        else
                            [])

                let removed = said @ short |> List.distinct

                let board =
                    removed
                    |> List.fold (fun board piece -> Position.remove piece.Where.At board) play.Board
                    |> fun board -> built |> List.fold (fun board piece -> Position.add piece board) board

                { play with
                    Board = board
                    Year = play.Year + 1 },
                { nothingHappened Building play.Year with
                    Built = built
                    Removed = removed },
                Moving Spring

        // Centres change hands on the autumn count only, which is to say on the way into a winter.
        let resolved, passing =
            if next = Building then
                let after, changed, goneOut = harvested resolved

                after,
                { passing with
                    Changed = changed
                    Eliminated = goneOut }
            else
                resolved, passing

        let after = entering next resolved
        let told = passing :: told

        match decided after with
        | Some finish -> Finished(after, finish), List.rev told
        | None when List.isEmpty (awaited after) -> through after told
        | None -> InPlay after, List.rev told

    let resolveNow play =
        let session, told = through play []

        let remembering play = { play with Last = told }

        (match session with
         | InPlay play -> InPlay(remembering play)
         | Finished(play, ending) -> Finished(remembering play, ending)),
        told

    let seal power play =
        let play =
            { play with
                Sealed = Set.add power play.Sealed }

        if List.isEmpty (awaited play) then resolveNow play else InPlay play, []

    let walkAway power play =
        let play =
            { play with
                Adrift = Set.add power play.Adrift }

        if List.isEmpty (awaited play) then resolveNow play else InPlay play, []


    let dealt =
        let opening =
            { Year = FirstYear
              Stage = Moving Spring
              Board = Position.dealt
              Written = Map.empty
              Sealed = Set.empty
              Beaten = []
              Contested = Set.empty
              Adrift = Set.empty
              Last = []
              Turn = 1 }

        InPlay
            { opening with
                Sealed =
                    Power.all
                    |> List.filter (fun power -> not (hasSomethingToDo power opening))
                    |> Set.ofList }
