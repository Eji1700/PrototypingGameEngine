namespace TCModel.Engine

/// Which seat at the table, and nothing else about who is in it.
///
/// The engine's own, rather than the game's, because everything above the rules speaks it:
/// the record says which seat asked for a move, a table hands out one seat per person, a
/// board is drawn for the seat reading it, and none of that is a fact about stones or cards
/// or anything else a game is made of.
///
/// Private, so the only ids in circulation are ones a table minted. A game deals its players
/// through `Seat.at` and hands them out; nothing else can invent one, and an id minted for
/// one game says nothing about another.
type PlayerId = private PlayerId of int

module Seat =

    /// The seat at that place in the dealing order, counting from one.
    let at place = PlayerId place

module PlayerId =
    let value (PlayerId n) = n
