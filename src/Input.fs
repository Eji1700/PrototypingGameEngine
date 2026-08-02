/// Turns a line of console input into something the game understands.
module TCModel.Input

open System

/// Either a message for the game or a request the shell handles itself.
type Command =
    | Game of Msg
    | Help
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

let private parsePlace color region =
    match StoneColor.tryParse color, tryInt region with
    | None, _ -> Error $"'{color}' is not a colour. Try red, blue or black."
    | _, None -> Error $"'{region}' is not a region number."
    | Some color, Some region -> Ok(Game(Place(color, RegionId region)))

let parse (line: string) : Result<Command, string> =
    let words = line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

    match words |> List.map (fun word -> word.ToLowerInvariant()) with
    | [] -> Ok Nothing
    | [ "help" ] | [ "?" ] -> Ok Help
    | [ "quit" ] | [ "exit" ] | [ "q" ] -> Ok(Game Quit)
    | [ "pass" ] -> Ok(Game Pass)
    | [ "restart" ] -> Ok(Game(Restart(None, None)))
    | [ "restart"; seed ] -> parseSeed seed |> Result.map (fun seed -> Game(Restart(None, seed)))
    | [ "players"; count ] -> parsePlayerCount count |> Result.map (fun count -> Game(Restart(count, None)))
    | [ "players"; count; seed ] ->
        parsePlayerCount count
        |> Result.bind (fun count -> parseSeed seed |> Result.map (fun seed -> Game(Restart(count, seed))))
    | [ ("place" | "p"); color; region ] -> parsePlace color region
    | word :: _ -> Error $"I don't know how to '{word}'. Type help."
