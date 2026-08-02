// Checks the two winning cascades: which faction carries the board, and which
// player carries the faction.
//
//   dotnet fsi tests/outcome.fsx

#load "../src/Prelude.fs"
#load "../src/Cascade.fs"
#load "../src/Rng.fs"
#load "../src/Domain.fs"
#load "../src/Ruling.fs"
#load "../src/Board.fs"
#load "../src/Model.fs"
#load "../src/Outcome.fs"
#load "../src/Setup.fs"

open TCModel

let mutable failures = 0

let report name expected actual =
    if actual = expected then
        printfn "ok   %s" name
    else
        failures <- failures + 1
        printfn "FAIL %s: expected %A, got %A" name expected actual

/// A finished game with the given regions stocked and the given bags held.
/// `stocked` names regions by number; everything else is emptied.
let game stocked bags =
    let model = Setup.init (max 2 (List.length bags)) 1UL

    let regions =
        model.Regions
        |> Map.map (fun (RegionId n) region ->
            match stocked |> List.tryFind (fst >> (=) n) with
            | Some(_, counts) -> { region with Stones = Pile.ofCounts counts }
            | None -> { region with Stones = Pile.empty })

    let players =
        bags
        |> List.mapi (fun index counts ->
            { Id = PlayerId(index + 1)
              Bag = Pile.ofCounts counts })

    { model with
        Regions = regions
        Players = players
        Active = PlayerId(List.length players) // so Player 1 would act next
        Status = Over "test" }

// --- the winning faction ----------------------------------------------------

let faction name expected stocked =
    report name expected (game stocked [ [ (Black, 1) ]; [ (Red, 1) ] ] |> Outcome.faction |> fst)

// Regions 1 and 2 go to Black, region 4 to Red.
faction "most regions ruled" [ Black ] [ 1, [ (Black, 2) ]; 2, [ (Black, 2) ]; 4, [ (Red, 2) ] ]

// One region each, so the Axe (13) settles it.
faction "level on regions, the Axe settles it" [ Blue ] [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ]; 13, [ (Blue, 3) ] ]

// One region each and a level Axe, so the Flag (12) settles it.
faction
    "level on regions and the Axe, the Flag settles it"
    [ Red ]
    [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ]; 13, [ (Red, 1); (Blue, 1) ]; 12, [ (Red, 4) ] ]

// Nothing separates them anywhere.
faction "level throughout is a draw" [ Red; Blue ] [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ] ]

// A faction out on land cannot come back on the Axe.
faction
    "faction out on land cannot win the Axe"
    [ Black ]
    [ 1, [ (Black, 2) ]; 2, [ (Black, 2) ]; 4, [ (Red, 2) ]; 13, [ (Red, 9) ] ]

// The Flag and the Axe are manoeuvres, not ground: holding both wins no land at all,
// so a single region of real ground beats them.
faction
    "the Flag and the Axe are not land"
    [ Black ]
    [ 1, [ (Black, 2) ]; 13, [ (Red, 4) ]; 12, [ (Red, 4) ] ]

// No faction rules anything, so every faction is level on nought and the Axe decides.
faction "no regions ruled at all, the Axe decides" [ Blue ] [ 13, [ (Blue, 1) ] ]

// --- the winning player -----------------------------------------------------

// Black carries the board throughout: two regions to Black, one to Red.
let blackWins = [ 1, [ (Black, 2) ]; 2, [ (Black, 2) ]; 4, [ (Red, 2) ] ]

let player name expected bags =
    report name expected (game blackWins bags |> Outcome.result)

// The example as given: P1 holds KKR, P2 holds KBR.
player
    "most stones of the winning faction"
    (Outcome.Won(Black, PlayerId 1))
    [ [ (Black, 2); (Red, 1) ]; [ (Black, 1); (Blue, 1); (Red, 1) ] ]

// The second example: both hold 2 black, so fewest losing stones decides.
player
    "level on black, fewest losing stones decides"
    (Outcome.Won(Black, PlayerId 1))
    [ [ (Black, 2); (Red, 1) ]; [ (Black, 2); (Blue, 1); (Red, 1) ] ]

// Identical bags: the player who would act next takes it. Active is the last
// player, so Player 1 is next.
player
    "identical bags go to whoever acts next"
    (Outcome.Won(Black, PlayerId 1))
    [ [ (Black, 2); (Red, 1) ]; [ (Black, 2); (Red, 1) ] ]

// A player out on black cannot win on holding fewer losing stones.
player
    "player out on the winning colour cannot win on losing stones"
    (Outcome.Won(Black, PlayerId 2))
    [ [ (Black, 1) ]; [ (Black, 3); (Red, 5) ] ]

report
    "every bag played out is a draw"
    (Outcome.Drawn "Black carried the board, but every player has played out their bag")
    (game blackWins [ []; [] ] |> Outcome.result)

report
    "a drawn faction draws the game"
    (Outcome.Drawn "Red, Blue could not be separated, so no faction carried the board")
    (game [ 1, [ (Red, 2) ]; 2, [ (Blue, 2) ] ] [ [ (Red, 1) ]; [ (Blue, 1) ] ] |> Outcome.result)

printfn ""

if failures = 0 then
    printfn "all checks passed"
else
    printfn "%d check(s) failed" failures

exit failures
