#load "Whole.fsx"

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Turncoats
open Harness
open Whole


let private machinesAt (skills: Skill list) seed model =
    Rival.seating seed (skills |> List.map Some) (Playing.session model)
    |> List.map (fun (playerId, rival) ->
        playerId,
        { Skill = rival.Skill.Name
          Plays = Machines.choosing Rival.taking rival })

let private playedOut skills seed =
    match Playing.start (List.length skills) seed with
    | Error _ -> failwith "the deal was refused"
    | Ok model ->
        Solo.opened playing "played-out" model
        |> Solo.against (machinesAt skills seed model)
        |> fst
        |> Solo.model

let private twoHard = playedOut [ Rival.hard; Rival.hard ] 42UL

let private threeMixed = playedOut [ Rival.easy; Rival.medium; Rival.hard ] 1234UL


report "a table of machines plays the game out to its end" true (Playing.isOver twoHard)

report "and again with three of them, of three different skills" true (Playing.isOver threeMixed)


report
    "a machine facing somebody who plays plays back, rather than negotiating the game away"
    true
    (Journal.length (playedOut [ Rival.hard; Rival.easy ] 5UL).Journal > 15)


let private asked model = Journal.entries model.Journal

let private toldIn model =
    asked model |> List.collect (fun entry -> entry.Told)

let private refusals model =
    toldIn model
    |> List.choose (function
        | Said(Refused rejection) -> Some(Words.rejection rejection)
        | _ -> None)


report "nothing a machine asked for over two whole games was refused" [] (refusals twoHard @ refusals threeMixed)


let private typedBack (msg: Msg) =
    match Playable.read playing (Words.command msg) with
    | Ok(Send read) -> read = msg
    | _ -> false

report
    "and every move it made is a line the prompt reads back as the same move"
    []
    (asked twoHard @ asked threeMixed
     |> List.map (fun entry -> entry.Asked)
     |> List.filter (typedBack >> not)
     |> List.map Words.command)


report
    "so a game between machines replays from its record"
    (Playing.session twoHard)
    (Playing.replay (Journal.players twoHard.Journal) (Journal.seed twoHard.Journal) (Journal.moves twoHard.Journal)
     |> Result.toOption
     |> Option.get
     |> Playing.session)


report
    "the same machines at the same deal play the same game twice"
    (Journal.moves twoHard.Journal)
    (Journal.moves (playedOut [ Rival.hard; Rival.hard ] 42UL).Journal)

report
    "and a different seed is a different game"
    false
    (Journal.moves twoHard.Journal = Journal.moves (playedOut [ Rival.hard; Rival.hard ] 43UL).Journal)


let private reshuffled (game: Game) =
    let players = Game.players game

    let acting =
        players |> List.findIndex (fun player -> player.Id = (Game.active game).Id)

    let pooled =
        players
        |> List.filter (fun player -> player.Id <> (Game.active game).Id)
        |> List.fold (fun pile player -> Pile.merge player.Bag pile) Pile.empty

    let dealt, _ =
        players
        |> List.fold
            (fun (bags, (pooled, rng)) player ->
                if player.Id = (Game.active game).Id then
                    bags @ [ player.Bag ], (pooled, rng)
                else
                    let (bag, left), rng = Pile.draw (Pile.total player.Bag) pooled rng
                    bags @ [ bag ], (left, rng))
            ([], (pooled, Rng.ofSeed 7UL))

    let seated =
        Table.trySeat dealt
        |> Result.toOption
        |> Option.get
        |> fun table -> List.fold (fun table _ -> Table.advance table) table [ 1..acting ]

    { game with Table = seated }

let private posed seed =
    let model =
        [ "recruit r 1"
          "recruit b 4"
          "negotiate"
          "return g"
          "recruit g 7"
          "march r 5 3 1" ]
        |> List.fold
            (fun model line ->
                match Playable.read playing line with
                | Ok(Send msg) -> Playing.update msg model
                | _ -> model)
            (Playing.start 3 seed |> Result.toOption |> Option.get)

    match Playing.session model with
    | InPlay play -> play
    | Finished _ -> failwith "the game ended before the position was posed"

let private chooses skill (play: Play) =
    Rival.plays play { Skill = skill; Rng = Rng.ofSeed 5UL } |> Option.map fst

let private posedGame = posed 2024UL

let private behindItsBack =
    { posedGame with
        Game = reshuffled posedGame.Game }

let private bags (play: Play) =
    Game.players play.Game |> List.map (fun player -> player.Bag)

report "the stones the machine cannot see really did move" false (bags posedGame = bags behindItsBack)

report "but its own bag did not" (Game.active posedGame.Game).Bag (Game.active behindItsBack.Game).Bag

report
    "and it plays the same move however the bags it cannot see are arranged"
    (chooses Rival.hard posedGame)
    (chooses Rival.hard behindItsBack)

report "which is true of the middle skill as well" (chooses Rival.medium posedGame) (chooses Rival.medium behindItsBack)


// Looking a reply ahead, the machine takes the next player to hold every stone it cannot see -
// here one Red stone, the whole of what is out of sight. A lone Red stone stands at Nightfen and
// the machine has one Red to recruit, with three empty regions to put it in. Two of them border
// Nightfen, so a Red stone in the other's bag could march the pair together and cost a region;
// Dunmoor's neighbours are held by lone Green stones, so a Red stone marched into any of them
// takes it. A guess one stone short holds no Red at all, fears no reply, and picks among the
// three by lot. Land alone is weighed, so nothing else tells the three apart.
let private farsighted =
    { Rival.hard with
        Name = "farsighted"
        Weighs =
            { Land = 10
              Nudge = 0
              Axe = 0
              Flag = 0
              Held = 0
              Spare = 0 } }

let private loneStone: Play =
    { Game =
        { gameOf
              [ 1, [ (Red, 1) ]
                3, [ (Blue, 3) ]
                5, [ (Blue, 3) ]
                7, [ (Blue, 3) ]
                8, [ (Blue, 3) ]
                9, [ (Green, 1) ]
                10, [ (Green, 1) ]
                11, [ (Green, 1) ] ]
              [ [ (Red, 1) ]; [ (Red, 1) ] ] with
            Reserve = Pile.empty }
      Phase = AwaitingAction
      Negotiations = 0
      Turn = 1 }

let private dunmoor = Board.tryId 12 |> Option.get

report
    "looking a move ahead, the machine takes the next player to hold every stone it cannot see"
    (List.replicate 6 (Some(Recruit(Red, dunmoor))))
    ([ 1UL .. 6UL ]
     |> List.map (fun seed ->
         Rival.plays
             loneStone
             { Skill = farsighted
               Rng = Rng.ofSeed seed }
         |> Option.map fst))


let private wonBy skills seed =
    let played = playedOut skills seed
    let seats = Game.players (Playing.game played) |> List.map (fun player -> player.Id)

    match Outcome.verdict (Playing.game played) with
    | Won(_, winner) -> Some(List.findIndex ((=) winner) seats)
    | Drawn _ -> None

let private duel one other =
    [| 1UL .. 12UL |]
    |> Array.Parallel.map (fun seed ->
        let asFirst =
            match wonBy [ one; other ] seed with
            | Some 0 -> 1
            | Some _ -> -1
            | None -> 0

        let asSecond =
            match wonBy [ other; one ] seed with
            | Some 0 -> -1
            | Some _ -> 1
            | None -> 0

        [ asFirst; asSecond ])
    |> List.concat

let private hoarder =
    { Rival.hard with
        Name = "hoarder"
        Weighs =
            { Rival.hard.Weighs with
                Land = 0
                Nudge = 0
                Held = 40 } }

let private tally (outcomes: int list) =
    let of' n =
        outcomes |> List.filter ((=) n) |> List.length

    List.sum outcomes, of' 1, of' -1, of' 0

let private hardOverEasy = duel Rival.hard Rival.easy
let private mediumOverEasy = duel Rival.medium Rival.easy
let private hardOverMedium = duel Rival.hard Rival.medium
let private hardOverHoarder = duel Rival.hard hoarder

printfn ""

for name, outcomes in
    [ "hard vs easy", hardOverEasy
      "medium vs easy", mediumOverEasy
      "hard vs medium", hardOverMedium
      "hard vs hoarder", hardOverHoarder ] do
    let net, won, lost, drawn = tally outcomes
    printfn "     %-16s net %+3d   won %2d  lost %2d  drawn %2d" name net won lost drawn

printfn ""

report "hard comes out ahead of easy over a run of games" true (List.sum hardOverEasy > 0)

report "and medium does too, by less" true (List.sum mediumOverEasy > 0)

report "and hard comes out ahead of medium, which is what the word is for" true (List.sum hardOverMedium > 0)


report "and ahead of a machine that simply sits on its stones" true (List.sum hardOverHoarder > 0)


report "a skill can be asked for by name" (Ok Rival.medium) (Rival.byName "MEDIUM")

report
    "and one nobody has is refused, saying which there are"
    true
    (match Rival.byName "cunning" with
     | Error problem -> problem.Contains "easy, medium, hard"
     | Ok _ -> false)

report
    "every skill there is can be asked for by its own name"
    true
    (Rival.all |> List.forall (fun skill -> Rival.byName skill.Name = Ok skill))


let private dealt = Playing.start 3 42UL |> Result.toOption |> Option.get

let private seated sitting =
    Rival.seating 42UL sitting (Playing.session dealt)
    |> List.map (fun (playerId, rival) -> PlayerId.value playerId, rival.Skill.Name)

report
    "the machine takes the seats it was given, and no others"
    [ 2, "easy"; 3, "hard" ]
    (seated [ None; Some Rival.easy; Some Rival.hard ])

report "including the first, which is nobody's by convention and not by rule" [ (1, "hard") ] (seated [ Some Rival.hard ])

report "so a person may sit between two machines" [ 1, "easy"; 3, "medium" ] (seated [ Some Rival.easy; None; Some Rival.medium ])

report "and none at all is a table of nothing but people" [] (seated [])


let private generators sitting =
    Rival.seating 42UL sitting (Playing.session dealt)
    |> List.map (fun (_, rival) -> rival.Rng)

report
    "a machine's generator follows the seat rather than the machine"
    (generators [ None; Some Rival.easy ])
    (generators [ Some Rival.hard; Some Rival.easy ] |> List.skip 1)

finish ()
