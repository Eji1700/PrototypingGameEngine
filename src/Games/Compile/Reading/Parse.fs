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

    let private playing card line face =
        match Card.byName card, Commands.tryInt line with
        // Not every number is in every protocol - each one goes without one of the seven - so this
        // is a refusal a correctly typed name can earn, and the wording has to leave room for it.
        | None, _ ->
            Error
                $"'{card}' is not a card. They are written like 'fire-3': a protocol, a dash, and one of the {Card.PerProtocol} numbers that protocol has, out of {List.min Card.values} to {List.max Card.values}."
        | _, None -> Error $"'{line}' is not a line. They are numbered 1 to {Lines.Count}."
        | Some card, Some line -> Ok(Send(Make(Play(card, line, face))))

    /// A card named as the answer to whatever the game is waiting on.
    let private choosing (word: string) =
        match Card.byName word with
        | Some card -> Ok(Send(Make(Choose(TheCard card))))
        | None -> Error $"'{word}' is not a card. Answer with one of the cards on offer - they are written like 'fire-3'."

    /// And a line, for the one command that asks twice.
    let private choosingLine (word: string) =
        match Commands.tryInt word with
        | Some line -> Ok(Send(Make(Choose(TheLine line))))
        | None -> Error $"'{word}' is not a line. They are numbered 1 to {Lines.Count}."

    /// The whole of what this game reads.
    let line typed =
        match Commands.words typed |> List.map (fun word -> word.ToLowerInvariant()) with
        | [ "draft"; taken ]
        | [ "take"; taken ] -> drafting taken

        | [ "arrange"; a; b; c ]
        | [ "order"; a; b; c ] -> arrangement [ a; b; c ]

        | [ "refresh" ]
        | [ "r" ] -> Ok(Send(Make Refresh))

        // Not a move: a question, which the game's own view answers. What a card says is read
        // here rather than drawn on the board, because a stack cell is nine characters wide and
        // a card's text is a sentence.
        | "what" :: _ :: _
        | "says" :: _ :: _ -> Ok(Asking typed)

        // Yes and no, for a card that offers rather than insists. Bare words, because that is
        // what somebody answering a yes-or-no question types.
        | [ "yes" ]
        | [ "y" ] -> Ok(Send(Make(Choose Yes)))
        | [ "no" ]
        | [ "n" ] -> Ok(Send(Make(Choose No)))

        // And which of two, for a card that offers a choice rather than an offer. Two words
        // rather than yes and no, because "either discard or flip" has no yes in it.
        | [ "first" ]
        | [ "1st" ] -> Ok(Send(Make(Choose TheFirst)))
        | [ "second" ]
        | [ "2nd" ] -> Ok(Send(Make(Choose TheSecond)))

        | [ "choose"; "line"; n ]
        | [ "pick"; "line"; n ] -> choosingLine n

        | [ "choose"; chosen ]
        | [ "pick"; chosen ] -> choosing chosen

        | [ "play"; card; line ] -> playing card line FaceUp
        | [ "play"; card; line; "down" ]
        | [ "play"; card; line; "face-down" ] -> playing card line FaceDown

        // The short forms. Which of them a line is, is settled by how many words are in it and
        // by what the last of them is - `down` is not a protocol, so a card, a line and `down`
        // cannot be read as an order however hard anybody tries.
        | [ card; line; "down" ]
        | [ card; line; "face-down" ] -> playing card line FaceDown
        | [ card; line ] -> playing card line FaceUp
        | [ a; b; c ] -> arrangement [ a; b; c ]

        // A lone word is a protocol at the draft and a card everywhere else, and the two cannot
        // be confused: a card is written with a dash in it and no protocol has one.
        | [ only ] when (Card.byName only).IsSome -> choosing only
        // And a bare number, which can only be an answer: a line on its own is not a move.
        | [ only ] when (Commands.tryInt only).IsSome -> choosingLine only
        | [ taken ] -> drafting taken

        | _ ->
            Error
                $"Say a protocol to draft it - 'fire'. Say your {Protocol.Each} in order to set them against the lines - 'water dark fire'. Say a card and a line to play it - 'fire-3 2', or 'fire-3 2 down' to play it face down for {Placed.FaceDownValue}. 'help' has the rest."
