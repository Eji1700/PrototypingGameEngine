namespace Prototyping.Table

open System
open Prototyping.Engine

/// Whose eyes a review looks through: everybody's at once, with nothing hidden, or one seat's, with
/// only what that seat was shown at the time.
[<RequireQualifiedAccess>]
type Eye =
    | Everyone
    | Seat of PlayerId

[<NoComparison; NoEquality>]
type Stop<'Move, 'State, 'Notice> =
    {
        Reached: Model<'Move, 'State, 'Notice>

        /// The turn written on the entry that made this stop, which is the rules' turn of the state
        /// the move was made from. The deal has no entry, and takes the dealt state's own turn - but
        /// it stands as a run of its own whatever that number is, since nothing was ordered in it.
        During: int
    }

/// A record being looked back over.
///
/// Nothing is played in one. Every position was worked out once, when it was opened, by the same
/// fold that takes a game up - so each stop is the game exactly as it stood after that line - and
/// nothing here reaches `Solo`, a machine or a file. All a review holds besides is where it stands
/// and through whose eyes, and changing one never changes the other.
[<NoComparison; NoEquality>]
type Review<'Move, 'State, 'Notice> =
    private
        {
            Reviewed: Playable<'Move, 'State, 'Notice>
            Filed: string

            /// One for each entry in the journal, after the deal, so the stop at k holds exactly k
            /// entries. A line the game neither took nor said anything about wrote no entry, and so
            /// is never a stop to stand on.
            Stops: Stop<'Move, 'State, 'Notice> array

            Stood: int
            Beholding: Eye

            /// This console's margins. The curtain in them is never lifted here: it is worked out
            /// from the eye at the moment the board is drawn, so there is one place that decides it.
            Margined: Margins
        }

module Review =

    let opened (game: Playable<'Move, 'State, 'Notice>) (named: string) (reading: Transcript.Reading<'Move>) =
        let restarts =
            reading.Moves
            |> List.exists (fun msg ->
                match msg with
                | Restart _ -> true
                | Make _
                | Undo
                | Redo -> false)

        if restarts then
            Error $"'{named}' deals again part of the way through, so it is two games in one file, and cannot be reviewed as one."
        else
            match Update.start game.Rules reading.Players reading.Seed with
            | Error _ -> Error $"'{named}' asks for a number of players the game does not take."
            | Ok dealt ->
                let stops =
                    reading.Moves
                    |> List.scan (fun model msg -> Update.update game.Rules msg model) dealt
                    |> List.pairwise
                    |> List.choose (fun (before, after) ->
                        if Journal.length after.Journal > Journal.length before.Journal then
                            Some
                                { Reached = after
                                  During = game.Rules.Turn(Model.state before) }
                        else
                            None)

                Ok
                    { Reviewed = game
                      Filed = named
                      Stops =
                        Array.ofList (
                            { Reached = dealt
                              During = game.Rules.Turn(Model.state dealt) }
                            :: stops
                        )
                      Stood = 0
                      Beholding = Eye.Everyone
                      Margined = Margins.all }

    let model review = review.Stops[review.Stood].Reached

    let eye review = review.Beholding

    let private last review = Array.length review.Stops - 1

    // A run is the stops one after another that share a turn. The deal is never part of the run
    // after it, so the first turn is a step away from the deal rather than the same place.
    let private together review earlier later =
        earlier > 0 && review.Stops[earlier].During = review.Stops[later].During

    let rec private runStart review at =
        if at > 1 && together review (at - 1) at then runStart review (at - 1) else at

    let rec private runEnd review at =
        if at < last review && together review at (at + 1) then runEnd review (at + 1) else at

    let private starts review =
        [ for at in 1 .. last review do
              if not (together review (at - 1) at) then yield at ]

    let place review =
        let begun = starts review

        {| Turn = begun |> List.filter (fun start -> start <= review.Stood) |> List.length
           Turns = List.length begun
           Move = review.Stood
           Moves = last review |}

    let private standing at review =
        { review with
            Stood = at |> max 0 |> min (last review) }

    let stepped by review = standing (review.Stood + by) review

    /// Forward lands on the last stop of the next turn, so the board shows how that turn came out and
    /// everything ordered in it is listed under it. Back lands on the last stop of the turn before.
    /// A turn taken back and played again is a run of its own each time, which is the game as it was
    /// really played.
    let rec turned by review =
        if by > 0 then
            let ending = runEnd review review.Stood

            let next = if ending < last review then runEnd review (ending + 1) else ending

            turned (by - 1) (standing next review)
        elif by < 0 then
            turned (by + 1) (standing (runStart review review.Stood - 1) review)
        else
            review

    let looking eye review = { review with Beholding = eye }

    /// What one entry looks like through the eye. Everybody's orders are shown to everyone; through a
    /// seat's eyes, its own orders are shown and nobody else's are - only what the seat was told of
    /// them, which the game words for it.
    ///
    /// A move taken back or made again is never named through a seat's eyes, because the table's
    /// own wording of either prints the move, and the move may not be the seat's: at one keyboard,
    /// the seat that says `undo` is the one to play, and what it takes back is the move just made by
    /// somebody else. Its own `undo` already says what it did; anybody else's is said without it.
    let private entry review (entry: Entry<'Move, 'Notice>) =
        let game = review.Reviewed
        let actor = game.Seat entry.Actor

        let walked told =
            match told with
            | TookBack _
            | MadeAgain _ -> true
            | _ -> false

        let order, told =
            match review.Beholding with
            | Eye.Everyone -> Some(game.Write entry.Asked), entry.Told |> List.map (Playable.told game)
            | Eye.Seat seat when entry.Actor = seat ->
                Some(game.Write entry.Asked),
                entry.Told
                |> List.filter (walked >> not)
                |> List.map (Playable.toldSeenBy game seat)
            | Eye.Seat seat ->
                None,
                entry.Told
                |> List.map (fun told ->
                    match told with
                    | TookBack _ -> "took something back"
                    | MadeAgain _ -> "made something again"
                    | told -> Playable.toldSeenBy game seat told)

        let told = told |> List.filter (fun line -> line <> "")

        let further =
            told
            |> List.skip (min 1 (List.length told))
            |> List.map (fun line -> "      " + line)

        match order, told with
        | Some order, [] -> [ $"  {actor}: {order}" ]
        | Some order, first :: _ -> $"  {actor}: {order}  -  {first}" :: further
        | None, [] -> [ $"  {actor}: sealed" ]
        | None, first :: _ -> $"  {actor}: {first}" :: further

    /// Every turn up to where the review stands, each as the entries of its run, numbered as the
    /// heading numbers them.
    let private turnsTo review =
        let entries = Journal.entries (model review).Journal |> Array.ofList

        starts review
        |> List.filter (fun start -> start <= review.Stood)
        |> List.mapi (fun index start ->
            index + 1, [ for at in start .. min review.Stood (runEnd review start) -> entries[at - 1] ])

    let private deal = [ "The deal"; "  Nothing has been played yet." ]

    /// The turn the review stands in, through its eye, as far as the stop it stands on.
    let thisTurn review =
        match turnsTo review |> List.tryLast with
        | None -> deal
        | Some(turn, entries) -> $"This turn ({turn})" :: (entries |> List.collect (entry review))
        |> String.concat Environment.NewLine
