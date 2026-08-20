namespace TCModel.Engine

open TCModel.Common

[<NoComparison; NoEquality>]
type Machine<'Move, 'State> = Choosing of ('State -> ('Move * Machine<'Move, 'State>) option)

module Machines =


    let rec choosing plays rival =
        Choosing(fun state -> plays state rival |> Option.map (fun (move, next) -> move, choosing plays next))

    let named nameOf all =
        all |> List.map nameOf |> String.concat ", "

    let byName nameOf all (name: string) =
        let wanted = name.ToLowerInvariant()

        match all |> List.tryFind (fun skill -> nameOf skill = wanted) with
        | Some skill -> Ok skill
        | None -> Error $"'{name}' is not a machine I have. There is {named nameOf all}."

    let seating (seats: PlayerId list) (seed: uint64) (sitting: 'Skill option list) =
        seats
        |> List.indexed
        |> List.choose (fun (place, seat) ->
            sitting
            |> List.tryItem place
            |> Option.flatten
            |> Option.map (fun skill -> seat, skill, Rng.ofSeed (seed + uint64 place)))


    let private toAct (rules: Rules<_, _, _>) rivals model =
        let standing = Model.state model

        if rules.Over standing then
            None
        else
            rivals |> List.tryFind (fst >> (=) (rules.Active standing)) |> Option.map snd

    let holds rules rivals model =
        toAct rules rivals model |> Option.isSome

    let playing state (Choosing choose) = choose state

    let private withRival playerId rival rivals =
        rivals
        |> List.map (fun (other, was) -> if other = playerId then other, rival else other, was)

    let rec answering rules plays rivals model =
        match toAct rules rivals model with
        | None -> model, rivals
        | Some rival ->
            let seat = rules.Active(Model.state model)

            match plays (Model.state model) rival with
            | None -> model, rivals
            | Some(move, rival) ->
                let next = Update.update rules (Make move) model
                let rivals = withRival seat rival rivals

                if Timeline.movesMade next.Timeline = Timeline.movesMade model.Timeline then
                    next, rivals
                else
                    answering rules plays rivals next
