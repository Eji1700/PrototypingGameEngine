namespace TCModel.Console

open System
open TCModel.Common
open TCModel.Domain
open TCModel.App

/// The front door: what a person can ask for before there is a game to play.
///
/// Pure, like the rest of the console layer - it says what the menu reads like and what
/// a typed line means, and leaves the reading and the writing to `Program`.
module Menu =

    /// What the menu was asked for. Every one of these either starts a game or comes
    /// back round to a screen, so there is nothing else for the front door to do.
    [<NoComparison; NoEquality>]
    type Choice =
        /// Deal this seating and play it at this keyboard. A seed left unsaid is taken from
        /// the clock, so the game is a new one every time.
        | Deal of seating: Sitter list * seed: uint64 option
        /// The same game, read as a page in a browser on this machine.
        | Serve of seating: Sitter list * seed: uint64 option
        /// Open it as a table and wait for its seats to be taken. Which of the three this
        /// is, is the seating's own answer rather than a separate question - see `dealing`.
        | Host of seating: Sitter list * seed: uint64 option
        /// Not a game yet: show these seats, so that one of them can be changed. This is
        /// what every row on the seat list comes to, and what makes walking a seat along a
        /// way of typing rather than a thing the screen has to remember.
        | Sitting of seating: Sitter list
        /// Sit down at somebody else's table.
        | Join of address: string * token: string option
        | Replay of path: string
        /// Show the rules and the commands at length.
        | Rules
        /// Read the game a different way from here on.
        | Looking of View
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
    /// The numbers are taken from the table rather than written out, so the menu cannot come
    /// to offer one the table would refuse, and each is picked by its own digit: the key
    /// that says three *is* the three, rather than being the third thing on a list.
    let private counting: Keys.Screen =
        { Title = "A new game"
          Prose = [ "How many seats at the table?" ]
          Rows =
            [ for players in Table.MinPlayers .. Table.MaxPlayers ->
                  Keys.sends
                      (Some(char (int '0' + players)))
                      $"{players} players"
                      ""
                      $"seats {Seating.line (Seating.here players)}" ]
          Note =
            [ "They all start out as people at this keyboard. The next list is where a seat"
              "becomes the machine's, or somebody else's." ]
          Backs = None }

    /// The seats themselves: what each one is, and what changes it.
    ///
    /// A seat's row stands for the whole seating with that one seat walked along, so nothing
    /// is remembered between presses - the line says the whole of the change, and the screen
    /// that comes back is built from the answer rather than from a memory of it. The same
    /// bargain the colour screen keeps, and for the same reason.
    let seats sitters : Keys.Screen =
        let walking at step =
            let sitter = List.item at sitters
            $"seats {Seating.line (Seating.seated at (Seating.walked step sitter) sitters)}"

        let seat at sitter =
            Keys.sends
                (Keys.nth at)
                $"Seat {at + 1}"
                (sprintf "%-8s%s" (Seating.says sitter) (Seating.describe sitter))
                (walking at 1)
            |> Keys.turning (walking at)

        let hosted = Seating.hosted sitters
        let line = Seating.line sitters
        let taken = List.length sitters

        { Title = "Who is playing"
          Prose = [ "Each seat is somebody here, the machine, or somebody at their own machine." ]
          Rows =
            (sitters |> List.mapi seat)
            @ [ if hosted then
                    Keys.sends (Keys.nth taken) "Open the table" "and wait for the seats to be taken" $"play {line}"
                else
                    Keys.sends (Keys.nth taken) "Deal" "and play it here at this keyboard" $"play {line}" ]
            @ (if hosted then
                   []
               else
                   [ Keys.sends
                         (Keys.nth (taken + 1))
                         "In a browser"
                         "the same game, read as a page on this machine"
                         $"serve {line}" ])
          Note =
            [ $"Left and right walk the one marked -> through {Seating.names}."
              "Enter takes the next one along, and so does the seat's own number."
              ""
              "A table with anybody joining is opened over the network and waited at. One without"
              "is dealt and played here, and the machine answers between your turns."
              ""
              $"Or type it: 'seats {Seating.line (Seating.after 3 [ Rival.hard ])}' sets them all at once,"
              $"and 'play {Seating.line (Seating.after 3 [ Rival.hard ])}' deals that outright." ]
          Backs = Some "back" }

    /// The menu is shown in the view it is offering, so a player choosing one can see what
    /// they are choosing before they commit a game to it.
    let screen (showing: View) : Keys.Screen =
        // Left and right walk the ways of drawing a board, rather than opening a list of
        // two. What is on offer comes from `View`, and where in it the reader already is
        // comes from the view doing the showing - so this needs to remember nothing.
        let looking step =
            let names =
                View.offered AtATerminal showing.Palette |> List.map (fun view -> view.Name)

            let at =
                names
                |> List.tryFindIndex (fun name -> name = showing.Name)
                |> Option.defaultValue 0

            let count = List.length names

            $"view {names[((at + step) % count + count) % count]}"

        let drawn =
            Keys.sends (Keys.nth 3) "How it is drawn" $"now {showing.Name} - {showing.Describe}" (looking 1)
            |> Keys.turning looking

        { Title = "TCModel"
          Prose = [ "Stones on a map, and a seat each." ]
          Rows =
            [ Keys.opens (Keys.nth 0) "New game" "how many are playing, and who each of them is" counting
              Keys.types (Keys.nth 1) "Join a table" "sit down at one somebody else is hosting" "join "
              Keys.types (Keys.nth 2) "Replay a record" "a saved game, played through again" "replay "
              drawn
              Keys.sends (Keys.nth 4) "Colours" "which colour is drawn for what" "colours"
              Keys.sends (Keys.nth 5) "Rules" "the rules and the commands, at length" "rules"
              Keys.sends (Keys.nth 6) "Quit" "" "quit" ]
          Note =
            [ "Move with the arrows or w and s. Enter takes the one marked ->, and so does its number."
              ""
              $"Or type it: a seat each, from {Seating.names} - 'play you hard joins' to deal"
              "one, 'serve you medium' to read it in a browser, 'seats you you' to lay it out"
              "first. The short ways still hold: '3' for a game of three, '3 42' for that same"
              $"game again, 'serve 3', 'host 3', 'vs <skill>...' for {Rival.names},"
              $"'join <address>', 'replay <file>', 'view <{View.namesFor AtATerminal}>', 'colours',"
              "'rules', 'quit'." ]
          Backs = None }

    // --- a typed line ----------------------------------------------------------------------

    /// A typed line as a choice. A bare number is the answer to the question the menu
    /// asks, so it needs no command word in front of it.
    ///
    /// The palette comes in because a view is built in one: asking for another way of
    /// reading keeps whatever colours have been settled on rather than going back to the
    /// ones the game started in.
    let choose (palette: Palette) (text: string) : Result<Choice, string> =
        let digits (word: string) =
            word <> "" && word |> Seq.forall Char.IsDigit

        /// A seating said either way: as a number, which is that many people here, or as a
        /// word to a seat. Both end at a whole seating, so the short way cannot come to mean
        /// something the long way round does not.
        let table words =
            match words with
            | [ only ] when digits only -> Parse.tryPlayerCount only |> Result.map Seating.here
            | _ -> Seating.read words

        /// Where a seating is played, which is the seating's own answer and not a separate
        /// question: anybody joining makes it a table to open and wait at, and nobody joining
        /// makes it a game to deal here.
        let dealing seed sitters =
            if Seating.hosted sitters then Host(sitters, seed) else Deal(sitters, seed)

        /// A page on this machine is one hot seat, the same as this keyboard is, so there is
        /// nobody for a seat at it to be at the far end of.
        let served seed sitters =
            if Seating.hosted sitters then
                Error "A game in a browser here is one hot seat; there is nobody to join it. Open a table instead."
            else
                Ok(Serve(sitters, seed))

        /// The machines named after `vs`. How many are playing is not asked for and is not
        /// something to get wrong: it is one seat for whoever is reading this and one for
        /// each machine named, which is what somebody saying 'vs medium' means.
        let facing names =
            names
            |> List.fold
                (fun found name ->
                    found
                    |> Result.bind (fun found -> Rival.byName name |> Result.map (fun skill -> found @ [ skill ])))
                (Ok [])
            |> Result.bind (fun skills ->
                match List.length skills with
                | 0 -> Error $"Say 'vs <skill>', for one or more of {Rival.names}."
                | many when many + 1 > Table.MaxPlayers ->
                    Error $"That is a table of {many + 1}. The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
                | many -> Ok(Seating.after (many + 1) skills))

        let counted players seed =
            result {
                let! players = Parse.tryPlayerCount players
                let! seed = Parse.trySeed seed
                return players, seed
            }

        match Parse.words text with
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
            | "seats", words -> table words |> Result.map Sitting
            | "play", [ players; seed ] when digits players ->
                counted players seed
                |> Result.map (fun (players, seed) -> dealing (Some seed) (Seating.here players))
            | "play", words -> table words |> Result.map (dealing None)
            | "host", [ players ] ->
                Parse.tryPlayerCount players
                |> Result.map (fun n -> Host(Seating.hosting n, None))
            | "host", [ players; seed ] ->
                counted players seed
                |> Result.map (fun (players, seed) -> Host(Seating.hosting players, Some seed))
            | "host", _ -> Error $"Say 'host <players>', for {Table.MinPlayers} to {Table.MaxPlayers} of you."
            // Before the seatings below, which would read 'vs' as a number of players and
            // say so rather than saying what is actually wrong.
            | "vs", names -> facing names |> Result.map (fun sitters -> Deal(sitters, None))
            | "serve", "vs" :: names -> facing names |> Result.bind (served None)
            | "serve", [ players; seed ] when digits players ->
                counted players seed
                |> Result.bind (fun (players, seed) -> served (Some seed) (Seating.here players))
            | "serve", words -> table words |> Result.bind (served None)
            | "view", [ name ] -> View.byName AtATerminal palette name |> Result.map Looking
            | "view", _ -> Error $"Say 'view <name>', for one of {View.namesFor AtATerminal}."
            | ("colours" | "colors" | "options"), [] -> Ok Options
            | ("colours" | "colors" | "options"), _ -> Error "Say 'colours' on its own; the screen it opens says the rest."
            | "join", [ address ] -> Ok(Join(address, None))
            | "join", [ address; token ] -> Ok(Join(address, Some token))
            | "join", _ -> Error "Say 'join <address>', naming the machine that is hosting."
            | "players", [ players ] -> Parse.tryPlayerCount players |> Result.map (fun n -> Deal(Seating.here n, None))
            | "players", [ players; seed ] ->
                counted players seed
                |> Result.map (fun (players, seed) -> Deal(Seating.here players, Some seed))
            | players, [] -> Parse.tryPlayerCount players |> Result.map (fun n -> Deal(Seating.here n, None))
            | players, [ seed ] ->
                counted players seed
                |> Result.map (fun (players, seed) -> Deal(Seating.here players, Some seed))
            | word, _ -> Error $"I don't know how to '{word}'. Say how many are playing, or quit."
