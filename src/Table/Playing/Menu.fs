namespace TCModel.Table

open TCModel.Common

/// The front door: what a person can ask for before there is a game to play.
///
/// Pure, like the rest of this layer - it says what the menu reads like and what a typed
/// line means, and leaves the reading and the writing to `Program`.
///
/// The game comes in as an argument everywhere rather than being known. How many may sit
/// down, which machines there are, what the table is called and how it can be drawn are all
/// the game's answers, so this file has none of them and the same screens serve any game.
module Menu =

    /// What the menu was asked for. Every one of these either starts a game or comes
    /// back round to a screen, so there is nothing else for the front door to do.
    [<NoComparison; NoEquality>]
    type Choice<'Move, 'State, 'Notice> =
        /// Deal this seating and play it at this keyboard. A seed left unsaid is taken from
        /// the clock, so the game is a new one every time.
        | Deal of seating: Sitter list * seed: uint64 option
        /// The same game, read as a page in a browser.
        | Serve of seating: Sitter list * seed: uint64 option * reach: Reach option
        /// Open it as a table and wait for its seats to be taken. Which of the three this
        /// is, is the seating's own answer rather than a separate question - see `dealing`.
        | Host of seating: Sitter list * seed: uint64 option * reach: Reach option
        /// Not a game yet: show these seats, so that one of them can be changed. This is
        /// what every row on the seat list comes to, and what makes walking a seat along a
        /// way of typing rather than a thing the screen has to remember.
        | Sitting of seating: Sitter list * reach: Reach option
        /// Nor this: show how far that table will reach, so that one part of it can be
        /// changed. The seating comes with it because a screen here is still a screen about
        /// the game being opened, and backing out of it has to land back at the seats.
        | Reaching of seating: Sitter list * reach: Reach
        /// Sit down at somebody else's table, saying the word at its door if it has one.
        ///
        /// No token here, unlike the command line: coming back to a seat you already hold is
        /// a line the program writes for you when you lose it, and what it writes is a
        /// command line. What the menu is for is the other thing - a table you were told
        /// about, an address and a word.
        | Join of address: string * code: string option
        | Replay of path: string
        /// Not a game yet: the saved games there are, so that one of them can be picked
        /// rather than remembered. Taking a game up again has always worked here; what it
        /// wanted was a path typed at a screen that never offered one.
        | Continuing
        /// Show the rules and the commands at length.
        | Rules
        /// Read the game a different way from here on.
        | Looking of View<'Move, 'State, 'Notice>
        /// Settle what is drawn in what colour.
        | Options
        | Leave
        /// Nothing was typed, so the screen simply asks again.
        | Waiting
        /// Back to whatever this screen was opened from, which at the front door is here.
        | Backing

    // --- the screens -------------------------------------------------------------------
    //
    // Every row here stands for a line the menu itself can read, so nothing on a screen is a
    // second way of meaning something: picking with the arrows and typing the words arrive
    // at `choose` together.

    /// How many are playing, which has to be asked before the seats can be: the list of
    /// seats is exactly as long as the answer, so the two can never disagree - which is the
    /// sum the old menu could get wrong, and did.
    ///
    /// The numbers are taken from the game rather than written out, so the menu cannot come
    /// to offer one the game would refuse, and each is picked by its own digit: the key
    /// that says three *is* the three, rather than being the third thing on a list.
    let private counting game : Keys.Screen =
        { Title = "A new game"
          Prose = [ "How many seats at the table?" ]
          Rows =
            [ for players in game.Fewest .. game.Most ->
                  Keys.sends
                      (Some(char (int '0' + players)))
                      $"{players} players"
                      ""
                      $"seats {Seating.line (Seating.here players)}" ]
          Note =
            [ "They all start out as people at this keyboard. The next list is where a seat"
              "becomes the machine's, or somebody else's." ]
          Backs = None }

    /// A line that says the whole of what is being opened: who is in each seat, and how far
    /// the table will reach. Two values, one line, because every row on both screens below
    /// stands for the whole thing after its own change - there is nothing either screen has
    /// to remember, and no way for the two halves to drift apart between them.
    let private saying said sitters reach =
        $"{said} {Seating.line sitters} via {Reach.line reach}"

    /// The seats themselves: what each one is, and what changes it.
    ///
    /// A seat's row stands for the whole seating with that one seat walked along, so nothing
    /// is remembered between presses - the line says the whole of the change, and the screen
    /// that comes back is built from the answer rather than from a memory of it. The same
    /// bargain the colour screen keeps, and for the same reason.
    let seats game sitters reach : Keys.Screen =
        let skills = game.Skills

        let walking at step =
            let sitter = List.item at sitters
            saying "seats" (Seating.seated at (Seating.walked skills step sitter) sitters) reach

        let seat at sitter =
            Keys.sends
                (Keys.nth at)
                $"Seat {at + 1}"
                (sprintf "%-8s%s" (Seating.says sitter) (Seating.describe skills sitter))
                (walking at 1)
            |> Keys.turning (walking at)

        let hosted = Seating.hosted sitters
        let taken = List.length sitters
        let opening said = saying said sitters reach

        { Title = "Who is playing"
          Prose = [ "Each seat is somebody here, the machine, or somebody at their own machine." ]
          Rows =
            (sitters |> List.mapi seat)
            @ [ if hosted then
                    Keys.sends (Keys.nth taken) "Open the table" "and wait for the seats to be taken" (opening "play")
                else
                    Keys.sends (Keys.nth taken) "Deal" "and play it here at this keyboard" (opening "play") ]
            @ (if hosted then
                   []
               else
                   [ Keys.sends (Keys.nth (taken + 1)) "In a browser" "the same game, read as a page" (opening "serve") ])
            @ [ Keys.sends
                    (Keys.nth (taken + (if hosted then 1 else 2)))
                    "How it is reached"
                    (Reach.reading reach)
                    (opening "reaches") ]
          Note =
            [ $"Left and right walk the one marked -> through {Seating.names skills}."
              "Enter takes the next one along, and so does the seat's own number."
              ""
              "A table with anybody joining is opened over the network and waited at. One without"
              "is dealt and played here, and the machine answers between your turns."
              ""
              $"Or type it: 'seats {Seating.line (Seating.here game.Most)}' sets them all at once,"
              $"and 'play {Seating.line (Seating.here game.Most)}' deals that outright." ]
          Backs = Some "back" }

    /// How far the table will reach: what it takes to sit down at it, and what carries what
    /// it says.
    ///
    /// Two of these are worth walking and two are worth typing, which is the whole layout.
    /// A row that wants typing writes the line exactly as it stands and puts the name of the
    /// one part being changed on the end of it - the last word about a part is the one that
    /// counts, so nothing has to be taken out of a line to put something else in.
    ///
    /// The word comes in from outside rather than being made up here, for the reason nothing
    /// in this file makes anything up: a screen is a function of what it is showing, and a
    /// word made up on the way past would be a different word every time the screen was
    /// drawn. Whoever opened the menu made one, and this is where it is offered.
    let reaches word sitters (reach: Reach) : Keys.Screen =
        let after change = saying "reaches" sitters change

        let door =
            let other =
                match reach.Doorway with
                | Ajar -> Locked word
                | Locked _ -> Ajar

            Keys.sends
                (Keys.nth 0)
                "The door"
                (match reach.Doorway with
                 | Locked said -> $"a word: {said}"
                 | Ajar -> "open - whoever can reach the address may sit down")
                (after { reach with Doorway = other })
            |> Keys.turning (fun _ -> after { reach with Doorway = other })

        let carried =
            let other =
                match reach.Wrapping with
                | InTheClear -> Ahead
                | Kept _
                | Ahead -> InTheClear

            Keys.sends
                (Keys.nth 1)
                "Carried"
                (match reach.Wrapping with
                 | InTheClear -> "in the clear - right on a network you trust, and nowhere else"
                 | Ahead -> "https, ended by a tunnel or proxy in front of this"
                 | Kept(certificate, _) -> $"https, with the certificate at {certificate}")
                (after { reach with Wrapping = other })
            |> Keys.turning (fun _ -> after { reach with Wrapping = other })

        { Title = "How far it reaches"
          Prose = [ "What it takes to sit down at this table, and what carries what it says." ]
          Rows =
            [ door
              carried
              Keys.types (Keys.nth 2) "The port" (string reach.Port) $"{after reach} port:"
              Keys.types
                  (Keys.nth 3)
                  "Tell players"
                  (reach.Address |> Option.defaultValue "this machine's own addresses")
                  $"{after reach} at:" ]
          Note =
            [ "Left and right change the one marked ->, and so does its own number. The two that"
              "want words write the line as far as they can and wait for the rest."
              ""
              "A word at the door is what keeps a stranger out of somebody's seat, and a table"
              "reachable from further than a room wants one. Over anything further than that,"
              "put it behind a tunnel or a proxy holding a certificate and say so here."
              ""
              $"Or type it: 'reaches {Seating.line sitters} via {Reach.line reach}'." ]
          Backs = Some(saying "seats" sitters reach) }

    /// The games there are to take up again, so that one can be picked rather than typed out.
    ///
    /// This screen is the whole of what was missing from taking a game up, and none of the
    /// machinery under it is new: every row hands back `replay <file>`, which is the line that
    /// has always worked and is the same one printed at the top of every record. So there is
    /// no second way of resuming a game here - there is the way there always was, and a list
    /// of what there is to say it about.
    ///
    /// The records come in from outside rather than being looked for here, because looking in
    /// a folder is reading the world and this file does not do that. Which of them are worth
    /// showing has been settled before they arrive, for the same reason.
    let continuing (game: Playable<_, _, _>) (records: Transcript.Saved list) : Keys.Screen =
        let row at (record: Transcript.Saved) =
            let seats = if record.Players = 1 then "1 seat" else $"{record.Players} seats"
            let moves = if record.Moves = 1 then "1 move" else $"{record.Moves} moves"

            // A record that does not name its game is one from before the name was part of a
            // record, and saying so is worth a column: it may be this game's and it may not,
            // and the only way to find out is to open it.
            let said =
                match record.Game with
                | Some _ -> ""
                | None -> "  (its name does not say which game)"

            Keys.sends
                (Keys.nth at)
                (record.Written.ToString "d MMM HH:mm")
                (sprintf "%-10s%-12s%s%s" seats moves record.Named said)
                $"replay {record.Named}"

        { Title = $"Take up a {game.Title} game"
          Prose =
            match records with
            | [] ->
                [ "There are no saved games here yet."
                  ""
                  "Every game writes itself down as it is played, so there will be one the moment you"
                  "put one down - 'quit' leaves the board exactly as it stands, and this is where it"
                  "comes back." ]
            | _ ->
                [ "Each of these is a game left where it stood. Taking one up puts the same players"
                  "back in the same seats - the machine at the seats it was playing, and at the"
                  "strength it was playing them - and it goes on being written to the same file." ]
          Rows = records |> List.mapi row
          Note =
            [ "The most recently put down is first. 'undo' walks a game you take up backwards"
              "through every state it really passed through, so this is how a game is reviewed as"
              "well as how it is carried on."
              ""
              "Or type it: 'replay <file>', naming any record - including one from somewhere else"
              $"than the {game.Title} games listed here." ]
          Backs = Some "back" }

    /// The menu is shown in the view it is offering, so a player choosing one can see what
    /// they are choosing before they commit a game to it.
    /// The front door.
    ///
    /// `behind` is whether there is anything to go back *to*. There was not, when this
    /// program had one game in it, and backing out of the front door meant asking again;
    /// with a list of games in front of it there is, and a door that could not be walked
    /// back out of would leave a player having to close the program to change their mind.
    let screen game (showing: View<_, _, _>) behind : Keys.Screen =
        // Bound out here rather than said inside the note below: an interpolation cannot
        // carry a quoted string of its own.
        let machines = game.Skills |> List.map fst |> String.concat ", "
        let fewest = Seating.line (Seating.here game.Fewest)

        // Left and right walk the ways of drawing a board, rather than opening a list of
        // two. What is on offer comes from the game, and where in it the reader already is
        // comes from the view doing the showing - so this needs to remember nothing.
        let looking step =
            let names =
                Playable.offered AtATerminal showing.Palette game
                |> List.map (fun view -> view.Name)

            let at =
                names
                |> List.tryFindIndex (fun name -> name = showing.Name)
                |> Option.defaultValue 0

            let count = List.length names

            $"view {names[((at + step) % count + count) % count]}"

        let drawn =
            Keys.sends (Keys.nth 3) "How it is drawn" $"now {showing.Name} - {showing.Describe}" (looking 1)
            |> Keys.turning looking

        { Title = game.Title
          Prose = [ game.Blurb ]
          Rows =
            [ Keys.opens (Keys.nth 0) "New game" "how many are playing, and who each of them is" (counting game)
              Keys.types (Keys.nth 1) "Join a table" "sit down at one somebody else is hosting" "join "
              // A row that opens a list rather than one that starts a line, which is the whole
              // of the difference this made: what it stands for is still `replay <file>`, but
              // a person who has never read a README now finds out there is such a thing.
              Keys.sends (Keys.nth 2) "Continue a game" "one you put down, taken up where it was left" "continue"
              drawn
              Keys.sends (Keys.nth 4) "Settings" "sound, how it is drawn, and what this game lets you settle" "settings"
              Keys.sends (Keys.nth 5) "Rules" "the rules and the commands, at length" "rules"
              Keys.sends (Keys.nth 6) "Quit" "" "quit" ]
          Note =
            [ "Move with the arrows or w and s. Enter takes the one marked ->, and so does its number."
              ""
              $"Or type it: a seat each, from {Seating.names game.Skills} - 'play {fewest}' to deal"
              $"one, 'serve {fewest}' to read it in a browser, 'seats {fewest}' to lay it out"
              $"first. The short ways still hold: '{game.Fewest}' for a game of {game.Fewest},"
              $"'{game.Fewest} 42' for that same game again, 'serve {game.Fewest}',"
              $"'host {game.Fewest}', 'vs <skill>...' for {machines},"
              $"'join <address> [word]', 'continue', 'replay <file>',"
              $"'view <{Playable.namesFor AtATerminal game}>',"
              (if behind then
                   "'settings', 'rules', 'back', 'quit'."
               else
                   "'settings', 'rules', 'quit'.") ]
          Backs = (if behind then Some "back" else None) }

    // --- a typed line ----------------------------------------------------------------------

    /// A typed line as a choice. A bare number is the answer to the question the menu
    /// asks, so it needs no command word in front of it.
    ///
    /// The palette comes in because a view is built in one: asking for another way of
    /// reading keeps whatever colours have been settled on rather than going back to the
    /// ones the game started in.
    let choose game (palette: Palette) (text: string) : Result<Choice<_, _, _>, string> =
        let skills = game.Skills
        let machines = skills |> List.map fst |> String.concat ", "
        let range = Playable.seats game

        let digits (word: string) =
            word <> "" && word |> Seq.forall System.Char.IsDigit

        /// A line with a table on one side of 'via' and how far it reaches on the other.
        ///
        /// Said nothing about, the reach is nobody's here: a word at a door has to be made up
        /// and nothing in this file makes anything up. So it comes back as nothing at all,
        /// and whoever is holding the screen answers for it - which is the same answer the
        /// command line gives to the same silence.
        let apart words =
            match words |> List.tryFindIndex (fun word -> word = "via") with
            | None -> Ok(words, None)
            | Some at ->
                Reach.read (List.skip (at + 1) words)
                |> Result.map (fun reach -> List.truncate at words, Some reach)

        /// A seating said either way: as a number, which is that many people here, or as a
        /// word to a seat. Both end at a whole seating, so the short way cannot come to mean
        /// something the long way round does not.
        let table words =
            match words with
            | [ only ] when digits only -> Commands.tryPlayerCount range only |> Result.map Seating.here
            | _ -> Seating.read skills range words

        /// The two halves of one of those lines, each read by the thing that knows how.
        let opening words =
            apart words
            |> Result.bind (fun (seated, reach) -> table seated |> Result.map (fun sitters -> sitters, reach))

        /// Where a seating is played, which is the seating's own answer and not a separate
        /// question: anybody joining makes it a table to open and wait at, and nobody joining
        /// makes it a game to deal here.
        let dealing seed (sitters, reach) =
            if Seating.hosted sitters then Host(sitters, seed, reach) else Deal(sitters, seed)

        /// A page is one hot seat, the same as this keyboard is, so there is nobody for a
        /// seat at it to be at the far end of.
        let served seed (sitters, reach) =
            if Seating.hosted sitters then
                Error "A game in a browser is one hot seat; there is nobody to join it. Open a table instead."
            else
                Ok(Serve(sitters, seed, reach))

        /// The machines named after `vs`. How many are playing is not asked for and is not
        /// something to get wrong: it is one seat for whoever is reading this and one for
        /// each machine named, which is what somebody saying 'vs medium' means.
        let facing names =
            names
            |> List.fold
                (fun found name ->
                    found
                    |> Result.bind (fun found ->
                        match Seating.byName skills name with
                        | Ok(Machine skill) -> Ok(found @ [ skill ])
                        | Ok _
                        | Error _ -> Error $"'{name}' is not a way for the machine to play. There is {Seating.names skills}."))
                (Ok [])
            |> Result.bind (fun found ->
                match List.length found with
                | 0 -> Error $"Say 'vs <skill>', for one or more of {machines}."
                | many when many + 1 > game.Most ->
                    Error $"That is a table of {many + 1}. The game takes {game.Fewest} to {game.Most}."
                | many -> Ok(Seating.after (many + 1) found))

        let counted players seed =
            result {
                let! players = Commands.tryPlayerCount range players
                let! seed = Commands.trySeed seed
                return players, seed
            }

        match Commands.words text with
        | [] -> Ok Waiting
        // The word is lowered to be read, but the rest is left as it was typed: a file
        // may be named in any case, and on some machines that is the difference between
        // finding it and not.
        | word :: rest ->
            match word.ToLowerInvariant(), rest with
            | ("quit" | "exit" | "q"), [] -> Ok Leave
            | ("back" | "menu"), [] -> Ok Backing
            | ("rules" | "help" | "?"), [] -> Ok Rules
            | "replay", [ path ] -> Ok(Replay path)
            | "replay", _ -> Error "Say 'replay <file>', naming one saved record."
            | ("continue" | "resume" | "saved"), [] -> Ok Continuing
            | ("continue" | "resume" | "saved"), _ ->
                Error "Say 'continue' on its own for the list, or 'replay <file>' to name one outright."
            | "seats", words -> opening words |> Result.map Sitting
            | "reaches", words ->
                opening words
                |> Result.bind (fun (sitters, reach) ->
                    match reach with
                    | Some reach -> Ok(Reaching(sitters, reach))
                    | None -> Error $"Say how far it reaches after 'via': {Reach.says}.")
            | "play", [ players; seed ] when digits players ->
                counted players seed
                |> Result.map (fun (players, seed) -> dealing (Some seed) (Seating.here players, None))
            | "play", words -> opening words |> Result.map (dealing None)
            | "host", [ players ] ->
                Commands.tryPlayerCount range players
                |> Result.map (fun n -> Host(Seating.hosting n, None, None))
            | "host", [ players; seed ] ->
                counted players seed
                |> Result.map (fun (players, seed) -> Host(Seating.hosting players, Some seed, None))
            | "host", _ when game.Fewest = game.Most -> Error $"Say 'host {game.Fewest}', for {game.Fewest} of you."
            | "host", _ -> Error $"Say 'host <players>', for {game.Fewest} to {game.Most} of you."
            // Before the seatings below, which would read 'vs' as a number of players and
            // say so rather than saying what is actually wrong.
            | "vs", names -> facing names |> Result.map (fun sitters -> Deal(sitters, None))
            | "serve", "vs" :: names -> facing names |> Result.bind (fun sitters -> served None (sitters, None))
            | "serve", [ players; seed ] when digits players ->
                counted players seed
                |> Result.bind (fun (players, seed) -> served (Some seed) (Seating.here players, None))
            | "serve", words -> opening words |> Result.bind (served None)
            | "view", [ name ] -> Playable.byName AtATerminal palette game name |> Result.map Looking
            | "view", _ -> Error $"Say 'view <name>', for one of {Playable.namesFor AtATerminal game}."
            // Still the colour words, and they have to be: 'colours' is what this has always
            // taken, it is in every note and every README, and a screen that grew a second
            // half is no reason for a line somebody already knows to stop working.
            | ("settings" | "colours" | "colors" | "options"), [] -> Ok Options
            | ("settings" | "colours" | "colors" | "options"), _ ->
                Error "Say 'settings' on its own; the screen it opens says the rest."
            | "join", [ address ] -> Ok(Join(address, None))
            | "join", [ address; code ] -> Ok(Join(address, Some code))
            | "join", _ ->
                Error "Say 'join <address>', naming the machine that is hosting, and the word at its door if it has one."
            | "players", [ players ] ->
                Commands.tryPlayerCount range players
                |> Result.map (fun n -> Deal(Seating.here n, None))
            | "players", [ players; seed ] ->
                counted players seed
                |> Result.map (fun (players, seed) -> Deal(Seating.here players, Some seed))
            | players, [] ->
                Commands.tryPlayerCount range players
                |> Result.map (fun n -> Deal(Seating.here n, None))
            | players, [ seed ] ->
                counted players seed
                |> Result.map (fun (players, seed) -> Deal(Seating.here players, Some seed))
            | word, _ -> Error $"I don't know how to '{word}'. Say how many are playing, or quit."
