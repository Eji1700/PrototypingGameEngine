namespace TCModel.Common

/// An immutable, deterministic pseudo-random generator (SplitMix64).
///
/// A generator is a value, handed back alongside whatever it produced, rather than
/// something that advances behind your back. That is what lets the game state stay
/// a value and folding a message over it stay a pure function - so a seed and a list
/// of messages reproduce a game exactly, and a restart can draw its next seed from
/// the generator already in play.
type Rng = private Rng of state: uint64

module Rng =

    [<Literal>]
    let private Gamma = 0x9E3779B97F4A7C15UL

    let ofSeed (seed: uint64) = Rng seed

    /// Advance the generator, producing the next 64 random bits.
    let next (Rng state) =
        let state = state + Gamma
        let z = state
        let z = (z ^^^ (z >>> 30)) * 0xBF58476D1CE4E5B9UL
        let z = (z ^^^ (z >>> 27)) * 0x94D049BB133111EBUL
        (z ^^^ (z >>> 31)), Rng state

    /// An integer in [0, exclusiveMax). Plain modulo: this game never asks for a
    /// bound above 63, and the bias that leaves across a 2^64 range is about one
    /// part in 10^18.
    let intBelow exclusiveMax rng =
        if exclusiveMax <= 0 then
            invalidArg (nameof exclusiveMax) "Upper bound must be positive."

        let value, rng = next rng
        int (value % uint64 exclusiveMax), rng
