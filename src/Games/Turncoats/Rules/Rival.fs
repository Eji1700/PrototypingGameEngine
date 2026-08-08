namespace TCModel.Turncoats

open TCModel.Common
open TCModel.Engine

/// What a machine weighs a position by, and how much each thing counts for.
///
/// These are the game's own winning conditions with a number against each, and nothing
/// else: land first, the Axe and the Flag behind it to break a tie between factions, and
/// then the two things that decide which player carried the faction that won - the stones
/// of it you are still holding, and the stones of the other two that count against you.
///
/// So there is no strategy written down anywhere here. There is a statement of what winning
/// is, which the rules already say, and a set of weights saying how much a machine cares
/// about each part of it. Changing how one plays is changing a number.
type Weights =
    {
        /// Regions ruled by the faction this seat is backing.
        Land: int
        /// Standing *inside* a region, short of ruling it: how far ahead of the next colour
        /// its stones are there, counted over the whole map.
        ///
        /// Ruling is a step, and most moves do not take one - so weighed on land alone, nearly
        /// every move a machine could make is worth exactly what every other one is, and it
        /// picks between them by drawing lots. This is the slope up to the step. It is what
        /// tells a machine that a second stone in a region it is one behind in was worth
        /// playing, and that a third stone in a region it already rules was not.
        Nudge: int
        /// Stones of it in the Axe, and in the Flag: the two tie-breakers between factions.
        Axe: int
        Flag: int
        /// Stones of it still in the bag.
        ///
        /// This is the one that decides which *player* carried the winning faction, and it is
        /// worth more than it looks: a bag played out to nothing wins nothing at all, whoever
        /// carried the board. Set high against `Land` it makes a machine that plays a stone
        /// only where the stone really buys something, which is close to how the game rewards
        /// being played. Set low it makes one that empties its bag onto the map and draws.
        Held: int
        /// Stones of the other two colours in the bag, which count against a player at the
        /// end. Subtracted, so this is how much they are disliked.
        Spare: int
    }

/// How well a machine plays.
///
/// One machine, three sets of numbers. Everything below reads these and nothing reads the
/// name, so a fourth way of playing is a fourth entry in the list at the foot of this file
/// and no other change anywhere.
type Skill =
    {
        Name: string
        Describe: string
        Weighs: Weights
        /// How often it throws its own judgement away and plays anything the rules allow
        /// instead, in hundredths. A hundred is a machine that never thinks at all.
        Careless: int
        /// How many of its own best moves it follows up by asking what the seat after it
        /// could do about them. Nought is a machine that never looks past its own turn.
        Ahead: int
    }

/// A machine at a seat: how well it plays, and the generator its own choices come out of.
///
/// The generator travels with it, the same way the game's own does, so a machine picking
/// between two moves it likes equally is still a fold: deal the same game against the same
/// machines and the same game comes back.
type Rival = { Skill: Skill; Rng: Rng }

/// A seat played by the program rather than by a person.
///
/// It is held to exactly what a player is held to. It asks the rules what is allowed rather
/// than keeping a second opinion about it, it reads the map and its own bag and nothing
/// else, and what it picks is a `Move` - the very thing a typed line turns into - so a
/// machine cannot make a move that nobody could have typed.
module Rival =

    // --- what a position is worth ------------------------------------------------------

    /// How far ahead in a region is as good as being well ahead in it. Past this the stones
    /// are doing nothing they were not already doing, and would be worth more somewhere else.
    [<Literal>]
    let private Reach = 2

    /// The faction this seat is playing for.
    ///
    /// Whichever faction carries the board, the player who carried it best is the one left
    /// holding most of its stones. So a machine backs whatever it already holds most of and
    /// everything after this is in aid of that one colour.
    let private backing bag =
        StoneColor.all |> List.maxBy (fun color -> Pile.count color bag)

    /// How good a position looks to a player backing `backed` and holding `bag`.
    ///
    /// The map and one bag. That is not a simplification for the sake of a simpler
    /// function: it is the whole of what somebody sitting at this seat is allowed to see,
    /// and saying so in the arguments is what stops a machine playing better than a person
    /// could by reading a bag it was never shown. There is nothing here it could cheat
    /// with, because there is nothing here to cheat with.
    let private appraise weights backed position bag =
        let ruled = Ruling.standings position
        let axe = Position.stones Board.axe position
        let flag = Position.stones Board.flag position

        /// How far ahead of the best of the other two, which is the question every cascade
        /// in the rules actually asks. Ruling six regions is worth nothing if somebody else
        /// rules seven.
        let over count =
            count backed
            - (StoneColor.all |> List.filter ((<>) backed) |> List.map count |> List.max)

        /// The same question asked of every region on the map at once, and kept small, so
        /// that a stone put down where it does some good is worth more than a stone put down
        /// where it does none - which land ruled, on its own, cannot tell apart.
        let ground =
            Board.landRegions
            |> List.sumBy (fun region ->
                let standing = Position.stones region.Id position

                max -Reach (min Reach (over (fun color -> Pile.count color standing))))

        weights.Land * over (fun color -> ruled[color])
        + weights.Nudge * ground
        + weights.Axe * over (fun color -> Pile.count color axe)
        + weights.Flag * over (fun color -> Pile.count color flag)
        + weights.Held * Pile.count backed bag
        - weights.Spare * (Pile.total bag - Pile.count backed bag)

    // --- what there is to choose from ---------------------------------------------------

    /// Every way of taking `wanted` stones out of what is standing there, colour by colour.
    /// A battle that could drive out more than one arrangement of stones is a real choice,
    /// and the rules make the attacker name it, so the machine has to have them all in front
    /// of it to name one.
    let rec private choosing wanted standing =
        match wanted, standing with
        | 0, _ -> [ [] ]
        | _, [] -> []
        | _, (color, held) :: rest ->
            [ for taken in 0 .. min wanted held do
                  for rest in choosing (wanted - taken) rest do
                      yield List.replicate taken color @ rest ]

    let private openRegions =
        Board.regions |> List.filter (fun region -> RegionKind.isOpen region.Kind)

    /// Regions an action may be aimed at: open, and part of the map.
    let private aimable =
        openRegions
        |> List.filter (fun region -> not (RegionKind.isIsolated region.Kind))

    /// Which stones a battle could drive out of a region, as the rules would take it.
    ///
    /// Always the most it is allowed to, because driving out fewer is never worth less. The
    /// choice worth making is *whose*, and that is what comes back with more than one entry
    /// in it. Where there is nothing to choose the rules are left to work it out themselves,
    /// so the machine writes `battle r 3` where a person would rather than spelling out an
    /// answer that was never in doubt.
    let private casualties color regionId position =
        let standing = Position.stones regionId position
        let allowed = Pile.count color standing

        let losing =
            Pile.toCounts standing |> List.filter (fun (other, _) -> other <> color)

        let available = losing |> List.sumBy snd

        if allowed < 1 || available < 1 then
            []
        else
            match choosing (min allowed available) losing with
            | []
            | [ _ ] -> [ AsManyAsAllowed ]
            | ways -> ways |> List.map These

    /// Every move a seat holding `bag` could ask for on this position.
    ///
    /// Asked for, not allowed: this is the shape of the four actions and no more. Whether
    /// the rules would actually take one of these is not decided here and is not guessed at
    /// - it is settled below by asking them.
    let private candidates position bag =
        let holding = Pile.toCounts bag |> List.map fst

        [ for color in holding do
              for region in openRegions do
                  yield Recruit(color, region.Id)

          for color in holding do
              for region in aimable do
                  for driven in casualties color region.Id position do
                      yield Battle(color, region.Id, driven)

          for color in holding do
              for from in aimable do
                  let marching = Pile.count color (Position.stones from.Id position)

                  for into in Board.neighbours from.Id do
                      if RegionKind.isOpen (Board.region into).Kind then
                          for count in 1..marching do
                              yield March(color, from.Id, into, count) ]

    /// What a move would leave behind, or nothing if the rules would not have it.
    ///
    /// The rules themselves are asked. A machine that worked out for itself which moves are
    /// legal would be a second copy of the rules, free to drift from the ones being played -
    /// and the way that shows up is a machine asking for something at the table and being
    /// told no, over and over, with the turn never passing.
    ///
    /// Negotiating is the one that cannot be tried. A draw comes out of the generator inside
    /// the game, so trying one would show the machine the stone it drew before it decided
    /// whether to draw - which is the one thing a person in that seat does not get to know.
    let private trying move game =
        let taken outcome =
            outcome |> Result.map fst |> Result.toOption

        match move with
        | Recruit(color, into) -> taken (Actions.recruit color into game)
        | Battle(color, target, driven) -> taken (Actions.battle color target driven game)
        | March(color, from, into, count) -> taken (Actions.march color from into count game)
        | Settle color -> taken (Actions.settle color game)
        | Negotiate
        | Resign -> None

    // --- looking past your own turn -------------------------------------------------------

    /// The game as it would stand if the next player held everything nobody can point at.
    ///
    /// A machine cannot see another player's bag, and it does not guess at one. It assumes
    /// the worst instead: that the seat about to act is holding every stone that is neither
    /// on the map nor in its own bag - which is exactly what `Knowledge` says is out there
    /// somewhere. Pessimistic, and honest. It never plays better for knowing something it
    /// was not told, only more carefully.
    let private handedOn me game =
        let unseen = (Knowledge.seenBy me game).Unseen

        let handed =
            { game with
                Table = Table.advance game.Table }

        handed |> Game.withActive { Game.active handed with Bag = unseen }

    // --- choosing ---------------------------------------------------------------------------

    /// The move the machine makes, and the machine with its generator moved on. Nothing
    /// comes back when there is not a single thing the rules would let this seat do, which
    /// is a seat holding nothing - and the game steps over those before they are ever asked.
    let plays (play: Play) rival =
        let game = play.Game
        let me = Game.active game
        let backed = backing me.Bag

        let worth position bag =
            appraise rival.Skill.Weighs backed position bag

        /// What a move is worth, played out. The bag read afterwards is this seat's own
        /// still: an action changes the player who took it and nobody else.
        let played move =
            trying move game
            |> Option.map (fun after -> worth after.Position (Game.active after).Bag)

        /// What a negotiation is worth, which is the one thing that has to be valued rather
        /// than tried.
        ///
        /// It is valued at what it is hoping for: the map left exactly as it stands, and a
        /// spare stone traded for one of its own colour. Holding nothing spare it hopes for
        /// nothing, and the value is the position as it already is - which is the important
        /// half. A turn spent not playing is a real option and has to be on the table beside
        /// the others, or a machine with nothing worth doing will do something anyway, and
        /// keep on doing it until its bag is empty and the game is drawn out from under it.
        let hoped () =
            if Pile.total game.Reserve < 1 || Pile.isEmpty me.Bag then
                None
            else
                match
                    StoneColor.all
                    |> List.filter (fun color -> color <> backed && Pile.count color me.Bag > 0)
                with
                | [] -> Some(worth game.Position me.Bag)
                | spare :: _ -> Some(worth game.Position (me.Bag |> Pile.remove spare 1 |> Pile.add backed 1))

        /// The worst the seat after this one could leave the position looking, supposing it
        /// can do anything at all. Weighed from this seat and in this seat's terms - what is
        /// being asked is not how good their move is for them, but how much of this one it
        /// takes back.
        let answered move =
            match trying move game with
            | None -> None
            | Some after ->
                let mine = (Game.active after).Bag
                let theirs = handedOn me after

                candidates theirs.Position (Game.active theirs).Bag
                |> List.choose (fun reply -> trying reply theirs |> Option.map (fun stood -> worth stood.Position mine))
                |> function
                    | [] -> Some(worth after.Position mine)
                    | answers -> Some(List.min answers)

        let offered =
            match play.Phase with
            // A stone has to go back before anything else can happen, so the only question
            // is which - and unlike the draw itself, that is a move like any other.
            | AwaitingReturn _ -> Pile.toCounts me.Bag |> List.map (fst >> Settle)
            | AwaitingAction -> candidates game.Position me.Bag @ [ Negotiate ]

        let weighed =
            offered
            |> List.choose (fun move ->
                match move with
                | Negotiate -> hoped () |> Option.map (fun worth -> move, worth)
                | _ -> played move |> Option.map (fun worth -> move, worth))

        // Only the handful it likes best are worth the cost of asking what could be done
        // about them, and a move it would never play is not made better by the answer.
        let weighed =
            if rival.Skill.Ahead < 1 then
                weighed
            else
                weighed
                |> List.sortByDescending snd
                |> List.truncate rival.Skill.Ahead
                |> List.map (fun (move, worth) -> move, answered move |> Option.defaultValue worth)

        match weighed with
        | [] -> None
        | weighed ->
            let careless, rng = Rng.intBelow 100 rival.Rng

            let among =
                if careless < rival.Skill.Careless then
                    weighed |> List.map fst
                else
                    let best = weighed |> List.map snd |> List.max

                    weighed |> List.filter (fun (_, worth) -> worth = best) |> List.map fst

            // Between moves it likes equally it draws lots, so two machines of the same
            // skill at the same table do not play the same game twice over.
            let picked, rng = Rng.intBelow (List.length among) rng

            Some(among[picked], { rival with Rng = rng })

    // --- how well they play --------------------------------------------------------------
    //
    // Three machines, which are one machine with different numbers in it. To change how
    // `hard` plays, or to add a fourth way of playing, is to change or add a line here:
    // nothing anywhere else in the program knows what any of these words mean.

    /// Land, and what is in the bag. A machine weighing this much is playing the board in
    /// front of it and has not thought about how a board nobody carried outright is settled.
    let private roughly =
        { Land = 10
          Nudge = 1
          Axe = 0
          Flag = 0
          Held = 12
          Spare = 1 }

    /// The same, and the two tie-breakers that settle it.
    let private closely =
        { Land = 10
          Nudge = 1
          Axe = 4
          Flag = 3
          Held = 12
          Spare = 1 }

    let easy =
        { Name = "easy"
          Describe = "plays anything the rules allow"
          Weighs = roughly
          Careless = 100
          Ahead = 0 }

    let medium =
        { Name = "medium"
          Describe = "plays the best move it can see, and now and again does not"
          Weighs = roughly
          Careless = 15
          Ahead = 0 }

    let hard =
        { Name = "hard"
          Describe = "counts the tie-breakers too, and what you could do about it"
          Weighs = closely
          Careless = 0
          Ahead = 5 }

    /// Every way there is of playing, in the order they are offered. Read by the command
    /// line, the menu and the checks rather than any of them keeping their own list.
    let all = [ easy; medium; hard ]

    let names = all |> List.map (fun skill -> skill.Name) |> String.concat ", "

    let byName (name: string) =
        let wanted = name.ToLowerInvariant()

        match all |> List.tryFind (fun skill -> skill.Name = wanted) with
        | Some skill -> Ok skill
        | None -> Error $"'{name}' is not a way for the machine to play. There is {names}."

    /// Which seats the machines take, and what each of them draws its choices out of.
    ///
    /// One entry per seat, in the order the game deals them, and `None` where the seat is
    /// somebody's. Said seat by seat rather than as a run of machines after the first,
    /// because which seats are the program's is a thing to be chosen and not a thing to be
    /// counted: a table of three may be a person between two machines.
    ///
    /// A generator each, drawn from the seed the game was dealt from and from where the seat
    /// sits, so a game against machines replays exactly like any other - and moving a machine
    /// along one seat gives it the generator that seat has always had.
    let seating (seed: uint64) sitting game =
        Game.players game
        |> List.indexed
        |> List.choose (fun (seat, player) ->
            sitting
            |> List.tryItem seat
            |> Option.flatten
            |> Option.map (fun skill ->
                player.Id,
                { Skill = skill
                  Rng = Rng.ofSeed (seed + uint64 seat) }))

    // --- taking their turns -----------------------------------------------------------------
    //
    // Which is the same thing anybody else does with a turn: it goes through the engine's own
    // `Playing.update`, so a machine's move lands in the record and on the screen exactly as a
    // person's does. *When* they play is not this game's business at all and is not written
    // here - `Machines` answers that for any game, and what it wants from this one is a way
    // of choosing a move.
    //
    // So these two are the same two functions every table already called, bound to this
    // game's rules and this game's chooser. A table asking a machine to take its turn is
    // asking the engine, and does not know it.

    /// What the engine asks of a chooser: a state and a machine, and a move if there is one.
    /// A finished game is not asked, but a total answer is cheaper than an argument about it.
    let private taking session rival =
        match session with
        | InPlay play -> plays play rival
        | Finished _ -> None

    /// Whether the game is standing at a seat the program plays. Asked by a table walking a
    /// game backwards: a move taken back has to take the machines' answers to it back with
    /// it, or one `undo` would simply be answered again before the board was looked at.
    let holds rivals model =
        Machines.holds Playing.rules rivals model

    /// Let the machines take their turns, and give back the game they left and themselves as
    /// they now stand - each having moved its own generator on.
    let answering rivals model =
        Machines.answering Playing.rules taking rivals model
