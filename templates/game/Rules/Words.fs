namespace Prototyping.MyGame

open Prototyping.Common
open Prototyping.Engine

module Words =

    // Never build a count by hand. Nought and one are where counts read wrong, and `Counting` is
    // the only place in the program that knows how to get them right.
    let private tokens = Counting.several "token" "tokens"

    let private remaining = Counting.orNone "nothing" "token" "tokens"

    let player playerId = $"Player {PlayerId.value playerId}"

    let seated yours playerId =
        player playerId + (if yours then " (you)" else "")

    let ending =
        function
        | TookTheLast seat -> $"{player (Seat.at seat)} took the last of them"
        | Abandoned seat -> $"{player (Seat.at seat)} walked away"

    let event =
        function
        | Taken(seat, count, left) -> $"{player (Seat.at seat)} takes {tokens count}, leaving {remaining left}."
        | GameEnded e -> $"The game is over: {ending e}."

    let rejection =
        function
        | NotThatMany said -> $"{tokens said}? Take 1 to {Row.Most} of them at a time."
        | NotSoManyLeft(said, left) -> $"{tokens said}? The row is down to {remaining left}."

    /// A move as the line that would have made it. This and `Parse.line` are the two ends of the
    /// same thing, and a record is nothing but the lines this writes - so a move that does not
    /// read back as itself is a game that cannot be replayed. `Conforms.against` checks that.
    let command =
        Msg.written (function
            | Take count -> $"take {count}"
            | Resign -> "resign")

    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    // Nothing here is hidden, so every seat is told the same thing. A game with anything secret in
    // it says less here than `said` does, and that is the only place the difference lives.
    let saidTo _ notice = said notice
