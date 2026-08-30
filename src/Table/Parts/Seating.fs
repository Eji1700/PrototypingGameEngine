namespace Prototyping.Table

type Sitter =
    | Here
    | Machine of skill: string
    | Elsewhere

module Seating =

    [<Literal>]
    let private Mine = "you"

    [<Literal>]
    let private Theirs = "joins"

    let says sitter =
        match sitter with
        | Here -> Mine
        | Machine skill -> skill
        | Elsewhere -> Theirs

    let describe skills sitter =
        match sitter with
        | Here -> "somebody at this keyboard"
        | Machine skill ->
            skills
            |> List.tryFind (fst >> (=) skill)
            |> Option.map snd
            |> Option.defaultValue "the program"
        | Elsewhere -> "somebody at their own machine, who joins this table"

    let all skills =
        [ Here ] @ (skills |> List.map (fst >> Machine)) @ [ Elsewhere ]

    let names skills =
        all skills |> List.map says |> String.concat ", "

    let byName skills (word: string) =
        let wanted = word.ToLowerInvariant()

        match all skills |> List.tryFind (fun sitter -> says sitter = wanted) with
        | Some sitter -> Ok sitter
        | None -> Error $"'{word}' is not somebody to seat. There is {names skills}."

    /// A machine by the name of its skill, for a line that says who plays a seat. The refusal lists
    /// the machines rather than every sitter, since 'you' and 'joins' are not ways for it to play.
    let machineByName skills (word: string) =
        let machines = skills |> List.map fst
        let offered = String.concat ", " machines

        match machines with
        | [] -> Error "This game has no machine to play it."
        | _ ->
            match machines |> List.tryFind (fun name -> name = word.ToLowerInvariant()) with
            | Some name -> Ok name
            | None -> Error $"'{word}' is not a way for the machine to play. There is {offered}."

    let line sitters =
        sitters |> List.map says |> String.concat " "

    let walked skills step sitter =
        let all = all skills

        let at =
            all
            |> List.tryFindIndex (fun other -> says other = says sitter)
            |> Option.defaultValue 0

        all[Keys.wrapped (List.length all) at step]

    let seated at sitter sitters =
        sitters |> List.mapi (fun seat was -> if seat = at then sitter else was)


    let machines sitters =
        sitters
        |> List.map (fun sitter ->
            match sitter with
            | Machine skill -> Some skill
            | Here
            | Elsewhere -> None)

    let hosted sitters = sitters |> List.exists ((=) Elsewhere)

    let awaited sitters =
        sitters |> List.filter ((=) Here) |> List.length, sitters |> List.filter ((=) Elsewhere) |> List.length

    let roster skills sitters =
        sitters
        |> List.mapi (fun seat sitter -> sprintf "  Seat %d  %-8s%s" (seat + 1) (says sitter) (describe skills sitter))


    let here players = List.replicate players Here

    let hosting players = List.replicate players Elsewhere

    let after players skills =
        [ for seat in 0 .. players - 1 ->
              match List.tryItem (seat - 1) skills with
              | Some skill -> Machine skill
              | None -> Here ]

    let resuming others sitters =
        sitters
        |> List.map (fun sitter ->
            match sitter with
            | Machine _ -> sitter
            | Here
            | Elsewhere -> others)


    let read skills (fewest, most) words =
        let sitters =
            words
            |> List.fold
                (fun found word ->
                    found
                    |> Result.bind (fun found -> byName skills word |> Result.map (fun sitter -> found @ [ sitter ])))
                (Ok [])

        sitters
        |> Result.bind (fun sitters ->
            Commands.tryPlayers (fewest, most) (List.length sitters)
            |> Result.map (fun _ -> sitters))
