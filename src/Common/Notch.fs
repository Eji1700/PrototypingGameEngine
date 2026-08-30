namespace Prototyping.Common

/// How a clock is wound: a notch either way, or straight to one. Qualified, because a game with
/// a `Speed` of its own - a protocol, say - would otherwise find this one first.
[<RequireQualifiedAccess>]
type Winding =
    | Faster
    | Slower
    | Speed of notch: int

/// A clock's speed as a player winds it: nine notches, and the middle one to start on. Three
/// games run on one, and what a notch is worth in time is each game's own; this is only which
/// notches there are and what winding one does.
module Notch =

    [<Literal>]
    let Slowest = 1

    [<Literal>]
    let Fastest = 9

    [<Literal>]
    let Ordinary = 5

    let holds notch = notch >= Slowest && notch <= Fastest

    /// The notch a clock stands at after this winding, or nothing where nothing changes - already
    /// at the end it is being wound towards, or already at the notch asked for. A game answers that
    /// with nothing at all rather than a refusal, so a key held down does not fill the record.
    let wound winding current =
        match winding with
        | Winding.Faster when current >= Fastest -> None
        | Winding.Slower when current <= Slowest -> None
        | Winding.Speed notch when notch = current -> None
        | Winding.Faster -> Some(current + 1)
        | Winding.Slower -> Some(current - 1)
        | Winding.Speed notch -> Some notch

    let written =
        function
        | Winding.Faster -> "faster"
        | Winding.Slower -> "slower"
        | Winding.Speed notch -> $"speed {notch}"

    let unknown (said: int) =
        $"Speed {said}? The clock winds from {Slowest} to {Fastest}, or say 'faster' and 'slower' - which is what + and - do."

    /// The words a winding is typed as. Nothing for a line that is not one; a complaint for a speed
    /// that is not a number, since the line was plainly meant to be one.
    let (|Winds|_|) (words: string list) =
        match words with
        | [ "faster" ]
        | [ "quicker" ]
        | [ "+" ] -> Some(Ok Winding.Faster)
        | [ "slower" ]
        | [ "-" ] -> Some(Ok Winding.Slower)
        | [ "speed"; notch ] ->
            match System.Int32.TryParse notch with
            | true, notch -> Some(Ok(Winding.Speed notch))
            | _ -> Some(Error $"'{notch}' is not a speed. They run from {Slowest} to {Fastest}.")
        | _ -> None
