namespace Prototyping.Common

type Rng = private Rng of state: uint64

// SplitMix64. The state walks by a fixed odd stride and every value handed out is that state
// put through a finalising mix, so two seeds one apart still give unrelated streams - which
// is what lets a seat take its generator from `seed + place`.
module Rng =

    [<Literal>]
    let private Gamma = 0x9E3779B97F4A7C15UL

    let ofSeed (seed: uint64) = Rng seed

    let next (Rng state) =
        let state = state + Gamma
        let z = state
        let z = (z ^^^ (z >>> 30)) * 0xBF58476D1CE4E5B9UL
        let z = (z ^^^ (z >>> 27)) * 0x94D049BB133111EBUL
        (z ^^^ (z >>> 31)), Rng state

    let intBelow exclusiveMax rng =
        if exclusiveMax <= 0 then
            invalidArg (nameof exclusiveMax) "Upper bound must be positive."

        let value, rng = next rng

        // Folding by remainder leaves a slight bias towards the low end for bounds that do
        // not divide 2^64. At the sizes anything here shuffles or picks from, that is far
        // below what a game could show.
        int (value % uint64 exclusiveMax), rng
