// Shared by the test scripts: loads the engine and the game in compile order, and keeps
// score.
//
// The engine comes first and mentions nothing below it - that is the whole of what makes it
// one - so this list is also the shape of the thing: everything from `Result.fs` to
// `Machines.fs` would be there whatever game was being checked, and everything after
// `Stones.fs` is this one.

#load "Checks.fsx"

#load "../src/Common/Result.fs"
#load "../src/Common/Cascade.fs"
#load "../src/Common/Random.fs"
#load "../src/Engine/Seats.fs"
#load "../src/Engine/Messages.fs"
#load "../src/Engine/Told.fs"
#load "../src/Engine/Rules.fs"
#load "../src/Engine/Timeline.fs"
#load "../src/Engine/Journal.fs"
#load "../src/Engine/Model.fs"
#load "../src/Engine/Update.fs"
#load "../src/Engine/Machines.fs"
#load "../src/Games/Turncoats/Rules/Stones.fs"
#load "../src/Games/Turncoats/Rules/Board.fs"
#load "../src/Games/Turncoats/Rules/Players.fs"
#load "../src/Games/Turncoats/Rules/Position.fs"
#load "../src/Games/Turncoats/Rules/Ruling.fs"
#load "../src/Games/Turncoats/Rules/Game.fs"
#load "../src/Games/Turncoats/Rules/Knowledge.fs"
#load "../src/Games/Turncoats/Rules/Events.fs"
#load "../src/Games/Turncoats/Rules/Actions.fs"
#load "../src/Games/Turncoats/Rules/Outcome.fs"
#load "../src/Games/Turncoats/Rules/Setup.fs"
#load "../src/Games/Turncoats/Rules/Turn.fs"
#load "../src/Games/Turncoats/Rules/Playing.fs"

open TCModel.Engine
open TCModel.Turncoats

// Keeping score lives on its own, so that the other game's checks - which cannot load this
// file, both games having a `Board.fs` - can have it without it. Named through again here,
// so nothing that already says `open Harness` has to learn a second name.
let report = Checks.report

let finish = Checks.finish

/// A game with the given regions stocked and the given bags held; everything else
/// is emptied. Regions are named by number.
let gameOf stocked bags =
    let game =
        Setup.deal (max Table.MinPlayers (List.length bags)) 1UL
        |> Result.toOption
        |> Option.get

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
