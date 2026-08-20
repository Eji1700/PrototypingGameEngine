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
