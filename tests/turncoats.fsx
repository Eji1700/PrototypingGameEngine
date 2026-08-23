#load "Whole.fsx"
#load "Conforms.fsx"

open Whole

// Turncoats is the one game with no suite of its own: what it is played by is checked in
// `actions.fsx`, `ruling.fsx`, `outcome.fsx`, `knowledge.fsx` and `history.fsx`, and what it is
// read by in `view.fsx`, `html.fsx` and `solo.fsx`. This file is the seam itself, held to the same
// contract as the other six - and it is here rather than folded into one of those because the
// contract is one thing, run once per game, and it should be as easy to find for this game as for
// any other.

// === The seam every game fills in ===

Conforms.against playing 2 [ "negotiate"; "recruit r 8"; "negotiate"; "return r" ]


Checks.finish ()
