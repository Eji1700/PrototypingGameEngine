namespace TCModel.Table

open TCModel.Common

module Menu =

    [<NoComparison; NoEquality>]
    type Choice<'Move, 'State, 'Notice> =
        | Deal of seating: Sitter list * seed: uint64 option
        | Serve of seating: Sitter list * seed: uint64 option * reach: Reach option
        | Host of seating: Sitter list * seed: uint64 option * reach: Reach option
        | Sitting of seating: Sitter list * reach: Reach option
        | Reaching of seating: Sitter list * reach: Reach
        | Join of address: string * code: string option
        | Replay of path: string
        | Continuing
        | Rules
        | Looking of View<'Move, 'State, 'Notice>
        | Options
        | Leave
        | Waiting
        | Backing


    let private counting game : Keys.Screen =
        { Title = "A new game"
          Prose = [ "How many seats at the table?" ]
          Rows =
            [ for players in game.Fewest .. game.Most ->
                  Keys.sends
                      (Some(char (int '0' + players)))
                      (if players = 1 then "1 player" else $"{players} players")
                      ""
                      $"seats {Seating.line (Seating.here players)}" ]
          Note =
            [ "They all start out as people at this keyboard. The next list is where a seat"
              "becomes the machine's, or somebody else's." ]
          Backs = None }

    let private saying said sitters reach =
        $"{said} {Seating.line sitters} via {Reach.line reach}"

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

    let continuing (game: Playable<_, _, _>) (records: Transcript.Saved list) : Keys.Screen =
        let row at (record: Transcript.Saved) =
            let seats = if record.Players = 1 then "1 seat" else $"{record.Players} seats"
            let moves = if record.Moves = 1 then "1 move" else $"{record.Moves} moves"

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

    let screen game (showing: View<_, _, _>) behind : Keys.Screen =
        let machines = game.Skills |> List.map fst |> String.concat ", "
        let fewest = Seating.line (Seating.here game.Fewest)

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
              (match game.Skills with
               | [] -> $"'host {game.Fewest}',"
               | _ -> $"'host {game.Fewest}', 'vs <skill>...' for {machines},")
              $"'join <address> [word]', 'continue', 'replay <file>',"
              $"'view <{Playable.namesFor AtATerminal game}>',"
              (if behind then "'settings', 'rules', 'back', 'quit'." else "'settings', 'rules', 'quit'.") ]
          Backs = (if behind then Some "back" else None) }


    let choose game (palette: Palette) (text: string) : Result<Choice<_, _, _>, string> =
        let skills = game.Skills
        let machines = skills |> List.map fst |> String.concat ", "
        let range = Playable.seats game

        let digits (word: string) =
            word <> "" && word |> Seq.forall System.Char.IsDigit

        // 'via' separates who is playing from how far the table reaches, so one typed line can say
        // both: "play you joins via port:5001 word:abc".
        let apart words =
            match words |> List.tryFindIndex (fun word -> word = "via") with
            | None -> Ok(words, None)
            | Some at ->
                Reach.read (List.skip (at + 1) words)
                |> Result.map (fun reach -> List.truncate at words, Some reach)

        let table words =
            match words with
            | [ only ] when digits only -> Commands.tryPlayerCount range only |> Result.map Seating.here
            | _ -> Seating.read skills range words

        let opening words =
            apart words
            |> Result.bind (fun (seated, reach) -> table seated |> Result.map (fun sitters -> sitters, reach))

        let dealing seed (sitters, reach) =
            if Seating.hosted sitters then Host(sitters, seed, reach) else Deal(sitters, seed)

        let served seed (sitters, reach) =
            if Seating.hosted sitters then
                Error "A game in a browser is one hot seat; there is nobody to join it. Open a table instead."
            else
                Ok(Serve(sitters, seed, reach))

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
            | "vs", names -> facing names |> Result.map (fun sitters -> Deal(sitters, None))
            | "serve", "vs" :: names -> facing names |> Result.bind (fun sitters -> served None (sitters, None))
            | "serve", [ players; seed ] when digits players ->
                counted players seed
                |> Result.bind (fun (players, seed) -> served (Some seed) (Seating.here players, None))
            | "serve", words -> opening words |> Result.bind (served None)
            | "view", [ name ] -> Playable.byName AtATerminal palette game name |> Result.map Looking
            | "view", _ -> Error $"Say 'view <name>', for one of {Playable.namesFor AtATerminal game}."
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
