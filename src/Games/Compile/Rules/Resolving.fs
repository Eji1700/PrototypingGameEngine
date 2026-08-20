namespace TCModel.Compile

open TCModel.Common
open TCModel.Engine

// Carrying out what the cards say.
//
// `session.Pile` is a stack of work still to do, and everything here is written in terms of
// pushing onto it: the head is what happens next, so a command that puts three things on the
// pile has them happen in the order they were listed. `walk` takes the head off and runs it
// until the pile is empty or the top of it is a question for a player, which is the only
// thing that stops the machine and waits.
module Resolving =

    // Cards that put more work on the pile than they take off can loop - one flipping another
    // that flips it back. Nothing in the printed text should, but a miscount in a card would
    // hang the table rather than misplay a turn, so the walk is given a step count and stops.
    [<Literal>]
    let private Runaway = 500


    /// Every card on the table the selector names. The clauses read as one long "and": a
    /// selector is written by adding conditions to `Select.any`, so an unset field means the
    /// card text said nothing about that and everything passes it.
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
            && (List.isEmpty selector.Worth
                || List.contains (Placed.value placed) selector.Worth)

        let found =
            Field.seats field
            |> List.collect (fun seat ->
                Lines.all
                |> List.collect (fun line ->
                    Side.stack line (Field.side seat field)
                    |> List.filter (picks seat line)
                    |> List.map (fun placed -> OnTable(seat, line, placed))))

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

    let private mayLay actor card line face field =
        (Field.barred actor line face field |> Option.isNone)
        && (face = FaceDown || Field.allows actor card line field)

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
        | Give
        | Reveal -> (Field.side actor field).Hand |> List.map (fun card -> InHand(actor, card))
        | May inner
        | Every inner -> targets actor inner source session
        | Either(first, second) -> targets actor first source session @ targets actor second source session
        | Discard -> (Field.side actor field).Hand |> List.map (fun card -> InHand(actor, card))
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


    let private replacing line placed change side =
        { side with
            Stacks =
                side.Stacks
                |> Map.add
                    line
                    (Side.stack line side
                     |> List.map (fun other -> if other = placed then change other else other)) }

    let private removing line placed side =
        { side with
            Stacks = side.Stacks |> Map.add line (Side.stack line side |> List.filter ((<>) placed))
            Discard = placed.Card :: side.Discard }

    /// Putting a card into a line. What was on top of that line speaks first: its `WhenCovered`
    /// goes on the pile ahead of the `Placing`, so the card gets to say its piece while it is
    /// still the uncovered one.
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

    let private moving seat placed from line session =
        laying seat placed line (Some from) session, [ Happened(Shifted(seat, placed, from, line)) ]

    let private discarded seat card session =
        { session with
            Field =
                session.Field
                |> Field.update seat (fun side ->
                    { side with
                        Hand = side.Hand |> List.filter ((<>) card)
                        Discard = card :: side.Discard }) },
        [ Happened(Discarded(seat, card)) ]

    let private carriedOut (source: Source) command target session =
        match command, target with
        // As with covering: the card's `WhenFlipped` is pushed ahead of the `Turning`, so it
        // speaks from the face it is being turned off.
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

        | Reveal, InHand(seat, card) -> session, [ Happened(Showed(seat, card)) ]

        | Show _, OnTable(seat, _, placed) -> session, [ Happened(Showed(seat, placed.Card)) ]

        | PlayFromHand(face, _), InHand(seat, card) ->
            let placed = Placed.laid face card

            let session =
                { session with
                    Field =
                        session.Field
                        |> Field.update seat (fun side ->
                            { side with
                                Hand = side.Hand |> List.filter ((<>) card) }) }

            laying seat placed source.Line None session, []

        | Give, InHand(seat, card) ->
            let them = Session.other seat

            { session with
                Field =
                    session.Field
                    |> Field.update seat (fun side ->
                        { side with
                            Hand = side.Hand |> List.filter ((<>) card) })
                    |> Field.update them (fun side ->
                        { side with
                            Hand = side.Hand @ [ card ] }) },
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


        | Shift(_, where), OnTable(seat, line, placed) ->
            let allowed =
                Lines.all
                |> List.filter ((<>) line)
                |> List.filter (fun other ->
                    match where with
                    | AnyLine -> true
                    | ThisLine -> other = source.Line
                    | OtherLines -> other <> source.Line
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

        | _ -> session, []


    let rec private resolve command (source: Source) session =
        let actor =
            match command with
            | Opposing _ -> Session.other source.Owner
            | _ -> source.Owner

        // `Done` is how much the command just carried out actually did, and `Chose` is what it
        // picked. Both are read by whatever comes after: `IfYouDo` gates on `Done`, `Times`
        // can count from it, and a selector can ask for the card that was chosen.
        let nothingDone session = { session with Done = 0; Chose = None }
        let doneIt session = { session with Done = 1 }

        let counting =
            function
            | Just n -> n
            | WorthOfChosen -> session.Chose |> Option.map (fun card -> card.Value) |> Option.defaultValue 0
            | HowManyPlus n -> session.Done + n
            | PerCards(each, selector) ->
                if each <= 0 then 0 else List.length (targets actor (Delete selector) source session) / each

        match command with
        | Opposing inner -> resolve inner { source with Owner = actor } session

        // The gate is pushed under the command it gates on, so by the time it is reached the
        // command has run and left its tally in `Done`.
        | IfYouDo(first, rest) ->
            { session with
                Pile = Run(first, source) :: Gate(rest, source) :: session.Pile },
            []

        | InAChosenLine inner ->
            { session with
                Pile =
                    Ask
                        { Chooser = actor
                          Because = ACardSaying source
                          Wanting = ALineFor(inner, Lines.all) }
                    :: session.Pile },
            [ Happened(Asked(actor, source.Saying)) ]

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

        | InAChosenLineOf(atLeast, inner) ->
            let deep =
                Lines.all
                |> List.filter ((<>) source.Line)
                |> List.filter (fun line -> List.length (Side.stack line (Session.side actor session)) >= atLeast)

            match deep with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
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

        | InEachLineHolding inner ->
            let holding =
                Lines.all
                |> List.filter (fun line -> Side.stack line (Session.side actor session) |> List.isEmpty |> not)

            match holding with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | lines ->
                { session with
                    Pile =
                        (lines |> List.map (fun line -> Run(inner, { source with Line = line })))
                        @ session.Pile },
                []

        // Once outright, and then a `Repeating` under it that keeps asking for another.
        | OneOrMore inner ->
            { session with
                Pile = Run(inner, source) :: Repeating(inner, source, 0) :: session.Pile },
            []

        | Times(wanted, inner) ->
            match counting wanted with
            | n when n <= 0 -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | n ->
                { session with
                    Pile = List.replicate n (Run(inner, source)) @ session.Pile },
                []

        // Straight to the bottom of the line, under everything already there - so unlike every
        // other way of playing a card, this one covers nothing and wakes nothing up.
        | UnderThis face ->
            let taken, side, rng = Side.drawnFrom (Session.side actor session) session.Rng

            match taken with
            | None -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | Some card ->
                let placed = Placed.laid face card

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

        | InEachOtherLine inner ->
            let each =
                Lines.all
                |> List.filter ((<>) source.Line)
                |> List.map (fun line -> Run(inner, { source with Line = line }))

            { session with
                Pile = each @ session.Pile },
            []

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

        | May inner ->
            match targets actor inner source session with
            | [] when
                (match inner with
                 | Draw _
                 | Refreshing' -> false
                 | _ -> true)
                ->
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

        // A choice is only put to a player if both halves could actually do something; if only
        // one could, that one just happens. Drawing and refreshing always can.
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

                let placed = Placed.laid face card

                match where with
                | ThisLine
                | ToOrFromHere ->
                    laying actor placed source.Line None session, [ Happened(PlayedFromDeck(actor, placed, source.Line)) ]
                | AnyLine
                | OtherLines ->
                    let offered =
                        match where with
                        | OtherLines -> Lines.all |> List.filter ((<>) source.Line)
                        | _ -> Lines.all

                    { session with
                        Pile =
                            Ask
                                { Chooser = actor
                                  Because = ACardSaying source
                                  Wanting = ALine(OnTable(actor, source.Line, placed), offered) }
                            :: session.Pile },
                    [ Happened(Asked(actor, source.Saying)) ]

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
                            |> Field.update them (fun side ->
                                { side with
                                    Hand = side.Hand |> List.filter ((<>) card) })
                            |> Field.update actor (fun side ->
                                { side with
                                    Hand = side.Hand @ [ card ] })
                        Rng = rng },
                [ Happened(TookAtRandom(actor, card)) ]

        | RevealTheirHand ->
            let them = Session.other actor

            match (Session.side them session).Hand with
            | [] -> nothingDone session, [ Happened(Fizzled(actor, source.Saying)) ]
            | hand -> doneIt session, [ Happened(ShowedHand(them, hand)) ]

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

        | Swap ->
            let order = (Session.side actor session).Order

            // Orders that differ from the standing one in exactly two places, which is to say
            // every way of swapping one pair and leaving the third protocol where it is.
            let swapped =
                Protocol.orders order
                |> List.filter (fun each -> List.zip each order |> List.filter (fun (a, b) -> a <> b) |> List.length = 2)

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

        | StopTheirCompile ->
            doneIt
                { session with
                    NoCompile = Some(Session.other actor) },
            [ Happened(StoppedCompiling(Session.other actor)) ]

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

                doneIt
                    { session with
                        Chose = Some(Target.card only) },
                said
            | many ->
                { session with
                    Pile =
                        Ask
                            { Chooser = actor
                              Because = ACardSaying source
                              Wanting = ACard(command, many) }
                        :: session.Pile },
                [ Happened(Asked(actor, source.Saying)) ]


    let private taking seat session =
        let them = Session.other seat
        let taken, theirs, rng = Side.drawnFrom (Session.side them session) session.Rng

        let field =
            match taken with
            | Some card ->
                session.Field
                |> Field.withSide them theirs
                |> Field.update seat (Side.took card)
            | None -> session.Field |> Field.withSide them theirs

        taken,
        { session with
            Field = field
            Rng = rng }

    /// Compiling one line. Both sides' stacks in that line are swept away either way; what
    /// differs is whether the protocol is new - a line compiled a second time takes a card off
    /// the top of the other player's deck instead of counting again.
    let private compileLine seat line (session, told) =
        match Side.protocolOn line (Session.side seat session) with
        | None -> session, told
        | Some protocol ->
            let again = Side.hasCompiled protocol (Session.side seat session)

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

    let private compiling lines session =
        let seat = session.ToPlay

        let session, told =
            lines |> List.fold (fun state line -> compileLine seat line state) (session, [])

        if Side.hasCompiledAll (Session.side seat session) then
            let ending = Won seat
            { session with Stage = Done ending }, told @ [ Happened(GameEnded ending) ]
        else
            session, told


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

    let private rearranging seat session =
        let order = (Session.side seat session).Order

        Ask
            { Chooser = seat
              Because = TheControlComponent
              Wanting = AnOrder(seat, Protocol.orders order |> List.filter ((<>) order)) }

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

    let private heard actor said session =
        let happened =
            said
            |> List.choose (function
                | Happened event -> Some event
                | _ -> None)

        [ if
              happened
              |> List.exists (function
                  | Drew(who, n) -> who = actor && n > 0
                  | _ -> false)
          then
              yield! listening YouDraw actor session

          if
              happened
              |> List.exists (function
                  | Deleted _ -> true
                  | _ -> false)
          then
              yield! listening YouDelete actor session

          for who in Session.seats do
              if
                  happened
                  |> List.exists (function
                      | Discarded(seat, _) -> seat = who
                      | _ -> false)
              then
                  yield! listening TheyDiscard (Session.other who) session ]

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

    let private beginning session =
        let seat = session.ToPlay
        let session, told = takingControl seat session

        if session.NoCompile = Some seat then
            { session with NoCompile = None }, told
        else

        match Field.winning seat session.Field with
        | [] -> session, told
        | lines when Session.holdsControl seat session ->
            { session with
                Pile = rearranging seat session :: Escaping lines :: Compiling lines :: session.Pile },
            told @ [ Happened(MustRearrange seat) ]
        | lines ->
            { session with
                Pile = Escaping lines :: Compiling lines :: session.Pile },
            told


    let private shownNow field =
        Field.seats field
        |> List.collect (fun seat ->
            Lines.all
            |> List.collect (fun line ->
                match Stack.uncovered (Side.stack line (Field.side seat field)) with
                | Some placed when Placed.isFaceUp placed -> [ seat, line, placed.Card ]
                | _ -> []))

    /// Cards showing face up and uncovered that were not showing last time round. `Revealed`
    /// is what was showing when the walk last looked, so a card speaks its `Shown` text once
    /// when it comes into view and not again until it has been out of view and come back.
    let private lookAgain session =
        let showing = shownNow session.Field
        let now = showing |> List.map (fun (_, _, card) -> card) |> Set.ofList

        let fresh =
            showing
            |> List.filter (fun (_, _, card) -> not (Set.contains card session.Revealed))

        fresh, { session with Revealed = now }

    let asRead session =
        { session with
            Revealed = shownNow session.Field |> List.map (fun (_, _, card) -> card) |> Set.ofList }


    let rec private walk fuel session told =
        if fuel <= 0 then
            session, told
        else

        // Newly uncovered cards speak before anything already on the pile, so a card revealed
        // by the work in hand gets its say before the rest of that work carries on.
        match lookAgain session with
        | (_ :: _) as fresh, session ->
            let pushed =
                fresh
                |> List.sortBy (fun (seat, line, _) -> PlayerId.value seat, line)
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

            walk
                (fuel - 1)
                { session with
                    Pile = pushed @ session.Pile }
                told

        | [], session ->

        match session.Pile with
        | [] -> session, told
        // A question at the head of the pile stops the walk. It stays there until the player
        // answers, and `choosing` takes it off again.
        | Ask _ :: _ -> session, told

        | Run(command, source) :: rest ->
            let session, said = resolve command source { session with Pile = rest }

            let actor =
                match command with
                | Opposing _ -> Session.other source.Owner
                | _ -> source.Owner

            let session =
                { session with
                    Pile = heard actor said session @ session.Pile }

            walk (fuel - 1) session (told @ said)

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

        | Turning(seat, placed, line) :: rest ->
            let stack = Side.stack line (Session.side seat session)

            // The card's own `WhenFlipped` ran first and may have deleted or moved it. If it
            // is no longer where it was, there is nothing left to turn over.
            if not (List.contains placed stack) then
                walk (fuel - 1) { session with Pile = rest } told
            else

            let turned = Placed.turned placed

            walk
                (fuel - 1)
                { session with
                    Pile = rest
                    Field = session.Field |> Field.update seat (replacing line placed (fun _ -> turned)) }
                (told @ [ Happened(Flipped(seat, turned, line)) ])

        | Gate(rest, source) :: tail ->
            if session.Done > 0 then
                walk
                    (fuel - 1)
                    { session with
                        Pile = (rest |> List.map (fun command -> Run(command, source))) @ tail }
                    told
            else
                walk (fuel - 1) { session with Pile = tail } (told @ [ Happened(Fizzled(source.Owner, source.Saying)) ])

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
                (told
                 @ [ Happened(Refreshed(seat, List.length before.Hand, List.length after.Hand)) ])

        // The cache check asks for one discard at a time and puts itself back under the
        // question, so it comes round again and again until the hand is down to size.
        | Trimming :: rest ->
            let seat = session.ToPlay
            let hand = (Session.side seat session).Hand

            let over =
                if Field.skipsCache seat session.Field then 0 else List.length hand - Deck.HandSize

            if over <= 0 then
                walk
                    (fuel - 1)
                    { session with
                        Pile = listening YouClearCache seat session @ rest }
                    told
            else
                let asking =
                    Ask
                        { Chooser = seat
                          Because = TheCacheCheck
                          Wanting = ACard(Discard, hand |> List.map (fun card -> InHand(seat, card))) }

                walk
                    (fuel - 1)
                    { session with
                        Pile = asking :: Trimming :: rest }
                    (told @ [ Happened(OverTheLimit(seat, over)) ])

        // "One or more": ask again as long as the last one did something and there is still
        // something to do it to. The running tally goes back into `Done` at the end, so a
        // later `IfYouDo` sees how many were carried out rather than just the last one.
        | Repeating(inner, source, tally) :: rest ->
            if session.Done = 0 then
                walk
                    (fuel - 1)
                    { session with
                        Pile = rest
                        Done = tally }
                    told
            else
                let tally = tally + 1

                match targets source.Owner inner source session with
                | [] ->
                    walk
                        (fuel - 1)
                        { session with
                            Pile = rest
                            Done = tally }
                        told
                | _ ->
                    walk
                        (fuel - 1)
                        { session with
                            Pile =
                                Ask
                                    { Chooser = source.Owner
                                      Because = ACardSaying source
                                      Wanting = Whether inner }
                                :: Repeating(inner, source, tally)
                                :: rest }
                        told

        | Opening :: rest ->
            walk
                (fuel - 1)
                { session with
                    Pile = timed _.AtStart session @ rest }
                told

        | Closing :: rest ->
            walk
                (fuel - 1)
                { session with
                    Pile = timed _.AtEnd session @ rest }
                told

    let settle session told = walk Runaway session told

    /// The end of a turn, put on the *bottom* of the pile - everything the move itself sets off
    /// runs first, and these three are what is left once it has all settled.
    let ending session =
        { session with
            Pile = session.Pile @ [ Trimming; Closing; EndTurn ] }

    let refreshing seat session =
        let session =
            { session with
                Pile = Refreshing :: session.Pile }

        if Session.holdsControl seat session then
            { session with
                Pile = rearranging seat session :: session.Pile },
            [ Happened(MustRearrange seat) ]
        else
            session, []


    let private carryOn session said =
        let session, told = settle session said
        Some session, told

    let private without session =
        { session with
            Pile = List.tail session.Pile }

    let private saying question =
        match question.Because with
        | ACardSaying source -> Some source
        | TheControlComponent
        | TheCacheCheck -> None

    let choosing question chosen session =
        match question.Wanting, chosen with
        | ACard(command, targets), TheCard card ->
            match targets |> List.tryFind (fun target -> Target.card target = card) with
            | None -> None, [ Refused(NotOnOffer question.Wanting) ]
            | Some target ->
                let session, said =
                    match saying question, target with
                    | Some source, _ -> carriedOut source command target (without session)
                    | None, InHand(seat, card) -> discarded seat card (without session)
                    | None, OnTable _ -> without session, []

                carryOn
                    { session with
                        Done = 1
                        Chose = Some(Target.card target) }
                    said

        | Whether inner, Yes ->
            match saying question with
            | None -> None, [ Refused(NotOnOffer question.Wanting) ]
            | Some source ->
                let session, said = resolve inner source (without session)
                carryOn session said

        | Whether _, No -> carryOn { without session with Done = 0 } [ Happened(Declined question.Chooser) ]

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

    let ordering question order session =
        match question.Wanting with
        | AnOrder(whose, offered) when List.contains order offered ->
            let session =
                { without session with
                    Field = session.Field |> Field.update whose (Side.arranged order) }

            carryOn session [ Happened(Rearranged(whose, order)) ]
        | _ -> None, [ Refused(NotOnOffer question.Wanting) ]
