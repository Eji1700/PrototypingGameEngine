namespace Prototyping.Common

open System

/// A cell on a board of rows and columns, counted from 1 at the top left.
type Cell = { Row: int; Column: int }

/// A board of so many columns by so many rows. Three games draw one, name its cells the same way -
/// a letter for the column and a number for the row, so `f7` - and refuse the same things off its
/// edge; this is that, said once.
type Grid = { Width: int; Height: int }

module Grid =

    let holds grid cell =
        cell.Row >= 1
        && cell.Row <= grid.Height
        && cell.Column >= 1
        && cell.Column <= grid.Width

    let rows grid =
        [ for row in 1 .. grid.Height -> [ for column in 1 .. grid.Width -> { Row = row; Column = column } ] ]

    let all grid = List.concat (rows grid)

    [<Literal>]
    let private First = 'a'

    [<Literal>]
    let private Last = 'z'

    /// The letters the columns are named by, from a: every name a cell can have begins with one.
    let letters grid =
        String(Array.init grid.Width (fun column -> char (int First + column)))

    let name cell =
        $"{char (int First + cell.Column - 1)}{cell.Row}"

    /// A cell by its name, whether or not any grid holds it - that is `holds`' question.
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

    /// Where the names run to, for a refusal: "The columns run a to p and the rows 1 to 16."
    let span grid =
        $"The columns run a to {Seq.last (letters grid)} and the rows 1 to {grid.Height}."

    /// What to say about a word that is not a cell's name at all.
    let unnamed grid (word: string) =
        $"'{word}' is not a cell. They are named by column and row - 'f7' is column f, row 7 - and they run to {Seq.last (letters grid)}{grid.Height}."

    /// What is wrong with a grid, if anything. A column past z has no letter to be named by, and a
    /// name that does not read back as the cell it was drawn on is a board nobody can type at.
    let faults grid =
        [ if grid.Width < 1 || grid.Height < 1 then
              yield $"a board {grid.Width} by {grid.Height}, which has nothing on it"

          if grid.Width > int Last - int First + 1 then
              yield $"{grid.Width} columns, where the letters they are named by run out at {int Last - int First + 1}"

          if List.length (all grid) <> grid.Width * grid.Height then
              yield $"{List.length (all grid)} cells on a board of {grid.Width} by {grid.Height}"

          if all grid |> List.exists (fun cell -> read (name cell) <> Some cell) then
              yield "a cell whose name does not read back as the cell it was drawn on" ]
