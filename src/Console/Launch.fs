namespace TCModel.Console

open System
open Argu
open TCModel.Common
open TCModel.Domain
open TCModel.Engine

/// What a command line asks the program to open, as a value.
///
/// Nothing here opens anything. This is the typed answer to "what was asked for", and the
/// reader of a command line and the code that acts on one meet at it and nowhere else - so
/// what a game needs to start is written down once, and adding to it is a case the compiler
/// makes everybody answer.
[<RequireQualifiedAccess>]
type Launch =
    /// Deal and play at this keyboard. A seed left unsaid is taken from the clock, and the
    /// skills are the seats after the first, in order, that the machine is to play.
    | Deal of players: int * seed: uint64 option * rivals: Skill list
    /// The same game, played in a browser rather than at this keyboard. It opens a port, so
    /// it says how far it can be reached like anything else that does.
    | Serve of players: int * seed: uint64 option * rivals: Skill list * reach: Reach
    /// Deal and wait for the other players to arrive from their own machines.
    | Host of players: int * seed: uint64 option * reach: Reach
    /// Sit down at somebody else's table, resuming a seat if a token says which, and saying
    /// the word at the door if that table has one.
    | Join of address: string * token: string option * code: string option
    | Replay of path: string

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
    | View of name: string
    | [<AltCommandLine("--color")>] Colour of slot: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Players _ -> "how many are playing"
            | Seed _ -> "deal from this seed rather than from the clock, for the same game again"
            | Rival _ -> $"let the machine play the next seat, at {Rival.names}; may be given more than once"
            | View _ -> $"how the board is drawn: {View.namesFor AtATerminal}"
            | Colour _ -> "what to draw something in, as 'blue=teal'; may be given more than once"

type ServeArgs =
    | [<MainCommand>] Players of players: int
    | [<AltCommandLine("-s")>] Seed of seed: uint64
    | [<AltCommandLine("-r")>] Rival of skill: string
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
            | Rival _ -> $"let the machine play the next seat, at {Rival.names}; may be given more than once"
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
            | View _ -> $"how your own board is drawn, if a seat here is yours: {View.namesFor AtATerminal}"
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
            | View _ -> $"how the board is drawn: {View.namesFor AtATerminal}"
            | Colour _ -> "what to draw something in, as 'blue=teal'; may be given more than once"

type ReplayArgs =
    | [<MainCommand; ExactlyOnce>] Path of path: string
    | View of name: string
    | [<AltCommandLine("--color")>] Colour of slot: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Path _ -> "the saved record to play again"
            | View _ -> $"how the board is drawn: {View.namesFor AtATerminal}"
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
            | Replay _ -> "play a saved record again, and stop where it left off"

/// What a command line came to: a game to open and how to read it, something to print, or
/// something to say about what was typed.
///
/// Three cases because a command line ends three ways, and a program that ran them together
/// would either exit non-zero on `--help` or say nothing about a line it could not read.
[<NoComparison; NoEquality>]
type Taken =
    | Opening of Launch * View
    | Printed of text: string
    | Wrong of problem: string

module Launch =

    // The parsers. The nested ones are here to be *written* through as much as read: turning
    // a launch back into words means handing each command its own arguments, and the same
    // declaration that reads them is what puts them back in order.

    /// What somebody asking for help is shown above the list. Worked lines rather than a
    /// description, because the list below already says what each option is *for* and what
    /// it does not say is what a whole command looks like with three of them on it.
    let private examples =
        String.concat
            Environment.NewLine
            [ "Stones on a map, and a seat each. With no arguments at all, the menu asks."
              ""
              "For example:"
              ""
              "    tcmodel play 3                       three of you, here at this keyboard"
              "    tcmodel play 2 --rival hard          the seat after yours, played by the machine"
              "    tcmodel play 2 --seed 42 --view rich the same game again, drawn in panels"
              "    tcmodel serve 3                      the same game, read in a browser"
              "    tcmodel host 3                       a table for three at their own machines"
              "    tcmodel host 3 --open                ...with no word at the door, for a room you trust"
              "    tcmodel host 3 --behind --at stones.example.org"
              "    tcmodel join greg-pc --code kbd4-9mtx-7rfp"
              "    tcmodel replay logs/2026-08-02-215823-2p-seed42.log" ]

    let private parser =
        ArgumentParser.Create<Argument>(
            programName = "tcmodel",
            errorHandler = ExceptionExiter(),
            usageStringCharacterWidth = 100,
            helpTextMessage = examples
        )

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

    let private arguments launch =
        match launch with
        | Launch.Deal(players, seed, rivals) ->
            [ Play(
                  playing.ToParseResults(
                      [ PlayArgs.Players players ]
                      @ (seed |> Option.toList |> List.map PlayArgs.Seed)
                      @ (rivals |> List.map (fun skill -> PlayArgs.Rival skill.Name))
                  )
              ) ]
        | Launch.Serve(players, seed, rivals, reach) ->
            [ Serve(
                  serving.ToParseResults(
                      [ ServeArgs.Players players ]
                      @ (seed |> Option.toList |> List.map ServeArgs.Seed)
                      @ (rivals |> List.map (fun skill -> ServeArgs.Rival skill.Name))
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
        | Launch.Host(players, seed, reach) ->
            [ Host(
                  hosting.ToParseResults(
                      [ HostArgs.Players players ]
                      @ (seed |> Option.toList |> List.map HostArgs.Seed)
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
        | Launch.Replay path -> [ Replay(replaying.ToParseResults [ ReplayArgs.Path path ]) ]

    /// A launch as the words a shell would hand the program, one to an entry.
    ///
    /// Words rather than a line, because a line has to be taken apart again and taking one
    /// apart is where quoting lives: an address or a path with a space in it survives this
    /// and would not survive a round trip through a single string.
    let words launch =
        parser.PrintCommandLineArguments(arguments launch) |> List.ofArray

    /// The same thing written out for somebody to read - and to type, which is the point.
    /// A player whose console drops off a table is shown this and expected to run it.
    let write launch =
        parser.PrintCommandLineArgumentsFlat(arguments launch)

    // --- and words as a launch ----------------------------------------------------------

    /// The word this process puts on its door, made up once however often it is asked for.
    ///
    /// Once, and lazily, because a process opens one table: a line read twice has to come
    /// back saying the same thing both times, and a word made up on demand would not. It is
    /// not made up at all by the commands that open nothing.
    let private ourWord = lazy (Reach.minted ())

    let private skills names =
        names
        |> List.fold
            (fun found name ->
                found
                |> Result.bind (fun found -> Rival.byName name |> Result.map (fun skill -> found @ [ skill ])))
            (Ok [])

    /// The machines a table was told to seat, checked against the seats there are for them.
    /// The first is yours - that is what playing at this keyboard means - so a game for two
    /// has one seat to give away, and asking for more of them is asking for a bigger table.
    let private facing players names =
        skills names
        |> Result.bind (fun rivals ->
            if List.length rivals > players - 1 then
                Error
                    $"A game for {players} leaves {players - 1} seat(s) for the machine, and it was given {List.length rivals}. Deal for more, or ask for fewer."
            else
                Ok rivals)

    /// The colours a command was told to draw in, folded up one at a time.
    let private painted given =
        given
        |> List.fold
            (fun palette (given: string) ->
                palette
                |> Result.bind (fun palette ->
                    match given.Split '=' with
                    | [| slot; colour |] -> Palette.set (slot.ToLowerInvariant()) (colour.ToLowerInvariant()) palette
                    | _ -> Error $"'{given}' is not a colour for something. Say it as 'blue=teal'."))
            (Ok Palette.standard)

    /// The colours first, then the view built in them, because a view is built in a palette
    /// and cannot be handed another afterwards.
    let private reading colours name =
        painted colours
        |> Result.bind (fun palette ->
            match name with
            | Some name -> View.byName AtATerminal palette name
            | None -> Ok(View.plain palette))

    /// How many are playing, said where a person would be told about it rather than after
    /// the deal.
    let private counted players =
        Parse.tryPlayerCount (string (players |> Option.defaultValue Table.MinPlayers))

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

        if port < 1 || port > 65535 then
            Error $"{port} is not a port. They run from 1 to 65535."
        else
            doorway
            |> Result.bind (fun doorway ->
                wrapping
                |> Result.bind (fun wrapping ->
                    address
                    |> Result.map (fun address ->
                        { Port = port
                          Doorway = doorway
                          Wrapping = wrapping
                          Address = address })))

    /// What one command line came to. Everything a command can be wrong about is answered
    /// here, in the words the person typed, before anything is opened.
    let private opened (taken: ParseResults<Argument>) =
        result {
            match taken.GetAllResults() |> List.tryHead with
            | Some(Play args) ->
                let! view = reading (args.GetResults PlayArgs.Colour) (args.TryGetResult PlayArgs.View)
                let! players = counted (args.TryGetResult PlayArgs.Players)
                let! rivals = facing players (args.GetResults PlayArgs.Rival)
                return Launch.Deal(players, args.TryGetResult PlayArgs.Seed, rivals), view
            | Some(Serve args) ->
                // No `--view` here and no choice to make: there is one way of drawing a board
                // a browser can read.
                let! palette = painted (args.GetResults ServeArgs.Colour)
                let! players = counted (args.TryGetResult ServeArgs.Players)
                let! rivals = facing players (args.GetResults ServeArgs.Rival)

                let! reach =
                    reached
                        (args.TryGetResult ServeArgs.Port)
                        (args.TryGetResult ServeArgs.Code)
                        (args.Contains ServeArgs.Open)
                        (args.TryGetResult ServeArgs.Cert)
                        (args.TryGetResult ServeArgs.CertPassword)
                        (args.Contains ServeArgs.Behind)
                        (args.TryGetResult ServeArgs.At)

                return Launch.Serve(players, args.TryGetResult ServeArgs.Seed, rivals, reach), View.html palette
            | Some(Host args) ->
                let! view = reading (args.GetResults HostArgs.Colour) (args.TryGetResult HostArgs.View)
                let! players = counted (args.TryGetResult HostArgs.Players)

                let! reach =
                    reached
                        (args.TryGetResult HostArgs.Port)
                        (args.TryGetResult HostArgs.Code)
                        (args.Contains HostArgs.Open)
                        (args.TryGetResult HostArgs.Cert)
                        (args.TryGetResult HostArgs.CertPassword)
                        (args.Contains HostArgs.Behind)
                        (args.TryGetResult HostArgs.At)

                return Launch.Host(players, args.TryGetResult HostArgs.Seed, reach), view
            | Some(Join args) ->
                let! view = reading (args.GetResults JoinArgs.Colour) (args.TryGetResult JoinArgs.View)

                return
                    Launch.Join(
                        args.GetResult JoinArgs.Address,
                        args.TryGetResult JoinArgs.Token,
                        args.TryGetResult JoinArgs.Code
                    ),
                    view
            | Some(Replay args) ->
                let! view = reading (args.GetResults ReplayArgs.Colour) (args.TryGetResult ReplayArgs.View)
                return Launch.Replay(args.GetResult ReplayArgs.Path), view
            | None -> return! Error "That does not say what to open. Say 'play', 'serve', 'host', 'join' or 'replay'."
        }

    /// Read a command line: the process's own, or one the program wrote earlier and is being
    /// handed again.
    ///
    /// A line copied off the screen may still have the runner in front of it, which is worth
    /// stepping over rather than complaining about - the program printed it that way.
    let taken (given: string seq) =
        let words =
            given
            |> Seq.filter (fun word -> word <> "dotnet" && word <> "run" && word <> "--")
            |> Array.ofSeq

        try
            match opened (parser.ParseCommandLine(words, raiseOnUsage = true)) with
            | Ok(launch, view) -> Opening(launch, view)
            | Error problem -> Wrong problem
        with
        | :? ArguParseException as problem when problem.ErrorCode = ErrorCode.HelpText -> Printed(problem.Message.Trim())
        | problem -> Wrong(problem.Message.Trim())

    /// The same, as the inverse of `words` and nothing else: what game a line asks for,
    /// leaving aside how it is to be read.
    let read given =
        match taken given with
        | Opening(launch, _) -> Ok launch
        | Printed text -> Error text
        | Wrong problem -> Error problem

    /// Read the process's own arguments and act on them.
    ///
    /// What to do with a launch comes in from outside, because acting on one means hosting a
    /// table or joining somebody else's, and this file is compiled before the part of the
    /// program that knows how.
    let run (act: View -> Launch -> int) (argv: string seq) =
        match taken argv with
        | Opening(launch, view) -> act view launch
        | Printed text ->
            printfn "%s" text
            0
        | Wrong problem ->
            eprintfn "%s" problem
            1
