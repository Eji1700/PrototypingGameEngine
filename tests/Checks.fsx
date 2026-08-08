// Keeping score, and nothing else.
//
// Apart from the rest of the harness because there is more than one game now, and `dotnet
// fsi` names a loaded file by its basename: two games each with a `Board.fs` cannot be
// loaded into one script, however happily they compile side by side in the project. So each
// game's checks load their own game and this, which loads nothing at all.

let mutable failures = 0

let report name expected actual =
    if actual = expected then
        printfn "ok   %s" name
    else
        failures <- failures + 1
        printfn "FAIL %s: expected %A, got %A" name expected actual

let finish () =
    printfn ""

    if failures = 0 then printfn "all checks passed" else printfn "%d check(s) failed" failures

    exit failures
