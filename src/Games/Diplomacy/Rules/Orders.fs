namespace Prototyping.Diplomacy

type Instruction =
    | Holds
    | MoveTo of Location
    | SupportHold of who: ProvinceId
    | SupportMove of who: ProvinceId * into: ProvinceId
    | Convoys of who: ProvinceId * into: ProvinceId
    | Disbands
    | Builds of Kind * Coast option

type Order = { At: ProvinceId; Says: Instruction }

type Fault =
    | NothingThere of ProvinceId
    | NotYours of ProvinceId * Power
    | CannotReach of ProvinceId * ProvinceId
    | StayWhereYouAre of ProvinceId
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
    | NotThisPhase of Instruction

module Orders =

    let at (order: Order) = order.At


    let private couldBeCarried from into =
        from <> into && Atlas.terrainOf from = Coastal && Atlas.terrainOf into = Coastal

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
                if not (Atlas.canGo piece.Kind piece.Where into) then Error(CannotReach(order.At, into))
                elif into = order.At then Error(StayWhereYouAre order.At)
                else Ok(SupportMove(who, into))

            | Convoys(who, into) ->
                if piece.Kind <> Fleet then Error(OnlyFleetsConvoy order.At)
                elif not (Atlas.isSea order.At) then Error(ConvoysAreAtSea order.At)
                elif not (couldBeCarried who into) then Error(ConvoysCarryArmies(who, into))
                else Ok(Convoys(who, into))

            | says -> Error(NotThisPhase says)


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


    let buildable power position =
        Atlas.homesOf power
        |> List.filter (fun home ->
            Position.ownerOf home position = Some power
            && not (Position.occupied home position))

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
