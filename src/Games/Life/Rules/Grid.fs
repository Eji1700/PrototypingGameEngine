namespace Prototyping.Life

open System

type Cell = { Row: int; Column: int }

type Cells = Set<Cell>

module Grid =

    [<Literal>]
    let Width = 26

    [<Literal>]
    let Height = 16

    let holds cell =
        cell.Row >= 1 && cell.Row <= Height && cell.Column >= 1 && cell.Column <= Width

    let rows =
        [ for row in 1..Height -> [ for column in 1..Width -> { Row = row; Column = column } ] ]

    let all = List.concat rows


    let private First = 'a'

    let letters = String(Array.init Width (fun column -> char (int First + column)))

    let name cell =
        $"{char (int First + cell.Column - 1)}{cell.Row}"

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


    // The grid wraps, so the edges join and a glider walks off one side and back in the other.
    // The doubled modulo is to bring a negative remainder back round, which .NET's does not do.
    let private round' n bound = ((n - 1) % bound + bound) % bound + 1

    let neighbours cell =
        [ for down in -1 .. 1 do
              for across in -1 .. 1 do
                  if down <> 0 || across <> 0 then
                      yield
                          { Row = round' (cell.Row + down) Height
                            Column = round' (cell.Column + across) Width } ]

    /// One generation. Counted by tallying the neighbours of the living rather than by walking
    /// every square, so the work is in what is alive: a cell that no living cell touches never
    /// appears in the tally, and could not have come to life anyway.
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
