#load "Harness.fsx"

open Prototyping.Engine
open Prototyping.Turncoats
open Harness


let faction name expected stocked =
    report name expected (gameOf stocked [ [ (Green, 1) ]; [ (Red, 1) ] ] |> Outcome.weighFactions |> fst)

faction "most land ruled" [ Green ] [ 1, [ (Green, 2) ]; 2, [ (Green, 2) ]; 4, [ (Red, 2) ] ]

faction "level on land, the Axe settles it" [ Blue ] [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ]; 14, [ (Blue, 3) ] ]

faction
    "level on land and the Axe, the Flag settles it"
    [ Red ]
    [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ]; 14, [ Red, 1; Blue, 1 ]; 13, [ (Red, 4) ] ]

faction "level throughout is a draw" [ Red; Blue ] [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ] ]

faction
    "faction out on land cannot win the Axe"
    [ Green ]
    [ 1, [ (Green, 2) ]; 2, [ (Green, 2) ]; 4, [ (Red, 2) ]; 14, [ (Red, 9) ] ]

faction "the Flag and the Axe are not land" [ Green ] [ 1, [ (Green, 2) ]; 14, [ (Red, 4) ]; 13, [ (Red, 4) ] ]

faction "no land ruled at all, the Axe decides" [ Blue ] [ 14, [ (Blue, 1) ] ]


type Result =
    | WonBy of StoneColor * seat: int
    | DrawnBecause of DrawReason

let private seated verdict =
    match verdict with
    | Won(faction, playerId) -> WonBy(faction, PlayerId.value playerId)
    | Drawn reason -> DrawnBecause reason

let greenWins = [ 1, [ (Green, 2) ]; 2, [ (Green, 2) ]; 4, [ (Red, 2) ] ]

let private lastToAct game =
    [ 2 .. Game.playerCount game ]
    |> List.fold
        (fun game _ ->
            { game with
                Table = Table.advance game.Table })
        game

let player name expected bags =
    report name expected (gameOf greenWins bags |> lastToAct |> Outcome.verdict |> seated)

player "most stones of the winning faction" (WonBy(Green, 1)) [ [ Green, 2; Red, 1 ]; [ Green, 1; Blue, 1; Red, 1 ] ]

player "level on green, fewest losing stones decides" (WonBy(Green, 1)) [ [ Green, 2; Red, 1 ]; [ Green, 2; Blue, 1; Red, 1 ] ]

player "identical bags go to whoever acts next" (WonBy(Green, 1)) [ [ Green, 2; Red, 1 ]; [ Green, 2; Red, 1 ] ]

player "player out on the winning colour cannot win on losing stones" (WonBy(Green, 2)) [ [ (Green, 1) ]; [ Green, 3; Red, 5 ] ]

player "every bag played out is a draw" (DrawnBecause(EveryBagPlayedOut Green)) [ []; [] ]

report
    "a drawn faction draws the game"
    (DrawnBecause(NoFactionSeparated [ Red; Blue ]))
    (gameOf [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ] ] [ [ (Red, 1) ]; [ (Blue, 1) ] ]
     |> Outcome.verdict
     |> seated)

finish ()
