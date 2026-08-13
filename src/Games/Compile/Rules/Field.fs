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
      Stacks: Map<int, Placed list>
      /// The protocols this player has compiled. All three of them is the game.
      Compiled: Set<Protocol> }

/// What a run of cards on one line is worth. The number both players are playing towards, and
/// the only arithmetic in this game - which is why it is here, once, rather than in whichever
/// screen or machine happened to want it.
/// What a card's middle commands say, asked of one card at a time.
///
/// The awkward half of card text, and this is where the awkwardness is kept. A `Command` runs
/// once and is gone; an `Ongoing` has to be *asked* at every point in the rules it touches, and
/// the whole of that is: what a stack is worth, and what may be deleted. Both are below, and
/// anything added here has to find its own place to be asked from.
///
/// A card lying **face down** says nothing at all. A card that is face up says what is in its
/// top box whether or not anything is built on it, and what is in its bottom box only while
/// nothing is - because a card played over another one covers its middle and bottom and leaves
/// its top showing. Which box a rule is printed in is therefore worth something: the same
/// sentence in the top box survives being covered and in the bottom box does not.
module Ruling =

    let private live uncovered placed =
        if Placed.isFaceUp placed then
            Printed.ongoing uncovered placed.Card
        else
            []

    /// What a card is worth, after whatever it says about itself.
    let worth uncovered placed =
        live uncovered placed
        |> List.tryPick (function
            | CountsAs n -> Some n
            | _ -> None)
        |> Option.defaultValue (Placed.value placed)

    let breakable uncovered placed =
        live uncovered placed |> List.contains Unbreakable |> not

module Stack =

    /// What a stack has to be worth before its line can be compiled. The clock of the whole
    /// game: two cards can reach it and three comfortably do, which is what stops a line
    /// becoming a pile nobody can move.
    [<Literal>]
    let ToCompile = 10

    /// The top card, which is the only one whose text is in play.
    let uncovered cards = List.tryHead cards

    /// What a stack is worth - which is not simply the sum of the cards in it, because a card may
    /// have something to say about what *it* is worth. A covered card can still say it, if it is
    /// printed in the top box.
    let value cards =
        cards |> List.mapi (fun depth placed -> Ruling.worth (depth = 0) placed) |> List.sum

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
          Stacks = Map.empty
          Compiled = Set.empty }

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

    /// Build the deck again out of its own discard, if it has run out and there is a discard to
    /// build it from. The only place in this game a shuffle happens after the deal - and the
    /// reason a long game does not simply run out of cards.
    let private restocked side rng =
        if List.isEmpty side.Deck && not (List.isEmpty side.Discard) then
            let deck, rng = Deck.shuffled side.Discard rng
            { side with Deck = deck; Discard = [] }, rng
        else
            side, rng

    /// Take that many into hand, restocking as often as it takes.
    ///
    /// A player with nothing anywhere draws nothing, and that is a position rather than a
    /// mistake: every card they own can be face up on the table, and then there is genuinely
    /// nothing to draw.
    let rec drawing count side rng =
        if count <= 0 then
            side, rng
        else
            match side.Deck with
            | top :: rest ->
                drawing
                    (count - 1)
                    { side with
                        Deck = rest
                        Hand = side.Hand @ [ top ] }
                    rng
            | [] ->
                let restocked, rng = restocked side rng

                // Nothing came back, so there is nothing left anywhere and asking again would
                // ask forever.
                if List.isEmpty restocked.Deck then
                    restocked, rng
                else
                    drawing count restocked rng

    /// The whole hand to the discard, and a fresh five up. One action, and it costs a turn.
    let refreshed side rng =
        { side with
            Hand = []
            Discard = side.Hand @ side.Discard }
        |> fun side -> drawing Deck.HandSize side rng

    /// Put a card from hand onto a line's stack, which way up it was played. The card is taken
    /// out of the hand it was in rather than copied out of it, which is the whole of what makes
    /// a hand run down.
    let played placed line side =
        { side with
            Hand = side.Hand |> List.filter ((<>) placed.Card)
            Stacks = side.Stacks |> Map.add line (placed :: stack line side) }

    /// What this player's stack on that line is worth.
    let valueOn line side = stack line side |> Stack.value

    /// Everything on that line, off the table and into this player's discard. Which way up a
    /// card was lying stops mattering the moment it leaves: a discard is cards.
    let swept line side =
        { side with
            Discard = (stack line side |> List.map (fun placed -> placed.Card)) @ side.Discard
            Stacks = side.Stacks |> Map.add line [] }

    let compiled protocol side =
        { side with Compiled = Set.add protocol side.Compiled }

    let hasCompiled protocol side = Set.contains protocol side.Compiled

    /// Whether this player has compiled all three - which is the game, and the only thing at
    /// this game that is.
    let hasCompiledAll side =
        List.length side.Order = Protocol.Each
        && side.Order |> List.forall (fun protocol -> hasCompiled protocol side)

    /// The top card of this deck, and the side without it.
    ///
    /// A deck that has run out is built again out of its own discard first, which is the only
    /// place in this game where a shuffle happens twice. `None` is a player with no cards left
    /// anywhere - which is a position rather than a mistake, and it is the taker who goes
    /// without.
    let drawnFrom side rng =
        let side, rng = restocked side rng

        match side.Deck with
        | [] -> None, side, rng
        | top :: rest -> Some top, { side with Deck = rest }, rng

    let took card side = { side with Hand = side.Hand @ [ card ] }

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

    let seats (Field sides) = sides |> Map.toList |> List.map fst

    // --- where a card may be played face up ------------------------------------------------
    //
    // A question about the *line* and never about whose card it is. A card may go face up
    // wherever its protocol is, and the two protocols meeting in the middle of a line belong to
    // two different players - so the opponent's Fire on line 2 lets you play your Fire cards
    // there, and a card taken off the top of their deck can be played face up against the
    // protocol it came from even though that protocol is on their side of the board.
    //
    // Here rather than in `Side` for exactly that reason: no one side of the table can answer
    // it.

    /// Both protocols facing that line, in seating order.
    let protocolsOn line field =
        seats field
        |> List.choose (fun seat -> Side.protocolOn line (side seat field))

    /// Whether a card may be played face up there.
    let allows card line field =
        protocolsOn line field |> List.contains card.Protocol

    /// The lines a card may be played face up on. At most one, because no protocol is drafted
    /// twice - but said as a list, because "none" is a real answer and the sentence a refusal
    /// wants is built out of it.
    let facingLines card field =
        Lines.all |> List.filter (fun line -> allows card line field)

    /// Whose protocol a card belongs to, which is not the same as who is holding it.
    ///
    /// Derived rather than stored, and it can be: protocols are settled at the draft and never
    /// move, so a card names its own home. That is why a card taken off somebody's deck needs no
    /// bookkeeping to be given back - and why an `Owner` field on every card would have been
    /// wrong twice over, duplicating what the zone already says and making two states that are
    /// the same position compare unequal.
    let homeOf card field =
        seats field
        |> List.tryFind (fun seat -> List.contains card.Protocol (side seat field).Drafted)

    // --- which lines have been won ----------------------------------------------------------

    /// The most anybody else has on that line, which at two players is simply theirs.
    let opposing seat line field =
        match seats field |> List.filter ((<>) seat) with
        | [] -> 0
        | others -> others |> List.map (fun other -> Side.valueOn line (side other field)) |> List.max

    /// Whether that line is won: ten or more, and strictly more than the other side.
    ///
    /// Strictly, so a tie is not a win. That one word is what makes playing a card face down
    /// into a lane you have already lost worth doing - two points against a stack of eleven
    /// does nothing, but two points against a stack of ten holds it for another turn.
    let won seat line field =
        let mine = Side.valueOn line (side seat field)
        mine >= Stack.ToCompile && mine > opposing seat line field

    /// Every line this player has won, in line order.
    let winning seat field =
        Lines.all |> List.filter (fun line -> won seat line field)

    // --- and who is leading, which is a different question ------------------------------------

    /// How many lanes a player must lead, strictly, to take the control component.
    [<Literal>]
    let LanesForControl = 2

    /// Leading a line is not winning it: no ten is needed, and a tie is still not a lead.
    let leads seat line field =
        Side.valueOn line (side seat field) > opposing seat line field

    let leading seat field =
        Lines.all |> List.filter (fun line -> leads seat line field) |> List.length
