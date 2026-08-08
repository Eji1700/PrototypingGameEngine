namespace TCModel.TicTacToe

/// What is on the board, and nothing about whose turn it is or how the game got here.
///
/// Private, so a board can only be built by placing marks on an empty one. There is no way
/// to hand the rules a position that could not have been played to.
type Board = private Board of Map<int, Mark>

module Board =

    let empty = Board Map.empty

    let at square (Board marks) = Map.tryFind square marks

    let isFree square board = at square board |> Option.isNone

    let place square mark (Board marks) = Board(Map.add square mark marks)

    /// Every square still to be had, in order. This is also the whole of what a machine has
    /// to choose between, which is why it is here rather than worked out again there.
    let free board =
        Squares.all |> List.filter (fun square -> isFree square board)

    let isFull board = free board |> List.isEmpty

    let marks (Board marks) = Map.count marks

    /// The line somebody holds all of, with the mark that holds it - the first one, which is
    /// the only one there can be: a move that completed two lines at once ends the game on
    /// the first, and both were made by the same mark anyway.
    let winning board =
        Squares.lines
        |> List.tryPick (fun line ->
            match line |> List.map (fun square -> at square board) with
            | Some mark :: rest when rest |> List.forall ((=) (Some mark)) -> Some(mark, line)
            | _ -> None)

    /// Which squares a mark holds, for anything counting rather than drawing.
    let held mark board =
        Squares.all |> List.filter (fun square -> at square board = Some mark)
