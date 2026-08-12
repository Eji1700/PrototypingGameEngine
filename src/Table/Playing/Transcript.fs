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
        { Players: int
          Seed: uint64
          /// One entry per seat. A record written before a seating was part of one has
          /// everybody down as a person, which is the only reading of it that cannot be wrong
          /// about a machine.
          Sitters: Sitter list
          Moves: Msg<'Move> list }

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
          $"#   {Invoked.program.Value} {game.Name} replay <this file>"
          ""
          dealt
          "" ]

    let private line game (entry: Entry<'Move, 'Notice>) =
        [ sprintf "# %3d  turn %d, %s" entry.Ordinal entry.Turn (game.Seat entry.Actor)
          game.Write entry.Asked ]
        @ (entry.Told |> List.map (fun notice -> $"#      {Playable.told game notice}"))
        @ [ "" ]

    /// The whole record as text.
    let write game sitters journal =
        let body = Journal.entries journal |> List.collect (line game)
        String.concat Environment.NewLine (preamble game sitters journal @ body)

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

    /// The stamp a record is already filed under, read back off its own name.
    ///
    /// This is what makes taking a game up again a continuation rather than a fork: the game
    /// that comes out of a record goes on writing to the file it came out of, so one game is
    /// one record however many sittings it took. The name is put together above, so it is
    /// taken apart here and nowhere else.
    ///
    /// `None` where the name is not one this wrote - a file somebody has renamed or moved -
    /// and then the game is filed fresh instead. Which is the safe way round: a record that
    /// cannot be shown to be this game's is a record not to write over.
    let stampOf (path: string) (players: int) (seed: uint64) =
        let tail = sprintf "-%dp-seed%d.log" players seed
        let name = Path.GetFileName path

        if name.Length > tail.Length && name.EndsWith(tail, StringComparison.Ordinal) then
            Some(name.Substring(0, name.Length - tail.Length))
        else
            None

    let save game stamp sitters journal =
        Directory.CreateDirectory folder |> ignore
        let path = path stamp journal
        File.WriteAllText(path, write game sitters journal)
        path
