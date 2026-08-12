namespace TCModel.Diplomacy

/// What one piece has been told to do.
///
/// Eight of them, and which are open depends on where the year has got to: five in a movement
/// phase, two when something has been dislodged, two when the centres have been counted. They
/// are one type rather than three because an order is an order - the record writes them the
/// same way, the parser reads them the same way, and a phase that will not take one says so
/// rather than being unable to represent it.
type Instruction =
    /// Stand where you are. What a piece with no order does anyway, said out loud.
    | Holds
    /// Go there. For an army this may be a province across water, which is a request for a
    /// convoy rather than a march.
    | MoveTo of Location
    /// Hold that province against whoever comes. The province, not the piece: a support does
    /// not care what is standing there.
    | SupportHold of who: ProvinceId
    /// Help that move through. Named by both ends, because a support that named only the
    /// destination would help everybody arriving at once.
    | SupportMove of who: ProvinceId * into: ProvinceId
    /// Carry an army over this water.
    | Convoys of who: ProvinceId * into: ProvinceId
    /// Off the board: a dislodged piece with nowhere to go, or a unit given up when the
    /// centres came up short.
    | Disbands
    /// A new piece, in a home centre. The coast is for the one home centre that has two, and
    /// is nothing anywhere else.
    | Builds of Kind * Coast option

/// One order: the province the piece is standing in, and what it was told.
///
/// Keyed by province rather than by piece, everywhere, and it is the crowding rule that makes
/// that safe: only one piece may ever be in a province, so a province names a piece exactly.
type Order = { At: ProvinceId; Says: Instruction }

/// Why an order was not taken.
///
/// Every one of these is a sentence somebody needs to read at the prompt, which is why they
/// carry what they carry. `Words` turns them into English; nothing here knows any.
type Fault =
    | NothingThere of ProvinceId
    | NotYours of ProvinceId * Power
    | CannotReach of ProvinceId * ProvinceId
    | StayWhereYouAre of ProvinceId
    /// A fleet sent to a province with two coastlines, from somewhere that can reach both.
    | WhichCoast of ProvinceId * Coast list
    | NoSuchCoast of ProvinceId * Coast
    | ArmiesHaveNoCoast of ProvinceId
    | OnlyFleetsConvoy of ProvinceId
    | ConvoysAreAtSea of ProvinceId
    | ConvoysCarryArmies of ProvinceId * ProvinceId
    | NotDislodged of ProvinceId
    | NoRoadOut of ProvinceId * Location list
    | NotAHomeOfYours of ProvinceId
    | HomeNotYours of ProvinceId * Power option
    | HomeOccupied of ProvinceId
    | NoFleetInland of ProvinceId
    | NothingOwed
    | NothingToGiveUp of ProvinceId
    /// An order that is a perfectly good order in some other part of the year.
    | NotThisPhase of Instruction

module Orders =

    /// Where an order is written to and read from. A province, since a province names a piece.
    let at (order: Order) = order.At

    // --- what a movement order has to satisfy ------------------------------------------------------

    /// Whether an army standing here could conceivably be carried there: two different coastal
    /// provinces, both of them land.
    ///
    /// "Conceivably" is the whole of what is checked. Whether any fleet is actually strung out
    /// across the water is a question for the adjudicator, and it has to be - the fleets doing
    /// the carrying are ordered in the same breath, and an order refused at the prompt for want
    /// of a convoy that is being written two seats away would be refused for the wrong reason.
    let private couldBeCarried from into =
        from <> into
        && Atlas.terrainOf from = Coastal
        && Atlas.terrainOf into = Coastal

    /// A destination as the piece would actually arrive at it, with the coast settled.
    ///
    /// A fleet told to go somewhere with two coastlines usually needs to say which, and this
    /// is where it is asked - but only where there is really a choice. From the Mid-Atlantic
    /// both coasts of Spain are open and the order is ambiguous; from Gascony only the north
    /// is, and demanding the coast there would be pedantry rather than a rule.
    let private landing piece into asked =
        match Atlas.waysInto piece.Kind piece.Where into with
        | [] -> Error(CannotReach(piece.Where.At, into))
        | ways ->
            match piece.Kind, asked with
            | Army, Some _ -> Error(ArmiesHaveNoCoast into)
            | Army, None -> Ok { At = into; Coast = None }
            | Fleet, Some coast ->
                match ways |> List.tryFind (fun way -> way.Coast = Some coast) with
                | Some way -> Ok way
                | None -> Error(NoSuchCoast(into, coast))
            | Fleet, None ->
                match ways with
                | [ only ] -> Ok only
                | many -> Error(WhichCoast(into, many |> List.choose (fun way -> way.Coast)))

    /// One movement-phase order, checked against the piece it is for.
    ///
    /// What is checked here is that the order *could* be carried out by that piece on that
    /// board - not that it will work. A move into a province three units are marching on is a
    /// perfectly good order and very often a failed one, and the difference between those two
    /// is the whole of what the adjudicator is for.
    let forMovement position power (order: Order) =
        match Position.at order.At position with
        | None -> Error(NothingThere order.At)
        | Some piece when piece.Power <> power -> Error(NotYours(order.At, piece.Power))
        | Some piece ->
            match order.Says with
            | Holds -> Ok Holds

            | MoveTo into when into.At = order.At -> Error(StayWhereYouAre order.At)

            | MoveTo into ->
                match landing piece into.At into.Coast with
                | Ok arrival -> Ok(MoveTo arrival)
                | Error(CannotReach _) when piece.Kind = Army && couldBeCarried order.At into.At ->
                    // Not adjacent, but an army between two coasts is asking to be carried.
                    // Whether anybody carries it is settled later.
                    Ok(MoveTo { At = into.At; Coast = None })
                | Error fault -> Error fault

            | SupportHold who when who = order.At -> Error(StayWhereYouAre order.At)

            | SupportHold who ->
                if Atlas.canGo piece.Kind piece.Where who then
                    Ok(SupportHold who)
                else
                    Error(CannotReach(order.At, who))

            | SupportMove(who, into) when who = order.At -> Error(StayWhereYouAre order.At)

            | SupportMove(who, into) ->
                // A support is help *into* a province, so what has to be reachable is the
                // destination and not the piece being helped. A fleet in the Ionian may
                // support an army out of Apulia into Greece without going near Apulia.
                if not (Atlas.canGo piece.Kind piece.Where into) then
                    Error(CannotReach(order.At, into))
                elif into = order.At then
                    // Supporting a move into your own province is supporting your own
                    // dislodgement, which no set of rules for this has ever allowed.
                    Error(StayWhereYouAre order.At)
                else
                    Ok(SupportMove(who, into))

            | Convoys(who, into) ->
                if piece.Kind <> Fleet then
                    Error(OnlyFleetsConvoy order.At)
                elif not (Atlas.isSea order.At) then
                    Error(ConvoysAreAtSea order.At)
                elif not (couldBeCarried who into) then
                    Error(ConvoysCarryArmies(who, into))
                else
                    Ok(Convoys(who, into))

            | says -> Error(NotThisPhase says)

    // --- a dislodged piece --------------------------------------------------------------------------

    /// One retreat, checked against where that piece may actually go.
    ///
    /// The list of places is worked out by the adjudicator and handed in, because it is not a
    /// fact about the map: a piece may not go back the way its attacker came, and may not go
    /// anywhere two armies bounced off each other this turn. Both of those are facts about the
    /// season that has just been resolved.
    ///
    /// A retreat is written as a move, and is one. There was a case of its own here for a
    /// while and it had to go: the parser reads a line without being told which phase the game
    /// is in, so `vie - tri` cannot mean two things - and a player who has just been thrown
    /// out of Vienna should not have to remember that this particular walk is spelt
    /// differently.
    let forRetreat (options: Location list) (order: Order) =
        match order.Says with
        | Disbands -> Ok Disbands
        | MoveTo into ->
            let ways =
                options
                |> List.filter (fun way -> way.At = into.At && (into.Coast.IsNone || way.Coast = into.Coast))

            match ways with
            | [ only ] -> Ok(MoveTo only)
            | [] -> Error(NoRoadOut(order.At, options))
            | many -> Error(WhichCoast(into.At, many |> List.choose (fun way -> way.Coast)))
        | says -> Error(NotThisPhase says)

    // --- and the winter ------------------------------------------------------------------------------

    /// Where a power may put a new piece: its own home centres, still owned by it, with
    /// nothing standing in them.
    ///
    /// All three conditions, and the middle one is the one that decides games. A power that
    /// has lost Berlin does not build in Berlin, however empty it is, and gets it back the
    /// moment it takes it in an autumn.
    let buildable power position =
        Atlas.homesOf power
        |> List.filter (fun home ->
            Position.ownerOf home position = Some power && not (Position.occupied home position))

    /// One build or one removal. `owed` is what the centres came to, which the session works
    /// out: a power builds when it is positive and gives units up when it is negative.
    let forWinter position power owed (order: Order) =
        match order.Says with
        | Builds(kind, coast) ->
            if owed <= 0 then
                Error NothingOwed
            elif Atlas.centreOf order.At <> Home power then
                Error(NotAHomeOfYours order.At)
            elif Position.ownerOf order.At position <> Some power then
                Error(HomeNotYours(order.At, Position.ownerOf order.At position))
            elif Position.occupied order.At position then
                Error(HomeOccupied order.At)
            elif kind = Fleet && Atlas.terrainOf order.At <> Coastal then
                Error(NoFleetInland order.At)
            else
                match kind, Atlas.coastsOf order.At, coast with
                | Fleet, [], Some _ -> Error(ArmiesHaveNoCoast order.At)
                | Fleet, coasts, None when not (List.isEmpty coasts) -> Error(WhichCoast(order.At, coasts))
                | Fleet, coasts, Some c when not (List.contains c coasts) -> Error(NoSuchCoast(order.At, c))
                | Army, _, Some _ -> Error(ArmiesHaveNoCoast order.At)
                | _ -> Ok(Builds(kind, coast))

        | Disbands ->
            if owed >= 0 then
                Error(NothingToGiveUp order.At)
            else
                match Position.at order.At position with
                | None -> Error(NothingThere order.At)
                | Some piece when piece.Power <> power -> Error(NotYours(order.At, piece.Power))
                | Some _ -> Ok Disbands

        | says -> Error(NotThisPhase says)
