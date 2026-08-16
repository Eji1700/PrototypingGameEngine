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

    /// The seven numbers a card can carry.
    let values = [ 0..6 ]

    /// The one of the seven each protocol goes without.
    ///
    /// **Every protocol has six cards, and every protocol skips exactly one number.** Twelve of
    /// the fifteen skip the 6 and run 0 to 5. Three of them carry a 6 instead and give up a
    /// number lower down to pay for it - which is why this is a rule about what a protocol is
    /// missing rather than a list of what it has.
    ///
    /// It matters more than a card list would: `Gravity-3` is not a card, and a player who types
    /// it should be told so rather than handed a card that is not in anybody's deck.
    let private without =
        function
        | Gravity -> 3
        | Love -> 0
        | Metal -> 4
        | _ -> 6

    /// A protocol's six, lowest first.
    let inProtocol protocol =
        values
        |> List.filter ((<>) (without protocol))
        |> List.map (fun value -> { Protocol = protocol; Value = value })

    /// How many cards a protocol has - six, whichever six they are.
    let PerProtocol = List.length values - 1

    /// Whether that number is printed in that protocol at all.
    let exists card = card.Value <> without card.Protocol && List.contains card.Value values

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
            | Some protocol, (true, value) when exists { Protocol = protocol; Value = value } ->
                Some { Protocol = protocol; Value = value }
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
type Placed =
    { Card: Card
      Face: Face

      /// Whether this card has been face up **in the place it is lying now**.
      ///
      /// A card face down says nothing, and to the player across the table it is usually not even
      /// a card - but one that was face up here and has since been turned over is a card both
      /// players have read, and pretending otherwise would be the board keeping a secret that was
      /// never one. So it is a fact about the game rather than about a screen, and it lives here.
      ///
      /// It rides on the `Placed` because that is exactly how long it should last. A card
      /// returned to hand, discarded, deleted or swept away by a compile leaves its `Placed`
      /// behind and the knowing goes with it - no list to keep in step, and no way to forget to
      /// forget. A card *shifted* to another line carries it along, because everybody watched it
      /// move and nobody stopped knowing what it was.
      ///
      /// Two positions that differ only in this really are two positions: a Fire-3 played face
      /// down is not the same as a Fire-3 played face up and turned over, because in the second
      /// one the other player knows something. They compare unequal, and they should.
      Seen: bool }

module Placed =

    /// What a card face down is worth, whatever is printed on it.
    [<Literal>]
    let FaceDownValue = 2

    let up card = { Card = card; Face = FaceUp; Seen = true }

    let down card = { Card = card; Face = FaceDown; Seen = false }

    /// A card laid whichever way up it was played. Laid face up is laid where both players can
    /// read it, which is the whole of what `Seen` starts out saying.
    let laid face card =
        { Card = card
          Face = face
          Seen = face = FaceUp }

    /// The same card the other way up.
    ///
    /// Turned face up it has been read by both players, and no amount of turning it back makes
    /// that untrue - so this only ever sets `Seen` and never clears it.
    let turned placed =
        match placed.Face with
        | FaceUp -> { placed with Face = FaceDown }
        | FaceDown -> { placed with Face = FaceUp; Seen = true }

    let value placed =
        match placed.Face with
        | FaceUp -> placed.Card.Value
        | FaceDown -> FaceDownValue

    let isFaceUp placed = placed.Face = FaceUp

    /// Whether a player may read what is printed on this card.
    ///
    /// Face up, anybody may. Face down, the player it belongs to may - they put it there, and a
    /// game that hid a card from the person who played it would be hiding it from the wrong
    /// person - and the other player may only where it has been face up here.
    let readableBy yours placed = isFaceUp placed || yours || placed.Seen

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
