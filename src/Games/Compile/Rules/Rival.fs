namespace Prototyping.Compile

open Prototyping.Common
open Prototyping.Engine

/// What a seat does with the board before it moves: counts what a play is worth, reads what the
/// cards say, or plays the move out and looks at what is left. Each skill keeps what the one
/// below it does, and the lowest does none of them.
type Skill =
    { Name: string
      Describe: string
      Counts: bool
      Reads: bool
      Looks: bool }

type Rival = { Skill: Skill; Rng: Rng }

module Rival =

    let private best score items rng =
        match items with
        | [] -> None, rng
        | items ->
            let top = items |> List.map score |> List.max
            let chosen, rng = Rng.pick (items |> List.filter (fun item -> score item = top)) rng
            Some chosen, rng

    // A question with nothing on offer is not one the machine can answer.
    let private among offered rng =
        match offered with
        | [] -> None
        | offered -> Some(Rng.pick offered rng)


    let private worthPlaying seat session (card, line, face) =
        let mine = Field.valueOn seat line session.Field
        let theirs = Field.valueOn (Session.other seat) line session.Field

        let gained =
            match face with
            | FaceUp -> card.Value
            | FaceDown -> Placed.FaceDownValue

        let towardsWinning =
            match Side.protocolOn line (Field.side seat session.Field) with
            | Some protocol -> not (Side.hasCompiled protocol (Field.side seat session.Field))
            | None -> false

        [ gained * 2
          if mine + gained >= Stack.ToCompile && mine + gained > theirs then 20
          if towardsWinning then 4
          match face with
          | FaceDown -> -card.Value
          | FaceUp -> 0 ]
        |> List.sum


    let private against selector cost gain =
        match selector.Whose with
        | Yours -> cost
        | Theirs
        | Anyone -> gain

    /// A rough price on what a command does, for a rival that reads card text rather than just
    /// counting values. The numbers only have to rank one card against another: what matters is that
    /// deleting the other side's card is worth more than deleting your own, that a command turned
    /// back on you is worth its own negative, and that a maybe is never worth less than nothing.
    let rec private weighing command =
        match command with
        | Draw(Just n) -> 3 * n
        | Draw _ -> 3
        | Discard -> -3
        | Delete selector -> against selector -4 5
        | Return selector -> against selector -2 4
        | Flip selector -> against selector 1 2
        | Shift(selector, _) -> against selector 1 1
        | RefreshHand -> 2
        | FromDeck _ -> 2
        | UnderThis _ -> 2
        | PlayFromHand _ -> 4
        | Give -> -2
        | TakeAtRandom -> 3
        | TakeTheirTop -> 3
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
        | May inner -> max 0 (weighing inner)
        | Either(first, second) -> max (weighing first) (weighing second)
        | IfYouDo(first, rest) -> weighing first + (rest |> List.sumBy weighing) / 2
        | IfCovering rest -> (rest |> List.sumBy weighing) / 2
        | Opposing inner -> -(weighing inner)

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

    let private weighingText card =
        let text = Printed.on card

        [ text.Top |> List.sumBy weighingRule
          text.Shown |> List.sumBy weighing
          text.Bottom |> List.sumBy weighingRule
          text.AtStart |> List.sumBy weighing
          text.AtEnd |> List.sumBy weighing
          (text.After |> List.collect snd |> List.sumBy weighing) / 2
          (text.WhenCovered |> List.sumBy weighing) / 2
          (text.WhenFlipped |> List.sumBy weighing) / 2
          (text.WhenCompiled |> List.sumBy weighing) / 2 ]
        |> List.sum

    let private weighingUncovered card =
        let text = Printed.on card

        (text.Bottom |> List.sumBy weighingRule)
        + (text.AtStart |> List.sumBy weighing)
        + (text.AtEnd |> List.sumBy weighing)

    let private worthChoosing seat command target =
        let value = (Target.card target).Value
        let theirs = Target.owner target <> seat

        match command with
        | Delete _
        | Return _
        | Shift _ -> if theirs then value else -value
        | Discard
        | Give
        | Reveal
        | Show _ -> -value
        | PlayFromHand _ -> value
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


    let private standingIn seat session =
        let them = Session.other seat
        let mine = Session.side seat session
        let theirs = Session.side them session

        [ (Set.count mine.Compiled - Set.count theirs.Compiled) * 100

          for line in Lines.all do
              let ours = Field.valueOn seat line session.Field
              let across = Field.valueOn them line session.Field

              min ours Stack.ToCompile * 2 - min across Stack.ToCompile * 2

              if Field.won seat line session.Field then 30
              if Field.won them line session.Field then -30

          (List.length mine.Hand - List.length theirs.Hand) * 3 ]
        |> List.sum

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

    /// The board as it would stand after a move, with the rival answering its own questions along the
    /// way - a card that asks something mid-resolution would otherwise leave the game stopped and the
    /// position unfinished. Only `deep` pays for this.
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

    let plays session rival =
        let reads = rival.Skill.Reads
        let looks = rival.Skill.Looks

        let counting score =
            if rival.Skill.Counts then score else (fun _ -> 0)

        let readingPlay seat (card, line, face) =
            if not reads || face = FaceDown then
                0
            else
                let covering =
                    match Stack.uncovered (Side.stack line (Field.side seat session.Field)) with
                    | Some under when Placed.isFaceUp under -> weighingUncovered under.Card
                    | _ -> 0

                weighingText card - covering

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
                among offered rival.Rng
                |> Option.map (fun (line, rng) -> Choose(TheLine line), { rival with Rng = rng })
            | AnOrder(_, offered) ->
                among offered rival.Rng
                |> Option.map (fun (order, rng) -> Arrange order, { rival with Rng = rng })
            | Whether inner ->
                if not rival.Skill.Counts then
                    let said, rng = Rng.pick [ Yes; No ] rival.Rng
                    Some(Choose said, { rival with Rng = rng })
                elif reads && weighing inner <= 0 then
                    Some(Choose No, rival)
                else
                    Some(Choose Yes, rival)
            | OneOf(first, second) ->
                if not rival.Skill.Counts then
                    let said, rng = Rng.pick [ TheFirst; TheSecond ] rival.Rng
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
                let whole protocol =
                    Card.inProtocol protocol |> List.sumBy (fun card -> card.Value)

                let taken, rng = best (counting whole) pool rival.Rng
                taken |> Option.map (fun taken -> Take taken, { rival with Rng = rng })

        | Arranging ->
            let drafted = (Session.side (Session.active session) session).Drafted

            if List.length drafted <> Protocol.Each then
                None
            else
                let order, rng = Rng.shuffle drafted rival.Rng
                Some(Arrange order, { rival with Rng = rng })

        | Playing ->
            let seat = session.ToPlay

            let open' line face =
                (Field.barred seat line face session.Field).IsNone

            let ways =
                [ for card in (Session.side seat session).Hand do
                      for line in Field.facingLines seat card session.Field do
                          if open' line FaceUp then card, line, FaceUp

                      for line in Lines.all do
                          if open' line FaceDown then card, line, FaceDown ]

            let score (card, line, face) =
                if not looks then
                    worthPlaying seat session (card, line, face)
                    + readingPlay seat (card, line, face)
                else
                    match after seat (Play(card, line, face)) session with
                    | Some played -> standingIn seat played * 4 + worthPlaying seat session (card, line, face)
                    | None -> System.Int32.MinValue

            match best (counting score) ways rival.Rng with
            | None, _ -> Some(Refresh, rival)
            | Some(card, line, face), rng -> Some(Play(card, line, face), { rival with Rng = rng })


    let easy =
        { Name = "easy"
          Describe = "drafts, arranges and plays at random - a seat filled, not an opponent"
          Counts = false
          Reads = false
          Looks = false }

    let medium =
        { Name = "medium"
          Describe =
            "counts: drafts the richest protocols, plays for the line nearest compiling, and will not spend a five as a two"
          Counts = true
          Reads = false
          Looks = false }

    let hard =
        { Name = "hard"
          Describe = "counts, and reads the cards - it plays for what a card says as well as for what it is worth"
          Counts = true
          Reads = true
          Looks = false }

    let deep =
        { Name = "deep"
          Describe = "plays every move out on a copy of the game and keeps the one that leaves the best board"
          Counts = true
          Reads = true
          Looks = true }

    let all = [ easy; medium; hard; deep ]

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    let seating (seed: uint64) sitting _ =
        Machines.seating Session.seats seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng })
