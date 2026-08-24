namespace Prototyping.Compile

open Prototyping.Common

type Card = { Protocol: Protocol; Value: int }

module Card =

    let values = [ 0..6 ]

    // Every protocol is missing one value, so a protocol is six cards rather than seven. Most of
    // them go without the 6; three go without something else instead and keep it.
    let private without =
        function
        | Gravity -> 3
        | Love -> 0
        | Metal -> 4
        | _ -> 6

    let inProtocol protocol =
        values
        |> List.filter ((<>) (without protocol))
        |> List.map (fun value -> { Protocol = protocol; Value = value })

    let PerProtocol = List.length values - 1

    let exists card =
        card.Value <> without card.Protocol && List.contains card.Value values

    let name card =
        $"{Protocol.name card.Protocol}-{card.Value}"

    let key card =
        $"{Protocol.key card.Protocol}-{card.Value}"

    let byName (word: string) =
        match word.Split '-' with
        | [| said; number |] ->
            match Protocol.byName said, System.Int32.TryParse number with
            | Some protocol, (true, value) when exists { Protocol = protocol; Value = value } ->
                Some { Protocol = protocol; Value = value }
            | _ -> None
        | _ -> None

type Face =
    | FaceUp
    | FaceDown

type Placed =
    {
        Card: Card
        Face: Face

        /// Whether the other side has ever had this card face up in front of them. A card turned
        /// face down again is still one they know, and the board is drawn to them accordingly.
        Seen: bool
    }

module Placed =

    [<Literal>]
    let FaceDownValue = 2

    let up card =
        { Card = card
          Face = FaceUp
          Seen = true }

    let down card =
        { Card = card
          Face = FaceDown
          Seen = false }

    let laid face card =
        { Card = card
          Face = face
          Seen = face = FaceUp }

    let turned placed =
        match placed.Face with
        | FaceUp -> { placed with Face = FaceDown }
        | FaceDown ->
            { placed with
                Face = FaceUp
                Seen = true }

    let value placed =
        match placed.Face with
        | FaceUp -> placed.Card.Value
        | FaceDown -> FaceDownValue

    let isFaceUp placed = placed.Face = FaceUp

    let readableBy yours placed = isFaceUp placed || yours || placed.Seen

module Deck =

    let Size = Protocol.Each * Card.PerProtocol

    [<Literal>]
    let HandSize = 5

    let ofProtocols protocols =
        protocols |> List.collect Card.inProtocol

    let private pluck cards rng =
        let n, rng = Rng.intBelow (List.length cards) rng
        List.item n cards, List.removeAt n cards, rng

    // Drawing one at a time out of what is left, which is a Fisher-Yates shuffle written the
    // long way round because the deck is a list.
    let shuffled cards rng =
        let rec draw taken left rng =
            match left with
            | [] -> List.rev taken, rng
            | _ ->
                let card, left, rng = pluck left rng
                draw (card :: taken) left rng

        draw [] cards rng
