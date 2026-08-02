/// Turns a line of console input into something the game understands.
module TCModel.Input

open System

/// Either a message for the game or a request the shell handles itself.
type Command =
    | Game of Msg
    | Help
    /// Ask about the game without changing it.
    | Explain of RegionId
    | Nothing

let private tryInt (text: string) =
    match Int32.TryParse text with
    | true, value -> Some value
    | _ -> None

let private tryUInt64 (text: string) =
    match UInt64.TryParse text with
    | true, value -> Some value
    | _ -> None

let private parseSeed text =
    match tryUInt64 text with
    | Some seed -> Ok(Some seed)
    | None -> Error $"'{text}' is not a seed."

let private parsePlayerCount text =
    match tryInt text with
    | Some n when n >= Setup.MinPlayers && n <= Setup.MaxPlayers -> Ok(Some n)
    | Some n -> Error $"{n} players? The game takes {Setup.MinPlayers} to {Setup.MaxPlayers}."
    | None -> Error $"'{text}' is not a number of players."

let private parseColor text =
    match StoneColor.tryParse text with
    | Some color -> Ok color
    | None -> Error $"'{text}' is not a colour. Try red, blue or black."

let private parseRegion text =
    match tryInt text with
    | Some n -> Ok(RegionId n)
    | None -> Error $"'{text}' is not a region number."

let private parseColors words =
    words
    |> List.fold
        (fun outcome word ->
            outcome
            |> Result.bind (fun colors -> parseColor word |> Result.map (fun color -> colors @ [ color ])))
        (Ok [])

let private parseRecruit color region =
    result {
        let! color = parseColor color
        let! region = parseRegion region
        return Game(Recruit(color, region))
    }

let private parseBattle color target driven =
    result {
        let! color = parseColor color
        let! target = parseRegion target
        let! driven = parseColors driven
        return Game(Battle(color, target, driven))
    }

let private parseMarch color from into count =
    result {
        let! color = parseColor color
        let! from = parseRegion from
        let! into = parseRegion into

        let! count =
            match tryInt count with
            | Some n when n >= 1 -> Ok n
            | _ -> Error $"'{count}' is not a number of stones to march."

        return Game(March(color, from, into, count))
    }

let parse (line: string) : Result<Command, string> =
    let words = line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

    match words |> List.map (fun word -> word.ToLowerInvariant()) with
    | [] -> Ok Nothing
    | [ "help" ] | [ "?" ] -> Ok Help
    | [ "quit" ] | [ "exit" ] | [ "q" ] -> Ok(Game Quit)
    | [ "restart" ] -> Ok(Game(Restart(None, None)))
    | [ "restart"; seed ] -> parseSeed seed |> Result.map (fun seed -> Game(Restart(None, seed)))
    | [ "players"; count ] -> parsePlayerCount count |> Result.map (fun count -> Game(Restart(count, None)))
    | [ "players"; count; seed ] ->
        parsePlayerCount count
        |> Result.bind (fun count -> parseSeed seed |> Result.map (fun seed -> Game(Restart(count, seed))))
    | [ ("recruit" | "r"); color; region ] -> parseRecruit color region
    | ("battle" | "b") :: color :: target :: driven -> parseBattle color target driven
    | [ ("march" | "m"); color; from; into ] -> parseMarch color from into "1"
    | [ ("march" | "m"); color; from; into; count ] -> parseMarch color from into count
    | [ "rule"; region ] -> parseRegion region |> Result.map Explain
    | [ "negotiate" ] | [ "n" ] -> Ok(Game Negotiate)
    | [ "keep" ] -> Ok(Game(Settle None))
    | [ "return"; color ] -> parseColor color |> Result.map (Some >> Settle >> Game)
    | word :: _ -> Error $"I don't know how to '{word}'. Type help."
