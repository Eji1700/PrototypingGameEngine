namespace TCModel

/// An immutable, deterministic pseudo-random generator (SplitMix64).
/// Values of this type are passed around explicitly so that every random
/// decision in the game is reproducible from a seed.
type Rng =
    private
    | Rng of state: uint64

/// A random computation: given a generator state it yields a value and the next state.
type Rand<'T> = Rng -> 'T * Rng

module Rng =

    [<Literal>]
    let private Gamma = 0x9E3779B97F4A7C15UL

    /// Create a generator from a seed.
    let ofSeed (seed: uint64) = Rng seed

    /// Advance the generator, producing the next 64 random bits.
    let next (Rng state) : uint64 * Rng =
        let state = state + Gamma
        let z = state
        let z = (z ^^^ (z >>> 30)) * 0xBF58476D1CE4E5B9UL
        let z = (z ^^^ (z >>> 27)) * 0x94D049BB133111EBUL
        (z ^^^ (z >>> 31)), Rng state

module Rand =

    /// A computation that yields `value` without consuming randomness.
    let retn value : Rand<'T> = fun rng -> value, rng

    let bind (f: 'T -> Rand<'U>) (m: Rand<'T>) : Rand<'U> =
        fun rng ->
            let value, rng = m rng
            f value rng

    let map (f: 'T -> 'U) (m: Rand<'T>) : Rand<'U> =
        fun rng ->
            let value, rng = m rng
            f value, rng

    /// Run a computation from a seed, discarding the final generator state.
    let evaluate (seed: uint64) (m: Rand<'T>) = m (Rng.ofSeed seed) |> fst

    /// A uniformly distributed integer in [0, exclusiveMax), free of modulo bias.
    let intBelow (exclusiveMax: int) : Rand<int> =
        if exclusiveMax <= 0 then
            invalidArg (nameof exclusiveMax) "Upper bound must be positive."

        fun rng ->
            let bound = uint64 exclusiveMax
            // 2^64 % bound: the tail of values that would skew the distribution.
            let overhang = (System.UInt64.MaxValue % bound + 1UL) % bound

            let rec draw rng =
                let value, rng = Rng.next rng
                if overhang <> 0UL && value >= System.UInt64.MaxValue - overhang + 1UL then
                    draw rng
                else
                    int (value % bound), rng

            draw rng

    /// Thread a random computation over a list while carrying an accumulator.
    let mapFold (f: 'State -> 'T -> Rand<'U * 'State>) (state: 'State) (items: 'T list) : Rand<'U list * 'State> =
        fun rng ->
            let (results, state), rng =
                items
                |> List.fold
                    (fun ((acc, state), rng) item ->
                        let (value, state), rng = f state item rng
                        (value :: acc, state), rng)
                    (([], state), rng)

            (List.rev results, state), rng

    /// A uniformly shuffled copy of the list (Fisher-Yates).
    let shuffle (items: 'T list) : Rand<'T list> =
        fun rng ->
            let slots = List.toArray items
            let mutable rng = rng

            for i in slots.Length - 1 .. -1 .. 1 do
                let j, next = intBelow (i + 1) rng
                rng <- next
                let held = slots[i]
                slots[i] <- slots[j]
                slots[j] <- held

            List.ofArray slots, rng

type RandBuilder() =
    member _.Return value = Rand.retn value
    member _.ReturnFrom(m: Rand<'T>) = m
    member _.Bind(m: Rand<'T>, f: 'T -> Rand<'U>) = Rand.bind f m
    member _.Zero() = Rand.retn ()
    member _.Delay(f: unit -> Rand<'T>) : Rand<'T> = fun rng -> f () rng

[<AutoOpen>]
module RandComputation =
    /// Computation expression for sequencing random draws.
    let rand = RandBuilder()
