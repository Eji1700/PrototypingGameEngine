namespace TCModel.Console

open System
open TCModel.Common
open TCModel.Domain
open TCModel.App

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
        /// is a new one every time; the skills are the seats after the first that the
        /// machine is to play.
        | Deal of players: int * seed: uint64 option * rivals: Skill list
        /// Deal one and play it in a browser on this machine instead of at this keyboard.
        | Serve of players: int * seed: uint64 option * rivals: Skill list
        /// Deal one and wait for the other players to arrive from their own machines.
        | Host of players: int * seed: uint64 option
        /// Sit down at somebody else's table.
        | Join of address: string * token: string option
        | Replay of path: string
        /// Show the rules and the commands at length.
        | Rules
        /// Read the game a different way from here on.
        | Looking of View
        /// Settle what is drawn in what colour.
        | Options
        | Leave
        /// Nothing was typed, so the menu simply asks again.
        | Waiting

    /// The seatings on offer, taken from the table rather than written out, so the menu
    /// cannot come to offer a number the table would refuse.
    let private seatings =
        [ Table.MinPlayers .. Table.MaxPlayers ]
        |> List.map string
        |> String.concat "  "

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
              choice "serve <players>" "the same, but played in a browser on this machine"
              ""
              "  Or, against the program:"
              ""
              choice "vs <skill>..." $"one seat each - the machine plays {Rival.names}"
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
              choice $"view <{View.namesFor AtATerminal}>" $"how the board is drawn - now {showing.Name}, {showing.Describe}"
              choice "colours" "which colour is drawn for what"
              choice "rules" "the rules and the commands, at length"
              choice "quit" "leave"
              "" ]

    /// A typed line as a choice. A bare number is the answer to the question the menu
    /// asks, so it needs no command word in front of it.
    ///
    /// The palette comes in because a view is built in one: asking for another way of
    /// reading keeps whatever colours have been settled on rather than going back to the
    /// ones the game started in.
    let choose (palette: Palette) (text: string) : Result<Choice, string> =
        let dealing players seed =
            result {
                let! players = Parse.tryPlayerCount players
                let! seed = Parse.trySeed seed
                return Deal(players, Some seed, [])
            }

        /// The machines named after `vs`. How many are playing is not asked for and is not
        /// something to get wrong: it is one seat for whoever is reading this and one for
        /// each machine named, which is what somebody saying 'vs medium' means.
        let facing names =
            names
            |> List.fold
                (fun found name ->
                    found
                    |> Result.bind (fun found -> Rival.byName name |> Result.map (fun skill -> found @ [ skill ])))
                (Ok [])
            |> Result.bind (fun skills ->
                match List.length skills with
                | 0 -> Error $"Say 'vs <skill>', for one or more of {Rival.names}."
                | many when many + 1 > Table.MaxPlayers ->
                    Error $"That is a table of {many + 1}. The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
                | many -> Ok(many + 1, skills))

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
            // Before the seatings below, which would read 'vs' as a number of players and
            // say so rather than saying what is actually wrong.
            | "vs", names ->
                facing names
                |> Result.map (fun (players, skills) -> Deal(players, None, skills))
            | "serve", "vs" :: names ->
                facing names
                |> Result.map (fun (players, skills) -> Serve(players, None, skills))
            | "serve", [ players ] -> Parse.tryPlayerCount players |> Result.map (fun n -> Serve(n, None, []))
            | "serve", [ players; seed ] ->
                result {
                    let! players = Parse.tryPlayerCount players
                    let! seed = Parse.trySeed seed
                    return Serve(players, Some seed, [])
                }
            | "serve", _ -> Error $"Say 'serve <players>', for {Table.MinPlayers} to {Table.MaxPlayers} of you."
            | "view", [ name ] -> View.byName AtATerminal palette name |> Result.map Looking
            | "view", _ -> Error $"Say 'view <name>', for one of {View.namesFor AtATerminal}."
            | ("colours" | "colors" | "options"), [] -> Ok Options
            | ("colours" | "colors" | "options"), _ -> Error "Say 'colours' on its own; the screen it opens says the rest."
            | "join", [ address ] -> Ok(Join(address, None))
            | "join", [ address; token ] -> Ok(Join(address, Some token))
            | "join", _ -> Error "Say 'join <address>', naming the machine that is hosting."
            | "players", [ players ] -> Parse.tryPlayerCount players |> Result.map (fun n -> Deal(n, None, []))
            | "players", [ players; seed ] -> dealing players seed
            | players, [] -> Parse.tryPlayerCount players |> Result.map (fun n -> Deal(n, None, []))
            | players, [ seed ] -> dealing players seed
            | word, _ -> Error $"I don't know how to '{word}'. Say how many are playing, or quit."
