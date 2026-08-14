namespace TCModel.Compile

/// What is printed on each of the ninety cards.
///
/// **All ninety are written, and every one of them says the whole of what the real card says.**
/// Nothing here is a placeholder and nothing is half-built; where a card was short of its printed
/// text it stayed marked until it was not.
///
/// This file is now the only copy of the ninety in the repository. The text they were transcribed
/// from was a `Cards.js` sitting beside it, kept until the count reached ninety and removed in the
/// commit that got there - because two copies of the same ninety cards are two things that can
/// disagree, and only one of them is the one the game plays.
///
/// Adding a card is a line in one section: no change to the interpreter, and `Faults` and `Words`
/// pick it up without being told. That is the test this file exists to pass, and the ninety below
/// are the evidence it does.
///
/// **Nothing here was invented.** An earlier draft of this file carried a handful of cards written
/// to exercise the machinery - a card that counts as nothing, a card nothing may delete, a command
/// that sends a card home. None of the three turned out to be printed on any of the ninety, and
/// all three came out with the vocabulary behind them. A rule with no card is a rule this game
/// does not have, and it reads exactly like one it does.
module Printed =

    let blank: Text =
        { Top = []
          After = []
          WhenFlipped = []
          WhenCompiled = []
          Shown = []
          Bottom = []
          AtStart = []
          AtEnd = []
          WhenCovered = [] }

    /// A standing rule in the top box - one that goes on applying after something is played over
    /// this card.
    let private standing rules = { blank with Top = rules }

    /// The middle box: what this card does when it is shown, which is when it is played face up,
    /// when it is flipped face up, and every time it is uncovered again.
    let private shown commands = { blank with Shown = commands }

    /// A standing rule in the bottom box - silenced the moment anything covers this card.
    let private whileClear rules = { blank with Bottom = rules }

    /// *"Start: ..."* - the first thing a turn does, before the component and before compiling.
    let private atStart commands = { blank with AtStart = commands }

    /// *"After you draw cards: ..."* - a top-box trigger, which goes on listening covered.
    let private after trigger commands = { blank with After = [ trigger, commands ] }

    let private atEnd commands = { blank with AtEnd = commands }

    /// *"When this card would be covered: First, ..."* - an interrupt, which resolves before the
    /// covering card lands rather than after it.
    let private whenCovered commands = { blank with WhenCovered = commands }

    /// **The 5 of every protocol:** *"You discard 1 card."*
    ///
    /// Fifteen identical cards, and the shape of the whole game in one line: a five is the biggest
    /// number in the deck and it costs a card out of hand to put down face up. The alternative is
    /// playing it face down for two and saying nothing - which is the choice every hand in Compile
    /// comes down to.
    let private theFive = shown [ Discard ]

    // --- Apathy -----------------------------------------------------------------------------
    //
    // The protocol that wants nothing to be readable: it turns cards over, and it pays you for
    // the ones lying face down.

    /// *"Your total value in this line is increased by 1 for each face-down card in this line."*
    ///
    /// The card that makes playing face down worth something in itself, and the only standing
    /// rule whose number is counted off the board rather than printed.
    let private apathyZero = standing [ LinePlusPerFaceDown 1 ]

    /// *"Flip all other face-up cards in this line."*
    ///
    /// Every card it reaches, and nobody asked - there is no choice to be made when the answer is
    /// all of them. *Other* means every one but this card, which would otherwise turn itself face
    /// down and stop being the card that did it.
    let private apathyOne =
        shown [ Every(Flip(Select.any |> Select.here |> Select.faceUp |> Select.other)) ]

    /// *"Ignore all middle commands of cards in this line. When this card would be covered:
    /// First, flip this card."*
    ///
    /// The interrupt: play something over it and it turns itself face down before the covering
    /// card lands, so what lands on top lands on a two rather than on whatever was printed.
    ///
    /// And `Silence`, which is the only rule in the game that **subtracts**. Everything else adds
    /// something; this takes every card in the line its voice away, both sides of it, while
    /// leaving them standing and counting exactly as before.
    let private apathyTwo =
        { standing [ Silence ] with
            WhenCovered = [ Flip(Select.any |> Select.this') ] }

    /// *"Flip 1 of your opponent's face-up cards."*
    let private apathyThree = shown [ Flip(Select.any |> Select.theirs |> Select.faceUp) ]

    /// *"You may flip 1 of your face-up covered cards."*
    ///
    /// **Covered**, which is the half of a stack whose text is silent - so this is the card that
    /// reaches down and turns a buried five back into a two, or an early card back face up to
    /// count. Worth nothing at all in a stack one deep, which is why it is offered rather than
    /// forced.
    let private apathyFour =
        shown [ May(Flip(Select.any |> Select.yours |> Select.faceUp |> Select.covered)) ]

    // --- Darkness ---------------------------------------------------------------------------
    //
    // Draws deep and moves what nobody can read.

    /// *"Draw 3 cards. Shift 1 of your opponent's covered cards."*
    ///
    /// The biggest draw in the game, and it pulls a card out from *under* something of theirs -
    /// which changes two lines at once and leaves whatever was on top of it sitting one lower.
    let private darknessZero =
        shown [ Draw(Just 3); Shift(Select.any |> Select.theirs |> Select.covered, AnyLine) ]

    /// *"All face-down cards in this stack have a value of 4. You may flip 1 covered card in this
    /// line."*
    ///
    /// Two rather than four is the usual, so the top box doubles what a face-down card is worth in
    /// the stack it is sitting in - which is what makes Darkness a protocol that would rather you
    /// could not read it.
    ///
    /// A top box and a middle box at once, which no card had before this one.
    let private darknessTwo =
        { standing [ FaceDownWorth 4 ] with
            Shown = [ May(Flip(Select.any |> Select.here |> Select.covered)) ] }

    /// *"Flip 1 of your opponent's cards. You may shift that card."*
    ///
    /// The first card that points a command at what the command before it landed on. **That card**
    /// narrows rather than names, so a card that has left the table in between - deleted by its own
    /// text on being turned face up, say - is simply not among the targets, and the offer is not
    /// made at all.
    let private darknessOne =
        shown
            [ Flip(Select.any |> Select.theirs)
              May(Shift(Select.any |> Select.thatCard, AnyLine)) ]

    /// *"Play 1 card face-down in another line."*
    ///
    /// A card out of the hand that does not cost the turn, which is the whole value of it - the
    /// action you were going to take is still yours. Face down and somewhere else, so it is two
    /// each turn into a line you were not building.
    let private darknessThree = shown [ PlayFromHand(FaceDown, OtherLines) ]

    /// *"Shift 1 face-down card."*
    let private darknessFour = shown [ Shift(Select.any |> Select.faceDown, AnyLine) ]

    // --- Death ------------------------------------------------------------------------------
    //
    // Deletes, and mostly deletes something specific.

    /// *"Draw 1 card. Delete all cards in 1 other line with 8 or more cards."*
    ///
    /// The only card that counts what a line **holds** rather than what it is worth: eight twos and
    /// eight fives are the same line to this. Eight is a great many, so it is a card for the game
    /// where somebody has been feeding a line all afternoon and cannot stop.
    let private metalThree =
        shown [ Draw(Just 1); InAChosenLineOf(8, Every(Delete(Select.any |> Select.here))) ]

    /// *"Delete 1 card from each other line."*
    let private deathZero = shown [ InEachOtherLine(Delete(Select.any |> Select.here)) ]

    /// *"Delete all cards in 1 line with values of 1 or 2."*
    let private deathTwo =
        shown [ InAChosenLine(Every(Delete(Select.any |> Select.here |> Select.worth [ 1; 2 ]))) ]

    /// *"Start: You may draw 1 card. If you do, delete 1 other card, then delete this card."*
    ///
    /// Decline the draw and **nothing** follows - not the other deletion and not this card's own -
    /// so the card sits on the table being an offer its owner can take whenever it suits them.
    /// Reading `then` as sequencing outside the condition would make it a card that deletes itself
    /// the moment you say no, which is a different card.
    ///
    /// In the start box, so the offer comes round at the top of every turn this card survives -
    /// and it comes round **before** the component is taken and before anything compiles, which
    /// is the whole use of it: the card it deletes is a card that was going to count.
    let private deathOne =
        atStart
            [ IfYouDo(
                  May(Draw(Just 1)),
                  [ Delete(Select.any |> Select.other); Delete(Select.any |> Select.this') ]
              ) ]

    /// *"Delete 1 face-down card."*
    let private deathThree = shown [ Delete(Select.any |> Select.faceDown) ]

    /// *"Delete a card with a value of 0 or 1."*
    ///
    /// A card picked by what it is worth **on the table** rather than by what is printed on it -
    /// so a face-down five is a two and out of reach, and a face-down card in a Darkness stack
    /// worth four is further out of reach still.
    let private deathFour = shown [ Delete(Select.any |> Select.worth [ 0; 1 ]) ]

    // --- Fire -------------------------------------------------------------------------------
    //
    // Spends the hand. Every card of it but one turns a card in hand into something on the table.

    /// *"Flip 1 other card. Draw 2 cards. When this card would be covered: First, draw 1 card and
    /// flip 1 other card."*
    ///
    /// The worked example the pile was designed around, and the real card it was drawn from: two
    /// commands rather than one sentence, so the game looks at the table between them - and if the
    /// flip turned something face up, that card's own text resolves before the draw.
    ///
    /// The bottom box is the same pair backwards, as an interrupt. Covering your own Fire-0 is a
    /// card and a flip, which is why it is worth building on rather than leaving alone.
    let private fireZero =
        { shown [ Flip(Select.any |> Select.other); Draw(Just 2) ] with
            WhenCovered = [ Draw(Just 1); Flip(Select.any |> Select.other) ] }

    /// *"Discard 1 card. If you do, delete 1 card."*
    ///
    /// Here for what it settles rather than for what it does: with an empty hand the discard
    /// cannot happen, and then the delete does not happen either. A command that could not be
    /// carried out did not happen, and everything waiting behind an *if you do* reads that.
    let private fireOne = shown [ IfYouDo(Discard, [ Delete Select.any ]) ]

    /// *"Discard 1 card. If you do, return 1 card."*
    let private fireTwo = shown [ IfYouDo(Discard, [ Return Select.any ]) ]

    /// *"End: You may discard 1 card. If you do, flip 1 card."*
    ///
    /// The offer and the condition in the same box, at the end of every turn this card survives
    /// uncovered - so it is a card you keep until the turn a flip is worth a card, and then it is
    /// still there next turn.
    let private fireThree = atEnd [ IfYouDo(May Discard, [ Flip Select.any ]) ]

    /// *"Discard 1 or more cards. Draw the amount discarded plus 1."*
    ///
    /// The only question in the game with **no fixed size**: one discard is forced, and then it is
    /// offered again for as long as there is a hand left and the player keeps saying yes. How many
    /// were done is left where the command after it can read it - which is why what the game
    /// remembers about the last command is a number rather than a yes.
    ///
    /// Wrapped in an *if you do*, so an empty hand does none of it: nought is not one or more.
    let private fireFour = shown [ IfYouDo(OneOrMore Discard, [ Draw(HowManyPlus 1) ]) ]

    // --- Gravity ----------------------------------------------------------------------------
    //
    // Pulls cards towards it, and the one card of it that is written pulls them off the deck.

    /// *"For every 2 cards in this line, play the top card of your deck face-down under this
    /// card."*
    ///
    /// Two shapes at once. A count read off the board rather than printed, and the only way a card
    /// arrives at the **bottom** of a stack - so it covers nothing, sets off no interrupt, and is
    /// covered by everything already there.
    let private gravityZero =
        shown [ Times(PerCards(2, Select.any |> Select.here), UnderThis FaceDown) ]

    /// *"Draw 2 cards. Shift 1 card either to or from this line."*
    ///
    /// The only card that constrains **both ends** of a shift: out of this line to anywhere, or in
    /// from anywhere to here. Never both, because a card already standing here cannot be shifted
    /// into it - so which half of the sentence applies is settled by where the card you point at
    /// happens to be.
    let private gravityOne =
        shown [ Draw(Just 2); Shift(Select.any, ToOrFromHere) ]

    /// *"Flip 1 card. Shift that card to this line."*
    ///
    /// Both halves of Gravity in one card: turn something over, and then drag the thing you turned
    /// over into the line this is standing in. Neither half asks anything the other did not already
    /// settle, so on a board with one card it is two commands and no questions at all.
    let private gravityTwo =
        shown [ Flip Select.any; Shift(Select.any |> Select.thatCard, ThisLine) ]

    /// *"Shift 1 face-down card to this line."*
    ///
    /// Gravity pulling: the card comes here, and there is nothing to ask because the card said
    /// where. Which is also the point of it - a shift that asks is a shift the player aims, and
    /// this one always aims at the line the Gravity is standing in.
    let private gravityFour = shown [ Shift(Select.any |> Select.faceDown, ThisLine) ]

    /// *"Your opponent plays the top card of their deck face-down in this line."*
    ///
    /// Gravity in one line: it makes *them* put something down, in the line it chooses, off a deck
    /// neither of you has seen. The command is the ordinary deck play with the actor swapped, so
    /// *their* deck and *their* side of the line follow from the swap rather than from the card.
    let private gravitySix = shown [ Opposing(FromDeck(FaceDown, ThisLine)) ]

    // --- Hate -------------------------------------------------------------------------------
    //
    // Deletes without caring whose, and one card of it deletes yours first.

    /// *"Delete 1 card."* The plainest card in the game.
    let private hateZero = shown [ Delete Select.any ]

    /// *"Discard 3 cards. Delete 1 card. Delete 1 card."*
    ///
    /// Three out of hand for two off the table, and the discard is not conditional: a hand of one
    /// discards the one it has, and both deletions still happen. Which makes it a card you play
    /// out of a full hand or not at all.
    let private hateOne =
        shown [ Times(Just 3, Discard); Delete Select.any; Delete Select.any ]

    /// *"Delete your highest value card. Delete your opponent's highest value card."*
    ///
    /// Two commands, and the table is looked at between them - so the second one picks the highest
    /// of what is left rather than the highest of what there was. Everything tied for highest
    /// survives the narrowing, so two fives still ask which.
    let private hateTwo =
        shown
            [ Delete(Select.any |> Select.yours |> Select.highest)
              Delete(Select.any |> Select.theirs |> Select.highest) ]

    /// *"After you delete cards: Draw 1 card."*
    ///
    /// Hate paid for: every card your text takes off the table buys one back into your hand. A
    /// line wiped by compiling is not a deletion anybody did, so this stays quiet through it - and
    /// it goes on listening while covered, because the trigger is printed in the top box.
    let private hateThree = after YouDelete [ Draw(Just 1) ]

    /// *"When this card would be covered: First, delete the lowest value covered card in this
    /// line."*
    ///
    /// An interrupt that eats the stack it is standing in, from the bottom. **Covered** and
    /// **lowest** at once, so it never reaches itself and never reaches whatever is arriving.
    let private hateFour =
        whenCovered [ Delete(Select.any |> Select.here |> Select.covered |> Select.lowest) ]

    // --- Life -------------------------------------------------------------------------------
    //
    // Flips, draws, and puts cards down off the deck.

    /// *"Play the top card of your deck face-down in each line where you have a card. When this
    /// card would be covered: First, delete this card."*
    ///
    /// Life spreading: one card off the deck into every line you are already standing in - so it
    /// pays most when you are everywhere, and nothing at all when this is your first card down.
    ///
    /// And it will not be built on. Cover it and it deletes itself first, so the card you played
    /// lands on whatever was underneath - which makes a Life-0 a line you fill and then leave.
    let private lifeZero =
        { shown [ InEachLineHolding(FromDeck(FaceDown, ThisLine)) ] with
            WhenCovered = [ Delete(Select.any |> Select.this') ] }

    /// *"Flip 1 card. Flip 1 card."*
    ///
    /// The same command twice rather than "flip 2 cards", which is the reading that puts a
    /// look-at-the-table between them: if the first flip turned something face up, that card
    /// resolves before the second flip is asked.
    let private lifeOne = shown [ Flip Select.any; Flip Select.any ]

    /// *"Draw 1 card. You may flip 1 face-down card."*
    let private lifeTwo = shown [ Draw(Just 1); May(Flip(Select.any |> Select.faceDown)) ]

    /// *"If this card is covering a card, draw 1 card."*
    ///
    /// The first card that asks a question about **the board** rather than about what a command
    /// just did - and about itself, which no selector could answer. A four is worth building on
    /// top of something, and this is the card that says so out loud: play it on an empty line and
    /// it is a four and nothing else.
    let private lifeFour = shown [ IfCovering [ Draw(Just 1) ] ]

    /// *"When this card would be covered: First, play the top card of your deck face-down in
    /// another line."*
    ///
    /// The interrupt and the deck play in one card - and it lands in a line the player chooses, so
    /// covering a Life-3 stops the game to ask them where.
    let private lifeThree = whenCovered [ FromDeck(FaceDown, OtherLines) ]

    // --- Light ------------------------------------------------------------------------------
    //
    // Draws, and shows what is hidden.

    /// *"Flip 1 card. Draw cards equal to that card's value."*
    ///
    /// The first card that looks **back** rather than at the board: *that card* is whatever the
    /// flip landed on, which is the only reason a session remembers what a command chose.
    ///
    /// **Which value is a real ambiguity**, and this reads it as the one *printed on the card*
    /// rather than what it is worth lying where it is. The two agree except on a card left face
    /// down, where the printed reading draws its number and the other draws two. Printed is the
    /// safer of the two - it does not depend on where the card ended up, or on whether it is still
    /// there at all - and it is a line to change if the table says otherwise.
    let private lightZero = shown [ Flip Select.any; Draw WorthOfChosen ]

    /// *"End: Draw 1 card."*
    ///
    /// A card every turn for as long as it is standing uncovered, which is the cheapest thing in
    /// the game and the reason a line with a Light-1 on top is one the opponent has to answer.
    let private lightOne = atEnd [ Draw(Just 1) ]

    /// *"Draw 2 cards. Reveal 1 face-down card. You may shift or flip that card."*
    ///
    /// Three of the newer shapes in one sentence: a card on the table shown without moving, the
    /// next command pointed at **that card**, and a choice between two things to do with it that
    /// you may also decline. `May(Either(...))` is the card that offers all three answers, and
    /// this is one of the two that says so.
    let private lightTwo =
        shown
            [ Draw(Just 2)
              Show(Select.any |> Select.faceDown)
              May(
                  Either(
                      Shift(Select.any |> Select.thatCard, AnyLine),
                      Flip(Select.any |> Select.thatCard)
                  )
              ) ]

    /// *"Shift all face-down cards in this line to another line."*
    ///
    /// Light clearing out what cannot be read. **Where each one goes is asked separately**, which
    /// is a reading rather than a certainty: *another line* could mean one destination for the
    /// whole lot. Asked per card is the looser of the two and the one that never has to refuse an
    /// answer, and it is a line to change if the table says otherwise.
    let private lightThree =
        shown [ Every(Shift(Select.any |> Select.here |> Select.faceDown, OtherLines)) ]

    /// *"Your opponent reveals their hand."*
    ///
    /// A reveal changes **nothing on the table**. What it leaves behind is knowledge, and
    /// knowledge at this table is the log - which both players read, and which is exactly what a
    /// reveal at a real table leaves behind too. So it needs no state, and the notice is public on
    /// purpose: a reveal only one seat could read would not be a reveal.
    let private lightFour = shown [ RevealTheirHand ]

    // --- Love -------------------------------------------------------------------------------
    //
    // Gives things away, and is paid for it.

    /// *"Draw the top card of your opponent's deck. End: You may give 1 card from your hand to
    /// your opponent. If you do, draw 2 cards."*
    ///
    /// Love taking and Love giving, on one card. The middle box is the second compile's steal said
    /// as a command; the end box hands a card back and pays two for it. A card of theirs is worth
    /// having on both counts - it is one they were counting on, and it is one you may give away
    /// again at a profit.
    ///
    /// Every piece of the end box was built for something else: the box, the offer, the condition
    /// behind it, and the giving. That half of the card is one line.
    let private loveOne =
        { shown [ TakeTheirTop ] with
            AtEnd = [ IfYouDo(May Give, [ Draw(Just 2) ]) ] }

    /// *"Your opponent draws 1 card. Refresh."*
    ///
    /// A card for them and five for you, at the cost of everything you were holding. The refresh
    /// as an **effect** rather than as the turn's action, which is the whole difference: this one
    /// happens in the middle of a turn that has already been spent.
    let private loveTwo = shown [ Opposing(Draw(Just 1)); Refreshing' ]

    /// *"Take 1 random card from your opponent's hand. Give 1 card from your hand to your
    /// opponent."*
    ///
    /// Taken at random and given by choice, which is the whole joke of the card - and the only
    /// place the generator is asked for anything after the deal.
    let private loveThree = shown [ TakeAtRandom; Give ]

    /// *"Reveal 1 card from your hand. Flip 1 card."*
    let private loveFour = shown [ Reveal; Flip Select.any ]

    /// *"Your opponent draws 2 cards."*
    ///
    /// The whole card, and the six that pays for Love having no nought. Two cards for them and
    /// nothing at all for you - which is a six on the table, and a six is most of a compile.
    let private loveSix = shown [ Opposing(Draw(Just 2)) ]

    // --- Metal ------------------------------------------------------------------------------
    //
    // Shuts doors. Three of its four written cards are standing rules rather than commands.

    /// *"Your opponent's total value in this line is reduced by 2. Flip 1 card."*
    ///
    /// The only standing rule that reaches across the table, and the reason a line's total cannot
    /// be worked out from one side of it.
    let private metalZero =
        { standing [ TheirLineMinus 2 ] with
            Shown = [ Flip Select.any ] }

    /// *"Draw 2 cards. Your opponent cannot compile next turn."*
    ///
    /// The only command whose effect outlives the turn it was made on - and therefore the only
    /// thing in this game that has to be *remembered* rather than read off the board. Every other
    /// standing rule is a card lying face up somewhere and stops when that card does.
    let private metalOne = shown [ Draw(Just 2); StopTheirCompile ]

    /// *"Your opponent cannot play cards face-down in this line."*
    ///
    /// In the top box, so it goes on holding whatever is built over it. It leaves them only a card
    /// whose protocol faces this line, which on most lines is a real bar and on one is none at all.
    let private metalTwo = standing [ TheyCannotPlayFaceDownHere ]

    /// *"When this card would be covered or flipped: First, delete this card."*
    ///
    /// A six that will not be built on and will not be turned over: touch it either way and it
    /// takes itself off the table rather than let you. Six is most of a compile on its own, so
    /// what it really says is *this line is worth six to me and there is nothing you can do about
    /// it except delete it outright.*
    ///
    /// **Both halves**, and they are two boxes saying the same thing rather than one box with two
    /// triggers - because covering and flipping fire from two different places in the rules, and a
    /// card that could be reached from only one of them would be a card you could still turn over.
    let private metalSix =
        { whenCovered [ Delete(Select.any |> Select.this') ] with
            WhenFlipped = [ Delete(Select.any |> Select.this') ] }

    // --- Plague -----------------------------------------------------------------------------
    //
    // Empties the other hand, and shuts one line while it does.

    /// *"Your opponent discards 1 card. Your opponent cannot play cards in this line."*
    ///
    /// The case the pile was really built for: the game stops on somebody whose turn it is not,
    /// and nothing moves until they answer.
    ///
    /// Its standing rule is in the **bottom** box, so covering it opens the line again - which
    /// makes a Plague-0 a line you shut and then have to leave alone.
    let private plagueZero =
        { whileClear [ TheyCannotPlayHere ] with
            Shown = [ Opposing Discard ] }

    /// *"After your opponent discards cards: Draw 1 card. Your opponent discards 1 card."*
    ///
    /// Plague reading its own weather: the middle box makes them discard, and the top box draws
    /// you a card for it - and for every other discard they make afterwards, for as long as this
    /// is face up. Two Plagues on the table and a hand of theirs goes very quickly.
    let private plagueOne =
        { after TheyDiscard [ Draw(Just 1) ] with
            Shown = [ Opposing Discard ] }

    /// *"Discard 1 or more cards. Your opponent discards the amount of cards discarded plus 1."*
    ///
    /// Fire-4's shape pointed the other way: the same open-ended question, and the tally it leaves
    /// behind is spent on them rather than on you.
    let private plagueTwo =
        shown [ IfYouDo(OneOrMore Discard, [ Opposing(Times(HowManyPlus 1, Discard)) ]) ]

    /// *"End: Your opponent deletes 1 of their face-down cards. You may flip this card."*
    ///
    /// The deletion is theirs to make and theirs to lose, and the offer after it is not conditional
    /// on it: turning this card face down is a thing its owner may do whenever they have had
    /// enough of it, which is what stops the card being a thing you dare not stand next to.
    let private plagueFour =
        atEnd
            [ Opposing(Delete(Select.any |> Select.yours |> Select.faceDown))
              May(Flip(Select.any |> Select.this')) ]

    /// *"Flip each other face-up card."*
    ///
    /// Every face-up card on the table, both sides of it, and this one left alone. The widest
    /// command in the game.
    let private plagueThree =
        shown [ Every(Flip(Select.any |> Select.faceUp |> Select.other)) ]

    // --- Psychic ----------------------------------------------------------------------------
    //
    // Takes the other hand apart and looks at what is left.

    /// *"Draw 2 cards. Your opponent discards 2 cards, then reveals their hand."*
    ///
    /// Two discards is the command twice, so they choose the second knowing what the first cost
    /// them - and then they show what they kept.
    let private psychicZero =
        shown [ Draw(Just 2); Opposing(Times(Just 2, Discard)); RevealTheirHand ]

    /// *"Your opponent can only play cards face-down. Start: Flip this card."*
    ///
    /// The widest bar in the game - not a line but the whole table - and the reason `Field.barred`
    /// is asked about a play rather than about a line. And it lasts exactly one of their turns:
    /// the start box turns the card face down at the top of your next one, so what it really buys
    /// is a single turn in which they cannot answer you.
    let private psychicOne =
        { standing [ TheyMustPlayFaceDown ] with
            AtStart = [ Flip(Select.any |> Select.this') ] }

    /// *"Your opponent discards 2 cards. Rearrange their protocols."*
    ///
    /// **You** do the rearranging, and it is *their* three that move - which is the one place in
    /// the game where the player being asked and the side being changed are two different seats.
    /// The stacks stay exactly where they are, so a line they had built for Metal can end up
    /// facing Spirit because you said so.
    let private psychicTwo =
        shown [ Opposing(Times(Just 2, Discard)); Rearrange Theirs ]

    /// *"Your opponent discards 1 card. Shift 1 of their cards."*
    let private psychicThree = shown [ Opposing Discard; Shift(Select.any |> Select.theirs, AnyLine) ]

    /// *"End: You may return 1 of your opponent's cards. If you do, flip this card."*
    ///
    /// A card that spends itself: take one of theirs off the table, and this one turns face down
    /// and stops offering. Decline and it is still there next turn, which is the whole of what
    /// makes it worth the line it stands in.
    let private psychicFour =
        atEnd [ IfYouDo(May(Return(Select.any |> Select.theirs)), [ Flip(Select.any |> Select.this') ]) ]

    // --- Speed ------------------------------------------------------------------------------
    //
    // Moves cards, and two of its six are written.

    /// *"Play 1 card."*
    ///
    /// Speed at its plainest: a whole extra action, and no strings. *"Play 1 card"* names no face,
    /// which is a choice between two commands rather than a third kind of command - and `Either`
    /// already declines to offer a half nobody could carry out, so a hand with nothing that could
    /// go face up anywhere is simply asked where to put it face down.
    let private speedZero =
        shown [ Either(PlayFromHand(FaceUp, AnyLine), PlayFromHand(FaceDown, AnyLine)) ]

    /// *"After you clear cache: Draw 1 card. Draw 2 cards."*
    ///
    /// The check cache phase happens every turn, so this is a card a turn for as long as it
    /// stands - and the timing is the joke: the cache has just been checked, so the card it draws
    /// you is one nothing will make you put down again this turn.
    let private speedOne =
        { after YouClearCache [ Draw(Just 1) ] with
            Shown = [ Draw(Just 2) ] }

    /// *"When this card would be deleted by compiling: Shift this card, even if this card is
    /// covered."*
    ///
    /// The one card that survives a compile. Every other interrupt fires on something a player
    /// did; this fires on the wiping itself, which is the only thing in the game that takes cards
    /// off the table without a card asking it to - and it runs *before* the sweeping, so by the
    /// time the line goes this is standing somewhere else.
    ///
    /// It shifts itself covered and all, which is why it is a two: it is not there to be worth
    /// anything, it is there to still be there.
    let private speedTwo =
        { blank with
            WhenCompiled = [ Shift(Select.any |> Select.this', AnyLine) ] }

    /// *"Shift 1 of your other cards. End: You may shift 1 of your cards. If you do, flip this
    /// card."*
    ///
    /// The same command in two boxes, once outright and once as a spending offer - and *other* in
    /// the middle rather than in the end box, because a card cannot shift itself onto the line it
    /// is already on but may certainly turn itself over.
    let private speedThree =
        { shown [ Shift(Select.any |> Select.yours |> Select.other, AnyLine) ] with
            AtEnd =
                [ IfYouDo(May(Shift(Select.any |> Select.yours, AnyLine)), [ Flip(Select.any |> Select.this') ]) ] }

    /// *"Shift 1 of your opponent's face-down cards."*
    let private speedFour = shown [ Shift(Select.any |> Select.theirs |> Select.faceDown, AnyLine) ]

    // --- Spirit -----------------------------------------------------------------------------
    //
    // Rearranges, refreshes, and gets out of the way.

    /// *"Refresh. Draw 1 card. Skip your check cache phase."*
    ///
    /// The two halves are one idea: five up and one more makes six, and the bottom box is what
    /// lets you keep it. It is the only standing rule in the game asked by a **phase** rather than
    /// by a value or by a move - and it is in the bottom box, so covering this card hands the
    /// limit straight back.
    let private spiritZero =
        { whileClear [ SkipsCacheCheck ] with
            Shown = [ Refreshing'; Draw(Just 1) ] }

    /// *"You can play cards in any line. Draw 2 cards."*
    ///
    /// The one standing rule that opens a door rather than shutting one, and the widest thing a
    /// card can say: face up anywhere, protocol or no protocol, for as long as it is standing.
    ///
    /// And its start box - *"Either discard 1 card or flip this card"* - is the rent: a card a
    /// turn to keep the board open, and an empty hand pays it by turning the card face down
    /// instead, because a branch nobody could carry out is not on offer.
    let private spiritOne =
        { standing [ YouMayPlayAnywhere ] with
            Shown = [ Draw(Just 2) ]
            AtStart = [ Either(Discard, Flip(Select.any |> Select.this')) ] }

    /// *"You may flip 1 card."*
    let private spiritTwo = shown [ May(Flip Select.any) ]

    /// *"After you draw cards: You may shift this card, even if this card is covered."*
    ///
    /// The card that says out loud what every one of these triggers does: a top box goes on
    /// working with something built over it, and this one uses that to climb out from under.
    /// Every draw is a chance to move it, and a Spirit deck draws a great deal.
    let private spiritThree =
        after YouDraw [ May(Shift(Select.any |> Select.this', AnyLine)) ]

    /// *"Swap the positions of 2 of your protocols."*
    ///
    /// A rearrangement of three orders rather than five - only the ones a single swap can reach -
    /// which is what makes it a different card from the one the control component forces.
    let private spiritFour = shown [ Swap ]

    // --- Water ------------------------------------------------------------------------------
    //
    // Puts things back.

    /// *"Flip 1 other card. Flip this card."*
    ///
    /// Turns something over and then turns itself face down, which is a two that has already spent
    /// its text - and the only card that names **itself** in its middle box rather than in an
    /// interrupt.
    let private waterZero = shown [ Flip(Select.any |> Select.other); Flip(Select.any |> Select.this') ]

    /// *"Play the top card of your deck face-down in each other line."*
    ///
    /// A card off the top of a deck is one **neither player has seen**, which is what makes it
    /// different from every other way a card arrives - and face down it stays that way. Two lines,
    /// so it is the command twice rather than a count.
    let private waterOne = shown [ InEachOtherLine(FromDeck(FaceDown, ThisLine)) ]

    /// *"Draw 2 cards. Rearrange your protocols."*
    ///
    /// The same command pointed the other way, and the cheaper half of the pair: two cards and a
    /// free reordering of your own three. Standing pat is on offer, unlike the rearrangement the
    /// control component forces - the component says *a different order*, and this says
    /// *rearrange*.
    let private waterTwo = shown [ Draw(Just 2); Rearrange Yours ]

    /// *"Return all cards with a value of 2 in 1 line."*
    ///
    /// Which reaches every face-down card in that line as well as every printed two, because what
    /// a card is worth is read off the table.
    let private waterThree =
        shown [ InAChosenLine(Every(Return(Select.any |> Select.here |> Select.worth [ 2 ]))) ]

    /// *"Return 1 of your cards."*
    ///
    /// Off the table without going to a discard - which puts a card back in hand to be played
    /// again, and takes its value out of the line to do it.
    let private waterFour = shown [ Return(Select.any |> Select.yours) ]

    /// What a card says. Everything not named here says nothing.
    ///
    /// **Ninety cases and no fall-through worth the name.** The wildcard at the foot catches the
    /// 5 of every protocol, which is fifteen cards of one line - and after that, nothing.
    let on card =
        match card.Protocol, card.Value with
        | _, 5 -> theFive

        | Apathy, 0 -> apathyZero
        | Apathy, 1 -> apathyOne
        | Apathy, 2 -> apathyTwo
        | Apathy, 3 -> apathyThree
        | Apathy, 4 -> apathyFour

        | Darkness, 0 -> darknessZero
        | Darkness, 1 -> darknessOne
        | Darkness, 2 -> darknessTwo
        | Darkness, 3 -> darknessThree
        | Darkness, 4 -> darknessFour

        | Death, 0 -> deathZero
        | Death, 1 -> deathOne
        | Death, 2 -> deathTwo
        | Death, 3 -> deathThree
        | Death, 4 -> deathFour

        | Fire, 0 -> fireZero
        | Fire, 1 -> fireOne
        | Fire, 2 -> fireTwo
        | Fire, 3 -> fireThree
        | Fire, 4 -> fireFour

        | Gravity, 0 -> gravityZero
        | Gravity, 1 -> gravityOne
        | Gravity, 2 -> gravityTwo
        | Gravity, 4 -> gravityFour
        | Gravity, 6 -> gravitySix

        | Hate, 0 -> hateZero
        | Hate, 1 -> hateOne
        | Hate, 2 -> hateTwo
        | Hate, 3 -> hateThree
        | Hate, 4 -> hateFour

        | Life, 0 -> lifeZero
        | Life, 1 -> lifeOne
        | Life, 2 -> lifeTwo
        | Life, 3 -> lifeThree
        | Life, 4 -> lifeFour

        | Light, 0 -> lightZero
        | Light, 1 -> lightOne
        | Light, 2 -> lightTwo
        | Light, 3 -> lightThree
        | Light, 4 -> lightFour

        | Love, 1 -> loveOne
        | Love, 2 -> loveTwo
        | Love, 3 -> loveThree
        | Love, 4 -> loveFour
        | Love, 6 -> loveSix

        | Metal, 0 -> metalZero
        | Metal, 1 -> metalOne
        | Metal, 2 -> metalTwo
        | Metal, 3 -> metalThree
        | Metal, 6 -> metalSix

        | Plague, 0 -> plagueZero
        | Plague, 1 -> plagueOne
        | Plague, 2 -> plagueTwo
        | Plague, 3 -> plagueThree
        | Plague, 4 -> plagueFour

        | Psychic, 0 -> psychicZero
        | Psychic, 1 -> psychicOne
        | Psychic, 2 -> psychicTwo
        | Psychic, 3 -> psychicThree
        | Psychic, 4 -> psychicFour

        | Speed, 0 -> speedZero
        | Speed, 1 -> speedOne
        | Speed, 2 -> speedTwo
        | Speed, 3 -> speedThree
        | Speed, 4 -> speedFour

        | Spirit, 0 -> spiritZero
        | Spirit, 1 -> spiritOne
        | Spirit, 2 -> spiritTwo
        | Spirit, 3 -> spiritThree
        | Spirit, 4 -> spiritFour

        | Water, 0 -> waterZero
        | Water, 1 -> waterOne
        | Water, 2 -> waterTwo
        | Water, 3 -> waterThree
        | Water, 4 -> waterFour

        | _ -> blank

    /// Whether a card says anything at all - for a screen deciding whether to draw a line of
    /// text under it, and for anything counting how much of this file is still empty.
    let says card = on card <> blank

    /// What a card says while it is standing on the table, which depends on whether anything
    /// is on top of it: the top box goes on applying, and the bottom box does not.
    let ongoing uncovered card =
        let text = on card
        if uncovered then text.Top @ text.Bottom else text.Top

    /// How many of the ninety are written, for the game to say so plainly rather than let
    /// somebody discover it a card at a time.
    let written =
        Protocol.all |> List.collect Card.inProtocol |> List.filter says |> List.length
