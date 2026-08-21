namespace TCModel.Table

open System
open Argu
open TCModel.Common
open TCModel.Engine

[<RequireQualifiedAccess>]
type Start =
    | Dealt of players: int * seed: uint64 option * rivals: string list
    | Saved of path: string

[<RequireQualifiedAccess>]
type Launch =
    | Play of Start
    | Serve of Start * reach: Reach
    | Host of Start * reach: Reach
    | Join of address: string * token: string option * code: string option * table: string option
    | House of reach: Reach * filling: bool


type PlayArgs =
    | [<MainCommand>] Players of players: int
    | [<AltCommandLine("-s")>] Seed of seed: uint64
    | [<AltCommandLine("-r")>] Rival of skill: string
    | [<AltCommandLine("-f")>] From of path: string
    | View of name: string
    | [<AltCommandLine("--color")>] Colour of slot: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Players _ -> "how many are playing"
            | Seed _ -> "deal from this seed rather than from the clock, for the same game again"
            | Rival _ -> "let the machine play the next seat; may be given more than once - see the list below"
            | From _ -> "take up this saved game instead of dealing one, against the players it names"
            | View _ -> "how the board is drawn - see the list below"
            | Colour _ -> "what to draw something in, as 'blue=teal'; may be given more than once"

type ServeArgs =
    | [<MainCommand>] Players of players: int
    | [<AltCommandLine("-s")>] Seed of seed: uint64
    | [<AltCommandLine("-r")>] Rival of skill: string
    | [<AltCommandLine("-f")>] From of path: string
    | [<AltCommandLine("--color")>] Colour of slot: string
    | [<AltCommandLine("-p")>] Port of port: int
    | Code of code: string
    | Open
    | Cert of certificate: string
    | [<CustomCommandLine("--cert-password")>] CertPassword of password: string
    | Behind
    | At of address: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Players _ -> "how many are playing"
            | Seed _ -> "deal from this seed rather than from the clock, for the same game again"
            | Rival _ -> "let the machine play the next seat; may be given more than once - see the list below"
            | From _ -> "take up this saved game instead of dealing one, against the players it names"
            | Colour _ -> "what to draw something in, as 'blue=teal'; may be given more than once"
            | Port _ -> "listen on this port rather than the usual one"
            | Code _ -> "the word players say at the door, rather than one made up here"
            | Open -> "no word at the door: whoever can reach the address may sit down"
            | Cert _ -> "hold this certificate and speak https; a .pfx file"
            | CertPassword _ -> "the password that certificate is locked with"
            | Behind -> "https is ended by a tunnel or proxy in front of this, which forwards to it"
            | At _ -> "the address to tell players, when it is not this machine's own name"

type HostArgs =
    | [<MainCommand>] Players of players: int
    | [<AltCommandLine("-s")>] Seed of seed: uint64
    | [<AltCommandLine("-f")>] From of path: string
    | View of name: string
    | [<AltCommandLine("--color")>] Colour of slot: string
    | [<AltCommandLine("-p")>] Port of port: int
    | Code of code: string
    | Open
    | Cert of certificate: string
    | [<CustomCommandLine("--cert-password")>] CertPassword of password: string
    | Behind
    | At of address: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Players _ -> "how many are playing"
            | Seed _ -> "deal from this seed rather than from the clock, for the same game again"
            | From _ -> "take up this saved game instead of dealing one, against the players it names"
            | View _ -> "how your own board is drawn, if a seat here is yours - see the list below"
            | Colour _ -> "what to draw something in, as 'blue=teal'; may be given more than once"
            | Port _ -> "listen on this port rather than the usual one"
            | Code _ -> "the word players say at the door, rather than one made up here"
            | Open -> "no word at the door: whoever can reach the address may sit down"
            | Cert _ -> "hold this certificate and speak https; a .pfx file"
            | CertPassword _ -> "the password that certificate is locked with"
            | Behind -> "https is ended by a tunnel or proxy in front of this, which forwards to it"
            | At _ -> "the address to tell players, when it is not this machine's own name"

type HouseArgs =
    | [<AltCommandLine("-p")>] Port of port: int
    | Code of code: string
    | Open
    | Cert of certificate: string
    | [<CustomCommandLine("--cert-password")>] CertPassword of password: string
    | Behind
    | At of address: string
    | Fill

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Port _ -> "listen on this port rather than the usual one"
            | Code _ -> "the word players say at the door, rather than one made up here"
            | Open -> "no word at the door: whoever can reach the address may open a table"
            | Cert _ -> "hold this certificate and speak https; a .pfx file"
            | CertPassword _ -> "the password that certificate is locked with"
            | Behind -> "https is ended by a tunnel or proxy in front of this, which forwards to it"
            | At _ -> "the address to tell players, when it is not this machine's own name"
            | Fill -> "take up the games in logs/ on the way up, so a restart is a pause rather than a loss"

type JoinArgs =
    | [<MainCommand; ExactlyOnce>] Address of address: string
    | [<AltCommandLine("-t")>] Token of token: string
    | Code of code: string
    | [<CustomCommandLine("--table")>] AtTable of name: string
    | View of name: string
    | [<AltCommandLine("--color")>] Colour of slot: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Address _ -> "the machine hosting the table: a name, an address, or a whole URL"
            | Token _ -> "come back to the seat this token claimed, after dropping off"
            | Code _ -> "the word at that table's door, if it has one"
            | AtTable _ -> "which table, at a house that is holding several; one table needs no name"
            | View _ -> "how the board is drawn - see the list below"
            | Colour _ -> "what to draw something in, as 'blue=teal'; may be given more than once"

type ReplayArgs =
    | [<MainCommand; ExactlyOnce>] Path of path: string
    | View of name: string
    | [<AltCommandLine("--color")>] Colour of slot: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Path _ -> "the saved game to take up again"
            | View _ -> "how the board is drawn - see the list below"
            | Colour _ -> "what to draw something in, as 'blue=teal'; may be given more than once"

type Argument =
    | [<CliPrefix(CliPrefix.None); First>] Play of ParseResults<PlayArgs>
    | [<CliPrefix(CliPrefix.None); First>] Serve of ParseResults<ServeArgs>
    | [<CliPrefix(CliPrefix.None); First>] Host of ParseResults<HostArgs>
    | [<CliPrefix(CliPrefix.None); First>] House of ParseResults<HouseArgs>
    | [<CliPrefix(CliPrefix.None); First>] Join of ParseResults<JoinArgs>
    | [<CliPrefix(CliPrefix.None); First>] Replay of ParseResults<ReplayArgs>

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Play _ -> "deal a game for that many and play it at this keyboard"
            | Serve _ -> "deal a game for that many and play it in a browser"
            | Host _ -> "open a table for that many and wait for them to arrive"
            | House _ -> "open a house: several games at once, listed on a page, dealt as people ask for them"
            | Join _ -> "sit down at a table someone else is hosting"
            | Replay _ -> "take up a saved game where it was left, against the same players"

[<NoComparison; NoEquality>]
type Taken<'Move, 'State, 'Notice> =
    | Opening of Launch * View<'Move, 'State, 'Notice>
    | Printed of text: string
    | Wrong of problem: string

module Launch =


    let private examples (game: Playable<_, _, _>) =
        let shown line said = sprintf "    %-52s %s" line said
        let listed label said = sprintf "    %-28s %s" label said

        let name = Invoked.opening game.Name
        let few, many = game.Fewest, game.Most

        let drawn =
            Playable.offered AtATerminal (Playable.standard game) game
            |> List.map (fun view -> view.Name)

        let another = drawn |> List.tryLast |> Option.defaultValue "plain"

        String.concat
            Environment.NewLine
            ([ $"{game.Title} - {game.Blurb} With no arguments at all, the menu asks."
               ""
               "For example:"
               "" ]
             @ [ shown $"{name} play {many}" $"{many} of you, here at this keyboard" ]
             @ (match game.Skills with
                | [] -> []
                | skills ->
                    [ shown $"{name} play {few} --rival {fst (List.last skills)}" "the seat after yours, played by the machine" ])
             @ [ shown $"{name} play {few} --seed 42 --view {another}" "the same game again, drawn another way"
                 shown $"{name} serve {few}" "the same game, read in a browser"
                 shown $"{name} host {many}" $"a table for {many} at their own machines"
                 shown $"{name} host {many} --open" "...with no word at the door, for a room you trust"
                 $"    {name} host {many} --behind --at {game.Name}.example.org"
                 $"    {name} join greg-pc --code kbd4-9mtx-7rfp"
                 ""
                 shown $"{name} replay logs/<saved>.log" "a saved game, taken up where it was left"
                 shown $"{name} serve --from logs/<saved>.log" "...the same game, taken up in a browser"
                 ""
                 listed "How the board can be drawn:" (String.concat ", " drawn)
                 listed "  ...and in a browser:" (Playable.namesFor InABrowser game) ]
             @ (match game.Skills with
                | [] -> []
                | skills -> [ listed "The machine plays:" (skills |> List.map fst |> String.concat ", ") ])
             @ [ listed
                     "What takes a colour:"
                     (Palette.slots (Playable.standard game)
                      |> List.map (fun slot -> slot.Key)
                      |> String.concat ", ") ])

    let private readers =
        Collections.Concurrent.ConcurrentDictionary<string, ArgumentParser<Argument>>()

    let private parserFor (game: Playable<_, _, _>) =
        readers.GetOrAdd(
            game.Name,
            fun _ ->
                ArgumentParser.Create<Argument>(
                    programName = Invoked.opening game.Name,
                    errorHandler = ExceptionExiter(),
                    usageStringCharacterWidth = 100,
                    helpTextMessage = examples game
                )
        )

    let private parser =
        ArgumentParser.Create<Argument>(programName = "", errorHandler = ExceptionExiter())

    let private playing = ArgumentParser.Create<PlayArgs>(programName = "tcmodel play")

    let private serving =
        ArgumentParser.Create<ServeArgs>(programName = "tcmodel serve")

    let private hosting = ArgumentParser.Create<HostArgs>(programName = "tcmodel host")

    let private housing =
        ArgumentParser.Create<HouseArgs>(programName = "tcmodel house")

    let private joining = ArgumentParser.Create<JoinArgs>(programName = "tcmodel join")

    let private replaying =
        ArgumentParser.Create<ReplayArgs>(programName = "tcmodel replay")


    let private reaching port code opened cert password behind at (reach: Reach) =
        [ if reach.Port <> Reach.DefaultPort then yield port reach.Port

          match reach.Doorway with
          | Ajar -> yield opened
          | Locked word -> yield code word

          match reach.Wrapping with
          | InTheClear -> ()
          | Ahead -> yield behind
          | Kept(certificate, held) ->
              yield cert certificate

              match held with
              | Some held -> yield password held
              | None -> ()

          match reach.Address with
          | Some address -> yield at address
          | None -> () ]

    let private starting players seed rival from start =
        match start with
        | Start.Dealt(count, seeded, rivals) ->
            [ players count ]
            @ (seeded |> Option.toList |> List.map seed)
            @ (rivals
               |> List.collect (fun skill -> rival |> Option.toList |> List.map (fun name -> name skill)))
        | Start.Saved path -> [ from path ]

    // The other way round: a `Launch` back into the arguments that would have produced it, so the
    // program can print the exact line somebody types to join the table it has just opened, or to
    // come back to the seat they were sitting in. Written through the parser rather than by hand so
    // that the two cannot drift apart.
    let private arguments launch =
        match launch with
        | Launch.Play start ->
            [ Play(playing.ToParseResults(starting PlayArgs.Players PlayArgs.Seed (Some PlayArgs.Rival) PlayArgs.From start)) ]
        | Launch.Serve(start, reach) ->
            [ Serve(
                  serving.ToParseResults(
                      starting ServeArgs.Players ServeArgs.Seed (Some ServeArgs.Rival) ServeArgs.From start
                      @ reaching
                          ServeArgs.Port
                          ServeArgs.Code
                          ServeArgs.Open
                          ServeArgs.Cert
                          ServeArgs.CertPassword
                          ServeArgs.Behind
                          ServeArgs.At
                          reach
                  )
              ) ]
        | Launch.Host(start, reach) ->
            [ Host(
                  hosting.ToParseResults(
                      starting HostArgs.Players HostArgs.Seed None HostArgs.From start
                      @ reaching
                          HostArgs.Port
                          HostArgs.Code
                          HostArgs.Open
                          HostArgs.Cert
                          HostArgs.CertPassword
                          HostArgs.Behind
                          HostArgs.At
                          reach
                  )
              ) ]
        | Launch.House(reach, filling) ->
            [ House(
                  housing.ToParseResults(
                      reaching
                          HouseArgs.Port
                          HouseArgs.Code
                          HouseArgs.Open
                          HouseArgs.Cert
                          HouseArgs.CertPassword
                          HouseArgs.Behind
                          HouseArgs.At
                          reach
                      @ [ if filling then yield HouseArgs.Fill ]
                  )
              ) ]
        | Launch.Join(address, token, code, table) ->
            [ Join(
                  joining.ToParseResults(
                      [ JoinArgs.Address address ]
                      @ (token |> Option.toList |> List.map JoinArgs.Token)
                      @ (code |> Option.toList |> List.map JoinArgs.Code)
                      @ (table |> Option.toList |> List.map JoinArgs.AtTable)
                  )
              ) ]

    let words launch =
        parser.PrintCommandLineArguments(arguments launch) |> List.ofArray

    let private write launch =
        parser.PrintCommandLineArgumentsFlat(arguments launch)

    let writtenFor (name: string) launch =
        $"{Invoked.opening name} {write launch}"

    let written (game: Playable<_, _, _>) launch = writtenFor game.Name launch


    let private ourWord = lazy (Reach.minted ())

    let private skills game names =
        let known = game.Skills |> List.map fst
        let offered = known |> String.concat ", "

        names
        |> List.fold
            (fun found (name: string) ->
                found
                |> Result.bind (fun found ->
                    let wanted = name.ToLowerInvariant()

                    if List.contains wanted known then
                        Ok(found @ [ wanted ])
                    else
                        Error $"'{name}' is not a way for the machine to play. There is {offered}."))
            (Ok [])

    let private facing game players names =
        skills game names
        |> Result.bind (fun rivals ->
            if List.length rivals > players - 1 then
                let spare = Counting.several "seat" "seats" (players - 1)
                let given = List.length rivals

                Error
                    $"A game for {players} leaves {spare} for the machine, and it was given {given}. Deal for more, or ask for fewer."
            else
                Ok rivals)

    let private settled game =
        Playable.opening AtATerminal (fst (Settings.load ())) game

    let private painted game given =
        let kept = (fst (settled game)).Palette

        given
        |> List.fold
            (fun palette (given: string) ->
                palette
                |> Result.bind (fun palette ->
                    match given.Split '=' with
                    | [| slot; colour |] -> Palette.set (slot.ToLowerInvariant()) (colour.ToLowerInvariant()) palette
                    | _ -> Error $"'{given}' is not a colour for something. Say it as 'blue=teal'."))
            (Ok kept)

    let private reading game colours name =
        painted game colours
        |> Result.bind (fun palette ->
            match name with
            | Some name -> Playable.byName AtATerminal palette game name
            | None -> Ok(Playable.recoloured palette game (fst (settled game))))

    let private counted game players =
        Commands.tryPlayerCount (Playable.seats game) (string (players |> Option.defaultValue game.Fewest))

    let private opening game players seed rivals from =
        match from with
        | Some path ->
            match
                [ if Option.isSome players then "how many are playing"
                  if Option.isSome seed then "--seed"
                  if not (List.isEmpty rivals) then "--rival" ]
            with
            | [] -> Ok(Start.Saved path)
            | also ->
                let said = String.Join(" and ", also)
                Error $"--from takes up a saved game, which already says {said}. Say one or the other."
        | None ->
            result {
                let! count = counted game players
                let! skills = facing game count rivals
                return Start.Dealt(count, seed, skills)
            }

    let private reached port code opened cert password behind at =
        let doorway =
            match code, opened with
            | Some _, true -> Error "Say one of --code and --open. A door cannot be both."
            | Some(word: string), false when word.Trim() = "" -> Error "A word at the door has to be a word. Say --open for none."
            | Some word, false -> Ok(Locked word)
            | None, true -> Ok Ajar
            | None, false -> Ok(Locked ourWord.Value)

        let wrapping =
            match cert, behind with
            | Some _, true -> Error "Say one of --cert and --behind. https is ended in one place or the other."
            | Some certificate, false when not (IO.File.Exists certificate) ->
                Error $"There is no certificate at '{certificate}'."
            | Some certificate, false -> Ok(Kept(certificate, password))
            | None, true when Option.isSome password ->
                Error "A certificate password says nothing about a certificate held somewhere else."
            | None, true -> Ok Ahead
            | None, false when Option.isSome password -> Error "There is no certificate for that password to unlock."
            | None, false -> Ok InTheClear

        let address =
            match at with
            | None -> Ok None
            | Some given -> Reach.address given |> Result.map Some

        let port = port |> Option.defaultValue Reach.DefaultPort

        result {
            do! require (port >= 1 && port <= 65535) $"{port} is not a port. They run from 1 to 65535."
            let! doorway = doorway
            let! wrapping = wrapping
            let! address = address

            return
                { Port = port
                  Doorway = doorway
                  Wrapping = wrapping
                  Address = address }
        }

    let private opened game (taken: ParseResults<Argument>) =
        result {
            match taken.GetAllResults() |> List.tryHead with
            | Some(Play args) ->
                let! view = reading game (args.GetResults PlayArgs.Colour) (args.TryGetResult PlayArgs.View)

                let! start =
                    opening
                        game
                        (args.TryGetResult PlayArgs.Players)
                        (args.TryGetResult PlayArgs.Seed)
                        (args.GetResults PlayArgs.Rival)
                        (args.TryGetResult PlayArgs.From)

                return Launch.Play start, view
            | Some(Serve args) ->
                let! palette = painted game (args.GetResults ServeArgs.Colour)

                let! start =
                    opening
                        game
                        (args.TryGetResult ServeArgs.Players)
                        (args.TryGetResult ServeArgs.Seed)
                        (args.GetResults ServeArgs.Rival)
                        (args.TryGetResult ServeArgs.From)

                let! reach =
                    reached
                        (args.TryGetResult ServeArgs.Port)
                        (args.TryGetResult ServeArgs.Code)
                        (args.Contains ServeArgs.Open)
                        (args.TryGetResult ServeArgs.Cert)
                        (args.TryGetResult ServeArgs.CertPassword)
                        (args.Contains ServeArgs.Behind)
                        (args.TryGetResult ServeArgs.At)

                return Launch.Serve(start, reach), Playable.plainest InABrowser palette game
            | Some(Host args) ->
                let! view = reading game (args.GetResults HostArgs.Colour) (args.TryGetResult HostArgs.View)

                let! start =
                    opening
                        game
                        (args.TryGetResult HostArgs.Players)
                        (args.TryGetResult HostArgs.Seed)
                        []
                        (args.TryGetResult HostArgs.From)

                let! reach =
                    reached
                        (args.TryGetResult HostArgs.Port)
                        (args.TryGetResult HostArgs.Code)
                        (args.Contains HostArgs.Open)
                        (args.TryGetResult HostArgs.Cert)
                        (args.TryGetResult HostArgs.CertPassword)
                        (args.Contains HostArgs.Behind)
                        (args.TryGetResult HostArgs.At)

                return Launch.Host(start, reach), view
            | Some(House args) ->
                let! reach =
                    reached
                        (args.TryGetResult HouseArgs.Port)
                        (args.TryGetResult HouseArgs.Code)
                        (args.Contains HouseArgs.Open)
                        (args.TryGetResult HouseArgs.Cert)
                        (args.TryGetResult HouseArgs.CertPassword)
                        (args.Contains HouseArgs.Behind)
                        (args.TryGetResult HouseArgs.At)

                return
                    Launch.House(reach, args.Contains HouseArgs.Fill), Playable.plainest InABrowser (Playable.standard game) game
            | Some(Join args) ->
                let! view = reading game (args.GetResults JoinArgs.Colour) (args.TryGetResult JoinArgs.View)

                return
                    Launch.Join(
                        args.GetResult JoinArgs.Address,
                        args.TryGetResult JoinArgs.Token,
                        args.TryGetResult JoinArgs.Code,
                        args.TryGetResult JoinArgs.AtTable
                    ),
                    view
            | Some(Replay args) ->
                let! view = reading game (args.GetResults ReplayArgs.Colour) (args.TryGetResult ReplayArgs.View)
                return Launch.Play(Start.Saved(args.GetResult ReplayArgs.Path)), view
            | None -> return! Error "That does not say what to open. Say 'play', 'serve', 'host', 'house', 'join' or 'replay'."
        }

    // A line copied off the screen may still have `dotnet run --` on the front of it, which is how the
    // program says its own name from a source directory. Dropping those leaves the arguments.
    let taken game (given: string seq) =
        let words =
            given
            |> Seq.filter (fun word -> word <> "dotnet" && word <> "run" && word <> "--")
            |> Array.ofSeq

        try
            match opened game ((parserFor game).ParseCommandLine(words, raiseOnUsage = true)) with
            | Ok(launch, view) -> Opening(launch, view)
            | Error problem -> Wrong problem
        with
        | :? ArguParseException as problem when problem.ErrorCode = ErrorCode.HelpText -> Printed(problem.Message.Trim())
        | problem -> Wrong(problem.Message.Trim())

    let read game given =
        match taken game given with
        | Opening(launch, _) -> Ok launch
        | Printed text -> Error text
        | Wrong problem -> Error problem

    let run game (act: View<_, _, _> -> Launch -> int) (argv: string seq) =
        match taken game argv with
        | Opening(launch, view) -> act view launch
        | Printed text ->
            printfn "%s" text
            0
        | Wrong problem ->
            eprintfn "%s" problem
            1
