namespace Prototyping.Table

open System
open Prototyping.Common
open Prototyping.Engine

[<NoComparison; NoEquality>]
type Command<'Move> =
    | Send of Msg<'Move>
    | Help
    | Notes of showing: bool option
    | Listing of showing: bool option
    | Logging of showing: bool option
    | Hushing of hushed: bool option
    | Looking of view: string

    /// Draw this console one of the game's own screens from now on, by whatever name the game calls
    /// it. Never read from a typed line here - `Playable.Read` is what turns a word into one, since
    /// the table has no idea what screens a game has or what they are called.
    | Showing of screen: string

    | Asking of question: string
    | Recount
    | Keep
    | Leave
    | Nothing

module Commands =

    // Split on the byte order mark as well as on spaces: a saved record or a settings file written
    // by another program can open with one, and it would otherwise glue itself to the first word.
    [<Literal>]
    let private ByteOrderMark = '﻿'

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

    let players = Counting.several "player" "players"

    /// A table of `count`, or why the game will not deal one - the one sentence for a count the
    /// game does not take, whether it was typed as a number or as a word a seat.
    let tryPlayers (fewest, most) count =
        if count >= fewest && count <= most then Ok count
        elif fewest = most then Error $"{players count}? The game takes {fewest}."
        else Error $"{players count}? The game takes {fewest} to {most}."

    let tryPlayerCount seats text =
        match tryInt text with
        | Some n -> tryPlayers seats n
        | None -> Error $"'{text}' is not a number of players."

    /// The words every game answers to at the prompt, described once. Every game used to describe
    /// them in its own commands box, and the descriptions had drifted - `quit` was said three
    /// ways - so a game lists its own moves and then these. `restart` and `resign` are beside the
    /// list rather than in it: a game says where they go, and not every game has the second.
    let verbs =
        [ "undo, redo", "walk the game back and forward"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
          "log", "hide what the game has been saying"
          "sound, mute", "whether this table is heard as well as read"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "quit", "leave; the record is written, and 'replay' takes the game up again" ]

    let restart =
        "restart", "deal a fresh game to the same players; 'restart 42' deals that one"

    let resign = "resign", "give the game up, but write it down"

    /// The two keys that wind a clock, as the lines they stand for - so + and - mean the same at
    /// every board that has a speed. For a game's `Pressed` to fall back on.
    let winding (key: ConsoleKeyInfo) =
        match key.Key with
        | ConsoleKey.OemPlus
        | ConsoleKey.Add -> Some "faster"
        | ConsoleKey.OemMinus
        | ConsoleKey.Subtract -> Some "slower"
        | _ -> None

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
        | [ "record" ] -> Some(Ok Recount)
        | [ "save" ] -> Some(Ok Keep)
        | [ "notes" ] -> Some(Ok(Notes None))
        | [ "notes"; "on" ] -> Some(Ok(Notes(Some true)))
        | [ "notes"; "off" ] -> Some(Ok(Notes(Some false)))
        | "notes" :: _ -> Some(Error "Say 'notes' to turn them the other way, or 'notes on' or 'notes off'.")
        | [ "commands" ] -> Some(Ok(Listing None))
        | [ "commands"; "on" ] -> Some(Ok(Listing(Some true)))
        | [ "commands"; "off" ] -> Some(Ok(Listing(Some false)))
        | "commands" :: _ -> Some(Error "Say 'commands' to turn the box the other way, or 'commands on' or 'commands off'.")
        | [ "log" ] -> Some(Ok(Logging None))
        | [ "log"; "on" ] -> Some(Ok(Logging(Some true)))
        | [ "log"; "off" ] -> Some(Ok(Logging(Some false)))
        | "log" :: _ ->
            Some(Error "Say 'log' to turn the box the other way, or 'log on' or 'log off'. 'history' is the record itself.")
        | [ "sound" ] -> Some(Ok(Hushing None))
        | [ "sound"; "on" ] -> Some(Ok(Hushing(Some false)))
        | [ "sound"; "off" ] -> Some(Ok(Hushing(Some true)))
        | [ "mute" ] -> Some(Ok(Hushing(Some true)))
        | [ "unmute" ] -> Some(Ok(Hushing(Some false)))
        | "sound" :: _ -> Some(Error "Say 'sound' to turn it the other way, or 'sound on' or 'sound off'. 'mute' is 'sound off'.")
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
