namespace TCModel.Diplomacy

open System.Collections.Generic

/// What became of one order.
type Fate =
    /// It went, and where it ended up - which for a fleet is a coast as well as a province.
    | Advanced of Location
    /// It was held up: something as strong or stronger was going the same way, or the province
    /// held out.
    | Bounced
    | Stood
    /// A support that was given.
    | Helped
    /// A support that was cut by an attack on the unit giving it.
    | Interrupted
    /// A convoy that went through.
    | Carried
    /// A move that never started, for want of the water being covered.
    | NoRoute
    /// A convoy whose fleet was dislodged before the crossing.
    | Swamped

/// One order and what came of it, which is the whole of what a season has to report.
type Report =
    { At: ProvinceId
      Piece: Piece
      Said: Instruction
      Fate: Fate }

/// A piece pushed out of its province, and where it may go.
///
/// The list of places is worked out here and carried, rather than asked for again later,
/// because two of the three things that shorten it are facts about the season just resolved
/// and not about the map: a unit may not retreat down the road its attacker came up, and may
/// not walk into a province two other units bounced off each other in.
type Retreating =
    { Piece: Piece
      From: ProvinceId
      Options: Location list }

/// A season resolved: the board it left, what every order came to, who has to retreat, and
/// which provinces are barred to them.
type Resolution =
    { Position: Position
      Reports: Report list
      Retreats: Retreating list
      Contested: Set<ProvinceId> }

/// Working out what actually happened when everybody moved at once.
///
/// This is the only hard thing in the game and it is hard for one reason: an order's outcome
/// can depend on an order whose outcome depends on the first. A support is cut by an attack
/// that may itself be beaten off by the support being cut. Three units can each move into the
/// province the next one is leaving, and every one of them succeeds only because the others
/// do. A convoy can be dislodged by an army that only crosses because the convoy holds.
///
/// So orders are not evaluated in any order. Each one is asked for its outcome, guessed at
/// while the question is still open, and settled when the guess comes back agreeing with
/// itself. Where both guesses hold together - a genuine cycle - the rules have a name for
/// what to do, and there are only two cases: a ring of units all moving is a ring that all
/// gets through, and anything else with a convoy in it is a paradox, which is broken by
/// disrupting the convoy. That is Szykman's rule, and it is the one every set of these rules
/// eventually adopts.
///
/// **The mutation is local and the function is not.** There is a dictionary of half-settled
/// answers and a stack of what is waiting on what, and both die when `outcome` returns. Given
/// the same board and the same orders it gives the same answer every time, which is the whole
/// of what the rest of this program needs from it - `Update` stays pure, the model stays a
/// value, and a game still replays from its seed.
module Adjudicate =

    /// An order as the adjudicator needs it, with the things it does not care about dropped:
    /// what a fleet's coast is once it has arrived, and which power gave the order.
    ///
    /// `carried` is the one thing worked out rather than said. An army ordered somewhere it
    /// cannot walk to is asking to be shipped; an army ordered next door is walking, whatever
    /// fleets are sitting in the water beside it. Deciding it by the map rather than by
    /// reading intent into the order is what keeps this from needing a rule about intent.
    type private Doing =
        | Stands
        | Marches of into: Location * carried: bool
        | HoldsUp of who: ProvinceId
        | HelpsMove of who: ProvinceId * into: ProvinceId
        | Carries of who: ProvinceId * into: ProvinceId

    type private Status =
        | Untouched
        | Guessing of bool
        | Settled of bool

    let private planned position (orders: Map<ProvinceId, Instruction>) =
        position.Units
        |> Map.map (fun province piece ->
            match Map.tryFind province orders with
            | None
            | Some Holds -> Stands
            | Some(MoveTo into) ->
                let walks = Atlas.canGo piece.Kind piece.Where into.At
                Marches(into, piece.Kind = Army && not walks)
            | Some(SupportHold who) -> HoldsUp who
            | Some(SupportMove(who, into)) -> HelpsMove(who, into)
            | Some(Convoys(who, into)) -> Carries(who, into)
            // Retreats, disbands and builds are not movement orders and never reach here:
            // the phase they belong to takes them and this one refuses them. Answered rather
            // than thrown, because a total function is cheaper than an argument about which
            // file is guarding it.
            | Some _ -> Stands)

    /// Whether that sea washes that province, which is the whole of what a convoy chain is
    /// built out of.
    let private washes sea province =
        Atlas.fleetReach { At = sea; Coast = None }
        |> List.exists (fun there -> there.At = province)

    let outcome position (orders: Map<ProvinceId, Instruction>) : Resolution =
        let plan = planned position orders
        let pieceAt province = Map.tryFind province position.Units

        let doing province =
            Map.tryFind province plan |> Option.defaultValue Stands

        let powerAt province =
            pieceAt province |> Option.map (fun piece -> piece.Power)

        /// Everybody who was told to go somewhere, as province, destination and whether they
        /// are walking or being carried.
        let movers =
            plan
            |> Map.toList
            |> List.choose (fun (province, doing) ->
                match doing with
                | Marches(into, carried) -> Some(province, into, carried)
                | _ -> None)

        let destinationOf province =
            movers
            |> List.tryPick (fun (from, into, _) -> if from = province then Some into else None)

        let isCarried province =
            movers |> List.exists (fun (from, _, carried) -> from = province && carried)

        let headingFor province =
            movers |> List.filter (fun (_, into, _) -> into.At = province)

        // --- the half-settled answers, and what is waiting on what ------------------------------

        let status = Dictionary<ProvinceId, Status>()
        let waiting = ResizeArray<ProvinceId>()

        let statusOf province =
            match status.TryGetValue province with
            | true, held -> held
            | _ -> Untouched

        let forget mark =
            for index in mark .. waiting.Count - 1 do
                status.Remove waiting[index] |> ignore

            waiting.RemoveRange(mark, waiting.Count - mark)

        let rec resolve province =
            match statusOf province with
            | Settled answer -> answer
            | Guessing guess ->
                // We have come round to a question that is still open. Say what was guessed
                // and write down that this answer is only as good as the guess.
                if not (waiting.Contains province) then waiting.Add province

                guess
            | Untouched ->
                let mark = waiting.Count
                status[province] <- Guessing false
                let first = adjudicate province

                if waiting.Count = mark then
                    // Nothing was guessed at along the way, so this answer stands on its own.
                    status[province] <- Settled first
                    first
                elif waiting[mark] <> province then
                    // This order is inside a cycle that began further up. Leave it open and let
                    // whoever started the cycle sort it out - but leave it open at the answer
                    // just worked out rather than at the guess it started from. Anything else
                    // asking the same question again before the cycle is settled would be told
                    // the guess and not the working, and a ring of three units all moving would
                    // come apart on the second look.
                    status[province] <- Guessing first

                    if not (waiting.Contains province) then waiting.Add province

                    first
                else
                    // This order began the cycle. Ask it again the other way round.
                    forget mark
                    status[province] <- Guessing true
                    let second = adjudicate province

                    if first = second then
                        // The cycle makes no difference to this one, so the answer is real.
                        forget mark
                        status[province] <- Settled first
                        first
                    else
                        breakTheRing mark
                        resolve province

        /// A cycle that answers differently depending on what it is told about itself, which
        /// the rules of the game rather than the arithmetic have to settle.
        ///
        /// Two cases and no more. A ring of units each moving into the province the next is
        /// leaving is not a paradox at all - it is a convoy of a different sort, and every one
        /// of them arrives. Anything else caught in a cycle has a convoy in it, and that *is*
        /// a paradox: the crossing holds only if it is not attacked, and it is attacked only
        /// if it holds. The convoy gives way, which is Szykman's rule and the one this game
        /// settled on.
        and breakTheRing mark =
            let ring = [ for index in mark .. waiting.Count - 1 -> waiting[index] ]

            let convoys =
                ring
                |> List.filter (fun province ->
                    match doing province with
                    | Carries _ -> true
                    | _ -> false)

            forget mark

            if List.isEmpty convoys then
                for province in ring do
                    status[province] <- Settled true
            else
                for province in convoys do
                    status[province] <- Settled false

        and adjudicate province =
            match doing province with
            | Stands -> true
            | HoldsUp who -> supportStands province who
            | HelpsMove(_, into) -> supportStands province into
            | Carries _ -> not (overrunAt province)
            | Marches(into, carried) -> marchGetsThrough province into carried

        /// Whether somebody actually arrives here, which is what dislodges whoever is standing.
        and overrunAt province =
            headingFor province |> List.exists (fun (from, _, _) -> resolve from)

        /// Whether a support is given.
        ///
        /// Any attack from anywhere cuts it, however feeble - support is not a fight, it is a
        /// unit with its attention elsewhere. Three things do not cut it: an attack by the
        /// power giving the support, an attack that never arrived because nobody covered the
        /// water, and an attack out of the very province the support is pointed at. That last
        /// one has an exception of its own, which is that being thrown out of your province
        /// ends your support whoever did it.
        and supportStands province direction =
            let ours = powerAt province

            let attackers =
                headingFor province
                |> List.filter (fun (from, _, _) -> powerAt from <> ours && pathOpen from)

            if attackers |> List.exists (fun (from, _, _) -> from <> direction) then
                false
            else
                not (attackers |> List.exists (fun (from, _, _) -> resolve from))

        /// Whether the water is covered, for a move that needs covering. A move on foot is
        /// always open; a crossing is open only while every link in some chain of convoying
        /// fleets is still afloat.
        and pathOpen province =
            match doing province with
            | Marches(into, true) -> crossingExists province into.At
            | _ -> true

        and crossingExists from into =
            let carriers =
                plan
                |> Map.toList
                |> List.choose (fun (sea, doing) ->
                    match doing with
                    | Carries(who, there) when who = from && there = into -> Some sea
                    | _ -> None)
                |> List.filter resolve
                |> Set.ofList

            let rec walk reached edge =
                match edge with
                | [] -> false
                | sea :: rest when Set.contains sea reached -> walk reached rest
                | sea :: rest ->
                    if washes sea into then
                        true
                    else
                        let next =
                            carriers
                            |> Set.filter (fun other -> not (Set.contains other reached) && washes other sea)
                            |> Set.toList

                        walk (Set.add sea reached) (next @ rest)

            walk Set.empty (carriers |> Set.filter (fun sea -> washes sea from) |> Set.toList)

        /// The supports actually given to one move, counted only from powers this filter lets
        /// through - which is how "you may not help a stranger throw your own unit out" is
        /// said in arithmetic.
        and helpFor province into keep =
            plan
            |> Map.toList
            |> List.filter (fun (helper, doing) ->
                match doing with
                | HelpsMove(who, there) -> who = province && there = into && keep (powerAt helper) && resolve helper
                | _ -> false)
            |> List.length

        /// Whether these two are walking into each other, which is the one case where a
        /// province is not vacated by the unit leaving it. Two armies swapping places by
        /// convoy are not: a crossing goes round, so there is nothing to meet in.
        and headToHead province into =
            match pieceAt into with
            | None -> false
            | Some _ ->
                match destinationOf into with
                | Some back -> back.At = province && not (isCarried province) && not (isCarried into)
                | None -> false

        /// How hard a move pushes.
        ///
        /// One for the unit and one for every support, except that a province held by a unit
        /// that is staying takes two things away: a power may never push its own unit out, and
        /// nobody may help a stranger do it either.
        and pushOf province =
            match destinationOf province with
            | None -> 0
            | Some into ->
                if not (pathOpen province) then
                    0
                else
                    let held = pieceAt into.At

                    let leaving =
                        match held with
                        | None -> true
                        | Some _ ->
                            not (headToHead province into.At)
                            && (match destinationOf into.At with
                                | Some _ -> resolve into.At
                                | None -> false)

                    match held with
                    | _ when leaving -> 1 + helpFor province into.At (fun _ -> true)
                    | Some sitting when Some sitting.Power = powerAt province -> 0
                    | Some sitting -> 1 + helpFor province into.At (fun giver -> giver <> Some sitting.Power)
                    | None -> 1 + helpFor province into.At (fun _ -> true)

        /// How hard a unit walking the other way pushes back. Every support counts here,
        /// including one from the power it is fighting: holding your ground is not throwing
        /// anybody out.
        and standOf province =
            match destinationOf province with
            | None -> 0
            | Some into -> 1 + helpFor province into.At (fun _ -> true)

        /// How hard a province is to walk into: nothing if it is empty, nothing if whoever is
        /// there is leaving and gets away, and one plus its supports otherwise.
        and gripOn province =
            match pieceAt province with
            | None -> 0
            | Some _ ->
                match doing province with
                | Marches _ -> if resolve province then 0 else 1
                | _ ->
                    1
                    + (plan
                       |> Map.toList
                       |> List.filter (fun (helper, doing) ->
                           match doing with
                           | HoldsUp who -> who = province && resolve helper
                           | _ -> false)
                       |> List.length)

        /// How much one move gets in another's way. A unit walking into the arms of somebody
        /// walking the other way who gets through has stopped being in anybody's way at all.
        and blockOf province =
            match destinationOf province with
            | None -> 0
            | Some into ->
                if not (pathOpen province) then 0
                elif headToHead province into.At && resolve into.At then 0
                else 1 + helpFor province into.At (fun _ -> true)

        // `carried` is not read here: whether the water is covered is `pathOpen`'s question and
        // it asks the same one. Taken as an argument all the same, so that the shape of a
        // march order is the same wherever one is being talked about.
        and marchGetsThrough province into _carried =
            if not (pathOpen province) then
                false
            else
                let push = pushOf province

                let beatsWhoIsThere =
                    if headToHead province into.At then push > standOf into.At else push > gripOn into.At

                push > 0
                && beatsWhoIsThere
                && headingFor into.At
                   |> List.filter (fun (from, _, _) -> from <> province)
                   |> List.forall (fun (from, _, _) -> push > blockOf from)

        // --- ask every order, and then read the answers off ----------------------------------------

        let settled =
            plan |> Map.toList |> List.map (fun (province, _) -> province, resolve province)

        let answered province =
            settled
            |> List.tryPick (fun (other, answer) -> if other = province then Some answer else None)
            |> Option.defaultValue false

        let arrived = movers |> List.filter (fun (from, _, _) -> answered from)

        // --- who was thrown out, and where they may go -------------------------------------------------

        let pushedOut =
            position.Units
            |> Map.toList
            |> List.choose (fun (province, piece) ->
                let leftOfOwnAccord =
                    match doing province with
                    | Marches _ -> answered province
                    | _ -> false

                if leftOfOwnAccord then
                    None
                else
                    arrived
                    |> List.tryFind (fun (_, into, _) -> into.At = province)
                    |> Option.map (fun (attacker, _, byWater) -> province, piece, attacker, byWater))

        /// A province two or more units bounced off each other in, and nobody took. Barred to
        /// anybody retreating, which is the rule that stops a dislodged unit tidying itself
        /// into the gap a fight left behind.
        let contested =
            movers
            |> List.map (fun (_, into, _) -> into.At)
            |> List.countBy id
            |> List.filter (fun (province, tries) ->
                tries > 1
                && not (arrived |> List.exists (fun (_, into, _) -> into.At = province)))
            |> List.map fst
            |> Set.ofList

        /// The board the season leaves behind: everybody who got through where they got to,
        /// everybody who stayed where they were, and the dislodged nowhere at all until they
        /// say where they are going.
        let landed =
            let gone = pushedOut |> List.map (fun (province, _, _, _) -> province) |> Set.ofList

            let moved = arrived |> List.map (fun (from, _, _) -> from) |> Set.ofList

            let staying =
                position.Units
                |> Map.filter (fun province _ -> not (Set.contains province gone) && not (Set.contains province moved))

            let walked =
                arrived
                |> List.choose (fun (from, into, _) ->
                    pieceAt from |> Option.map (fun piece -> into.At, { piece with Where = into }))

            { position with
                Units =
                    walked
                    |> List.fold (fun units (province, piece) -> Map.add province piece units) staying }

        let retreats =
            pushedOut
            |> List.map (fun (province, piece, attacker, byWater) ->
                let options =
                    Atlas.reach piece.Kind piece.Where
                    |> List.filter (fun there ->
                        not (Position.occupied there.At landed)
                        && not (Set.contains there.At contested)
                        // Back down the road the attacker came up is closed - unless the
                        // attacker came over water, in which case it came up no road.
                        && (byWater || there.At <> attacker))

                { Piece = piece
                  From = province
                  Options = options })

        // --- and what to tell everybody ---------------------------------------------------------------

        let reports =
            plan
            |> Map.toList
            |> List.choose (fun (province, doing) ->
                pieceAt province
                |> Option.map (fun piece ->
                    let said = Map.tryFind province orders |> Option.defaultValue Holds

                    let fate =
                        match doing with
                        | Stands -> Stood
                        | HoldsUp _
                        | HelpsMove _ -> if answered province then Helped else Interrupted
                        | Carries _ -> if answered province then Carried else Swamped
                        | Marches(into, carried) ->
                            if answered province then Advanced into
                            elif carried && not (crossingExists province into.At) then NoRoute
                            else Bounced

                    { At = province
                      Piece = piece
                      Said = said
                      Fate = fate }))
            |> List.sortBy (fun report -> Atlas.code report.At)

        { Position = landed
          Reports = reports
          Retreats = retreats
          Contested = contested }

    // --- and the short phase that follows a bloody one -------------------------------------------------

    /// Where the retreats leave the board.
    ///
    /// Everything about this is simpler than a movement, with one exception that is not: two
    /// units retreating to the same province both disband. There is no fight, no support and
    /// no strength - they are already beaten, and two beaten units cannot share a province, so
    /// both walk off the board.
    let retreat position (retreating: Retreating list) (orders: Map<ProvinceId, Instruction>) =
        let going =
            retreating
            |> List.choose (fun who ->
                match Map.tryFind who.From orders with
                | Some(MoveTo into) when who.Options |> List.contains into -> Some(who, into)
                | _ -> None)

        let crowded =
            going
            |> List.countBy (fun (_, into) -> into.At)
            |> List.filter (fun (_, many) -> many > 1)
            |> List.map fst
            |> Set.ofList

        let survivors =
            going |> List.filter (fun (_, into) -> not (Set.contains into.At crowded))

        let position =
            survivors
            |> List.fold (fun board (who, into) -> Position.add { who.Piece with Where = into } board) position

        let scattered =
            retreating
            |> List.filter (fun who -> not (survivors |> List.exists (fun (other, _) -> other.From = who.From)))

        position, survivors, scattered
