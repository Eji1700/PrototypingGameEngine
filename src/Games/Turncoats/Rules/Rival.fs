namespace Prototyping.Turncoats

open Prototyping.Common
open Prototyping.Engine

type Weights =
    { Land: int
      Nudge: int
      Axe: int
      Flag: int
      Held: int
      Spare: int }

type Skill =
    { Name: string
      Describe: string
      Weighs: Weights
      Careless: int
      Ahead: int }

type Rival = { Skill: Skill; Rng: Rng }

module Rival =


    [<Literal>]
    let private Reach = 2

    let private backing bag =
        StoneColour.all |> List.maxBy (fun colour -> Pile.count colour bag)

    /// What the board is worth to whoever is backing `backed`, as a lead over the best of the other
    /// two colours rather than as a bare count - a colour is only winning relative to its rivals.
    let private appraise weights backed position bag =
        let ruled = Ruling.standings position
        let axe = Position.stones Board.axe position
        let flag = Position.stones Board.flag position

        let over count =
            count backed
            - (StoneColour.all |> List.filter ((<>) backed) |> List.map count |> List.max)

        let ground =
            Board.landRegions
            |> List.sumBy (fun region ->
                let standing = Position.stones region.Id position

                // Clamped, so that piling a colour into one region it already rules cannot pass for
                // progress. It is worth a nudge to be ahead somewhere, and no more than a nudge.
                max -Reach (min Reach (over (fun colour -> Pile.count colour standing))))

        weights.Land * over (fun colour -> ruled[colour])
        + weights.Nudge * ground
        + weights.Axe * over (fun colour -> Pile.count colour axe)
        + weights.Flag * over (fun colour -> Pile.count colour flag)
        + weights.Held * Pile.count backed bag
        - weights.Spare * (Pile.total bag - Pile.count backed bag)


    /// Every way of taking `wanted` stones out of the colours standing there, as a list of colours
    /// each time. Used to offer a battle every combination of casualties it could drive out.
    let rec private choosing wanted standing =
        match wanted, standing with
        | 0, _ -> [ [] ]
        | _, [] -> []
        | _, (colour, held) :: rest ->
            [ for taken in 0 .. min wanted held do
                  for rest in choosing (wanted - taken) rest do
                      yield List.replicate taken colour @ rest ]

    let private casualties colour regionId position =
        let standing = Position.stones regionId position
        let allowed = Pile.count colour standing
        let losing = Actions.losingStones colour standing
        let available = losing |> List.sumBy snd

        if allowed < 1 || available < 1 then
            []
        else
            match choosing (min allowed available) losing with
            | []
            | [ _ ] -> [ AsManyAsAllowed ]
            | ways -> ways |> List.map These

    let private candidates position bag =
        let holding = Pile.toCounts bag |> List.map fst

        [ for colour in holding do
              for region in Board.openRegions do
                  yield Recruit(colour, region.Id)

          for colour in holding do
              for region in Board.contestableRegions do
                  for driven in casualties colour region.Id position do
                      yield Battle(colour, region.Id, driven)

          for colour in holding do
              for from in Board.contestableRegions do
                  let marching = Pile.count colour (Position.stones from.Id position)

                  for into in Board.neighbours from.Id do
                      if RegionKind.isOpen (Board.region into).Kind then
                          for count in 1..marching do
                              yield March(colour, from.Id, into, count) ]

    let private trying move game =
        let taken outcome =
            outcome |> Result.map fst |> Result.toOption

        match move with
        | Recruit(colour, into) -> taken (Actions.recruit colour into game)
        | Battle(colour, target, driven) -> taken (Actions.battle colour target driven game)
        | March(colour, from, into, count) -> taken (Actions.march colour from into count game)
        | Settle colour -> taken (Actions.settle colour game)
        | Negotiate
        | Resign -> None


    /// The game as it will stand for the next player, with their bag taken to be everything this
    /// one cannot see. Bags are hidden, so looking a move ahead means guessing at one - and the
    /// unseen pool is the honest guess, since it is exactly what the rival might hold.
    ///
    /// Read off the game as it stands after the move. The mover's bag from before it still holds
    /// the stone just played, and so does the map, and a guess taken from there came out a stone
    /// short.
    let private handedOn game =
        let unseen = (Knowledge.seenBy (Game.active game) game).Unseen

        let handed =
            { game with
                Table = Table.advance game.Table }

        handed |> Game.withActive { Game.active handed with Bag = unseen }


    let plays (play: Play) rival =
        let game = play.Game
        let me = Game.active game
        let backed = backing me.Bag

        let worth position bag =
            appraise rival.Skill.Weighs backed position bag

        let hoped () =
            if Pile.total game.Reserve < 1 || Pile.isEmpty me.Bag then
                None
            else
                match
                    StoneColour.all
                    |> List.filter (fun colour -> colour <> backed && Pile.count colour me.Bag > 0)
                with
                | [] -> Some(worth game.Position me.Bag)
                | spare :: _ -> Some(worth game.Position (me.Bag |> Pile.remove spare 1 |> Pile.add backed 1))

        // One reply deep: from where the move leaves the game, take the worst the next player could
        // leave us. What makes `hard` different from `medium`, and why only the few best moves are
        // looked at this way - every move against every reply is more positions than a turn is
        // worth.
        let answered after =
            let mine = (Game.active after).Bag
            let theirs = handedOn after

            candidates theirs.Position (Game.active theirs).Bag
            |> List.choose (fun reply -> trying reply theirs |> Option.map (fun stood -> worth stood.Position mine))
            |> function
                | [] -> worth after.Position mine
                | answers -> List.min answers

        let offered =
            match play.Phase with
            | AwaitingReturn _ -> Pile.toCounts me.Bag |> List.map (fst >> Settle)
            | AwaitingAction -> candidates game.Position me.Bag @ [ Negotiate ]

        // Each move is tried once, and the game it leaves is kept beside its worth, since a reply
        // is looked for from that same game.
        let weighed =
            offered
            |> List.choose (fun move ->
                match move with
                | Negotiate -> hoped () |> Option.map (fun worth -> move, None, worth)
                | _ ->
                    trying move game
                    |> Option.map (fun after -> move, Some after, worth after.Position (Game.active after).Bag))

        let weighed =
            if rival.Skill.Ahead < 1 then
                weighed |> List.map (fun (move, _, worth) -> move, worth)
            else
                weighed
                |> List.sortByDescending (fun (_, _, worth) -> worth)
                |> List.truncate rival.Skill.Ahead
                |> List.map (fun (move, after, worth) -> move, after |> Option.map answered |> Option.defaultValue worth)

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

            let picked, rng = Rng.pick among rng

            Some(picked, { rival with Rng = rng })


    let private roughly =
        { Land = 10
          Nudge = 1
          Axe = 0
          Flag = 0
          Held = 12
          Spare = 1 }

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

    let all = [ easy; medium; hard ]

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    let seating (seed: uint64) sitting session =
        Machines.seating (Game.players (Session.game session) |> List.map (fun player -> player.Id)) seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng })


    let taking session rival =
        match session with
        | InPlay play -> plays play rival
        | Finished _ -> None
