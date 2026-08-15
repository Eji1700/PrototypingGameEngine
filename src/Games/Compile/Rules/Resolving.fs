namespace TCModel.Compile

open TCModel.Common
open TCModel.Engine

/// The pile, and everything that walks down it.
///
/// A card's text is a list of commands, and they resolve one at a time with a look at the table
/// between every two of them - because a command that turned a card face up has put that card's
/// own text on the pile, and it resolves before whatever was already waiting. Three properties,
/// and all three are in the types rather than in anybody's discipline:
///
///   * **Newest first.** What a command causes goes in front of what was waiting. A pile, not a
///     queue.
///   * **Look again between every two commands**, never only at the end.
///   * **Still valid, checked when it resolves**, not when it was written. A command whose
///     targets have left the table since fizzles, says so, and the pile carries on - which is
///     why "delete a card, draw a card" draws a card even when there was nothing to delete.
///
/// Compiling lives here too, because it is a thing that happens *to* a turn rather than a move
/// anybody makes, and because `BeginTurn` is a step on the same pile as everything else.
module Resolving =

    /// A backstop, not a length. Nothing in this game should resolve anywhere near this many
    /// steps in one move; a run that does is two cards flipping each other over for ever, and
    /// stopping is better than hanging. `Play` cannot fail, and this is what keeps that true.
    [<Literal>]
    let private Runaway = 500

    // --- what a selector picks out ------------------------------------------------------------

    /// Every card on the table that a selector points at, from the point of view of whoever is
    /// carrying the command out.
    ///
    /// The whole game rather than the field, for one narrowing out of ten: *"that card"* is
    /// whatever the command before this one landed on, which is the only thing a selector asks
    /// that the table cannot answer.
    let private onTable actor selector (source: Source) session =
        let field = session.Field

        let picks seat line (placed: Placed) =
            (match selector.Whose with
             | Yours -> seat = actor
             | Theirs -> seat <> actor
             | Anyone -> true)
            && (match selector.Where with
                | ThisLine -> line = source.Line
                | OtherLines -> line <> source.Line
                | AnyLine
                // A selector never reads this one - it is a rule about a destination - so anywhere.
                | ToOrFromHere -> true)
            && (match selector.Showing with
                | Some wanted -> placed.Face = wanted
                | None -> true)
            && (not selector.Uncovered
                || Stack.uncovered (Side.stack line (Field.side seat field)) = Some placed)
            && (not selector.Covered
                || Stack.uncovered (Side.stack line (Field.side seat field)) <> Some placed)
            && (not selector.NotThis || placed.Card <> source.Saying)
            && (not selector.JustThis || placed.Card = source.Saying)
            && (not selector.WasChosen || Some placed.Card = session.Chose)
            // What a card is worth *on the table*, so a face-down five is a two - which is the
            // reading a player would use, and the only one they can see.
            && (List.isEmpty selector.Worth || List.contains (Placed.value placed) selector.Worth)

        let found =
            Field.seats field
            |> List.collect (fun seat ->
                Lines.all
                |> List.collect (fun line ->
                    Side.stack line (Field.side seat field)
                    |> List.filter (picks seat line)
                    |> List.map (fun placed -> OnTable(seat, line, placed))))

        // And then narrowed to the best or worst of what is left, if the card asked for that.
        // Everything tied for it survives, so "your highest" among two fives still asks which.
        let worth =
            function
            | OnTable(_, _, placed) -> Placed.value placed
            | InHand(_, card) -> card.Value

        match selector.Pick, found with
        | Whichever, _
        | _, [] -> found
        | Highest, found ->
            let best = found |> List.map worth |> List.max
            found |> List.filter (fun target -> worth target = best)
        | Lowest, found ->
            let worst = found |> List.map worth |> List.min
            found |> List.filter (fun target -> worth target = worst)

    /// Whether that card may be laid there, that way up - the same two questions `Turn` asks of a
    /// play, asked of a card that is being played by a *card* rather than as somebody's action.
    let private mayLay actor card line face field =
        (Field.barred actor line face field |> Option.isNone)
        && (face = FaceDown || Field.allows actor card line field)

    /// Which lines a play out of the hand could go to: the ones the command allows, narrowed to
    /// those something in hand could actually be laid on.
    let private layable actor face where (source: Source) session =
        let hand = (Field.side actor session.Field).Hand

        Lines.all
        |> List.filter (fun line ->
            (match where with
             | ThisLine -> line = source.Line
             | OtherLines -> line <> source.Line
             | AnyLine
             | ToOrFromHere -> true)
            && hand |> List.exists (fun card -> mayLay actor card line face session.Field))

    /// What a command may be pointed at, right now. Empty means it has nothing to do.
    ///
    /// This is also where "still valid, checked when it resolves" actually lives: the targets are
    /// worked out at the moment the command comes off the pile, so a card that has left the table
    /// since simply is not among them.
    let rec private targets actor command source (session: Session) =
        let field = session.Field

        match command with
        | Draw _
        | Refreshing'
        | IfYouDo _
        | IfCovering _
        | FromDeck _
        | TakeAtRandom
        | InAChosenLine _
        | InAChosenLineOf _
        | InEachOtherLine _
        | InEachLineHolding _
        | StopTheirCompile
        | RevealTheirHand
        | Swap
        | Rearrange _
        | TakeTheirTop
        | UnderThis _
        | Times _
        | OneOrMore _
        | Opposing _ -> []
        // The giver chooses, out of their own hand.
        | Give
        | Reveal -> (Field.side actor field).Hand |> List.map (fun card -> InHand(actor, card))
        // What a `may` could be pointed at is what the command inside it could be pointed at,
        // which is how a card knows not to offer something impossible. An `every` is the same
        // question asked for a different reason: what it would reach, rather than what it would
        // choose between.
        | May inner
        | Every inner -> targets actor inner source session
        // What an "either" could be pointed at is what either half could, which is what makes an
        // offer of it an offer at all: a choice both halves of which are impossible is no choice.
        | Either(first, second) -> targets actor first source session @ targets actor second source session
        | Discard -> (Field.side actor field).Hand |> List.map (fun card -> InHand(actor, card))
        // Whatever in hand could be laid somewhere the command allows. With the line already
        // settled that is one line's worth; before it, it is what makes the play possible at all.
        | PlayFromHand(face, where) ->
            let lines = layable actor face where source session

            (Field.side actor field).Hand
            |> List.filter (fun card -> lines |> List.exists (fun line -> mayLay actor card line face field))
            |> List.map (fun card -> InHand(actor, card))
        | Delete selector
        | Flip selector
        | Return selector
        | Show selector
        | Shift(selector, _) -> onTable actor selector source session

    // --- doing one thing to one card ----------------------------------------------------------

    let private replacing line placed change side =
        { side with
            Stacks =
                side.Stacks
                |> Map.add line (Side.stack line side |> List.map (fun other -> if other = placed then change other else other)) }

    let private removing line placed side =
        { side with
            Stacks = side.Stacks |> Map.add line (Side.stack line side |> List.filter ((<>) placed))
            Discard = placed.Card :: side.Discard }

    /// A card about to be laid on a line.
    ///
    /// If whatever is on top of that line has something to say about being covered, it says it
    /// **first** - and the card then lands on whatever the saying left behind. A card that flips
    /// itself face down is covered face down; one that deletes itself is not covered at all,
    /// because by the time the covering card arrives it is not there.
    ///
    /// This is the only thing in the game that happens *during* a move, and it is on the pile
    /// like everything else: the interrupt goes on top of a `Placing` step, so an interrupt that
    /// stops to ask stops the card in mid-air until it is answered. Which is what an interrupt is.
    /// `from` is the line it is coming off, for a card already on the table - and it travels with
    /// the step rather than being done first, which the `Placing` case explains.
    let laying seat placed line from session =
        let interrupting =
            match Stack.uncovered (Side.stack line (Session.side seat session)) with
            | Some under when Placed.isFaceUp under ->
                (Printed.on under.Card).WhenCovered
                |> List.map (fun command ->
                    Run(
                        command,
                        { Owner = seat
                          Saying = under.Card
                          Line = line }
                    ))
            | _ -> []

        { session with
            Pile = interrupting @ (Placing(seat, placed, line, from) :: session.Pile) }

    /// A card off the line it is on and onto another - laid there the same way a play is, so a
    /// card shifted onto something with an interrupt sets that interrupt off too. Covering is
    /// covering, however the card got there.
    ///
    /// Shared because a shift arrives here two ways: with the line answered, and with the line
    /// printed on the card and nothing to answer.
    let private moving seat placed from line session =
        laying seat placed line (Some from) session, [ Happened(Shifted(seat, placed, from, line)) ]

    /// A card out of a hand and onto its owner's discard.
    ///
    /// Named because two different things do it, and only one of them is a card: a `Discard` on
    /// somebody's text, and the check cache phase, which is the rules and has no card behind it
    /// to carry a source for.
    let private discarded seat card session =
        { session with
            Field =
                session.Field
                |> Field.update seat (fun side ->
                    { side with
                        Hand = side.Hand |> List.filter ((<>) card)
                        Discard = card :: side.Discard }) },
        [ Happened(Discarded(seat, card)) ]

    /// Carry a command out on the card it was pointed at. Whose card it is is not asked for - the
    /// target already says - and the source is here only because a shift may be told where to go
    /// in terms of the line the card saying it stands in.
    let private carriedOut (source: Source) command target session =
        match command, target with
        // Held back, the way a card about to be covered is: if this one has something to say about
        // being turned over it says it first, and the turning then finds whatever the saying left
        // behind - which on the one card that does this is nothing at all, because it deleted
        // itself. Covering and flipping are the two things that happen to a card where it lies,
        // and a card may interrupt either.
        | Flip _, OnTable(seat, line, placed) ->
            let interrupting =
                (Printed.on placed.Card).WhenFlipped
                |> List.map (fun command ->
                    Run(
                        command,
                        { Owner = seat
                          Saying = placed.Card
                          Line = line }
                    ))

            { session with
                Pile = interrupting @ (Turning(seat, placed, line) :: session.Pile) },
            []

        | Delete _, OnTable(seat, line, placed) ->
            { session with
                Field = session.Field |> Field.update seat (removing line placed) },
            [ Happened(Deleted(seat, placed, line)) ]

        // Out of one hand and into the other. The only command that puts a card somewhere its
        // owner cannot see it and its holder can.
        // Shown, and put straight back. The card does not move, which is why this is the only
        // command whose whole effect is the sentence it produces.
        | Reveal, InHand(seat, card) -> session, [ Happened(Showed(seat, card)) ]

        // A card on the table shown to both players and left exactly where it was lying. It is the
        // only command that changes nothing at all, and it still counts as having been done -
        // because what it leaves behind is what the rest of the sentence points at.
        | Show _, OnTable(seat, _, placed) -> session, [ Happened(Showed(seat, placed.Card)) ]

        // Out of the hand and onto the line the command is standing in - laid the same way a play
        // is, interrupt and all, because it is a play. What it is not is the turn's action.
        | PlayFromHand(face, _), InHand(seat, card) ->
            let placed = { Card = card; Face = face }

            let session =
                { session with
                    Field =
                        session.Field
                        |> Field.update seat (fun side -> { side with Hand = side.Hand |> List.filter ((<>) card) }) }

            laying seat placed source.Line None session, []

        | Give, InHand(seat, card) ->
            let them = Session.other seat

            { session with
                Field =
                    session.Field
                    |> Field.update seat (fun side -> { side with Hand = side.Hand |> List.filter ((<>) card) })
                    |> Field.update them (fun side -> { side with Hand = side.Hand @ [ card ] }) },
            [ Happened(Gave(seat, card)) ]

        | Discard, InHand(seat, card) -> discarded seat card session

        | Return _, OnTable(seat, line, placed) ->
            { session with
                Field =
                    session.Field
                    |> Field.update seat (fun side ->
                        { side with
                            Stacks = side.Stacks |> Map.add line (Side.stack line side |> List.filter ((<>) placed))
                            Hand = side.Hand @ [ placed.Card ] }) },
            [ Happened(Returned(seat, placed, line)) ]


        // A shift is the one command that can ask twice. Which card is settled by now; where it
        // goes is a second question - unless the card said where, in which case there is nothing
        // to ask and it simply goes.
        | Shift(_, where), OnTable(seat, line, placed) ->
            let allowed =
                Lines.all
                |> List.filter ((<>) line)
                |> List.filter (fun other ->
                    match where with
                    | AnyLine -> true
                    | ThisLine -> other = source.Line
                    | OtherLines -> other <> source.Line
                    // Out of this line to anywhere, or in from anywhere to here - and never both,
                    // because a card already in this line cannot be shifted into it.
                    | ToOrFromHere -> line = source.Line || other = source.Line)

            match allowed with
            | [] -> session, [ Happened(Fizzled(seat, placed.Card)) ]
            | [ only ] -> moving seat placed line only session
            | many ->
                { session with
                    Pile =
                        Ask
                            { Chooser = seat
                              Because = ACardSaying source
                              Wanting = ALine(OnTable(seat, line, placed), many) }
                        :: session.Pile },
                [ Happened(Asked(seat, placed.Card)) ]

        // A target and a command that do not belong together cannot be built by anything above,
        // so this is unreachable. Answered rather than argued about.
        | _ -> session, []

    // --- one command --------------------------------------------------------------------------

    /// Resolve one command, asking if it needs to be asked.
    ///
    /// A command with nothing to point at fizzles and the pile carries on. A command with
    /// exactly one thing to point at does it without asking - the answer could not have been
    /// anything else, and a prompt with one button on it is a prompt that wastes somebody's
    /// time. Anything else stops and asks.
    ///
    /// Every path through here sets `Did`, which is what the *"if you do"* underneath it reads.
    /// A command that stopped to ask leaves it alone: it has not done anything *yet*, and the
    /// answer will say.
    let rec private resolve command (source: Source) session =
        let actor =
            match command with
            | Opposing _ -> Session.other source.Owner
            | _ -> source.Owner

        let nothingDone session = { session with Done = 0; Chose = None }
        let doneIt session = { session with Done = 1 }

        // What a count comes to, asked when the command carrying it resolves rather than when the
        // card was written - which is the whole point of "that card".
        let counting =
            function
            | Just n -> n
            | WorthOfChosen -> session.Chose |> Option.map (fun card -> card.Value) |> Option.defaultValue 0
            | HowManyPlus n -> session.Done + n
            | PerCards(each, selector) ->
                if each <= 0 then
                    0
                else
                    List.length (targets actor (Delete selector) source session) / each

        match command with
        | Opposing inner ->
            // The same command, done by the other player. `Yours` inside it now means theirs,
            // which falls out of `actor` rather than being said again.
            resolve inner { source with Owner = actor } session

        // "X. If you do, Y." - run X, and leave Y underneath it on a gate that will read whether
        // X did anything. It cannot be settled here: X may stop to ask, and until it is answered
        // nobody knows.
        | IfYouDo(first, rest) ->
            { session with
                Pile = Run(first, source) :: Gate(rest, source) :: session.Pile },
            []

        // "...in 1 line" - which line is the question, and the command then runs as though it had
        // been printed on a card standing there. Nothing about the command changes; its `Source`
        // moves, and every `here` in it moves with it.
        | InAChosenLine inner ->
            { session with
                Pile =
                    Ask
                        { Chooser = actor
                          Because = ACardSaying source
                          Wanting = ALineFor(inner, Lines.all) }
                    :: session.Pile },
            [ Happened(Asked(actor, source.Saying)) ]

        // A card out of the hand, and the **line goes first**. With the line settled the command
        // is `PlayFromHand(face, ThisLine)`, which falls through to the ordinary asking below -
        // so the second half of this is the same question every other command asks, and nothing
        // here has to know about legality twice.
        | PlayFromHand(face, where) when where <> ThisLine ->
            match layable actor face where source session with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | [ only ] ->
                { session with
                    Pile = Run(PlayFromHand(face, ThisLine), { source with Line = only }) :: session.Pile },
                []
            | many ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = ACardSaying source
                              Wanting = ALineFor(PlayFromHand(face, ThisLine), many) }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]

        // "...in 1 other line with 8 or more cards" - the same question, asked of fewer lines. A
        // line is counted by what it *holds* on this player's side, not by what it is worth.
        | InAChosenLineOf(atLeast, inner) ->
            let deep =
                Lines.all
                |> List.filter ((<>) source.Line)
                |> List.filter (fun line -> List.length (Side.stack line (Session.side actor session)) >= atLeast)

            match deep with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            // One line deep enough is not a choice, and a prompt with one button on it wastes
            // somebody's time - the same reading every other command here uses.
            | [ only ] ->
                { session with
                    Pile = Run(inner, { source with Line = only }) :: session.Pile },
                []
            | many ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = ACardSaying source
                              Wanting = ALineFor(inner, many) }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]

        // "...in each line where you have a card" - the command once per line they are standing
        // in, which is a question about the *line* rather than about anything in it.
        | InEachLineHolding inner ->
            let holding =
                Lines.all
                |> List.filter (fun line -> Side.stack line (Session.side actor session) |> List.isEmpty |> not)

            match holding with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | lines ->
                { session with
                    Pile = (lines |> List.map (fun line -> Run(inner, { source with Line = line }))) @ session.Pile },
                []

        // "1 or more" - one forced, and then offered again for as long as they keep saying yes.
        // The tally goes on the step rather than in the session, so nothing else can disturb it.
        | OneOrMore inner ->
            { session with
                Pile = Run(inner, source) :: Repeating(inner, source, 0) :: session.Pile },
            []

        // "For every 2 cards in this line, ..." - the command that many times over, counted when
        // it resolves. Nought times is a command that does nothing, which is ordinary rather than
        // wrong.
        | Times(wanted, inner) ->
            match counting wanted with
            | n when n <= 0 -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | n ->
                { session with
                    Pile = List.replicate n (Run(inner, source)) @ session.Pile },
                []

        // A card off the deck and under everything already in the line: it covers nothing, so it
        // sets off no interrupt, and it is covered by whatever is there.
        | UnderThis face ->
            let taken, side, rng = Side.drawnFrom (Session.side actor session) session.Rng

            match taken with
            | None -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | Some card ->
                let placed = { Card = card; Face = face }

                doneIt
                    { session with
                        Rng = rng
                        Field =
                            session.Field
                            |> Field.withSide actor side
                            |> Field.update actor (fun side ->
                                { side with
                                    Stacks = side.Stacks |> Map.add source.Line (Side.stack source.Line side @ [ placed ]) }) },
                [ Happened(PlayedFromDeck(actor, placed, source.Line)) ]

        // "...in each other line" - the same trick without the question, once per line.
        | InEachOtherLine inner ->
            let each =
                Lines.all
                |> List.filter ((<>) source.Line)
                |> List.map (fun line -> Run(inner, { source with Line = line }))

            { session with Pile = each @ session.Pile }, []

        // "...all cards..." - every one of them, and nobody asked. A command that asks which is
        // a command with a choice in it, and there is no choice in "all".
        | Every inner ->
            match targets actor inner source session with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | many ->
                let session, said =
                    many
                    |> List.fold
                        (fun (session, said) target ->
                            let session, more = carriedOut source inner target session
                            session, said @ more)
                        (session, [])

                doneIt session, said

        // "You may X." Not offered at all if X could not have been done anyway.
        | May inner ->
            match targets actor inner source session with
            | [] when (match inner with
                       | Draw _
                       | Refreshing' -> false
                       | _ -> true) ->
                nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | _ ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = ACardSaying source
                              Wanting = Whether inner }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]

        // A condition on the board rather than on what a command did. Read where the card saying
        // it is standing: anything underneath it in that stack, and the rest of the sentence runs.
        | IfCovering rest ->
            let stack = Side.stack source.Line (Session.side actor session)

            let covering =
                stack
                |> List.tryFindIndex (fun placed -> placed.Card = source.Saying)
                |> Option.map (fun depth -> depth < List.length stack - 1)
                |> Option.defaultValue false

            if covering then
                { session with
                    Pile = (rest |> List.map (fun command -> Run(command, source))) @ session.Pile },
                []
            else
                nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]

        // "Either X or Y." Which of the two, and not whether - so a half nobody could carry out
        // is not on offer, one live half is that half done without a word, and neither is a
        // fizzle. Asked with the same `targets` a `may` uses, which is what keeps the two
        // agreeing about what "could be done" means.
        | Either(first, second) ->
            let live inner =
                match inner with
                | Draw _
                | Refreshing' -> true
                | _ -> targets actor inner source session |> List.isEmpty |> not

            match live first, live second with
            | false, false -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | true, false -> resolve first source session
            | false, true -> resolve second source session
            | true, true ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = ACardSaying source
                              Wanting = OneOf(first, second) }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]

        | Draw wanted ->
            let count = counting wanted
            let side, rng = Side.drawing count (Session.side actor session) session.Rng
            let drew = List.length side.Hand - List.length (Session.side actor session).Hand

            { session with
                Field = session.Field |> Field.withSide actor side
                Rng = rng
                Done = drew },
            [ Happened(Drew(actor, drew)) ]

        // A card off the top of a deck and onto a line. Nobody has seen it, including the player
        // playing it - which is what makes it different from every other way a card arrives.
        | FromDeck(face, where) ->
            let taken, side, rng = Side.drawnFrom (Session.side actor session) session.Rng

            match taken with
            | None -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | Some card ->
                let session =
                    { session with
                        Field = session.Field |> Field.withSide actor side
                        Rng = rng
                        Done = 1 }

                let placed = { Card = card; Face = face }

                match where with
                | ThisLine
                | ToOrFromHere -> laying actor placed source.Line None session, [ Happened(PlayedFromDeck(actor, placed, source.Line)) ]
                | AnyLine
                | OtherLines ->
                    let offered =
                        match where with
                        | OtherLines -> Lines.all |> List.filter ((<>) source.Line)
                        | _ -> Lines.all

                    // Where it goes is a choice, and the card is in the air until it is made -
                    // the same shape a shift has, and the same one used to land it.
                    { session with
                        Pile =
                            Ask
                                { Chooser = actor
                                  Because = ACardSaying source
                                  Wanting = ALine(OnTable(actor, source.Line, placed), offered) }
                            :: session.Pile },
                    [ Happened(Asked(actor, source.Saying)) ]

        // The only place the generator is asked for anything after the deal, and the reason it
        // is: a card taken at random is a card neither player chose.
        | TakeAtRandom ->
            let them = Session.other actor

            match (Session.side them session).Hand with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | hand ->
                let which, rng = Rng.intBelow (List.length hand) session.Rng
                let card = List.item which hand

                doneIt
                    { session with
                        Field =
                            session.Field
                            |> Field.update them (fun side -> { side with Hand = side.Hand |> List.filter ((<>) card) })
                            |> Field.update actor (fun side -> { side with Hand = side.Hand @ [ card ] })
                        Rng = rng },
                [ Happened(TookAtRandom(actor, card)) ]

        // Everything about a reveal is that it was said out loud, so the whole of it is a notice
        // and nothing at all changes on the table.
        | RevealTheirHand ->
            let them = Session.other actor

            match (Session.side them session).Hand with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | hand -> doneIt session, [ Happened(ShowedHand(them, hand)) ]

        // The second compile's steal, said by a card instead. Their deck is shuffled from their
        // discard first if it has run out, and a player with nothing anywhere is taken nothing
        // from - which is the same three sentences `compileLine` needs and the same code.
        | TakeTheirTop ->
            let them = Session.other actor
            let taken, theirs, rng = Side.drawnFrom (Session.side them session) session.Rng

            match taken with
            | None -> nothingDone { session with Rng = rng }, [ Happened(TookNothing actor) ]
            | Some card ->
                doneIt
                    { session with
                        Field =
                            session.Field
                            |> Field.withSide them theirs
                            |> Field.update actor (Side.took card)
                        Rng = rng
                        Chose = Some card },
                [ Happened(Took(actor, card)) ]

        // Three of the six orders, and never the one they are in: a swap moves exactly two.
        | Swap ->
            let order = (Session.side actor session).Order

            let swapped =
                Protocol.orders order
                |> List.filter (fun each ->
                    List.zip each order |> List.filter (fun (a, b) -> a <> b) |> List.length = 2)

            match swapped with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | offered ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = ACardSaying source
                              Wanting = AnOrder(actor, offered) }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]

        // A rearrangement a card asked for, rather than the one the component forces. Every order
        // is on offer including the one they are already in - the component says *a different
        // order*, and these cards say *rearrange*.
        | Rearrange whose ->
            let side =
                match whose with
                | Theirs -> Session.other actor
                | Yours
                | Anyone -> actor

            match Protocol.orders (Session.side side session).Order with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | offered ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = ACardSaying source
                              Wanting = AnOrder(side, offered) }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]

        // Remembered rather than asked of the board, because the card that said it will be gone
        // long before the turn it is about.
        | StopTheirCompile ->
            doneIt { session with NoCompile = Some(Session.other actor) }, [ Happened(StoppedCompiling(Session.other actor)) ]

        | Refreshing' ->
            let side, rng = Side.refreshed (Session.side actor session) session.Rng

            doneIt
                { session with
                    Field = session.Field |> Field.withSide actor side
                    Rng = rng },
            [ Happened(Refreshed(actor, 0, List.length side.Hand)) ]

        | Discard
        | Give
        | Reveal
        | Delete _
        | Flip _
        | Return _
        | Show _
        | PlayFromHand _
        | Shift _ ->
            match targets actor command source session with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | [ only ] ->
                let session, said = carriedOut source command only session
                doneIt { session with Chose = Some(Target.card only) }, said
            | many ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = ACardSaying source
                              Wanting = ACard(command, many) }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]

    // --- compiling ------------------------------------------------------------------------------

    let private taking seat session =
        let them = Session.other seat
        let taken, theirs, rng = Side.drawnFrom (Session.side them session) session.Rng

        let field =
            match taken with
            | Some card -> session.Field |> Field.withSide them theirs |> Field.update seat (Side.took card)
            | None -> session.Field |> Field.withSide them theirs

        taken, { session with Field = field; Rng = rng }

    /// One line, won.
    ///
    /// The protocol facing it is turned over - or, if it was turned over already, the top card
    /// of the other deck comes across instead. Either way the line is wiped, both players' cards
    /// alike, which is what makes a second compile a weapon rather than a consolation.
    let private compileLine seat line (session, told) =
        match Side.protocolOn line (Session.side seat session) with
        // Unreachable - there is no play until both orders are in.
        | None -> session, told
        | Some protocol ->
            let again = Side.hasCompiled protocol (Session.side seat session)

            // The taking comes before the sweeping, deliberately: it is the top of their deck as
            // it stands, and sweeping first could put a card into their discard, shuffle it back
            // in, and hand it straight to the player who just deleted it.
            let taken, session = if again then taking seat session else None, session

            let field =
                Field.seats session.Field
                |> List.fold (fun field who -> Field.update who (Side.swept line) field) session.Field

            let field =
                if again then field else field |> Field.update seat (Side.compiled protocol)

            let said =
                if again then
                    [ Happened(CompiledAgain(seat, protocol, line))
                      match taken with
                      | Some card -> Happened(Took(seat, card))
                      | None -> Happened(TookNothing seat) ]
                else
                    [ Happened(Compiled(seat, protocol, line)) ]

            { session with Field = field }, told @ said

    /// Every one of those lines compiled, in line order - and then the game, if that was the
    /// last of this player's three.
    ///
    /// Whichever protocol is facing a line *now* is the one that compiles, which is the whole
    /// point of this being a step of its own: a rearrangement forced by the control component
    /// has already happened by the time this runs, so a stack built patiently for Metal can
    /// compile Spirit instead.
    let private compiling lines session =
        let seat = session.ToPlay

        let session, told =
            lines |> List.fold (fun state line -> compileLine seat line state) (session, [])

        if Side.hasCompiledAll (Session.side seat session) then
            let ending = Won seat
            { session with Stage = Done ending }, told @ [ Happened(GameEnded ending) ]
        else
            session, told

    // --- the control component -------------------------------------------------------------
    //
    // An optional rule, and the reason the pile had to be general. What it costs its holder is a
    // question no card wrote - and if the pile can carry that, it can carry the cards.

    /// The component, to whoever now leads two lanes. Nobody loses it except by somebody else
    /// taking it, so it sits where it last landed until a start-of-turn earns it away.
    let private takingControl seat session =
        match session.Control with
        | NotInPlay -> session, []
        | _ when Field.leading seat session.Field < Field.LanesForControl -> session, []
        | HeldBy who when who = seat -> session, []
        | held ->
            let from =
                match held with
                | HeldBy who -> Some who
                | InTheMiddle
                | NotInPlay -> None

            { session with Control = HeldBy seat }, [ Happened(TookControl(seat, from)) ]

    /// Every order this player's protocols could be in *except* the one they are in. Five of the
    /// six, because the component forces a different order and standing pat is not on offer.
    let private rearranging seat session =
        let order = (Session.side seat session).Order

        Ask
            { Chooser = seat
              Because = TheControlComponent
              Wanting = AnOrder(seat, Protocol.orders order |> List.filter ((<>) order)) }

    /// Everything a seat has listening for that trigger, as steps to push.
    ///
    /// **Covered or not.** All four triggers are printed in top boxes, so a card goes on listening
    /// with something built over it - which is what *"even if this card is covered"* on Spirit-3
    /// says out loud, and the one place where being covered does not shut a card up.
    let private listening trigger seat session =
        Lines.all
        |> List.collect (fun line ->
            Side.stack line (Session.side seat session)
            |> List.filter Placed.isFaceUp
            |> List.collect (fun placed ->
                (Printed.on placed.Card).After
                |> List.filter (fst >> (=) trigger)
                |> List.collect snd
                |> List.map (fun command ->
                    Run(
                        command,
                        { Owner = seat
                          Saying = placed.Card
                          Line = line }
                    ))))

    /// What a command just did, read back off what it *said* - and whoever was listening for it.
    ///
    /// One place rather than a hook at every effect, because the notices are already the honest
    /// record of what happened: a deletion that was refused or fizzled did not report one, so
    /// nothing hears it. The only thing the notices cannot say is *who* deleted - `Deleted` names
    /// the side the card was sitting in - so the actor is worked out the same way `resolve` works
    /// it out, from the command and its source.
    let private heard actor said session =
        let happened =
            said
            |> List.choose (function
                | Happened event -> Some event
                | _ -> None)

        [ if happened |> List.exists (function
              | Drew(who, n) -> who = actor && n > 0
              | _ -> false) then
              yield! listening YouDraw actor session

          if happened |> List.exists (function
              | Deleted _ -> true
              | _ -> false) then
              yield! listening YouDelete actor session

          for who in Session.seats do
              if happened |> List.exists (function
                  | Discarded(seat, _) -> seat = who
                  | _ -> false) then
                  yield! listening TheyDiscard (Session.other who) session ]

    /// One of the two timed boxes, read off everything the player to move has standing face up
    /// and uncovered, in line order.
    ///
    /// The start box and the end box differ in nothing but *when*, so they are one function and
    /// two steps rather than two functions that would have to be kept saying the same thing.
    let private timed box session =
        let seat = session.ToPlay
        let side = Session.side seat session

        Lines.all
        |> List.collect (fun line ->
            match Stack.uncovered (Side.stack line side) with
            | Some placed when Placed.isFaceUp placed ->
                box (Printed.on placed.Card)
                |> List.map (fun command ->
                    Run(
                        command,
                        { Owner = seat
                          Saying = placed.Card
                          Line = line }
                    ))
            | _ -> [])

    /// What a turn begins with: the component, and then whatever has been won - with a
    /// rearrangement wedged in between if the two coincide.
    ///
    /// The lines are worked out here and compiled later, which is right rather than convenient:
    /// wiping one line changes nothing about what another is worth, and the rearrangement that
    /// happens in between changes only which protocol each line is *for*.
    let private beginning session =
        let seat = session.ToPlay
        let session, told = takingControl seat session

        // Stopped from compiling, and the stopping is spent by being used. It is cleared here
        // whether or not there was anything to compile, because it was for *this* turn and this
        // turn is the one beginning.
        if session.NoCompile = Some seat then
            { session with NoCompile = None }, told
        else

        match Field.winning seat session.Field with
        | [] -> session, told
        | lines when Session.holdsControl seat session ->
            { session with
                Pile = rearranging seat session :: Escaping lines :: Compiling lines :: session.Pile },
            told @ [ Happened(MustRearrange seat) ]
        | lines -> { session with Pile = Escaping lines :: Compiling lines :: session.Pile }, told

    // --- the look at the table --------------------------------------------------------------

    /// Every card whose middle box is **shown**: face up, and with nothing built on top of it.
    ///
    /// Both halves matter and the second is the one that is easy to miss. A card played over
    /// another covers its middle box, and the card underneath says its piece again when whatever
    /// was over it leaves - returned to a hand, deleted, shifted away. So being shown is not a
    /// thing that happens once to a card; it is a thing that can happen to the same card several
    /// times in a game, and the set below is what notices.
    let private shownNow field =
        Field.seats field
        |> List.collect (fun seat ->
            Lines.all
            |> List.collect (fun line ->
                match Stack.uncovered (Side.stack line (Field.side seat field)) with
                | Some placed when Placed.isFaceUp placed -> [ seat, line, placed.Card ]
                | _ -> []))

    /// What has become face up since the pile was last looked at, and the game with the record
    /// of what is face up brought up to date.
    ///
    /// Becoming face up is the only trigger in this game, which is worth stating as a rule
    /// because it collapses two cases into one: a card played face up and a card flipped face up
    /// fire their text by the same mechanism, and nothing else fires anything.
    ///
    /// A card that has left the table drops out of the record, so one that comes back and is
    /// turned over again says its piece again - which is the reading that matches a table.
    let private lookAgain session =
        let showing = shownNow session.Field
        let now = showing |> List.map (fun (_, _, card) -> card) |> Set.ofList
        let fresh = showing |> List.filter (fun (_, _, card) -> not (Set.contains card session.Revealed))

        fresh, { session with Revealed = now }

    /// A position taken as already read: everything lying face up and uncovered counted as
    /// *shown* rather than newly shown.
    ///
    /// The difference between a board and a history. A card face up on the table has said its
    /// piece; only a card that has *just* become face up says it again. So a position put
    /// together card by card - by a check, or by anything else that describes a table rather
    /// than plays to it - has to say which of those it means, and this is the one that means the
    /// board is what it is.
    let asRead session =
        { session with
            Revealed = shownNow session.Field |> List.map (fun (_, _, card) -> card) |> Set.ofList }

    // --- the loop ------------------------------------------------------------------------------

    /// Run the pile down until it is empty or the top of it is a question.
    ///
    /// The look at the table comes first rather than last, so a command that revealed something
    /// cannot hand over to the next command before that something has had its say.
    let rec private walk fuel session told =
        if fuel <= 0 then
            session, told
        else

        match lookAgain session with
        | (_ :: _) as fresh, session ->
            // Everything newly face up puts its text on the pile, in front of whatever was
            // waiting. Two at once is settled by seating order and then by line, which is
            // arbitrary but is at least the same every time.
            let pushed =
                fresh
                |> List.sortBy (fun (seat, line, _) -> PlayerId.value seat, line)
                // A silenced line takes every card's voice away. The card is still shown, still
                // counts, and says nothing - so it goes into the record of what is shown just the
                // same, and simply puts nothing on the pile.
                |> List.filter (fun (_, line, _) -> not (Field.silenced line session.Field))
                |> List.collect (fun (seat, line, card) ->
                    (Printed.on card).Shown
                    |> List.map (fun command ->
                        Run(
                            command,
                            { Owner = seat
                              Saying = card
                              Line = line }
                        )))

            walk (fuel - 1) { session with Pile = pushed @ session.Pile } told

        | [], session ->

        match session.Pile with
        | [] -> session, told
        // Waiting on somebody. Nothing moves until they answer.
        | Ask _ :: _ -> session, told

        | Run(command, source) :: rest ->
            let session, said = resolve command source { session with Pile = rest }

            // The actor, worked out the same way `resolve` works it out - and then whatever on
            // the board was listening for what just happened goes on top, in front of the rest
            // of the card's own text. Which is the reading a table would use: a draw that sets
            // something off sets it off *now*, not after the sentence finishes.
            let actor =
                match command with
                | Opposing _ -> Session.other source.Owner
                | _ -> source.Owner

            let session =
                { session with
                    Pile = heard actor said session @ session.Pile }

            walk (fuel - 1) session (told @ said)

        // The start commands go on top of the rest of the beginning, so they resolve before the
        // control component is taken and before anything is compiled.
        | EndTurn :: rest ->
            walk
                (fuel - 1)
                { session with
                    Pile = Opening :: BeginTurn :: rest
                    ToPlay = Session.other session.ToPlay
                    Turn = session.Turn + 1 }
                told

        | BeginTurn :: rest ->
            let session, said = beginning { session with Pile = rest }
            walk (fuel - 1) session (told @ said)

        // The card lands, on whatever the interrupt above it left behind.
        // The card lands - and if it was already on the table, it leaves where it was **in the
        // same step**. Which matters more than it sounds: the pile looks at the table between
        // every two steps, so a card lifted in one step and laid in the next would be *off* the
        // table for one of those looks. It would come back a card the game had never seen, its
        // middle box would fire again, and a card that shifts itself would do that for ever.
        | Placing(seat, placed, line, from) :: rest ->
            let lifted side =
                match from with
                | Some was ->
                    { side with
                        Stacks = side.Stacks |> Map.add was (Side.stack was side |> List.filter ((<>) placed)) }
                | None -> side

            walk
                (fuel - 1)
                { session with
                    Pile = rest
                    Field = session.Field |> Field.update seat (lifted >> Side.played placed line) }
                (told @ [ if from.IsNone then Happened(Played(seat, placed, line)) ])

        // And the card turns over, on whatever the interrupt above it left behind. A card that
        // deleted itself is not there to be turned, which is "still valid, checked when it
        // resolves" doing the work: the flip finds nothing and nothing is said about it.
        | Turning(seat, placed, line) :: rest ->
            let stack = Side.stack line (Session.side seat session)

            if not (List.contains placed stack) then
                walk (fuel - 1) { session with Pile = rest } told
            else

            let turned =
                { placed with
                    Face =
                        match placed.Face with
                        | FaceUp -> FaceDown
                        | FaceDown -> FaceUp }

            walk
                (fuel - 1)
                { session with
                    Pile = rest
                    Field = session.Field |> Field.update seat (replacing line placed (fun _ -> turned)) }
                (told @ [ Happened(Flipped(seat, turned, line)) ])

        // The tail of an "if you do", and the whole of what that phrase means: it runs if the
        // command it was waiting under did something, and is thrown away if it did not.
        | Gate(rest, source) :: tail ->
            if session.Done > 0 then
                walk (fuel - 1) { session with Pile = (rest |> List.map (fun command -> Run(command, source))) @ tail } told
            else
                walk (fuel - 1) { session with Pile = tail } (told @ [ Happened(Fizzled(source.Owner, source.Saying)) ])

        // Whatever is standing in those lines and has something to say about being wiped says it
        // now, before the sweeping. Its own step rather than part of `compiling`, so a card that
        // stops to ask stops the compile in mid-air - and so this cannot fire twice.
        | Escaping lines :: rest ->
            let saying =
                Field.seats session.Field
                |> List.collect (fun seat ->
                    lines
                    |> List.collect (fun line ->
                        Side.stack line (Field.side seat session.Field)
                        |> List.filter Placed.isFaceUp
                        |> List.collect (fun placed ->
                            (Printed.on placed.Card).WhenCompiled
                            |> List.map (fun command ->
                                Run(
                                    command,
                                    { Owner = seat
                                      Saying = placed.Card
                                      Line = line }
                                )))))

            walk (fuel - 1) { session with Pile = saying @ rest } told

        | Compiling lines :: rest ->
            let session, said = compiling lines { session with Pile = rest }
            walk (fuel - 1) session (told @ said)

        | Refreshing :: rest ->
            let seat = session.ToPlay
            let before = Session.side seat session
            let after, rng = Side.refreshed before session.Rng

            walk
                (fuel - 1)
                { session with
                    Pile = rest
                    Field = session.Field |> Field.withSide seat after
                    Rng = rng }
                (told @ [ Happened(Refreshed(seat, List.length before.Hand, List.length after.Hand)) ])

        // The check cache phase. The cache is the hand, and a hand over its limit is discarded
        // back down to it - by the player it belongs to, a card at a time.
        //
        // It is here at all because cards draw: "draw 3 cards" can put a hand at seven, and this
        // is what takes it back to five. One card per pass rather than a batch, so that a hand of
        // seven is two questions rather than one asking for a list.
        //
        // **The rules ask, and no card does.** This used to go on the pile as an ordinary
        // `Run(Discard, …)`, and a `Run` carries the card whose text is talking - so it carried
        // whatever happened to be at the top of the hand, and the board and the record both said
        // *"Water-5 asks Player 1 to choose"* about a card that had said nothing whatever. There
        // is nothing to ask on behalf of here, which is what `TheCacheCheck` says.
        | Trimming :: rest ->
            let seat = session.ToPlay
            let hand = (Session.side seat session).Hand

            let over =
                if Field.skipsCache seat session.Field then
                    0
                else
                    List.length hand - Deck.HandSize

            if over <= 0 then
                // The phase is over - which happens every turn, whether or not there was anything
                // to put down. So this is the one trigger that fires on a *phase* rather than on
                // something a command reported.
                walk (fuel - 1) { session with Pile = listening YouClearCache seat session @ rest } told
            else
                // Always a question and never a straight answer: over the limit is six cards or
                // more, so there are always at least six to choose between. The step goes back on
                // underneath, so a hand of seven comes round twice.
                let asking =
                    Ask
                        { Chooser = seat
                          Because = TheCacheCheck
                          Wanting = ACard(Discard, hand |> List.map (fun card -> InHand(seat, card))) }

                walk
                    (fuel - 1)
                    { session with Pile = asking :: Trimming :: rest }
                    (told @ [ Happened(OverTheLimit(seat, over)) ])

        // Another one? Asked for as long as the last attempt did something and there is anything
        // left to do it to. Saying no is a nothing-done, which is what stops it.
        | Repeating(inner, source, tally) :: rest ->
            if session.Done = 0 then
                walk (fuel - 1) { session with Pile = rest; Done = tally } told
            else
                let tally = tally + 1

                match targets source.Owner inner source session with
                | [] -> walk (fuel - 1) { session with Pile = rest; Done = tally } told
                | _ ->
                    walk
                        (fuel - 1)
                        { session with
                            Pile =
                                Ask
                                    { Chooser = source.Owner
                                      Because = ACardSaying source
                                      Wanting = Whether inner }
                                :: Repeating(inner, source, tally) :: rest }
                        told

        // Either end of a turn: everything this player has face up and uncovered says that box,
        // in line order. On the pile like anything else, so one of them that stops to ask stops
        // the turn where it stands - which is the behaviour a table would expect and would have
        // been a special case anywhere else.
        | Opening :: rest -> walk (fuel - 1) { session with Pile = timed _.AtStart session @ rest } told

        | Closing :: rest -> walk (fuel - 1) { session with Pile = timed _.AtEnd session @ rest } told

    /// Settle whatever is waiting, and say what happened.
    let settle session told = walk Runaway session told

    /// Put an action's housekeeping at the bottom of the pile: the cache is checked, the end
    /// commands fire, and then the turn is handed on - after everything that action set off has
    /// finished. Which is what the pile is for.
    ///
    /// The cache is checked before the end commands and not after, because an end command that
    /// draws should not be undone by the same turn's trimming. Which way round it goes is an
    /// assumption rather than a rule anybody gave; it is one line if it is the other way.
    let ending session =
        { session with Pile = session.Pile @ [ Trimming; Closing; EndTurn ] }

    /// A refresh, with the rearrangement the control component forces in front of it.
    ///
    /// Two steps rather than one function, for the same reason compiling is: what the component
    /// costs is a question, and a question has to be able to stop the game where it stands.
    let refreshing seat session =
        let session = { session with Pile = Refreshing :: session.Pile }

        if Session.holdsControl seat session then
            { session with
                Pile = rearranging seat session :: session.Pile },
            [ Happened(MustRearrange seat) ]
        else
            session, []

    // --- answering ------------------------------------------------------------------------------

    /// The question answered and off the pile. What is underneath it was put there by whoever
    /// asked - the rest of a card's text, or the compiling a rearrangement was wedged in front
    /// of - and it carries on from here.
    let private carryOn session said =
        let session, told = settle session said
        Some session, told

    let private without session = { session with Pile = List.tail session.Pile }

    /// The card behind a question, where a card is behind it.
    ///
    /// Every question that resumes a *command* has one, because a command is something printed on
    /// a card. The two the rules ask on their own behalf do not resume anything: an order is put
    /// into effect where it is answered, and a discard the check cache phase asked for points at
    /// nothing and needs nothing to point with.
    let private saying question =
        match question.Because with
        | ACardSaying source -> Some source
        | TheControlComponent
        | TheCacheCheck -> None

    /// A card, a line, or a yes or a no, named as the answer to a question that wanted one.
    let choosing question chosen session =
        match question.Wanting, chosen with
        | ACard(command, targets), TheCard card ->
            match targets |> List.tryFind (fun target -> Target.card target = card) with
            | None -> None, [ Refused(NotOnOffer question.Wanting) ]
            | Some target ->
                let session, said =
                    match saying question, target with
                    | Some source, _ -> carriedOut source command target (without session)
                    // The check cache phase, and nothing else reaches here without a card: the
                    // rules asked for a discard, and a discard is the one command that wants
                    // neither the card that said it nor the line that card is standing in.
                    | None, InHand(seat, card) -> discarded seat card (without session)
                    | None, OnTable _ -> without session, []

                carryOn { session with Done = 1; Chose = Some(Target.card target) } said

        // "You may." Saying no is an answer like any other, and it is the answer that leaves
        // whatever was waiting on it with nothing to do.
        | Whether inner, Yes ->
            match saying question with
            | None -> None, [ Refused(NotOnOffer question.Wanting) ]
            | Some source ->
                let session, said = resolve inner source (without session)
                carryOn session said

        | Whether _, No -> carryOn { without session with Done = 0 } [ Happened(Declined question.Chooser) ]

        // Which of two, and either way something happens - so unlike a `no` there is nothing here
        // that leaves what was waiting behind it with nothing to do.
        | OneOf(first, _), TheFirst
        | OneOf(_, first), TheSecond ->
            match saying question with
            | None -> None, [ Refused(NotOnOffer question.Wanting) ]
            | Some source ->
                let session, said = resolve first source (without session)
                carryOn session said

        | ALine(OnTable(seat, from, placed), offered), TheLine line when List.contains line offered ->
            let session, said = moving seat placed from line (without session)
            carryOn session said

        // A line picked for a command rather than for a card: the command goes back on the pile
        // with its source standing in the line that was chosen.
        | ALineFor(command, offered), TheLine line when List.contains line offered ->
            match saying question with
            | None -> None, [ Refused(NotOnOffer question.Wanting) ]
            | Some source ->
                let session = without session

                carryOn
                    { session with
                        Pile = Run(command, { source with Line = line }) :: session.Pile }
                    []

        | _ -> None, [ Refused(NotOnOffer question.Wanting) ]

    /// An order named as the answer to the rearrangement the control component forced.
    let ordering question order session =
        match question.Wanting with
        | AnOrder(whose, offered) when List.contains order offered ->
            let session =
                { without session with
                    Field = session.Field |> Field.update whose (Side.arranged order) }

            carryOn session [ Happened(Rearranged(whose, order)) ]
        | _ -> None, [ Refused(NotOnOffer question.Wanting) ]
