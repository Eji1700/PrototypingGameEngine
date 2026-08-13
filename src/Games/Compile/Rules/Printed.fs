namespace TCModel.Compile

/// What is printed on each of the ninety cards.
///
/// **Almost all of it is still blank, and that is the state of the work rather than an
/// oversight.** The machinery that resolves card text went in before the text did, and what is
/// written here is six cards chosen to exercise every shape that machinery has: a command that
/// asks its own player, one that stops the game on the other, one that asks twice, a rule change
/// that lasts while a card is uncovered, a command that fires at the end of every turn, and one
/// that undoes a card taken off somebody's deck.
///
/// The shape the rest will take is one module per protocol, six lines each - fifteen files of six
/// being reviewable where one file of ninety is not. Adding a card is a line in one file: no
/// change to the interpreter, and `Faults` and `Words` pick it up without being told. That is the
/// test this file exists to pass, and the six below are the evidence it does.
module Printed =

    let blank: Text =
        { Top = []
          Shown = []
          Bottom = []
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

    let private atEnd commands = { blank with AtEnd = commands }

    /// The worked example the pile was designed around: *flip a card, then draw a card.*
    ///
    /// Two commands rather than one sentence, so the game looks at the table between them - and
    /// if the flip turned something face up, that card's own text resolves before the draw.
    let private fireThree = shown [ Flip(Select.any |> Select.faceDown); Draw 1 ]

    /// *Your opponent discards a card.* The case the pile was really built for: the game stops on
    /// somebody whose turn it is not, and nothing moves until they answer.
    let private waterZero = shown [ Opposing Discard ]

    /// *Move one of your cards to another line.* The only command that asks twice - which card,
    /// and then where - because there is no line it obviously goes to.
    let private darknessOne = shown [ Shift(Select.any |> Select.yours) ]

    /// *This card counts as nothing.* A rule change rather than an event: it is asked every time
    /// the stack under it is added up, and it stops the moment something covers this card.
    ///
    /// Printed on the five deliberately. A "counts as nothing" written on a card already worth
    /// nothing says nothing, and a card that cannot be told from a blank one is no use as the
    /// only example of its kind.
    let private metalFive = standing [ CountsAs 0 ]

    /// *Return one of your opponent's cards to their hand.* Off the table without going to a
    /// discard - which is worse for them than a deletion in one way and better in another.
    let private waterTwo = shown [ Return(Select.any |> Select.theirs) ]

    /// *This card cannot be deleted, and draws a card at the end of your turn.* Both halves of a
    /// card that keeps working: one that every deletion has to ask about, and one that fires
    /// again every turn it survives.
    let private lightFive =
        { blank with
            Bottom = [ Unbreakable ]
            AtEnd = [ Draw 1 ] }

    /// *Give a card back to whoever it belongs to.* What undoes a card taken off the top of a
    /// deck by a second compile - and the only place the difference between holding a card and
    /// its being yours is worth anything.
    let private gravityTwo = shown [ Rehome Select.any ]

    /// **Fire-1, as it is really printed:** *"Discard 1 card. If you do, delete 1 card."*
    ///
    /// The first card in from the real ninety, and it is here for what it settles rather than
    /// for what it does: with an empty hand the discard cannot happen, and then the delete does
    /// not happen either. A command that could not be carried out did not happen, and everything
    /// waiting behind an *if you do* reads that.
    let private fireOne = shown [ IfYouDo(Discard, [ Delete Select.any ]) ]

    /// **Death-1, as it is really printed:** *"Start: You may draw 1 card. If you do, delete 1
    /// other card, then delete this card."*
    ///
    /// The other shape, and the sharper one. Decline the draw and **nothing** follows - not the
    /// other deletion and not this card's own - so the card sits on the table being an offer its
    /// owner can take whenever it suits them. Reading `then` as sequencing outside the condition
    /// would make it a card that deletes itself the moment you say no, which is a different card.
    ///
    /// Its trigger is `Start:`, which is not built. It is written in the shown box until it is.
    let private deathOne =
        shown [ IfYouDo(May(Draw 1), [ Delete Select.any; Delete(Select.any |> Select.here) ]) ]

    /// **Apathy-2, half of it as it is really printed:** *"When this card would be covered:
    /// First, flip this card."*
    ///
    /// The interrupt, and the only card that has one so far. Play something over it and it turns
    /// itself face down before the covering card lands - so what lands on top of it lands on a
    /// two rather than on whatever was printed, and the card underneath has stopped saying
    /// anything at all.
    ///
    /// Its top box - *"Ignore all middle commands of cards in this line"* - is not written,
    /// because nothing can express it yet.
    let private apathyTwo =
        { blank with
            WhenCovered = [ Flip(Select.any |> Select.here |> Select.faceUp |> Select.uncovered) ] }

    /// What a card says. Everything not named here says nothing, which at the moment is
    /// eighty of them.
    let on card =
        match card.Protocol, card.Value with
        | Apathy, 2 -> apathyTwo
        | Fire, 1 -> fireOne
        | Fire, 3 -> fireThree
        | Death, 1 -> deathOne
        | Water, 0 -> waterZero
        | Water, 2 -> waterTwo
        | Darkness, 1 -> darknessOne
        | Metal, 5 -> metalFive
        | Light, 5 -> lightFive
        | Gravity, 2 -> gravityTwo
        | _ -> blank

    /// Whether a card says anything at all - for a screen deciding whether to draw a line of
    /// text under it, and for anything counting how much of this file is still empty.
    let says card = on card <> blank

    /// What a card does while it is face up and uncovered, which is nothing unless it is.
    /// What a card says while it is standing on the table, which depends on whether anything
    /// is on top of it: the top box goes on applying, and the bottom box does not.
    let ongoing uncovered card =
        let text = on card
        if uncovered then text.Top @ text.Bottom else text.Top

    /// How many of the ninety are written, for the game to say so plainly rather than let
    /// somebody discover it a card at a time.
    let written =
        Protocol.all |> List.collect Card.inProtocol |> List.filter says |> List.length
