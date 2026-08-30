#load "Harness.fsx"

open Prototyping.Turncoats
open Harness

let private at n = Board.tryId n |> Option.get

let emberfall = at 5
let crossroads = at 8
let nightfen = at 1
let flag = Board.flag
let axe = Board.axe
let waste = at 6

report "the map hangs together" [] Board.problems

let private game stocked =
    gameOf [ (8, stocked) ] [ [ Red, 1; Blue, 1; Green, 1 ]; [ (Red, 1) ] ]

let private refuses name expected outcome =
    report name (Error expected) (outcome |> Result.map ignore)

let private stonesIn regionId outcome =
    outcome
    |> Result.map (fun (game, _) -> Game.stones regionId game |> Pile.toCounts)

let private bagOf outcome =
    outcome |> Result.map (fun (game, _) -> (Game.active game).Bag |> Pile.toCounts)


report
    "recruit puts the stone on the map"
    (Ok [ (Red, 1) ])
    (Actions.recruit Red emberfall (game [])
     |> stonesIn emberfall
     |> Result.map (List.filter (fst >> (=) Red)))

refuses "recruit cannot enter the dead region" (DeadGround waste) (Actions.recruit Red waste (game []))

report "recruit may enter the Flag" (Ok [ (Red, 1) ]) (Actions.recruit Red flag (game []) |> stonesIn flag)


let blueVsGreen = game [ Blue, 1; Green, 1 ]

report
    "an unnamed battle drives out all it may"
    (Ok [ (Blue, 1) ])
    (Actions.battle Blue crossroads AsManyAsAllowed blueVsGreen
     |> stonesIn crossroads)

report
    "the battling stone goes to the Axe"
    (Ok [ (Blue, 1) ])
    (Actions.battle Blue crossroads AsManyAsAllowed blueVsGreen |> stonesIn axe)

report
    "driven stones go back to the reserve"
    (Ok 1)
    (Actions.battle Blue crossroads AsManyAsAllowed blueVsGreen
     |> Result.map (fun (game, _) -> Pile.count Green game.Reserve - Pile.count Green blueVsGreen.Reserve))

refuses
    "no stone of the attacking colour"
    (NothingToBattleWith(crossroads, Red))
    (Actions.battle Red crossroads AsManyAsAllowed blueVsGreen)

refuses
    "nothing of another colour to drive out"
    (NothingToDriveOut(crossroads, Blue))
    (Actions.battle Blue crossroads AsManyAsAllowed (game [ (Blue, 2) ]))

refuses "a battle must drive out something" BattleMustDriveOutSomething (Actions.battle Blue crossroads (These []) blueVsGreen)

refuses
    "a battle cannot drive out its own colour"
    (CannotDriveOutOwnColour Blue)
    (Actions.battle Blue crossroads (These [ Blue ]) (game [ Blue, 2; Green, 1 ]))

refuses
    "no more driven out than stones matching"
    (MoreDrivenThanAllowed(crossroads, Blue, 1))
    (Actions.battle Blue crossroads (These [ Green; Red ]) (game [ Blue, 1; Green, 1; Red, 1 ]))

refuses
    "a real choice goes back to the attacker"
    (MustChooseCasualties(crossroads, Pile.ofCounts [ Red, 1; Green, 1 ], 1))
    (Actions.battle Blue crossroads AsManyAsAllowed (game [ Blue, 1; Green, 1; Red, 1 ]))

report
    "one losing colour is no choice at all"
    (Ok [ Blue, 1; Green, 1 ])
    (Actions.battle Blue crossroads AsManyAsAllowed (game [ Blue, 1; Green, 2 ])
     |> stonesIn crossroads)

refuses "a battle cannot target the Axe" (StandsApart axe) (Actions.battle Blue axe AsManyAsAllowed blueVsGreen)


let twoBlue = game [ (Blue, 2) ]

report "marching empties the source" (Ok []) (Actions.march Blue crossroads emberfall 2 twoBlue |> stonesIn crossroads)

report
    "marching fills the destination"
    (Ok [ Red, 2; Blue, 2 ])
    (Actions.march Blue crossroads emberfall 2 (gameOf [ 8, [ (Blue, 2) ]; 5, [ (Red, 2) ] ] [ [ (Blue, 1) ]; [ (Red, 1) ] ])
     |> stonesIn emberfall)

report
    "the marching stone goes to the Flag"
    (Ok [ (Blue, 1) ])
    (Actions.march Blue crossroads emberfall 1 twoBlue |> stonesIn flag)

refuses "nothing of that colour to march" (NothingToMarch(crossroads, Red)) (Actions.march Red crossroads emberfall 1 twoBlue)

refuses
    "not enough of that colour to march"
    (NotEnoughToMarch(crossroads, Blue, 2, 3))
    (Actions.march Blue crossroads emberfall 3 twoBlue)

refuses "a march moves at least one stone" MarchNeedsAStone (Actions.march Blue crossroads emberfall 0 twoBlue)

refuses "a march must cross a border" (NotAdjacent(crossroads, nightfen)) (Actions.march Blue crossroads nightfen 1 twoBlue)

refuses "a march cannot enter the dead region" (DeadGround waste) (Actions.march Blue crossroads waste 1 twoBlue)

refuses
    "the Flag borders nothing, so nothing marches into it"
    (NotAdjacent(crossroads, flag))
    (Actions.march Blue crossroads flag 1 twoBlue)


let private drawable =
    { game [] with
        Reserve = Pile.ofCounts [ (Red, 1) ] }

report
    "negotiating draws from the reserve into the bag"
    (Ok [ Red, 2; Blue, 1; Green, 1 ])
    (Actions.negotiate drawable
     |> Result.map (fun (game, _, _) -> (Game.active game).Bag |> Pile.toCounts))

report "settling hands a stone back" (Ok [ Blue, 1; Green, 1 ]) (Actions.settle Red (game []) |> bagOf)

report
    "a negotiation leaves the bag the size it began"
    (Ok 3)
    (Actions.negotiate drawable
     |> Result.bind (fun (game, drawn, _) -> Actions.settle drawn game)
     |> Result.map (fun (game, _) -> Pile.total (Game.active game).Bag))

refuses "nothing to draw" ReserveEmpty (Actions.negotiate { game [] with Reserve = Pile.empty })

refuses
    "cannot hand back what is not held"
    (NotInBag((Game.active (game [])).Id, Red))
    (Actions.settle Red (gameOf [] [ [ (Blue, 1) ]; [ (Red, 1) ] ]))


let private dealt = Setup.deal 3 7UL |> Result.toOption |> Option.get

let private conserved name outcome =
    report name (Ok 63) (outcome |> Result.map (fun (game, _) -> Pile.total (Game.allStones game)))

let private held game =
    (Game.active game).Bag |> Pile.toColours |> List.head

report "a fresh deal has all 63 stones" 63 (Pile.total (Game.allStones dealt))

conserved "recruiting conserves the stones" (Actions.recruit (held dealt) emberfall dealt)

conserved
    "marching conserves the stones"
    (Actions.march
        Blue
        crossroads
        emberfall
        2
        { dealt with
            Position = Position.withStones crossroads (Pile.ofCounts [ (Blue, 2) ]) dealt.Position })

conserved
    "battling conserves the stones"
    (Actions.battle
        Blue
        crossroads
        AsManyAsAllowed
        { dealt with
            Position = Position.withStones crossroads (Pile.ofCounts [ Blue, 1; Green, 1 ]) dealt.Position })

conserved
    "a whole negotiation conserves the stones"
    (Actions.negotiate dealt
     |> Result.bind (fun (game, drawn, _) -> Actions.settle drawn game))

finish ()
