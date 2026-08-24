namespace Prototyping.Warband

open Prototyping.Common
open Prototyping.Engine

type Skill =
    {
        Name: string
        Describe: string
        /// Whether it musters to a plan. The other one takes a kind and a hex at random, which is
        /// exactly as good as it sounds and is the reason it is offered.
        Careful: bool
    }

/// A machine only ever musters. Once both squads are in the field there is nothing left for
/// anybody to decide, so this hands back nothing and the table gets on with the battle.
type Rival =
    { Skill: Skill
      Rng: Rng
      Plan: (Kind * Hex) list }

module Rival =

    let private hex word =
        Formation.read word |> Option.defaultValue { Rank = Front; Step = 1 }

    /// Three squads that know what the ranks are for: the heavy at the front where it strikes
    /// most, the reach behind it, the bow and the mender at the back where they are worth the
    /// most and are hardest to reach. Written out rather than searched for, because a machine
    /// that muster well enough to teach the ranks is the whole of what this one is for.
    let plans =
        [ [ Rider, hex "f2"
            Footman, hex "f1"
            Warder, hex "m2"
            Bowman, hex "b2"
            Mender, hex "b1" ]
          [ Footman, hex "f1"
            Footman, hex "f3"
            Spearman, hex "m2"
            Warder, hex "m3"
            Bowman, hex "b2" ]
          [ Warder, hex "f2"
            Rider, hex "f1"
            Spearman, hex "m3"
            Bowman, hex "b2"
            Bowman, hex "b3" ] ]

    let private free squad =
        Formation.hexes |> List.filter (fun hex -> (Squad.at hex squad).IsNone)

    let private allowed squad =
        Kinds.all |> List.filter (fun kind -> Squad.manyOf kind squad < Squad.Alike)

    /// The plan this rival is following, drawn once and then carried: a plan redrawn every
    /// placement would be five halves of five different squads.
    let private planning rival =
        match rival.Plan with
        | [] ->
            let picked, rng = Rng.intBelow (List.length plans) rival.Rng

            plans[picked],
            { rival with
                Rng = rng
                Plan = plans[picked] }
        | plan -> plan, rival

    let plays play rival =
        match play.Stage with
        | Mustering place ->
            let squad = Session.squadOf place play

            if Squad.full squad then
                None
            elif rival.Skill.Careful then
                let plan, rival = planning rival

                plan
                |> List.tryFind (fun (_, hex) -> (Squad.at hex squad).IsNone)
                |> Option.map (fun (kind, hex) -> Muster(kind, hex), rival)
            else
                match allowed squad, free squad with
                | [], _
                | _, [] -> None
                | kinds, hexes ->
                    let picked, rng = Rng.intBelow (List.length kinds) rival.Rng
                    let where, rng = Rng.intBelow (List.length hexes) rng
                    Some(Muster(kinds[picked], hexes[where]), { rival with Rng = rng })

        | Fighting _
        | Ended _ -> None


    let raw =
        { Name = "raw"
          Describe = "musters a kind at random onto a hex at random, and finds out what the ranks were for"
          Careful = false }

    let steady =
        { Name = "steady"
          Describe = "musters to a plan: the heavy at the front, the reach behind it, the bow and the mender at the back"
          Careful = true }

    let all = [ raw; steady ]

    let names = Machines.named (fun skill -> skill.Name) all

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    let seating (seed: uint64) sitting =
        Machines.seating (Session.places |> List.map Seat.at) seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng; Plan = [] })
