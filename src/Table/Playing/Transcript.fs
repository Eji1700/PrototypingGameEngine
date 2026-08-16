namespace TCModel.Table

open System
open System.IO
open TCModel.Engine

/// The record of a game as a file: written so a person can read it, and so the game can
/// play it again.
///
/// There is no second language here. Every move is written exactly as it is typed at the
/// prompt, one to a line, and everything else is a comment - so reading a record back is
/// the same job as reading input from a player, done by the same reader. What a player
/// could not have typed cannot end up in a record, and what is in a record can always be
/// typed.
///
/// Which is why this file knows nothing about what was played. A game says how one of its
/// moves is written and how a line is read; the shape of the file - the deal it opens with,
/// the comments, the one move to a line - is the same at every game there could be.
module Transcript =

    /// Opens the record and says what the game was dealt from, and who was at it. Not a
    /// move: a game has to exist before anything can be asked of it.
    [<Literal>]
    let private DealWord = "deal"

    /// What a record says: the deal it began from, who was in each seat, and every move that
    /// was asked for, oldest first.
    [<NoComparison; NoEquality>]
    type Reading<'Move> =
        {
            Players: int
            Seed: uint64
            /// One entry per seat. A record written before a seating was part of one has
            /// everybody down as a person, which is the only reading of it that cannot be wrong
            /// about a machine.
            Sitters: Sitter list
            Moves: Msg<'Move> list
        }

    let private preamble game sitters journal =
        // Written as one line rather than assembled with spaces between, so a record with no
        // seating in it - there being nobody to name - does not end its deal line in air.
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
          // Who was in each seat is part of the deal and not part of the play, so it is on
          // the deal line and nowhere else. Without it a game taken up again would be taken
          // up against nobody: the machines would be gone and their seats would be waiting
          // for a person who never sat there.
          "# The deal line says how many were playing, what they were dealt from, and who"
          "# was in each seat - 'you' for a person here, a skill for the program."
          "#"
          // The game's own name is in the line, because a record does not otherwise say
          // which game it is in any way a program could read - only in the sentence at the
          // top of it. Which is a fair thing to leave to a person and not a fair thing to
          // leave to whoever pastes this line.
          $"#   {Invoked.opening game.Name} replay <this file>"
          ""
          dealt
          "" ]

    let private line game (entry: Entry<'Move, 'Notice>) =
        [ sprintf "# %3d  turn %d, %s" entry.Ordinal entry.Turn (game.Seat entry.Actor)
          game.Write entry.Asked ]
        @ (entry.Told |> List.map (fun notice -> $"#      {Playable.told game notice}"))
        @ [ "" ]

    /// One self-contained piece of the file: what the record opens with, or one entry.
    ///
    /// Every piece ends in a line break of its own rather than the pieces being strung
    /// together with one between them, so that adding to a record is putting bytes after the
    /// last piece and nothing has to know whether the line above was finished. That is what
    /// lets the save below add to a file instead of writing it out again.
    let private piece lines =
        lines |> List.map (fun line -> line + Environment.NewLine) |> String.concat ""

    /// The record as the pieces it is made of: what it opens with, then one for every move
    /// that was asked for, oldest first.
    ///
    /// A record only ever grows - `Journal.write` puts an entry on the front and nothing ever
    /// takes one off, undoing included - so the pieces of a game fifty moves in are the pieces
    /// of it ten moves in with forty more behind them. Which is the whole of why a save can be
    /// an addition, and is worth saying out loud because it is a promise the engine keeps
    /// rather than anything this file could enforce.
    let private pieces game sitters journal =
        piece (preamble game sitters journal)
        :: (Journal.entries journal |> List.map (line game >> piece))

    /// The whole record as text.
    let write game sitters journal =
        pieces game sitters journal |> String.concat ""

    /// Read a record back. Comments and blank lines fall away; what is left is the deal
    /// and the moves.
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
                // Nothing else can have been written by `Write`, so anything else means the
                // file was not written by this game.
                | Ok(Send msg) -> Ok(msg :: moves)
                | Ok _ -> Error $"'{line}' is not a move, so it cannot be part of a record."
                | Error problem -> Error problem)

        match meaningful with
        | head :: rest ->
            match words head with
            | word :: players :: seed :: seating when word = DealWord ->
                match Int32.TryParse players, UInt64.TryParse seed with
                | (true, players), (true, seed) ->
                    // A record from before the seating was written down says nothing about
                    // who was where, and the only reading of that which cannot be wrong is
                    // that everybody was a person: a machine put back at the wrong seat, or
                    // at the wrong strength, would be a different game taken up.
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

    /// Where records are kept, beside wherever the game was started from.
    let private folder = Path.Combine(Directory.GetCurrentDirectory(), "logs")

    /// The file one game's record lives in. The stamp is fixed when the game is dealt,
    /// so saving the same game again writes over the same file rather than littering.
    let path stamp journal =
        Path.Combine(folder, sprintf "%s-%dp-seed%d.log" stamp (Journal.players journal) (Journal.seed journal))

    // --- what a record is called ----------------------------------------------------------
    //
    // A name is put together in one place and taken apart in the same one, which is the whole
    // of what makes taking a game up again a continuation rather than a fork.
    //
    // The name says which game it is, and it did not always. Two things wanted that. One: a
    // list of saved games has to say what each of them is, and the sentence at the top of a
    // record is a fair thing to leave to a person and not a fair thing to read with a program.
    // Two: handing a record to the wrong game got as far as replaying it and then refused a
    // move in that game's own words - `tictactoe` handed a game of stones answered "say a
    // square's number", which is true, unhelpful, and not what was wrong.
    //
    // It goes in the *stamp* rather than beside it, and that is what keeps every record
    // written before this still working. The stamp is whatever comes before the seats and the
    // seed; `path` never knew what was in it and still does not, so a record filed under the
    // old shape reads back under the old shape and goes on being written to.

    [<Literal>]
    let private Clock = "yyyy-MM-dd-HHmmss"

    /// How many parts of a stamp the clock accounts for, which is what tells a game's name
    /// from the time of day in one. Written from the format above rather than as four, so
    /// that a clock changed here cannot leave this counting the old one.
    let private clockParts = Clock.Split('-').Length

    /// The stamp a fresh game is filed under: when it was dealt, and what it is.
    let stamping (game: string) (at: DateTime) = $"{at.ToString Clock}-{game}"

    /// And the game back out of one. `None` for a record filed before its name was part of
    /// this, which is not a fault - it is a record from an older day, and the only honest
    /// thing to say about which game it is, is that the name does not say.
    ///
    /// What is left after the clock rather than the next word along, because a game's name
    /// may have a dash in it - `compile-control` does - and half a name is worse than none.
    let gameOf (stamp: string) =
        match stamp.Split '-' with
        | parts when parts.Length > clockParts -> Some(String.Join("-", parts[clockParts..]))
        | _ -> None

    /// A record's name taken apart: what it is filed under, how many were playing, and what
    /// they were dealt from.
    ///
    /// `None` where the name is not one this wrote - a file somebody has renamed or moved -
    /// and every caller treats that as "cannot be shown to be a record of ours", which is the
    /// safe way round for all three of the things asked of it.
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

    /// The stamp a record is already filed under, where its name agrees with what is in it.
    ///
    /// This is what makes a resumed game go on writing to the file it came out of, so one game
    /// is one record however many sittings it took. A name that says a different deal from the
    /// one inside it is a name this did not write, and then the game is filed fresh instead -
    /// because a record that cannot be shown to be this game's is a record not to write over.
    let stampOf (path: string) (players: int) (seed: uint64) =
        filed path
        |> Option.bind (fun (stamp, said, dealt) -> if said = players && dealt = seed then Some stamp else None)

    /// Which game a record is, as far as its name says. Both `None`s mean the same thing here
    /// and are worth keeping apart nowhere: nothing is known, so nothing is claimed.
    let about path =
        filed path |> Option.bind (fun (stamp, _, _) -> gameOf stamp)

    /// A record off the disk and back into a game: played through move by move to where it was
    /// left, with the seating it was left with and the file it came out of.
    ///
    /// Here rather than at a way in, and that is the change worth writing down. It was written
    /// once at the keyboard's way in, which was the only thing that had ever needed it - and
    /// then a house of tables needed exactly the same thing, and a second copy of "what a
    /// record means" is the one thing this file exists to stop there being.
    ///
    /// Nothing is printed. What a person is told about taking a game up is the business of
    /// whoever is telling them, and a table in a house is telling nobody - so the count of
    /// moves comes back with the game and the sentence about it is written where there is
    /// somebody to read it.
    ///
    /// `hint` is what to add to the refusal when the record turns out to be some other game's.
    /// A program with a list of games in it can say which line would open that one; a house
    /// that plays one game cannot, and hands back a hint of nothing. Asked for rather than
    /// worked out here, because this file has never heard of a command line.
    ///
    /// Which game it is, is asked *before* the record is read rather than after, because a
    /// record read by the wrong game gets a fair way in: the deal line means the same thing at
    /// every game there is, and what stops it is the first move, refused in that game's own
    /// words. Handing `tictactoe` a game of stones used to answer "say a square's number, they
    /// are numbered 1 to 9" - which is true, and no help at all to somebody who has simply
    /// named the wrong game.
    ///
    /// A record whose name does not say is let through, which is the only honest thing to do
    /// with it: those were written before a name said, and refusing every one of them would
    /// make this check cost more than it caught.
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

    /// How much of what is being saved is already in the file, matched piece by piece from
    /// the top: how far the two run together, and what is left over to write.
    ///
    /// A file that runs on past where they part - a record of some other game filed under this
    /// name, one somebody has edited by hand, one left half-written by a machine that stopped -
    /// agrees for fewer characters than it holds, and that is the signal to write it out again
    /// from the top rather than put this game's moves on the end of somebody else's.
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

    /// Put the whole file down in a way that leaves it either wholly the record it was or
    /// wholly the record it is becoming, and never half of each.
    ///
    /// Written beside itself under another name and then moved over the top, because writing
    /// straight to the file is not one step: a machine that stops in the middle of one leaves
    /// a record cut off wherever it had got to. And a record cut off is not an unreadable file
    /// that says so - it is a shorter game that reads perfectly well, which is the kind of
    /// damage nobody finds until they take the game up again and the last few moves are gone.
    let private replace path (text: string) =
        let beside = path + ".writing"
        File.WriteAllText(beside, text)
        File.Move(beside, path, true)

    // --- and every record there is ----------------------------------------------------------
    //
    // Taking a game up again has worked at every game in this program since there was a record
    // to take up, and almost nobody found it: it wanted a path typed at a menu that never
    // offered one, and the only game whose own README mentioned it was the one long enough to
    // need it. So the records say what they are, here, and a screen offers them.

    /// One saved record, as much of it as can be told without knowing whose game it is.
    type Saved =
        {
            Path: string
            /// The same, said the way a person would type it - which is the way it is printed
            /// when a record is written, and the way a row on a list of them hands it back.
            Named: string
            /// The game it says it is. `None` for a record filed before the name said, which is
            /// worth listing all the same - it is still somebody's game, and the only thing not
            /// known about it is a thing a person may well know.
            Game: string option
            Players: int
            Seed: uint64
            /// How many moves are in it, which is the one thing worth knowing about a saved game
            /// that its name cannot say: whether it is a game or an opening.
            Moves: int
            Written: DateTime
        }

    /// A record read only as far as a list of them needs, which is the deal line, the count of
    /// what follows it, and what is on the outside of the file.
    ///
    /// Not `read` above, and it must not be: that one needs the game whose moves these are, and
    /// the whole point here is a list drawn before anybody has said which game they mean. What
    /// a move *is* is nobody's business at this depth - a line that is not a comment is a move,
    /// and counting them is all that is being asked.
    let private glance (path: string) =
        filed path
        |> Option.map (fun (stamp, players, seed) ->
            let moves =
                try
                    File.ReadAllLines path
                    |> Array.map (fun line -> line.Trim())
                    |> Array.filter (fun line -> line <> "" && not (line.StartsWith "#"))
                    |> Array.length
                    // The deal opens the record and is not a move, so a file with nothing but
                    // a deal line in it is a game nobody has moved in rather than a game of one.
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

    /// Every record there is, the one put down most recently first - which is very nearly
    /// always the one somebody means.
    ///
    /// A folder that is not there is no records rather than a fault: it is what every machine
    /// this program has never been played on looks like.
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
        // The file is this record as far as it goes, so the rest of it goes on the end. This
        // is the ordinary case - a game saved after every move is a file one entry short of
        // itself - and it is why a long game no longer costs the whole of itself in writing
        // every time somebody moves in it. A file that is not there yet is the same case with
        // nothing agreed and everything left over, which appending creates.
        | at, rest when at = existing.Length -> File.AppendAllText(path, String.concat "" rest)
        // They part before the end of the file, so this is not the record that is in it - or
        // is, with a torn piece after it from a save that did not finish. Either way the file
        // stops being trusted and the record is written out whole, which is what makes a torn
        // record heal itself on the next save rather than stay broken.
        | _ -> replace path (String.concat "" pieces)

        path
