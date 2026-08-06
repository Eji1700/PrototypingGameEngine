namespace TCModel.Console

open TCModel.Common
open TCModel.Domain
open TCModel.App

/// The front door: what a person can ask for before there is a game to play.
///
/// Pure, like the rest of the console layer - it says what the menu reads like and what
/// a typed line means, and leaves the reading and the writing to `Program`.
module Menu =

    /// What the menu was asked for. Every one of these either starts a game or comes
    /// back round to the menu, so there is nothing else for the front door to do.
    [<NoComparison; NoEquality>]
    type Choice =
        /// Deal a fresh game. A seed left unsaid is taken from the clock, so the game
        /// is a new one every time; the skills are the seats after the first that the
        /// machine is to play.
        | Deal of players: int * seed: uint64 option * rivals: Skill list
        /// Deal one and play it in a browser on this machine instead of at this keyboard.
        | Serve of players: int * seed: uint64 option * rivals: Skill list
        /// Deal one and wait for the other players to arrive from their own machines.
        | Host of players: int * seed: uint64 option
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
        /// Nothing was typed, so the menu simply asks again.
        | Waiting

    /// How many are playing, asked the same way wherever the answer is wanted - round this
    /// keyboard, in a browser, or at a table other machines will join.
    ///
    /// The numbers are taken from the table rather than written out, so the menu cannot come
    /// to offer one the table would refuse, and each is picked by its own digit: the key
    /// that says three *is* the three, rather than being the third thing on a list.
    let private seating title asked word : Keys.Screen =
        { Title = title
          Prose = [ asked ]
          Rows =
            [ for players in Table.MinPlayers .. Table.MaxPlayers ->
                  Keys.sends
                      (Some(char (int '0' + players)))
                      $"{players} players"
                      ""
                      (if word = "" then string players else $"{word} {players}") ]
          Note = [ $"Or type the number with a seed after it, for the same game again: '{Table.MinPlayers} 42'." ]
          Backs = None }

    /// The machines on offer, read off `Rival` rather than written out here, so a fourth
    /// way of playing is offered the moment there is one.
    let private facing: Keys.Screen =
        { Title = "Against the program"
          Prose = [ "One seat for you and one for the machine." ]
          Rows =
            Rival.all
            |> List.mapi (fun at skill -> Keys.sends (Keys.nth at) skill.Name skill.Describe $"vs {skill.Name}")
          Note = [ "For more than one, type 'vs easy hard' - a seat for you, and one for each machine named." ]
          Backs = None }

    /// The menu is shown in the view it is offering, so a player choosing one can see what
    /// they are choosing before they commit a game to it.
    ///
    /// Every row here stands for a line the menu itself can read, so nothing on the screen
    /// is a second way of meaning something: picking with the arrows and typing the words
    /// arrive at `choose` together.
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
            Keys.sends (Keys.nth 6) "How it is drawn" $"now {showing.Name} - {showing.Describe}" (looking 1)
            |> Keys.turning looking

        { Title = "TCModel"
          Prose = [ "Stones on a map, and a seat each." ]
          Rows =
            [ Keys.opens
                  (Keys.nth 0)
                  "Play here"
                  "everyone round this keyboard"
                  (seating "Playing here" "How many are playing?" "")
              Keys.opens (Keys.nth 1) "Against the program" $"the machine plays {Rival.names}" facing
              Keys.opens
                  (Keys.nth 2)
                  "In a browser"
                  "the same game, read as a page on this machine"
                  (seating "In a browser" "How many are playing?" "serve")
              Keys.opens
                  (Keys.nth 3)
                  "Host a table"
                  "the others sit down from their own machines"
                  (seating "Hosting a table" "How many seats to wait for?" "host")
              Keys.types (Keys.nth 4) "Join a table" "sit down at one somebody else is hosting" "join "
              Keys.types (Keys.nth 5) "Replay a record" "a saved game, played through again" "replay "
              drawn
              Keys.sends (Keys.nth 7) "Colours" "which colour is drawn for what" "colours"
              Keys.sends (Keys.nth 8) "Rules" "the rules and the commands, at length" "rules"
              Keys.sends (Keys.nth 9) "Quit" "" "quit" ]
          Note =
            [ "Move with the arrows or w and s. Enter takes the one marked ->, and so does its number."
              ""
              "Or type it: '3' for a game of three, '3 42' for that same game again, 'serve 3',"
              $"'vs <skill>...' for {Rival.names}, 'host 3', 'join <address>', 'replay <file>',"
              $"'view <{View.namesFor AtATerminal}>', 'colours', 'rules', 'quit'." ]
          Backs = None }

    /// A typed line as a choice. A bare number is the answer to the question the menu
    /// asks, so it needs no command word in front of it.
    ///
    /// The palette comes in because a view is built in one: asking for another way of
    /// reading keeps whatever colours have been settled on rather than going back to the
    /// ones the game started in.
    let choose (palette: Palette) (text: string) : Result<Choice, string> =
        let dealing players seed =
            result {
                let! players = Parse.tryPlayerCount players
                let! seed = Parse.trySeed seed
                return Deal(players, Some seed, [])
            }

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
                | many -> Ok(many + 1, skills))

        match Parse.words text with
        | [] -> Ok Waiting
        // The word is lowered to be read, but the rest is left as it was typed: a file
        // may be named in any case, and on some machines that is the difference between
        // finding it and not.
        | word :: rest ->
            match word.ToLowerInvariant(), rest with
            | ("quit" | "exit" | "q"), [] -> Ok Leave
            | ("rules" | "help" | "?"), [] -> Ok Rules
            | "replay", [ path ] -> Ok(Replay path)
            | "replay", _ -> Error "Say 'replay <file>', naming one saved record."
            | "host", [ players ] -> Parse.tryPlayerCount players |> Result.map (fun n -> Host(n, None))
            | "host", [ players; seed ] ->
                result {
                    let! players = Parse.tryPlayerCount players
                    let! seed = Parse.trySeed seed
                    return Host(players, Some seed)
                }
            | "host", _ -> Error $"Say 'host <players>', for {Table.MinPlayers} to {Table.MaxPlayers} of you."
            // Before the seatings below, which would read 'vs' as a number of players and
            // say so rather than saying what is actually wrong.
            | "vs", names ->
                facing names
                |> Result.map (fun (players, skills) -> Deal(players, None, skills))
            | "serve", "vs" :: names ->
                facing names
                |> Result.map (fun (players, skills) -> Serve(players, None, skills))
            | "serve", [ players ] -> Parse.tryPlayerCount players |> Result.map (fun n -> Serve(n, None, []))
            | "serve", [ players; seed ] ->
                result {
                    let! players = Parse.tryPlayerCount players
                    let! seed = Parse.trySeed seed
                    return Serve(players, Some seed, [])
                }
            | "serve", _ -> Error $"Say 'serve <players>', for {Table.MinPlayers} to {Table.MaxPlayers} of you."
            | "view", [ name ] -> View.byName AtATerminal palette name |> Result.map Looking
            | "view", _ -> Error $"Say 'view <name>', for one of {View.namesFor AtATerminal}."
            | ("colours" | "colors" | "options"), [] -> Ok Options
            | ("colours" | "colors" | "options"), _ -> Error "Say 'colours' on its own; the screen it opens says the rest."
            | "join", [ address ] -> Ok(Join(address, None))
            | "join", [ address; token ] -> Ok(Join(address, Some token))
            | "join", _ -> Error "Say 'join <address>', naming the machine that is hosting."
            | "players", [ players ] -> Parse.tryPlayerCount players |> Result.map (fun n -> Deal(n, None, []))
            | "players", [ players; seed ] -> dealing players seed
            | players, [] -> Parse.tryPlayerCount players |> Result.map (fun n -> Deal(n, None, []))
            | players, [ seed ] -> dealing players seed
            | word, _ -> Error $"I don't know how to '{word}'. Say how many are playing, or quit."
