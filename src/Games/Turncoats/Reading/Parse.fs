namespace Prototyping.Turncoats

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

module Parse =

    // `k` and `black` stay: the green faction was black, and written `k`, when the earliest records
    // were played, and logs/2026-08-03-193909-turncoats-2p-seed639214079490240407.log marches one.
    // Nothing writes either any more, and a record is meant to replay for good.
    let private colour (text: string) =
        match text with
        | "r"
        | "red" -> Ok Red
        | "b"
        | "blue" -> Ok Blue
        | "g"
        | "green"
        | "k"
        | "black" -> Ok Green
        | _ -> Error $"'{text}' is not a colour. Try red, blue or green."

    let private region text =
        match Commands.tryInt text |> Option.bind Board.tryId with
        | Some regionId -> Ok regionId
        | None -> Error $"'{text}' is not a region on the board. They run 1 to {Board.count}."

    let asked = region

    let private colours words =
        words
        |> List.fold
            (fun outcome word ->
                outcome
                |> Result.bind (fun found -> colour word |> Result.map (fun c -> found @ [ c ])))
            (Ok [])

    let private recruit c r =
        result {
            let! c = colour c
            let! r = region r
            return Send(Make(Recruit(c, r)))
        }

    let private battle c target driven =
        result {
            let! c = colour c
            let! target = region target

            let! driven =
                match driven with
                | [] -> Ok AsManyAsAllowed
                | [ "none" ] -> Ok(These [])
                | named -> colours named |> Result.map These

            return Send(Make(Battle(c, target, driven)))
        }

    let private counted count =
        match Commands.tryInt count with
        | Some n when n >= 1 -> Ok n
        | _ -> Error $"'{count}' is not a number of stones to march."

    let private march c from into count =
        result {
            let! c = colour c
            let! from = region from
            let! into = region into
            let! count = count
            return Send(Make(March(c, from, into, count)))
        }

    let line (text: string) : Result<Command<Move>, string> =
        match Commands.lowered text with
        | [ ("recruit" | "r"); c; r ] -> recruit c r
        | ("battle" | "b") :: c :: target :: driven -> battle c target driven
        | [ ("march" | "m"); c; from; into ] -> march c from into (Ok 1)
        | [ ("march" | "m"); c; from; into; count ] -> march c from into (counted count)
        | [ "negotiate" ]
        | [ "n" ] -> Ok(Send(Make Negotiate))
        | [ "return"; c ] -> colour c |> Result.map (Settle >> Make >> Send)
        | [ "rule"; r ] -> region r |> Result.map (fun _ -> Asking r)
        | _ -> Error "Say one of the four actions - 'r b 5', 'b r 8', 'm g 8 5 2' or 'n'. 'help' has the rest."
