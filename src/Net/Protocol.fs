namespace TCModel.Net

/// What crosses the wire, and nothing about how it gets there.
///
/// There is no second language here either. A player sends the line they typed, exactly
/// as they would have typed it at their own keyboard, and what comes back is the screen
/// they would have been looking at. The game's own parser reads the one and its own
/// renderer writes the other, so there is no third thing to keep in step with the rules.
module Protocol =

    /// Where the table listens. A player joining says an address; this is the rest.
    [<Literal>]
    let Path = "/table"

    [<Literal>]
    let DefaultPort = 5000

    /// The calls each end makes on the other, named once so both spell them the same.
    module Call =

        [<Literal>]
        let Join = "Join"

        [<Literal>]
        let Say = "Say"

        [<Literal>]
        let Seated = "Seated"

        [<Literal>]
        let Screen = "Screen"

        [<Literal>]
        let Told = "Told"

        [<Literal>]
        let TurnedAway = "TurnedAway"

/// What the table says back to one console. Only strings and numbers go over the wire,
/// so nothing has to teach a serialiser how the game's own types are shaped.
type ToPlayer =
    /// You are sitting at this seat, and this token is what brings you back to it.
    | Seated of seat: int * token: string
    /// A board to look at, drawn for the console it is going to and nobody else.
    | Screen of text: string
    /// A line of news with no board to go with it.
    | Told of text: string
    /// There is no seat here for you, and this is why.
    | TurnedAway of why: string

/// One thing to say and the console to say it to.
type Post = { To: string; Say: ToPlayer }
