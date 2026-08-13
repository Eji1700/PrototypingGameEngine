// The fourth game: the draft, the lines, the deal, and what a card does when it is played.
//
// Half of this is the ordinary thing - the rules should be right. The other half is what a
// fourth game is actually for: this one is the first that is a *deck* rather than a board,
// and the first whose game is three games in a row with three different senses of whose turn
// it is. Every check below that is not about protocols is really about that: the timeline, the
// record, the replay, the shared verbs and all three screens work here too, and not one line
// of them was written with cards in mind.
//
//   dotnet fsi tests/compile.fsx

#load "Compiled.fsx"

open TCModel.Engine
open TCModel.Table
open TCModel.Compile
open Checks
open Compiled

let private rules = compiled.Rules

let private dealt seed =
    Update.start rules Session.Seats seed |> Result.toOption |> Option.get

/// Fold a run of moves over a fresh deal.
let private played seed moves =
    moves |> List.fold (fun model move -> Update.update rules (Make move) model) (dealt seed)

let private standing model = Model.state model

let private one = Seat.at 1
let private two = Seat.at 2

/// A draft that gives Player 1 Fire, Water and Darkness, and Player 2 Light, Metal and Gravity -
/// which is the game the rules were described with, picked in the order 1-2-2-1.
let private draft =
    [ Take Fire; Take Light; Take Metal; Take Water; Take Darkness; Take Gravity ]

let private orders =
    [ Arrange [ Water; Darkness; Fire ]; Arrange [ Gravity; Metal; Light ] ]

/// A whole game up to the first card: drafted, arranged, dealt.
let private opened seed = played seed (draft @ orders)

let private handOf seat model = (Session.side seat (standing model)).Hand

let private mentions (needle: string) (text: string) = text.Contains needle

/// The board as one seat reads it. Up here rather than beside the screens below, because what
/// one seat may see of another is checked from the moment the protocols go down.
let private drawn view seat model = view.Board true seat model

let private card protocol value = { Protocol = protocol; Value = value }

// A fixture that lays a card on the table lays a card that *does something*, if that card has
// anything printed on it - so the checks below use blank ones as scenery on purpose, and say so
// where it matters. This is checked rather than remembered: get it wrong and the game stops to
// ask a question in the middle of a check about something else.
let private blank protocol value =
    let chosen = card protocol value

    if Printed.says chosen then
        failwith $"{Card.name chosen} has text on it and cannot be scenery"

    chosen

/// Every card of one player's, wherever it is - in the deck, in hand, discarded, or on the
/// table. Which way up it is lying is not a fact about where it is, so a stack is read back
/// down to the cards in it.
let private allOf side =
    side.Deck
    @ side.Hand
    @ side.Discard
    @ (Lines.all
       |> List.collect (fun line -> Side.stack line side |> List.map (fun placed -> placed.Card)))

// --- what the game says is wrong with itself ---------------------------------------------
//
// The one thing a game built out of data can check about itself before anybody sits down.
// Twelve protocols, six cards apiece and a draft of six picks are three lists that have to
// agree with each other, and this is where they say whether they do.

report "the game finds nothing wrong with itself" [] compiled.Faults

report "fifteen protocols, none of them twice" 15 (List.distinct Protocol.all |> List.length)

report "six cards to a protocol" 6 Card.PerProtocol

report "eighteen to a deck" 18 Deck.Size

report "ninety cards in all" 90 (Protocol.all |> List.collect Card.inProtocol |> List.distinct |> List.length)

// --- the draft ------------------------------------------------------------------------------

report "the draft is six picks" 6 Draft.Picks

report "1-2-2-1: one, two, two, one, one, two" [ one; two; two; one; one; two ] Draft.order

report "three picks each" [ 3; 3 ] (Session.seats |> List.map Draft.picksBy)

report
    "the first pick belongs to the first seat"
    one
    (rules.Active(standing (dealt 1UL)))

report
    "the second and third belong to the other"
    [ two; two ]
    ([ 1; 2 ] |> List.map (fun n -> rules.Active(standing (played 1UL (draft |> List.take n)))))

report
    "a protocol already taken is refused"
    (Some(Refused(AlreadyTaken Fire)))
    (Turn.asked (Take Fire) (standing (played 1UL [ Take Fire ])) |> snd |> List.tryHead)

report
    "a refused pick leaves the draft exactly where it was"
    (standing (played 1UL [ Take Fire ]))
    (standing (played 1UL [ Take Fire; Take Fire ]))

report
    "six picks and the draft is over"
    TheProtocols
    (Session.doing (standing (played 1UL draft)))

report
    "each player holds the three they took"
    ([ Fire; Water; Darkness ], [ Light; Metal; Gravity ])
    (let session = standing (played 1UL draft)
     (Session.side one session).Drafted, (Session.side two session).Drafted)

report
    "the six nobody took are gone, not held"
    []
    (let session = standing (played 1UL draft)

     let asked =
         draft
         |> List.choose (function
             | Take protocol -> Some protocol
             | _ -> None)

     Session.seats
     |> List.collect (fun seat -> (Session.side seat session).Drafted)
     |> List.filter (fun protocol -> not (List.contains protocol asked)))

// --- setting the protocols against the lines --------------------------------------------------

let private drafted seed = played seed draft

report
    "an order of two is refused"
    (Some(Refused(NotThree 2)))
    (Turn.asked (Arrange [ Fire; Water ]) (standing (drafted 1UL)) |> snd |> List.tryHead)

report
    "the same protocol twice is refused"
    (Some(Refused(SaidTwice Fire)))
    (Turn.asked (Arrange [ Fire; Fire; Water ]) (standing (drafted 1UL)) |> snd |> List.tryHead)

report
    "a protocol they never drafted is refused"
    (Some(Refused(NotDrafted Light)))
    (Turn.asked (Arrange [ Fire; Water; Light ]) (standing (drafted 1UL)) |> snd |> List.tryHead)

report
    "the second player lays theirs out after the first"
    two
    (rules.Active(standing (played 1UL (draft @ [ orders[0] ]))))

// The protocols go down face down and turn over together, and this is the first thing at this
// game that is genuinely secret. It has to be: a card may be played face up against *either*
// protocol on a line, so an order seen before you have chosen your own is worth a great deal -
// and the seats coming round one at a time must not turn that into an advantage for whoever is
// asked second.

let private halfLaid seed = played seed (draft @ [ orders[0] ])

report
    "an order laid face down is not in the words the other player hears"
    (true, true)
    (let told = Turn.asked orders[0] (standing (drafted 1UL)) |> snd

     let heardBy seat =
         told |> List.map (compiled.SeenBy seat) |> String.concat " "

     mentions "Water" (heardBy one), not (mentions "Water" (heardBy two)))

report
    "and the other player is still told there was one"
    true
    (let told = Turn.asked orders[0] (standing (drafted 1UL)) |> snd
     told |> List.map (compiled.SeenBy two) |> List.exists (mentions "face down"))

// Which protocol somebody drafted is public; which *line* they put it on is not. So none of
// these can be checked by looking for a protocol's name - it is in the draft either way - and
// they look for the order instead.

report
    "an order laid face down is drawn face down to the other player, and to nobody else"
    (true, false)
    (let model = halfLaid 1UL
     mentions Words.hidden (drawn plain two model), mentions Words.hidden (drawn plain one model))

report
    "the whole of it is kept back, not merely the first line"
    Lines.Count
    (let screen = drawn plain two (halfLaid 1UL)
     screen.Split Words.hidden |> Array.length |> (+) -1)

report
    "nor is it in the record the other player can read"
    (true, false)
    (let model = halfLaid 1UL
     mentions "arrange water" (plain.History one model), mentions "arrange water" (plain.History two model))

report
    "and once both are in, nothing is face down and both orders are on both boards"
    [ true; true ]
    (let model = opened 1UL

     [ one; two ]
     |> List.map (fun seat ->
         let screen = drawn plain seat model

         not (mentions Words.hidden screen)
         && mentions "Water" screen
         && mentions "Gravity" screen))

report
    "the turning over is one thing said to both of them, in the same words"
    true
    (let told = Turn.asked orders[1] (standing (halfLaid 1UL)) |> snd

     let revealed =
         told
         |> List.filter (function
             | Happened(Revealed _) -> true
             | _ -> false)

     match revealed with
     | [ notice ] ->
         let said = compiled.Says notice
         compiled.SeenBy one notice = said
         && compiled.SeenBy two notice = said
         && mentions "Water" said
         && mentions "Gravity" said
     | _ -> false)

report
    "each protocol faces the line it was said for"
    ([ Some Water; Some Darkness; Some Fire ], [ Some Gravity; Some Metal; Some Light ])
    (let session = standing (opened 1UL)

     Lines.all |> List.map (fun line -> Side.protocolOn line (Session.side one session)),
     Lines.all |> List.map (fun line -> Side.protocolOn line (Session.side two session)))

// --- the deal --------------------------------------------------------------------------------

report
    "both are dealt once the second order is in"
    ThePlay
    (Session.doing (standing (opened 1UL)))

report
    "five in hand and thirteen left"
    [ 5, 13; 5, 13 ]
    (let session = standing (opened 1UL)

     Session.seats
     |> List.map (fun seat ->
         let side = Session.side seat session
         List.length side.Hand, List.length side.Deck))

report
    "a deck is the eighteen cards of the three protocols drafted, and nothing else"
    [ true; true ]
    (let session = standing (opened 1UL)

     Session.seats
     |> List.map (fun seat ->
         let side = Session.side seat session
         List.sort (side.Deck @ side.Hand) = List.sort (Deck.ofProtocols side.Order)))

report
    "the same seed deals the same two hands"
    true
    (handOf one (opened 9UL) = handOf one (opened 9UL)
     && handOf two (opened 9UL) = handOf two (opened 9UL))

report
    "a different seed does not"
    false
    (handOf one (opened 9UL) = handOf one (opened 10UL))

report
    "the shuffle is a shuffle, not a sort"
    false
    (let side = Session.side one (standing (opened 9UL))
     side.Deck @ side.Hand = Deck.ofProtocols side.Order)

// --- playing a card ----------------------------------------------------------------------------

let private firstCard model = handOf one model |> List.head

/// Where a card may go face up. Exactly one line, at this point in the game - no protocol is
/// drafted twice, so a card's protocol sits in exactly one place on the table.
let private faceUp model card =
    Field.facingLines card (standing model).Field |> List.exactlyOne

/// Any other line, for checking that face up will not go there.
let private elsewhere model card =
    Lines.all |> List.find (fun line -> line <> faceUp model card)

report
    "a card played leaves the hand and lands on the line"
    (4, [ true ])
    (let model = opened 1UL
     let card = firstCard model
     let after = Update.update rules (Make(Play(card, 2, FaceDown))) model |> standing
     let side = Session.side one after

     List.length side.Hand, [ Side.stack 2 side = [ Placed.down card ] ])

report
    "the card is on the line it was played to, and on no other"
    [ []; [ true ]; [] ]
    (let model = opened 1UL
     let card = firstCard model
     let after = Update.update rules (Make(Play(card, 2, FaceDown))) model |> standing
     let side = Session.side one after

     Lines.all
     |> List.map (fun line ->
         match Side.stack line side with
         | [] -> []
         | stack -> [ stack = [ Placed.down card ] ]))

report
    "the newest card is on top of the stack"
    2
    (let model = opened 1UL
     let cards = handOf one model

     let after =
         [ cards[0]; cards[1] ]
         |> List.fold
             (fun model card ->
                 // The other seat plays in between, so both of these are the first player's.
                 let model = Update.update rules (Make(Play(card, 1, FaceDown))) model
                 Update.update rules (Make(Play(handOf two model |> List.head, 1, FaceDown))) model)
             model

     match Side.stack 1 (Session.side one (standing after)) with
     | top :: _ -> (if top.Card = cards[1] then 2 else 1)
     | [] -> 0)

report
    "the turn passes to the other seat"
    two
    (let model = opened 1UL
     rules.Active(standing (Update.update rules (Make(Play(firstCard model, 1, FaceDown))) model)))

report
    "a line that is not there is refused"
    (Some(Refused(NoSuchLine 4)))
    (let model = opened 1UL
     Turn.asked (Play(firstCard model, 4, FaceDown)) (standing model) |> snd |> List.tryHead)

report
    "a card that is not in hand is refused"
    true
    (let model = opened 1UL
     let theirs = handOf two model |> List.head

     match Turn.asked (Play(theirs, 1, FaceDown)) (standing model) |> snd with
     | [ Refused(NotInHand card) ] -> card = theirs
     | _ -> false)

report
    "a refused play leaves the position exactly where it was"
    true
    (let model = opened 1UL
     let before = standing model
     Turn.asked (Play(firstCard model, 9, FaceDown)) before |> fst = None)

// --- which way up ---------------------------------------------------------------------------
//
// The second decision every turn, and the one the rest of the game leans on: face up is worth
// what is printed on it and may only go where its protocol is; face down is worth two and goes
// anywhere. So a hand is never dead, and spending a 5 as a 2 is a real cost.

report
    "the protocols meeting on a line are one from each side of the table"
    [ [ Water; Gravity ]; [ Darkness; Metal ]; [ Fire; Light ] ]
    (let field = (standing (opened 1UL)).Field
     Lines.all |> List.map (fun line -> Field.protocolsOn line field))

report
    "a card may go face up on exactly one line, and it is where its protocol is"
    []
    (let model = opened 1UL
     let field = (standing model).Field

     handOf one model
     |> List.filter (fun card -> Field.facingLines card field <> [ faceUp model card ]))

report
    "face up, where its protocol is, is taken - and is worth what is printed on it"
    true
    (let model = opened 1UL
     let card = firstCard model
     let line = faceUp model card
     let after = Update.update rules (Make(Play(card, line, FaceUp))) model |> standing

     Side.stack line (Session.side one after) = [ Placed.up card ]
     && Side.valueOn line (Session.side one after) = card.Value)

report
    "face up anywhere else is refused, and the refusal says where it could have gone"
    true
    (let model = opened 1UL
     let card = firstCard model
     let said = elsewhere model card

     match Turn.asked (Play(card, said, FaceUp)) (standing model) |> snd with
     | [ Refused(NotFacingThere(refused, at, couldGo)) ] -> refused = card && at = said && couldGo = [ faceUp model card ]
     | _ -> false)

report
    "and the words for it name the line it could have gone to"
    true
    (let model = opened 1UL
     let card = firstCard model
     let line = faceUp model card

     Turn.asked (Play(card, elsewhere model card, FaceUp)) (standing model)
     |> snd
     |> List.map compiled.Says
     |> List.exists (fun said -> mentions $"line {line}" said && mentions "face down" said))

report
    "face down goes on every line there is"
    [ true; true; true ]
    (let model = opened 1UL
     let card = firstCard model

     Lines.all
     |> List.map (fun line -> Turn.asked (Play(card, line, FaceDown)) (standing model) |> fst |> Option.isSome))

report
    "face down is worth two, whatever is printed on it"
    [ 2; 2; 2; 2; 2; 2 ]
    (Card.values |> List.map (fun value -> Placed.value (Placed.down { Protocol = Fire; Value = value })))

report
    "face up is worth what is printed on it"
    Card.values
    (Card.values |> List.map (fun value -> Placed.value (Placed.up { Protocol = Fire; Value = value })))

report
    "a stack is worth the sum of what is in it"
    (2, 2 + 2)
    (let model = opened 1UL
     let cards = handOf one model

     let after one' =
         let model = Update.update rules (Make(Play(cards[one'], 1, FaceDown))) model
         model, Update.update rules (Make(Play(handOf two model |> List.head, 1, FaceDown))) model

     let first, passed = after 0
     let both = Update.update rules (Make(Play(cards[1], 1, FaceDown))) passed

     Side.valueOn 1 (Session.side one (standing first)), Side.valueOn 1 (Session.side one (standing both)))

report
    "a five spent face down is worth two, and the stack says so"
    (5, 2)
    (let model = opened 1UL
     let five = { Protocol = Water; Value = 5 }

     // Straight into a hand, so this does not depend on what the shuffle dealt.
     let holding =
         { standing model with
             Field = (standing model).Field |> Field.update one (fun side -> { side with Hand = [ five ] }) }

     let played face =
         match Turn.asked (Play(five, 1, face)) holding with
         | Some after, _ -> Side.valueOn 1 (Session.side one after)
         | None, _ -> -1

     played FaceUp, played FaceDown)

// --- refreshing ----------------------------------------------------------------------------------
//
// The second clock. Nothing is drawn at the end of a turn, so five cards is five turns of tempo
// and the turn spent getting five more is a turn the other player spends getting closer to ten.

/// Play the whole hand away, a card a turn, the other seat doing the same - so both hands are
/// empty and neither has built anything worth compiling.
let private handsSpent seed =
    [ 1 .. Deck.HandSize ]
    |> List.fold
        (fun model n ->
            let line = (n % Lines.Count) + 1

            [ one; two ]
            |> List.fold
                (fun model seat ->
                    match handOf seat model with
                    | [] -> model
                    | card :: _ -> Update.update rules (Make(Play(card, line, FaceDown))) model)
                model)
        (opened seed)

report
    "a hand can be played down to nothing"
    (0, 0)
    (let model = handsSpent 1UL
     List.length (handOf one model), List.length (handOf two model))

report
    "with an empty hand, playing is refused - and the refusal says what to do instead"
    true
    (let session = standing (handsSpent 1UL)
     let anything = card Water 3

     match Turn.asked (Play(anything, 1, FaceDown)) session with
     | None, [ Refused MustRefresh ] -> true
     | _ -> false)

report
    "and the words for it name the command"
    true
    (compiled.Says(Refused MustRefresh) |> mentions "'refresh'")

report
    "refreshing puts the hand down and takes five up"
    (5, 13, 0)
    (let session = standing (opened 1UL)

     match Turn.asked Refresh session with
     | Some after, _ ->
         let side = Session.side one after
         // Five drawn out of the thirteen left, and the five put down are in the discard - so
         // eight remain in the deck and the discard holds the old hand.
         List.length side.Hand, List.length side.Deck + List.length side.Discard, List.length side.Discard - 5
     | None, _ -> -1, -1, -1)

report
    "refreshing costs the whole turn"
    two
    (let session = standing (opened 1UL)

     match Turn.asked Refresh session with
     | Some after, _ -> Session.active after
     | None, _ -> one)

report
    "the hand that comes up is not the hand that went down"
    false
    (let before = handOf one (opened 1UL)

     match Turn.asked Refresh (standing (opened 1UL)) with
     | Some after, _ -> List.sort (Session.side one after).Hand = List.sort before
     | None, _ -> true)

report
    "an empty hand refreshed says so, and comes back to five"
    (true, 5)
    (let session = standing (handsSpent 1UL)

     match Turn.asked Refresh session with
     | Some after, told ->
         let said = told |> List.map compiled.Says |> String.concat " "
         mentions "empty hand" said, List.length (Session.side one after).Hand
     | None, _ -> false, -1)

report
    "a deck run down is built again out of its own discard"
    (5, true)
    (let session = standing (opened 1UL)

     // Everything but two cards into the discard: refreshing has to shuffle it back to find five.
     let thin =
         { session with
             Field =
                 session.Field
                 |> Field.update one (fun side ->
                     { side with
                         Discard = side.Deck |> List.skip 2
                         Deck = side.Deck |> List.truncate 2 }) }

     match Turn.asked Refresh thin with
     | Some after, _ ->
         let side = Session.side one after
         List.length side.Hand, List.length side.Deck > 0
     | None, _ -> -1, false)

report
    "a player with nothing anywhere draws nothing, and the game does not hang on it"
    (0, two)
    (let session = standing (handsSpent 1UL)

     let stripped =
         { session with
             Field = session.Field |> Field.update one (fun side -> { side with Deck = []; Discard = [] }) }

     match Turn.asked Refresh stripped with
     | Some after, _ -> List.length (Session.side one after).Hand, Session.active after
     | None, _ -> -1, one)

report
    "refreshing loses no cards"
    (36, [ 18; 18 ])
    (match Turn.asked Refresh (standing (opened 1UL)) with
     | Some after, _ ->
         let counted = Session.seats |> List.map (fun seat -> Session.side seat after |> allOf |> List.length)
         List.sum counted, counted
     | None, _ -> -1, [])

// --- compiling ---------------------------------------------------------------------------------
//
// Ten or more and strictly more than theirs, checked at the *start* of a turn rather than the
// moment a stack reaches ten - which is the whole tension of the game: a stack at eleven is a
// stack the other player has one turn to answer.
//
// So these are set up by putting cards on a line and then handing the turn over: an ordinary
// move by the other seat passes it back, and the compile happens as that turn begins.

/// A game where `seat` has these cards face up on that line. Whose turn it is, is left alone -
/// setting it here as well would make two of these in a row quietly undo each other, which is
/// exactly the sort of fixture that passes a check by not running it.
let private poised seat line cards session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Stacks = side.Stacks |> Map.add line (cards |> List.map Placed.up) }) }

/// `seat` makes an ordinary move that touches nothing these checks are about, handing the turn
/// to the other player - which is when their compile check runs.
let private handedOverBy seat session =
    let session = { session with ToPlay = seat }
    let played = (Session.side seat session).Hand |> List.head
    Turn.asked (Play(played, Lines.Count, FaceDown)) session

/// The usual one: the second seat hands the turn to the first.
let private handedOver session = handedOverBy two session

report
    "ten is the number, and one card cannot reach it"
    (10, true)
    (Stack.ToCompile, Stack.ToCompile > List.max Card.values)

report
    "a line is won at ten and ahead, and not at nine, and not at a tie"
    [ false; false; true; true ]
    (let session = standing (opened 1UL)

     [ [ card Water 5; blank Water 4 ], []                        // nine, and ahead
       [ card Water 5; blank Water 5 ], [ card Gravity 5; blank Gravity 5 ]  // ten, but level
       [ card Water 5; blank Water 5 ], [ card Gravity 5; blank Gravity 4 ]  // ten against nine
       [ card Water 5; blank Water 5; blank Water 1 ], [] ]        // eleven
     |> List.map (fun (mine, theirs) ->
         let field =
             session |> poised one 1 mine |> poised two 1 theirs |> fun s -> s.Field

         Field.won one 1 field))

report
    "a won line compiles as the turn comes round, without being asked"
    (true, [ Water ])
    (let session = standing (opened 1UL) |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]

     match handedOver session with
     | Some after, _ ->
         let side = Session.side one after
         Side.hasCompiled Water side, Set.toList side.Compiled
     | None, _ -> false, [])

report
    "and it says so, naming the protocol and the line"
    true
    (let session = standing (opened 1UL) |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]

     handedOver session
     |> snd
     |> List.map compiled.Says
     |> List.exists (fun told -> mentions "compiles Water" told && mentions "line 1" told))

report
    "compiling wipes that line, both players' cards alike, into their own discards"
    ((0, 3), (0, 2))
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]
        |> poised two 1 [ blank Gravity 3; blank Gravity 1 ]

     match handedOver session with
     | Some after, _ ->
         let swept seat =
             let side = Session.side seat after
             List.length (Side.stack 1 side), List.length side.Discard

         swept one, swept two
     | None, _ -> (-1, -1), (-1, -1))

report
    "and leaves the other two lines exactly as they were"
    [ 4; 0 ]
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]
        |> poised one 2 [ blank Darkness 4 ]

     match handedOver session with
     | Some after, _ ->
         let side = Session.side one after
         [ Side.valueOn 2 side; Side.valueOn 1 side ]
     | None, _ -> [])

report
    "two lines won at once are both compiled, in line order"
    [ Water, 1; Darkness, 2 ]
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]
        |> poised one 2 [ blank Darkness 5; blank Darkness 5; blank Darkness 2 ]

     // Read off the notices rather than off the set, which is sorted rather than remembered.
     handedOver session
     |> snd
     |> List.choose (function
         | Happened(Compiled(_, protocol, line)) -> Some(protocol, line)
         | _ -> None))

/// A game where a seat has already compiled these.
let private having seat protocols session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side -> { side with Compiled = Set.ofList protocols }) }

report
    "compiling all three wins the game"
    (true, Some(Won one))
    (let session =
        standing (opened 1UL)
        |> having one [ Water; Darkness ]
        |> poised one 3 [ blank Fire 5; blank Fire 5; blank Fire 4 ]

     match handedOver session with
     | Some after, _ -> Session.isOver after, Session.ending after
     | None, _ -> false, None)

report
    "and two of three does not"
    (false, None)
    (let session =
        standing (opened 1UL)
        |> having one [ Water ]
        |> poised one 3 [ blank Fire 5; blank Fire 5; blank Fire 4 ]

     match handedOver session with
     | Some after, _ -> Session.isOver after, Session.ending after
     | None, _ -> true, Some(Abandoned one))

// The same thing again with nothing doctored: ten reached by playing five cards face down, one
// a turn, with the other seat playing elsewhere. Slower to set up and worth it - it is the only
// check here that the *board* says a line is about to go, and the board is the whole of the
// warning a player gets.

/// Ten on line one, the honest way, with the turn just handed to the other player - which is
/// the one turn they have to answer it.
let private nearlyTen seed =
    let model =
        [ 1..4 ]
        |> List.fold
            (fun model n ->
                let mine = handOf one model |> List.head
                let model = Update.update rules (Make(Play(mine, 1, FaceDown))) model
                let theirs = handOf two model |> List.head
                // Alternating, so the other seat never builds ten of its own by accident.
                Update.update rules (Make(Play(theirs, (if n % 2 = 0 then 2 else 3), FaceDown))) model)
            (opened seed)

    let last = handOf one model |> List.head
    Update.update rules (Make(Play(last, 1, FaceDown))) model

report
    "five cards face down is ten, and ten with nothing against it is a won line"
    (10, true, two)
    (let session = standing (nearlyTen 1UL)
     Side.valueOn 1 (Session.side one session), Field.won one 1 session.Field, rules.Active session)

report
    "a line about to compile says so, on both boards"
    [ true; true ]
    (let model = nearlyTen 1UL
     [ one; two ] |> List.map (fun seat -> mentions "ready" (drawn plain seat model)))

report
    "and it compiles the moment that turn comes round"
    (true, false, true)
    (let model = nearlyTen 1UL
     let theirs = handOf two model |> List.head
     let after = Update.update rules (Make(Play(theirs, 2, FaceDown))) model

     Side.hasCompiled Water (Session.side one (standing after)),
     Field.won one 1 (standing after).Field,
     mentions "done" (drawn plain one after))

// --- compiling one that is already compiled ------------------------------------------------------
//
// Reachable because compiling is mandatory: a line whose protocol is already turned over is
// compiled again whether or not its owner wanted it. No nearer winning, the line wiped just the
// same, and the top card of the other deck comes across - which makes it a weapon rather than a
// consolation.

/// A game where `one` has already compiled Water and is about to win line 1 again.
let private secondCompile seed =
    standing (opened seed)
    |> having one [ Water ]
    |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]

report
    "a second compile takes the top card of the other deck, and is no nearer winning"
    (1, true)
    (match handedOver (secondCompile 1UL) with
     | Some after, _ ->
         let side = Session.side one after
         Set.count side.Compiled, Side.hasCompiled Water side
     | None, _ -> -1, false)

report
    "their deck is one lighter and the taker's hand one heavier"
    (12, 6)
    (match handedOver (secondCompile 1UL) with
     | Some after, _ ->
         // The other seat played a card to hand the turn over, so their hand is four.
         List.length (Session.side two after).Deck, List.length (Session.side one after).Hand
     | None, _ -> -1, -1)

report
    "and the card taken is one of theirs, of a protocol the taker never drafted"
    true
    (let before = secondCompile 1UL

     match handedOver before with
     | Some after, _ ->
         let taken =
             (Session.side one after).Hand
             |> List.filter (fun card -> not (List.contains card (Session.side one before).Hand))

         match taken with
         | [ card ] -> List.contains card.Protocol (Session.side two before).Drafted
         | _ -> false
     | None, _ -> false)

report
    "the line still goes, both sides of it"
    (0, 0)
    (let session = secondCompile 1UL |> poised two 1 [ blank Gravity 3 ]

     match handedOver session with
     | Some after, _ -> Side.valueOn 1 (Session.side one after), Side.valueOn 1 (Session.side two after)
     | None, _ -> -1, -1)

report
    "the words name the card to the taker, and say only that one went to the other"
    (true, false)
    (let told = handedOver (secondCompile 1UL) |> snd

     let taken =
         told
         |> List.choose (function
             | Happened(Took(_, card)) -> Some card
             | _ -> None)

     match taken with
     | [ card ] ->
         let heardBy seat = told |> List.map (compiled.SeenBy seat) |> String.concat " "
         mentions (Card.name card) (heardBy one), mentions (Card.name card) (heardBy two)
     | _ -> false, true)

report
    "an empty deck is shuffled from its discard before the card is taken"
    (true, true)
    (let session = secondCompile 1UL

     // Their whole deck moved to their discard: nothing to draw until it is shuffled back.
     let starved =
         { session with
             Field =
                 session.Field
                 |> Field.update two (fun side ->
                     { side with
                         Deck = []
                         Discard = side.Deck @ side.Discard }) }

     match handedOver starved with
     | Some after, told ->
         let side = Session.side two after

         // One card less than what went in, and it is in the other player's hand.
         let took =
             told
             |> List.exists (function
                 | Happened(Took _) -> true
                 | _ -> false)

         took, List.isEmpty side.Discard |> not || List.length side.Deck > 0
     | None, _ -> false, false)

report
    "a player with nothing anywhere is taken nothing from, and the game says so"
    true
    (let session = secondCompile 1UL

     let stripped =
         { session with
             Field = session.Field |> Field.update two (fun side -> { side with Deck = []; Discard = [] }) }

     // Their hand still has a card to play, which is what hands the turn over.
     handedOver stripped
     |> snd
     |> List.exists (function
         | Happened(TookNothing _) -> true
         | _ -> false))

// --- the pile ------------------------------------------------------------------------------------
//
// A card's text is a list of commands, and they resolve one at a time with a look at the table
// between every two of them. Two cards carry any text at all so far, and they are the two shapes
// worth having: Fire-3 is "flip a card, draw a card" - the worked example the pile was designed
// around - and Water-0 hands the choosing to the other player, which is the case that made a
// pile necessary rather than a loop.

/// A seat holding a particular card, **swapped** for one they were holding rather than added to
/// what they had - so the count adds up and, just as importantly, the hand stays the size it was.
///
/// The size matters now that the check cache phase exists: a fixture that quietly dealt a sixth
/// card would have the game stop to trim the hand in the middle of a check about something else.
let private holding seat wanted session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                if List.contains wanted side.Hand then
                    side
                else
                    match side.Hand with
                    | first :: rest ->
                        { side with
                            Hand = wanted :: rest
                            Deck = first :: (side.Deck |> List.filter ((<>) wanted))
                            Discard = side.Discard |> List.filter ((<>) wanted) }
                    | [] ->
                        { side with
                            Hand = [ wanted ]
                            Deck = side.Deck |> List.filter ((<>) wanted) }) }

/// Cards lying face down on a line, for something to point at.
let private lyingDown seat line cards session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Stacks = side.Stacks |> Map.add line (cards |> List.map Placed.down) }) }

let private fireThree = card Fire 3

/// Fire-3 in the first player's hand, and Fire is on line 3 - so it goes face up there.
let private playFireThree session = Turn.asked (Play(fireThree, 3, FaceUp)) session

let private happenings told =
    told
    |> List.choose (function
        | Happened e -> Some e
        | _ -> None)

report
    "a card with nothing printed on it sets nothing off"
    []
    (let session = standing (opened 1UL)
     let plain' = handOf one (opened 1UL) |> List.find (fun c -> not (Printed.says c))

     Turn.asked (Play(plain', faceUp (opened 1UL) plain', FaceUp)) session
     |> snd
     |> List.filter (function
         | Happened(Played _) -> false
         | _ -> true))

report
    "one target and no question: the card flips it, then draws"
    [ true; true ]
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4 ]

     match playFireThree session with
     | Some after, told ->
         [ happenings told
           |> List.exists (function
               | Flipped(_, turned, 1) -> turned.Card = card Water 4 && Placed.isFaceUp turned
               | _ -> false)
           happenings told
           |> List.exists (function
               | Drew(_, 1) -> true
               | _ -> false) ]
     | None, _ -> [ false; false ])

report
    "in that order, and the turn passes only after both"
    (true, two)
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4 ]

     match playFireThree session with
     | Some after, told ->
         let order =
             happenings told
             |> List.choose (function
                 | Flipped _ -> Some "flip"
                 | Drew _ -> Some "draw"
                 | _ -> None)

         order = [ "flip"; "draw" ], Session.active after
     | None, _ -> false, one)

report
    "nothing to point at fizzles, and the command after it still runs"
    (true, true)
    (let session = standing (opened 1UL) |> holding one fireThree

     match playFireThree session with
     | Some _, told ->
         happenings told
         |> List.exists (function
             | Fizzled _ -> true
             | _ -> false),
         happenings told
         |> List.exists (function
             | Drew _ -> true
             | _ -> false)
     | None, _ -> false, false)

report
    "and the words for a fizzle name the card that found nothing"
    true
    (let session = standing (opened 1UL) |> holding one fireThree

     playFireThree session
     |> snd
     |> List.map compiled.Says
     |> List.exists (fun said -> mentions "Fire-3" said && mentions "nothing to do" said))

report
    "more than one target stops the game and asks"
    (true, one, AChoice)
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4; blank Water 5 ]

     match playFireThree session with
     | Some after, _ ->
         (Session.asking after).IsSome, Session.active after, Session.doing after
     | None, _ -> false, two, Nothing)

report
    "the turn does not pass while a card is waiting on an answer"
    (true, false)
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4; blank Water 5 ]

     match playFireThree session with
     | Some after, told ->
         after.ToPlay = one,
         happenings told
         |> List.exists (function
             | Drew _ -> true
             | _ -> false)
     | None, _ -> false, true)

report
    "and every other move is refused, in words that say what may be answered"
    true
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4; blank Water 5 ]

     match playFireThree session with
     | Some waiting, _ ->
         match Turn.asked Refresh waiting with
         | None, [ Refused(AnswerFirst(ACard(_, targets))) as refusal ] ->
             List.length targets = 2
             && mentions "Water-4" (compiled.Says refusal)
             && mentions "Water-5" (compiled.Says refusal)
         | _ -> false
     | None, _ -> false)

report
    "an answer that was not on offer is refused"
    true
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4; blank Water 5 ]

     match playFireThree session with
     | Some waiting, _ ->
         (match Turn.asked (Choose(TheCard(card Darkness 1))) waiting with
          | None, [ Refused(NotOnOffer _) ] -> true
          | _ -> false)
     | None, _ -> false)

report
    "answering carries on where the pile left off: the draw happens, then the turn passes"
    (true, true, two)
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4; blank Water 5 ]

     match playFireThree session with
     | Some waiting, _ ->
         match Turn.asked (Choose(TheCard(card Water 5))) waiting with
         | Some after, told ->
             Side.stack 1 (Session.side one after)
             |> List.exists (fun placed -> placed.Card = card Water 5 && Placed.isFaceUp placed),
             happenings told
             |> List.exists (function
                 | Drew _ -> true
                 | _ -> false),
             Session.active after
         | None, _ -> false, false, one
     | None, _ -> false, false, one)

report
    "and the card that was not chosen is left exactly as it was"
    true
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4; blank Water 5 ]

     match playFireThree session with
     | Some waiting, _ ->
         match Turn.asked (Choose(TheCard(card Water 5))) waiting with
         | Some after, _ ->
             Side.stack 1 (Session.side one after)
             |> List.exists (fun placed -> placed.Card = card Water 4 && not (Placed.isFaceUp placed))
         | None, _ -> false
     | None, _ -> false)

// The case the pile was really built for: a card that stops the game on the player whose turn
// it is *not*.

let private waterZero = card Water 0

report
    "a card can stop the game on the other player, mid-turn"
    (true, two, one)
    (let session = standing (opened 1UL) |> holding one waterZero

     // Water is on line 1 for the first player, so it goes face up there.
     match Turn.asked (Play(waterZero, 1, FaceUp)) session with
     | Some after, _ -> (Session.asking after).IsSome, Session.active after, after.ToPlay
     | None, _ -> false, one, two)

report
    "they choose out of their own hand, and it is their hand that shrinks"
    (4, 5)
    (let session = standing (opened 1UL) |> holding one waterZero

     match Turn.asked (Play(waterZero, 1, FaceUp)) session with
     | Some waiting, _ ->
         let theirs = (Session.side two waiting).Hand

         match Turn.asked (Choose(TheCard(List.head theirs))) waiting with
         | Some after, _ -> List.length (Session.side two after).Hand, List.length theirs
         | None, _ -> -1, -1
     | None, _ -> -1, -1)

report
    "the turn passes once they have answered, and it passes to them"
    two
    (let session = standing (opened 1UL) |> holding one waterZero

     match Turn.asked (Play(waterZero, 1, FaceUp)) session with
     | Some waiting, _ ->
         let theirs = (Session.side two waiting).Hand

         match Turn.asked (Choose(TheCard(List.head theirs))) waiting with
         | Some after, _ -> Session.active after
         | None, _ -> one
     | None, _ -> one)

report
    "a choice survives being written down and read back"
    true
    (match Playable.read compiled (compiled.Write(Make(Choose(TheCard fireThree)))) with
     | Ok(Send(Make(Choose(TheCard read)))) -> read = fireThree
     | _ -> false)

report
    "and a bare card name is a choice, where a bare protocol is a draft"
    (true, true)
    ((match Playable.read compiled "fire-3" with
      | Ok(Send(Make(Choose _))) -> true
      | _ -> false),
     (match Playable.read compiled "fire" with
      | Ok(Send(Make(Take _))) -> true
      | _ -> false))

report
    "the screen says what is being asked, and offers a control per card"
    (true, true)
    (let session =
        standing (opened 1UL)
        |> holding one fireThree
        |> lyingDown one 1 [ blank Water 4; blank Water 5 ]

     match playFireThree session with
     | Some waiting, _ ->
         // Reached through a real fold, so the board is drawn from a model rather than a
         // doctored session.
         let model =
             played 1UL (draft @ orders)
             |> fun model -> { model with Timeline = Timeline.advance (Make Refresh) waiting model.Timeline }

         let screen = drawn plain one model
         mentions "needs you to pick a card" screen, mentions "Water-4" screen && mentions "Water-5" screen
     | None, _ -> false, false)

// --- the other two thirds of a card -----------------------------------------------------------
//
// A card has three commands: the top fires when it becomes face up, the middle applies
// continuously while it is face up and uncovered, and the bottom fires at the end of every turn
// it survives. The top went in with the pile; these are the other two, and the middle is the
// awkward one - a command runs once and is gone, but a rule change has to be *asked* at every
// point in the rules it touches.

report
    "ten of the ninety are written, and the rest say nothing"
    (10, 80)
    (Printed.written, 90 - Printed.written)

report
    "a card counts as what its middle command says, and the stack adds up accordingly"
    (0, 3)
    (let counts = card Metal 5
     let session = standing (opened 1UL) |> poised two 2 [ counts; blank Metal 3 ]

     // Metal-5 says it counts as nothing, so a stack of it and a three is worth three.
     Ruling.worth true (Placed.up counts), Side.valueOn 2 (Session.side two session))

// Which box a standing rule is printed in is the whole of what covering decides. A card played
// over another covers its middle and bottom and leaves its top showing, so a rule in the top box
// survives being built on and the same rule in the bottom box does not.

report
    "a rule in the top box goes on applying after something is played over the card"
    (3, 4)
    (let uncovered = standing (opened 1UL) |> poised two 2 [ card Metal 5; blank Metal 3 ]

     let covered =
         standing (opened 1UL)
         |> poised two 2 [ blank Metal 1; card Metal 5; blank Metal 3 ]

     // Metal-5 says it counts as nothing, in its top box. Covered, it still does - so the pile
     // is the 1 on top plus nothing plus the 3 underneath.
     Side.valueOn 2 (Session.side two uncovered), Side.valueOn 2 (Session.side two covered))

report
    "a rule in the bottom box stops the moment anything covers the card"
    (false, true)
    (let unbreakable = card Light 5

     let breakableWhen cards =
         let session = standing (opened 1UL) |> poised two 3 cards
         let stack = Side.stack 3 (Session.side two session)
         let onTop = Stack.uncovered stack = Some(Placed.up unbreakable)
         Ruling.breakable onTop (Placed.up unbreakable)

     // Light-5 says it cannot be deleted, in its bottom box.
     breakableWhen [ unbreakable ], breakableWhen [ blank Light 1; unbreakable ])

report
    "and only while it is face up: a card lying face down says nothing at all"
    Placed.FaceDownValue
    (let session = standing (opened 1UL) |> lyingDown two 2 [ card Metal 5 ]
     Side.valueOn 2 (Session.side two session))

report
    "a card that cannot be deleted is not a target, so a command pointed only at it finds nothing"
    (true, true)
    (let session =
        standing (opened 1UL)
        |> holding one (card Fire 3)
        // Light-5 says it cannot be deleted. Nothing else is face down for the flip to reach.
        |> poised two 3 [ card Light 5 ]

     match playFireThree session with
     | Some _, told ->
         happenings told
         |> List.exists (function
             | Fizzled _ -> true
             | _ -> false),
         happenings told
         |> List.exists (function
             | Drew _ -> true
             | _ -> false)
     | None, _ -> false, false)

report
    "a bottom command fires at the end of its owner's turn, once per turn it survives"
    (true, true)
    (let session =
        standing (opened 1UL)
        // Light-5 draws a card at the end of its owner's turn. It is the second player's card,
        // so it fires on the second player's turns and not on the first player's.
        |> poised two 3 [ card Light 5 ]

     let drewOn seat =
         let played = (Session.side seat { session with ToPlay = seat }).Hand |> List.head

         Turn.asked (Play(played, 1, FaceDown)) { session with ToPlay = seat }
         |> snd
         |> List.exists (function
             | Happened(Drew(who, _)) -> who = two
             | _ -> false)

     drewOn two, not (drewOn one))

report
    "a card can be taken back off the table and into the hand it came from - theirs, not yours"
    (0, 6, 3)
    (let session =
        standing (opened 1UL)
        |> holding one (card Water 2)
        // One card of theirs, so the return has exactly one thing to point at and does not ask.
        |> poised two 2 [ blank Metal 3 ]

     // Water is on line 1 for the first player, so Water-2 goes face up there.
     match Turn.asked (Play(card Water 2, 1, FaceUp)) session with
     | Some after, _ ->
         Side.valueOn 2 (Session.side two after),
         List.length (Session.side two after).Hand,
         // Back into a hand, not onto a discard: that is what makes it a return.
         List.length (Session.side two after).Discard + 3
     | None, _ -> -1, -1, -1)

report
    "a shift asks twice - which card, and then where"
    (true, true)
    (let session =
        standing (opened 1UL)
        |> holding one (card Darkness 1)
        |> poised one 1 [ blank Water 4 ]
        |> poised one 3 [ blank Fire 4 ]

     // Darkness is on line 2 for the first player, so Darkness-1 goes face up there.
     match Turn.asked (Play(card Darkness 1, 2, FaceUp)) session with
     | Some waiting, _ ->
         match Session.asking waiting with
         | Some { Wanting = ACard(Shift _, targets) } ->
             // It picked which card; now it has to be told where.
             let chosen = targets |> List.map Target.card |> List.head

             match Turn.asked (Choose(TheCard chosen)) waiting with
             | Some next, _ ->
                 (match Session.asking next with
                  | Some { Wanting = ALine _ } -> true
                  | _ -> false),
                 true
             | None, _ -> false, true
         | _ -> false, false
     | None, _ -> false, false)

report
    "and answering the second question moves the card, leaving the line it was on"
    (0, true)
    (let session =
        standing (opened 1UL)
        |> holding one (card Darkness 1)
        |> poised one 1 [ blank Water 4 ]

     match Turn.asked (Play(card Darkness 1, 2, FaceUp)) session with
     | Some waiting, _ ->
         match Turn.asked (Choose(TheCard(blank Water 4))) waiting with
         | Some next, _ ->
             match Turn.asked (Choose(TheLine 3)) next with
             | Some after, _ ->
                 let side = Session.side one after

                 Side.valueOn 1 side,
                 Side.stack 3 side |> List.exists (fun placed -> placed.Card = blank Water 4)
             | None, _ -> -1, false
         | None, _ -> -1, false
     | None, _ -> -1, false)

// Card text is generated from what the card does, which is the whole argument for it being data:
// a card cannot say one thing and do another, and seventy-two of them cannot drift one at a time.

report
    "what a card says is written from what it does"
    [ "Flip any face-down card. Draw a card." ]
    (Words.printed (card Fire 3))

report
    "including the boxes below it, and which box a line is in is said"
    [ "While uncovered: This card cannot be deleted."
      "At the end of your turn, while uncovered: Draw a card." ]
    (Words.printed (card Light 5))

report
    "a top-box rule is printed without that warning, because it does not need one"
    [ "This card counts as 0." ]
    (Words.printed (card Metal 5))

report
    "a card with nothing on it prints nothing"
    []
    (Words.printed (blank Water 4))

report
    "and asking about a card is answered with what it says"
    (true, true)
    (let screen = plain.Answer "what fire-3" (opened 1UL)
     mentions "Flip any face-down card" screen, mentions "Draw a card" screen)

report
    "asking about a blank one says so rather than saying nothing"
    true
    (plain.Answer "what water-4" (opened 1UL) |> mentions "nothing printed on it")

report
    "the board marks a card that has something to read, and does not mark one that has not"
    (true, false)
    (let model = opened 1UL

     /// A model standing at a doctored position, so the board can be drawn from it.
     let shown session =
         drawn plain one { model with Timeline = Timeline.advance (Make Refresh) session model.Timeline }

     let holdingOnly card =
         { standing model with
             Field =
                 (standing model).Field
                 |> Field.update one (fun side -> { side with Hand = [ card ] }) }

     // Counted rather than looked for: the commands block explains what a star means, and so
     // contains one - a check for the character alone would pass whatever the hand held.
     let stars session =
         (shown session).Split '*' |> Array.length

     stars (holdingOnly (card Fire 3)) > stars (holdingOnly (blank Water 4)),
     stars (holdingOnly (blank Water 4)) > stars (holdingOnly (card Fire 3)))

report
    "a line answer survives being written down and read back"
    true
    (match Playable.read compiled (compiled.Write(Make(Choose(TheLine 2)))) with
     | Ok(Send(Make(Choose(TheLine read)))) -> read = 2
     | _ -> false)

// --- an interrupt ----------------------------------------------------------------------------------
//
// "When this card would be covered: First, ..." resolves *before* the covering card lands, and
// the card then lands on whatever the interrupting left behind. It is the only thing in the game
// that happens during a move rather than before or after one.
//
//   Apathy-2  "When this card would be covered: First, flip this card."

report
    "a card about to be covered says its piece first, and the covering card lands after"
    (true, 2)
    (let session =
        standing (opened 1UL)
        |> holding one (blank Water 4)
        // Apathy is nobody's protocol here, so this is scenery placed by hand - what matters is
        // that it is the top card of the line about to be played to.
        |> poised one 1 [ card Apathy 2 ]

     match Turn.asked (Play(blank Water 4, 1, FaceUp)) session with
     | Some after, told ->
         let stack = Side.stack 1 (Session.side one after)

         // The interrupt flipped it face down, and then the Water-4 landed on top of it.
         let flippedFirst =
             told
             |> List.map (function
                 | Happened(Flipped _) -> "flip"
                 | Happened(Played _) -> "play"
                 | _ -> "")
             |> List.filter ((<>) "")

         flippedFirst = [ "flip"; "play" ], List.length stack
     | None, _ -> false, -1)

report
    "and the card underneath is face down when it lands, so it is worth two"
    (Placed.FaceDownValue + 4)
    (let session =
        standing (opened 1UL)
        |> holding one (blank Water 4)
        |> poised one 1 [ card Apathy 2 ]

     match Turn.asked (Play(blank Water 4, 1, FaceUp)) session with
     | Some after, _ -> Side.valueOn 1 (Session.side one after)
     | None, _ -> -1)

/// Whatever is lying on a line already, however it is lying.
let private beneath seat line cards session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side -> { side with Stacks = side.Stacks |> Map.add line cards }) }

report
    "nothing interrupts an empty line, or a card lying face down"
    [ false; false ]
    ([ []; [ Placed.down (card Apathy 2) ] ]
     |> List.map (fun under ->
         let session =
             standing (opened 1UL) |> holding one (blank Water 4) |> beneath one 1 under

         match Turn.asked (Play(blank Water 4, 1, FaceUp)) session with
         | Some _, told ->
             told
             |> List.exists (function
                 | Happened(Flipped _) -> true
                 | _ -> false)
         | None, _ -> true))

report
    "a shift onto a card sets its interrupt off too - covering is covering"
    true
    (let session =
        standing (opened 1UL)
        |> holding one (card Darkness 1)
        |> poised one 1 [ blank Water 5 ]
        |> poised one 3 [ card Apathy 2 ]

     // Darkness-1 shifts one of the first player's cards. Darkness is on line 2 for them.
     match Turn.asked (Play(card Darkness 1, 2, FaceUp)) session with
     | Some waiting, _ ->
         match Session.asking waiting with
         | Some({ Wanting = ACard(Shift _, _) } as question) ->
             match Resolving.choosing question (TheCard(blank Water 5)) waiting with
             | Some next, _ ->
                 match Resolving.choosing (Session.asking next).Value (TheLine 3) next with
                 | Some after, told ->
                     told
                     |> List.exists (function
                         | Happened(Flipped(_, turned, 3)) -> turned.Card = card Apathy 2
                         | _ -> false)
                 | None, _ -> false
             | None, _ -> false
         | _ -> false
     | None, _ -> false)

report
    "and the interrupt is printed as one"
    [ "When this card would be covered, first: Flip any uncovered face-up card in this line." ]
    (Words.printed (card Apathy 2))

// --- you may, and if you do -----------------------------------------------------------------------
//
// Two real cards, and they are the first two in from the ninety. Both are here for what they
// settle rather than for what they do.
//
//   Fire-1   "Discard 1 card. If you do, delete 1 card."
//   Death-1  "You may draw 1 card. If you do, delete 1 other card, then delete this card."

report
    "a command that could not happen stops everything waiting behind it"
    (true, false)
    (let session =
        standing (opened 1UL)
        |> holding one (card Fire 1)
        // Something to delete, if the delete were ever reached.
        |> poised two 2 [ blank Metal 3 ]

     // An empty hand, so the discard cannot happen - and Fire-1 is played out of it, which is
     // exactly the position the card gets into.
     let empty =
         { session with
             Field = session.Field |> Field.update one (fun side -> { side with Hand = [ card Fire 1 ] }) }

     match Turn.asked (Play(card Fire 1, 3, FaceUp)) empty with
     | Some after, told ->
         told
         |> List.exists (function
             | Happened(Fizzled _) -> true
             | _ -> false),
         // The card that would have been deleted is still there.
         Side.stack 2 (Session.side two after) |> List.isEmpty
     | None, _ -> false, true)

report
    "and when it can happen, what was waiting happens too"
    true
    (let session =
        standing (opened 1UL)
        |> holding one (card Fire 1)
        |> poised two 2 [ blank Metal 3 ]

     // Answer every question the card asks, whatever it asks, keeping what it said.
     let rec answered model told count =
         if count > 8 then
             told
         else
             match Session.asking model with
             | Some({ Wanting = ACard(_, targets) } as question) ->
                 match Resolving.choosing question (TheCard(Target.card (List.head targets))) model with
                 | Some next, said -> answered next (told @ said) (count + 1)
                 | None, _ -> told
             | _ -> told

     // Which card the delete lands on is the player's business - `Select.any` reaches the
     // Fire-1 itself as readily as anything of theirs. What is being checked is that the
     // delete happened at all, which is what the discard before it unlocked.
     match Turn.asked (Play(card Fire 1, 3, FaceUp)) session with
     | Some waiting, told ->
         answered waiting told 0
         |> List.exists (function
             | Happened(Deleted _) -> true
             | _ -> false)
     | None, _ -> false)

report
    "an offer is an offer: saying no leaves the whole of the rest undone"
    (true, false)
    (let session = standing (opened 1UL) |> holding one (card Death 1)

     // Death is nobody's protocol in this fixture, so it goes face down - which is not the
     // point; the point is the command, asked directly.
     match Turn.asked (Play(card Death 1, 1, FaceDown)) session with
     | Some _, _ ->
         let asked, _ =
             Resolving.settle
                 { session with
                     Pile =
                         [ Run(
                               IfYouDo(May(Draw 1), [ Delete Select.any ]),
                               { Owner = one
                                 Saying = card Death 1
                                 Line = 1 }
                           ) ] }
                 []

         match Session.asking asked with
         | Some { Wanting = Whether _ } ->
             match Resolving.choosing (Session.asking asked).Value No asked with
             | Some after, _ -> List.length (Session.side one after).Hand = Deck.HandSize, after.Did
             | None, _ -> false, true
         | _ -> false, true
     | None, _ -> false, true)

report
    "and saying yes lets it through"
    (true, true)
    (let session = standing (opened 1UL) |> holding one (card Death 1)

     let asked, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           IfYouDo(May(Draw 1), [ Draw 1 ]),
                           { Owner = one
                             Saying = card Death 1
                             Line = 1 }
                       ) ] }
             []

     match Resolving.choosing (Session.asking asked).Value Yes asked with
     // One drawn by the offer and one by what was waiting behind it.
     | Some after, _ -> List.length (Session.side one after).Hand = Deck.HandSize + 2, after.Did
     | None, _ -> false, false)

report
    "an offer of something impossible is not made at all"
    true
    (let session =
        standing (opened 1UL)
        |> holding one (card Death 1)
        // Nothing on the table, so there is nothing to delete and nothing to ask about.
        |> poised one 1 []
        |> poised two 1 []

     let after, told =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           May(Delete(Select.any |> Select.theirs)),
                           { Owner = one
                             Saying = card Death 1
                             Line = 1 }
                       ) ] }
             []

     (Session.asking after).IsNone
     && told
        |> List.exists (function
            | Happened(Fizzled _) -> true
            | _ -> false))

report
    "yes and no survive being written down and read back"
    [ true; true ]
    ([ Yes; No ]
     |> List.map (fun said ->
         match Playable.read compiled (compiled.Write(Make(Choose said))) with
         | Ok(Send(Make(Choose read))) -> read = said
         | _ -> false))

report
    "and both cards print what they do"
    [ "Discard a card. If you do, delete any card."
      "You may draw a card. If you do, delete any card, then delete any card in this line." ]
    ([ card Fire 1; card Death 1 ] |> List.map (fun each -> Words.printed each |> List.head))

// --- the cache, and the phase that checks it ------------------------------------------------------
//
// The cache is the hand. A card that draws can put it over five, and the check cache phase takes
// it back down - by the player it belongs to, a card at a time, so every one of them is an
// ordinary discard and asks the way any other discard would.

report
    "a hand under its limit is left alone"
    (Deck.HandSize - 1, false)
    (let model = opened 1UL
     let played = handOf one model |> List.head

     match Turn.asked (Play(played, 1, FaceDown)) (standing model) with
     | Some after, _ -> List.length (Session.side one after).Hand, (Session.asking after).IsSome
     | None, _ -> -1, true)

report
    "a hand over its limit is asked to come back down to it"
    (true, one)
    (let session = standing (opened 1UL)

     // Two too many, which is what a card that drew three would leave behind.
     let bulging =
         { session with
             Field =
                 session.Field
                 |> Field.update one (fun side ->
                     { side with
                         Hand = side.Hand @ (side.Deck |> List.truncate 2)
                         Deck = side.Deck |> List.skip 2 }) }

     let played = (Session.side one bulging).Hand |> List.head

     match Turn.asked (Play(played, 1, FaceDown)) bulging with
     | Some after, _ -> (Session.asking after).IsSome, Session.active after
     | None, _ -> false, two)

report
    "and answering trims it, one card at a time, until it is down to five"
    (Deck.HandSize, two)
    (let session = standing (opened 1UL)

     let bulging =
         { session with
             Field =
                 session.Field
                 |> Field.update one (fun side ->
                     { side with
                         Hand = side.Hand @ (side.Deck |> List.truncate 2)
                         Deck = side.Deck |> List.skip 2 }) }

     let played = (Session.side one bulging).Hand |> List.head

     // Answer the trim as many times as it asks, then look.
     let rec settle model count =
         if count > 10 then
             model
         else
             match Session.asking model with
             | Some { Wanting = ACard(_, targets) } ->
                 match Turn.asked (Choose(TheCard(Target.card (List.head targets)))) model with
                 | Some next, _ -> settle next (count + 1)
                 | None, _ -> model
             | _ -> model

     match Turn.asked (Play(played, 1, FaceDown)) bulging with
     | Some after, _ ->
         let settled = settle after 0
         List.length (Session.side one settled).Hand, Session.active settled
     | None, _ -> -1, one)

// --- shown, and shown again ----------------------------------------------------------------------
//
// The middle box fires when it becomes *shown*, and that is three different moments: the card
// played face up, the card flipped face up, and the card **uncovered** by whatever was over it
// leaving. A card can say its piece more than once in a game.

let private drew told =
    told
    |> List.exists (function
        | Happened(Drew _) -> true
        | _ -> false)

report
    "a card covered says nothing, and says it again when whatever covered it goes"
    (false, true)
    (let covered =
        standing (opened 1UL)
        // Fire-3 face up on line 3 with a blank Fire-1 played over it. Both face up; only the
        // top one is shown.
        |> poised one 3 [ blank Fire 0; card Fire 3 ]

     // Settling once takes a record of what is shown - the Fire-1, and not the Fire-3 under it.
     let quiet, first = Resolving.settle covered []

     // Take the covering card away, and the one underneath is shown for the first time.
     let uncovered =
         { quiet with
             Field =
                 quiet.Field
                 |> Field.update one (fun side ->
                     { side with
                         Stacks = side.Stacks |> Map.add 3 [ Placed.up (card Fire 3) ] }) }

     drew first, drew (Resolving.settle uncovered [] |> snd))

report
    "and a card at the bottom of a stack says nothing however face up it is"
    (2, 1)
    (let session = standing (opened 1UL) |> poised two 2 [ blank Metal 3; blank Metal 4 ]
     let stack = Side.stack 2 (Session.side two session)

     // Two face up, one of them shown.
     List.length (stack |> List.filter Placed.isFaceUp),
     List.length (stack |> List.filter (fun placed -> Stack.uncovered stack = Some placed)))

// --- the control component ------------------------------------------------------------------------
//
// The optional rule, and it ships as a *second game* rather than a flag - one function, two
// `Playable`s, and nothing above either of them the wiser. It is also the honest test of the
// pile: what the component costs its holder is a question no card wrote, and if the pile can
// carry that it can carry the cards.

let private controlRules = controlled.Rules

let private openedWith seed =
    (draft @ orders)
    |> List.fold
        (fun model move -> Update.update controlRules (Make move) model)
        (Update.start controlRules Session.Seats seed |> Result.toOption |> Option.get)

/// The other seat hands the turn back, which is when a start-of-turn happens.
let private handedBack session =
    let session = { session with ToPlay = two }
    let played = (Session.side two session).Hand |> List.head
    Turn.asked (Play(played, Lines.Count, FaceDown)) session

let private holdingControl seat session = { session with Control = HeldBy seat }

report
    "the two games are the same game, and differ by one thing"
    ("compile", "compile-control", true)
    (compiled.Name, controlled.Name, compiled.Faults = controlled.Faults)

report
    "without the rule there is no component, and there never is one"
    (NotInPlay, NotInPlay)
    ((standing (opened 1UL)).Control,
     (match handedOver (standing (opened 1UL) |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]) with
      | Some after, _ -> after.Control
      | None, _ -> InTheMiddle))

report
    "with the rule it starts in the middle"
    InTheMiddle
    ((standing (openedWith 1UL)).Control)

report
    "leading a lane is not winning one: no ten needed, and a tie is still not a lead"
    [ true; false; false ]
    (let session = standing (openedWith 1UL)

     [ [ card Water 1 ], []
       [ card Water 1 ], [ card Gravity 1 ]
       [], [] ]
     |> List.map (fun (mine, theirs) ->
         let doctored = session |> poised one 1 mine |> poised two 1 theirs
         Field.leads one 1 doctored.Field))

report
    "leading two lanes at the start of a turn takes the component out of the middle"
    (HeldBy one, true)
    (let session =
        standing (openedWith 1UL)
        |> poised one 1 [ blank Water 1 ]
        |> poised one 2 [ blank Darkness 2 ]

     match handedBack session with
     | Some after, told ->
         after.Control,
         told
         |> List.map controlled.Says
         |> List.exists (mentions "takes the control component")
     | None, _ -> InTheMiddle, false)

report
    "leading one does not"
    InTheMiddle
    (let session = standing (openedWith 1UL) |> poised one 1 [ blank Water 1 ]

     match handedBack session with
     | Some after, _ -> after.Control
     | None, _ -> HeldBy one)

report
    "and it comes off the other player when it has to"
    (HeldBy one, true)
    (let session =
        standing (openedWith 1UL)
        |> holdingControl two
        |> poised one 1 [ blank Water 1 ]
        |> poised one 2 [ blank Darkness 2 ]

     match handedBack session with
     | Some after, told ->
         after.Control,
         told |> List.map controlled.Says |> List.exists (mentions "from Player 2")
     | None, _ -> InTheMiddle, false)

// What it costs. Holding the component and compiling or refreshing means the protocols move
// first - and it is not a choice about whether, only about where.

report
    "holding it and refreshing stops the game and asks for a different order"
    (true, one, AChoice)
    (let session = standing (openedWith 1UL) |> holdingControl one

     match Turn.asked Refresh session with
     | Some after, _ -> (Session.asking after).IsSome, Session.active after, Session.doing after
     | None, _ -> false, two, Nothing)

report
    "five orders are offered, and the one they are in is not among them"
    (5, false)
    (let session = standing (openedWith 1UL) |> holdingControl one

     match Turn.asked Refresh session with
     | Some after, _ ->
         match Session.asking after with
         | Some { Wanting = AnOrder offered } -> List.length offered, List.contains [ Water; Darkness; Fire ] offered
         | _ -> -1, true
     | None, _ -> -1, true)

report
    "not holding it, refreshing asks nothing"
    (false, two)
    (let session = standing (openedWith 1UL)

     match Turn.asked Refresh session with
     | Some after, _ -> (Session.asking after).IsSome, Session.active after
     | None, _ -> true, one)

report
    "answering moves the protocols and nothing else - the stacks stay exactly where they are"
    ([ Darkness; Water; Fire ], [ card Water 4 ], [ card Darkness 2 ])
    (let session =
        standing (openedWith 1UL)
        |> holdingControl one
        |> poised one 1 [ blank Water 4 ]
        |> poised one 2 [ blank Darkness 2 ]

     match Turn.asked Refresh session with
     | Some waiting, _ ->
         match Turn.asked (Arrange [ Darkness; Water; Fire ]) waiting with
         | Some after, _ ->
             let side = Session.side one after

             side.Order,
             Side.stack 1 side |> List.map (fun placed -> placed.Card),
             Side.stack 2 side |> List.map (fun placed -> placed.Card)
         | None, _ -> [], [], []
     | None, _ -> [], [], [])

report
    "and the refresh it was holding up then happens"
    (5, two)
    (let session = standing (openedWith 1UL) |> holdingControl one

     match Turn.asked Refresh session with
     | Some waiting, _ ->
         match Turn.asked (Arrange [ Darkness; Water; Fire ]) waiting with
         | Some after, _ -> List.length (Session.side one after).Hand, Session.active after
         | None, _ -> -1, one
     | None, _ -> -1, one)

report
    "an order that was not offered is refused"
    true
    (let session = standing (openedWith 1UL) |> holdingControl one

     match Turn.asked Refresh session with
     | Some waiting, _ ->
         (match Turn.asked (Arrange [ Water; Darkness; Fire ]) waiting with
          | None, [ Refused(NotOnOffer _) ] -> true
          | _ -> false)
     | None, _ -> false)

// And the thing the whole rule is for: compiling is no longer atomic, because the protocol a
// line compiles is read *after* the rearrangement rather than before it.

report
    "holding it and compiling asks first, and the compile has not happened yet"
    (true, false)
    (let session =
        standing (openedWith 1UL)
        |> holdingControl one
        |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]

     match handedBack session with
     | Some after, _ -> (Session.asking after).IsSome, Set.isEmpty (Session.side one after).Compiled |> not
     | None, _ -> false, true)

report
    "a stack built for Water compiles Darkness, because holding the component moved it"
    ([ Darkness ], false)
    (let session =
        standing (openedWith 1UL)
        |> holdingControl one
        |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]

     match handedBack session with
     | Some waiting, _ ->
         match Turn.asked (Arrange [ Darkness; Water; Fire ]) waiting with
         | Some after, _ ->
             let side = Session.side one after
             Set.toList side.Compiled, Side.hasCompiled Water side
         | None, _ -> [], true
     | None, _ -> [], true)

report
    "and the line is wiped just the same"
    0
    (let session =
        standing (openedWith 1UL)
        |> holdingControl one
        |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]

     match handedBack session with
     | Some waiting, _ ->
         match Turn.asked (Arrange [ Darkness; Water; Fire ]) waiting with
         | Some after, _ -> Side.valueOn 1 (Session.side one after)
         | None, _ -> -1
     | None, _ -> -1)

report
    "not holding it, a won line compiles the protocol that was facing it all along"
    [ Water ]
    (let session =
        standing (openedWith 1UL)
        |> poised one 1 [ blank Water 5; blank Water 5; blank Water 1 ]

     match handedBack session with
     | Some after, _ -> Set.toList (Session.side one after).Compiled
     | None, _ -> [])

report
    "the machine plays the game with the component in it, out to a win"
    [ true; true; true ]
    ([ 4UL; 17UL; 99UL ]
     |> List.map (fun seed ->
         let rec walk model rivals count =
             if count > 4000 then
                 model
             else
                 let next, rivals = Machines.answering controlRules Playable.plays rivals model

                 if Timeline.movesMade next.Timeline = Timeline.movesMade model.Timeline then
                     next
                 else
                     walk next rivals (count + 1)

         let start = Update.start controlRules Session.Seats seed |> Result.toOption |> Option.get
         let finished = walk start (controlled.Seating seed [ Some "easy"; Some "easy" ] (standing start)) 0

         match Session.ending (standing finished) with
         | Some(Won _) -> true
         | _ -> false))

// --- what nobody may lose track of ------------------------------------------------------------
//
// Cards are conserved, and how to say that changed the moment a second compile could take a card
// off the other player's deck.
//
// It used to be *eighteen each, wherever they are*, and that was the strongest thing true. It is
// no longer: a card can cross the table now, so a player can be holding nineteen while the other
// is down to seventeen. What survives is **thirty-six in total, each in exactly one place** - and
// the per-player count is a thing that drifts on purpose.
//
// Both are checked below, the weaker one over a game where nothing crossed, so that the day
// something makes cards appear from nowhere, one of them says so.

let private accounted session =
    Session.seats |> List.map (fun seat -> Session.side seat session |> allOf |> List.length)

/// Six cards played in turn, alternating, wherever each will go. Face down, so that where they
/// land is not a question about protocols.
let private sixPlayed seed =
    [ 1..6 ]
    |> List.fold
        (fun model n ->
            let seat = rules.Active(standing model)
            let card = handOf seat model |> List.head
            Update.update rules (Make(Play(card, (n % Lines.Count) + 1, FaceDown))) model)
        (opened seed)

report
    "thirty-six cards in all, wherever they are"
    36
    (accounted (standing (opened 1UL)) |> List.sum)

report
    "eighteen each at the deal, before anything can have crossed"
    [ 18; 18 ]
    (accounted (standing (opened 1UL)))

report
    "and still eighteen each after six cards have been played"
    [ 18; 18 ]
    (accounted (standing (sixPlayed 1UL)))

/// A line loaded past ten out of that player's *own deck*, so nothing is created and the count
/// still means something. The fixtures above invent cards, which is fine when what is being
/// checked is behaviour and useless when what is being checked is arithmetic.
let private loadedLine seat line session =
    let chosen =
        (Session.side seat session).Deck |> List.sortByDescending (fun c -> c.Value) |> List.truncate 3

    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Deck = side.Deck |> List.filter (fun c -> not (List.contains c chosen))
                    Stacks = side.Stacks |> Map.add line (chosen |> List.map Placed.up) }) }

report
    "the loaded line is over ten, so the two checks below are checking something"
    true
    (Side.valueOn 1 (Session.side one (standing (opened 1UL) |> loadedLine one 1)) >= Stack.ToCompile)

report
    "compiling moves cards without making or losing any"
    (36, [ 18; 18 ])
    (match handedOver (standing (opened 1UL) |> loadedLine one 1) with
     | Some after, _ ->
         let counted = Session.seats |> List.map (fun seat -> Session.side seat after |> allOf |> List.length)
         List.sum counted, counted
     | None, _ -> -1, [])

report
    "a second compile takes a card across the table: nineteen against seventeen, and still thirty-six"
    (36, [ 19; 17 ])
    (match handedOver (standing (opened 1UL) |> having one [ Water ] |> loadedLine one 1) with
     | Some after, _ ->
         let counted = Session.seats |> List.map (fun seat -> Session.side seat after |> allOf |> List.length)
         List.sum counted, counted
     | None, _ -> -1, [])

report
    "no card is in two places at once"
    [ true; true ]
    (Session.seats
     |> List.map (fun seat ->
         let everywhere = Session.side seat (standing (sixPlayed 1UL)) |> allOf
         List.distinct everywhere |> List.length = List.length everywhere))

// --- giving it up ------------------------------------------------------------------------------

report
    "a game can be put down at any of the three stages"
    [ true; true; true ]
    ([ dealt 1UL; drafted 1UL; opened 1UL ]
     |> List.map (fun model -> Update.update rules (Make Resign) model |> standing |> Session.isOver))

report
    "and it says who walked away"
    (Some(Abandoned two))
    (let model = opened 1UL
     let model = Update.update rules (Make(Play(firstCard model, 1, FaceDown))) model
     Session.ending (standing (Update.update rules (Make Resign) model)))

// --- the words, and the record they are kept in --------------------------------------------------
//
// A record is written in the words the prompt takes, so what `Write` writes, `Read` has to
// read back to the same move. Checked over every kind of move this game has rather than over
// one of them, because the round trip is exactly the thing that quietly stops holding.

let private roundTrips move =
    match Playable.read compiled (compiled.Write(Make move)) with
    | Ok(Send(Make read)) -> read = move
    | _ -> false

report
    "every move survives being written down and read back"
    []
    ([ Take Fire
       Take Psychic
       Arrange [ Water; Darkness; Fire ]
       Play({ Protocol = Fire; Value = 3 }, 2, FaceUp)
       Play({ Protocol = Gravity; Value = 0 }, 1, FaceDown)
       Resign ]
     |> List.filter (roundTrips >> not)
     |> List.map (compiled.Write << Make))

report
    "the short forms mean the same as the long ones"
    [ true; true; true ]
    ([ "fire", "draft fire"
       "water darkness fire", "arrange water darkness fire"
       "fire-3 2", "play fire-3 2" ]
     |> List.map (fun (short, long) ->
         match Playable.read compiled short, Playable.read compiled long with
         | Ok(Send a), Ok(Send b) -> a = b
         | _ -> false))

report
    "a word that is not a protocol is refused where it was typed"
    true
    (match Playable.read compiled "brimstone" with
     | Error problem -> mentions "not a protocol" problem
     | Ok _ -> false)

report
    "a card written the wrong way is refused where it was typed"
    true
    (match Playable.read compiled "fire3 2" with
     | Error problem -> mentions "fire-3" problem
     | Ok _ -> false)

report
    "a card at the draft is told what the game is asking for instead"
    true
    (let told = Turn.asked (Play({ Protocol = Fire; Value = 1 }, 1, FaceDown)) (standing (dealt 1UL)) |> snd
     told |> List.map compiled.Says |> List.exists (mentions "the draft is still going"))

// --- the machinery this game gets for nothing -------------------------------------------------
//
// None of the following is about protocols. It is the engine and the table, checked through a
// fourth game to see that they still do not know which one they are carrying.

report
    "a table of three is turned away, in words a person can read"
    true
    (match rules.Deal 3 1UL with
     | Error problem -> mentions "Compile takes 2" problem
     | Ok _ -> false)

report
    "undo takes the position back, and the record grows rather than shrinks"
    (true, true)
    (let model = opened 1UL
     let after = Update.update rules (Make(Play(firstCard model, 1, FaceDown))) model
     let back = Update.update rules Undo after

     let entries journal = Journal.entries journal |> List.length

     standing back = standing model, entries back.Journal = entries after.Journal + 1)

report
    "a game replays from its record to exactly where it was"
    true
    (let moves = draft @ orders |> List.map Make

     let recorded = played 1UL (draft @ orders)

     match Update.replay rules Session.Seats 1UL moves with
     | Ok again -> standing again = standing recorded
     | Error _ -> false)

/// Two machines, a whole game, from the draft to whatever ends it.
///
/// The cap is a runaway guard rather than a length: a game of this takes a few hundred moves
/// once refreshing is in, and a game that reached the cap would be a stalemate rather than a
/// slow win - which is a thing worth failing on rather than waiting through.
let private machineGame seed =
    let rec walk model rivals count =
        if count > 4000 then
            model
        else
            let next, rivals = Machines.answering rules Playable.plays rivals model

            if Timeline.movesMade next.Timeline = Timeline.movesMade model.Timeline then
                next
            else
                walk next rivals (count + 1)

    let start = dealt seed
    walk start (compiled.Seating seed [ Some "easy"; Some "easy" ] (standing start)) 0

report
    "the machine plays a whole game legally, from the draft to somebody winning"
    (true, Nothing, true)
    (let finished = machineGame 4UL
     let session = standing finished

     // Nothing was refused: every entry in the record moved the game on.
     let refused =
         Journal.entries finished.Journal
         |> List.collect (fun entry -> entry.Told)
         |> List.exists (function
             | Said(Refused _) -> true
             | _ -> false)

     not refused,
     Session.doing session,
     (match Session.ending session with
      | Some(Won _) -> true
      | _ -> false))

report
    "and does it over and over, from four different deals"
    [ true; true; true; true ]
    ([ 4UL; 17UL; 99UL; 2026UL ]
     |> List.map (fun seed ->
         match Session.ending (standing (machineGame seed)) with
         | Some(Won _) -> true
         | _ -> false))

report
    "both seats win some of them - the first is not simply better placed"
    2
    ([ 4UL .. 23UL ]
     |> List.choose (fun seed ->
         match Session.ending (standing (machineGame seed)) with
         | Some(Won winner) -> Some winner
         | _ -> None)
     |> List.distinct
     |> List.length)

report
    "a machine's game replays from its own record"
    true
    (let finished = machineGame 4UL
     let asked = Journal.entries finished.Journal |> List.map (fun entry -> entry.Asked)

     match Update.replay rules Session.Seats 4UL asked with
     | Ok again -> standing again = standing finished
     | Error _ -> false)

// --- the screens --------------------------------------------------------------------------------
//
// The one thing this game hides is a hand, and hiding it is the board's business rather than
// the notices': nothing the game *says* is a secret, and what a player holds is never said.

report
    "a player's own hand is on their own screen"
    true
    (let model = opened 1UL
     let mine = handOf one model
     let screen = drawn plain one model
     mine |> List.forall (fun card -> mentions (Card.name card) screen))

report
    "and is on nobody else's"
    true
    (let model = opened 1UL
     let mine = handOf one model
     let theirs = handOf two model
     let screen = drawn plain two model

     // Their own hand may share a card *name* with nothing here - the two decks are built from
     // six different protocols - so a card of the first player's on the second's screen could
     // only have come from the first player's hand.
     mine
     |> List.filter (fun card -> not (List.contains card theirs))
     |> List.forall (fun card -> not (mentions (Card.name card) screen)))

report
    "a card played face up is on both screens"
    true
    (let model = opened 1UL
     let card = firstCard model
     let after = Update.update rules (Make(Play(card, faceUp model card, FaceUp))) model

     [ one; two ]
     |> List.forall (fun seat -> mentions (Card.name card) (drawn plain seat after)))

report
    "a card played face down is a card to the player who played it, and a two to the other"
    (true, false)
    (let model = opened 1UL
     let card = firstCard model
     let after = Update.update rules (Make(Play(card, 2, FaceDown))) model

     mentions (Card.name card) (drawn plain one after), mentions (Card.name card) (drawn plain two after))

report
    "but what it is worth is on both, because a two is a two to everybody"
    [ true; true ]
    (let model = opened 1UL
     let after = Update.update rules (Make(Play(firstCard model, 2, FaceDown))) model

     [ one; two ]
     |> List.map (fun seat -> mentions $"[{Placed.FaceDownValue}]" (drawn plain seat after)))

report
    "and the words for it name the card only to the player who played it"
    (true, false)
    (let model = opened 1UL
     let card = firstCard model
     let told = Turn.asked (Play(card, 2, FaceDown)) (standing model) |> snd

     let heardBy seat =
         told |> List.map (compiled.SeenBy seat) |> String.concat " "

     mentions (Card.name card) (heardBy one), mentions (Card.name card) (heardBy two))

report
    "how many the other holds is on the screen, without saying what they are"
    true
    (let screen = drawn plain one (opened 1UL)
     mentions "hand 5" screen && mentions "deck 13" screen)

report
    "every view says the same things about the table"
    true
    (let model = opened 1UL
     let card = firstCard model
     let after = Update.update rules (Make(Play(card, 3, FaceUp))) model

     Playable.offered AtATerminal standard compiled
     |> List.forall (fun view -> mentions (Card.name card) (view.Board true one after)))

report
    "the page carries no control the game would not take"
    []
    (let model = opened 1UL
     let page = asPage.Board true one model

     // Every `data-on-click` on the page types a line; each of them has to be a line this
     // game can read back into a move.
     System.Text.RegularExpressions.Regex.Matches(page, "sendLine\\('([^']*)'\\)")
     |> Seq.map (fun found -> found.Groups[1].Value)
     |> Seq.filter (fun line ->
         match Playable.read compiled line with
         | Ok(Send(Make _)) -> false
         | _ -> true)
     |> List.ofSeq)

report
    "the draft offers a button for every protocol still on the table"
    true
    (let page = asPage.Board true one (dealt 1UL)
     Protocol.all |> List.forall (fun protocol -> mentions (Protocol.name protocol) page))

report
    "both seats take a colour, and they are not the same one"
    (2, 2)
    (List.length compiled.Slots, compiled.Slots |> List.map (fun slot -> slot.Standard) |> List.distinct |> List.length)

finish ()
