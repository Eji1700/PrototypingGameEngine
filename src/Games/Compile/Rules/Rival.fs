namespace TCModel.Compile

open TCModel.Common
open TCModel.Engine

/// How well a machine plays.
///
/// **Two, and they differ by something a player can see.** For a long while there was only one,
/// and that was the honest state of the rules rather than modesty: a machine that plays *better*
/// needs something to be better *at*, and until the ninety cards were in there was no win to
/// steer towards and no stack worth more than another.
///
/// There is now. A line at ten and ahead compiles, three compiles win, and a card is worth what
/// is printed on it face up and two face down - which is enough for arithmetic, and arithmetic is
/// what `medium` plays. It does not search and it does not read card text, so it is not `hard`;
/// what it does is count, and counting beats not counting.
type Skill = { Name: string; Describe: string }

/// A machine at a seat: how it plays, and its own generator. The generator is what stops a
/// machine playing the same game every time, and it travels with the machine so that the same
/// table dealt twice plays the same twice.
type Rival = { Skill: Skill; Rng: Rng }

module Rival =

    /// One of them, at random, and the generator as it then stands.
    let private pick items rng =
        let n, rng = Rng.intBelow (List.length items) rng
        List.item n items, rng

    /// A list in some order of its own choosing, by drawing it out one at a time.
    let private shuffled items rng =
        let rec draw taken left rng =
            match left with
            | [] -> List.rev taken, rng
            | _ ->
                let one, rng = pick left rng
                draw (one :: taken) (left |> List.filter ((<>) one)) rng

        draw [] items rng

    /// The best of them by that score, with the generator breaking a tie.
    ///
    /// Ties are broken **at random rather than by order**, which matters more than it looks: a
    /// machine that always took the first of an equal set would draft the same three protocols
    /// every game and play the same line every turn, and two of them at a table would play one
    /// game over and over.
    let private best score items rng =
        match items with
        | [] -> None, rng
        | items ->
            let top = items |> List.map score |> List.max
            let chosen, rng = pick (items |> List.filter (fun item -> score item = top)) rng
            Some chosen, rng

    // --- what a machine that counts is counting -------------------------------------------------
    //
    // No card text is read here, and no search is done. What `medium` knows is the arithmetic the
    // board already shows a player: what a line is worth, what it takes to compile one, and what a
    // card is worth face up against face down. That is enough to stop it throwing fives away.

    /// What laying that card there, that way up, is worth.
    let private worthPlaying seat session (card, line, face) =
        let mine = Field.valueOn seat line session.Field
        let theirs = Field.valueOn (Session.other seat) line session.Field

        let gained =
            match face with
            | FaceUp -> card.Value
            | FaceDown -> Placed.FaceDownValue

        // A line whose protocol is already compiled is worth having, but it is not a step towards
        // winning: compiling it again is a card off their deck rather than one of the three.
        let towardsWinning =
            match Side.protocolOn line (Field.side seat session.Field) with
            | Some protocol -> not (Side.hasCompiled protocol (Field.side seat session.Field))
            | None -> false

        [ gained * 2
          // Ten and strictly ahead is a compile the moment this turn comes round again, which is
          // worth more than any amount of building towards one.
          if mine + gained >= Stack.ToCompile && mine + gained > theirs then 20
          if towardsWinning then 4
          // And a five spent as a two is three thrown away.
          match face with
          | FaceDown -> -card.Value
          | FaceUp -> 0 ]
        |> List.sum

    /// What answering a question with that card is worth.
    ///
    /// One score for every command that points at a card, because what a machine wants out of a
    /// deletion and out of a discard are the same wish said twice: **their** cards should be big
    /// and **yours** should be small.
    let private worthChoosing seat command target =
        let value = (Target.card target).Value
        let theirs = Target.owner target <> seat

        match command with
        // Off their table, the bigger the better - and off yours, the smaller.
        | Delete _
        | Return _
        | Shift _ -> if theirs then value else -value
        // Out of your own hand, so spend the least you can.
        | Discard
        | Give
        | Reveal
        | Show _ -> -value
        // Into your own line, so spend the most you can.
        | PlayFromHand _ -> value
        // Their five face up becomes a two; your face-down five becomes a five. Either way the
        // gap between what it is worth now and what it would be worth turned over is the prize.
        | Flip _ ->
            match target with
            | OnTable(_, _, placed) ->
                let now = Placed.value placed

                let after =
                    match placed.Face with
                    | FaceUp -> Placed.FaceDownValue
                    | FaceDown -> placed.Card.Value

                if theirs then now - after else after - now
            | InHand _ -> 0
        | _ -> 0

    /// Which move a machine plays, and the machine as it then stands.
    ///
    /// Three stages and therefore three answers, each of them picked out of what the rules
    /// would actually take - a machine that guessed and was refused would be asked again, and
    /// again, with the turn never passing and nothing on the screen to say why.
    let plays session rival =
        // Nothing at all, for the machine that plays at random - so every `best` below collapses
        // to a draw out of the hat, and the two machines share one set of legal moves.
        let counting score =
            if rival.Skill.Name = "easy" then (fun _ -> 0) else score

        let asked = Session.asking session

        // A card waiting on a choice outranks the stage, exactly as it does for a person: until
        // it is answered there is no other move the rules will take.
        match asked with
        | Some question ->
            match question.Wanting with
            | ACard(command, targets) ->
                let chosen, rng = best (counting (worthChoosing question.Chooser command)) targets rival.Rng
                chosen |> Option.map (fun target -> Choose(TheCard(Target.card target)), { rival with Rng = rng })
            | ALine(_, offered)
            | ALineFor(_, offered) ->
                let line, rng = pick offered rival.Rng
                Some(Choose(TheLine line), { rival with Rng = rng })
            | AnOrder(_, offered) ->
                let order, rng = pick offered rival.Rng
                Some(Arrange order, { rival with Rng = rng })
            | Whether _ ->
                // Yes, for the machine that counts. A card that offers something is almost always
                // offering *you* something, and a machine that declined half of them would decline
                // half of what its own cards were for.
                if rival.Skill.Name = "easy" then
                    let said, rng = pick [ Yes; No ] rival.Rng
                    Some(Choose said, { rival with Rng = rng })
                else
                    Some(Choose Yes, rival)
            | OneOf _ ->
                let said, rng = pick [ TheFirst; TheSecond ] rival.Rng
                Some(Choose said, { rival with Rng = rng })
        | None ->

        match session.Stage with
        | Done _ -> None

        | Drafting pool ->
            match pool with
            | [] -> None
            | pool ->
                // The richest protocol left. This game is a race to ten three times over, so the
                // six cards a protocol brings are worth exactly what they add up to - which is the
                // one thing about a protocol a machine can judge without reading ninety cards.
                let whole protocol =
                    Card.inProtocol protocol |> List.sumBy (fun card -> card.Value)

                let taken, rng = best (counting whole) pool rival.Rng
                taken |> Option.map (fun taken -> Take taken, { rival with Rng = rng })

        | Arranging ->
            let drafted = (Session.side (Session.active session) session).Drafted

            if List.length drafted <> Protocol.Each then
                None
            else
                let order, rng = shuffled drafted rival.Rng
                Some(Arrange order, { rival with Rng = rng })

        | Playing ->
            let seat = session.ToPlay

            // Every way every card in hand could legally be laid: face up wherever its protocol
            // is, face down anywhere, and neither where something on the table forbids it.
            //
            // Worked out from what the rules would take rather than guessed at - a machine whose
            // move was refused would be asked again, and again, with the turn never passing and
            // nothing on the screen to say why. Which is exactly what happened the moment cards
            // learned to shut a line: this list is the machine's copy of the legality test, and
            // it has to be kept honest by the same `Field` that answers it for a person.
            //
            // **The whole hand at once**, which is the other half of what `medium` is: the machine
            // that picked a card first and then a place for it could only ever choose the best
            // place for an arbitrary card.
            let open' line face =
                (Field.barred seat line face session.Field).IsNone

            let ways =
                [ for card in (Session.side seat session).Hand do
                      for line in Field.facingLines seat card session.Field do
                          if open' line FaceUp then
                              card, line, FaceUp

                      for line in Lines.all do
                          if open' line FaceDown then
                              card, line, FaceDown ]

            match best (counting (worthPlaying seat session)) ways rival.Rng with
            // Nothing in hand, or every line shut against all of it - which a pair of cards can
            // really do. Either way there is still an action to take, and it is the one that gets
            // a different hand.
            | None, _ -> Some(Refresh, rival)
            | Some(card, line, face), rng -> Some(Play(card, line, face), { rival with Rng = rng })

    // --- the one on offer -----------------------------------------------------------------

    let easy =
        { Name = "easy"
          Describe = "drafts, arranges and plays at random - a seat filled, not an opponent" }

    let medium =
        { Name = "medium"
          Describe =
            "counts: drafts the richest protocols, plays for the line nearest compiling, and will not spend a five as a two" }

    let all = [ easy; medium ]

    let names = all |> List.map (fun skill -> skill.Name) |> String.concat ", "

    let byName (name: string) =
        let wanted = name.ToLowerInvariant()

        match all |> List.tryFind (fun skill -> skill.Name = wanted) with
        | Some skill -> Ok skill
        | None -> Error $"'{name}' is not a machine I have. There is {names}."

    /// Seat the machines named - one entry per seat, in dealing order, naming the skill or
    /// nobody - each with a generator of its own drawn from the deal and from where the seat
    /// sits, so that moving a machine along a seat hands it the generator that seat has
    /// always had.
    let seating (seed: uint64) sitting =
        Session.seats
        |> List.indexed
        |> List.choose (fun (place, seat) ->
            sitting
            |> List.tryItem place
            |> Option.flatten
            |> Option.map (fun skill ->
                seat,
                { Skill = skill
                  Rng = Rng.ofSeed (seed + uint64 place) }))
