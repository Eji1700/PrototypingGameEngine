#r "nuget: FsCheck, 3.3.3"

#load "Whole.fsx"

open FsCheck
open FsCheck.FSharp
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Turncoats
open Harness
open Whole


type Play =
    { Players: int
      Seed: uint64
      Moves: Msg list }

let private color = Gen.elements StoneColor.all

let private region =
    Gen.elements (Board.regions |> List.map (fun region -> region.Id))

let private casualties =
    Gen.oneof
        [ Gen.constant AsManyAsAllowed
          gen {
              let! n = Gen.choose (0, 2)
              let! named = Gen.listOfLength n color
              return These named
          } ]

let private recruit =
    gen {
        let! color = color
        let! into = region
        return Recruit(color, into)
    }

let private battle =
    gen {
        let! color = color
        let! target = region
        let! driven = casualties
        return Battle(color, target, driven)
    }

let private march =
    gen {
        let! color = color
        let! from = region
        let! into = region
        let! count = Gen.choose (1, 3)
        return March(color, from, into, count)
    }

let private move =
    Gen.frequency
        [ 5, recruit
          4, battle
          4, march
          4, Gen.constant Negotiate
          3, color |> Gen.map Settle
          1, Gen.constant Resign ]

let private message =
    Gen.frequency
        [ 15, move |> Gen.map Make
          2, Gen.constant Undo
          1, Gen.constant Redo
          1, Gen.constant (Restart(None, None)) ]

let private plays =
    let generated =
        gen {
            let! players = Gen.choose (Table.MinPlayers, Table.MaxPlayers)
            let! seed = Gen.choose (1, 1_000_000) |> Gen.map uint64
            let! length = Gen.choose (0, 40)
            let! moves = Gen.listOfLength length message

            return
                { Players = players
                  Seed = seed
                  Moves = moves }
        }

    let shorter play =
        seq {
            for i in 0 .. List.length play.Moves - 1 ->
                { play with
                    Moves = List.removeAt i play.Moves }
        }

    Arb.fromGenShrink (generated, shorter)

let private played play =
    match Playing.start play.Players play.Seed with
    | Error _ -> failwith "the generator dealt a table the game would refuse"
    | Ok model -> play.Moves |> List.fold (fun model msg -> Playing.update msg model) model


let private config =
    Config.QuickThrowOnFailure.WithMaxTest(300).WithQuietOnSuccess(true)

let private holds name property =
    let failure =
        try
            Check.One(config, property)
            None
        with problem ->
            Some problem.Message

    match failure with
    | None -> report name true true
    | Some message ->
        report name true false

        message.Split '\n'
        |> Array.iter (fun line -> printfn "     %s" (line.TrimEnd()))

let private about property = Prop.forAll plays property


holds
    "every colour keeps all 21 of its stones, whatever the game is asked to do"
    (about (fun play ->
        let game = Playing.game (played play)

        StoneColor.all
        |> List.forall (fun color -> Pile.count color (Game.allStones game) = 21)))


holds
    "a move the rules refuse leaves the game exactly where it was"
    (about (fun play ->
        let step (model, sound) msg =
            let next = Playing.update msg model

            let refused =
                match next.Log with
                | Said(Refused _) :: _ -> true
                | _ -> false

            next, sound && (not refused || Playing.session next = Playing.session model)

        match Playing.start play.Players play.Seed with
        | Error _ -> false
        | Ok model -> play.Moves |> List.fold step (model, true) |> snd))


holds
    "whoever rules a region is holding as many stones there as anyone"
    (about (fun play ->
        let game = Playing.game (played play)

        Board.regions
        |> List.forall (fun region ->
            let stones = Game.stones region.Id game

            let most =
                StoneColor.all |> List.map (fun color -> Pile.count color stones) |> List.max

            let leading color =
                Pile.count color stones = most && most > 0

            match Game.ruleOver region.Id game with
            | Unclaimed -> Pile.isEmpty stones
            | RuledBy color -> leading color
            | Contested tied -> List.length tied >= 2 && tied |> List.forall leading)))


holds
    "a player sees their own bag, the size of everyone else's, and nothing more"
    (about (fun play ->
        let game = Playing.game (played play)

        Game.players game
        |> List.forall (fun beholder ->
            let seen = Knowledge.seenBy beholder game

            let bagsRight =
                seen.Bags
                |> List.forall (fun (playerId, sight) ->
                    match Game.tryPlayer playerId game, sight with
                    | Some player, Open pile -> playerId = beholder.Id && pile = player.Bag
                    | Some player, Closed n -> playerId <> beholder.Id && n = Pile.total player.Bag
                    | None, _ -> false)

            let reserveClosed =
                match seen.Reserve with
                | Closed n -> n = Pile.total game.Reserve
                | Open _ -> false

            let unseenAddsUp =
                StoneColor.all
                |> List.forall (fun color ->
                    let out = Pile.count color seen.Unseen

                    out >= 0
                    && out = Pile.count color (Game.allStones game)
                             - Pile.count color (Position.total game.Position)
                             - Pile.count color beholder.Bag)

            bagsRight && reserveClosed && unseenAddsUp)))


holds
    "a game written down and read back is the same game, state for state"
    (about (fun play ->
        let model = played play

        let written =
            Transcript.write playing (Seating.here (Model.players model)) model.Journal

        let replayed =
            Transcript.read playing written
            |> Result.mapError (fun _ -> ())
            |> Result.bind (fun read ->
                Playing.replay read.Players read.Seed read.Moves
                |> Result.mapError (fun _ -> ()))

        match replayed with
        | Error() -> false
        | Ok again ->
            Playing.session again = Playing.session model
            && Timeline.states again.Timeline = Timeline.states model.Timeline))


holds
    "taking the last move back and making it again leaves the game where it stood"
    (about (fun play ->
        let model = played play
        let back = model |> Playing.update Undo

        if Timeline.movesMade back.Timeline = Timeline.movesMade model.Timeline then
            true
        else
            Playing.session (Playing.update Redo back) = Playing.session model))

finish ()
