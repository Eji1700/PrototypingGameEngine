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
/// the word for the thing that moves.
///
/// Two readers rather than one, because this game has two paces and they take different lines:
/// at a game of turns a direction is a step and `go` is a step onward; on a clock a direction
/// turns a named snake's head and `go` is the beat itself. Each way of playing carries its own,
/// so a line can only ever mean one thing at the table it was typed at - which is what lets one
/// record be read back by the way that wrote it and by nothing else.
///
/// **There is no `wasd` in the words here, on purpose.** `w` is west on a board with compass
/// points on it and up on a keyboard, and `d` is down to half the people who type it and right
/// to the other half. The single letters are the compass and nothing else - and the keys, which
/// *are* `wasd` on a clock, say which snake they steer and never a bare direction.
module Parse =

    /// A direction, in any of the three spellings a person uses for it.
    let direction word =
        match word with
        | "north"
        | "n"
        | "up" -> Some North
        | "east"
        | "e"
        | "right" -> Some East
        | "south"
        | "s"
        | "down" -> Some South
        | "west"
        | "w"
        | "left" -> Some West
        | _ -> None

    /// Which snake a line names, by the letter it is drawn with.
    let private snake (word: string) =
        if word.Length = 1 && word[0] >= 'a' && word[0] <= 'z' then
            Some(Seat.at (int word[0] - int 'a' + 1))
        else
            None

    let private unreadable =
        "Say a way to go - 'north', 'east', 'south', 'west', or 'n', 'e', 's', 'w'. 'help' has the rest."

    /// A question rather than a move: the words go back out as they came in and this game's own
    /// screen answers them. Read for shape here all the same, so a question nobody could answer
    /// is refused where it was typed.
    let private asking word =
        match direction word with
        | Some _ -> Ok(Asking word)
        | None -> Error $"'{word}' is not a way to look. Say 'why east', or 'why up'."

    // --- a game of turns ---------------------------------------------------------------------

    /// What a table that waits for you reads: a direction is a step, and `go` is a step the way
    /// you are already facing.
    let turning typed =
        match Commands.lowered typed with
        | [ "why"; word ]
        | [ "look"; word ] -> asking word
        | [ "go" ]
        | [ "on" ]
        | [ "ahead" ] -> Ok(Send(Make Onward))
        | [ word ] ->
            match direction word with
            | Some way -> Ok(Send(Make(Go way)))
            | None -> Error unreadable
        | _ -> Error(unreadable + " Or 'go' to keep going the way you are.")

    // --- and a game on a clock -----------------------------------------------------------------

    /// What a table that does not wait reads: a direction turns a snake's head, and the snakes
    /// move when the beat does.
    ///
    /// A bare direction is Snake A's, because at a table of one there is nobody else it could
    /// be and that is the table this pace is usually played at. Everybody else says which snake
    /// they are - `b north` - which is also what every key on the board sends, so nobody at a
    /// table of four is typing a letter they did not mean.
    ///
    /// `go` is the beat, spelt out, and it is here for the one console that cannot press
    /// anything: a game piped in from a file, or a record replaying. It is the same move the
    /// clock plays, so a game played by hand and a game played on time are the same game and
    /// the same record.
    let racing typed =
        match Commands.lowered typed with
        | [ "why"; word ]
        | [ "look"; word ] -> asking word
        | [ "go" ]
        | [ "beat" ]
        | [ "tick" ] -> Ok(Send(Make Beat))
        | [ word ] ->
            match direction word with
            | Some way -> Ok(Send(Make(Steer(Seat.at 1, way))))
            | None -> Error unreadable
        | [ whose; word ] ->
            match snake whose, direction word with
            | Some seat, Some way -> Ok(Send(Make(Steer(seat, way))))
            | None, _ -> Error $"'{whose}' is not a snake. They are lettered from 'a', so 'b north' turns Snake B."
            | _, None -> Error unreadable
        | _ -> Error(unreadable + " A snake of your own is 'b north', and 'go' is one beat.")
