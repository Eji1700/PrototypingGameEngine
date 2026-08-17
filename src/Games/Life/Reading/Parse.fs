namespace TCModel.Life

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Life

/// A typed line as this game's own move.
///
/// Short, and it should be: `undo`, `save`, `view rich`, `quit` and `restart` mean the same
/// thing whatever is on the board and have already been read, once, for every game there is.
/// What is left here is the words this game invented, which is a cell and a count.
module Parse =

    let private cell (word: string) =
        match Grid.read word with
        | Some cell -> Ok cell
        | None -> Error $"'{word}' is not a cell. They are named by column and row - 'f7' is column f, row 7."

    let private toggling word =
        cell word |> Result.map (fun cell -> Send(Make(Toggle cell)))

    /// A question rather than a move: the words go back out as they came in, and this game's
    /// own screen answers them. Read for shape here all the same, so that a question nobody
    /// could answer is refused where it was typed.
    let private asking word =
        cell word |> Result.map (fun _ -> Asking word)

    let private running (word: string) =
        match Commands.tryInt word with
        | Some generations -> Ok(Send(Make(Step generations)))
        | None -> Error $"'{word}' is not a number of generations. Say 'step 10', or 'step' for one."

    /// The whole of what this game reads.
    ///
    /// A bare cell is a move and a bare number is a run, and the two can never be mistaken for
    /// each other: a cell begins with a letter and a run does not. Which is worth the shortcut
    /// - `f7 f8 f9` typed three times is how a glider gets drawn, and nobody wants to write
    /// `toggle` in front of each of them. The long way round is kept because it is what a
    /// record is written in, and a record that read as a column of bare coordinates would be a
    /// record nobody could skim.
    let line typed =
        match Commands.words typed |> List.map (fun word -> word.ToLowerInvariant()) with
        | [ "step" ]
        | [ "s" ]
        | [ "run" ] -> Ok(Send(Make(Step 1)))
        | [ "step"; n ]
        | [ "s"; n ]
        | [ "run"; n ] -> running n
        | [ "clear" ] -> Ok(Send(Make Clear))
        | [ "toggle"; c ]
        | [ "t"; c ] -> toggling c
        | [ "why"; c ]
        | [ "ask"; c ] -> asking c
        | [ one ] when (Commands.tryInt one).IsSome -> running one
        | [ one ] -> toggling one
        | _ ->
            Error
                "Say a cell to turn it on or off - 'f7' - or 'step' to let the rule run, 'step 10' for ten. 'help' has the rest."
