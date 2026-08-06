namespace TCModel.Console

open Argu
open TCModel.App

/// What a command line asks the program to open, as a value.
///
/// Nothing here opens anything. This is the typed answer to "what was asked for", and the
/// shell that reads a command line and the code that acts on one meet at it and nowhere
/// else - so what a game needs to start is written down once, and adding to it is a case
/// the compiler makes everybody answer.
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

/// The same thing as a command line, in the words a person types.
///
/// This exists because the program has to *write* one. A console that drops off a
/// networked table is told how to get back to its seat, and that instruction is a command
/// line the program will later be asked to read. Written by hand it is a second spelling
/// of the command line, free to drift from the one the shell accepts; written from the
/// same declaration that parses it, it cannot.
///
/// It is the same bargain the record keeps - moves are written in the words the prompt
/// takes, so a record can always be replayed - applied one level out, to the way a game
/// is started rather than the way it is played.
type Argument =
    | [<CliPrefix(CliPrefix.None); First>] Play of players: int
    | [<CliPrefix(CliPrefix.None); First>] Serve of players: int
    | [<CliPrefix(CliPrefix.None); First>] Host of players: int
    | [<CliPrefix(CliPrefix.None); First>] Join of address: string
    | [<CliPrefix(CliPrefix.None); First>] Replay of path: string
    | [<AltCommandLine("-s")>] Seed of seed: uint64
    | [<AltCommandLine("-t")>] Token of token: string
    | [<AltCommandLine("-r")>] Rival of skill: string
    | [<AltCommandLine("-p")>] Port of port: int
    | Code of code: string
    | Open
    | Cert of certificate: string
    // Spelt out, because the two libraries would otherwise spell it differently - this one
    // runs the words together and the shell's own hyphenates them - and a line written here
    // that the front door will not take is exactly what the checks in `cli.fsx` are for.
    | [<CustomCommandLine("--cert-password")>] CertPassword of password: string
    | At of address: string
    | Behind

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Play _ -> "deal a game for that many and play it at this keyboard"
            | Serve _ -> "deal a game for that many and play it in a browser here"
            | Host _ -> "open a table for that many and wait for them to arrive"
            | Join _ -> "sit down at a table someone else is hosting"
            | Replay _ -> "play a saved record again"
            | Seed _ -> "deal from this seed rather than from the clock"
            | Token _ -> "come back to the seat this token claimed"
            | Rival _ -> $"let the machine play the next seat, at {Rival.names}"
            | Port _ -> $"listen on this port rather than {Reach.DefaultPort}"
            | Code _ -> "the word players say at the door, rather than one made up here"
            | Open -> "no word at the door: whoever can reach the address may sit down"
            | Cert _ -> "hold this certificate and speak https (a .pfx file)"
            | CertPassword _ -> "the password that certificate is locked with"
            | At _ -> "the address to tell players, when it is not this machine's own name"
            | Behind -> "https is ended by something in front of this, which forwards to it"

module Launch =

    let private parser =
        ArgumentParser.Create<Argument>(programName = "tcmodel", errorHandler = ProcessExiter())

    let private said seed (rivals: Skill list) =
        (seed |> Option.toList |> List.map Seed)
        @ (rivals |> List.map (fun skill -> Rival skill.Name))

    /// How far a table reaches, written out. Every part of it is said outright, including
    /// the parts that happen to be the usual ones - a line the program writes has to read
    /// back as the very table it was written from, and a door left unmentioned is a door
    /// the reader would have to guess at.
    let private reaching (reach: Reach) =
        [ if reach.Port <> Reach.DefaultPort then yield Port reach.Port

          match reach.Doorway with
          | Ajar -> yield Open
          | Locked code -> yield Code code

          match reach.Wrapping with
          | InTheClear -> ()
          | Ahead -> yield Behind
          | Kept(certificate, password) ->
              yield Cert certificate

              match password with
              | Some password -> yield CertPassword password
              | None -> ()

          match reach.Address with
          | Some address -> yield At address
          | None -> () ]

    let private arguments launch =
        match launch with
        | Launch.Deal(players, seed, rivals) -> [ Play players ] @ said seed rivals
        | Launch.Serve(players, seed, rivals, reach) -> [ Argument.Serve players ] @ said seed rivals @ reaching reach
        | Launch.Host(players, seed, reach) ->
            [ Argument.Host players ]
            @ (seed |> Option.toList |> List.map Seed)
            @ reaching reach
        | Launch.Join(address, token, code) ->
            [ Argument.Join address ]
            @ (token |> Option.toList |> List.map Token)
            @ (code |> Option.toList |> List.map Code)
        | Launch.Replay path -> [ Argument.Replay path ]

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

    /// Read the words back. Not the process's own arguments - `Shell` reads those, and
    /// answers a person who gets them wrong - but a line the program wrote earlier and is
    /// being handed again.
    ///
    /// Nothing in the program calls this yet; the checks do. It is the inverse of `write`,
    /// and a line written with no way of reading it back is a line nobody can prove the
    /// program would accept.
    let read (given: string seq) =
        // A line copied off the screen may still have the runner in front of it.
        let words =
            given
            |> Seq.filter (fun word -> word <> "dotnet" && word <> "run" && word <> "--")
            |> Array.ofSeq

        try
            let taken =
                ArgumentParser
                    .Create<Argument>(programName = "tcmodel", errorHandler = ExceptionExiter())
                    .ParseCommandLine(words, raiseOnUsage = true)

            let seed = taken.TryGetResult Seed
            let token = taken.TryGetResult Token
            let code = taken.TryGetResult Code

            // A door said neither way is a door left ajar. That is not the command line's
            // answer to the same silence - `Shell` makes a word up, because a table opened
            // for strangers ought to have one and nobody would think to ask - and it cannot
            // be, because this has to give the same answer twice and making one up does not.
            // Nothing the program writes leaves it unsaid, which is what keeps the two from
            // ever meeting.
            let reach =
                { Port = taken.TryGetResult Port |> Option.defaultValue Reach.DefaultPort
                  Doorway = code |> Option.map Locked |> Option.defaultValue Ajar
                  Wrapping =
                    match taken.TryGetResult Cert, taken.Contains Behind with
                    | Some certificate, _ -> Kept(certificate, taken.TryGetResult CertPassword)
                    | None, true -> Ahead
                    | None, false -> InTheClear
                  Address = taken.TryGetResult At }

            // Read back as skills rather than as the words they were written in, so a line
            // naming a way of playing that does not exist stops here rather than at a seat.
            let rivals =
                taken.GetResults Rival
                |> List.fold
                    (fun found name ->
                        found
                        |> Result.bind (fun found -> Rival.byName name |> Result.map (fun skill -> found @ [ skill ])))
                    (Ok [])

            match rivals with
            | Error problem -> Error problem
            | Ok rivals ->

            match taken.GetAllResults() |> List.tryHead with
            | Some(Play players) -> Ok(Launch.Deal(players, seed, rivals))
            | Some(Serve players) -> Ok(Launch.Serve(players, seed, rivals, reach))
            | Some(Host players) -> Ok(Launch.Host(players, seed, reach))
            | Some(Join address) -> Ok(Launch.Join(address, token, code))
            | Some(Replay path) -> Ok(Launch.Replay path)
            | Some(Seed _)
            | Some(Token _)
            | Some(Rival _)
            | Some(Port _)
            | Some(Code _)
            | Some Open
            | Some(Cert _)
            | Some(CertPassword _)
            | Some(At _)
            | Some Behind
            | None ->
                let line = String.concat " " words
                Error $"'{line}' does not say what to open."
        with problem ->
            Error(problem.Message.Trim())
