namespace TCModel.Console

open Argu

/// What a command line asks the program to open, as a value.
///
/// Nothing here opens anything. This is the typed answer to "what was asked for", and the
/// shell that reads a command line and the code that acts on one meet at it and nowhere
/// else - so what a game needs to start is written down once, and adding to it is a case
/// the compiler makes everybody answer.
[<RequireQualifiedAccess>]
type Launch =
    /// Deal and play at this keyboard. A seed left unsaid is taken from the clock.
    | Deal of players: int * seed: uint64 option
    /// Deal and wait for the other players to arrive from their own machines.
    | Host of players: int * seed: uint64 option
    /// Sit down at somebody else's table, resuming a seat if a token says which.
    | Join of address: string * token: string option
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
    | [<CliPrefix(CliPrefix.None); First>] Host of players: int
    | [<CliPrefix(CliPrefix.None); First>] Join of address: string
    | [<CliPrefix(CliPrefix.None); First>] Replay of path: string
    | [<AltCommandLine("-s")>] Seed of seed: uint64
    | [<AltCommandLine("-t")>] Token of token: string

    interface IArgParserTemplate with
        member this.Usage =
            match this with
            | Play _ -> "deal a game for that many and play it at this keyboard"
            | Host _ -> "open a table for that many and wait for them to arrive"
            | Join _ -> "sit down at a table someone else is hosting"
            | Replay _ -> "play a saved record again"
            | Seed _ -> "deal from this seed rather than from the clock"
            | Token _ -> "come back to the seat this token claimed"

module Launch =

    let private parser =
        ArgumentParser.Create<Argument>(programName = "tcmodel", errorHandler = ProcessExiter())

    let private arguments launch =
        match launch with
        | Launch.Deal(players, seed) -> [ Play players ] @ (seed |> Option.toList |> List.map Seed)
        | Launch.Host(players, seed) -> [ Argument.Host players ] @ (seed |> Option.toList |> List.map Seed)
        | Launch.Join(address, token) -> [ Argument.Join address ] @ (token |> Option.toList |> List.map Token)
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

            match taken.GetAllResults() |> List.tryHead with
            | Some(Play players) -> Ok(Launch.Deal(players, seed))
            | Some(Host players) -> Ok(Launch.Host(players, seed))
            | Some(Join address) -> Ok(Launch.Join(address, token))
            | Some(Replay path) -> Ok(Launch.Replay path)
            | Some(Seed _)
            | Some(Token _)
            | None ->
                let line = String.concat " " words
                Error $"'{line}' does not say what to open."
        with problem ->
            Error(problem.Message.Trim())
