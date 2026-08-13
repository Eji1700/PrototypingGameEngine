namespace TCModel.Compile

open TCModel.Common
open TCModel.Engine

/// How well a machine plays.
///
/// One of them, and that is not modesty - it is the honest state of the rules. A machine that
/// plays *better* needs something to be better at, and what a player is trying to do at this
/// game has not been settled yet: there is no win to steer towards and no stack worth more
/// than another. So there is one seat-filler here, it plays legally and at random, and it is
/// enough to walk a whole game from the draft to the table without a second person in the
/// room.
///
/// Which is worth saying plainly rather than shipping an "easy" and a "hard" that differ by
/// nothing a player could ever notice.
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

    /// Which move a machine plays, and the machine as it then stands.
    ///
    /// Three stages and therefore three answers, each of them picked out of what the rules
    /// would actually take - a machine that guessed and was refused would be asked again, and
    /// again, with the turn never passing and nothing on the screen to say why.
    let plays session rival =
        match session.Stage with
        | Done _ -> None

        | Drafting pool ->
            match pool with
            | [] -> None
            | pool ->
                let taken, rng = pick pool rival.Rng
                Some(Take taken, { rival with Rng = rng })

        | Arranging ->
            let drafted = (Session.side (Session.active session) session).Drafted

            if List.length drafted <> Protocol.Each then
                None
            else
                let order, rng = shuffled drafted rival.Rng
                Some(Arrange order, { rival with Rng = rng })

        | Playing ->
            match (Session.side session.ToPlay session).Hand with
            | [] -> None
            | hand ->
                let card, rng = pick hand rival.Rng
                let line, rng = pick Lines.all rng
                Some(Play(card, line), { rival with Rng = rng })

    // --- the one on offer -----------------------------------------------------------------

    let easy =
        { Name = "easy"
          Describe = "drafts, arranges and plays at random - a seat filled, not an opponent" }

    let all = [ easy ]

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
