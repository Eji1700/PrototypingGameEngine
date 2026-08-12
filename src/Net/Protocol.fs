namespace TCModel.Net

/// What crosses the wire, and nothing about how it gets there.
///
/// There is no second language here either. A player sends the line they typed, exactly
/// as they would have typed it at their own keyboard, and what comes back is the screen
/// they would have been looking at. The game's own parser reads the one and its own
/// renderer writes the other, so there is no third thing to keep in step with the rules.
module Protocol =

    /// Where the table listens. A player joining says an address; this is the rest.
    ///
    /// The port that goes with it is `Reach.DefaultPort`, and lives there rather than here
    /// because how far a table can be reached is settled before there is a wire: the command
    /// line says it, and the console filling out an address needs it.
    [<Literal>]
    let Path = "/table"

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

        [<Literal>]
        let GotUp = "GotUp"

        [<Literal>]
        let Nudged = "Nudged"

// What actually crosses - `ToPlayer` and `Post` - is in the Console layer, because a table
// at one keyboard says the same things and has no wire to say them over.
