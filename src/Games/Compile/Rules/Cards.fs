namespace TCModel.Compile

open TCModel.Common

/// One card: the protocol it belongs to and the number printed on it.
///
/// A protocol's six cards carry six different numbers, so a card is named by the two
/// together and no two cards in a deck are the same card. That is what lets a player name a
/// card rather than a place in their hand - `fire-3` is a card, and it is still that card
/// after the hand beside it has changed.
type Card = { Protocol: Protocol; Value: int }

module Card =

    /// The numbers on a protocol's six cards.
    ///
    /// This is the one thing about a card that is not yet the game's own: what each card
    /// *does* is written on it, and none of that has been said yet. Six numbers is what a
    /// deck needs to be dealt, shuffled, held and played, and it is deliberately the whole of
    /// what is here - so that adding an effect is adding a field to this file rather than
    /// unpicking one.
    let values = [ 0..5 ]

    /// How many cards a protocol has.
    let PerProtocol = List.length values

    /// A protocol's six, lowest first.
    let inProtocol protocol =
        values |> List.map (fun value -> { Protocol = protocol; Value = value })

    /// What a card is called, which is also what a player types for it.
    let name card = $"{Protocol.name card.Protocol}-{card.Value}"

    let key card = $"{Protocol.key card.Protocol}-{card.Value}"

    /// A card as somebody would write it: the protocol, a dash, and the number. Refused
    /// rather than guessed at - a word that is not a card of a protocol that exists is worth
    /// two different sentences.
    let byName (word: string) =
        match word.Split '-' with
        | [| said; number |] ->
            match Protocol.byName said, System.Int32.TryParse number with
            | Some protocol, (true, value) when List.contains value values -> Some { Protocol = protocol; Value = value }
            | _ -> None
        | _ -> None

/// Which way up a card was played.
///
/// The whole of the second decision a player makes every turn, and the reason a hand is never
/// dead: a card you cannot use is still worth something anywhere.
type Face =
    | FaceUp
    | FaceDown

/// A card as it lies in a stack.
///
/// Face down it is worth two and says nothing, whatever is printed on it - so a 5 spent as a 2
/// is a real cost, and a 0 played face down is a real gain. The card underneath is still
/// carried, because it is still that card: it can be turned over later, and what it is then is
/// not something the table gets to invent.
type Placed = { Card: Card; Face: Face }

module Placed =

    /// What a card face down is worth, whatever is printed on it.
    [<Literal>]
    let FaceDownValue = 2

    let up card = { Card = card; Face = FaceUp }

    let down card = { Card = card; Face = FaceDown }

    let value placed =
        match placed.Face with
        | FaceUp -> placed.Card.Value
        | FaceDown -> FaceDownValue

    let isFaceUp placed = placed.Face = FaceUp

/// A deck: the three drafted protocols' cards, shuffled together.
module Deck =

    /// Eighteen: three protocols of six. Worked out rather than written down, so a protocol
    /// that grew a seventh card would grow the deck too.
    let Size = Protocol.Each * Card.PerProtocol

    /// How many are drawn at the start, and how many a hand may hold.
    ///
    /// One number, because they are one rule said at two moments: a refresh takes you *to* five,
    /// and the check cache phase takes you *back to* five when a card has drawn you past it. A
    /// game where they differed would be a game where refreshing could put you over your own
    /// limit.
    [<Literal>]
    let HandSize = 5

    /// The cards of these protocols, unshuffled.
    let ofProtocols protocols = protocols |> List.collect Card.inProtocol

    /// Draw one at random, and what is left. The generator comes back rather than advancing
    /// behind anybody's back, which is what keeps a deal a value and a seed a whole game.
    let private pluck cards rng =
        let n, rng = Rng.intBelow (List.length cards) rng
        List.item n cards, List.removeAt n cards, rng

    /// A shuffle, by drawing the whole deck out one card at a time.
    let shuffled cards rng =
        let rec draw taken left rng =
            match left with
            | [] -> List.rev taken, rng
            | _ ->
                let card, left, rng = pluck left rng
                draw (card :: taken) left rng

        draw [] cards rng
