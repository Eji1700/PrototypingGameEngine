// Checks the two winning cascades: which faction carries the board, and which
// player carries the faction.
//
//   dotnet fsi tests/outcome.fsx

#load "Harness.fsx"

open TCModel.Domain
open Harness

// --- the winning faction ----------------------------------------------------

let faction name expected stocked =
    report name expected (gameOf stocked [ [ (Green, 1) ]; [ (Red, 1) ] ] |> Outcome.weighFactions |> fst)

// Regions 1 and 2 go to Green, region 4 to Red.
faction "most land ruled" [ Green ] [ 1, [ (Green, 2) ]; 2, [ (Green, 2) ]; 4, [ (Red, 2) ] ]

// One region each, so the Axe (14) settles it.
faction "level on land, the Axe settles it" [ Blue ] [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ]; 14, [ (Blue, 3) ] ]

// One region each and a level Axe, so the Flag (13) settles it.
faction
    "level on land and the Axe, the Flag settles it"
    [ Red ]
    [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ]; 14, [ Red, 1; Blue, 1 ]; 13, [ (Red, 4) ] ]

faction "level throughout is a draw" [ Red; Blue ] [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ] ]

// A faction out on land cannot come back on the Axe.
faction
    "faction out on land cannot win the Axe"
    [ Green ]
    [ 1, [ (Green, 2) ]; 2, [ (Green, 2) ]; 4, [ (Red, 2) ]; 14, [ (Red, 9) ] ]

// The Flag and the Axe are manoeuvres, not ground: holding both wins no land at all,
// so a single region of real ground beats them.
faction "the Flag and the Axe are not land" [ Green ] [ 1, [ (Green, 2) ]; 14, [ (Red, 4) ]; 13, [ (Red, 4) ] ]

// Nobody rules any land, so every faction is level on nought and the Axe decides.
faction "no land ruled at all, the Axe decides" [ Blue ] [ 14, [ (Blue, 1) ] ]

// --- the winning player -----------------------------------------------------

/// A verdict with the winner named by seat, since a PlayerId cannot be built here.
type Result =
    | WonBy of StoneColor * seat: int
    | DrawnBecause of DrawReason

let private seated verdict =
    match verdict with
    | Won(faction, playerId) -> WonBy(faction, PlayerId.value playerId)
    | Drawn reason -> DrawnBecause reason

// Green carries the board throughout: two regions to Green, one to Red.
let greenWins = [ 1, [ (Green, 2) ]; 2, [ (Green, 2) ]; 4, [ (Red, 2) ] ]

/// Hand the active seat to the last player, so Player 1 is the one who acts next.
let private lastToAct game =
    [ 2 .. Game.playerCount game ]
    |> List.fold
        (fun game _ ->
            { game with
                Table = Table.advance game.Table })
        game

let player name expected bags =
    report name expected (gameOf greenWins bags |> lastToAct |> Outcome.verdict |> seated)

// The example as given: P1 holds KKR, P2 holds KBR.
player "most stones of the winning faction" (WonBy(Green, 1)) [ [ Green, 2; Red, 1 ]; [ Green, 1; Blue, 1; Red, 1 ] ]

// The second example: both hold 2 green, so fewest losing stones decides.
player "level on green, fewest losing stones decides" (WonBy(Green, 1)) [ [ Green, 2; Red, 1 ]; [ Green, 2; Blue, 1; Red, 1 ] ]

// Identical bags: the player who would act next takes it.
player "identical bags go to whoever acts next" (WonBy(Green, 1)) [ [ Green, 2; Red, 1 ]; [ Green, 2; Red, 1 ] ]

// A player out on green cannot win on holding fewer losing stones.
player "player out on the winning colour cannot win on losing stones" (WonBy(Green, 2)) [ [ (Green, 1) ]; [ Green, 3; Red, 5 ] ]

player "every bag played out is a draw" (DrawnBecause(EveryBagPlayedOut Green)) [ []; [] ]

report
    "a drawn faction draws the game"
    (DrawnBecause(NoFactionSeparated [ Red; Blue ]))
    (gameOf [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ] ] [ [ (Red, 1) ]; [ (Blue, 1) ] ]
     |> Outcome.verdict
     |> seated)

finish ()
