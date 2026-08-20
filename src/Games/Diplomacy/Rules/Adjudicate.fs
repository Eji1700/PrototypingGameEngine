namespace TCModel.Diplomacy

open System.Collections.Generic

type Fate =
    | Advanced of Location
    | Bounced
    | Stood
    | Helped
    | Interrupted
    | Carried
    | NoRoute
    | Swamped

type Report =
    { At: ProvinceId
      Piece: Piece
      Said: Instruction
      Fate: Fate }

type Retreating =
    { Piece: Piece
      From: ProvinceId
      Options: Location list }

type Resolution =
    { Position: Position
      Reports: Report list
      Retreats: Retreating list
      Contested: Set<ProvinceId> }

module Adjudicate =

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
            | Some _ -> Stands)

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
                if not (waiting.Contains province) then waiting.Add province

                guess
            | Untouched ->
                let mark = waiting.Count
                status[province] <- Guessing false
                let first = adjudicate province

                if waiting.Count = mark then
                    status[province] <- Settled first
                    first
                elif waiting[mark] <> province then
                    status[province] <- Guessing first

                    if not (waiting.Contains province) then waiting.Add province

                    first
                else
                    forget mark
                    status[province] <- Guessing true
                    let second = adjudicate province

                    if first = second then
                        forget mark
                        status[province] <- Settled first
                        first
                    else
                        breakTheRing mark
                        resolve province

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

        and overrunAt province =
            headingFor province |> List.exists (fun (from, _, _) -> resolve from)

        and supportStands province direction =
            let ours = powerAt province

            let attackers =
                headingFor province
                |> List.filter (fun (from, _, _) -> powerAt from <> ours && pathOpen from)

            if attackers |> List.exists (fun (from, _, _) -> from <> direction) then
                false
            else
                not (attackers |> List.exists (fun (from, _, _) -> resolve from))

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

        and helpFor province into keep =
            plan
            |> Map.toList
            |> List.filter (fun (helper, doing) ->
                match doing with
                | HelpsMove(who, there) -> who = province && there = into && keep (powerAt helper) && resolve helper
                | _ -> false)
            |> List.length

        and headToHead province into =
            match pieceAt into with
            | None -> false
            | Some _ ->
                match destinationOf into with
                | Some back -> back.At = province && not (isCarried province) && not (isCarried into)
                | None -> false

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

        and standOf province =
            match destinationOf province with
            | None -> 0
            | Some into -> 1 + helpFor province into.At (fun _ -> true)

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

        and blockOf province =
            match destinationOf province with
            | None -> 0
            | Some into ->
                if not (pathOpen province) then 0
                elif headToHead province into.At && resolve into.At then 0
                else 1 + helpFor province into.At (fun _ -> true)

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


        let settled =
            plan |> Map.toList |> List.map (fun (province, _) -> province, resolve province)

        let answered province =
            settled
            |> List.tryPick (fun (other, answer) -> if other = province then Some answer else None)
            |> Option.defaultValue false

        let arrived = movers |> List.filter (fun (from, _, _) -> answered from)


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

        let contested =
            movers
            |> List.map (fun (_, into, _) -> into.At)
            |> List.countBy id
            |> List.filter (fun (province, tries) ->
                tries > 1
                && not (arrived |> List.exists (fun (_, into, _) -> into.At = province)))
            |> List.map fst
            |> Set.ofList

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
                        && (byWater || there.At <> attacker))

                { Piece = piece
                  From = province
                  Options = options })


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
