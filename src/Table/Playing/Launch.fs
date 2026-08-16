namespace TCModel.Table

open System
open Argu
open TCModel.Common
open TCModel.Engine

/// Where a game comes from: dealt fresh for that many, or a saved one taken up again.
///
/// The two answer the same three questions - how many are playing, what they were dealt
/// from, and who is in each seat - and a record answers all three at once, which is what
/// makes taking one up the other way of saying the same thing rather than a special case
/// beside it. So it is one type here, asked in the same words by every way of opening a
/// game, and none of them has to know which of the two answers it got.
[<RequireQualifiedAccess>]
type Start =
    /// A seed left unsaid is taken from the clock, and the skills are the seats after the
    /// first, in order, that the machine is to play.
    | Dealt of players: int * seed: uint64 option * rivals: string list
    /// A record, which says all three itself - and which the game goes on being written to,
    /// so one game stays one record however many sittings it took.
    | Saved of path: string

/// What a command line asks the program to open, as a value.
///
/// Nothing here opens anything. This is the typed answer to "what was asked for", and the
/// reader of a command line and the code that acts on one meet at it and nowhere else - so
/// what a game needs to start is written down once, and adding to it is a case the compiler
/// makes everybody answer.
[<RequireQualifiedAccess>]
type Launch =
    /// Play at this keyboard.
    | Play of Start
    /// The same game, played in a browser rather than at this keyboard. It opens a port, so
    /// it says how far it can be reached like anything else that does.
    | Serve of Start * reach: Reach
    /// Open it and wait for the other players to arrive from their own machines.
    | Host of Start * reach: Reach
    /// Sit down at somebody else's table, resuming a seat if a token says which, and saying
    /// the word at the door if that table has one.
    | Join of address: string * token: string option * code: string option

// --- what can be asked for -----------------------------------------------------------------
//
// One declaration per command, and it is the whole of that command: what the help prints,
// what the reader accepts, and what the program writes when it has to write a line for
// somebody to type. There is no second spelling of any of it to drift from the first, which
// there was - `--cert-password` was `--certpassword` on the writing side for a while, and
// nothing but a check noticed.
//
// The colours and the view are declared once per command rather than shared, because a
// command is a type here and types do not mix in. That is a few lines repeated; what is not
// repeated is what any of them *mean*, which is read in one place further down.

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
            // "See the list below" like the other four, and for a reason worth writing down:
            // `Usage` is a *static* member, so it cannot be handed the game being opened and
            // cannot name that game's views. Naming them was left here by accident and came
            // out as a sentence ending in a colon and nothing at all. What there is to choose
            // from is in the epilogue, which is written by a function that has the game.
            | View _ -> "how your own board is drawn, if a seat here is yours - see the list below"
            | Colour _ -> "what to draw something in, as 'blue=teal'; may be given more than once"
            | Port _ -> "listen on this port rather than the usual one"
            | Code _ -> "the word players say at the door, rather than one made up here"
            | Open -> "no word at the door: whoever can reach the address may sit down"
            | Cert _ -> "hold this certificate and speak https; a .pfx file"
            | CertPassword _ -> "the password that certificate is locked with"
            | Behind -> "https is ended by a tunnel or proxy in front of this, which forwards to it"
            | At _ -> "the address to tell players, when it is not this machine's own name"

type JoinArgs =
    | [<MainCommand; ExactlyOnce>] Address of address: string
    | [<AltCommandLine("-t")>] Token of token: string
    | Code of code: string
    | View of name: string
    | [<AltCommandLine("--color")>] Colour of slot: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Address _ -> "the machine hosting the table: a name, an address, or a whole URL"
            | Token _ -> "come back to the seat this token claimed, after dropping off"
            | Code _ -> "the word at that table's door, if it has one"
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

/// The five ways in, each holding its own.
///
/// This is read *and* written from the one declaration, which is the whole point of it: a
/// console that drops off a networked table is told how to get back to its seat, and that
/// instruction is a command line the program will later be handed. Written by hand it would
/// be a second spelling of the command line, free to drift from the one the door accepts;
/// written from the same declaration that parses it, it cannot be.
///
/// It is the same bargain the record keeps - moves are written in the words the prompt
/// takes, so a record can always be replayed - applied one level out, to the way a game is
/// started rather than the way it is played.
type Argument =
    | [<CliPrefix(CliPrefix.None); First>] Play of ParseResults<PlayArgs>
    | [<CliPrefix(CliPrefix.None); First>] Serve of ParseResults<ServeArgs>
    | [<CliPrefix(CliPrefix.None); First>] Host of ParseResults<HostArgs>
    | [<CliPrefix(CliPrefix.None); First>] Join of ParseResults<JoinArgs>
    | [<CliPrefix(CliPrefix.None); First>] Replay of ParseResults<ReplayArgs>

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Play _ -> "deal a game for that many and play it at this keyboard"
            | Serve _ -> "deal a game for that many and play it in a browser"
            | Host _ -> "open a table for that many and wait for them to arrive"
            | Join _ -> "sit down at a table someone else is hosting"
            | Replay _ -> "take up a saved game where it was left, against the same players"

/// What a command line came to: a game to open and how to read it, something to print, or
/// something to say about what was typed.
///
/// Three cases because a command line ends three ways, and a program that ran them together
/// would either exit non-zero on `--help` or say nothing about a line it could not read.
[<NoComparison; NoEquality>]
type Taken<'Move, 'State, 'Notice> =
    | Opening of Launch * View<'Move, 'State, 'Notice>
    | Printed of text: string
    | Wrong of problem: string

module Launch =

    // The parsers. The nested ones are here to be *written* through as much as read: turning
    // a launch back into words means handing each command its own arguments, and the same
    // declaration that reads them is what puts them back in order.

    /// What somebody asking for help is shown above the list, and the lists the options
    /// themselves point at.
    ///
    /// Worked lines rather than a description, because the list below already says what each
    /// option is *for*, and what it does not say is what a whole command looks like with
    /// three of them on it. Written from the game rather than out by hand, and that is not
    /// tidiness: `IArgParserTemplate.Usage` is a *static* member, so not one of the options
    /// above can name this game's views or its machines. They say "see the list below", and
    /// this is the list below - the only place in the help that has the game to ask.
    let private examples (game: Playable<_, _, _>) =
        // What a whole line looks like, and what a list of names looks like under them. Two
        // widths because they are two different things: one column holds command lines and
        // the other holds labels, and padding a label out to fit a command line leaves a
        // corridor of nothing between it and what it says.
        let shown line said = sprintf "    %-52s %s" line said
        let listed label said = sprintf "    %-28s %s" label said

        // Whatever this program is called from where the reader is standing, and then the
        // game, because everything after that is read by the game.
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

    /// One parser per game, because the help it prints is that game's - its name at the head
    /// of every usage line, its blurb, its views, its machines. Kept rather than built afresh
    /// on every read: `Create` goes over the argument types by reflection, and the command
    /// line is read hundreds of times by [cli.fsx](tests/cli.fsx) alone.
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

    /// And one for writing a line back out, which never prints a program name and so needs
    /// no game to be about.
    let private parser =
        ArgumentParser.Create<Argument>(programName = "", errorHandler = ExceptionExiter())

    let private playing = ArgumentParser.Create<PlayArgs>(programName = "tcmodel play")

    let private serving =
        ArgumentParser.Create<ServeArgs>(programName = "tcmodel serve")

    let private hosting = ArgumentParser.Create<HostArgs>(programName = "tcmodel host")
    let private joining = ArgumentParser.Create<JoinArgs>(programName = "tcmodel join")

    let private replaying =
        ArgumentParser.Create<ReplayArgs>(programName = "tcmodel replay")

    // --- a launch as words ------------------------------------------------------------

    /// How far a table reaches, written out. Every part of it is said outright, including
    /// the parts that happen to be the usual ones - a line the program writes has to read
    /// back as the very table it was written from, and a door left unmentioned is a door
    /// the reader would have to make its own mind up about.
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

    /// Where the game came from, written out. `host` has no machines to name, so it hands in
    /// no way of naming one and a seating it could not have asked for cannot be written.
    let private starting players seed rival from start =
        match start with
        | Start.Dealt(count, seeded, rivals) ->
            [ players count ]
            @ (seeded |> Option.toList |> List.map seed)
            @ (rivals |> List.collect (fun skill -> rival |> Option.toList |> List.map (fun name -> name skill)))
        | Start.Saved path -> [ from path ]

    let private arguments launch =
        match launch with
        | Launch.Play start ->
            [ Play(
                  playing.ToParseResults(
                      starting PlayArgs.Players PlayArgs.Seed (Some PlayArgs.Rival) PlayArgs.From start
                  )
              ) ]
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
        | Launch.Join(address, token, code) ->
            [ Join(
                  joining.ToParseResults(
                      [ JoinArgs.Address address ]
                      @ (token |> Option.toList |> List.map JoinArgs.Token)
                      @ (code |> Option.toList |> List.map JoinArgs.Code)
                  )
              ) ]

    /// A launch as the words a shell would hand the program, one to an entry.
    ///
    /// Words rather than a line, because a line has to be taken apart again and taking one
    /// apart is where quoting lives: an address or a path with a space in it survives this
    /// and would not survive a round trip through a single string.
    let words launch =
        parser.PrintCommandLineArguments(arguments launch) |> List.ofArray

    /// The launch on its own, written out flat. Private, because the only thing that reads
    /// one is `written` below and a line short of its game is not a line to hand anybody.
    let private write launch =
        parser.PrintCommandLineArgumentsFlat(arguments launch)

    /// And the whole line, game and all.
    ///
    /// `write` is the launch on its own, because that is the half a round trip is about:
    /// which game is being opened is settled before a word of it is read. But a line a person
    /// is told to type has to open the *right* game, and it is exactly the lines this program
    /// prints for people to copy - the seat you were given, the address to read out to the
    /// room - that would otherwise be one word short.
    ///
    /// One word short and nearly working, which is the awkward part: a console that joined
    /// the wrong game would still be drawn the right board, because a board is drawn at the
    /// table. What it would get wrong is everything settled at this end - the colours it asks
    /// for, and whether `--colour x=teal` is even a thing that parses.
    /// Said out in full, because `Name` is a field on a good many things that are in scope
    /// here - Argu's own description of an argument among them - and inference would
    /// cheerfully pick one of those.
    let written (game: Playable<_, _, _>) launch =
        $"{Invoked.opening game.Name} {write launch}"

    // --- and words as a launch ----------------------------------------------------------

    /// The word this process puts on its door, made up once however often it is asked for.
    ///
    /// Once, and lazily, because a process opens one table: a line read twice has to come
    /// back saying the same thing both times, and a word made up on demand would not. It is
    /// not made up at all by the commands that open nothing.
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

    /// The machines a table was told to seat, checked against the seats there are for them.
    /// The first is yours - that is what playing at this keyboard means - so a game for two
    /// has one seat to give away, and asking for more of them is asking for a bigger table.
    let private facing game players names =
        skills game names
        |> Result.bind (fun rivals ->
            if List.length rivals > players - 1 then
                Error
                    $"A game for {players} leaves {players - 1} seat(s) for the machine, and it was given {List.length rivals}. Deal for more, or ask for fewer."
            else
                Ok rivals)

    /// Where a command line starts from before a word of it is read: the way this game was
    /// last left, which for somebody who has never settled on anything is exactly what it
    /// always was.
    ///
    /// Read here as well as at the menu, and through the same function, because they are the
    /// same question. A default that only people who pick from a list got, and people who type
    /// did not, would be two defaults - and the one thing a person is sure of about a setting
    /// is that it applies to them next time, whichever door they came in by.
    let private settled game =
        Playable.opening AtATerminal (fst (Settings.load ())) game

    /// The colours a command was told to draw in, folded up one at a time over the ones that
    /// were settled on. Said on the line and settled on beforehand is not two answers at odds:
    /// what is typed now is the later word about that slot, and every slot it says nothing
    /// about stays as it was left.
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

    /// The colours first, then the view built in them, because a view is built in a palette
    /// and cannot be handed another afterwards. A line that says nothing about how to draw is
    /// drawn the way it was last left, which is the whole of what a kept view is for.
    let private reading game colours name =
        painted game colours
        |> Result.bind (fun palette ->
            match name with
            | Some name -> Playable.byName AtATerminal palette game name
            | None -> Ok(Playable.recoloured palette game (fst (settled game))))

    /// How many are playing, said where a person would be told about it rather than after
    /// the deal.
    let private counted game players =
        Commands.tryPlayerCount (Playable.seats game) (string (players |> Option.defaultValue game.Fewest))

    /// Where the game is coming from, which every way of opening one asks in the same words.
    ///
    /// A record already says how many are playing, what they were dealt from and who was in
    /// each seat, so saying any of those alongside `--from` is not a shorter way of saying
    /// the same thing - it is two different games asked for at once, and there is no way to
    /// tell which was meant. Refused in the words the person typed, like everything else here.
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

    /// Everything that can be said two ways at once is refused here rather than settled
    /// quietly, because each of these pairs is somebody meaning one of two quite different
    /// things and there is no way to tell which.
    let private reached port code opened cert password behind at =
        let doorway =
            match code, opened with
            | Some _, true -> Error "Say one of --code and --open. A door cannot be both."
            | Some(word: string), false when word.Trim() = "" -> Error "A word at the door has to be a word. Say --open for none."
            | Some word, false -> Ok(Locked word)
            | None, true -> Ok Ajar
            // A table nobody said anything about is a table with a word on it. It is opened
            // on every address this machine answers to, and out past a network the first
            // stranger through the door takes somebody's seat.
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

    /// What one command line came to. Everything a command can be wrong about is answered
    /// here, in the words the person typed, before anything is opened.
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
                // No `--view` here and no choice to make: there is one way of drawing a board
                // a browser can read.
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
            | Some(Join args) ->
                let! view = reading game (args.GetResults JoinArgs.Colour) (args.TryGetResult JoinArgs.View)

                return
                    Launch.Join(
                        args.GetResult JoinArgs.Address,
                        args.TryGetResult JoinArgs.Token,
                        args.TryGetResult JoinArgs.Code
                    ),
                    view
            // The short way of saying `play --from <file>`, which is how people already ask
            // for it and is what every record's own header tells them to type. It reads to
            // the very same launch, so the two cannot come to mean different things - the
            // same bargain the seating shorthands keep.
            | Some(Replay args) ->
                let! view = reading game (args.GetResults ReplayArgs.Colour) (args.TryGetResult ReplayArgs.View)
                return Launch.Play(Start.Saved(args.GetResult ReplayArgs.Path)), view
            | None -> return! Error "That does not say what to open. Say 'play', 'serve', 'host', 'join' or 'replay'."
        }

    /// Read a command line: the process's own, or one the program wrote earlier and is being
    /// handed again.
    ///
    /// A line copied off the screen may still have the runner in front of it, which is worth
    /// stepping over rather than complaining about - the program printed it that way.
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

    /// The same, as the inverse of `words` and nothing else: what game a line asks for,
    /// leaving aside how it is to be read.
    let read game given =
        match taken game given with
        | Opening(launch, _) -> Ok launch
        | Printed text -> Error text
        | Wrong problem -> Error problem

    /// Read the process's own arguments and act on them.
    ///
    /// What to do with a launch comes in from outside, because acting on one means hosting a
    /// table or joining somebody else's, and this file is compiled before the part of the
    /// program that knows how.
    let run game (act: View<_, _, _> -> Launch -> int) (argv: string seq) =
        match taken game argv with
        | Opening(launch, view) -> act view launch
        | Printed text ->
            printfn "%s" text
            0
        | Wrong problem ->
            eprintfn "%s" problem
            1
