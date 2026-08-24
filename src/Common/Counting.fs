namespace Prototyping.Common

/// A count and the word that agrees with it.
///
/// Every game says "3 turns" and "1 turn" somewhere, and each of them used to say it in its own
/// hand-written way - which is how "1 cell are still turning" and "leaves 1 seat(s)" got written.
/// The word comes first so a game can name its own counters once: `let turns = several "turn"
/// "turns"`, and every line after that reads `turns n`.
module Counting =

    /// "1 build", "2 builds". A count is read as its size, since a shortfall of two units is
    /// still "2 units".
    let several one many count =
        let word = if abs count = 1 then one else many
        $"{abs count} {word}"

    /// The same, with a word of its own for none at all: "no touches", "1 touch", "3 touches".
    let orNone none one many count =
        if count = 0 then none else several one many count

    /// "a stone", "3 stones" - where one of a thing is worth naming rather than counting.
    let a one many count =
        if abs count = 1 then $"a {one}" else several one many count
