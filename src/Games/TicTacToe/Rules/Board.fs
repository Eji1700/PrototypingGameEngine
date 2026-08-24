namespace Prototyping.TicTacToe

type Board = private Board of Map<int, Mark>

module Board =

    let empty = Board Map.empty

    let at square (Board marks) = Map.tryFind square marks

    let isFree square board = at square board |> Option.isNone

    let place square mark (Board marks) = Board(Map.add square mark marks)

    let free board =
        Squares.all |> List.filter (fun square -> isFree square board)

    let isFull board = free board |> List.isEmpty

    let marks (Board marks) = Map.count marks

    let winning board =
        Squares.lines
        |> List.tryPick (fun line ->
            match line |> List.map (fun square -> at square board) with
            | Some mark :: rest when rest |> List.forall ((=) (Some mark)) -> Some(mark, line)
            | _ -> None)

    let held mark board =
        Squares.all |> List.filter (fun square -> at square board = Some mark)
