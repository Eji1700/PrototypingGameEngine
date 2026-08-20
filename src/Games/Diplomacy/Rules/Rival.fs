namespace TCModel.Diplomacy

open TCModel.Common
open TCModel.Engine
open TCModel.Diplomacy

type Skill =
    { Name: string
      Describe: string
      Sight: int
      Backs: bool
      Guards: bool
      Slips: int }

type Rival = { Skill: Skill; Rng: Rng }

module Rival =


    let private worthOf power position province =
        if not (Atlas.isCentre province) then
            0
        else
            match Position.ownerOf province position with
            | Some owner when owner = power -> 3
            | Some _ -> 8
            | None -> 6

    let private nearness power position =
        let wanted =
            Atlas.centres
            |> List.filter (fun centre -> Position.ownerOf centre position <> Some power)

        let step province =
            Atlas.armyReach province
            @ (Atlas.fleetReach { At = province; Coast = None }
               |> List.map (fun there -> there.At))
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

                let found =
                    next |> List.fold (fun found province -> Map.add province depth found) found

                spread found next (depth + 1)

        spread (wanted |> List.map (fun centre -> centre, 0) |> Map.ofList) wanted 1


    let private spokenFor power play =
        play.Written
        |> Map.toList
        |> List.choose (fun (province, says) ->
            match says, Position.at province play.Board with
            | MoveTo into, Some piece when piece.Power = power -> Some into.At
            | _ -> None)

    let private offers skill power play (piece: Piece) =
        let position = play.Board
        let steps = nearness power position
        let taken = spokenFor power play

        let pull province =
            let far = Map.tryFind province steps |> Option.defaultValue 9
            worthOf power position province * 10 + max 0 (skill.Sight - far)

        let here = pull piece.Where.At

        let moves =
            Atlas.reach piece.Kind piece.Where
            |> List.filter (fun into ->
                not (List.contains into.At taken)
                && (match Position.at into.At position with
                    | Some sitting -> sitting.Power <> power
                    | None -> true))
            |> List.map (fun into ->
                let against =
                    match Position.at into.At position with
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
            let picked, rng = Rng.intBelow (List.length choices) rng
            Some(snd choices[picked]), { rival with Rng = rng }
        else
            let picked, rng = pickFrom rng choices
            picked, { rival with Rng = rng }

    let private unwritten power play =
        Position.unitsOf power play.Board
        |> List.filter (fun piece -> not (Map.containsKey piece.Where.At play.Written))

    let private beatenAndSilent power play =
        play.Beaten
        |> List.filter (fun beaten -> beaten.Piece.Power = power && not (Map.containsKey beaten.From play.Written))

    let private raising power position province =
        let mine = Position.unitsOf power position

        let fleets = mine |> List.filter (fun piece -> piece.Kind = Fleet) |> List.length

        if Atlas.terrainOf province <> Coastal then Army
        elif power = England then Fleet
        elif fleets * 3 < List.length mine then Fleet
        else Army

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
                | None, rival -> Some(Give(beaten.From, Disbands), rival)

        | Building ->
            let owing = Session.owed power play.Board

            let already =
                play.Written
                |> Map.toList
                |> List.filter (fun (province, says) ->
                    match says with
                    | Builds _ -> Position.ownerOf province play.Board = Some power
                    | Disbands -> Position.at province play.Board |> Option.map (fun piece -> piece.Power) = Some power
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

    let names = Machines.named (fun skill -> skill.Name) all

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    let seating (seed: uint64) sitting =
        Machines.seating (Power.all |> List.map Power.seatOf) seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng })
