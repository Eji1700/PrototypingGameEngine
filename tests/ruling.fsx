// Checks the ruling cascade, and in particular that a colour knocked out by one
// measure never comes back through a later one.
//
//   dotnet fsi tests/ruling.fsx

#load "Harness.fsx"

open TCModel.Domain
open Harness

let check name expected axe flag stones =
    report name expected (Ruling.decide (Pile.ofCounts axe) (Pile.ofCounts flag) (Pile.ofCounts stones))

check "outright winner" (RuledBy Black) [] [] [ Red, 2; Blue, 2; Black, 3 ]
check "single stone rules" (RuledBy Red) [] [] [ (Red, 1) ]
check "empty region has no ruler" Unclaimed [ (Red, 5) ] [ (Blue, 5) ] []
check "empty region ignores a loaded Axe" Unclaimed [ (Red, 9) ] [] []

check "two-way tie broken by the Axe" (RuledBy Blue) [ (Blue, 1) ] [] [ Blue, 2; Black, 2 ]

check "two-way tie broken by the Flag" (RuledBy Black) [ Blue, 1; Black, 1 ] [ (Black, 2) ] [ Blue, 2; Black, 2 ]

check "level all the way through" (Contested [ Blue; Black ]) [] [] [ Blue, 2; Black, 2 ]
check "three-way tie, Axe settles it" (RuledBy Red) [ (Red, 1) ] [] [ Red, 1; Blue, 1; Black, 1 ]

check
    "three-way tie narrowed to two, Flag settles it"
    (RuledBy Blue)
    [ Red, 1; Blue, 1 ]
    [ (Blue, 1) ]
    [ Red, 1; Blue, 1; Black, 1 ]

// Elimination: a colour out of contention stays out, however it stands later on.
check "colour out on count cannot win the Axe" (Contested [ Blue; Black ]) [ (Red, 9) ] [] [ Red, 1; Blue, 2; Black, 2 ]

check "colour out on count cannot win the Flag" (Contested [ Blue; Black ]) [] [ (Red, 9) ] [ Red, 1; Blue, 2; Black, 2 ]

check
    "colour out on the Axe cannot win the Flag"
    (RuledBy Blue)
    [ Blue, 2; Black, 2; Red, 1 ]
    [ Red, 9; Blue, 1 ]
    [ Red, 2; Blue, 2; Black, 2 ]

check
    "out on the Axe, and the Flag cannot separate the rest"
    (Contested [ Blue; Black ])
    [ Blue, 2; Black, 2; Red, 1 ]
    [ (Red, 9) ]
    [ Red, 2; Blue, 2; Black, 2 ]

finish ()
