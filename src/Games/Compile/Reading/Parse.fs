namespace Prototyping.Compile

open Prototyping.Engine
open Prototyping.Table
open Prototyping.Compile

module Parse =

    let private protocol word =
        match Protocol.byName word with
        | Some protocol -> Ok protocol
        | None -> Error $"'{word}' is not a protocol. There are: {Protocol.names}."

    let private drafting word =
        protocol word |> Result.map (Take >> Make >> Send)

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
        | None, _ ->
            Error
                $"'{card}' is not a card. They are written like 'fire-3': a protocol, a dash, and one of the {Card.PerProtocol} numbers that protocol has, out of {List.min Card.values} to {List.max Card.values}."
        | _, None -> Error $"'{line}' is not a line. They are numbered 1 to {Lines.Count}."
        | Some card, Some line -> Ok(Send(Make(Play(card, line, face))))

    let private choosing (word: string) =
        match Card.byName word with
        | Some card -> Ok(Send(Make(Choose(TheCard card))))
        | None -> Error $"'{word}' is not a card. Answer with one of the cards on offer - they are written like 'fire-3'."

    let private choosingLine (word: string) =
        match Commands.tryInt word with
        | Some line -> Ok(Send(Make(Choose(TheLine line))))
        | None -> Error $"'{word}' is not a line. They are numbered 1 to {Lines.Count}."

    let line typed =
        match Commands.lowered typed with
        | [ "draft"; taken ]
        | [ "take"; taken ] -> drafting taken

        | [ "arrange"; a; b; c ]
        | [ "order"; a; b; c ] -> arrangement [ a; b; c ]

        | [ "refresh" ]
        | [ "r" ] -> Ok(Send(Make Refresh))

        | "what" :: _ :: _
        | "says" :: _ :: _
        | "peek" :: _
        | "pile" :: _ -> Ok(Asking typed)

        | [ "yes" ]
        | [ "y" ] -> Ok(Send(Make(Choose Yes)))
        | [ "no" ]
        | [ "n" ] -> Ok(Send(Make(Choose No)))

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

        | [ card; line; "down" ]
        | [ card; line; "face-down" ] -> playing card line FaceDown
        | [ card; line ] -> playing card line FaceUp
        | [ a; b; c ] -> arrangement [ a; b; c ]

        | [ only ] when (Card.byName only).IsSome -> choosing only
        | [ only ] when (Commands.tryInt only).IsSome -> choosingLine only
        | [ only ] when (Protocol.byName only).IsSome -> drafting only

        | _ -> Ok(Asking typed)
