namespace TCModel.Snake

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Snake

/// A typed line as this game's own move.
///
/// Short, and it should be: `undo`, `save`, `view rich`, `resign`, `quit` and `restart` mean
/// the same thing whatever is on the board and have already been read, once, for every game
/// there is. What is left here is the words this game invented, which is four directions and
/// the word for carrying on.
///
/// **There is no `wasd` here, on purpose.** Every player's fingers want it and it cannot be
/// had: `w` is west on a board with compass points on it and up on a keyboard, and `d` is down
/// to half the people who type it and right to the other half. A key that means two things at
/// a game where one wrong move is the end of the game is worse than one key more to reach for,
/// so the single letters are the compass and nothing else.
module Parse =

    let private way direction = Ok(Send(Make(Go direction)))

    /// The whole of what this game reads.
    ///
    /// The compass words and their initials, and the screen words for the same four - because
    /// a player looking at a board sees up and down rather than north and south, and both are
    /// worth taking. A record is written in the compass, so it reads the same whichever a
    /// player typed.
    let rec line typed =
        match Commands.words typed |> List.map (fun word -> word.ToLowerInvariant()) with
        | [ "north" ]
        | [ "n" ]
        | [ "up" ] -> way North
        | [ "east" ]
        | [ "e" ]
        | [ "right" ] -> way East
        | [ "south" ]
        | [ "s" ]
        | [ "down" ] -> way South
        | [ "west" ]
        | [ "w" ]
        | [ "left" ] -> way West
        | [ "go" ]
        | [ "on" ]
        | [ "ahead" ] -> Ok(Send(Make Onward))
        // A question rather than a move: the word goes back out as it came in and this game's
        // own screen answers it. Read for shape here all the same, so that a question nobody
        // could answer is refused where it was typed.
        | [ "why"; word ]
        | [ "look"; word ] ->
            match line word with
            | Ok(Send(Make(Go _))) -> Ok(Asking word)
            | _ -> Error $"'{word}' is not a way to look. Say 'why east', or 'why up'."
        | _ ->
            Error
                "Say a way to go - 'north', 'east', 'south', 'west', or 'n', 'e', 's', 'w' - or 'go' to keep going the way you are. 'help' has the rest."
