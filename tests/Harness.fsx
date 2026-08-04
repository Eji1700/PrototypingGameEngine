// Shared by the test scripts: loads the domain in compile order and keeps score.

#load "../src/Common/Result.fs"
#load "../src/Common/Cascade.fs"
#load "../src/Common/Random.fs"
#load "../src/Domain/Stones.fs"
#load "../src/Domain/Board.fs"
#load "../src/Domain/Players.fs"
#load "../src/Domain/Position.fs"
#load "../src/Domain/Ruling.fs"
#load "../src/Domain/Game.fs"
#load "../src/Domain/Knowledge.fs"
#load "../src/Domain/Events.fs"
#load "../src/Domain/Actions.fs"
#load "../src/Domain/Outcome.fs"
#load "../src/Domain/Setup.fs"

open TCModel.Domain

let mutable failures = 0

let report name expected actual =
    if actual = expected then
        printfn "ok   %s" name
    else
        failures <- failures + 1
        printfn "FAIL %s: expected %A, got %A" name expected actual

let finish () =
    printfn ""

    if failures = 0 then
        printfn "all checks passed"
    else
        printfn "%d check(s) failed" failures

    exit failures

/// A game with the given regions stocked and the given bags held; everything else
/// is emptied. Regions are named by number.
let gameOf stocked bags =
    let game = Setup.deal (max Table.MinPlayers (List.length bags)) 1UL |> Result.toOption |> Option.get

    let position =
        Board.regions
        |> List.fold
            (fun position region ->
                let counts =
                    stocked
                    |> List.tryFind (fun (n, _) -> Board.tryId n = Some region.Id)
                    |> Option.map snd
                    |> Option.defaultValue []

                Position.withStones region.Id (Pile.ofCounts counts) position)
            Position.empty

    { game with
        Position = position
        Table = bags |> List.map Pile.ofCounts |> Table.trySeat |> Result.toOption |> Option.get }
