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

/// A draft that gives Player 1 Fire, Water and Dark, and Player 2 Light, Metal and Gravity -
/// which is the game the rules were described with, picked in the order 1-2-2-1.
let private draft =
    [ Take Fire; Take Light; Take Metal; Take Water; Take Dark; Take Gravity ]

let private orders =
    [ Arrange [ Water; Dark; Fire ]; Arrange [ Gravity; Metal; Light ] ]

/// A whole game up to the first card: drafted, arranged, dealt.
let private opened seed = played seed (draft @ orders)

let private handOf seat model = (Session.side seat (standing model)).Hand

let private mentions (needle: string) (text: string) = text.Contains needle

// --- what the game says is wrong with itself ---------------------------------------------
//
// The one thing a game built out of data can check about itself before anybody sits down.
// Twelve protocols, six cards apiece and a draft of six picks are three lists that have to
// agree with each other, and this is where they say whether they do.

report "the game finds nothing wrong with itself" [] compiled.Faults

report "twelve protocols, none of them twice" 12 (List.distinct Protocol.all |> List.length)

report "six cards to a protocol" 6 Card.PerProtocol

report "eighteen to a deck" 18 Deck.Size

report "seventy-two cards in all" 72 (Protocol.all |> List.collect Card.inProtocol |> List.distinct |> List.length)

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
    ([ Fire; Water; Dark ], [ Light; Metal; Gravity ])
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

report
    "each protocol faces the line it was said for"
    ([ Some Water; Some Dark; Some Fire ], [ Some Gravity; Some Metal; Some Light ])
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

report
    "a card played leaves the hand and lands on the line"
    (4, [ true ])
    (let model = opened 1UL
     let card = firstCard model
     let after = Update.update rules (Make(Play(card, 2))) model |> standing
     let side = Session.side one after

     List.length side.Hand, [ Side.stack 2 side = [ card ] ])

report
    "the card is on the line it was played to, and on no other"
    [ []; [ true ]; [] ]
    (let model = opened 1UL
     let card = firstCard model
     let after = Update.update rules (Make(Play(card, 2))) model |> standing
     let side = Session.side one after

     Lines.all
     |> List.map (fun line ->
         match Side.stack line side with
         | [] -> []
         | stack -> [ stack = [ card ] ]))

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
                 let model = Update.update rules (Make(Play(card, 1))) model
                 Update.update rules (Make(Play(handOf two model |> List.head, 1))) model)
             model

     match Side.stack 1 (Session.side one (standing after)) with
     | top :: _ -> (if top = cards[1] then 2 else 1)
     | [] -> 0)

report
    "the turn passes to the other seat"
    two
    (let model = opened 1UL
     rules.Active(standing (Update.update rules (Make(Play(firstCard model, 1))) model)))

report
    "a line that is not there is refused"
    (Some(Refused(NoSuchLine 4)))
    (let model = opened 1UL
     Turn.asked (Play(firstCard model, 4)) (standing model) |> snd |> List.tryHead)

report
    "a card that is not in hand is refused"
    true
    (let model = opened 1UL
     let theirs = handOf two model |> List.head

     match Turn.asked (Play(theirs, 1)) (standing model) |> snd with
     | [ Refused(NotInHand card) ] -> card = theirs
     | _ -> false)

report
    "a refused play leaves the position exactly where it was"
    true
    (let model = opened 1UL
     let before = standing model
     Turn.asked (Play(firstCard model, 9)) before |> fst = None)

// --- what nobody may lose track of ------------------------------------------------------------
//
// Cards are conserved. A deck of eighteen is eighteen cards for the whole game, wherever they
// are - which is the one invariant this game has before the rest of its rules are written, and
// the one that any rule added later could break without anybody noticing.

let private accounted session =
    Session.seats
    |> List.map (fun seat ->
        let side = Session.side seat session

        let onTheTable =
            Lines.all |> List.collect (fun line -> Side.stack line side)

        List.length side.Deck + List.length side.Hand + List.length side.Discard + List.length onTheTable)

report
    "eighteen cards each, wherever they are"
    [ 18; 18 ]
    (accounted (standing (opened 1UL)))

report
    "and still eighteen each after six cards have been played"
    [ 18; 18 ]
    (let model =
        [ 1..6 ]
        |> List.fold
            (fun model line ->
                let seat = rules.Active(standing model)
                let card = handOf seat model |> List.head
                Update.update rules (Make(Play(card, (line % Lines.Count) + 1))) model)
            (opened 1UL)

     accounted (standing model))

report
    "no card is in two places at once"
    [ true; true ]
    (let model =
        [ 1..6 ]
        |> List.fold
            (fun model line ->
                let seat = rules.Active(standing model)
                let card = handOf seat model |> List.head
                Update.update rules (Make(Play(card, (line % Lines.Count) + 1))) model)
            (opened 1UL)

     Session.seats
     |> List.map (fun seat ->
         let side = Session.side seat (standing model)

         let everywhere =
             side.Deck
             @ side.Hand
             @ side.Discard
             @ (Lines.all |> List.collect (fun line -> Side.stack line side))

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
     let model = Update.update rules (Make(Play(firstCard model, 1))) model
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
       Arrange [ Water; Dark; Fire ]
       Play({ Protocol = Fire; Value = 3 }, 2)
       Play({ Protocol = Gravity; Value = 0 }, 1)
       Resign ]
     |> List.filter (roundTrips >> not)
     |> List.map (compiled.Write << Make))

report
    "the short forms mean the same as the long ones"
    [ true; true; true ]
    ([ "fire", "draft fire"
       "water dark fire", "arrange water dark fire"
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
    (let told = Turn.asked (Play({ Protocol = Fire; Value = 1 }, 1)) (standing (dealt 1UL)) |> snd
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
     let after = Update.update rules (Make(Play(firstCard model, 1))) model
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

report
    "the machine plays a whole game legally, from the draft to an empty hand"
    (true, ThePlay)
    (let rec walk model rivals count =
        if count > 60 then
            model
        else
            let next, rivals = Machines.answering rules Playable.plays rivals model

            if Timeline.movesMade next.Timeline = Timeline.movesMade model.Timeline then
                next
            else
                walk next rivals (count + 1)

     let start = dealt 4UL

     let rivals =
         compiled.Seating 4UL [ Some "easy"; Some "easy" ] (standing start)

     let finished = walk start rivals 0
     let session = standing finished

     // Nothing was refused: every entry in the record moved the game on.
     let refused =
         Journal.entries finished.Journal
         |> List.collect (fun entry -> entry.Told)
         |> List.exists (function
             | Said(Refused _) -> true
             | _ -> false)

     not refused, Session.doing session)

report
    "a machine's game replays from its own record"
    true
    (let rec walk model rivals count =
        if count > 60 then
            model
        else
            let next, rivals = Machines.answering rules Playable.plays rivals model

            if Timeline.movesMade next.Timeline = Timeline.movesMade model.Timeline then
                next
            else
                walk next rivals (count + 1)

     let start = dealt 4UL
     let rivals = compiled.Seating 4UL [ Some "easy"; Some "easy" ] (standing start)
     let finished = walk start rivals 0
     let asked = Journal.entries finished.Journal |> List.map (fun entry -> entry.Asked)

     match Update.replay rules Session.Seats 4UL asked with
     | Ok again -> standing again = standing finished
     | Error _ -> false)

// --- the screens --------------------------------------------------------------------------------
//
// The one thing this game hides is a hand, and hiding it is the board's business rather than
// the notices': nothing the game *says* is a secret, and what a player holds is never said.

let private drawn view seat model = view.Board true seat model

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
    "what is on the table is on both screens"
    true
    (let model = opened 1UL
     let card = firstCard model
     let after = Update.update rules (Make(Play(card, 2))) model

     [ one; two ]
     |> List.forall (fun seat -> mentions (Card.name card) (drawn plain seat after)))

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
     let after = Update.update rules (Make(Play(card, 3))) model

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
