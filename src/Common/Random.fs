namespace TCModel.Common

type Rng = private Rng of state: uint64

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
        int (value % uint64 exclusiveMax), rng
