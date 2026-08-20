namespace TCModel.Table

open System
open TCModel.Common
open TCModel.Engine

[<NoComparison; NoEquality>]
type Command<'Move> =
    | Send of Msg<'Move>
    | Help
    | Notes of showing: bool option
    | Listing of showing: bool option
    | Looking of view: string
    | Asking of question: string
    | Recount
    | Keep
    | Leave
    | Nothing

module Commands =

    // Split on the byte order mark as well as on spaces: a saved record or a settings file written
    // by another program can open with one, and it would otherwise glue itself to the first word.
    let private ByteOrderMark = char 0xFEFF

    let words (text: string) =
        text.Split([| ' '; '\t'; ByteOrderMark |], StringSplitOptions.RemoveEmptyEntries)
        |> List.ofArray

    let lowered text =
        words text |> List.map (fun word -> word.ToLowerInvariant())

    let tryInt (text: string) =
        match Int32.TryParse text with
        | true, value -> Some value
        | _ -> None

    let trySeed (text: string) =
        match UInt64.TryParse text with
        | true, value -> Ok value
        | _ -> Error $"'{text}' is not a seed."

    let tryPlayerCount (fewest, most) text =
        match tryInt text with
        | Some n when n >= fewest && n <= most -> Ok n
        | Some n when fewest = most -> Error $"{n} players? The game takes {fewest}."
        | Some n -> Error $"{n} players? The game takes {fewest} to {most}."
        | None -> Error $"'{text}' is not a number of players."

    let private seed text = trySeed text |> Result.map Some

    let private playerCount seats text =
        tryPlayerCount seats text |> Result.map Some

    let read seats (resign: 'Move option) (typed: string) : Result<Command<'Move>, string> option =
        match lowered typed with
        | [] -> Some(Ok Nothing)
        | [ "help" ]
        | [ "?" ] -> Some(Ok Help)
        | [ "quit" ]
        | [ "exit" ]
        | [ "q" ] -> Some(Ok Leave)
        | [ "history" ]
        | [ "log" ] -> Some(Ok Recount)
        | [ "save" ] -> Some(Ok Keep)
        | [ "notes" ] -> Some(Ok(Notes None))
        | [ "notes"; "on" ] -> Some(Ok(Notes(Some true)))
        | [ "notes"; "off" ] -> Some(Ok(Notes(Some false)))
        | "notes" :: _ -> Some(Error "Say 'notes' to turn them the other way, or 'notes on' or 'notes off'.")
        | [ "commands" ] -> Some(Ok(Listing None))
        | [ "commands"; "on" ] -> Some(Ok(Listing(Some true)))
        | [ "commands"; "off" ] -> Some(Ok(Listing(Some false)))
        | "commands" :: _ -> Some(Error "Say 'commands' to turn the box the other way, or 'commands on' or 'commands off'.")
        | [ "view"; name ] -> Some(Ok(Looking name))
        | "view" :: _ -> Some(Error "Say 'view <name>' to change how the board is drawn.")
        | [ "undo" ]
        | [ "u" ] -> Some(Ok(Send Undo))
        | [ "redo" ] -> Some(Ok(Send Redo))
        | [ "resign" ] ->
            match resign with
            | Some move -> Some(Ok(Send(Make move)))
            | None -> Some(Error "There is no resigning from this one.")
        | [ "restart" ] -> Some(Ok(Send(Restart(None, None))))
        | [ "restart"; s ] -> Some(seed s |> Result.map (fun s -> Send(Restart(None, s))))
        | [ "players"; n ] -> Some(playerCount seats n |> Result.map (fun n -> Send(Restart(n, None))))
        | [ "players"; n; s ] ->
            Some(
                result {
                    let! n = playerCount seats n
                    let! s = seed s
                    return Send(Restart(n, s))
                }
            )
        | _ -> None
