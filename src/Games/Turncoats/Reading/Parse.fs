namespace TCModel.Turncoats

open TCModel.Common
open TCModel.Engine
open TCModel.Table

module Parse =

    let private tryInt = Commands.tryInt

    let private color (text: string) =
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
        match tryInt text |> Option.bind Board.tryId with
        | Some regionId -> Ok regionId
        | None -> Error $"'{text}' is not a region on the board. They run 1 to {Board.count}."

    let asked = region

    let private colors words =
        words
        |> List.fold
            (fun outcome word ->
                outcome
                |> Result.bind (fun found -> color word |> Result.map (fun c -> found @ [ c ])))
            (Ok [])

    let private recruit c r =
        result {
            let! c = color c
            let! r = region r
            return Send(Make(Recruit(c, r)))
        }

    let private battle c target driven =
        result {
            let! c = color c
            let! target = region target

            let! driven =
                match driven with
                | [] -> Ok AsManyAsAllowed
                | [ "none" ] -> Ok(These [])
                | named -> colors named |> Result.map These

            return Send(Make(Battle(c, target, driven)))
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

            return Send(Make(March(c, from, into, count)))
        }

    let line (text: string) : Result<Command<Move>, string> =
        match Commands.lowered text with
        | [ ("recruit" | "r"); c; r ] -> recruit c r
        | ("battle" | "b") :: c :: target :: driven -> battle c target driven
        | [ ("march" | "m"); c; from; into ] -> march c from into "1"
        | [ ("march" | "m"); c; from; into; count ] -> march c from into count
        | [ "negotiate" ]
        | [ "n" ] -> Ok(Send(Make Negotiate))
        | [ "return"; c ] -> color c |> Result.map (Settle >> Make >> Send)
        | [ "rule"; r ] -> region r |> Result.map (fun _ -> Asking r)
        | word :: _ -> Error $"I don't know how to '{word}'. Type help."
        | [] -> Ok Nothing
