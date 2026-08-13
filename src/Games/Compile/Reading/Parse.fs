namespace TCModel.Compile

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Compile

/// A typed line as this game's own move.
///
/// Three stages, three verbs, and each of them has a short form because the stage already
/// says which of the three a bare line could be: a lone protocol at the draft is a pick,
/// three of them is an order, and a card and a number is a card going onto a line. The long
/// forms are kept because a record is written in them, and a record that read as a column of
/// bare words would be a record nobody could skim.
module Parse =

    let private protocol word =
        match Protocol.byName word with
        | Some protocol -> Ok protocol
        | None -> Error $"'{word}' is not a protocol. There are: {Protocol.names}."

    let private drafting word = protocol word |> Result.map (Take >> Make >> Send)

    let private arrangement words =
        words
        |> List.fold
            (fun sofar word ->
                match sofar, protocol word with
                | Ok taken, Ok protocol -> Ok(taken @ [ protocol ])
                | Error problem, _ -> Error problem
                | _, Error problem -> Error problem)
            (Ok [])
        |> Result.map (Arrange >> Make >> Send)

    let private playing card line =
        match Card.byName card, Commands.tryInt line with
        | None, _ -> Error $"'{card}' is not a card. They are written like 'fire-3': a protocol, a dash, and a number from 0 to {List.length Card.values - 1}."
        | _, None -> Error $"'{line}' is not a line. They are numbered 1 to {Lines.Count}."
        | Some card, Some line -> Ok(Send(Make(Play(card, line))))

    /// The whole of what this game reads.
    let line typed =
        match Commands.words typed |> List.map (fun word -> word.ToLowerInvariant()) with
        | [ "draft"; taken ]
        | [ "take"; taken ] -> drafting taken

        | [ "arrange"; a; b; c ]
        | [ "order"; a; b; c ] -> arrangement [ a; b; c ]

        | [ "play"; card; line ] -> playing card line

        // The short forms. Which of them a line is, is settled by how many words are in it
        // and by nothing else, so no stage has to be consulted to read one.
        | [ card; line ] -> playing card line
        | [ a; b; c ] -> arrangement [ a; b; c ]
        | [ taken ] -> drafting taken

        | _ ->
            Error
                $"Say a protocol to draft it - 'fire'. Say your {Protocol.Each} in order to set them against the lines - 'water dark fire'. Say a card and a line to play it - 'fire-3 2'. 'help' has the rest."
