namespace TCModel.Table

open TCModel.Engine

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
    /// You are up from the table, and this is what there is to say about coming back.
    ///
    /// The one thing a table says that is not about the game. A player who puts a game down
    /// has to be told they have - a seat left quietly looks exactly like a seat still being
    /// played from - and what a console *does* about it is the far end's: a terminal says the
    /// words, stops reading and hangs up, and a page shows them if it is still there to.
    ///
    /// It carries words for the same reason `TurnedAway` does. Getting up from a table with
    /// seats at it is not the end of anything: the seat is kept, and there is a line that
    /// brings the player back to it.
    | GotUp of said: string
    /// The game has come round to you, and nothing you did brought it round.
    ///
    /// It carries no words, because it is not something to read. A player who is looking at
    /// the board can already see whose turn it is - the board says so at the top of it - and
    /// this is for the one who is not looking. So the table says only that the turn arrived
    /// unasked, and what to do about that is settled at the far end: a terminal rings, and a
    /// page marks itself and says so out loud if it has been allowed to. Only the far end
    /// can know whether anybody is there, and only the table can know whether they asked.
    | Nudged

/// One thing to say and the console to say it to.
type Post = { To: string; Say: ToPlayer }
