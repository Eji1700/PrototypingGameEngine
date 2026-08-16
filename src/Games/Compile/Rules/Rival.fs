namespace TCModel.Compile

open TCModel.Common
open TCModel.Engine

/// How well a machine plays.
///
/// **Four, and each one differs from the last by something a player can see.** For a long while
/// there was only one, and that was the honest state of the rules rather than modesty: a machine
/// that plays *better* needs something to be better *at*, and until the ninety cards were in there
/// was no win to steer towards and no stack worth more than another.
///
/// There is now, and it comes in three layers:
///
///   * `medium` **counts.** A line at ten and ahead compiles, three compiles win, and a card is
///     worth what is printed on it face up and two face down. That is enough for arithmetic.
///   * `hard` **reads.** Arithmetic cannot see what a card says, so this one weighs the text -
///     structurally, out of the command tree, never out of a table of ninety cards.
///   * `deep` **stops guessing.** It plays the move out on a copy of the game and looks at the
///     board it leaves, so what a card does is answered by the rules that resolve it.
///
/// Four rather than the three the other games here carry, because each of these earns its rung:
/// over four hundred deals from both seats, `medium` takes ninety-nine games in a hundred from
/// `easy`, `hard` about three in five from `medium`, and `deep` about seven in ten from `hard`.
/// A rung that could not show that would be a word in a list.
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

    // --- and what a machine that reads is reading ----------------------------------------------
    //
    // **Structural, not a table of ninety cards.** A card's text is data, which is the whole reason
    // this file can have an opinion about it: the machine walks the same `Command` tree that
    // `Words` prints and `Faults` checks, so a card written tomorrow is weighed tomorrow without
    // anybody coming back here. A ninety-entry lookup would be ninety more things to keep in step.
    //
    // The numbers are in the same currency as a point of line value, and they are estimates rather
    // than truths - a card in hand is worth about three, a card of theirs off the table about five.
    // What matters is not that they are right but that they are **relative**, and that no card is
    // scored twice.

    /// Which side a command is aimed at, which is most of what it is worth: taking a card off
    /// **their** table is a gain and off yours is a cost, and it is the same command either way.
    let private against selector cost gain =
        match selector.Whose with
        | Yours -> cost
        | Theirs
        | Anyone -> gain

    /// What one command is worth to whoever carries it out.
    let rec private weighing command =
        match command with
        | Draw(Just n) -> 3 * n
        | Draw _ -> 3
        | Discard -> -3
        | Delete selector -> against selector -4 5
        | Return selector -> against selector -2 4
        | Flip selector -> against selector 1 2
        | Shift(selector, _) -> against selector 1 1
        | Refreshing' -> 2
        // A card onto the table that did not cost the turn, which is the cheapest thing in the game.
        | FromDeck _ -> 2
        | UnderThis _ -> 2
        | PlayFromHand _ -> 4
        | Give -> -2
        | TakeAtRandom -> 3
        | TakeTheirTop -> 3
        // A turn where they cannot compile is a turn of yours that cannot be answered.
        | StopTheirCompile -> 8
        | Reveal -> -1
        | RevealTheirHand -> 2
        | Show _ -> 1
        | Swap -> 1
        | Rearrange Yours -> 1
        | Rearrange _ -> 2
        | OneOrMore inner -> weighing inner
        | Times(Just n, inner) -> n * weighing inner
        | Times(_, inner) -> 2 * weighing inner
        | Every inner
        | InEachOtherLine inner
        | InEachLineHolding inner -> 2 * weighing inner
        | InAChosenLine inner
        | InAChosenLineOf(_, inner) -> weighing inner
        // You may decline, so an offer is never a cost - which is exactly what the rules say.
        | May inner -> max 0 (weighing inner)
        | Either(first, second) -> max (weighing first) (weighing second)
        // Halved, because it only happens if the first half did.
        | IfYouDo(first, rest) -> weighing first + (rest |> List.sumBy weighing) / 2
        | IfCovering rest -> (rest |> List.sumBy weighing) / 2
        // Done by them, so every sign in it turns over.
        | Opposing inner -> -(weighing inner)

    /// And what one standing rule is worth, for as long as it stands.
    let private weighingRule =
        function
        | FaceDownWorth n -> (n - Placed.FaceDownValue) * 2
        | LinePlus n
        | LinePlusPerFaceDown n
        | TheirLineMinus n -> n * 2
        | TheyCannotPlayHere -> 6
        | TheyMustPlayFaceDown -> 6
        | YouMayPlayAnywhere -> 5
        | TheyCannotPlayFaceDownHere -> 4
        | SkipsCacheCheck -> 3
        | Silence -> 2

    /// What a card's whole text is worth to its owner, standing face up and uncovered.
    ///
    /// The boxes are not weighed alike, and the reason is the same one the boxes exist for: a rule
    /// in the top box survives being built on and one in the bottom box does not, and a trigger is
    /// worth what it is worth only when the thing it listens for happens.
    let private weighingText card =
        let text = Printed.on card

        [ text.Top |> List.sumBy weighingRule
          text.Shown |> List.sumBy weighing
          text.Bottom |> List.sumBy weighingRule
          text.AtStart |> List.sumBy weighing
          text.AtEnd |> List.sumBy weighing
          // Halved: these wait on something that may never happen.
          (text.After |> List.collect snd |> List.sumBy weighing) / 2
          (text.WhenCovered |> List.sumBy weighing) / 2
          (text.WhenFlipped |> List.sumBy weighing) / 2
          (text.WhenCompiled |> List.sumBy weighing) / 2 ]
        |> List.sum

    /// And what a card loses by being built on: the boxes that go quiet under a cover.
    let private weighingUncovered card =
        let text = Printed.on card

        (text.Bottom |> List.sumBy weighingRule)
        + (text.AtStart |> List.sumBy weighing)
        + (text.AtEnd |> List.sumBy weighing)

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

    // --- and what a machine that looks ahead is looking at ---------------------------------------
    //
    // `deep` weighs no card at all. It **plays the move out on a copy of the game** and looks at
    // the board it leaves - so what a card does is answered by the rules that resolve it rather
    // than by an estimate of what its words are worth. A draw shows up as cards in hand; a delete
    // shows up as their line dropping; a card whose text fizzles shows up as nothing, which is
    // exactly what it was.
    //
    // It sees further than "one move" sounds. Resolving a play runs the rest of the turn and then
    // the opponent's turn *begins* - the component taken, every won line compiled - so the board it
    // scores is one where the answer to a careless move has already been paid for. What it stops
    // short of is the card they choose to *play*, and that is where it should stop: their hand is
    // hidden, half the table is face down, and every draw is a shuffle, so a machine reaching past
    // it would be searching a game it cannot see.
    //
    // **It also stops at the first question they have to answer**, which leaves the best half of a
    // card like *"your opponent discards 1 card"* unpaid: the board it scores is one on which they
    // have not discarded yet. Paying for it with `hard`'s estimate at that boundary - search while
    // searching is sound, weigh where it is not - was written, measured and taken out again: over
    // eight hundred deals it moved the record by six games, which is inside the noise, and on a
    // posed board it did not change so much as which card was chosen.

    /// How well that seat is standing, in the only terms the game settles anything by.
    let private standingIn seat session =
        let them = Session.other seat
        let mine = Session.side seat session
        let theirs = Session.side them session

        [ // Three of these ends it, so nothing else on the board is worth one.
          (Set.count mine.Compiled - Set.count theirs.Compiled) * 100

          for line in Lines.all do
              let ours = Field.valueOn seat line session.Field
              let across = Field.valueOn them line session.Field

              // Past ten is no better than ten: a line at fourteen compiles exactly as hard as a
              // line at ten, and the four over are four cards that could have been somewhere else.
              min ours Stack.ToCompile * 2 - min across Stack.ToCompile * 2

              if Field.won seat line session.Field then 30
              if Field.won them line session.Field then -30

          (List.length mine.Hand - List.length theirs.Hand) * 3 ]
        |> List.sum

    /// How that seat would answer a question, when nothing about it is a matter of chance.
    ///
    /// Shared, because a machine that looked a move ahead has to answer its own questions inside
    /// the move it is looking at - and it should answer them there exactly as it would answer them
    /// for real, or it is looking at a game it will not go on to play.
    let private answering question =
        match question.Wanting with
        | ACard(command, targets) ->
            targets
            |> List.sortByDescending (worthChoosing question.Chooser command)
            |> List.tryHead
            |> Option.map (fun target -> Choose(TheCard(Target.card target)))
        | ALine(_, offered)
        | ALineFor(_, offered) -> offered |> List.tryHead |> Option.map (TheLine >> Choose)
        | AnOrder(_, offered) -> offered |> List.tryHead |> Option.map Arrange
        | Whether inner -> Some(Choose(if weighing inner > 0 then Yes else No))
        | OneOf(first, second) -> Some(Choose(if weighing second > weighing first then TheSecond else TheFirst))

    /// The move played out on a copy of the game, answering our own questions along the way.
    ///
    /// It stops at anything the **other** seat has to answer, which is the honest place to stop:
    /// what they will say is their business, and a machine that guessed for them would be reading
    /// a hand it cannot see.
    let private after seat move session =
        let rec settle session fuel =
            if fuel = 0 then
                session
            else
                match Session.asking session with
                | Some question when question.Chooser = seat ->
                    match answering question with
                    | Some answer ->
                        match Turn.asked answer session with
                        | Some next, _ -> settle next (fuel - 1)
                        | None, _ -> session
                    | None -> session
                | _ -> session

        match Turn.asked move session with
        | Some next, _ -> Some(settle next 40)
        | None, _ -> None

    /// Which move a machine plays, and the machine as it then stands.
    ///
    /// Three stages and therefore three answers, each of them picked out of what the rules
    /// would actually take - a machine that guessed and was refused would be asked again, and
    /// again, with the turn never passing and nothing on the screen to say why.
    let plays session rival =
        // Everything above `easy` counts; everything above `medium` reads; and the last of them
        // stops reading and looks instead.
        let reads = rival.Skill.Name <> "easy" && rival.Skill.Name <> "medium"
        let looks = rival.Skill.Name = "deep"

        // Nothing at all, for the machine that plays at random - so every `best` below collapses
        // to a draw out of the hat, and all three machines share one set of legal moves.
        let counting score =
            if rival.Skill.Name = "easy" then (fun _ -> 0) else score

        // And what the card would *say*, for the machine that reads - on top of what it is worth,
        // never instead of it. A five with nothing printed on it is still a five.
        let readingPlay seat (card, line, face) =
            if not reads || face = FaceDown then
                0
            else
                // Playing over your own card silences its bottom box, which is a cost the counting
                // machine cannot see: covering your own Light-1 costs you a card a turn.
                let covering =
                    match Stack.uncovered (Side.stack line (Field.side seat session.Field)) with
                    | Some under when Placed.isFaceUp under -> weighingUncovered under.Card
                    | _ -> 0

                weighingText card - covering

        // A card waiting on a choice outranks the stage, exactly as it does for a person: until
        // it is answered there is no other move the rules will take.
        match Session.asking session with
        | Some question ->
            match question.Wanting with
            | ACard(command, targets) ->
                let chosen, rng =
                    best (counting (worthChoosing question.Chooser command)) targets rival.Rng

                chosen
                |> Option.map (fun target -> Choose(TheCard(Target.card target)), { rival with Rng = rng })
            | ALine(_, offered)
            | ALineFor(_, offered) ->
                let line, rng = pick offered rival.Rng
                Some(Choose(TheLine line), { rival with Rng = rng })
            | AnOrder(_, offered) ->
                let order, rng = pick offered rival.Rng
                Some(Arrange order, { rival with Rng = rng })
            // The two questions that are pure judgement: no card to pick and no line to pick, only
            // whether, and which. `hard` weighs what is on offer; `medium` says yes and takes the
            // first, which is right more often than it is wrong because a card that offers
            // something is almost always offering it to *you*.
            //
            // Which is also why weighing them here shows up in no measurement: `medium` was
            // already right nearly every time. It is kept because it is the *reason* rather than
            // the guess - a card that offers you a bad bargain should be declined on its merits.
            | Whether inner ->
                if rival.Skill.Name = "easy" then
                    let said, rng = pick [ Yes; No ] rival.Rng
                    Some(Choose said, { rival with Rng = rng })
                elif reads && weighing inner <= 0 then
                    Some(Choose No, rival)
                else
                    Some(Choose Yes, rival)
            | OneOf(first, second) ->
                if rival.Skill.Name = "easy" then
                    let said, rng = pick [ TheFirst; TheSecond ] rival.Rng
                    Some(Choose said, { rival with Rng = rng })
                elif reads && weighing second > weighing first then
                    Some(Choose TheSecond, rival)
                else
                    Some(Choose TheFirst, rival)
        | None ->

        match session.Stage with
        | Done _ -> None

        | Drafting pool ->
            match pool with
            | [] -> None
            | pool ->
                // The richest protocol left. This game is a race to ten three times over, so the
                // six cards a protocol brings are worth exactly what they add up to.
                //
                // **`hard` drafts on this too, and not on what the cards say** - which is a
                // measurement rather than an omission. Adding the text weights here made the
                // machine markedly *worse*: value totals are already a sharp signal (Love 21,
                // Gravity 18, Metal 17, and the other twelve tied at 15), and the weights are too
                // coarse to improve on them without drowning them. They earn their keep at a play,
                // where the choice is between cards already in hand.
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
                          if open' line FaceUp then card, line, FaceUp

                      for line in Lines.all do
                          if open' line FaceDown then card, line, FaceDown ]

            // The machine that looks ahead ignores every estimate above and plays the move out:
            // the board it leaves is the score. It keeps the counting machine's arithmetic only as
            // a tie-break, because two moves that leave the same board are still not equal - one
            // of them may have spent a five to get there.
            let score (card, line, face) =
                if not looks then
                    worthPlaying seat session (card, line, face)
                    + readingPlay seat (card, line, face)
                else
                    match after seat (Play(card, line, face)) session with
                    | Some played -> standingIn seat played * 4 + worthPlaying seat session (card, line, face)
                    | None -> System.Int32.MinValue

            match best (counting score) ways rival.Rng with
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

    let hard =
        { Name = "hard"
          Describe = "counts, and reads the cards - it plays for what a card says as well as for what it is worth" }

    let deep =
        { Name = "deep"
          Describe = "plays every move out on a copy of the game and keeps the one that leaves the best board" }

    let all = [ easy; medium; hard; deep ]

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
