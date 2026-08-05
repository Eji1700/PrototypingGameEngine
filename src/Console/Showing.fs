namespace TCModel.Console

/// What a table shows one console, and which console it is for.
///
/// There are two kinds of table in this program - one keyboard with everybody round it,
/// and one machine each - and both answer a typed line the same way: with a list of things
/// to show, each addressed to somebody. That is why this is here rather than with the
/// networked half. A wire is one way of getting a screen to a person and not the thing
/// being described.
///
/// Only strings and numbers, so nothing has to teach a serialiser how the game's own types
/// are shaped for the times a wire is involved after all.
type ToPlayer =
    /// You are sitting at this seat, and this token is what brings you back to it. Only a
    /// table with seats at it ever says this.
    | Seated of seat: int * token: string
    /// A board to look at, drawn for the console it is going to and nobody else.
    | Screen of text: string
    /// A line of news with no board to go with it.
    | Told of text: string
    /// There is no seat here for you, and this is why.
    | TurnedAway of why: string

/// One thing to say and the console to say it to.
type Post = { To: string; Say: ToPlayer }
