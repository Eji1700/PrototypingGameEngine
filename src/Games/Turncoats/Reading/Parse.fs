namespace TCModel.Turncoats

open TCModel.Common
open TCModel.Engine
open TCModel.Table

/// Turns a line of console input into something this game understands. Region numbers
/// are checked against the board here, so a `Msg` can only ever name a real region.
///
/// Only this game's own words. What a `Command` is, and the reading of every word that
/// means the same at any game, are `Commands`' - which is why there is no `undo` in this
/// file and no list of the ways of saying quit.
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
        // Green was once called Black and written K, and records written then say so. A
        // record is meant to replay for good, so the old words are still read even though
        // nothing writes them any more.
        | "k"
        | "black" -> Ok Green
        | _ -> Error $"'{text}' is not a colour. Try red, blue or green."

    let private region text =
        match tryInt text |> Option.bind Board.tryId with
        | Some regionId -> Ok regionId
        | None -> Error $"'{text}' is not a region on the board. They run 1 to {Board.count}."

    /// The region behind a `rule` question, read back where the view answers it. The words
    /// travelled as they were typed, so this is where they become a region again.
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
                // Unsaid means drive out everything the rule allows. Naming none is a
                // legal thing to ask for and an illegal thing to do, and the rules say
                // so themselves - the shell does not need its own copy of that.
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

    /// This game's own words, and only those. `undo`, `save`, `view rich`, `resign` and the
    /// rest have already been read by `Commands` before a line gets here, so what is left is
    /// exactly the vocabulary this game invented - which is the whole of what a second game
    /// has to write for itself.
    let line (text: string) : Result<Command<Move>, string> =
        match Commands.lowered text with
        | [ ("recruit" | "r"); c; r ] -> recruit c r
        | ("battle" | "b") :: c :: target :: driven -> battle c target driven
        | [ ("march" | "m"); c; from; into ] -> march c from into "1"
        | [ ("march" | "m"); c; from; into; count ] -> march c from into count
        | [ "negotiate" ]
        | [ "n" ] -> Ok(Send(Make Negotiate))
        | [ "return"; c ] -> color c |> Result.map (Settle >> Make >> Send)
        // Not a move, and not something the table could answer for itself: which region is
        // being asked about is this board's business. The words go through as they were
        // typed and this game's own view reads them.
        | [ "rule"; r ] -> region r |> Result.map (fun _ -> Asking r)
        | word :: _ -> Error $"I don't know how to '{word}'. Type help."
        | [] -> Ok Nothing
