// Checks the ruling cascade, and in particular that a colour knocked out by one
// measure never comes back through a later one.
//
//   dotnet fsi tests/ruling.fsx

#load "../src/Rng.fs"
#load "../src/Domain.fs"
#load "../src/Ruling.fs"

open TCModel

let mutable failures = 0

let check name expected axe flag stones =
    let actual = Ruling.decide (Pile.ofCounts axe) (Pile.ofCounts flag) (Pile.ofCounts stones)

    if actual = expected then
        printfn "ok   %s" name
    else
        failures <- failures + 1
        printfn "FAIL %s: expected %A, got %A" name expected actual

check "outright winner" (Ruling.RuledBy Black) [] [] [ Red, 2; Blue, 2; Black, 3 ]
check "single stone rules" (Ruling.RuledBy Red) [] [] [ (Red, 1) ]
check "empty region has no ruler" Ruling.Unclaimed [ (Red, 5) ] [ (Blue, 5) ] []
check "empty region ignores a loaded Axe" Ruling.Unclaimed [ (Red, 9) ] [] []

check "two-way tie broken by the Axe" (Ruling.RuledBy Blue) [ (Blue, 1) ] [] [ Blue, 2; Black, 2 ]

check
    "two-way tie broken by the Flag"
    (Ruling.RuledBy Black)
    [ Blue, 1; Black, 1 ]
    [ (Black, 2) ]
    [ Blue, 2; Black, 2 ]

check "level all the way through" (Ruling.Contested [ Blue; Black ]) [] [] [ Blue, 2; Black, 2 ]
check "three-way tie, Axe settles it" (Ruling.RuledBy Red) [ (Red, 1) ] [] [ Red, 1; Blue, 1; Black, 1 ]

check
    "three-way tie narrowed to two, Flag settles it"
    (Ruling.RuledBy Blue)
    [ Red, 1; Blue, 1 ]
    [ (Blue, 1) ]
    [ Red, 1; Blue, 1; Black, 1 ]

// Elimination: a colour out of contention stays out, however it stands later on.
check
    "colour out on count cannot win the Axe"
    (Ruling.Contested [ Blue; Black ])
    [ (Red, 9) ]
    []
    [ Red, 1; Blue, 2; Black, 2 ]

check
    "colour out on count cannot win the Flag"
    (Ruling.Contested [ Blue; Black ])
    []
    [ (Red, 9) ]
    [ Red, 1; Blue, 2; Black, 2 ]

check
    "colour out on the Axe cannot win the Flag"
    (Ruling.RuledBy Blue)
    [ Blue, 2; Black, 2; Red, 1 ]
    [ Red, 9; Blue, 1 ]
    [ Red, 2; Blue, 2; Black, 2 ]

check
    "out on the Axe, and the Flag cannot separate the rest"
    (Ruling.Contested [ Blue; Black ])
    [ Blue, 2; Black, 2; Red, 1 ]
    [ (Red, 9) ]
    [ Red, 2; Blue, 2; Black, 2 ]

printfn ""

if failures = 0 then
    printfn "all checks passed"
else
    printfn "%d check(s) failed" failures

exit failures
