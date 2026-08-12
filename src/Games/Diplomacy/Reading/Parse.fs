namespace TCModel.Diplomacy

open TCModel.Common
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses - `Open`.
open TCModel.Diplomacy

/// A typed line as this game's own move.
///
/// The words read here are the ones every set of rules for this game prints: `vie - tri`,
/// `bud s vie - tri`, `nth c lon - bel`. That is not a flourish - it is the reason the record
/// on disk is readable by anybody who has played this before, and the reason `Words.order` and
/// this file are two halves of the same bargain rather than two languages.
///
/// `undo`, `save`, `view rich`, `resign`, `quit` and `restart` are not here. They mean the same
/// thing whatever is on the board and have already been read, once, for every game there is.
module Parse =

    /// The words a unit is named with, which an order does not need and everybody types anyway.
    /// No province is called `a` or `f`, so throwing them away cannot throw away a place.
    let private naming = set [ "a"; "f"; "army"; "fleet" ]

    let private province word =
        match Atlas.byWord word with
        | Some id -> Ok id
        | None -> Error $"'{word}' is not a province. They are named by their first three letters - 'vie', 'tri', 'nth'."

    let private spot word =
        match Atlas.spotBy word with
        | Some location -> Ok location
        | None ->
            Error
                $"'{word}' is not a place on this board. Say a province's first three letters, and a coast after a slash where it has two - 'stp/sc'."

    let private send at says = Send(Make(Give(at, says)))

    /// One order, with the province it is for already read.
    let private order (words: string list) =
        match words with
        | [ from; "hold" ]
        | [ from; "h" ]
        | [ from; "holds" ] -> province from |> Result.map (fun at -> send at Holds)

        | [ from; "-"; into ]
        | [ from; "r"; into ]
        | [ from; "retreat"; into ] ->
            result {
                let! at = province from
                let! there = spot into
                return send at (MoveTo there)
            }

        | [ from; "s"; who ]
        | [ from; "support"; who ] ->
            result {
                let! at = province from
                let! whom = province who
                return send at (SupportHold whom)
            }

        | [ from; "s"; who; "-"; into ]
        | [ from; "support"; who; "-"; into ] ->
            result {
                let! at = province from
                let! whom = province who
                let! there = province into
                return send at (SupportMove(whom, there))
            }

        | [ from; "c"; who; "-"; into ]
        | [ from; "convoy"; who; "-"; into ] ->
            result {
                let! at = province from
                let! whom = province who
                let! there = province into
                return send at (Convoys(whom, there))
            }

        | [ from; "disband" ]
        | [ from; "d" ] -> province from |> Result.map (fun at -> send at Disbands)

        | _ ->
            Error
                "Say an order the way the rules print one - 'vie - tri', 'bud s vie - tri', 'nth c lon - bel', 'vie hold'. 'help' has the rest."

    /// A build, which is the one order written the other way round - the piece first, because
    /// there is no piece there yet to name the province by.
    let private build (words: string list) =
        let raise kind where =
            spot where
            |> Result.bind (fun location ->
                match Kind.byName kind with
                | Some kind -> Ok(send location.At (Builds(kind, location.Coast)))
                | None -> Error $"'{kind}' is not an army or a fleet.")

        match words with
        | [ kind; where ] when (Kind.byName kind).IsSome -> raise kind where
        | [ where; kind ] -> raise kind where
        | _ -> Error "Say 'build a vie' or 'build f stp/sc'."

    /// The whole of what this game reads.
    let line (typed: string) =
        let raw = Commands.words typed
        let plainly = raw |> List.map (fun word -> word.ToLowerInvariant())

        match plainly with
        // Read before anything else and off the line as it was typed, because what somebody
        // says to another power is theirs and not this parser's to lower-case, split on
        // dashes, or have an opinion about.
        | "press" :: who :: (_ :: _) ->
            let text = raw |> List.skip 2 |> String.concat " "

            match who with
            | "all"
            | "table"
            | "everyone" -> Ok(Send(Make(Whisper(None, text))))
            | who ->
                match Power.byName who with
                | Some heard -> Ok(Send(Make(Whisper(Some heard, text))))
                | None -> Error $"'{who}' is not a power. There is {Power.names}, or 'all' for the table."

        | [ "press" ]
        | [ "press"; _ ] -> Error "Say 'press <power> <what you want to say>', or 'press all ...' for the table."

        | _ ->

        // `vie-tri` and `vie - tri` are the same order, and a player who leaves the spaces out
        // has not made a mistake. Done after press and before anything else, so it is the one
        // place the shape of a line is tidied up.
        let words =
            Commands.words (typed.Replace("-", " - ").Replace(">", " - "))
            |> List.map (fun word -> word.ToLowerInvariant())

        match words with
        | [ "commit" ]
        | [ "done" ]
        | [ "ready" ]
        | [ "seal" ] -> Ok(Send(Make Commit))

        | [ "cancel"; where ]
        | [ "clear"; where ] -> province where |> Result.map (fun at -> Send(Make(Take at)))

        | "build" :: rest -> build rest

        | [ "disband"; where ]
        | [ "remove"; where ] -> province where |> Result.map (fun at -> send at Disbands)

        // Not a move: something the game can be asked about, answered in the words it was
        // asked in. This game's `rule 8`.
        | [ "borders"; _ ]
        | [ "where"; _ ]
        | [ "orders" ] -> Ok(Asking typed)

        | words -> order (words |> List.filter (fun word -> not (Set.contains word naming)))
