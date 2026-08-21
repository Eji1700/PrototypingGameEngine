namespace TCModel.Cascade

open System

type Way =
    | North
    | East
    | South
    | West

/// Which two ways a cell's arms point. Listed clockwise from the one with an arm up, so a quarter
/// turn to the right is one step along this list - the only place that has to know what a quarter
/// turn is.
type Facing =
    | UpRight
    | RightDown
    | DownLeft
    | LeftUp

type Cell = { Row: int; Column: int }

/// A shape the board is watched for: a whole row, a whole column, or two cells by two named by the
/// one at its top left. All a new one has to say is which cells it stands over - everything that
/// counts, lights and writes them reads `Shape.cells` and knows nothing else.
type Shape =
    | Rank of row: int
    | File of column: int
    | Square of at: Cell

module Way =

    let all = [ North; East; South; West ]

    let opposite =
        function
        | North -> South
        | South -> North
        | East -> West
        | West -> East

module Facing =

    let all = [ UpRight; RightDown; DownLeft; LeftUp ]

    let arms =
        function
        | UpRight -> [ North; East ]
        | RightDown -> [ East; South ]
        | DownLeft -> [ South; West ]
        | LeftUp -> [ West; North ]

    /// A quarter turn to the right, which is the only turn there is.
    let turned =
        function
        | UpRight -> RightDown
        | RightDown -> DownLeft
        | DownLeft -> LeftUp
        | LeftUp -> UpRight

    let reaches way facing = arms facing |> List.contains way

    /// Half way round. A corner points between its two arms, so a quarter turn from one pointing
    /// north-east leaves it pointing east - the second of the arms it started with. Nothing in the
    /// rules turns by halves; this is for what draws them.
    let halfway facing = arms facing |> List.item 1

module Board =

    [<Literal>]
    let Width = 16

    [<Literal>]
    let Height = 16

    let rows =
        [ for row in 1..Height -> [ for column in 1..Width -> { Row = row; Column = column } ] ]

    let all = List.concat rows

    let holds cell =
        cell.Row >= 1 && cell.Row <= Height && cell.Column >= 1 && cell.Column <= Width

    /// The next cell that way. It may be off the board, and the caller is the one that cares - an
    /// arm pointing off the edge reaches nothing, which is all being an edge means here.
    let along way cell =
        match way with
        | North -> { cell with Row = cell.Row - 1 }
        | South -> { cell with Row = cell.Row + 1 }
        | West -> { cell with Column = cell.Column - 1 }
        | East -> { cell with Column = cell.Column + 1 }

    [<Literal>]
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

module Shape =

    [<Literal>]
    let Side = 2

    let cells =
        function
        | Rank row -> [ for column in 1 .. Board.Width -> { Row = row; Column = column } ]
        | File column -> [ for row in 1 .. Board.Height -> { Row = row; Column = column } ]
        | Square at ->
            [ for down in 0 .. Side - 1 do
                  for across in 0 .. Side - 1 ->
                      { Row = at.Row + down
                        Column = at.Column + across } ]

    /// Every shape there is, laid out once. Squares overlap and are meant to: a run four wide and
    /// two deep is three squares and worth three, because the shape that came up is the shape and
    /// not the cells it shares with its neighbour.
    let all =
        [ for row in 1 .. Board.Height -> Rank row ]
        @ [ for column in 1 .. Board.Width -> File column ]
        @ [ for row in 1 .. Board.Height - Side + 1 do
                for column in 1 .. Board.Width - Side + 1 -> Square { Row = row; Column = column } ]

    let isLine =
        function
        | Rank _
        | File _ -> true
        | Square _ -> false
