namespace TCModel.Table

open System
open System.IO
open TCModel.Engine

module Transcript =

    [<Literal>]
    let private DealWord = "deal"

    [<NoComparison; NoEquality>]
    type Reading<'Move> =
        { Players: int
          Seed: uint64
          Sitters: Sitter list
          Moves: Msg<'Move> list }

    let private preamble game sitters journal =
        let dealt =
            [ DealWord
              string (Journal.players journal)
              string (Journal.seed journal)
              Seating.line sitters ]
            |> List.filter (fun word -> word <> "")
            |> String.concat " "

        [ $"# A {game.Title} game, written down move by move."
          "#"
          "# Lines that are not comments are commands, exactly as they are typed at the"
          "# prompt. Undo and redo are moves like any other, so reading this file back"
          "# retraces the game as it was really played - second thoughts and all - and"
          "# arrives at the same position it was saved from."
          "#"
          "# The deal line says how many were playing, what they were dealt from, and who"
          "# was in each seat - 'you' for a person here, a skill for the program."
          "#"
          $"#   {Invoked.opening game.Name} replay <this file>"
          ""
          dealt
          "" ]

    let private line game (entry: Entry<'Move, 'Notice>) =
        [ sprintf "# %3d  turn %d, %s" entry.Ordinal entry.Turn (game.Seat entry.Actor)
          game.Write entry.Asked ]
        @ (entry.Told |> List.map (fun notice -> $"#      {Playable.told game notice}"))
        @ [ "" ]

    let private piece lines =
        lines |> List.map (fun line -> line + Environment.NewLine) |> String.concat ""

    let private pieces game sitters journal =
        piece (preamble game sitters journal)
        :: (Journal.entries journal |> List.map (line game >> piece))

    let write game sitters journal =
        pieces game sitters journal |> String.concat ""

    let read game (text: string) =
        let meaningful =
            text.Split('\n')
            |> Array.map (fun line -> line.Trim())
            |> Array.filter (fun line -> line <> "" && not (line.StartsWith "#"))
            |> List.ofArray

        let words (line: string) =
            line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
            |> List.ofArray

        let move outcome line =
            outcome
            |> Result.bind (fun moves ->
                match Playable.read game line with
                | Ok(Send msg) -> Ok(msg :: moves)
                | Ok _ -> Error $"'{line}' is not a move, so it cannot be part of a record."
                | Error problem -> Error problem)

        match meaningful with
        | head :: rest ->
            match words head with
            | word :: players :: seed :: seating when word = DealWord ->
                match Int32.TryParse players, UInt64.TryParse seed with
                | (true, players), (true, seed) ->
                    let sitters =
                        match seating with
                        | [] -> Ok(Seating.here players)
                        | words ->
                            Seating.read game.Skills (game.Fewest, game.Most) words
                            |> Result.bind (fun sitters ->
                                if List.length sitters = players then
                                    Ok sitters
                                else
                                    Error
                                        $"That record deals {players} and then seats {List.length sitters}, which cannot both be true.")

                    sitters
                    |> Result.bind (fun sitters ->
                        rest
                        |> List.fold move (Ok [])
                        |> Result.map (fun moves ->
                            { Players = players
                              Seed = seed
                              Sitters = sitters
                              Moves = List.rev moves }))
                | _ -> Error $"'{head}' does not say how many players and from what seed."
            | _ -> Error $"A record opens with '{DealWord} <players> <seed> <seats>', not '{head}'."
        | [] -> Error "That record says nothing at all."

    let private folder = Path.Combine(Directory.GetCurrentDirectory(), "logs")

    let path stamp journal =
        Path.Combine(folder, sprintf "%s-%dp-seed%d.log" stamp (Journal.players journal) (Journal.seed journal))


    [<Literal>]
    let private Clock = "yyyy-MM-dd-HHmmss"

    let private clockParts = Clock.Split('-').Length

    let stamping (game: string) (at: DateTime) = $"{at.ToString Clock}-{game}"

    let gameOf (stamp: string) =
        match stamp.Split '-' with
        | parts when parts.Length > clockParts -> Some(String.Join("-", parts[clockParts..]))
        | _ -> None

    /// The stamp, seats and seed out of a record's file name, read from the right - the stamp itself
    /// has dashes in it, and so may the game's name, so only the last two parts can be counted on.
    let filed (path: string) =
        match
            Path.GetFileNameWithoutExtension(path: string).Split '-'
            |> List.ofArray
            |> List.rev
        with
        | seed :: seats :: rest when seed.StartsWith "seed" && seats.EndsWith "p" ->
            match UInt64.TryParse(seed.Substring 4), Int32.TryParse(seats.Substring(0, seats.Length - 1)), List.rev rest with
            | (true, seed), (true, players), (_ :: _ as stamp) -> Some(String.Join("-", stamp), players, seed)
            | _ -> None
        | _ -> None

    let stampOf (path: string) (players: int) (seed: uint64) =
        filed path
        |> Option.bind (fun (stamp, said, dealt) -> if said = players && dealt = seed then Some stamp else None)

    let about path =
        filed path |> Option.bind (fun (stamp, _, _) -> gameOf stamp)

    let takenUp hint game (path: string) =
        let ours =
            match about path with
            | Some other when other <> game.Name -> Error $"'{path}' is a game of {other}, not of {game.Name}.{hint other}"
            | _ -> Ok()

        if not (File.Exists path) then
            Error $"There is no record at '{path}'."
        else
            ours
            |> Result.bind (fun () -> read game (File.ReadAllText path))
            |> Result.bind (fun reading ->
                Update.replay game.Rules reading.Players reading.Seed reading.Moves
                |> Result.mapError (fun _ -> $"'{path}' asks for a number of players the game does not take.")
                |> Result.map (fun model ->
                    model, reading.Sitters, stampOf path reading.Players reading.Seed, List.length reading.Moves))

    /// How much of what is already on disk the new record agrees with, and what is left to write. A
    /// game is saved after every move, and a game that has only gone forward writes the same bytes
    /// again with more on the end - so the common case is an append rather than a rewrite.
    let private shared (existing: string) pieces =
        let rec walk at rest =
            match rest with
            | (piece: string) :: more when
                at + piece.Length <= existing.Length
                && String.CompareOrdinal(existing, at, piece, 0, piece.Length) = 0
                ->
                walk (at + piece.Length) more
            | _ -> at, rest

        walk 0 pieces

    // Written beside and moved over, so a record is never half-written: an interruption leaves either
    // the game as it stood before or the game as it stands now, and not something in between.
    let private replace path (text: string) =
        let beside = path + ".writing"
        File.WriteAllText(beside, text)
        File.Move(beside, path, true)


    type Saved =
        { Path: string
          Named: string
          Game: string option
          Players: int
          Seed: uint64
          Moves: int
          Written: DateTime }

    let private glance (path: string) =
        filed path
        |> Option.map (fun (stamp, players, seed) ->
            let moves =
                try
                    File.ReadAllLines path
                    |> Array.map (fun line -> line.Trim())
                    |> Array.filter (fun line -> line <> "" && not (line.StartsWith "#"))
                    |> Array.length
                    |> fun lines -> max 0 (lines - 1)
                with _ ->
                    0

            { Path = path
              Named = Path.GetRelativePath(Directory.GetCurrentDirectory(), path)
              Game = gameOf stamp
              Players = players
              Seed = seed
              Moves = moves
              Written = File.GetLastWriteTime path })

    let saved () =
        try
            if Directory.Exists folder then
                Directory.EnumerateFiles(folder, "*.log")
                |> Seq.choose glance
                |> Seq.sortByDescending (fun saved -> saved.Written)
                |> List.ofSeq
            else
                []
        with _ ->
            []

    let save game stamp sitters journal =
        Directory.CreateDirectory folder |> ignore
        let path = path stamp journal
        let pieces = pieces game sitters journal

        let existing = if File.Exists path then File.ReadAllText path else ""

        match shared existing pieces with
        | at, rest when at = existing.Length -> File.AppendAllText(path, String.concat "" rest)
        | _ -> replace path (String.concat "" pieces)

        path
