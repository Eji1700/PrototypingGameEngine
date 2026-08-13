namespace TCModel.Compile

open TCModel.Engine

/// The lines that run across the table. Three of them, one per protocol, and the same three
/// for both players - which is the whole geometry of this game: two players sit opposite each
/// other, and a line is the pair of protocols that meet in the middle of it.
module Lines =

    /// One line per protocol drafted. Said this way rather than as a 3, because the two are
    /// the same fact and a game where they differed would have a protocol with nowhere to go.
    [<Literal>]
    let Count = Protocol.Each

    let all = [ 1..Count ]

    let holds line = line >= 1 && line <= Count

/// One player's half of the field: what they drafted, where they put it, and everything of
/// theirs that is on the table or waiting to be.
///
/// A stack is the run of cards played to one line, newest first - so the card on top of a
/// stack is the head of the list, which is the card everything about a stack is going to want
/// to ask about first.
type Side =
    { /// The three protocols this player took, in the order they took them.
      Drafted: Protocol list
      /// The same three, in the order they face the lines: the first is line one. Empty until
      /// they have been arranged, which is what says the arranging is still to do.
      Order: Protocol list
      /// Top first.
      Deck: Card list
      /// Most recently discarded first.
      Discard: Card list
      Hand: Card list
      /// Line to the cards played on it, newest first.
      Stacks: Map<int, Card list> }

module Side =

    /// A player with nothing yet: no protocols, no deck, no hand. Every side starts here and
    /// is filled in by the draft and the deal, so there is no way to write down a player who
    /// was dealt a hand out of protocols they never took.
    let empty =
        { Drafted = []
          Order = []
          Deck = []
          Discard = []
          Hand = []
          Stacks = Map.empty }

    let stack line side =
        side.Stacks |> Map.tryFind line |> Option.defaultValue []

    /// Which protocol this player has facing that line, if they have arranged yet.
    let protocolOn line side = side.Order |> List.tryItem (line - 1)

    /// And the other way round: which line a protocol of theirs is on.
    let lineOf protocol side =
        side.Order |> List.tryFindIndex ((=) protocol) |> Option.map ((+) 1)

    let holds card side = side.Hand |> List.contains card

    let drafted protocol side =
        { side with Drafted = side.Drafted @ [ protocol ] }

    let arranged order side = { side with Order = order }

    /// Take the top cards into hand - or as many as there are, because a deck that has run
    /// out is a position rather than a mistake.
    let drew count side =
        let taken = side.Deck |> List.truncate count

        { side with
            Deck = side.Deck |> List.skip (List.length taken)
            Hand = side.Hand @ taken }

    /// Put a card from hand onto a line's stack. The card is taken out of the hand it was in
    /// rather than copied out of it, which is the whole of what makes a hand run down.
    let played card line side =
        { side with
            Hand = side.Hand |> List.filter ((<>) card)
            Stacks = side.Stacks |> Map.add line (card :: stack line side) }

/// Both halves of the table.
///
/// Private, so a field can only be built by seating the players and then dealing to them.
/// There is no way to hand the rules a table with one player on it, or with a side belonging
/// to a seat nobody is in.
type Field = private Field of Map<PlayerId, Side>

module Field =

    let ofSeats seats =
        seats |> List.map (fun seat -> seat, Side.empty) |> Map.ofList |> Field

    /// A seat's half. A seat that was never dealt in has nothing rather than nothing to
    /// answer with - which is what an empty side is for.
    let side seat (Field sides) =
        sides |> Map.tryFind seat |> Option.defaultValue Side.empty

    let withSide seat replacement (Field sides) = Field(Map.add seat replacement sides)

    let update seat change field = withSide seat (change (side seat field)) field
