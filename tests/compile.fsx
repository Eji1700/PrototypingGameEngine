#load "Compiled.fsx"
#load "Conforms.fsx"

open Prototyping.Common

open Prototyping.Engine
open Prototyping.Table
open Prototyping.Compile
open Checks
open Compiled

let private rules = compiled.Rules

let private dealt seed =
    Update.start rules Session.Seats seed |> Result.toOption |> Option.get

let private played seed moves =
    moves
    |> List.fold (fun model move -> Update.update rules (Make move) model) (dealt seed)

let private standing model = Model.state model

let private one = Seat.at 1
let private two = Seat.at 2

let private draft =
    [ Take Fire; Take Light; Take Metal; Take Water; Take Darkness; Take Gravity ]

let private orders =
    [ Arrange [ Water; Darkness; Fire ]; Arrange [ Gravity; Metal; Light ] ]

let private opened seed = played seed (draft @ orders)

let private handOf seat model =
    (Session.side seat (standing model)).Hand

let private mentions (needle: string) (text: string) = text.Contains needle

let private flowing (text: string) =
    text.Split([| ' '; '\t'; '\r'; '\n' |], System.StringSplitOptions.RemoveEmptyEntries)
    |> String.concat " "

let private reads (needle: string) (text: string) =
    mentions (flowing needle) (flowing text)

let private drawn view seat model = view.Board Margins.all seat model

let private card protocol value = { Protocol = protocol; Value = value }

let private printedIn card said =
    let top, middle, bottom = Words.boxes card

    [ "top", top; "middle", middle; "bottom", bottom ]
    |> List.tryPick (fun (which, box) -> if List.contains said box then Some which else None)
    |> Option.defaultValue "no box at all"

let private quiet protocol value =
    let chosen = card protocol value

    if not (Card.exists chosen) then
        failwith $"{Card.name chosen} is not a card any protocol has"

    let text = Printed.on chosen

    let heard =
        List.isEmpty text.Top
        && List.isEmpty text.Bottom
        && List.isEmpty text.AtEnd
        && List.isEmpty text.WhenCovered

    if not heard then
        failwith $"{Card.name chosen} does more than sit there and cannot be scenery"

    chosen

let private mute protocol value =
    let chosen = quiet protocol value

    if not (List.isEmpty (Printed.on chosen).Shown) then
        failwith $"{Card.name chosen} speaks when it is turned over and cannot be flipped as scenery"

    chosen

let private allOf side =
    side.Deck
    @ side.Hand
    @ side.Discard
    @ (Lines.all
       |> List.collect (fun line -> Side.stack line side |> List.map (fun placed -> placed.Card)))


report "the game finds nothing wrong with itself" [] compiled.Faults

report "fifteen protocols, none of them twice" 15 (List.distinct Protocol.all |> List.length)

report "six cards to a protocol" 6 Card.PerProtocol

report "eighteen to a deck" 18 Deck.Size

report "ninety cards in all" 90 (Protocol.all |> List.collect Card.inProtocol |> List.distinct |> List.length)


report "seven numbers a card can carry, nought to six" [ 0..6 ] Card.values

report
    "and every protocol goes without exactly one of them"
    [ for _ in Protocol.all -> 1 ]
    (Protocol.all
     |> List.map (fun protocol ->
         let has = Card.inProtocol protocol |> List.map (fun card -> card.Value)

         Card.values
         |> List.filter (fun value -> not (List.contains value has))
         |> List.length))

report
    "twelve go without the six; the three that have one go without a 3, a 0 and a 4"
    ([ Gravity, 3; Love, 0; Metal, 4 ], 12)
    (let missing protocol =
        let has = Card.inProtocol protocol |> List.map (fun card -> card.Value)
        Card.values |> List.find (fun value -> not (List.contains value has))

     Protocol.all
     |> List.filter (fun p -> missing p <> 6)
     |> List.map (fun p -> p, missing p),
     Protocol.all |> List.filter (fun p -> missing p = 6) |> List.length)

report
    "a number a protocol does not have is not a card, and saying it is refused rather than guessed"
    [ false; false; false; true; true ]
    ([ "gravity-3"; "love-0"; "metal-4"; "gravity-6"; "fire-5" ]
     |> List.map (Card.byName >> Option.isSome))


report "the draft is six picks" 6 Draft.Picks

report "1-2-2-1: one, two, two, one, one, two" [ one; two; two; one; one; two ] Draft.order

report "three picks each" [ 3; 3 ] (Session.seats |> List.map Draft.picksBy)

report "the first pick belongs to the first seat" one (rules.Active(standing (dealt 1UL)))

report
    "the second and third belong to the other"
    [ two; two ]
    ([ 1; 2 ]
     |> List.map (fun n -> rules.Active(standing (played 1UL (draft |> List.take n)))))

report
    "a protocol already taken is refused"
    (Some(Refused(AlreadyTaken Fire)))
    (Turn.asked (Take Fire) (standing (played 1UL [ Take Fire ]))
     |> snd
     |> List.tryHead)

report
    "a refused pick leaves the draft exactly where it was"
    (standing (played 1UL [ Take Fire ]))
    (standing (played 1UL [ Take Fire; Take Fire ]))

report "six picks and the draft is over" TheProtocols (Session.doing (standing (played 1UL draft)))

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


let private drafted seed = played seed draft

report
    "an order of two is refused"
    (Some(Refused(NotThree 2)))
    (Turn.asked (Arrange [ Fire; Water ]) (standing (drafted 1UL))
     |> snd
     |> List.tryHead)

report
    "the same protocol twice is refused"
    (Some(Refused(SaidTwice Fire)))
    (Turn.asked (Arrange [ Fire; Fire; Water ]) (standing (drafted 1UL))
     |> snd
     |> List.tryHead)

report
    "a protocol they never drafted is refused"
    (Some(Refused(NotDrafted Light)))
    (Turn.asked (Arrange [ Fire; Water; Light ]) (standing (drafted 1UL))
     |> snd
     |> List.tryHead)

report "the second player lays theirs out after the first" two (rules.Active(standing (played 1UL (draft @ [ orders[0] ]))))


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

     Lines.all
     |> List.map (fun line -> Side.protocolOn line (Session.side one session)),
     Lines.all
     |> List.map (fun line -> Side.protocolOn line (Session.side two session)))


report "both are dealt once the second order is in" ThePlay (Session.doing (standing (opened 1UL)))

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

report "a different seed does not" false (handOf one (opened 9UL) = handOf one (opened 10UL))

report
    "the shuffle is a shuffle, not a sort"
    false
    (let side = Session.side one (standing (opened 9UL))
     side.Deck @ side.Hand = Deck.ofProtocols side.Order)


let private firstCard model = handOf one model |> List.head

let private faceUp model card =
    Field.facingLines one card (standing model).Field |> List.exactlyOne

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

     Turn.asked (Play(firstCard model, 4, FaceDown)) (standing model)
     |> snd
     |> List.tryHead)

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
     |> List.filter (fun card -> Field.facingLines one card field <> [ faceUp model card ]))

report
    "face up, where its protocol is, is taken - and is worth what is printed on it"
    true
    (let model = opened 1UL
     let card = firstCard model
     let line = faceUp model card
     let after = Update.update rules (Make(Play(card, line, FaceUp))) model |> standing

     Side.stack line (Session.side one after) = [ Placed.up card ]
     && Field.valueOn one line after.Field = card.Value)

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
    [ 2; 2; 2; 2; 2; 2; 2 ]
    (Card.values
     |> List.map (fun value -> Placed.value (Placed.down { Protocol = Fire; Value = value })))

report
    "face up is worth what is printed on it"
    Card.values
    (Card.values
     |> List.map (fun value -> Placed.value (Placed.up { Protocol = Fire; Value = value })))

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

     Field.valueOn one 1 (standing first).Field, Field.valueOn one 1 (standing both).Field)

report
    "a five spent face down is worth two, and the stack says so"
    (5, 2)
    (let model = opened 1UL
     let five = { Protocol = Water; Value = 5 }

     let holding =
         { standing model with
             Field =
                 (standing model).Field
                 |> Field.update one (fun side -> { side with Hand = [ five ] }) }

     let played face =
         match Turn.asked (Play(five, 1, face)) holding with
         | Some after, _ -> Field.valueOn one 1 after.Field
         | None, _ -> -1

     played FaceUp, played FaceDown)


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

report "and the words for it name the command" true (compiled.Says(Refused MustRefresh) |> mentions "'refresh'")

report
    "refreshing puts the hand down and takes five up"
    (5, 13, 0)
    (let session = standing (opened 1UL)

     match Turn.asked Refresh session with
     | Some after, _ ->
         let side = Session.side one after
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
             Field =
                 session.Field
                 |> Field.update one (fun side -> { side with Deck = []; Discard = [] }) }

     match Turn.asked Refresh stripped with
     | Some after, _ -> List.length (Session.side one after).Hand, Session.active after
     | None, _ -> -1, one)

report
    "refreshing loses no cards"
    (36, [ 18; 18 ])
    (match Turn.asked Refresh (standing (opened 1UL)) with
     | Some after, _ ->
         let counted =
             Session.seats
             |> List.map (fun seat -> Session.side seat after |> allOf |> List.length)

         List.sum counted, counted
     | None, _ -> -1, [])


let private poised seat line cards session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Stacks = side.Stacks |> Map.add line (cards |> List.map Placed.up) }) }
    |> Resolving.asRead

let private handedOverBy seat session =
    let session = { session with ToPlay = seat }
    let played = (Session.side seat session).Hand |> List.head
    Turn.asked (Play(played, Lines.Count, FaceDown)) session

let private handedOver session = handedOverBy two session

report "ten is the number, and one card cannot reach it" (10, true) (Stack.ToCompile, Stack.ToCompile > List.max Card.values)

report
    "a line is won at ten and ahead, and not at nine, and not at a tie"
    [ false; false; true; true ]
    (let session = standing (opened 1UL)

     [ [ card Water 5; quiet Water 4 ], []
       [ card Water 5; quiet Water 5 ], [ card Gravity 5; quiet Gravity 5 ]
       [ card Water 5; quiet Water 5 ], [ card Gravity 5; quiet Gravity 4 ]
       [ card Water 5; quiet Water 5; quiet Gravity 1 ], [] ]
     |> List.map (fun (mine, theirs) ->
         let field =
             session |> poised one 1 mine |> poised two 1 theirs |> (fun s -> s.Field)

         Field.won one 1 field))

report
    "a won line compiles as the turn comes round, without being asked"
    (true, [ Water ])
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]

     match handedOver session with
     | Some after, _ ->
         let side = Session.side one after
         Side.hasCompiled Water side, Set.toList side.Compiled
     | None, _ -> false, [])

report
    "and it says so, naming the protocol and the line"
    true
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]

     handedOver session
     |> snd
     |> List.map compiled.Says
     |> List.exists (fun told -> mentions "compiles Water" told && mentions "line 1" told))

report
    "compiling wipes that line, both players' cards alike, into their own discards"
    ((0, 3), (0, 2))
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]
        |> poised two 1 [ quiet Gravity 4; quiet Gravity 1 ]

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
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]
        |> poised one 2 [ quiet Darkness 4 ]

     match handedOver session with
     | Some after, _ ->
         let side = Session.side one after
         [ Field.valueOn one 2 after.Field; Field.valueOn one 1 after.Field ]
     | None, _ -> [])

report
    "two lines won at once are both compiled, in line order"
    [ Water, 1; Darkness, 2 ]
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]
        |> poised one 2 [ quiet Darkness 5; quiet Darkness 5; quiet Darkness 3 ]

     handedOver session
     |> snd
     |> List.choose (function
         | Happened(Compiled(_, protocol, line)) -> Some(protocol, line)
         | _ -> None))

let private having seat protocols session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Compiled = Set.ofList protocols }) }

report
    "compiling all three wins the game"
    (true, Some(Won one))
    (let session =
        standing (opened 1UL)
        |> having one [ Water; Darkness ]
        |> poised one 3 [ quiet Fire 5; quiet Fire 5; quiet Fire 5 ]

     match handedOver session with
     | Some after, _ -> Session.isOver after, Session.ending after
     | None, _ -> false, None)

report
    "and two of three does not"
    (false, None)
    (let session =
        standing (opened 1UL)
        |> having one [ Water ]
        |> poised one 3 [ quiet Fire 5; quiet Fire 5; quiet Fire 5 ]

     match handedOver session with
     | Some after, _ -> Session.isOver after, Session.ending after
     | None, _ -> true, Some(Abandoned one))


let private nearlyTen seed =
    let model =
        [ 1..4 ]
        |> List.fold
            (fun model n ->
                let mine = handOf one model |> List.head
                let model = Update.update rules (Make(Play(mine, 1, FaceDown))) model
                let theirs = handOf two model |> List.head
                Update.update rules (Make(Play(theirs, (if n % 2 = 0 then 2 else 3), FaceDown))) model)
            (opened seed)

    let last = handOf one model |> List.head
    Update.update rules (Make(Play(last, 1, FaceDown))) model

report
    "five cards face down is ten, and ten with nothing against it is a won line"
    (10, true, two)
    (let session = standing (nearlyTen 1UL)
     Field.valueOn one 1 session.Field, Field.won one 1 session.Field, rules.Active session)

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


let private secondCompile seed =
    standing (opened seed)
    |> having one [ Water ]
    |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]

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
     | Some after, _ -> List.length (Session.side two after).Deck, List.length (Session.side one after).Hand
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
    (let session = secondCompile 1UL |> poised two 1 [ quiet Gravity 4 ]

     match handedOver session with
     | Some after, _ -> Field.valueOn one 1 after.Field, Field.valueOn two 1 after.Field
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
         let heardBy seat =
             told |> List.map (compiled.SeenBy seat) |> String.concat " "

         mentions (Card.name card) (heardBy one), mentions (Card.name card) (heardBy two)
     | _ -> false, true)

report
    "an empty deck is shuffled from its discard before the card is taken"
    (true, true)
    (let session = secondCompile 1UL

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
             Field =
                 session.Field
                 |> Field.update two (fun side -> { side with Deck = []; Discard = [] }) }

     handedOver stripped
     |> snd
     |> List.exists (function
         | Happened(TookNothing _) -> true
         | _ -> false))


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

let private onlyHolding seat wanted session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Hand = [ wanted ]
                    Deck =
                        (side.Hand |> List.filter ((<>) wanted))
                        @ (side.Deck |> List.filter ((<>) wanted)) }) }

let private beneath seat line cards session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Stacks = side.Stacks |> Map.add line cards }) }
    |> Resolving.asRead

let private lyingDown seat line cards session =
    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Stacks = side.Stacks |> Map.add line (cards |> List.map Placed.down) }) }
    |> Resolving.asRead

let private fireZero = card Fire 0

let private playFireZero session =
    Turn.asked (Play(fireZero, 3, FaceUp)) session

let private happenings told =
    told
    |> List.choose (function
        | Happened e -> Some e
        | _ -> None)

let private running command source session =
    Resolving.settle
        { session with
            Pile =
                [ Run(
                      command,
                      { Owner = one
                        Saying = source
                        Line = 1 }
                  ) ] }
        []

report
    "a card played face down sets nothing off, whatever is printed on it"
    []
    (let loud = card Fire 5
     let session = standing (opened 1UL) |> holding one loud

     Turn.asked (Play(loud, elsewhere (opened 1UL) loud, FaceDown)) session
     |> snd
     |> List.filter (function
         | Happened(Played _) -> false
         | _ -> true))

report
    "one target and no question: the card flips it, then draws"
    [ true; true ]
    (let session =
        standing (opened 1UL)
        |> holding one fireZero
        |> lyingDown one 1 [ mute Speed 2 ]

     match playFireZero session with
     | Some after, told ->
         [ happenings told
           |> List.exists (function
               | Flipped(_, turned, 1) -> turned.Card = card Speed 2 && Placed.isFaceUp turned
               | _ -> false)
           happenings told
           |> List.exists (function
               | Drew(_, 2) -> true
               | _ -> false) ]
     | None, _ -> [ false; false ])

report
    "in that order, and the turn passes only after both"
    (true, two)
    (let session =
        standing (opened 1UL)
        |> onlyHolding one fireZero
        |> lyingDown one 1 [ mute Speed 2 ]

     match playFireZero session with
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
    (let session = standing (opened 1UL) |> holding one fireZero

     match playFireZero session with
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
    (let session = standing (opened 1UL) |> holding one fireZero

     playFireZero session
     |> snd
     |> List.map compiled.Says
     |> List.exists (fun said -> mentions "Fire-0" said && mentions "nothing to do" said))

report
    "more than one target stops the game and asks"
    (true, one, AChoice)
    (let session =
        standing (opened 1UL)
        |> holding one fireZero
        |> lyingDown one 1 [ mute Speed 2; mute Hate 3 ]

     match playFireZero session with
     | Some after, _ -> (Session.asking after).IsSome, Session.active after, Session.doing after
     | None, _ -> false, two, Nothing)

report
    "the turn does not pass while a card is waiting on an answer"
    (true, false)
    (let session =
        standing (opened 1UL)
        |> holding one fireZero
        |> lyingDown one 1 [ mute Speed 2; mute Hate 3 ]

     match playFireZero session with
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
        |> holding one fireZero
        |> lyingDown one 1 [ mute Speed 2; mute Hate 3 ]

     match playFireZero session with
     | Some waiting, _ ->
         match Turn.asked Refresh waiting with
         | None, [ Refused(AnswerFirst(ACard(_, targets))) as refusal ] ->
             List.length targets = 2
             && mentions "Speed-2" (compiled.Says refusal)
             && mentions "Hate-3" (compiled.Says refusal)
         | _ -> false
     | None, _ -> false)

report
    "an answer that was not on offer is refused"
    true
    (let session =
        standing (opened 1UL)
        |> holding one fireZero
        |> lyingDown one 1 [ mute Speed 2; mute Hate 3 ]

     match playFireZero session with
     | Some waiting, _ ->
         (match Turn.asked (Choose(TheCard(card Speed 0))) waiting with
          | None, [ Refused(NotOnOffer _) ] -> true
          | _ -> false)
     | None, _ -> false)

report
    "answering carries on where the pile left off: the draw happens, then the turn passes"
    (true, true, two)
    (let session =
        standing (opened 1UL)
        |> onlyHolding one fireZero
        |> lyingDown one 1 [ mute Speed 2; mute Hate 3 ]

     match playFireZero session with
     | Some waiting, _ ->
         match Turn.asked (Choose(TheCard(card Hate 3))) waiting with
         | Some after, told ->
             Side.stack 1 (Session.side one after)
             |> List.exists (fun placed -> placed.Card = card Hate 3 && Placed.isFaceUp placed),
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
        |> holding one fireZero
        |> lyingDown one 1 [ mute Speed 2; mute Hate 3 ]

     match playFireZero session with
     | Some waiting, _ ->
         match Turn.asked (Choose(TheCard(card Hate 3))) waiting with
         | Some after, _ ->
             Side.stack 1 (Session.side one after)
             |> List.exists (fun placed -> placed.Card = card Speed 2 && not (Placed.isFaceUp placed))
         | None, _ -> false
     | None, _ -> false)


let private plagueZero = card Plague 0

let private lineOne protocol session =
    { session with
        Field =
            session.Field
            |> Field.update one (fun side ->
                { side with
                    Drafted = protocol :: List.tail side.Drafted
                    Order = protocol :: List.tail side.Order }) }

let private stopping session =
    session |> lineOne Plague |> holding one plagueZero

report
    "a card can stop the game on the other player, mid-turn"
    (true, two, one)
    (let session = standing (opened 1UL) |> stopping

     match Turn.asked (Play(plagueZero, 1, FaceUp)) session with
     | Some after, _ -> (Session.asking after).IsSome, Session.active after, after.ToPlay
     | None, _ -> false, one, two)

report
    "they choose out of their own hand, and it is their hand that shrinks"
    (4, 5)
    (let session = standing (opened 1UL) |> stopping

     match Turn.asked (Play(plagueZero, 1, FaceUp)) session with
     | Some waiting, _ ->
         let theirs = (Session.side two waiting).Hand

         match Turn.asked (Choose(TheCard(List.head theirs))) waiting with
         | Some after, _ -> List.length (Session.side two after).Hand, List.length theirs
         | None, _ -> -1, -1
     | None, _ -> -1, -1)

report
    "the turn passes once they have answered, and it passes to them"
    two
    (let session = standing (opened 1UL) |> stopping

     match Turn.asked (Play(plagueZero, 1, FaceUp)) session with
     | Some waiting, _ ->
         let theirs = (Session.side two waiting).Hand

         match Turn.asked (Choose(TheCard(List.head theirs))) waiting with
         | Some after, _ -> Session.active after
         | None, _ -> one
     | None, _ -> one)

report
    "a choice survives being written down and read back"
    true
    (match Playable.read compiled (compiled.Write(Make(Choose(TheCard fireZero)))) with
     | Ok(Send(Make(Choose(TheCard read)))) -> read = fireZero
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
        |> holding one fireZero
        |> lyingDown one 1 [ mute Speed 2; mute Hate 3 ]

     match playFireZero session with
     | Some waiting, _ ->
         let model =
             played 1UL (draft @ orders)
             |> fun model ->
                 { model with
                     Timeline = Timeline.advance (Make Refresh) waiting model.Timeline }

         let screen = drawn plain one model
         mentions "needs you to pick a card" screen, mentions "Speed-2" screen && mentions "Hate-3" screen
     | None, _ -> false, false)


report "all ninety of them are written" (90, 0) (Printed.written, 90 - Printed.written)

report
    "every written card is a card that is really printed, and says exactly what it says"
    []
    (Protocol.all
     |> List.map (fun protocol -> Printed.on { Protocol = protocol; Value = 5 })
     |> List.distinct
     |> List.filter ((<>) (Printed.on (card Fire 5))))


report
    "a rule in the top box goes on applying after something is played over the card"
    (6, 10)
    (let stacked cards =
        standing (opened 1UL) |> beneath one 2 cards

     let uncovered = stacked [ Placed.up (card Darkness 2); Placed.down (mute Speed 2) ]

     let covered =
         stacked
             [ Placed.down (mute Hate 3)
               Placed.up (card Darkness 2)
               Placed.down (mute Speed 2) ]

     Field.valueOn one 2 uncovered.Field, Field.valueOn one 2 covered.Field)

report
    "a rule in the bottom box stops the moment anything covers the card"
    (true, false)
    (let shutWhen cards =
        let session = standing (opened 1UL) |> beneath two 2 cards
        Field.barred one 2 FaceUp session.Field |> Option.isSome

     shutWhen [ Placed.up (card Plague 0) ], shutWhen [ Placed.down (mute Speed 2); Placed.up (card Plague 0) ])

report
    "and only while it is face up: a card lying face down says nothing at all"
    (Placed.FaceDownValue * 2, false)
    (let session =
        standing (opened 1UL) |> lyingDown one 2 [ card Darkness 2; mute Speed 2 ]

     Field.valueOn one 2 session.Field,
     Field.barred one 2 FaceUp (standing (opened 1UL) |> lyingDown two 2 [ card Plague 0 ]).Field
     |> Option.isSome)

report
    "a bottom command fires at the end of its owner's turn, once per turn it survives"
    (true, true)
    (let session = standing (opened 1UL) |> poised two 3 [ card Light 1 ]

     let drewOn seat =
         let played = (Session.side seat { session with ToPlay = seat }).Hand |> List.head

         Turn.asked (Play(played, 1, FaceDown)) { session with ToPlay = seat }
         |> snd
         |> List.exists (function
             | Happened(Drew(who, _)) -> who = two
             | _ -> false)

     drewOn two, not (drewOn one))


report
    "a card can play a card out of your hand, and it does not cost you the turn"
    (true, 1, Deck.HandSize - 1)
    (let session = standing (opened 1UL) |> holding one (card Darkness 3)

     match Turn.asked (Play(card Darkness 3, 2, FaceUp)) session with
     | Some waiting, _ ->
         match Turn.asked (Choose(TheLine 3)) waiting with
         | Some next, _ ->
             let played = (Session.side one next).Hand |> List.head

             match Turn.asked (Choose(TheCard played)) next with
             | Some after, _ ->
                 Side.stack 3 (Session.side one after)
                 |> List.exists (fun placed -> placed.Card = played && not (Placed.isFaceUp placed)),
                 List.length (Side.stack 3 (Session.side one after)),
                 List.length (Session.side one after).Hand + 1
             | None, _ -> false, -1, -1
         | None, _ -> false, -1, -1
     | None, _ -> false, -1, -1)

report
    "and a play with no face named is a choice between the two, not a third kind of play"
    (true, true)
    (let session = standing (opened 1UL) |> holding one (card Speed 0)

     let after, _ =
         running (Either(PlayFromHand(FaceUp, AnyLine), PlayFromHand(FaceDown, AnyLine))) (card Speed 0) session

     match Session.asking after with
     | Some { Wanting = OneOf(PlayFromHand(FaceUp, _), PlayFromHand(FaceDown, _)) } -> true, true
     | _ -> false, false)

report
    "a card can get itself out of a line about to be compiled, and it is the only thing that can"
    (true, true)
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]
        |> poised two 1 [ card Speed 2 ]

     match handedOver session with
     | Some waiting, _ ->
         match Session.asking waiting with
         | Some { Wanting = ALine _ } ->
             match Turn.asked (Choose(TheLine 2)) waiting with
             | Some after, _ ->
                 Side.hasCompiled Water (Session.side one after),
                 Side.stack 2 (Session.side two after)
                 |> List.exists (fun placed -> placed.Card = card Speed 2)
             | None, _ -> false, false
         | _ -> false, false
     | None, _ -> false, false)

report
    "...and lying face down it says nothing, so it goes with the rest of the line"
    (true, false)
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]
        |> lyingDown two 1 [ card Speed 2 ]

     match handedOver session with
     | Some after, _ ->
         Side.hasCompiled Water (Session.side one after),
         Lines.all
         |> List.exists (fun line ->
             Side.stack line (Session.side two after)
             |> List.exists (fun placed -> placed.Card = card Speed 2))
     | None, _ -> false, true)

report
    "and all three print what they do"
    [ "Play a card from your hand face down in another line."
      "Either play a card from your hand face up or play a card from your hand face down."
      "When this card would be deleted by compiling, first: Shift this card to another line." ]
    ([ card Darkness 3; card Speed 0; card Speed 2 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "a card can interrupt being turned over, and the turning then finds it gone"
    (true, false, false)
    (let session = standing (opened 1UL) |> poised two 2 [ card Metal 6 ]

     let after, told = running (Flip Select.any) (card Apathy 3) session

     happenings told
     |> List.exists (function
         | Deleted(_, gone, 2) -> gone.Card = card Metal 6
         | _ -> false),
     happenings told
     |> List.exists (function
         | Flipped _ -> true
         | _ -> false),
     Side.stack 2 (Session.side two after) |> List.isEmpty |> not)

report
    "and an ordinary card is simply turned over, which is the same machinery saying nothing"
    (true, true)
    (let session = standing (opened 1UL) |> lyingDown two 2 [ mute Speed 2 ]

     let after, told = running (Flip Select.any) (card Apathy 3) session

     happenings told
     |> List.exists (function
         | Flipped(_, turned, 2) -> turned.Card = mute Speed 2 && Placed.isFaceUp turned
         | _ -> false),
     Side.stack 2 (Session.side two after) |> List.forall Placed.isFaceUp)

report
    "and the card prints both of its interrupts"
    [ "When this card would be flipped, first: Delete this card."
      "When this card would be covered, first: Delete this card." ]
    (Words.printed (card Metal 6))


report
    "a shift can be told about both ends at once: out of this line, or into it, and never both"
    ([ 2; 3 ], [ 1 ])
    (let offered from =
        let session = standing (opened 1UL) |> lyingDown one from [ mute Speed 2 ]

        let after, _ = running (Shift(Select.any, ToOrFromHere)) (card Gravity 1) session

        match Session.asking after with
        | Some { Wanting = ALine(_, lines) } -> lines
        | _ ->
            Lines.all
            |> List.filter (fun line -> Side.stack line (Session.side one after) |> List.isEmpty |> not)

     offered 1, offered 3)

report
    "and the second question is the card's, not the rules' - it says where *this* card goes"
    (Some(card Gravity 1))
    (let session = standing (opened 1UL) |> lyingDown one 1 [ mute Speed 2 ]

     let after, _ = running (Shift(Select.any, ToOrFromHere)) (card Gravity 1) session

     match Session.asking after with
     | Some { Because = ACardSaying source } -> Some source.Saying
     | _ -> None)

report
    "a line can be picked by how many cards it holds, and a shallow board offers none"
    (true, false)
    (let asked deep =
        let session =
            standing (opened 1UL) |> lyingDown one 2 (List.replicate deep (mute Speed 2))

        let after, _ =
            running (InAChosenLineOf(8, Every(Delete(Select.any |> Select.here)))) (card Metal 3) session

        Side.stack 2 (Session.side one after) |> List.isEmpty

     asked 8, asked 7)

report
    "and a command can be run in every line its owner is standing in, and nowhere else"
    ([ 1; 3 ], 2)
    (let session =
        standing (opened 1UL)
        |> lyingDown one 1 [ mute Speed 2 ]
        |> lyingDown one 3 [ mute Speed 2 ]

     let after, told =
         running (InEachLineHolding(FromDeck(FaceDown, ThisLine))) (card Life 0) session

     Lines.all
     |> List.filter (fun line -> Side.stack line (Session.side one after) |> List.length > 1),
     happenings told
     |> List.filter (function
         | PlayedFromDeck _ -> true
         | _ -> false)
     |> List.length)

report
    "and all three print the question they ask"
    [ "Draw 2 cards. Shift any card either to or from this line."
      "Draw a card. Delete every card in this line, in another line of your choosing with 8 or more cards."
      "Play the top card of your deck face down in this line, in each line where you have a card." ]
    ([ card Gravity 1; card Metal 3; card Life 0 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "a card can ask whether it is covering something, and a four on bare table is only a four"
    (true, false)
    (let drewOn under =
        let session =
            standing (opened 1UL) |> beneath one 1 (Placed.up (card Life 4) :: under)

        let _, told = running (IfCovering [ Draw(Just 1) ]) (card Life 4) session

        happenings told
        |> List.exists (function
            | Drew _ -> true
            | _ -> false)

     drewOn [ Placed.down (mute Speed 2) ], drewOn [])

report
    "and a card can call the check cache phase off, so a hand over its limit stays over it"
    (true, false)
    (let trimmed keeping =
        let session =
            standing (opened 1UL)
            |> beneath one 1 keeping
            |> fun session ->
                { session with
                    Field =
                        session.Field
                        |> Field.update one (fun side ->
                            { side with
                                Hand = side.Hand @ (side.Deck |> List.truncate 2)
                                Deck = side.Deck |> List.skip 2 }) }

        let played = (Session.side one session).Hand |> List.head

        match Turn.asked (Play(played, 2, FaceDown)) session with
        | Some after, _ -> (Session.asking after).IsSome
        | None, _ -> false

     trimmed [ Placed.down (mute Speed 2); Placed.up (card Spirit 0) ], trimmed [ Placed.up (card Spirit 0) ])

report
    "and both of them print what they ask, in the box that says how long it holds"
    [ "If this card is covering a card, draw a card.", "middle"
      "You skip your check cache phase.", "bottom" ]
    ([ card Life 4; card Spirit 0 ]
     |> List.map (fun each ->
         let said = Words.printed each |> List.last
         said, printedIn each said))


report
    "a card can listen for what a command just did, and it goes off at once"
    (true, true)
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ card Hate 3 ]
        |> lyingDown two 2 [ mute Speed 2 ]

     let _, told = running (Delete(Select.any |> Select.theirs)) (card Death 0) session

     happenings told
     |> List.exists (function
         | Deleted _ -> true
         | _ -> false),
     happenings told
     |> List.exists (function
         | Drew(who, 1) -> who = one
         | _ -> false))

report
    "and it goes on listening while it is covered, which the start and end boxes do not"
    (true, true)
    (let heard under =
        let session =
            standing (opened 1UL) |> beneath one 1 under |> lyingDown two 2 [ mute Speed 2 ]

        let _, told = running (Delete(Select.any |> Select.theirs)) (card Death 0) session

        happenings told
        |> List.exists (function
            | Drew _ -> true
            | _ -> false)

     heard [ Placed.up (card Hate 3) ], heard [ Placed.down (mute Hate 3); Placed.up (card Hate 3) ])

report
    "...but not face down, because a card lying face down says nothing at all"
    false
    (let session =
        standing (opened 1UL)
        |> lyingDown one 1 [ card Hate 3 ]
        |> lyingDown two 2 [ mute Speed 2 ]

     let _, told = running (Delete(Select.any |> Select.theirs)) (card Death 0) session

     happenings told
     |> List.exists (function
         | Drew _ -> true
         | _ -> false))

report
    "a compile is not a deletion anybody did, so nothing hears it"
    (true, false)
    (let session =
        standing (opened 1UL)
        |> poised one 2 [ card Hate 3 ]
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]

     match handedOver session with
     | Some after, told ->
         Side.hasCompiled Water (Session.side one after),
         happenings told
         |> List.exists (function
             | Drew _ -> true
             | _ -> false)
     | None, _ -> false, true)

report
    "and one listens for the *other* seat: their discard, your card"
    (true, false)
    (let listened seat =
        let session =
            standing (opened 1UL)
            |> poised seat 1 [ card Plague 1 ]
            |> onlyHolding two (mute Speed 2)

        let _, told = running (Opposing Discard) (card Plague 0) session

        happenings told
        |> List.exists (function
            | Drew(who, _) -> who = seat
            | _ -> false)

     listened one, listened two)

report
    "and the check cache phase fires one every turn, whether or not there was anything to put down"
    (true, true)
    (let session = standing (opened 1UL) |> poised two 3 [ card Speed 1 ]

     let played = (Session.side two { session with ToPlay = two }).Hand |> List.head

     let told =
         Turn.asked (Play(played, 1, FaceDown)) { session with ToPlay = two } |> snd

     happenings told
     |> List.exists (function
         | Drew(who, 1) -> who = two
         | _ -> false),
     happenings told
     |> List.exists (function
         | Discarded _ -> true
         | _ -> false)
     |> not)

report
    "and all four of them print what they listen for"
    [ "After you delete cards: Draw a card."
      "After your opponent discards cards: Draw a card."
      "After you clear cache: Draw a card."
      "After you draw cards: You may shift this card to another line." ]
    ([ card Hate 3; card Plague 1; card Speed 1; card Spirit 3 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "a start command fires at the top of its owner's turn, and stops it where it stands"
    (true, one, AChoice)
    (let session = standing (opened 1UL) |> poised one 1 [ card Death 1 ]

     match handedOver session with
     | Some after, _ -> (Session.asking after).IsSome, Session.active after, Session.doing after
     | None, _ -> false, two, Nothing)

report
    "and it fires before the compile check, so what it changes is what compiles"
    (false, true)
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ card Psychic 1; quiet Water 5; quiet Darkness 3 ]

     match handedOver session with
     | Some after, _ -> Field.won one 1 session.Field, Side.hasCompiled Water (Session.side one after)
     | None, _ -> true, false)

report
    "a card can be taken back off the table and into the hand it is sitting in front of"
    (0, Deck.HandSize, 3)
    (let session =
        standing (opened 1UL)
        |> holding one (card Water 4)
        |> poised one 2 [ quiet Metal 3 ]

     match Turn.asked (Play(card Water 4, 1, FaceUp)) session with
     | Some waiting, _ ->
         match Turn.asked (Choose(TheCard(quiet Metal 3))) waiting with
         | Some after, _ ->
             Field.valueOn one 2 after.Field,
             List.length (Session.side one after).Hand,
             List.length (Session.side one after).Discard + 3
         | None, _ -> -1, -1, -1
     | None, _ -> -1, -1, -1)

report
    "a shift asks twice - which card, and then where"
    (true, true)
    (let session =
        standing (opened 1UL)
        |> holding one (card Darkness 4)
        |> lyingDown one 1 [ mute Speed 2 ]
        |> lyingDown one 3 [ mute Hate 3 ]

     match Turn.asked (Play(card Darkness 4, 2, FaceUp)) session with
     | Some waiting, _ ->
         match Session.asking waiting with
         | Some { Wanting = ACard(Shift _, targets) } ->
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
        |> holding one (card Darkness 4)
        |> lyingDown one 1 [ mute Speed 2 ]

     match Turn.asked (Play(card Darkness 4, 2, FaceUp)) session with
     | Some next, _ ->
         match Turn.asked (Choose(TheLine 3)) next with
         | Some after, _ ->
             let side = Session.side one after

             Field.valueOn one 1 after.Field, Side.stack 3 side |> List.exists (fun placed -> placed.Card = mute Speed 2)
         | None, _ -> -1, false
     | None, _ -> -1, false)


report
    "a shift that says where it goes does not ask - the card already answered"
    (0, true, false)
    (let session = standing (opened 1UL) |> lyingDown one 1 [ mute Speed 2 ]

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           Shift(Select.any |> Select.faceDown, ThisLine),
                           { Owner = one
                             Saying = card Gravity 4
                             Line = 3 }
                       ) ] }
             []

     Field.valueOn one 1 after.Field,
     Side.stack 3 (Session.side one after)
     |> List.exists (fun placed -> placed.Card = mute Speed 2),
     (Session.asking after).IsSome)

report
    "and one that says which lines it may not go to still asks, out of what is left"
    [ 2 ]
    (let session = standing (opened 1UL) |> lyingDown one 1 [ mute Speed 2 ]

     let after, told =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           Shift(Select.any |> Select.faceDown, OtherLines),
                           { Owner = one
                             Saying = card Light 3
                             Line = 3 }
                       ) ] }
             []

     ignore told

     Lines.all
     |> List.filter (fun line -> Side.stack line (Session.side one after) |> List.isEmpty |> not))

report
    "and both of them print the difference"
    [ "Shift any face-down card to this line."
      "Shift every face-down card in this line to another line." ]
    ([ card Gravity 4; card Light 3 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "what a card says is written from what it does"
    [ "Flip any other card. Draw 2 cards." ]
    (Words.printed (card Fire 0) |> List.take 1)

report
    "including the boxes below it, and which box a line is in is said"
    [ "When this card would be covered, first: Draw a card. Flip any other card." ]
    (Words.printed (card Fire 0) |> List.tail)

report
    "a top-box rule is printed without that warning, because it does not need one"
    [ "Every face-down card in this stack is worth 4."
      "You may flip any covered card in this line." ]
    (Words.printed (card Darkness 2))

report
    "a card that says one thing prints one line, out of the box it says it in"
    [ "When this card would be deleted by compiling, first: Shift this card to another line." ]
    (Words.printed (card Speed 2))

report
    "and asking about a card is answered with what it says"
    (true, true)
    (let screen = plain.Answer one "what fire-0" (opened 1UL)
     reads "Flip any other card" screen, reads "Draw 2 cards" screen)

report
    "and asking about the quietest of them still says something"
    true
    (plain.Answer one "what speed-2" (opened 1UL) |> reads "deleted by compiling")

report
    "every one of the ninety says something, which is why the board marks none of them"
    (90, true)
    (let model = opened 1UL

     let shown session =
         drawn
             plain
             one
             { model with
                 Timeline = Timeline.advance (Make Refresh) session model.Timeline }

     let holdingOnly card =
         { standing model with
             Field =
                 (standing model).Field
                 |> Field.update one (fun side -> { side with Hand = [ card ] }) }

     Protocol.all
     |> List.collect Card.inProtocol
     |> List.filter Printed.says
     |> List.length,
     shown (holdingOnly (card Fire 0)) |> mentions " *" |> not)

report
    "a line answer survives being written down and read back"
    true
    (match Playable.read compiled (compiled.Write(Make(Choose(TheLine 2)))) with
     | Ok(Send(Make(Choose(TheLine read)))) -> read = 2
     | _ -> false)


report
    "a card can point at what things are worth on the table, not at what is printed on them"
    (true, false, false)
    (let session =
        standing (opened 1UL)
        |> holding one (card Death 4)
        |> poised two 1 [ quiet Gravity 1 ]
        |> poised two 2 [ quiet Metal 3 ]
        |> beneath two 3 [ Placed.down (quiet Light 3) ]

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           Delete(Select.any |> Select.worth [ 0; 1 ]),
                           { Owner = one
                             Saying = card Death 4
                             Line = 1 }
                       ) ] }
             []

     Side.stack 1 (Session.side two after) |> List.isEmpty,
     Side.stack 2 (Session.side two after) |> List.isEmpty,
     Side.stack 3 (Session.side two after) |> List.isEmpty)

report
    "and at the highest of them, with everything tied for it still in the running"
    (2, [ 5; 5 ])
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ quiet Water 5 ]
        |> poised one 2 [ quiet Water 5 ]
        |> poised one 3 [ quiet Gravity 1 ]

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           Delete(Select.any |> Select.yours |> Select.highest),
                           { Owner = one
                             Saying = card Hate 2
                             Line = 1 }
                       ) ] }
             []

     match Session.asking after with
     | Some { Wanting = ACard(_, targets) } ->
         List.length targets, targets |> List.map (fun t -> (Target.card t).Value) |> List.sort
     | _ -> -1, [])

report
    "an 'all' takes every one of them and asks nobody"
    (0, 1)
    (let session =
        standing (opened 1UL)
        |> holding one (card Apathy 1)
        |> poised one 2 [ quiet Water 4 ]
        |> poised one 3 [ quiet Fire 5 ]

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           Every(Flip(Select.any |> Select.faceUp |> Select.yours)),
                           { Owner = one
                             Saying = card Apathy 1
                             Line = 2 }
                       ) ] }
             []

     (Side.stack 2 (Session.side one after) @ Side.stack 3 (Session.side one after)
      |> List.filter Placed.isFaceUp
      |> List.length),
     (if (Session.asking after).IsNone then 1 else 0))

report
    "and 'other' spares the card that said it"
    true
    (let session = standing (opened 1UL) |> poised one 2 [ card Apathy 1; quiet Water 4 ]

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           Every(Flip(Select.any |> Select.here |> Select.faceUp |> Select.other)),
                           { Owner = one
                             Saying = card Apathy 1
                             Line = 2 }
                       ) ] }
             []

     Side.stack 2 (Session.side one after)
     |> List.exists (fun placed -> placed.Card = card Apathy 1 && Placed.isFaceUp placed))

report
    "and all three print what they mean"
    [ "Flip every other face-up card in this line."
      "Delete any card worth 0 or 1."
      "Delete your highest-value card. Delete their highest-value card." ]
    ([ card Apathy 1; card Death 4; card Hate 2 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "one is forced, and then it keeps offering until they say no - and the count is what they did"
    (2, 3)
    (let session = standing (opened 1UL)

     let rec answered model said count =
         if count > 12 then
             model, said
         else
             match Session.asking model with
             | Some({ Wanting = ACard(_, targets) } as question) ->
                 match Resolving.choosing question (TheCard(Target.card (List.head targets))) model with
                 | Some next, more -> answered next (said @ more) (count + 1)
                 | None, _ -> model, said
             | Some({ Wanting = Whether _ } as question) ->
                 let answer = if count < 3 then Yes else No

                 match Resolving.choosing question answer model with
                 | Some next, more -> answered next (said @ more) (count + 1)
                 | None, _ -> model, said
             | _ -> model, said

     let started, told =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           IfYouDo(OneOrMore Discard, [ Draw(HowManyPlus 1) ]),
                           { Owner = one
                             Saying = card Fire 4
                             Line = 1 }
                       ) ] }
             []

     let after, said = answered started told 0

     said
     |> List.filter (function
         | Happened(Discarded _) -> true
         | _ -> false)
     |> List.length,
     said
     |> List.tryPick (function
         | Happened(Drew(_, n)) -> Some n
         | _ -> None)
     |> Option.defaultValue -1)

report
    "and an empty hand does none of it - nought is not one or more"
    (0, 0)
    (let session = standing (opened 1UL)

     let starved =
         { session with
             Field = session.Field |> Field.update one (fun side -> { side with Hand = [] }) }

     let _, told =
         Resolving.settle
             { starved with
                 Pile =
                     [ Run(
                           IfYouDo(OneOrMore Discard, [ Draw(HowManyPlus 1) ]),
                           { Owner = one
                             Saying = card Fire 4
                             Line = 1 }
                       ) ] }
             []

     told
     |> List.filter (function
         | Happened(Discarded _) -> true
         | _ -> false)
     |> List.length,
     told
     |> List.filter (function
         | Happened(Drew _) -> true
         | _ -> false)
     |> List.length)

report
    "and the card prints what it does"
    "Discard a card, one or more times. If you do, draw that many cards plus 1."
    (Words.printed (card Fire 4) |> List.head)


report
    "a count read off the board, rounded down - and nought times does nothing"
    [ 0; 0; 1; 2 ]
    ([ 0; 1; 2; 5 ]
     |> List.map (fun many ->
         let session =
             standing (opened 1UL)
             |> beneath one 2 (List.replicate many (Placed.down (quiet Water 4)))

         let deck = List.length (Session.side one session).Deck

         let after, _ =
             Resolving.settle
                 { session with
                     Pile =
                         [ Run(
                               Times(PerCards(2, Select.any |> Select.here), UnderThis FaceDown),
                               { Owner = one
                                 Saying = card Gravity 0
                                 Line = 2 }
                           ) ] }
                 []

         deck - List.length (Session.side one after).Deck))

report
    "and a card arriving underneath goes to the bottom, covering nothing"
    (3, true)
    (let session =
        standing (opened 1UL)
        |> beneath one 2 [ Placed.up (quiet Water 4); Placed.up (quiet Water 5) ]

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           UnderThis FaceDown,
                           { Owner = one
                             Saying = card Gravity 0
                             Line = 2 }
                       ) ] }
             []

     let stack = Side.stack 2 (Session.side one after)

     List.length stack, (List.last stack).Face = FaceDown && (List.head stack).Card = quiet Water 4)

report
    "and it prints both halves"
    "Play the top card of your deck face down under this card, once for every 2 cards in this line."
    (Words.printed (card Gravity 0) |> List.head)


report
    "a reveal names the card, changes nothing, and is said to both players alike"
    (true, true, true)
    (let session = standing (opened 1UL)
     let held = (Session.side two session).Hand

     let after, told =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           RevealTheirHand,
                           { Owner = one
                             Saying = card Light 4
                             Line = 1 }
                       ) ] }
             []

     let said = told |> List.map compiled.Says |> String.concat " "

     held |> List.forall (fun each -> mentions (Card.name each) said),
     (Session.side two after).Hand = held,
     told
     |> List.forall (fun notice -> compiled.SeenBy one notice = compiled.SeenBy two notice))

report
    "a swap offers only the orders one swap can reach - three of the six, never the current one"
    (3, false)
    (let session = standing (opened 1UL)

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           Swap,
                           { Owner = one
                             Saying = card Spirit 4
                             Line = 1 }
                       ) ] }
             []

     match Session.asking after with
     | Some { Wanting = AnOrder(_, offered) } -> List.length offered, List.contains [ Water; Darkness; Fire ] offered
     | _ -> -1, true)

report
    "and all three print what they mean"
    [ "Your opponent reveals their hand."
      "Reveal a card from your hand. Flip any card."
      "Swap the positions of two of your protocols." ]
    ([ card Light 4; card Love 4; card Spirit 4 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "'that card' is the card the command before it landed on, read afterwards"
    (3, 3)
    (let drewAfterFlipping under =
        let session =
            standing (opened 1UL) |> holding one (card Light 0) |> beneath two 2 [ under ]

        let after, told =
            Resolving.settle
                { session with
                    Pile =
                        [ Run(
                              Flip Select.any,
                              { Owner = one
                                Saying = card Light 0
                                Line = 1 }
                          )
                          Run(
                              Draw WorthOfChosen,
                              { Owner = one
                                Saying = card Light 0
                                Line = 1 }
                          ) ] }
                []

        told
        |> List.tryPick (function
            | Happened(Drew(_, n)) -> Some n
            | _ -> None)
        |> Option.defaultValue -1

     drewAfterFlipping (Placed.up (mute Hate 3)), drewAfterFlipping (Placed.down (mute Hate 3)))

report
    "and it is the card that was chosen, not the one before it"
    (2, 3)
    (let drewAfterFlipping under =
        let session =
            standing (opened 1UL)
            |> holding one (card Light 0)
            |> beneath two 2 [ Placed.down under ]

        let _, told =
            Resolving.settle
                { session with
                    Pile =
                        [ Run(
                              Flip Select.any,
                              { Owner = one
                                Saying = card Light 0
                                Line = 1 }
                          )
                          Run(
                              Draw WorthOfChosen,
                              { Owner = one
                                Saying = card Light 0
                                Line = 1 }
                          ) ] }
                []

        told
        |> List.tryPick (function
            | Happened(Drew(_, n)) -> Some n
            | _ -> None)
        |> Option.defaultValue -1

     drewAfterFlipping (mute Speed 2), drewAfterFlipping (mute Hate 3))

report
    "and nothing chosen draws nothing"
    0
    (let session = standing (opened 1UL)

     let _, told =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           Draw WorthOfChosen,
                           { Owner = one
                             Saying = card Light 0
                             Line = 1 }
                       ) ] }
             []

     told
     |> List.tryPick (function
         | Happened(Drew(_, n)) -> Some n
         | _ -> None)
     |> Option.defaultValue -1)


let private gravityTwo line =
    [ Run(
          Flip Select.any,
          { Owner = one
            Saying = card Gravity 2
            Line = line }
      )
      Run(
          Shift(Select.any |> Select.thatCard, ThisLine),
          { Owner = one
            Saying = card Gravity 2
            Line = line }
      ) ]

report
    "a command can be pointed at that card, and follows it rather than the board"
    (true, 0)
    (let session = standing (opened 1UL) |> lyingDown one 1 [ mute Speed 2 ]

     let after, _ = Resolving.settle { session with Pile = gravityTwo 3 } []

     Side.stack 3 (Session.side one after)
     |> List.exists (fun placed -> placed.Card = mute Speed 2 && Placed.isFaceUp placed),
     List.length (Side.stack 1 (Session.side one after)))

report
    "and with nothing chosen it points at nothing at all, however full the table is"
    true
    (let session = standing (opened 1UL) |> lyingDown one 1 [ mute Speed 2 ]

     let _, told =
         Resolving.settle
             { session with
                 Chose = None
                 Pile =
                     [ Run(
                           Shift(Select.any |> Select.thatCard, ThisLine),
                           { Owner = one
                             Saying = card Gravity 2
                             Line = 3 }
                       ) ] }
             []

     happenings told
     |> List.exists (function
         | Fizzled _ -> true
         | _ -> false))

report
    "and both cards say so in the same two words"
    [ "Flip any card. Shift that card to this line."
      "Flip their card. You may shift that card to another line." ]
    ([ card Gravity 2; card Darkness 1 ]
     |> List.map (fun each -> Words.printed each |> List.head))

report
    "and the card prints what it does"
    "Flip any card. Draw cards equal to that card's value."
    (Words.printed (card Light 0) |> List.head)


report
    "a silenced line takes every card's voice away, both sides of it"
    (true, false)
    (let loud = standing (opened 1UL) |> holding one (card Fire 0)
     let hushed = loud |> poised two 3 [ card Apathy 2 ]

     let drewIn session =
         match Turn.asked (Play(card Fire 0, 3, FaceUp)) session with
         | Some _, told ->
             told
             |> List.exists (function
                 | Happened(Drew _) -> true
                 | _ -> false)
         | None, _ -> false

     drewIn loud, drewIn hushed)

report
    "but the card still lands, and still counts"
    (2, 2)
    (let session =
        standing (opened 1UL)
        |> holding one (card Fire 0)
        |> poised one 3 [ quiet Water 2 ]
        |> poised two 3 [ card Apathy 2 ]

     match Turn.asked (Play(card Fire 0, 3, FaceUp)) session with
     | Some after, _ -> List.length (Side.stack 3 (Session.side one after)), Field.valueOn one 3 after.Field
     | None, _ -> -1, -1)

report
    "a stopped compile is remembered, spent on the turn it was for, and gone after"
    (Some two, true, None)
    (let session =
        standing (opened 1UL) |> poised two 1 [ quiet Gravity 5; quiet Gravity 5 ]

     let stopped = { session with NoCompile = Some two }

     match handedOverBy one stopped with
     | Some after, _ -> stopped.NoCompile, Set.isEmpty (Session.side two after).Compiled, after.NoCompile
     | None, _ -> None, false, Some one)

report
    "and without it that same line compiles"
    false
    (let session =
        standing (opened 1UL) |> poised two 1 [ quiet Gravity 5; quiet Gravity 5 ]

     match handedOverBy one session with
     | Some after, _ -> Set.isEmpty (Session.side two after).Compiled
     | None, _ -> true)

report
    "and both print what they mean"
    [ "The middle commands of cards in this line do nothing."
      "Draw 2 cards. Your opponent cannot compile next turn." ]
    ([ card Apathy 2; card Metal 1 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "'in each other line' runs the command once per line, and not in the one it came from"
    (1, 0, 0)
    (let session =
        standing (opened 1UL)
        |> poised two 1 [ quiet Metal 3 ]
        |> poised two 2 [ quiet Metal 3 ]
        |> poised two 3 [ quiet Light 3 ]

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           InEachOtherLine(Delete(Select.any |> Select.here)),
                           { Owner = one
                             Saying = card Death 0
                             Line = 1 }
                       ) ] }
             []

     List.length (Side.stack 1 (Session.side two after)),
     List.length (Side.stack 2 (Session.side two after)),
     List.length (Side.stack 3 (Session.side two after)))

report
    "'in 1 line' asks which, and then runs there and nowhere else"
    (2, 0)
    (let session =
        standing (opened 1UL)
        |> poised two 1 [ quiet Gravity 1; quiet Light 2 ]
        |> poised two 2 [ quiet Gravity 1; quiet Light 2 ]

     let asked, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           InAChosenLine(Every(Delete(Select.any |> Select.here |> Select.worth [ 1; 2 ]))),
                           { Owner = one
                             Saying = card Death 2
                             Line = 3 }
                       ) ] }
             []

     match Session.asking asked with
     | Some question ->
         match Resolving.choosing question (TheLine 2) asked with
         | Some after, _ ->
             List.length (Side.stack 1 (Session.side two after)), List.length (Side.stack 2 (Session.side two after))
         | None, _ -> -1, -1
     | _ -> -1, -1)

report
    "and both print where as well as what"
    [ "Delete any card in this line, in each other line."
      "Delete every card worth 1 or 2 in this line, in a line of your choosing." ]
    ([ card Death 0; card Death 2 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "a card of theirs can shut a line against you, whichever way up you were playing"
    (Some NoPlayHere, Some NoPlayHere, None)
    (let session = standing (opened 1UL) |> poised two 2 [ card Plague 0 ]

     Field.barred one 2 FaceUp session.Field, Field.barred one 2 FaceDown session.Field, Field.barred one 1 FaceUp session.Field)

report
    "or shut only the face-down half of it"
    (None, Some NoFaceDownHere)
    (let session = standing (opened 1UL) |> poised two 2 [ card Metal 2 ]
     Field.barred one 2 FaceUp session.Field, Field.barred one 2 FaceDown session.Field)

report
    "or leave them nothing but face down, wherever the card is standing"
    [ Some OnlyFaceDown; Some OnlyFaceDown; Some OnlyFaceDown ]
    (let session = standing (opened 1UL) |> poised two 3 [ card Psychic 1 ]
     Lines.all |> List.map (fun line -> Field.barred one line FaceUp session.Field))

report
    "and a rule in the bottom box stops when something covers it"
    (Some NoPlayHere, None)
    (let shut = standing (opened 1UL) |> poised two 2 [ card Plague 0 ]

     let covered =
         standing (opened 1UL) |> poised two 2 [ quiet Plague 3; card Plague 0 ]

     Field.barred one 2 FaceUp shut.Field, Field.barred one 2 FaceUp covered.Field)

report
    "the refusal says which line and why, and the move does not carry"
    true
    (let session = standing (opened 1UL) |> poised two 2 [ card Plague 0 ]

     let card' = handOf one (opened 1UL) |> List.head

     match Turn.asked (Play(card', 2, FaceDown)) session with
     | None, [ Refused(Forbidden(NoPlayHere, 2)) as refusal ] ->
         mentions "line 2" (compiled.Says refusal)
         && mentions "cannot play" (compiled.Says refusal)
     | _ -> false)

report
    "and one card opens the whole board: face up anywhere, protocol or no protocol"
    (false, true)
    (let plainly = standing (opened 1UL)
     let opened' = plainly |> poised one 2 [ card Spirit 1 ]

     Field.allows one (quiet Water 4) 3 plainly.Field, Field.allows one (quiet Water 4) 3 opened'.Field)

report
    "and all four print what they mean, and where"
    [ "Your opponent cannot play cards face down in this line.", "top"
      "Your opponent cannot play cards in this line.", "bottom"
      "Your opponent can only play cards face down.", "top"
      "You can play cards in any line.", "top" ]
    ([ card Metal 2; card Plague 0; card Psychic 1; card Spirit 1 ]
     |> List.map (fun each ->
         let said = Words.printed each |> List.find (fun said -> said.Contains "play cards")

         said, printedIn each said))


report
    "a card off the top of a deck lands on the table, and the deck is one lighter"
    (12, 1, true)
    (let session = standing (opened 1UL) |> holding one (card Water 1)

     let after, told =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           FromDeck(FaceDown, ThisLine),
                           { Owner = one
                             Saying = card Water 1
                             Line = 2 }
                       ) ] }
             []

     let side = Session.side one after

     List.length side.Deck,
     List.length (Side.stack 2 side),
     told
     |> List.exists (function
         | Happened(PlayedFromDeck _) -> true
         | _ -> false))

report
    "and where it goes is asked when the card does not say - but never the line it came from"
    [ 1; 3 ]
    (let session = standing (opened 1UL) |> holding one (card Water 1)

     let after, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           FromDeck(FaceDown, OtherLines),
                           { Owner = one
                             Saying = card Water 1
                             Line = 2 }
                       ) ] }
             []

     match Session.asking after with
     | Some { Wanting = ALine(_, offered) } -> offered
     | _ -> [])

report
    "a card given goes out of one hand and into the other, and one taken at random comes back"
    (5, 5, true)
    (let session = standing (opened 1UL) |> holding one (card Love 3)

     let asked, _ =
         Resolving.settle
             { session with
                 Pile =
                     [ Run(
                           TakeAtRandom,
                           { Owner = one
                             Saying = card Love 3
                             Line = 1 }
                       )
                       Run(
                           Give,
                           { Owner = one
                             Saying = card Love 3
                             Line = 1 }
                       ) ] }
             []

     match Session.asking asked with
     | Some question ->
         match Resolving.choosing question (TheCard((Session.side one asked).Hand |> List.head)) asked with
         | Some after, _ ->
             List.length (Session.side one after).Hand,
             List.length (Session.side two after).Hand,
             List.length (Session.side two asked).Hand = Deck.HandSize - 1
         | None, _ -> -1, -1, false
     | _ -> -1, -1, false)

report
    "and the four new cards print what they mean"
    [ "Play the top card of your deck face down in this line, in each other line."
      "Take a card at random from your opponent's hand. Give a card from your hand to your opponent."
      "Draw the top card of your opponent's deck."
      "When this card would be covered, first: Play the top card of your deck face down in another line." ]
    ([ card Water 1; card Love 3; card Love 1; card Life 3 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "a card can take the top of their deck, and it is theirs no longer"
    (Deck.HandSize + 1, Deck.Size - Deck.HandSize - 1, true)
    (let session = standing (opened 1UL)
     let theirs = (Session.side two session).Deck |> List.head
     let after, _ = running TakeTheirTop (card Love 1) session

     List.length (Session.side one after).Hand,
     List.length (Session.side two after).Deck,
     List.contains theirs (Session.side one after).Hand)

report
    "and a reveal moves nothing at all, but leaves what it showed for the next command to point at"
    (true, true, 1)
    (let session = standing (opened 1UL) |> lyingDown two 2 [ mute Speed 2 ]

     let after, told =
         running (Show(Select.any |> Select.faceDown)) (card Light 2) session

     after.Chose = Some(mute Speed 2),
     happenings told
     |> List.exists (function
         | Showed(_, shown') -> shown' = mute Speed 2
         | _ -> false),
     Side.stack 2 (Session.side two after)
     |> List.filter (Placed.isFaceUp >> not)
     |> List.length)

report
    "rearranging is a card's command as well as the component's, and it may leave things as they were"
    (true, true)
    (let session = standing (opened 1UL)
     let after, _ = running (Rearrange Yours) (card Water 2) session

     match Session.asking after with
     | Some { Wanting = AnOrder(whose, offered) } -> whose = one, List.contains [ Water; Darkness; Fire ] offered
     | _ -> false, false)

report
    "and a card can make you rearrange theirs - the chooser and the side are two different seats"
    (true, true, true)
    (let session = standing (opened 1UL)
     let asked, _ = running (Rearrange Theirs) (card Psychic 2) session

     match Session.asking asked with
     | Some({ Wanting = AnOrder(whose, _) } as question) ->
         whose = two,
         question.Chooser = one,
         (match Resolving.ordering question [ Metal; Light; Gravity ] asked with
          | Some after, _ ->
              (Session.side two after).Order = [ Metal; Light; Gravity ]
              && (Session.side one after).Order = [ Water; Darkness; Fire ]
          | None, _ -> false)
     | _ -> false, false, false)


report
    "a stack of nothing but cards is still the sum of the cards"
    9
    (let session = standing (opened 1UL) |> poised one 1 [ quiet Water 4; quiet Water 5 ]
     Field.valueOn one 1 session.Field)

report
    "a card can say what the face-down cards beside it are worth"
    (4, 6)
    (let stacked way =
        standing (opened 1UL)
        |> beneath one 1 [ way (card Darkness 2); Placed.down (quiet Water 5) ]

     Field.valueOn one 1 (stacked Placed.down).Field, Field.valueOn one 1 (stacked Placed.up).Field)

report
    "a card can add to the total for each face-down card beside it"
    (Placed.FaceDownValue * 2 + 2, 0)
    (let session =
        standing (opened 1UL)
        |> beneath
            one
            1
            [ Placed.up (card Apathy 0)
              Placed.down (quiet Water 4)
              Placed.down (quiet Water 5) ]

     Field.valueOn one 1 session.Field, (card Apathy 0).Value)

report
    "and a card across the table can take away from it - which is why a side cannot answer alone"
    (9, 7)
    (let alone = standing (opened 1UL) |> poised one 1 [ quiet Water 4; quiet Water 5 ]

     let opposed = alone |> poised two 1 [ card Metal 0 ]

     Field.valueOn one 1 alone.Field, Field.valueOn one 1 opposed.Field)

report
    "a total is never less than nothing"
    0
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ quiet Gravity 1 ]
        |> poised two 1 [ card Metal 0 ]

     Field.valueOn one 1 session.Field)

report
    "a modifier stops when the card carrying it stops - Metal-0 says it from its top box"
    (7, 7)
    (let uncovered =
        standing (opened 1UL) |> poised one 1 [ quiet Water 4; quiet Water 5 ]

     let both = uncovered |> poised two 1 [ quiet Gravity 1; card Metal 0 ]

     Field.valueOn one 1 (uncovered |> poised two 1 [ card Metal 0 ]).Field, Field.valueOn one 1 both.Field)

report
    "the compile check reads the same number, so a modifier can hold a line back"
    (true, false)
    (let winning = standing (opened 1UL) |> poised one 1 [ quiet Water 5; quiet Water 5 ]

     let held = winning |> poised two 1 [ card Metal 0 ]

     Field.won one 1 winning.Field, Field.won one 1 held.Field)

report
    "and all three print what they mean"
    [ "Your total in this line is increased by 1 for each face-down card in it."
      "Every face-down card in this stack is worth 4."
      "Their total in this line is reduced by 2." ]
    ([ card Apathy 0; card Darkness 2; card Metal 0 ]
     |> List.map (fun each -> Words.printed each |> List.head))


report
    "a card about to be covered says its piece first, and the covering card lands after"
    (true, 2)
    (let session =
        standing (opened 1UL)
        |> holding one (quiet Water 4)
        |> poised one 1 [ card Apathy 2 ]

     match Turn.asked (Play(quiet Water 4, 1, FaceUp)) session with
     | Some after, told ->
         let stack = Side.stack 1 (Session.side one after)

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
        |> holding one (quiet Water 4)
        |> poised one 1 [ card Apathy 2 ]

     match Turn.asked (Play(quiet Water 4, 1, FaceUp)) session with
     | Some after, _ -> Field.valueOn one 1 after.Field
     | None, _ -> -1)

report
    "nothing interrupts an empty line, or a card lying face down"
    [ false; false ]
    ([ []; [ Placed.down (card Apathy 2) ] ]
     |> List.map (fun under ->
         let session =
             standing (opened 1UL) |> holding one (quiet Water 4) |> beneath one 1 under

         match Turn.asked (Play(quiet Water 4, 1, FaceUp)) session with
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
        |> holding one (card Darkness 4)
        |> lyingDown one 1 [ mute Speed 2 ]
        |> poised one 3 [ card Apathy 2 ]

     match Turn.asked (Play(card Darkness 4, 2, FaceUp)) session with
     | Some next, _ ->
         match Session.asking next with
         | Some({ Wanting = ALine _ } as question) ->
             match Resolving.choosing question (TheLine 3) next with
             | Some _, told ->
                 told
                 |> List.exists (function
                     | Happened(Flipped(_, turned, 3)) -> turned.Card = card Apathy 2
                     | _ -> false)
             | None, _ -> false
         | _ -> false
     | None, _ -> false)

report
    "and the interrupt is printed as one"
    "When this card would be covered, first: Flip this card."
    (Words.printed (card Apathy 2) |> List.last)


report
    "a card that shifts does not read as newly shown, however far it goes"
    (true, false)
    (let session = standing (opened 1UL) |> poised one 1 [ card Gravity 1 ]

     let after, told =
         running (Shift(Select.any |> Select.this', AnyLine)) (card Gravity 1) session

     match Session.asking after with
     | Some question ->
         match Resolving.choosing question (TheLine 2) after with
         | Some moved, said ->
             Side.stack 2 (Session.side one moved)
             |> List.exists (fun placed -> placed.Card = card Gravity 1),
             happenings (told @ said)
             |> List.exists (function
                 | Drew _ -> true
                 | _ -> false)
         | None, _ -> false, true
     | None -> false, true)


report
    "a command that could not happen stops everything waiting behind it"
    (true, false)
    (let session =
        standing (opened 1UL)
        |> holding one (card Fire 1)
        |> poised two 2 [ quiet Metal 3 ]

     let empty =
         { session with
             Field =
                 session.Field
                 |> Field.update one (fun side -> { side with Hand = [ card Fire 1 ] }) }

     match Turn.asked (Play(card Fire 1, 3, FaceUp)) empty with
     | Some after, told ->
         told
         |> List.exists (function
             | Happened(Fizzled _) -> true
             | _ -> false),
         Side.stack 2 (Session.side two after) |> List.isEmpty
     | None, _ -> false, true)

report
    "and when it can happen, what was waiting happens too"
    true
    (let session =
        standing (opened 1UL)
        |> holding one (card Fire 1)
        |> poised two 2 [ quiet Metal 3 ]

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

     match Turn.asked (Play(card Death 1, 1, FaceDown)) session with
     | Some _, _ ->
         let asked, _ =
             Resolving.settle
                 { session with
                     Pile =
                         [ Run(
                               IfYouDo(May(Draw(Just 1)), [ Delete Select.any ]),
                               { Owner = one
                                 Saying = card Death 1
                                 Line = 1 }
                           ) ] }
                 []

         match Session.asking asked with
         | Some { Wanting = Whether _ } ->
             match Resolving.choosing (Session.asking asked).Value No asked with
             | Some after, _ -> List.length (Session.side one after).Hand = Deck.HandSize, after.Done > 0
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
                           IfYouDo(May(Draw(Just 1)), [ Draw(Just 1) ]),
                           { Owner = one
                             Saying = card Death 1
                             Line = 1 }
                       ) ] }
             []

     match Resolving.choosing (Session.asking asked).Value Yes asked with
     | Some after, _ -> List.length (Session.side one after).Hand = Deck.HandSize + 2, after.Done > 0
     | None, _ -> false, false)

report
    "an offer of something impossible is not made at all"
    true
    (let session =
        standing (opened 1UL)
        |> holding one (card Death 1)
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
    [ true; true; true; true ]
    ([ Yes; No; TheFirst; TheSecond ]
     |> List.map (fun said ->
         match Playable.read compiled (compiled.Write(Make(Choose said))) with
         | Ok(Send(Make(Choose read))) -> read = said
         | _ -> false))


let private eitherOr session =
    { session with
        Pile =
            [ Run(
                  Either(Discard, Flip(Select.any |> Select.this')),
                  { Owner = one
                    Saying = card Spirit 1
                    Line = 1 }
              ) ] }

report
    "either-or asks which of the two, and it is not a yes-or-no"
    (true, true)
    (let session = standing (opened 1UL) |> poised one 1 [ card Spirit 1 ]

     let after, _ = Resolving.settle (eitherOr session) []

     match Session.asking after with
     | Some { Wanting = OneOf(Discard, Flip _) } -> true, Session.active after = one
     | _ -> false, false)

report
    "and answering it does that half, with nothing left waiting either way"
    (true, false)
    (let session = standing (opened 1UL) |> poised one 1 [ card Spirit 1 ]

     let asked, _ = Resolving.settle (eitherOr session) []

     match Resolving.choosing (Session.asking asked).Value TheSecond asked with
     | Some after, _ ->
         Side.stack 1 (Session.side one after)
         |> List.exists (fun placed -> placed.Card = card Spirit 1 && not (Placed.isFaceUp placed)),
         (Session.asking after).IsSome
     | None, _ -> false, true)

report
    "a half nobody could carry out is not offered at all - the other one simply happens"
    (false, true)
    (let session =
        standing (opened 1UL)
        |> poised one 1 [ card Spirit 1 ]
        |> fun session ->
            { session with
                Field = session.Field |> Field.update one (fun side -> { side with Hand = [] }) }

     let after, _ = Resolving.settle (eitherOr session) []

     (Session.asking after).IsSome,
     Side.stack 1 (Session.side one after)
     |> List.exists (fun placed -> placed.Card = card Spirit 1 && not (Placed.isFaceUp placed)))

report
    "and neither of them possible is a fizzle, like any other command with nothing to do"
    true
    (let session =
        standing (opened 1UL)
        |> fun session ->
            { session with
                Field = session.Field |> Field.update one (fun side -> { side with Hand = [] }) }

     let _, told = Resolving.settle (eitherOr session) []

     happenings told
     |> List.exists (function
         | Fizzled _ -> true
         | _ -> false))

report
    "and the card prints both halves"
    "At the start of your turn: Either discard a card or flip this card."
    (Words.printed (card Spirit 1) |> List.last)

report
    "and both cards print what they do"
    [ "Discard a card. If you do, delete any card."
      "At the start of your turn: You may draw a card. If you do, delete any other card, then delete this card." ]
    ([ card Fire 1; card Death 1 ]
     |> List.map (fun each -> Words.printed each |> List.head))


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
    "and it is the rules asking, not whatever card is at the top of the hand"
    (Some TheCacheCheck, true)
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

     match Turn.asked (Play(played, 1, FaceDown)) bulging with
     | Some after, told ->
         (Session.asking after |> Option.map (fun asked -> asked.Because)),
         told
         |> List.map compiled.Says
         |> List.exists (fun said -> mentions "check cache phase" said)
     | None, _ -> None, false)

report
    "and every screen names the phase rather than a card, the same as the log does"
    true
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

     match Turn.asked (Play(played, 1, FaceDown)) bulging with
     | Some after, _ ->
         let model = opened 1UL

         let drawnAt =
             { model with
                 Timeline = Timeline.advance (Make Refresh) after model.Timeline }

         Playable.offered AtATerminal standard compiled
         |> List.forall (fun view ->
             let screen = view.Board Margins.all one drawnAt

             mentions "check cache phase" screen
             && (Session.side one after).Hand
                |> List.forall (fun held -> not (mentions $"{Card.name held} is waiting" screen)))
     | None, _ -> false)

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


let private drew told =
    told
    |> List.exists (function
        | Happened(Drew _) -> true
        | _ -> false)

report
    "a card covered says nothing, and says it again when whatever covered it goes"
    (false, true)
    (let covered = standing (opened 1UL) |> poised one 3 [ mute Hate 3; card Fire 0 ]

     let settled, first = Resolving.settle covered []

     let uncovered =
         { settled with
             Field =
                 settled.Field
                 |> Field.update one (fun side ->
                     { side with
                         Stacks = side.Stacks |> Map.add 3 [ Placed.up (card Fire 0) ] }) }

     drew first, drew (Resolving.settle uncovered [] |> snd))

report
    "and a card at the bottom of a stack says nothing however face up it is"
    (2, 1)
    (let session = standing (opened 1UL) |> poised two 2 [ quiet Metal 3; quiet Light 3 ]
     let stack = Side.stack 2 (Session.side two session)

     List.length (stack |> List.filter Placed.isFaceUp),
     List.length (stack |> List.filter (fun placed -> Stack.uncovered stack = Some placed)))


let private controlRules = controlled.Rules

let private openedWith seed =
    (draft @ orders)
    |> List.fold
        (fun model move -> Update.update controlRules (Make move) model)
        (Update.start controlRules Session.Seats seed |> Result.toOption |> Option.get)

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
     (match
         handedOver (
             standing (opened 1UL)
             |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]
         )
      with
      | Some after, _ -> after.Control
      | None, _ -> InTheMiddle))

report "with the rule it starts in the middle" InTheMiddle ((standing (openedWith 1UL)).Control)

report
    "leading a lane is not winning one: no ten needed, and a tie is still not a lead"
    [ true; false; false ]
    (let session = standing (openedWith 1UL)

     [ [ card Water 1 ], []; [ card Water 1 ], [ card Gravity 1 ]; [], [] ]
     |> List.map (fun (mine, theirs) ->
         let doctored = session |> poised one 1 mine |> poised two 1 theirs
         Field.leads one 1 doctored.Field))

report
    "leading two lanes at the start of a turn takes the component out of the middle"
    (HeldBy one, true)
    (let session =
        standing (openedWith 1UL)
        |> poised one 1 [ quiet Gravity 1 ]
        |> poised one 2 [ quiet Darkness 3 ]

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
    (let session = standing (openedWith 1UL) |> poised one 1 [ quiet Gravity 1 ]

     match handedBack session with
     | Some after, _ -> after.Control
     | None, _ -> HeldBy one)

report
    "and it comes off the other player when it has to"
    (HeldBy one, true)
    (let session =
        standing (openedWith 1UL)
        |> holdingControl two
        |> poised one 1 [ quiet Gravity 1 ]
        |> poised one 2 [ quiet Darkness 3 ]

     match handedBack session with
     | Some after, told -> after.Control, told |> List.map controlled.Says |> List.exists (mentions "from Player 2")
     | None, _ -> InTheMiddle, false)


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
         | Some { Wanting = AnOrder(_, offered) } -> List.length offered, List.contains [ Water; Darkness; Fire ] offered
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
    ([ Darkness; Water; Fire ], [ card Water 4 ], [ card Darkness 3 ])
    (let session =
        standing (openedWith 1UL)
        |> holdingControl one
        |> poised one 1 [ quiet Water 4 ]
        |> poised one 2 [ quiet Darkness 3 ]

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


report
    "holding it and compiling asks first, and the compile has not happened yet"
    (true, false)
    (let session =
        standing (openedWith 1UL)
        |> holdingControl one
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]

     match handedBack session with
     | Some after, _ -> (Session.asking after).IsSome, Set.isEmpty (Session.side one after).Compiled |> not
     | None, _ -> false, true)

report
    "a stack built for Water compiles Darkness, because holding the component moved it"
    ([ Darkness ], false)
    (let session =
        standing (openedWith 1UL)
        |> holdingControl one
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]

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
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]

     match handedBack session with
     | Some waiting, _ ->
         match Turn.asked (Arrange [ Darkness; Water; Fire ]) waiting with
         | Some after, _ -> Field.valueOn one 1 after.Field
         | None, _ -> -1
     | None, _ -> -1)

report
    "not holding it, a won line compiles the protocol that was facing it all along"
    [ Water ]
    (let session =
        standing (openedWith 1UL)
        |> poised one 1 [ quiet Water 5; quiet Water 5; quiet Gravity 1 ]

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

         let start =
             Update.start controlRules Session.Seats seed |> Result.toOption |> Option.get

         let finished =
             walk start (controlled.Seating seed [ Some "easy"; Some "easy" ] (standing start)) 0

         match Session.ending (standing finished) with
         | Some(Won _) -> true
         | _ -> false))


let private accounted session =
    Session.seats
    |> List.map (fun seat -> Session.side seat session |> allOf |> List.length)

let private sixPlayed seed =
    [ 1..6 ]
    |> List.fold
        (fun model n ->
            let seat = rules.Active(standing model)
            let card = handOf seat model |> List.head
            Update.update rules (Make(Play(card, (n % Lines.Count) + 1, FaceDown))) model)
        (opened seed)

report "thirty-six cards in all, wherever they are" 36 (accounted (standing (opened 1UL)) |> List.sum)

report "eighteen each at the deal, before anything can have crossed" [ 18; 18 ] (accounted (standing (opened 1UL)))

report "and still eighteen each after six cards have been played" [ 18; 18 ] (accounted (standing (sixPlayed 1UL)))

let private loadedLine seat line session =
    let chosen =
        (Session.side seat session).Deck
        |> List.sortByDescending (fun c -> c.Value)
        |> List.truncate 3

    { session with
        Field =
            session.Field
            |> Field.update seat (fun side ->
                { side with
                    Deck = side.Deck |> List.filter (fun c -> not (List.contains c chosen))
                    Stacks = side.Stacks |> Map.add line (chosen |> List.map Placed.up) }) }
    |> Resolving.asRead

report
    "the loaded line is over ten, so the two checks below are checking something"
    true
    (Field.valueOn one 1 (standing (opened 1UL) |> loadedLine one 1).Field
     >= Stack.ToCompile)

report
    "compiling moves cards without making or losing any"
    (36, [ 18; 18 ])
    (match handedOver (standing (opened 1UL) |> loadedLine one 1) with
     | Some after, _ ->
         let counted =
             Session.seats
             |> List.map (fun seat -> Session.side seat after |> allOf |> List.length)

         List.sum counted, counted
     | None, _ -> -1, [])

report
    "a second compile takes a card across the table: nineteen against seventeen, and still thirty-six"
    (36, [ 19; 17 ])
    (match handedOver (standing (opened 1UL) |> having one [ Water ] |> loadedLine one 1) with
     | Some after, _ ->
         let counted =
             Session.seats
             |> List.map (fun seat -> Session.side seat after |> allOf |> List.length)

         List.sum counted, counted
     | None, _ -> -1, [])

report
    "no card is in two places at once"
    [ true; true ]
    (Session.seats
     |> List.map (fun seat ->
         let everywhere = Session.side seat (standing (sixPlayed 1UL)) |> allOf
         List.distinct everywhere |> List.length = List.length everywhere))


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
    "a word the parser cannot place is asked about rather than guessed at"
    (Ok "brimstone")
    (match Playable.read compiled "brimstone" with
     | Ok(Asking asked) -> Ok asked
     | Ok _ -> Error "read as a move"
     | Error problem -> Error problem)

report
    "and a protocol named at the draft is still a pick, said the short way"
    (Ok(Make(Take Fire)))
    (match Playable.read compiled "fire" with
     | Ok(Send(Make move)) -> Ok(Make move)
     | _ -> Error "not a move")

report
    "but a record with a line like that in it still cannot be read, because only a move may be in one"
    true
    (match Transcript.read compiled "deal 2 1 you you\nbrimstone\n" with
     | Error problem -> mentions "brimstone" problem
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
    (let told =
        Turn.asked (Play({ Protocol = Fire; Value = 1 }, 1, FaceDown)) (standing (dealt 1UL))
        |> snd

     told
     |> List.map compiled.Says
     |> List.exists (mentions "the draft is still going"))


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

let private gameBetween skills seed =
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
    walk start (compiled.Seating seed (skills |> List.map Some) (standing start)) 0

let private machineGame = gameBetween [ "easy"; "easy" ]

report
    "the machine plays a whole game legally, from the draft to somebody winning"
    (true, Nothing, true)
    (let finished = machineGame 4UL
     let session = standing finished

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


let private wonBy skills seed =
    match Session.ending (standing (gameBetween skills seed)) with
    | Some(Won winner) -> Some winner
    | _ -> None

report
    "counting beats not counting, and from either seat - which is the whole of what 'medium' says"
    (true, true)
    (let deals = [ 4UL .. 15UL ]

     let took skills who =
         deals |> List.filter (fun seed -> wonBy skills seed = Some who) |> List.length

     took [ "medium"; "easy" ] one >= 8, took [ "easy"; "medium" ] two >= 8)

report
    "and reading beats counting against the same machine that only counts"
    (true, true)
    (let deals = [ 4UL .. 15UL ]

     let took skills who =
         deals |> List.filter (fun seed -> wonBy skills seed = Some who) |> List.length

     took [ "hard"; "easy" ] one >= 8, took [ "easy"; "hard" ] two >= 8)

report
    "and looking a move ahead beats reading, from the seat that has to come from behind"
    (true, true)
    (let deals = [ 4UL .. 15UL ]

     let took skills who =
         deals |> List.filter (fun seed -> wonBy skills seed = Some who) |> List.length

     took [ "deep"; "hard" ] one >= 8, took [ "hard"; "deep" ] two >= 5)


report
    "the machine that reads takes the card that does more; the one that counts takes the bigger number"
    ("Water-4", "Water-3")
    (let posed =
        let session = standing (opened 1UL)

        { session with
            Field =
                session.Field
                |> Field.update one (fun side ->
                    { side with
                        Hand = [ card Water 3; card Water 4 ] }) }

     let plays skill =
         match Rival.plays posed { Skill = skill; Rng = Rng.ofSeed 7UL } with
         | Some(Play(chosen, 1, FaceUp), _) -> Card.name chosen
         | _ -> "nothing"

     plays Rival.medium, plays Rival.hard)

report
    "and no machine, however much it reads or looks, is ever left with nothing it will play"
    [ true; true; true; true ]
    (let posed hand =
        let session = standing (opened 1UL)

        { session with
            Field = session.Field |> Field.update one (fun side -> { side with Hand = hand }) }

     Rival.all
     |> List.map (fun skill ->
         match Rival.plays (posed [ card Water 5 ]) { Skill = skill; Rng = Rng.ofSeed 7UL } with
         | Some(Play _, _) -> true
         | _ -> false))


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
     |> List.forall (fun view -> mentions (Card.name card) (view.Board Margins.all one after)))

report
    "the page carries no control the game would not take"
    []
    (let model = opened 1UL
     let page = asPage.Board Margins.all one model

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
    (let page = asPage.Board Margins.all one (dealt 1UL)

     Protocol.all
     |> List.forall (fun protocol -> mentions (Protocol.name protocol) page))

report
    "both seats take a colour, and they are not the same one"
    (2, 2)
    (List.length compiled.Slots,
     compiled.Slots
     |> List.map (fun slot -> slot.Standard)
     |> List.distinct
     |> List.length)


let rec private wording scene : string list =
    match scene with
    | Blank -> []
    | Say line -> [ Scene.plainText line ]
    | Note text
    | Written text
    | Heading text -> [ text ]
    | Does(caption, _, _) -> [ caption ]
    | Block(_, body)
    | Stack body
    | Beside body -> body |> List.collect wording
    | Patch(_, _, body) -> body |> List.collect wording
    | Tile(title, _, body) ->
        Option.toList title
        @ [ body |> List.collect wording |> String.concat " " |> flowing ]
    | Walled(_, rows) -> rows |> List.collect (fun row -> row.Cells |> List.collect wording)
    | Aligned rows -> rows |> List.map (List.map Scene.plainText >> String.concat " ")
    | Big span -> [ span.Text ]

let rec private named scene : string list =
    match scene with
    | Tile(title, _, body) -> Option.toList title @ (body |> List.collect named)
    | Block(_, body)
    | Stack body
    | Beside body
    | Patch(_, _, body) -> body |> List.collect named
    | Walled(_, rows) -> rows |> List.collect (fun row -> row.Cells |> List.collect named)
    | Blank
    | Say _
    | Note _
    | Written _
    | Heading _
    | Does _
    | Aligned _
    | Big _ -> []

let rec private blockOf title scene =
    match scene with
    | Block(name, body) when name = title -> Some(Block(name, body))
    | Block(_, body)
    | Stack body
    | Beside body -> body |> List.tryPick (blockOf title)
    | _ -> None

let private boardAt seat session =
    let model = opened 1UL

    Render.board
        Margins.all
        seat
        { model with
            Timeline = Timeline.advance (Make Refresh) session model.Timeline }

let private shownAt seat session =
    boardAt seat session |> wording |> String.concat "\n"

let private boxesOn seat session =
    boardAt seat session
    |> blockOf Render.Blocks.field
    |> Option.get
    |> named
    |> List.filter (fun name -> List.contains name [ "top"; "middle"; "bottom" ])

let private inTheTop = "After your opponent discards cards"

let private inTheMiddle = "Your opponent: discard a card"

report
    "a card face up on its own says everything printed on it"
    (true, true)
    (let screen = shownAt one (standing (opened 1UL) |> poised one 1 [ card Plague 1 ])
     mentions inTheTop screen, mentions inTheMiddle screen)

report
    "and once something is played over it, only what a cover cannot silence"
    (true, false)
    (let screen =
        shownAt one (standing (opened 1UL) |> poised one 1 [ quiet Water 4; card Plague 1 ])

     mentions inTheTop screen, mentions inTheMiddle screen)

report
    "a card face down says nothing at all, to the player holding it or to anybody else"
    [ false; false ]
    (let session =
        standing (opened 1UL)
        |> lyingDown one 1 [ card Plague 1 ]
        |> onlyHolding one (card Water 4)

     [ one; two ]
     |> List.map (fun seat ->
         let screen = shownAt seat session
         mentions inTheTop screen || mentions inTheMiddle screen))

report
    "what a card says is on the board of whoever is looking, theirs as well as yours"
    true
    (let screen = shownAt two (standing (opened 1UL) |> poised one 1 [ card Plague 1 ])
     mentions inTheMiddle screen)

report
    "a card in hand says what it does, so a hand can be played from without being memorised"
    true
    (let model = opened 1UL
     let screen = Render.board Margins.all one model |> wording |> String.concat "\n"

     handOf one model
     |> List.forall (fun held -> Words.printed held |> List.forall (fun said -> mentions said screen)))

report
    "and only the reader's own hand does - what the other is holding is still nobody's business"
    false
    (let model = opened 1UL
     let screen = drawn plain two model

     handOf one model
     |> List.filter (fun held -> not (List.contains held (handOf two model)))
     |> List.exists (fun held -> mentions (Card.name held) screen))


report
    "a card face up is drawn as three boxes, and one that uses a single box still has three"
    [ "top"; "middle"; "bottom" ]
    (boxesOn one (standing (opened 1UL) |> poised one 1 [ card Water 5 ]))

report
    "and a card face down is drawn as no boxes at all, having nothing to say out of any of them"
    []
    (boxesOn one (standing (opened 1UL) |> lyingDown one 1 [ card Water 5 ]))

report
    "a covered card is drawn without its boxes, and what is left is its top and nothing else"
    ([ "top"; "middle"; "bottom" ], true, false)
    (let session = standing (opened 1UL) |> poised one 1 [ quiet Water 4; card Plague 1 ]
     let screen = shownAt one session
     boxesOn one session, mentions inTheTop screen, mentions inTheMiddle screen)

report
    "and a card played face down covers just the same, being a card played on a line like any other"
    ([], true, false)
    (let session =
        standing (opened 1UL)
        |> beneath one 1 [ Placed.down (quiet Water 4); Placed.up (card Plague 1) ]

     let screen = shownAt one session
     boxesOn one session, mentions inTheTop screen, mentions inTheMiddle screen)

report
    "and every one of the ninety is drawn the same way, whatever is printed on it"
    []
    (Protocol.all
     |> List.collect Card.inProtocol
     |> List.filter (fun each ->
         boxesOn one (standing (opened 1UL) |> poised one 1 [ each ])
         <> [ "top"; "middle"; "bottom" ]))

let rec private typing scene : string list =
    match scene with
    | Does(_, line, _) -> [ line ]
    | Block(_, body)
    | Stack body
    | Beside body
    | Tile(_, _, body)
    | Patch(_, _, body) -> body |> List.collect typing
    | Walled(_, rows) -> rows |> List.collect (fun row -> row.Cells |> List.collect typing)
    | Blank
    | Say _
    | Note _
    | Written _
    | Heading _
    | Aligned _
    | Big _ -> []

report
    "the hand offers a control for the one line a card could go face up on, and none for face down"
    true
    (let model = opened 1UL

     let offered =
         Render.board Margins.all one model
         |> blockOf Render.Blocks.hand
         |> Option.get
         |> typing

     not (List.isEmpty offered)
     && offered
        |> List.forall (fun line -> line = "refresh" || not (line.EndsWith " down")))


report
    "every view names the cards a question is offering, however little else is in the cell"
    true
    (let session =
        standing (opened 1UL)
        |> onlyHolding one (card Fire 4)
        |> fun session ->
            { session with
                Field =
                    session.Field
                    |> Field.update one (fun side ->
                        { side with
                            Hand = [ card Fire 4; card Water 2; card Water 3 ] }) }

     let model = opened 1UL

     let asked =
         Update.update
             rules
             (Make(Play(card Fire 4, 3, FaceUp)))
             { model with
                 Timeline = Timeline.advance (Make Refresh) session model.Timeline }

     Session.asking (standing asked) |> Option.isSome
     && (Playable.offered AtATerminal standard compiled
         |> List.forall (fun view ->
             let screen = view.Board Margins.all one asked

             mentions (Card.name (card Water 2)) screen
             && mentions (Card.name (card Water 3)) screen)))


let private asking seat model = plain.Answer seat "nonsense" model

report
    "a line nobody could read is answered with what is being asked for, and says so"
    true
    (asking one (opened 1UL) |> reads "I do not know how to 'nonsense'")

report
    "at the draft that is the protocols still on the table"
    true
    (let screen = asking one (dealt 1UL)

     Protocol.all
     |> List.forall (fun protocol -> mentions (Protocol.key protocol) screen))

report
    "and in play it is your own hand, and the lines each card of it could go face up on"
    true
    (let model = opened 1UL
     let screen = asking one model

     handOf one model
     |> List.forall (fun held ->
         mentions (Card.name held) screen
         && Field.facingLines one held (standing model).Field
            |> List.forall (fun line -> reads $"'{Card.key held} {line}'" screen)))

report
    "and never the other player's, whoever it was that asked"
    false
    (let model = opened 1UL
     let screen = asking two model

     handOf one model
     |> List.filter (fun held -> not (List.contains held (handOf two model)))
     |> List.exists (fun held -> mentions (Card.name held) screen))

report
    "and a card waiting on somebody outranks the stage, here as on the board"
    (true, true)
    (let session =
        standing (opened 1UL)
        |> onlyHolding one (card Fire 4)
        |> fun session ->
            { session with
                Field =
                    session.Field
                    |> Field.update one (fun side ->
                        { side with
                            Hand = [ card Fire 4; card Water 2; card Water 3 ] }) }

     let model = opened 1UL

     let asked =
         Update.update
             rules
             (Make(Play(card Fire 4, 3, FaceUp)))
             { model with
                 Timeline = Timeline.advance (Make Refresh) session model.Timeline }

     let screen = asking one asked
     reads "Fire-4 is waiting on you" screen, reads "Water-2" screen)


let private posed =
    standing (opened 1UL)
    |> beneath one 1 [ Placed.down (card Water 4) ]
    |> beneath two 2 [ Placed.turned (Placed.up (card Metal 2)); Placed.down (card Gravity 5) ]

let private peeked seat rest =
    let model = opened 1UL

    plain.Answer
        seat
        rest
        { model with
            Timeline = Timeline.advance (Make Refresh) posed model.Timeline }

report
    "a card you played face down is one you may read, and what is printed on it comes with it"
    (true, true)
    (let screen = peeked one "peek"
     mentions (Card.name (card Water 4)) screen, reads "Return your card to hand" screen)

report
    "and 'peek' alone is your own side - it says nothing about the other half of the table"
    (false, false)
    (let screen = peeked one "peek"
     mentions (Card.name (card Metal 2)) screen, mentions (Card.name (card Gravity 5)) screen)

report
    "'peek all' adds a card of theirs that was face up here and has been turned over since"
    true
    (peeked one "peek all" |> mentions (Card.name (card Metal 2)))

report
    "and never one of theirs that has only ever been face down"
    false
    (peeked one "peek all" |> mentions (Card.name (card Gravity 5)))

report
    "which is the same card to its owner, who put it there and may read it"
    (true, true)
    (let screen = peeked two "peek all"
     mentions (Card.name (card Gravity 5)) screen, mentions (Card.name (card Metal 2)) screen)

report
    "the knowing is on the card and dies with it - back to hand and played down again, it is a secret"
    (true, false)
    (let over =
        standing (opened 1UL)
        |> beneath two 2 [ Placed.turned (Placed.up (card Metal 2)) ]

     let afresh = standing (opened 1UL) |> beneath two 2 [ Placed.down (card Metal 2) ]

     let reading session =
         let model = opened 1UL

         plain.Answer
             one
             "peek all"
             { model with
                 Timeline = Timeline.advance (Make Refresh) session model.Timeline }
         |> mentions (Card.name (card Metal 2))

     reading over, reading afresh)

report
    "and a card played face up and flipped in play is read the same way, without a fixture saying so"
    true
    (let session =
        standing (opened 1UL)
        |> onlyHolding one (card Fire 0)
        |> poised two 2 [ card Metal 2 ]

     match Turn.asked (Play(card Fire 0, 3, FaceUp)) session with
     | Some after, _ ->
         let model = opened 1UL

         let screen =
             plain.Answer
                 one
                 "peek all"
                 { model with
                     Timeline = Timeline.advance (Make Refresh) after model.Timeline }

         Side.stack 2 (Session.side two after) |> List.forall (Placed.isFaceUp >> not)
         && mentions (Card.name (card Metal 2)) screen
     | None, _ -> false)


report
    "the pile can be read, in the order it will resolve, and says what is waiting on whom"
    (true, true)
    (let session =
        standing (opened 1UL)
        |> onlyHolding one (card Fire 4)
        |> fun session ->
            { session with
                Field =
                    session.Field
                    |> Field.update one (fun side ->
                        { side with
                            Hand = [ card Fire 4; card Water 2; card Water 3 ] }) }

     let model = opened 1UL

     let asked =
         Update.update
             rules
             (Make(Play(card Fire 4, 3, FaceUp)))
             { model with
                 Timeline = Timeline.advance (Make Refresh) session model.Timeline }

     let screen = plain.Answer one "pile" asked
     reads "waiting on you" screen, reads "the turn is handed on" screen)

report
    "and a game in the middle of nothing says so rather than drawing an empty list"
    true
    (plain.Answer one "pile" (opened 1UL) |> reads "nothing waiting")


let private theirHandShown seat (asked: Model<Move, Session, Notice>) drawn =
    (Session.side (Session.other seat) (standing asked)).Hand
    |> List.exists (fun held -> mentions (Card.name held) drawn)

report
    "a card that makes them discard does not print their hand on your board, in any view"
    ([], true)
    (let session =
        standing (opened 1UL)
        |> onlyHolding one (card Plague 0)
        |> fun session ->
            { session with
                Pile =
                    [ Run(
                          Opposing Discard,
                          { Owner = one
                            Saying = card Plague 0
                            Line = 1 }
                      ) ] }

     let after, _ = Resolving.settle session []
     let model = opened 1UL

     let asked =
         { model with
             Timeline = Timeline.advance (Make Refresh) after model.Timeline }

     (Playable.offered AtATerminal standard compiled
      |> List.filter (fun view -> theirHandShown one asked (view.Board Margins.all one asked))
      |> List.map (fun view -> view.Name)),
     (match Session.asking (standing asked) with
      | Some { Chooser = chooser
               Wanting = ACard(_, targets) } ->
          chooser = two
          && targets |> List.forall (fun target -> Target.owner target = two)
      | _ -> false))

report
    "nor in the pile, nor in what is being asked - and their own screen still lists them"
    (false, false, true)
    (let session =
        standing (opened 1UL)
        |> fun session ->
            { session with
                Pile =
                    [ Run(
                          Opposing Discard,
                          { Owner = one
                            Saying = card Plague 0
                            Line = 1 }
                      ) ] }

     let after, _ = Resolving.settle session []
     let model = opened 1UL

     let asked =
         { model with
             Timeline = Timeline.advance (Make Refresh) after model.Timeline }

     theirHandShown one asked (plain.Answer one "pile" asked),
     theirHandShown one asked (plain.Answer one "nonsense" asked),
     theirHandShown two asked (plain.Answer two "pile" asked) |> not)


// === The seam every game fills in ===

Conforms.against compiled 2 [ "draft fire"; "draft water"; "draft metal" ]

Conforms.against controlled 2 [ "draft fire"; "draft water"; "draft metal" ]

finish ()
