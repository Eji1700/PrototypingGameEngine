namespace TCModel.Life

open System

/// One square of the grid, counted from the top-left corner: rows down, columns across, both
/// from one. Nothing about whether anything is in it - what is alive is the world's business,
/// and a cell is only a place.
type Cell = { Row: int; Column: int }

/// Everything alive at once, and the whole of what a position is at this game.
///
/// A set of the living rather than a grid of true and false, because that is what the rule is
/// actually about: a generation is worked out from the cells that are there and the ones they
/// touch, and the eight hundred squares nobody is standing on have nothing to say.
type Cells = Set<Cell>

/// The board, and the one rule played on it.
///
/// Everything below is worked out from the two sides rather than written down, in the way
/// [the other small game](../../TicTacToe/Rules/Marks.fs) works its nine squares out from
/// one - and `Faults` is where this game says whether the arithmetic came out.
///
/// The edges are joined: the column after the last is the first again, and the row below the
/// last is the top. Which is a decision about the game and not about the drawing. A glider on
/// a board with edges runs off it and the game is over in fifty generations; on a board with
/// none it goes round for ever, and a board small enough to read on a screen is only worth
/// watching if it does.
module Grid =

    /// Twenty-six across because a column is named by a letter, and there are twenty-six of
    /// those. Sixteen down because a board and the writing round it should fit on a screen
    /// somebody has not scrolled.
    [<Literal>]
    let Width = 26

    [<Literal>]
    let Height = 16

    let holds cell =
        cell.Row >= 1 && cell.Row <= Height && cell.Column >= 1 && cell.Column <= Width

    /// The board as rows of cells, top to bottom - for anything laying it out in a shape
    /// rather than reasoning about it.
    let rows =
        [ for row in 1..Height -> [ for column in 1..Width -> { Row = row; Column = column } ] ]

    let all = List.concat rows

    // --- what a cell is called ----------------------------------------------------------------
    //
    // A letter for the column and a number for the row, which is how a person reads a square
    // off a board they are looking at: `f7` is six across and seven down. One word, so it is
    // also the whole of a move, and the name a player types and the name the board is drawn
    // with are one string said once.

    let private First = 'a'

    /// The column letters, in order - the heading a board is drawn under.
    let letters = String(Array.init Width (fun column -> char (int First + column)))

    let name cell =
        $"{char (int First + cell.Column - 1)}{cell.Row}"

    /// A cell as somebody typed it, or nothing at all.
    ///
    /// The shape only: `a40` reads perfectly well and is not on this board, and refusing it
    /// here would refuse it in the wrong place. What a player typed is a move like any other,
    /// and a move the rules will not take is something the *rules* say - and write down.
    let read (word: string) =
        match List.ofSeq (word.ToLowerInvariant()) with
        | letter :: (_ :: _ as digits) when Char.IsAsciiLetterLower letter && digits |> List.forall Char.IsAsciiDigit ->
            match Int32.TryParse(String(Array.ofList digits)) with
            | true, row ->
                Some
                    { Row = row
                      Column = int letter - int First + 1 }
            | _ -> None
        | _ -> None

    // --- and the rule -----------------------------------------------------------------------

    /// The same number brought back onto the board, however far off it went. One step off the
    /// right-hand edge is the left-hand one, which is the whole of what joining the edges
    /// means.
    let private round' n bound = ((n - 1) % bound + bound) % bound + 1

    /// The eight cells touching this one, corners counted.
    let neighbours cell =
        [ for down in -1 .. 1 do
              for across in -1 .. 1 do
                  if down <> 0 || across <> 0 then
                      yield
                          { Row = round' (cell.Row + down) Height
                            Column = round' (cell.Column + across) Width } ]

    /// The next generation, which is the whole of Conway's rule and the whole of this game:
    /// a living cell with two or three neighbours lives on, a dead one with exactly three
    /// comes alive, and everything else is empty.
    ///
    /// Worked out from the living outwards rather than by walking every square. Counting the
    /// neighbours of each living cell tallies, in one pass, exactly the cells that could
    /// possibly be alive next - the living and what they touch - and anything not in that
    /// tally has no neighbours at all and is dead whatever it was.
    let step (cells: Cells) : Cells =
        let around =
            cells
            |> Seq.collect neighbours
            |> Seq.fold
                (fun tally cell ->
                    tally
                    |> Map.change cell (function
                        | Some many -> Some(many + 1)
                        | None -> Some 1))
                Map.empty

        around
        |> Map.toSeq
        |> Seq.filter (fun (cell, many) -> many = 3 || (many = 2 && Set.contains cell cells))
        |> Seq.map fst
        |> Set.ofSeq
