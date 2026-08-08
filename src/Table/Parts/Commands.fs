namespace TCModel.Table

open System
open TCModel.Common
open TCModel.Engine

/// Either a message for the game or a request the table answers itself.
///
/// Generic in the move, because everything here except the move is the same at every game:
/// whatever is being played, a person may ask to see it again, take it back, put it down,
/// write it out, or read it differently.
[<NoComparison; NoEquality>]
type Command<'Move> =
    | Send of Msg<'Move>
    | Help
    /// Show or hide the notes that explain the board. Saying neither turns them
    /// whichever way they are not.
    | Notes of showing: bool option
    /// Read the game a different way. Only the name is taken here: which views there
    /// are is a question for whatever is doing the showing, not for the parser.
    | Looking of view: string
    /// Something this game can be asked about that is not a move - why a region is ruled the
    /// way it is, what a piece does. The words are handed over as they were typed and the
    /// game's own view answers them, so nothing here has to know what there is to ask.
    | Asking of question: string
    /// Show the record of the game so far.
    | Recount
    /// Write the record out now.
    | Keep
    /// Put the game down and go.
    | Leave
    | Nothing

/// The words a person can type at any game, read once for all of them.
///
/// The split with a game's own reader is the useful line: `undo`, `save`, `view rich` and
/// `restart` mean the same thing whatever is on the board, so a game that had to spell them
/// out for itself would be a game that could get them subtly wrong. What is left for a game
/// to read is only the words that name its own moves.
///
/// Nothing here is answered - reading a line and acting on one are different jobs, and this
/// is the first. `Solo` and `Lobby` both act on what comes back, which is why neither of them
/// keeps a second opinion about what a line meant.
module Commands =

    /// A typed line as the words in it, in the case they were typed.
    ///
    /// A byte order mark can lead a line that was piped in or saved by an editor. It is
    /// not part of what anyone typed, so it is thrown away with the spaces. The start
    /// menu reads lines from the same keyboard, so it splits them the same way.
    /// Written as a code point rather than as itself, because as itself it is invisible and
    /// the next person to touch this line would have no way of seeing it was there.
    let private ByteOrderMark = char 0xFEFF

    let words (text: string) =
        text.Split([| ' '; '\t'; ByteOrderMark |], StringSplitOptions.RemoveEmptyEntries)
        |> List.ofArray

    let tryInt (text: string) =
        match Int32.TryParse text with
        | true, value -> Some value
        | _ -> None

    let trySeed (text: string) =
        match UInt64.TryParse text with
        | true, value -> Ok value
        | _ -> Error $"'{text}' is not a seed."

    /// How many may play, which the game says and this only enforces. A game of two says
    /// two and two, and the same words turn away a table of three without either the menu
    /// or the command line having an opinion about it.
    let tryPlayerCount (fewest, most) text =
        match tryInt text with
        | Some n when n >= fewest && n <= most -> Ok n
        | Some n when fewest = most -> Error $"{n} players? The game takes {fewest}."
        | Some n -> Error $"{n} players? The game takes {fewest} to {most}."
        | None -> Error $"'{text}' is not a number of players."

    // `Restart` carries both as options, saying "leave it as it is" by leaving it out.
    let private seed text = trySeed text |> Result.map Some

    let private playerCount seats text =
        tryPlayerCount seats text |> Result.map Some

    /// A line, if it is one of the words every game knows. `None` means the line is the
    /// game's own business - which is how a game's reader comes to see only its own moves.
    ///
    /// `resign` is here rather than with the moves because putting a game down is something
    /// a person does to any game; what it *plays* is the game's, and comes in as the move to
    /// send. A game with none simply cannot be resigned, and says so.
    let read seats (resign: 'Move option) (typed: string) : Result<Command<'Move>, string> option =
        match words typed |> List.map (fun word -> word.ToLowerInvariant()) with
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
