namespace TCModel.Diplomacy

open TCModel.Engine

/// Which half of the year. Two movement phases, and only the second of them counts centres -
/// which is the whole reason a spring is worth spending on getting into position.
type Season =
    | Spring
    | Autumn

/// Where in the year the game is, and therefore what kind of order may be written.
///
/// A game of this is not a sequence of turns; it is a sequence of *phases*, and two of the
/// three of them are skipped most of the time. Nobody retreats in a season where nothing was
/// dislodged, and there is no building to do in a year where the map did not move.
type Stage =
    | Moving of Season
    | Falling of Season
    | Building

/// How it finished.
type Ending =
    /// Eighteen of the thirty-four, which is more than half and the only outright win there is.
    | Solo of Power * centres: int
    /// Everybody else is off the board or has walked away.
    | LastStanding of Power
    /// Everybody walked away. Not a rule of the game - a fact about a table with nobody at it.
    | Deserted

/// One phase resolved, in the terms the phase itself deals in. `Turn` turns this into things
/// to say; nothing here knows any English.
type Passing =
    {
        Was: Stage
        Year: int
        Reports: Report list
        /// Beaten units that went somewhere, and beaten units that had nowhere to go.
        Retreated: (Piece * Location) list
        Scattered: Piece list
        Built: Piece list
        Removed: Piece list
        /// A centre changing hands: which, to whom, and from whom.
        Changed: (ProvinceId * Power * Power option) list
        Eliminated: Power list
    }

/// A game still going.
type Play =
    {
        Year: int
        Stage: Stage
        Board: Position

        /// The orders written so far this phase, by the province they are for. Secret: what a
        /// seat may read of them is `Knowledge`'s business, and it is the whole reason this game
        /// is worth having beside the other two.
        Written: Map<ProvinceId, Instruction>

        /// Who has said their orders are final. When the last of them does, the phase resolves -
        /// which is what makes a game where everybody moves at once playable at one prompt.
        Sealed: Set<Power>

        /// Waiting to be told where to go, in a falling phase.
        Beaten: Retreating list

        /// Provinces two units bounced off each other in, which nobody may retreat into.
        Contested: Set<ProvinceId>

        /// Powers that walked away. Their units stand where they are and are taken off the board
        /// as they are pushed out - which is what a set of rules for this calls civil disorder,
        /// and is a great deal more honest than ending a game of seven because one of them left.
        Adrift: Set<Power>

        /// The phases that resolved to get here, kept so that every screen can show what all
        /// those orders came to.
        ///
        /// The log would not do. It holds the last dozen lines and a bloody autumn is thirty
        /// orders, and the one thing everybody wants to look at while writing the next set is
        /// the last set and what happened to it.
        Last: Passing list

        Turn: int
    }

type Session =
    | InPlay of Play
    | Finished of Play * Ending

module Session =

    /// Eighteen of thirty-four. The one number in this game that everybody knows.
    [<Literal>]
    let Victory = 18

    [<Literal>]
    let private FirstYear = 1901

    // --- reading where it stands -------------------------------------------------------------------

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

    /// What a power's centres come to against what it has on the board: positive is a build
    /// owed, negative is a unit that has to go.
    let owed power position =
        let centres, units = Position.counts power position
        centres - units

    /// Whether a power still has anything to answer for in this phase.
    ///
    /// The one question the whole flow of a year turns on. A phase is over when nobody has an
    /// answer left to give, and most phases most years have nobody in them at all: a spring
    /// where nothing was dislodged skips its falling phase entirely, and nobody stops to be
    /// asked.
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
                // Builds nobody has room for are waived without anybody being asked.
                | _ -> Orders.buildable power play.Board |> List.isEmpty |> not

    /// Who has still to answer, in seating order.
    let awaited play =
        Power.all
        |> List.filter (fun power -> hasSomethingToDo power play && not (Set.contains power play.Sealed))

    /// Whose turn it is - the next power that owes an answer.
    ///
    /// A finished game is answered with the first seat rather than with nothing. The engine
    /// asks this of every model it holds, including ones it has already stopped playing, and
    /// an answer that could be missing would put an option through every table above.
    let active session =
        match session with
        | Finished _ -> Seat.at 1
        | InPlay play ->
            match awaited play with
            | power :: _ -> seatOf power
            | [] -> Seat.at 1

    // --- taking units off when nobody said which -----------------------------------------------------

    /// How far a province is from the nearest home centre of a power, by any road at all.
    ///
    /// Only ever used to decide which units a power that would not choose loses. The rule
    /// every set of these has for that is "furthest from home first", and this is that,
    /// measured over both maps at once because a fleet's distance and an army's are the same
    /// question asked of different roads.
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

    /// Which units go when a power owes removals and has not said which - furthest from home
    /// first, then fleets before armies, then by name. Settled down to the last unit on
    /// purpose: a rule that left any of it to chance would make a game that no longer replays
    /// from its record.
    let private givenUp power position howMany =
        Position.unitsOf power position
        |> List.sortBy (fun piece ->
            -(fromHome power piece.Where.At),
            (match piece.Kind with
             | Fleet -> 0
             | Army -> 1),
            Atlas.code piece.Where.At)
        |> List.truncate howMany

    // --- one phase resolved, and the next one entered ------------------------------------------------

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

    /// The centres counted, which happens once a year and not once a phase.
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

    /// Whether anybody has won, asked only where it can have changed.
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

    /// Entering a phase: nothing written, and everybody with nothing to do already counted as
    /// having answered.
    ///
    /// Sealing the idle here rather than skipping them at the prompt is what makes `awaited`
    /// the only place that decides whose turn it is. A power that cannot build is not asked
    /// whether it would like to.
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

    /// Work the phase out, apply it, and walk on until somebody is owed a question.
    ///
    /// Recursive, because most phases have nobody in them: an autumn with no dislodgements
    /// goes straight to the winter, and a winter where every power's centres already match
    /// its units goes straight to the following spring. Each pass reports itself, so a player
    /// reads what happened in the phases nobody was asked about too.
    let rec private through play (told: Passing list) =
        match decided play with
        | Some finish -> Finished(play, finish), List.rev told
        | None ->

        // What this phase came to, and which phase the year moves on to. Nothing is entered
        // yet: the centres are still to be counted, and that happens on the way into a winter
        // whichever phase led there.
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
                // Anybody who was not told where to go, and anybody whose power walked away
                // without saying, walks off the board.
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

                // Whatever a power owed and did not name is taken from it, furthest from home
                // first. Without this a table waits forever on somebody who has left.
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

        // The centres change hands once a year, on the way into the winter - not at the end
        // of an autumn's movement, because an autumn's retreats can still take one back.
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

        // Walk on if the phase just entered has nobody in it. That is the common case rather
        // than the odd one: most seasons dislodge nobody and most winters owe nothing.
        match decided after with
        | Some finish -> Finished(after, finish), List.rev told
        | None when List.isEmpty (awaited after) -> through after told
        | None -> InPlay after, List.rev told

    /// Everybody has answered. Resolve, and hand back where the game stands with everything
    /// that happened on the way - kept on the game as well as said, so that the next screen
    /// can show the last set of orders beside the one being written.
    let resolveNow play =
        let session, told = through play []

        let remembering play = { play with Last = told }

        (match session with
         | InPlay play -> InPlay(remembering play)
         | Finished(play, ending) -> Finished(remembering play, ending)),
        told

    /// Say a power's orders are final, and resolve if that was the last of them.
    let seal power play =
        let play =
            { play with
                Sealed = Set.add power play.Sealed }

        if List.isEmpty (awaited play) then resolveNow play else InPlay play, []

    /// A power walks away. Its units stand and are taken off as they are pushed out, and it is
    /// never asked for anything again - so sealing it may be what finishes the phase.
    let walkAway power play =
        let play =
            { play with
                Adrift = Set.add power play.Adrift }

        if List.isEmpty (awaited play) then resolveNow play else InPlay play, []

    // --- and the board it all starts from -------------------------------------------------------------

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
