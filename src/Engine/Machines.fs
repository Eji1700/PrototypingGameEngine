namespace TCModel.Engine

open TCModel.Common

/// A seat played by the program: given where the game stands, what it plays and what it
/// becomes.
///
/// A function, and recursive, so that whatever a machine carries between turns - a
/// generator, a plan, a book of openings, nothing at all - stays its own business instead of
/// becoming a type parameter on every table, screen and seating above it. The engine only
/// ever asks one; what is inside is the game's.
///
/// Answering `None` is a machine with nothing to play, which stops the run as surely as a
/// finished game does.
[<NoComparison; NoEquality>]
type Machine<'Move, 'State> = Choosing of ('State -> ('Move * Machine<'Move, 'State>) option)

/// Seats played by something that is not a person: which machines there are, which seats
/// they take, and when they play.
///
/// What a machine *chooses* is the game's own and is nowhere near here - it comes in as one
/// function, from a seat's state to a move. Everything else about one is the same at every
/// game and is here: a machine is named by a word a person types, it takes the seats it was
/// given with a generator of its own drawn from the deal, and it plays as soon as the game
/// reaches its seat, the run of them between one person's move and their next being played
/// out before the prompt comes back.
///
/// Here rather than at either table, because both tables want it and there is one answer. A
/// machine at one keyboard and a machine at a seat nobody drove to are the same machine.
module Machines =

    // --- one machine, and the ones a game offers ---------------------------------------------
    //
    // What a skill *is* is the game's and is nowhere near here: one game weighs five things,
    // another walks nine squares to the end. The only fact about one anything above the seam
    // ever wants is the word a person types for it, so `nameOf` is the whole of what these
    // ask for.

    /// One of a game's rivals as a `Machine`: a function from where the game stands to what
    /// it plays, carrying whatever the rival carries inside it.
    ///
    /// `plays` is the whole of what a game says about a machine - a state and a rival, and a
    /// move and the rival as it then stands. Tying that into a `Machine` is the same knot at
    /// every game and was tied five times: the rival handed back is the one asked next turn,
    /// so a generator moved on stays moved on.
    let rec choosing plays rival =
        Choosing(fun state -> plays state rival |> Option.map (fun (move, next) -> move, choosing plays next))

    /// What a game's machines are called, worst to best, for saying what there is.
    let named nameOf all =
        all |> List.map nameOf |> String.concat ", "

    /// A machine by the word a person typed, or the list of the ones there are.
    let byName nameOf all (name: string) =
        let wanted = name.ToLowerInvariant()

        match all |> List.tryFind (fun skill -> nameOf skill = wanted) with
        | Some skill -> Ok skill
        | None -> Error $"'{name}' is not a machine I have. There is {named nameOf all}."

    /// Which seats the machines take, and what each of them draws its choices out of.
    ///
    /// One entry per seat named, in the order the game deals them, and nothing at all where
    /// the seat is somebody's. Said seat by seat rather than as a run of machines after the
    /// first, because which seats are the program's is a thing to be chosen and not a thing
    /// to be counted: a table of three may be a person between two machines.
    ///
    /// A generator each, drawn from the seed the game was dealt from and from where the seat
    /// sits, so a game against machines replays exactly like any other - and moving a machine
    /// along one seat gives it the generator that seat has always had.
    ///
    /// The seats come in as a list rather than a count, because which seat is third is the
    /// game's own answer: one deals them in order and another has a mark, a power or a player
    /// at each. What comes back is a skill and a generator per seat, which each game wraps in
    /// a record of its own - that record being the one part of this that is a game's business.
    let seating (seats: PlayerId list) (seed: uint64) (sitting: 'Skill option list) =
        seats
        |> List.indexed
        |> List.choose (fun (place, seat) ->
            sitting
            |> List.tryItem place
            |> Option.flatten
            |> Option.map (fun skill -> seat, skill, Rng.ofSeed (seed + uint64 place)))

    // --- and when they take their turns ------------------------------------------------------

    /// The machine at the seat to act, if that seat is one of theirs and there is still a
    /// game to play.
    let private toAct (rules: Rules<_, _, _>) rivals model =
        let standing = Model.state model

        if rules.Over standing then
            None
        else
            rivals |> List.tryFind (fst >> (=) (rules.Active standing)) |> Option.map snd

    /// Whether the game is standing at a seat a machine plays. Asked by a table walking a
    /// game backwards: a move taken back has to take the machines' answers to it back with
    /// it, or one `undo` would simply be answered again before the board was looked at.
    let holds rules rivals model =
        toAct rules rivals model |> Option.isSome

    /// What one machine plays, in the shape `answering` asks for it. Here so that the usual
    /// case - a seat held by nothing but a `Machine` - needs no lambda at the call site.
    let playing state (Choosing choose) = choose state

    let private withRival playerId rival rivals =
        rivals
        |> List.map (fun (other, was) -> if other = playerId then other, rival else other, was)

    /// Let the machines take their turns, and give back the game they left and themselves as
    /// they now stand - each having moved its own generator on.
    ///
    /// They play one after another for as long as the seat to act is one of theirs, so a line
    /// typed by a person is answered by everybody between them and their next turn - which is
    /// what sitting down opposite a machine looks like.
    ///
    /// A move that left the game exactly where it found it stops them, and that is the whole
    /// of what stops them. Nothing a machine picks should be refused - a sensible one asks
    /// the rules what they will take before it chooses - but one that had somehow found a
    /// move the rules would not have would otherwise be asked for it again, and again, with
    /// the turn never passing and nothing on the screen to say why.
    let rec answering rules plays rivals model =
        match toAct rules rivals model with
        | None -> model, rivals
        | Some rival ->
            let seat = rules.Active(Model.state model)

            match plays (Model.state model) rival with
            | None -> model, rivals
            | Some(move, rival) ->
                let next = Update.update rules (Make move) model
                let rivals = withRival seat rival rivals

                // Whether the position moved, asked of the history rather than of the two
                // states: a refused move leaves the timeline exactly as long as it was, and
                // that is a cheaper question than whether two whole games are the same one -
                // and one the engine can ask of a game it knows nothing about.
                if Timeline.movesMade next.Timeline = Timeline.movesMade model.Timeline then
                    next, rivals
                else
                    answering rules plays rivals next
