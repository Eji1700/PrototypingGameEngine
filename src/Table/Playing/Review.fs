namespace Prototyping.Table

open System
open Prototyping.Common
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

/// What a line typed at a review comes to.
[<RequireQualifiedAccess; NoComparison; NoEquality>]
type Said<'Move, 'State, 'Notice> =
    | Moved of Review<'Move, 'State, 'Notice>
    | Printed of Review<'Move, 'State, 'Notice> * string

    /// Another way of drawing the board. The view belongs to the console rather than the review, as
    /// it does at a board, so it goes back to whoever holds it.
    | Looked of View<'Move, 'State, 'Notice>

    | Left

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

    /// A line said once. Through a seat's eyes every hidden move of another seat reads the same -
    /// "Player 1: sealed" - and seven of those in a row say less than one.
    let private once lines =
        lines
        |> List.fold
            (fun kept line ->
                match kept with
                | last :: _ when last = line -> kept
                | _ -> line :: kept)
            []
        |> List.rev

    /// The turn the review stands in, through its eye, as far as the stop it stands on.
    let thisTurn review =
        match turnsTo review |> List.tryLast with
        | None -> deal
        | Some(turn, entries) -> $"This turn ({turn})" :: once (entries |> List.collect (entry review))
        |> String.concat Environment.NewLine

    let private seats review =
        Playable.seatsOf review.Reviewed (Model.state review.Stops[0].Reached)

    /// The seat the board is drawn for. Everyone is drawn from the first seat with the curtain
    /// lifted: a game that lifts it shows every seat from there, and one chair for the whole review
    /// keeps the board from turning round every time the seat to play changes.
    let private seat review =
        match review.Beholding with
        | Eye.Everyone -> Seat.at 1
        | Eye.Seat seat -> seat

    let private margins review =
        { review.Margined with
            Curtain =
                match review.Beholding with
                | Eye.Everyone -> Lifted
                | Eye.Seat _ -> Hanging }

    let private eyeing review =
        match review.Beholding with
        | Eye.Everyone -> "as everyone, with nothing hidden"
        | Eye.Seat seat -> $"as {review.Reviewed.Seat seat} saw it"

    /// The same list for every turn so far, which is what `history` is in a review. The game's own
    /// history is never asked for here: it takes no curtain, so it could not show everything to
    /// everyone, and a game's history may tell a seat more of the others' orders than its board did.
    let private history review =
        let turns =
            match turnsTo review with
            | [] -> deal
            | turns ->
                turns
                |> List.collect (fun (turn, entries) -> $"Turn {turn}" :: once (entries |> List.collect (entry review)))

        String.concat Environment.NewLine ($"The game so far, {eyeing review}" :: "" :: turns)

    let private heading review =
        let place = place review

        let where =
            if place.Move = 0 then
                let turns = Counting.several "turn" "turns" place.Turns
                let moves = Counting.several "move" "moves" place.Moves
                $"the deal, with {turns} and {moves} to come"
            else
                $"turn {place.Turn} of {place.Turns}, move {place.Move} of {place.Moves}"

        $"Reviewing {review.Filed} - {where} - {eyeing review}"

    let private board (view: View<'Move, 'State, 'Notice>) review =
        view.Board (margins review) (seat review) (model review)

    /// The heading and the turn first, then the game's board. A window too short for the lot loses
    /// the foot of the board, and the turn's orders are what a review is for.
    let drawn view review =
        String.concat Environment.NewLine [ heading review; thisTurn review; ""; board view review ]

    let private screen review (pages: (int -> string) option) : Keys.Screen =
        // Nothing is said beside any row, so the list is laid in columns and takes three lines of a
        // window that has a board to show. The eye and the page are in the heading already, and the
        // typed words are under 'help'.
        let eye =
            Keys.sends (Some '3') "Eye" "" "eye next"
            |> Keys.turning (fun by -> if by < 0 then "eye back" else "eye next")

        // Only where the game turns pages of its own here: a row that turned nothing would be a row
        // somebody stood on and pressed keys at.
        let paging =
            match pages with
            | Some turn -> [ Keys.sends (Some '4') "Screen" "" (turn 1) |> Keys.turning turn ]
            | None -> []

        // One row a unit of stepping, turned either way: a row each for the four steps was four
        // lines where two will do, and the list is laid one row to a line until it has eight.
        let stepping digit says unit =
            Keys.sends (Some digit) says "" $"next {unit}"
            |> Keys.turning (fun by -> if by < 0 then $"back a {unit}" else $"next {unit}")

        { Title = "Review"
          Prose = []
          Rows =
            [ stepping '1' "Turn" "turn"; stepping '2' "Move" "move"; eye ]
            @ paging
            @ [ Keys.sends (Some '0') "Leave" "" "quit" ]
          Note = []
          Backs = Some "quit" }

    /// The review's own rows, with the game's page turn beside them where the game has one for the
    /// seat and the stop being drawn. Nothing seals: there is no turn here to give up.
    let steered view review =
        let pages =
            review.Reviewed.Steering (board view review) (margins review) (seat review) (model review)
            |> Option.bind (fun (steered: Keys.Steered) -> steered.Pages)

        { Keys.steered (screen review pages) with
            Pages = pages }

    let private eyes review =
        Eye.Everyone :: (seats review |> List.map Eye.Seat)

    let private turnedEye by review =
        let eyes = eyes review
        let at = eyes |> List.findIndex ((=) review.Beholding)
        looking eyes[Keys.wrapped (List.length eyes) at by] review

    let private helping =
        Scene.prose
            "A review walks a record without changing it. 'next turn' and 'back a turn' go a turn at a time, and 'next move' and 'back a move' a move; 'move 12', 'start' and 'end' go straight there. 'eye all' shows everything, and 'eye 2' only what the second seat was shown then. 'history' is every turn so far, through the same eye. Nothing can be played, and nothing is written. 'quit' leaves."

    let said (view: View<'Move, 'State, 'Notice>) (line: string) review =
        let game = review.Reviewed
        let printed text = Said.Printed(review, text)

        let margined change =
            Said.Moved
                { review with
                    Margined = change review.Margined }

        let switched wanted now = wanted |> Option.defaultValue (not now)

        match Commands.lowered line with
        | [ "next"; "turn" ] -> Said.Moved(turned 1 review)
        | [ "back"; "a"; "turn" ]
        | [ "back"; "turn" ]
        | [ "previous"; "turn" ] -> Said.Moved(turned -1 review)
        | [ "next"; "move" ]
        | [ "next" ] -> Said.Moved(stepped 1 review)
        | [ "back"; "a"; "move" ]
        | [ "back"; "move" ]
        | [ "previous"; "move" ]
        | [ "previous" ] -> Said.Moved(stepped -1 review)
        | [ "start" ] -> Said.Moved(standing 0 review)
        | [ "end" ] -> Said.Moved(standing (last review) review)
        | [ "move"; number ] ->
            match Commands.tryInt number with
            | Some at -> Said.Moved(standing at review)
            | None -> printed $"'{number}' is not a move. Say 'move 12' for the twelfth."
        | [ "eye"; "all" ]
        | [ "eye"; "everyone" ] -> Said.Moved(looking Eye.Everyone review)
        | [ "eye"; "next" ] -> Said.Moved(turnedEye 1 review)
        | [ "eye"; "back" ] -> Said.Moved(turnedEye -1 review)
        | [ "eye"; number ] ->
            match
                Commands.tryInt number
                |> Option.map Seat.at
                |> Option.filter (fun seat -> List.contains seat (seats review))
            with
            | Some seat -> Said.Moved(looking (Eye.Seat seat) review)
            | None ->
                printed
                    $"There is no seat '{number}' at this game. Say 'eye all', or a seat from 1 to {List.length (seats review)}."
        | "eye" :: _ -> printed "Say 'eye all' to see everything, or 'eye 2' to see what the second seat saw."
        | [ "back" ]
        | [ "menu" ] -> Said.Left
        | _ ->
            match Playable.read game line with
            | Error problem -> printed problem
            | Ok Nothing -> Said.Moved review
            | Ok Help -> printed helping
            | Ok Recount -> printed (history review)

            // `Answer` takes no curtain, so as everyone the answer is the first seat's, and says so.
            | Ok(Asking question) ->
                let answer = view.Answer (seat review) question (model review)

                match review.Beholding with
                | Eye.Everyone -> printed $"As {game.Seat(seat review)} saw it:{Environment.NewLine}{answer}"
                | Eye.Seat _ -> printed answer
            | Ok(Notes wanted) ->
                margined (fun margins ->
                    { margins with
                        Notes = switched wanted margins.Notes })
            | Ok(Listing wanted) ->
                margined (fun margins ->
                    { margins with
                        Commands = switched wanted margins.Commands })
            | Ok(Logging wanted) ->
                margined (fun margins ->
                    { margins with
                        Logged = switched wanted margins.Logged })
            | Ok(Showing screen) -> margined (fun margins -> { margins with Showing = screen })
            | Ok(Looking name) ->
                match Playable.byName view.Shown view.Palette game name with
                | Ok chosen -> Said.Looked chosen
                | Error problem -> printed problem
            | Ok(Hushing _) -> printed "A review makes no sound, so there is nothing to hush."
            | Ok Keep -> printed "A review writes nothing down. The record is left as it was."
            | Ok Leave -> Said.Left
            | Ok(Send _) -> printed "A review cannot be played. 'quit' leaves."
