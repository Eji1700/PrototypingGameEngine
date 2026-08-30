namespace Prototyping.Net

open System

module Protocol =

    [<Literal>]
    let Path = "/table"

    /// The spans both ends of the wire keep time by. The table sends a beat every `KeepAlive`, and
    /// a console gives the table up for gone after `GivenUp` without one - so the second has to be
    /// at least twice the first, or one late beat would lose a table that was still there. Kept
    /// together so the two ends cannot drift apart; the page waits six beats before saying the
    /// same, in `Page.fs`.
    let KeepAlive = TimeSpan.FromSeconds 15.0
    let GivenUp = TimeSpan.FromSeconds 60.0
    let Handshake = TimeSpan.FromSeconds 30.0

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

        [<Literal>]
        let Rang = "Rang"
