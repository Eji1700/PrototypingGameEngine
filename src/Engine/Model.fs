namespace TCModel.Engine

/// The whole of what a game in progress keeps.
///
/// The present position is not held here: it is whatever the timeline's finger is on, so
/// there is one place a state can come from and no way for a stored "current game" to drift
/// from the history behind it.
type Model<'Move, 'State, 'Notice> =
    {
        Timeline: Timeline<'Move, 'State>
        /// The durable record of play, saved when the game is done.
        Journal: Journal<'Move, 'Notice>
        /// Newest first: the last few lines of chatter for the screen. This is not the
        /// record - it also carries input nobody could make sense of, which never became a
        /// move at all.
        Log: Told<'Move, 'Notice> list
    }

module Model =

    /// Longest run of notices kept on screen. The journal keeps the rest.
    [<Literal>]
    let LogDepth = 12

    /// Where the game stands, which is the one thing every game has and the only thing the
    /// engine ever asks a game about.
    let state model = Timeline.present model.Timeline

    let seed model = Journal.seed model.Journal

    let players model = Journal.players model.Journal

    let record notice model =
        { model with
            Log = notice :: model.Log |> List.truncate LogDepth }

    /// Write down that something was asked for and what came of it, and move the game to
    /// where the new timeline points.
    ///
    /// Everything that happens goes through this one door, so nothing can reach the record
    /// without reaching the screen, or the screen without the record.
    let happen (rules: Rules<_, _, _>) asked told timeline model =
        let asking = state model

        { model with
            Timeline = timeline
            Journal = Journal.write (rules.Turn asking) (rules.Active asking) asked told model.Journal
            Log = (List.rev told) @ model.Log |> List.truncate LogDepth }
