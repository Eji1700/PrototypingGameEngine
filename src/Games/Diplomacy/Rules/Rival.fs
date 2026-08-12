namespace TCModel.Diplomacy

open TCModel.Common

/// How well a machine plays.
///
/// Nothing here is a search, and there is nothing to search. A game of noughts and crosses can
/// be walked to its end; a game of stones can at least be scored. This one is seven powers
/// writing in secret and a resolution nobody can see coming, and the part of it that actually
/// decides games happens in conversation between the people playing - which a machine at this
/// table is not having.
///
/// So what is here is a machine that plays the board and nothing else: it wants centres, it
/// wants them near, it will hold what it has, and the better ones will put a second unit behind
/// a push rather than send it somewhere on its own. That is a recognisable game of Diplomacy
/// and a beatable one, and saying so plainly is better than a name that promises more.
type Skill =
    { Name: string
      Describe: string
      /// How far it will look for something worth taking. One is "next door only".
      Sight: int
      /// Whether it will spend a unit backing another unit's move instead of moving itself.
      Backs: bool
      /// Whether it will stand a unit in front of a centre it already holds.
      Guards: bool
      /// Out of a hundred, how often it does something other than the best it saw.
      Slips: int }

/// A machine at a seat: how it plays, and its own generator.
///
/// The generator breaks ties, and at this game there are a great many: half a dozen provinces
/// very often score exactly alike, and a machine that always took the first would open the same
/// way every time and be read by the second year. It travels with the machine, so the same
/// table dealt twice plays the same twice.
type Rival = { Skill: Skill; Rng: Rng }

module Rival =

    // --- what a province is worth ---------------------------------------------------------------

    /// What standing in a province is worth, before distance is thought about.
    ///
    /// Only supply centres are worth anything, which is the whole game said in four lines: a
    /// centre somebody else holds is worth most, a neutral one nearly as much, one of its own
    /// is worth holding on to, and everywhere else on the map is worth nothing at all and is
    /// only ever a road to somewhere.
    let private worthOf power position province =
        if not (Atlas.isCentre province) then
            0
        else
            match Position.ownerOf province position with
            | Some owner when owner = power -> 3
            | Some _ -> 8
            | None -> 6

    /// How many steps every province is from the nearest centre this power does not already
    /// hold, over both maps at once.
    ///
    /// One walk outwards from every such centre at once, rather than a walk from each unit -
    /// same answer, and it is asked of thirty provinces a turn.
    let private nearness power position =
        let wanted =
            Atlas.centres
            |> List.filter (fun centre -> Position.ownerOf centre position <> Some power)

        let step province =
            Atlas.armyReach province
            @ (Atlas.fleetReach { At = province; Coast = None } |> List.map (fun there -> there.At))
            |> List.distinct

        let rec spread found edge depth =
            match edge with
            | [] -> found
            | _ ->
                let next =
                    edge
                    |> List.collect step
                    |> List.distinct
                    |> List.filter (fun province -> not (Map.containsKey province found))

                let found = next |> List.fold (fun found province -> Map.add province depth found) found
                spread found next (depth + 1)

        spread (wanted |> List.map (fun centre -> centre, 0) |> Map.ofList) wanted 1

    // --- what one unit might be told to do ----------------------------------------------------------

    /// The provinces this power's other units are already being sent to. A machine that
    /// bounced its own units off each other every spring would be worse than one that held.
    let private spokenFor power play =
        play.Written
        |> Map.toList
        |> List.choose (fun (province, says) ->
            match says, Position.at province play.Board with
            | MoveTo into, Some piece when piece.Power = power -> Some into.At
            | _ -> None)

    /// Every order this piece could be given, each with what it is worth. Highest wins, and the
    /// generator picks between the ones that tie.
    let private offers skill power play (piece: Piece) =
        let position = play.Board
        let steps = nearness power position
        let taken = spokenFor power play

        /// A province's worth, with distance folded in: something worth having three steps away
        /// still pulls, and pulls less than the same thing next door.
        let pull province =
            let far = Map.tryFind province steps |> Option.defaultValue 9
            worthOf power position province * 10 + max 0 (skill.Sight - far)

        let here = pull piece.Where.At

        let moves =
            Atlas.reach piece.Kind piece.Where
            |> List.filter (fun into ->
                not (List.contains into.At taken)
                // A province one of its own is standing in and not leaving is a province it
                // cannot enter, so it is not worth wanting.
                && (match Position.at into.At position with
                    | Some sitting -> sitting.Power <> power
                    | None -> true))
            |> List.map (fun into ->
                let against =
                    match Position.at into.At position with
                    // Walking at somebody unsupported is worth a little less than walking into
                    // an empty province of the same value: most of the time it simply bounces.
                    | Some _ -> -4
                    | None -> 0

                pull into.At + against, MoveTo into)

        let backing =
            if not skill.Backs then
                []
            else
                play.Written
                |> Map.toList
                |> List.choose (fun (from, says) ->
                    match says, Position.at from position with
                    | MoveTo into, Some other when other.Power = power && from <> piece.Where.At ->
                        if Atlas.canGo piece.Kind piece.Where into.At && into.At <> piece.Where.At then
                            // Worth a shade more than making the same move alone: two units on
                            // one province is how anything defended is ever taken.
                            Some(pull into.At + 2, SupportMove(from, into.At))
                        else
                            None
                    | _ -> None)

        let guarding =
            if not skill.Guards then
                []
            else
                Atlas.reach piece.Kind piece.Where
                |> List.choose (fun into ->
                    match Position.at into.At position with
                    | Some sitting when sitting.Power = power && Atlas.isCentre into.At ->
                        Some(worthOf power position into.At * 10 - 5, SupportHold into.At)
                    | _ -> None)

        (here, Holds) :: moves @ backing @ guarding

    // --- and what it actually says ---------------------------------------------------------------------

    let private pickFrom rng (choices: (int * 'a) list) =
        match choices with
        | [] -> None, rng
        | choices ->
            let best = choices |> List.map fst |> List.max
            let wanted = choices |> List.filter (fun (worth, _) -> worth = best) |> List.map snd
            let picked, rng = Rng.intBelow (List.length wanted) rng
            Some wanted[picked], rng

    let private slipping rival choices =
        let slip, rng = Rng.intBelow 100 rival.Rng

        if slip < rival.Skill.Slips && List.length choices > 1 then
            // Something other than the best it saw, which is the whole of what makes a beatable
            // opponent out of a machine that always plays its own advice.
            let picked, rng = Rng.intBelow (List.length choices) rng
            Some(snd choices[picked]), { rival with Rng = rng }
        else
            let picked, rng = pickFrom rng choices
            picked, { rival with Rng = rng }

    /// The first unit of this power still without an order, in board order. One order per turn
    /// of the crank: the engine asks again as long as the seat is still this machine's, so a
    /// power writes its whole set and then seals it.
    let private unwritten power play =
        Position.unitsOf power play.Board
        |> List.filter (fun piece -> not (Map.containsKey piece.Where.At play.Written))

    let private beatenAndSilent power play =
        play.Beaten
        |> List.filter (fun beaten -> beaten.Piece.Power = power && not (Map.containsKey beaten.From play.Written))

    /// What kind of unit to raise. Armies except at sea powers and except where a coast is the
    /// only way anywhere - said as a rule of thumb rather than worked out, because it is one.
    let private raising power position province =
        let mine = Position.unitsOf power position

        let fleets =
            mine
            |> List.filter (fun piece -> piece.Kind = Fleet)
            |> List.length

        if Atlas.terrainOf province <> Coastal then Army
        elif power = England then Fleet
        elif fleets * 3 < List.length mine then Fleet
        else Army

    /// Which move this machine makes, and the machine as it then stands. The whole of what a
    /// game hands the engine about a seat it plays; the *when* is `Machines`' and is the same
    /// at every game.
    let plays session rival =
        match session with
        | Finished _ -> None
        | InPlay play ->

        match Session.awaited play with
        | [] -> None
        | power :: _ ->

        match play.Stage with
        | Moving _ ->
            match unwritten power play with
            | [] -> Some(Commit, rival)
            | piece :: _ ->
                let choices = offers rival.Skill power play piece

                match slipping rival choices with
                | Some says, rival -> Some(Give(piece.Where.At, says), rival)
                | None, rival -> Some(Give(piece.Where.At, Holds), rival)

        | Falling _ ->
            match beatenAndSilent power play with
            | [] -> Some(Commit, rival)
            | beaten :: _ ->
                let steps = nearness power play.Board

                let choices =
                    beaten.Options
                    |> List.map (fun into ->
                        let far = Map.tryFind into.At steps |> Option.defaultValue 9
                        worthOf power play.Board into.At * 10 + max 0 (rival.Skill.Sight - far), MoveTo into)

                match slipping rival choices with
                | Some says, rival -> Some(Give(beaten.From, says), rival)
                // Nowhere to go is nowhere to go, whatever it would have preferred.
                | None, rival -> Some(Give(beaten.From, Disbands), rival)

        | Building ->
            let owing = Session.owed power play.Board

            let already =
                play.Written
                |> Map.toList
                |> List.filter (fun (province, says) ->
                    match says with
                    | Builds _ -> Position.ownerOf province play.Board = Some power
                    | Disbands ->
                        Position.at province play.Board
                        |> Option.map (fun piece -> piece.Power) = Some power
                    | _ -> false)

            if owing > 0 then
                let room =
                    Orders.buildable power play.Board
                    |> List.filter (fun home -> not (already |> List.exists (fst >> (=) home)))

                if List.length already >= owing || List.isEmpty room then
                    Some(Commit, rival)
                else
                    let steps = nearness power play.Board

                    let choices =
                        room
                        |> List.map (fun home ->
                            let far = Map.tryFind home steps |> Option.defaultValue 9
                            let kind = raising power play.Board home
                            let coast = if kind = Fleet then Atlas.coastsOf home |> List.tryHead else None
                            // A home centre near the fighting is where a new unit is wanted.
                            max 0 (12 - far), (home, Builds(kind, coast)))

                    match slipping rival choices with
                    | Some(home, says), rival -> Some(Give(home, says), rival)
                    | None, rival -> Some(Commit, rival)

            elif owing < 0 then
                let keeping =
                    Position.unitsOf power play.Board
                    |> List.filter (fun piece -> not (already |> List.exists (fst >> (=) piece.Where.At)))

                if List.length already >= -owing || List.isEmpty keeping then
                    Some(Commit, rival)
                else
                    // Whatever is worth least where it stands goes first, which comes to much
                    // the same answer as "furthest from home" and needs no second walk of the
                    // map to work out.
                    let steps = nearness power play.Board

                    let choices =
                        keeping
                        |> List.map (fun piece ->
                            let far = Map.tryFind piece.Where.At steps |> Option.defaultValue 9
                            far - worthOf power play.Board piece.Where.At, piece.Where.At)

                    match slipping rival choices with
                    | Some where, rival -> Some(Give(where, Disbands), rival)
                    | None, rival -> Some(Commit, rival)

            else
                Some(Commit, rival)

    // --- the three on offer -------------------------------------------------------------------------

    let easy =
        { Name = "easy"
          Describe = "walks at whatever is next door and worth having, and often somewhere else instead"
          Sight = 1
          Backs = false
          Guards = false
          Slips = 35 }

    let medium =
        { Name = "medium"
          Describe = "looks three provinces out and will put a second unit behind a push"
          Sight = 3
          Backs = true
          Guards = false
          Slips = 12 }

    let hard =
        { Name = "hard"
          Describe = "looks across half the board, supports its own attacks and stands over its centres"
          Sight = 6
          Backs = true
          Guards = true
          Slips = 0 }

    let all = [ easy; medium; hard ]

    let names = all |> List.map (fun skill -> skill.Name) |> String.concat ", "

    let byName (name: string) =
        let wanted = name.ToLowerInvariant()

        match all |> List.tryFind (fun skill -> skill.Name = wanted) with
        | Some skill -> Ok skill
        | None -> Error $"'{name}' is not a machine I have. There is {names}."

    /// Seat the machines named - one entry per seat, in dealing order, naming the skill or
    /// nobody - each with a generator of its own drawn from the deal and from where the seat
    /// sits, so that moving a machine along a seat hands it the generator that seat has always
    /// had.
    let seating (seed: uint64) sitting =
        Power.all
        |> List.indexed
        |> List.choose (fun (seat, power) ->
            sitting
            |> List.tryItem seat
            |> Option.flatten
            |> Option.map (fun skill ->
                Power.seatOf power,
                { Skill = skill
                  Rng = Rng.ofSeed (seed + uint64 seat) }))
