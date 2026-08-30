namespace Prototyping.Diplomacy

open Prototyping.Common
open Prototyping.Engine
// After the engine, so that a Play's fields are found on it and not on a Journal's.
open Prototyping.Diplomacy

type Skill =
    { Name: string
      Describe: string
      Sight: int
      Backs: bool
      Guards: bool
      Slips: int }

type Rival = { Skill: Skill; Rng: Rng }

module Rival =

    // The whole valuation. Only a centre is worth anything - one somebody else holds the most, one
    // nobody holds nearly as much, one's own enough to keep - and a worth is counted in tens so that
    // being near one, never worth more than a skill's Sight, only ever comes between centres worth
    // the same.
    [<Literal>]
    let private Kept = 3

    [<Literal>]
    let private Theirs = 8

    [<Literal>]
    let private Unclaimed = 6

    [<Literal>]
    let private Tens = 10

    // Against the same ground bare: walking at a unit, backing a push instead of making one, and
    // standing over a centre instead of going anywhere.
    [<Literal>]
    let private Occupied = -4

    [<Literal>]
    let private Backing = 2

    [<Literal>]
    let private Guarding = -5

    // Further than any Sight reaches, for ground no centre worth having can be walked to from.
    [<Literal>]
    let private BeyondSight = 9

    // A home is the better to build in the nearer it stands to something worth taking, out to here.
    [<Literal>]
    let private BuildingReach = 12


    let private worthOf power position province =
        if not (Atlas.isCentre province) then
            0
        else
            match Position.ownerOf province position with
            | Some owner when owner = power -> Kept
            | Some _ -> Theirs
            | None -> Unclaimed

    /// How far every province is from the nearest centre this power does not already own, worked out
    /// once by spreading outwards from all of them at once rather than by searching from each unit.
    /// `Sight` then says how far out a rival can actually feel this, which is most of what separates
    /// the skills.
    let private nearness power position =
        let wanted =
            Atlas.centres
            |> List.filter (fun centre -> Position.ownerOf centre position <> Some power)

        let rec spread found edge depth =
            match edge with
            | [] -> found
            | _ ->
                let next =
                    edge
                    |> List.collect Atlas.anyReach
                    |> List.distinct
                    |> List.filter (fun province -> not (Map.containsKey province found))

                let found =
                    next |> List.fold (fun found province -> Map.add province depth found) found

                spread found next (depth + 1)

        let found =
            spread (wanted |> List.map (fun centre -> centre, 0) |> Map.ofList) wanted 1

        fun province -> Map.tryFind province found |> Option.defaultValue BeyondSight

    /// What going to a province is worth: its centre, and how near it stands to one this power wants.
    let private pull skill power position =
        let far = nearness power position

        fun province -> worthOf power position province * Tens + max 0 (skill.Sight - far province)


    // Where this power's other units have already been sent. Orders are written one unit at a time,
    // so without this two of them would be sent at the same province and bounce off each other.
    let private spokenFor power play =
        play.Written
        |> Map.toList
        |> List.choose (fun (province, says) ->
            match says, Position.at province play.Board with
            | MoveTo into, Some piece when piece.Power = power -> Some into.At
            | _ -> None)

    let private offers skill power play (piece: Piece) =
        let position = play.Board
        let pull = pull skill power position
        let taken = spokenFor power play

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
                    | Some _ -> Occupied
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
                            Some(pull into.At + Backing, SupportMove(from, into.At))
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
                        Some(worthOf power position into.At * Tens + Guarding, SupportHold into.At)
                    | _ -> None)

        (here, Holds) :: moves @ backing @ guarding


    let private pickFrom rng (choices: (int * 'a) list) =
        match choices with
        | [] -> None, rng
        | choices ->
            let best = choices |> List.map fst |> List.max
            let wanted = choices |> List.filter (fun (worth, _) -> worth = best) |> List.map snd
            let picked, rng = Rng.pick wanted rng
            Some picked, rng

    let private slipping rival choices =
        let slip, rng = Rng.intBelow 100 rival.Rng

        if slip < rival.Skill.Slips && List.length choices > 1 then
            let (_, picked), rng = Rng.pick choices rng
            Some picked, { rival with Rng = rng }
        else
            let picked, rng = pickFrom rng choices
            picked, { rival with Rng = rng }

    let private unwritten power play =
        Position.unitsOf power play.Board
        |> List.filter (fun piece -> not (Map.containsKey piece.Where.At play.Written))

    let private beatenAndSilent power play =
        play.Beaten
        |> List.filter (fun beaten -> beaten.Piece.Power = power && not (Map.containsKey beaten.From play.Written))

    // What to build: armies inland, and at a coast a fleet if this power is short of them. England
    // always takes the fleet, having nowhere to walk to.
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
                let pull = pull rival.Skill power play.Board

                let choices = beaten.Options |> List.map (fun into -> pull into.At, MoveTo into)

                match slipping rival choices with
                | Some says, rival -> Some(Give(beaten.From, says), rival)
                | None, rival -> Some(Give(beaten.From, Disbands), rival)

        | Building ->
            let owing = Session.owed power play.Board
            let already = Session.writtenBy power play

            if owing > 0 then
                let room =
                    Orders.buildable power play.Board
                    |> List.filter (fun home -> not (already |> List.exists (fst >> (=) home)))

                if List.length already >= owing || List.isEmpty room then
                    Some(Commit, rival)
                else
                    let far = nearness power play.Board

                    let choices =
                        room
                        |> List.map (fun home ->
                            let kind = raising power play.Board home
                            let coast = if kind = Fleet then Atlas.coastsOf home |> List.tryHead else None
                            max 0 (BuildingReach - far home), (home, Builds(kind, coast)))

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
                    let far = nearness power play.Board

                    let choices =
                        keeping
                        |> List.map (fun piece -> far piece.Where.At - worthOf power play.Board piece.Where.At, piece.Where.At)

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

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    let seating (seed: uint64) sitting _ =
        Machines.seating (Power.all |> List.map Power.seatOf) seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng })
