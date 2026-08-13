namespace TCModel.Compile

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
    let private onTable actor selector (source: Source) field =
        let picks seat line (placed: Placed) =
            (match selector.Whose with
             | Yours -> seat = actor
             | Theirs -> seat <> actor
             | Anyone -> true)
            && (match selector.Where with
                | ThisLine -> line = source.Line
                | AnyLine -> true)
            && (match selector.Showing with
                | Some wanted -> placed.Face = wanted
                | None -> true)
            && (not selector.Uncovered
                || Stack.uncovered (Side.stack line (Field.side seat field)) = Some placed)

        Field.seats field
        |> List.collect (fun seat ->
            Lines.all
            |> List.collect (fun line ->
                Side.stack line (Field.side seat field)
                |> List.filter (picks seat line)
                |> List.map (fun placed -> OnTable(seat, line, placed))))

    /// Whether that card is standing on the table saying it cannot be deleted.
    let private breakable seat line placed field =
        Ruling.breakable (Stack.uncovered (Side.stack line (Field.side seat field)) = Some placed) placed

    /// What a command may be pointed at, right now. Empty means it has nothing to do.
    ///
    /// This is also where "still valid, checked when it resolves" actually lives: the targets are
    /// worked out at the moment the command comes off the pile, so a card that has left the table
    /// since simply is not among them.
    let rec private targets actor command source field =
        match command with
        | Draw _
        | Refreshing'
        | IfYouDo _
        | Opposing _ -> []
        // What a `may` could be pointed at is what the command inside it could be pointed at,
        // which is how a card knows not to offer something impossible.
        | May inner -> targets actor inner source field
        | Discard -> (Field.side actor field).Hand |> List.map (fun card -> InHand(actor, card))
        | Delete selector ->
            // The one place a middle command is asked about besides the value of a stack. A card
            // that cannot be deleted is not a deletion that fails - it is not a target at all,
            // so a command pointed only at it finds nothing to do and says so.
            onTable actor selector source field
            |> List.filter (function
                | OnTable(seat, line, placed) -> breakable seat line placed field
                | InHand _ -> true)
        | Flip selector
        | Return selector
        | Shift selector -> onTable actor selector source field
        | Rehome selector ->
            // Only a card sitting somewhere that is not its home has anywhere to be sent.
            onTable actor selector source field
            |> List.filter (fun target -> Field.homeOf (Target.card target) field <> Some(Target.owner target))

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

    /// Carry a command out on the card it was pointed at. Who is doing it is not asked for:
    /// the target already says whose card it is, and that is the only side that changes.
    let private carriedOut command target session =
        match command, target with
        | Flip _, OnTable(seat, line, placed) ->
            let turned =
                { placed with
                    Face =
                        match placed.Face with
                        | FaceUp -> FaceDown
                        | FaceDown -> FaceUp }

            { session with
                Field = session.Field |> Field.update seat (replacing line placed (fun _ -> turned)) },
            [ Happened(Flipped(seat, turned, line)) ]

        | Delete _, OnTable(seat, line, placed) ->
            { session with
                Field = session.Field |> Field.update seat (removing line placed) },
            [ Happened(Deleted(seat, placed, line)) ]

        | Discard, InHand(seat, card) ->
            { session with
                Field =
                    session.Field
                    |> Field.update seat (fun side ->
                        { side with
                            Hand = side.Hand |> List.filter ((<>) card)
                            Discard = card :: side.Discard }) },
            [ Happened(Discarded(seat, card)) ]

        | Return _, OnTable(seat, line, placed) ->
            { session with
                Field =
                    session.Field
                    |> Field.update seat (fun side ->
                        { side with
                            Stacks = side.Stacks |> Map.add line (Side.stack line side |> List.filter ((<>) placed))
                            Hand = side.Hand @ [ placed.Card ] }) },
            [ Happened(Returned(seat, placed, line)) ]

        // Home is worked out from the card rather than remembered, so this needs nothing to have
        // been kept along the way.
        | Rehome _, OnTable(seat, line, placed) ->
            match Field.homeOf placed.Card session.Field with
            | None -> session, [ Happened(Fizzled(seat, placed.Card)) ]
            | Some home ->
                { session with
                    Field =
                        session.Field
                        |> Field.update seat (fun side ->
                            { side with
                                Stacks = side.Stacks |> Map.add line (Side.stack line side |> List.filter ((<>) placed)) })
                        |> Field.update home (fun side -> { side with Hand = side.Hand @ [ placed.Card ] }) },
                [ Happened(WentHome(home, placed.Card)) ]

        // A shift is the one command that asks twice. Which card is settled; where it goes is
        // another question, and it goes on the pile like any other.
        | Shift _, OnTable(seat, line, placed) ->
            let elsewhere = Lines.all |> List.filter ((<>) line)

            { session with
                Pile =
                    Ask
                        { Chooser = seat
                          Because = None
                          Wanting = ALine(OnTable(seat, line, placed), elsewhere) }
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

        let nothingDone session = { session with Did = false }
        let doneIt session = { session with Did = true }

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

        // "You may X." Not offered at all if X could not have been done anyway.
        | May inner ->
            match targets actor inner source session.Field with
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
                              Because = Some source
                              Wanting = Whether inner }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]

        | Draw count ->
            let side, rng = Side.drawing count (Session.side actor session) session.Rng
            let drew = List.length side.Hand - List.length (Session.side actor session).Hand

            { session with
                Field = session.Field |> Field.withSide actor side
                Rng = rng
                Did = drew > 0 },
            [ Happened(Drew(actor, drew)) ]

        | Refreshing' ->
            let side, rng = Side.refreshed (Session.side actor session) session.Rng

            doneIt
                { session with
                    Field = session.Field |> Field.withSide actor side
                    Rng = rng },
            [ Happened(Refreshed(actor, 0, List.length side.Hand)) ]

        | Discard
        | Delete _
        | Flip _
        | Return _
        | Shift _
        | Rehome _ ->
            match targets actor command source session.Field with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | [ only ] ->
                let session, said = carriedOut command only session
                doneIt session, said
            | many ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = Some source
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
              Because = None
              Wanting = AnOrder(Protocol.orders order |> List.filter ((<>) order)) }

    /// What a turn begins with: the component, and then whatever has been won - with a
    /// rearrangement wedged in between if the two coincide.
    ///
    /// The lines are worked out here and compiled later, which is right rather than convenient:
    /// wiping one line changes nothing about what another is worth, and the rearrangement that
    /// happens in between changes only which protocol each line is *for*.
    let private beginning session =
        let seat = session.ToPlay
        let session, told = takingControl seat session

        match Field.winning seat session.Field with
        | [] -> session, told
        | lines when Session.holdsControl seat session ->
            { session with
                Pile = rearranging seat session :: Compiling lines :: session.Pile },
            told @ [ Happened(MustRearrange seat) ]
        | lines -> { session with Pile = Compiling lines :: session.Pile }, told

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
            walk (fuel - 1) session (told @ said)

        | EndTurn :: rest ->
            walk
                (fuel - 1)
                { session with
                    Pile = BeginTurn :: rest
                    ToPlay = Session.other session.ToPlay
                    Turn = session.Turn + 1 }
                told

        | BeginTurn :: rest ->
            let session, said = beginning { session with Pile = rest }
            walk (fuel - 1) session (told @ said)

        // The card lands, on whatever the interrupt above it left behind.
        | Placing(seat, placed, line) :: rest ->
            walk
                (fuel - 1)
                { session with
                    Pile = rest
                    Field = session.Field |> Field.update seat (Side.played placed line) }
                (told @ [ Happened(Played(seat, placed, line)) ])

        // The tail of an "if you do", and the whole of what that phrase means: it runs if the
        // command it was waiting under did something, and is thrown away if it did not.
        | Gate(rest, source) :: tail ->
            if session.Did then
                walk (fuel - 1) { session with Pile = (rest |> List.map (fun command -> Run(command, source))) @ tail } told
            else
                walk (fuel - 1) { session with Pile = tail } (told @ [ Happened(Fizzled(source.Owner, source.Saying)) ])

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
        // is what takes it back to five. One card per pass rather than a batch, so that each one
        // is an ordinary `Discard` and goes through the same asking as any other - and so that a
        // hand of six asks once instead of asking for a list of one.
        | Trimming :: rest ->
            let seat = session.ToPlay
            let over = List.length (Session.side seat session).Hand - Deck.HandSize

            if over <= 0 then
                walk (fuel - 1) { session with Pile = rest } told
            else
                let discarding =
                    Run(
                        Discard,
                        { Owner = seat
                          Saying = List.head (Session.side seat session).Hand
                          Line = 1 }
                    )

                // The step goes back on underneath, so a hand of seven comes round twice.
                walk (fuel - 1) { session with Pile = discarding :: Trimming :: rest } told

        // The end of a turn: everything this player has face up and uncovered says its bottom
        // command, in line order. On the pile like anything else, so a bottom command that stops
        // to ask stops the turn from ending - which is the behaviour a table would expect and
        // would have been a special case anywhere else.
        | Closing :: rest ->
            let seat = session.ToPlay
            let side = Session.side seat session

            let pushed =
                Lines.all
                |> List.collect (fun line ->
                    match Stack.uncovered (Side.stack line side) with
                    | Some placed when Placed.isFaceUp placed ->
                        (Printed.on placed.Card).AtEnd
                        |> List.map (fun command ->
                            Run(
                                command,
                                { Owner = seat
                                  Saying = placed.Card
                                  Line = line }
                            ))
                    | _ -> [])

            walk (fuel - 1) { session with Pile = pushed @ rest } told

    /// Settle whatever is waiting, and say what happened.
    let settle session told = walk Runaway session told

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
    let laying seat placed line session =
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
            Pile = interrupting @ (Placing(seat, placed, line) :: session.Pile) }

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

    /// A card, a line, or a yes or a no, named as the answer to a question that wanted one.
    let choosing question chosen session =
        match question.Wanting, chosen with
        | ACard(command, targets), TheCard card ->
            match targets |> List.tryFind (fun target -> Target.card target = card) with
            | None -> None, [ Refused(NotOnOffer question.Wanting) ]
            | Some target ->
                let session, said = carriedOut command target (without session)
                carryOn { session with Did = true } said

        // "You may." Saying no is an answer like any other, and it is the answer that leaves
        // whatever was waiting on it with nothing to do.
        | Whether inner, Yes ->
            let session, said = resolve inner question.Because.Value (without session)
            carryOn session said

        | Whether _, No -> carryOn { without session with Did = false } [ Happened(Declined question.Chooser) ]

        | ALine(OnTable(seat, from, placed), offered), TheLine line when List.contains line offered ->
            // Off the line it was on, and then laid on the new one the same way a play is - so a
            // card shifted onto something with an interrupt sets that interrupt off too. Covering
            // is covering, however the card got there.
            let session =
                { without session with
                    Field =
                        session.Field
                        |> Field.update seat (fun side ->
                            { side with
                                Stacks = side.Stacks |> Map.add from (Side.stack from side |> List.filter ((<>) placed)) }) }

            carryOn (laying seat placed line session) [ Happened(Shifted(seat, placed, from, line)) ]

        | _ -> None, [ Refused(NotOnOffer question.Wanting) ]

    /// An order named as the answer to the rearrangement the control component forced.
    let ordering question order session =
        match question.Wanting with
        | AnOrder offered when List.contains order offered ->
            let session =
                { without session with
                    Field = session.Field |> Field.update question.Chooser (Side.arranged order) }

            carryOn session [ Happened(Rearranged(question.Chooser, order)) ]
        | _ -> None, [ Refused(NotOnOffer question.Wanting) ]
