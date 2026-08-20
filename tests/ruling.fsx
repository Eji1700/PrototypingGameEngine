#load "Harness.fsx"

open TCModel.Turncoats
open Harness

let check name expected axe flag stones =
    report name expected (Ruling.decide (Pile.ofCounts axe) (Pile.ofCounts flag) (Pile.ofCounts stones))

check "outright winner" (RuledBy Green) [] [] [ Red, 2; Blue, 2; Green, 3 ]
check "single stone rules" (RuledBy Red) [] [] [ (Red, 1) ]
check "empty region has no ruler" Unclaimed [ (Red, 5) ] [ (Blue, 5) ] []
check "empty region ignores a loaded Axe" Unclaimed [ (Red, 9) ] [] []

check "two-way tie broken by the Axe" (RuledBy Blue) [ (Blue, 1) ] [] [ Blue, 2; Green, 2 ]

check "two-way tie broken by the Flag" (RuledBy Green) [ Blue, 1; Green, 1 ] [ (Green, 2) ] [ Blue, 2; Green, 2 ]

check "level all the way through" (Contested [ Blue; Green ]) [] [] [ Blue, 2; Green, 2 ]
check "three-way tie, Axe settles it" (RuledBy Red) [ (Red, 1) ] [] [ Red, 1; Blue, 1; Green, 1 ]

check
    "three-way tie narrowed to two, Flag settles it"
    (RuledBy Blue)
    [ Red, 1; Blue, 1 ]
    [ (Blue, 1) ]
    [ Red, 1; Blue, 1; Green, 1 ]

check "colour out on count cannot win the Axe" (Contested [ Blue; Green ]) [ (Red, 9) ] [] [ Red, 1; Blue, 2; Green, 2 ]

check "colour out on count cannot win the Flag" (Contested [ Blue; Green ]) [] [ (Red, 9) ] [ Red, 1; Blue, 2; Green, 2 ]

check
    "colour out on the Axe cannot win the Flag"
    (RuledBy Blue)
    [ Blue, 2; Green, 2; Red, 1 ]
    [ Red, 9; Blue, 1 ]
    [ Red, 2; Blue, 2; Green, 2 ]

check
    "out on the Axe, and the Flag cannot separate the rest"
    (Contested [ Blue; Green ])
    [ Blue, 2; Green, 2; Red, 1 ]
    [ (Red, 9) ]
    [ Red, 2; Blue, 2; Green, 2 ]

finish ()
