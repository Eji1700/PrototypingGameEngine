// The things that are true of every game, rather than of the games somebody thought to
// write down.
//
// Every other script here plays a game by hand and checks what came out. This one has the
// machine think of the games: it deals from an arbitrary seed and throws an arbitrary
// string of moves at the rules - legal, illegal, out of turn, in the middle of a
// negotiation, after the game is over - and checks that what must always be true still is.
// Where a check fails, FsCheck cuts moves out of the game until it has the shortest one
// that still fails, and that shortest game is usually the whole explanation.
//
//   dotnet fsi tests/properties.fsx

#r "nuget: FsCheck, 3.3.3"

#load "Whole.fsx"

open FsCheck
open FsCheck.FSharp
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.Turncoats
open Harness
open Whole

// --- a game somebody might have played ------------------------------------------------

/// How many sat down, what the game was dealt from, and every line they typed.
///
/// The moves are not filtered for legality, and that is the point. A player at a prompt
/// types whatever they like and the rules answer for themselves, so a generated game that
/// is mostly refusals is a perfectly good game to hold these checks against - and
/// refusing without disturbing anything is itself one of the things being checked.
type Play =
    { Players: int
      Seed: uint64
      Moves: Msg list }

let private color = Gen.elements StoneColor.all

let private region =
    Gen.elements (Board.regions |> List.map (fun region -> region.Id))

/// Which stones a battle drives out: as many as the rule allows, or a named few - which
/// may name none at all, or more than the region could possibly give up.
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

/// Recruiting, battling and marching are what a game is mostly made of. Resigning ends
/// it, so it stays rare - a game that resigns on move two leaves the rules with nothing
/// left to be asked.
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
          // A restart deals a fresh game mid-record, which is the most disruptive thing
          // that can be asked for and so worth asking for now and then.
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

    // A failing game is shortened by dropping moves out of it one at a time. Which move
    // was the one that mattered is the question a counterexample has to answer, and a
    // forty-move game does not answer it.
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

// --- running one ------------------------------------------------------------------------

let private config =
    Config.QuickThrowOnFailure.WithMaxTest(300).WithQuietOnSuccess(true)

/// Check a property and report it the way every other check in this suite is reported.
/// FsCheck throws with the shortest failing game it could find, and that is the whole
/// diagnosis, so it is printed under the failure rather than summarised away.
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

// --- the stones ---------------------------------------------------------------------------

// Stones are never made or destroyed: they move between the bags, the map and the reserve
// and nowhere else. Counting them by colour rather than in total is what makes this worth
// checking - a total stays right through a swap that a colour would catch.

holds
    "every colour keeps all 21 of its stones, whatever the game is asked to do"
    (about (fun play ->
        let game = Playing.game (played play)

        StoneColor.all
        |> List.forall (fun color -> Pile.count color (Game.allStones game) = 21)))

// --- the rules ------------------------------------------------------------------------------

// A move the rules refuse is still written into the record - asking is part of what
// happened at the table - but it must not disturb the position by so much as a stone.

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

// Who rules a region is decided by a cascade, and the first thing the cascade weighs is
// how many stones of each colour stand there. So whoever comes out of it - one ruler or
// several still level - must be among the colours holding the most stones in the region,
// and a region holding nothing at all belongs to nobody.

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

// --- what a player may know --------------------------------------------------------------------

// Whatever the game has been through, a player is told how much is in every bag and what
// is in only their own. What is out of sight is the rest of the stones exactly: it is
// worked out by subtraction rather than remembered, so it cannot drift, but it can go
// wrong - and a negative count would be the sign of it.

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

// --- the record ------------------------------------------------------------------------------

// The promise the whole design rests on: a game written to a file and read back plays out
// as the same game. Not only ending in the same position - passing through every position
// it passed through on the way, doubling back and all.

holds
    "a game written down and read back is the same game, state for state"
    (about (fun play ->
        let model = played play
        let written = Transcript.write playing model.Journal

        // The two failures are of different kinds - a file that will not read and a
        // table that will not seat - and neither may happen, so both come out as one.
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

// Only when the undo actually took something back. At the deal there is nothing to take
// back and the undo is refused - but a move undone earlier may still be waiting to be
// made again, and the redo would then carry the game forward rather than return it. That
// is `redo` keeping its own promise rather than a hole in this one, so the check says
// which case it means instead of quietly covering both.

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
