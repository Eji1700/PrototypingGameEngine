namespace TCModel.Turncoats

open TCModel.Common
open TCModel.Engine

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
        StoneColor.all |> List.maxBy (fun color -> Pile.count color bag)

    /// What the board is worth to whoever is backing `backed`, as a lead over the best of the other
    /// two colours rather than as a bare count - a colour is only winning relative to its rivals.
    let private appraise weights backed position bag =
        let ruled = Ruling.standings position
        let axe = Position.stones Board.axe position
        let flag = Position.stones Board.flag position

        let over count =
            count backed
            - (StoneColor.all |> List.filter ((<>) backed) |> List.map count |> List.max)

        let ground =
            Board.landRegions
            |> List.sumBy (fun region ->
                let standing = Position.stones region.Id position

                // Clamped, so that piling a colour into one region it already rules cannot pass for
                // progress. It is worth a nudge to be ahead somewhere, and no more than a nudge.
                max -Reach (min Reach (over (fun color -> Pile.count color standing))))

        weights.Land * over (fun color -> ruled[color])
        + weights.Nudge * ground
        + weights.Axe * over (fun color -> Pile.count color axe)
        + weights.Flag * over (fun color -> Pile.count color flag)
        + weights.Held * Pile.count backed bag
        - weights.Spare * (Pile.total bag - Pile.count backed bag)


    /// Every way of taking `wanted` stones out of the colours standing there, as a list of colours
    /// each time. Used to offer a battle every combination of casualties it could drive out.
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

    let private aimable =
        openRegions
        |> List.filter (fun region -> not (RegionKind.isIsolated region.Kind))

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


    /// The game as it will stand for the next player, with their bag taken to be everything this
    /// one cannot see. Bags are hidden, so looking a move ahead means guessing at one - and the
    /// unseen pool is the honest guess, since it is exactly what the rival might hold.
    let private handedOn me game =
        let unseen = (Knowledge.seenBy me game).Unseen

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

        let played move =
            trying move game
            |> Option.map (fun after -> worth after.Position (Game.active after).Bag)

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

        // One reply deep: play the move, then take the worst the next player could leave us. What
        // makes `hard` different from `medium`, and why only the few best moves are looked at this
        // way - every move against every reply is more positions than a turn is worth.
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
            | AwaitingReturn _ -> Pile.toCounts me.Bag |> List.map (fst >> Settle)
            | AwaitingAction -> candidates game.Position me.Bag @ [ Negotiate ]

        let weighed =
            offered
            |> List.choose (fun move ->
                match move with
                | Negotiate -> hoped () |> Option.map (fun worth -> move, worth)
                | _ -> played move |> Option.map (fun worth -> move, worth))

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

            let picked, rng = Rng.intBelow (List.length among) rng

            Some(among[picked], { rival with Rng = rng })


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

    let names = Machines.named (fun skill -> skill.Name) all

    let byName name =
        Machines.byName (fun skill -> skill.Name) all name

    let seating (seed: uint64) sitting game =
        Machines.seating (Game.players game |> List.map (fun player -> player.Id)) seed sitting
        |> List.map (fun (seat, skill, rng) -> seat, { Skill = skill; Rng = rng })


    let taking session rival =
        match session with
        | InPlay play -> plays play rival
        | Finished _ -> None

    let holds rivals model =
        Machines.holds Playing.rules rivals model

    let answering rivals model =
        Machines.answering Playing.rules taking rivals model
