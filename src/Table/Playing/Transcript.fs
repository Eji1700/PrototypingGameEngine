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

    /// Opens the record and says what the game was dealt from. Not a move: a game has to
    /// exist before anything can be asked of it.
    [<Literal>]
    let private DealWord = "deal"

    /// What a record says: the deal it began from, and every move that was asked for,
    /// oldest first.
    [<NoComparison; NoEquality>]
    type Reading<'Move> =
        { Players: int
          Seed: uint64
          Moves: Msg<'Move> list }

    let private preamble game journal =
        [ $"# A {game.Title} game, written down move by move."
          "#"
          "# Lines that are not comments are commands, exactly as they are typed at the"
          "# prompt. Undo and redo are moves like any other, so reading this file back"
          "# retraces the game as it was really played - second thoughts and all - and"
          "# arrives at the same position it was saved from."
          "#"
          // The game's own name is in the line, because a record does not otherwise say
          // which game it is in any way a program could read - only in the sentence at the
          // top of it. Which is a fair thing to leave to a person and not a fair thing to
          // leave to whoever pastes this line.
          $"#   {Invoked.program.Value} {game.Name} replay <this file>"
          ""
          $"{DealWord} {Journal.players journal} {Journal.seed journal}"
          "" ]

    let private line game (entry: Entry<'Move, 'Notice>) =
        [ sprintf "# %3d  turn %d, %s" entry.Ordinal entry.Turn (game.Seat entry.Actor)
          game.Write entry.Asked ]
        @ (entry.Told |> List.map (fun notice -> $"#      {Playable.told game notice}"))
        @ [ "" ]

    /// The whole record as text.
    let write game journal =
        let body = Journal.entries journal |> List.collect (line game)
        String.concat Environment.NewLine (preamble game journal @ body)

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
            | [ word; players; seed ] when word = DealWord ->
                match Int32.TryParse players, UInt64.TryParse seed with
                | (true, players), (true, seed) ->
                    rest
                    |> List.fold move (Ok [])
                    |> Result.map (fun moves ->
                        { Players = players
                          Seed = seed
                          Moves = List.rev moves })
                | _ -> Error $"'{head}' does not say how many players and from what seed."
            | _ -> Error $"A record opens with '{DealWord} <players> <seed>', not '{head}'."
        | [] -> Error "That record says nothing at all."

    /// Where records are kept, beside wherever the game was started from.
    let private folder = Path.Combine(Directory.GetCurrentDirectory(), "logs")

    /// The file one game's record lives in. The stamp is fixed when the game is dealt,
    /// so saving the same game again writes over the same file rather than littering.
    let path stamp journal =
        Path.Combine(folder, sprintf "%s-%dp-seed%d.log" stamp (Journal.players journal) (Journal.seed journal))

    let save game stamp journal =
        Directory.CreateDirectory folder |> ignore
        let path = path stamp journal
        File.WriteAllText(path, write game journal)
        path
