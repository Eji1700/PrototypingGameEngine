namespace TCModel.Console

open System
open TCModel.Common
open TCModel.Domain

/// The front door: what a person can ask for before there is a game to play.
///
/// Pure, like the rest of the console layer - it says what the menu reads like and what
/// a typed line means, and leaves the reading and the writing to `Program`.
module Menu =

    /// What the menu was asked for. Every one of these either starts a game or comes
    /// back round to the menu, so there is nothing else for the front door to do.
    [<NoComparison; NoEquality>]
    type Choice =
        /// Deal a fresh game. A seed left unsaid is taken from the clock, so the game
        /// is a new one every time.
        | Deal of players: int * seed: uint64 option
        /// Deal one and wait for the other players to arrive from their own machines.
        | Host of players: int * seed: uint64 option
        /// Sit down at somebody else's table.
        | Join of address: string * token: string option
        | Replay of path: string
        /// Show the rules and the commands at length.
        | Rules
        /// Read the game a different way from here on.
        | Looking of View
        | Leave
        /// Nothing was typed, so the menu simply asks again.
        | Waiting

    /// The seatings on offer, taken from the table rather than written out, so the menu
    /// cannot come to offer a number the table would refuse.
    let private seatings =
        [ Table.MinPlayers .. Table.MaxPlayers ] |> List.map string |> String.concat "  "

    let private choice typed does = sprintf "    %-22s %s" typed does

    /// The menu is shown in the view it is offering, so a player choosing one can see
    /// what they are choosing before they commit a game to it.
    let screen (showing: View) =
        String.concat
            Environment.NewLine
            [ ""
              "=== TCModel ==="
              ""
              "  Stones on a map, and a seat each. How many are playing?"
              ""
              choice seatings "deal a game for that many, round this keyboard"
              ""
              "  Or, to play from separate machines:"
              ""
              choice "host <players>" "open a table and wait for them to arrive"
              choice "join <address>" "sit down at a table someone else is hosting"
              ""
              "  Or:"
              ""
              choice "<players> <seed>" "the same game again, from a seed"
              choice "replay <file>" "play a saved record again"
              choice $"view <{View.names}>" $"how the board is drawn - now {showing.Name}, {showing.Describe}"
              choice "rules" "the rules and the commands, at length"
              choice "quit" "leave"
              "" ]

    /// A typed line as a choice. A bare number is the answer to the question the menu
    /// asks, so it needs no command word in front of it.
    let choose (text: string) : Result<Choice, string> =
        let dealing players seed =
            result {
                let! players = Parse.tryPlayerCount players
                let! seed = Parse.trySeed seed
                return Deal(players, Some seed)
            }

        match Parse.words text with
        | [] -> Ok Waiting
        // The word is lowered to be read, but the rest is left as it was typed: a file
        // may be named in any case, and on some machines that is the difference between
        // finding it and not.
        | word :: rest ->
            match word.ToLowerInvariant(), rest with
            | ("quit" | "exit" | "q"), [] -> Ok Leave
            | ("rules" | "help" | "?"), [] -> Ok Rules
            | "replay", [ path ] -> Ok(Replay path)
            | "replay", _ -> Error "Say 'replay <file>', naming one saved record."
            | "host", [ players ] -> Parse.tryPlayerCount players |> Result.map (fun n -> Host(n, None))
            | "host", [ players; seed ] ->
                result {
                    let! players = Parse.tryPlayerCount players
                    let! seed = Parse.trySeed seed
                    return Host(players, Some seed)
                }
            | "host", _ -> Error $"Say 'host <players>', for {Table.MinPlayers} to {Table.MaxPlayers} of you."
            | "view", [ name ] -> View.byName name |> Result.map Looking
            | "view", _ -> Error $"Say 'view <name>', for one of {View.names}."
            | "join", [ address ] -> Ok(Join(address, None))
            | "join", [ address; token ] -> Ok(Join(address, Some token))
            | "join", _ -> Error "Say 'join <address>', naming the machine that is hosting."
            | "players", [ players ] -> Parse.tryPlayerCount players |> Result.map (fun n -> Deal(n, None))
            | "players", [ players; seed ] -> dealing players seed
            | players, [] -> Parse.tryPlayerCount players |> Result.map (fun n -> Deal(n, None))
            | players, [ seed ] -> dealing players seed
            | word, _ -> Error $"I don't know how to '{word}'. Say how many are playing, or quit."
