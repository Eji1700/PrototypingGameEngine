namespace TCModel.Console

open System
open TCModel.Common
open TCModel.Domain
open TCModel.App

/// Turns a line of console input into something the game understands. Region numbers
/// are checked against the board here, so a `Msg` can only ever name a real region.
module Parse =

    /// Either a message for the game or a request the shell answers itself.
    type Command =
        | Send of Msg
        | Help
        | Explain of RegionId
        | Nothing

    let private tryInt (text: string) =
        match Int32.TryParse text with
        | true, value -> Some value
        | _ -> None

    let private color (text: string) =
        match text with
        | "r"
        | "red" -> Ok Red
        | "b"
        | "blue" -> Ok Blue
        | "k"
        | "black" -> Ok Black
        | _ -> Error $"'{text}' is not a colour. Try red, blue or black."

    let private region text =
        match tryInt text |> Option.bind Board.tryId with
        | Some regionId -> Ok regionId
        | None -> Error $"'{text}' is not a region on the board. They run 1 to {Board.count}."

    let private seed (text: string) =
        match UInt64.TryParse text with
        | true, value -> Ok(Some value)
        | _ -> Error $"'{text}' is not a seed."

    let private playerCount text =
        match tryInt text with
        | Some n when n >= Table.MinPlayers && n <= Table.MaxPlayers -> Ok(Some n)
        | Some n -> Error $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
        | None -> Error $"'{text}' is not a number of players."

    let private colors words =
        words
        |> List.fold
            (fun outcome word -> outcome |> Result.bind (fun found -> color word |> Result.map (fun c -> found @ [ c ])))
            (Ok [])

    let private recruit c r =
        result {
            let! c = color c
            let! r = region r
            return Send(Act(Recruit(c, r)))
        }

    let private battle c target driven =
        result {
            let! c = color c
            let! target = region target

            let! driven =
                match driven with
                // Unsaid means drive out everything the rule allows.
                | [] -> Ok AsManyAsAllowed
                | [ "none" ] -> Error "A battle must drive out at least one stone."
                | named -> colors named |> Result.map These

            return Send(Act(Battle(c, target, driven)))
        }

    let private march c from into count =
        result {
            let! c = color c
            let! from = region from
            let! into = region into

            let! count =
                match tryInt count with
                | Some n when n >= 1 -> Ok n
                | _ -> Error $"'{count}' is not a number of stones to march."

            return Send(Act(March(c, from, into, count)))
        }

    let line (text: string) : Result<Command, string> =
        let words =
            text.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

        match words |> List.map (fun word -> word.ToLowerInvariant()) with
        | [] -> Ok Nothing
        | [ "help" ] | [ "?" ] -> Ok Help
        | [ "quit" ] | [ "exit" ] | [ "q" ] -> Ok(Send Quit)
        | [ "rule"; r ] -> region r |> Result.map Explain
        | [ "restart" ] -> Ok(Send(Restart(None, None)))
        | [ "restart"; s ] -> seed s |> Result.map (fun s -> Send(Restart(None, s)))
        | [ "players"; n ] -> playerCount n |> Result.map (fun n -> Send(Restart(n, None)))
        | [ "players"; n; s ] ->
            result {
                let! n = playerCount n
                let! s = seed s
                return Send(Restart(n, s))
            }
        | [ ("recruit" | "r"); c; r ] -> recruit c r
        | ("battle" | "b") :: c :: target :: driven -> battle c target driven
        | [ ("march" | "m"); c; from; into ] -> march c from into "1"
        | [ ("march" | "m"); c; from; into; count ] -> march c from into count
        | [ "negotiate" ] | [ "n" ] -> Ok(Send(Act Negotiate))
        | [ "return"; c ] -> color c |> Result.map (Settle >> Send)
        | word :: _ -> Error $"I don't know how to '{word}'. Type help."
