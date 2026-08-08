// The seat the program plays: that it plays legally, that it plays fairly, and that
// playing better is what the skills actually mean.
//
// A machine at a table is the one thing in this program that can be wrong quietly. Every
// other part of it either draws something a person looks at or refuses something a person
// typed; a machine that has picked a move nobody would has picked a legal move, drawn a
// perfectly good board and said nothing at all. So what is worth holding it to is not "does
// it work" but the three things it would be embarrassing to have got wrong:
//
//   - it only ever asks for moves the rules take, and only ones a person could have typed;
//   - it plays no better for stones it was never shown;
//   - and `hard` really does beat `easy`, which is the whole of what the word is promising.
//
// The last one is the reason a whole game is played out here rather than a position being
// posed. Machine against machine is the only way to ask it, and it doubles as the check
// that a table of machines finishes at all - the failure worth catching being not a wrong
// move but a turn that never passes.
//
//   dotnet fsi tests/rival.fsx

#load "Whole.fsx"

open TCModel.Common
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.Domain
open Harness
open Whole

// --- a whole game, machine against machine ---------------------------------------------
//
// Every seat taken by a machine, which is a table `Solo.against` will play out to its end
// in one call, there being no seat in it for a person to be asked anything at.

/// Machines at every seat, a generator each. Asked for the way any other table is: a
/// seating is one entry to a seat, and every one of these is the machine's.
let private machinesAt (skills: Skill list) seed model =
    Rival.seating seed (skills |> List.map Some) (Playing.game model)
    |> List.map (fun (playerId, rival) ->
        playerId,
        { Skill = rival.Skill.Name
          Plays = Offer.machine rival })

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

// That this line was reached at all is the check: `Solo.against` does not come back until
// the machines have nothing left to say, so a turn that never passed would hang here rather
// than fail. The count is only there to say the game was played rather than abandoned.

report "a table of machines plays the game out to its end" true (Playing.isOver twoHard)

report "and again with three of them, of three different skills" true (Playing.isOver threeMixed)

// A game ends the moment everybody negotiates in a row, so a machine that never played a
// stone would end one in three lines and look, from the outside, exactly like a machine that
// had played a game. This says it really does play: fifteen entries is well past anything
// standing still could reach.
//
// Against `easy` rather than against itself, because two machines that weigh a position the
// same way and are both content with it will both close the game out at once - which is not
// a fault, and is not what is being asked about here.

report
    "a machine facing somebody who plays plays back, rather than negotiating the game away"
    true
    (Journal.length (playedOut [ Rival.hard; Rival.easy ] 5UL).Journal > 15)

// --- what it asked for ------------------------------------------------------------------

let private asked model = Journal.entries model.Journal

/// Every notice the rules gave back over a whole game.
let private toldIn model =
    asked model |> List.collect (fun entry -> entry.Told)

let private refusals model =
    toldIn model
    |> List.choose (function
        | Said(Refused rejection) -> Some(Words.rejection rejection)
        | _ -> None)

// The machine asks the rules what they will take before it chooses, rather than keeping an
// opinion of its own about what is legal. If it ever kept a second copy of the rules by
// accident, this is where it would show: a refusal, and a turn that did not pass.

report "nothing a machine asked for over two whole games was refused" [] (refusals twoHard @ refusals threeMixed)

// And the other half of the same promise. A machine picks a `Move`, which is the very thing
// a typed line turns into - but a move nobody could type would still be written into the
// record, and a record is meant to be readable and re-playable for good.

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

// A record of a game between machines is a record like any other, so it plays again.

report
    "so a game between machines replays from its record"
    (Playing.session twoHard)
    (Playing.replay (Journal.players twoHard.Journal) (Journal.seed twoHard.Journal) (Journal.moves twoHard.Journal)
     |> Result.toOption
     |> Option.get
     |> Playing.session)

// --- and that it is a fold ----------------------------------------------------------------
//
// The generator travels inside the machine the way the game's own travels inside the game,
// so the same table dealt twice plays the same game twice. Without this a game against
// machines could not be replayed, written down, or asked about after the fact.

report
    "the same machines at the same deal play the same game twice"
    (Journal.moves twoHard.Journal)
    (Journal.moves (playedOut [ Rival.hard; Rival.hard ] 42UL).Journal)

report
    "and a different seed is a different game"
    false
    (Journal.moves twoHard.Journal = Journal.moves (playedOut [ Rival.hard; Rival.hard ] 43UL).Journal)

// --- it plays no better for what it was never shown -------------------------------------
//
// This is the one that cannot be read off the types, and the one worth being sure of. A
// machine weighs the map and its own bag; the argument list says so, and this says the same
// thing from outside, by moving stones between the bags it is not allowed to see and
// insisting it does not notice.
//
// Between them rather than into them: the stones out of sight are still the same stones, so
// everything it may legitimately work out - what must be in the reserve or in somebody's
// hand - is untouched, and the only thing that has changed is the one thing it is not
// entitled to know.

let private reshuffled (game: Game) =
    let players = Game.players game

    let acting =
        players |> List.findIndex (fun player -> player.Id = (Game.active game).Id)

    /// Everything in the other bags, poured together and dealt back out - each of them
    /// keeping the number of stones it had, so nothing about them changes but the colours.
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

    // Seating starts the table at the first chair, so it is walked round to where the game
    // had actually got to. Whose turn it is is no secret, and changing it would be changing
    // the question rather than hiding anything.
    let seated =
        Table.trySeat dealt
        |> Result.toOption
        |> Option.get
        |> fun table -> List.fold (fun table _ -> Table.advance table) table [ 1..acting ]

    { game with Table = seated }

let private posed seed =
    // A few moves in, so the bags have parted company with the deal and the map has
    // something on it worth weighing.
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

// --- and that the skills mean something ----------------------------------------------------
//
// `easy`, `medium` and `hard` are words this program says to a player, so they owe them
// something. Twelve deals, each played twice with the seats the other way round, so that
// going first is not what is being measured.
//
// Fixed seeds throughout: there is nothing to be flaky here, and a run that came out
// differently would mean the machine had changed rather than the dice.

let private wonBy skills seed =
    let played = playedOut skills seed
    let seats = Game.players (Playing.game played) |> List.map (fun player -> player.Id)

    match Outcome.verdict (Playing.game played) with
    | Won(_, winner) -> Some(List.findIndex ((=) winner) seats)
    | Drawn _ -> None

/// Played across cores rather than one after another. A game is a fold from a seed and
/// nothing else, so which core plays it cannot change how it comes out - and the results are
/// gathered back by seed rather than by whoever finished first, so the run is the same run.
/// This is the slowest thing in `tests/` by a distance, and it was idling fifteen of sixteen
/// cores to get there.
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

/// A machine that would sooner not play at all, which the rules of this game reward a good
/// deal more than they look as though they would: the player left holding most of the
/// winning faction carries it, whoever did the winning. Anything claiming to play well has
/// to beat sitting still, and it is the one opponent that is easy to write by accident.
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

// The one that would be embarrassing. A machine can be made to beat `easy` handsomely by
// never playing a stone at all, and it makes for a dreadful opponent - so the weights are
// held to beating that as well, and not by leaning on it.

report "and ahead of a machine that simply sits on its stones" true (List.sum hardOverHoarder > 0)

// --- the words the skills are asked for by --------------------------------------------------

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

// Seating: one entry to a seat, in the order the game deals them, and nothing at the seats
// that are somebody's. Said seat by seat rather than as a run of machines after the first,
// because which seats are the program's is a thing to be chosen: a table of three may be a
// person between two machines, and that is a table the old shape could not describe.

let private dealt = Playing.start 3 42UL |> Result.toOption |> Option.get

let private seated sitting =
    Rival.seating 42UL sitting (Playing.game dealt)
    |> List.map (fun (playerId, rival) -> PlayerId.value playerId, rival.Skill.Name)

report
    "the machine takes the seats it was given, and no others"
    [ 2, "easy"; 3, "hard" ]
    (seated [ None; Some Rival.easy; Some Rival.hard ])

report "including the first, which is nobody's by convention and not by rule" [ (1, "hard") ] (seated [ Some Rival.hard ])

report "so a person may sit between two machines" [ 1, "easy"; 3, "medium" ] (seated [ Some Rival.easy; None; Some Rival.medium ])

report "and none at all is a table of nothing but people" [] (seated [])

// A machine's generator comes from the seed and from where the seat sits, so moving one
// along a seat hands it the generator that seat has always had - which is what keeps a game
// against machines replaying exactly like any other.

let private generators sitting =
    Rival.seating 42UL sitting (Playing.game dealt)
    |> List.map (fun (_, rival) -> rival.Rng)

report
    "a machine's generator follows the seat rather than the machine"
    (generators [ None; Some Rival.easy ])
    (generators [ Some Rival.hard; Some Rival.easy ] |> List.skip 1)

finish ()
