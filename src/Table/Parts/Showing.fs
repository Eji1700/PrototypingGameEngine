namespace TCModel.Table

open TCModel.Engine

/// What a table can be heard doing.
///
/// Three of them, and no more: a terminal has one bell, and a game asking for a dozen sounds
/// would be a game asking to be read with the eyes shut. They are named for what happened
/// rather than for what they sound like, so the table is free to make them however it can -
/// the same bargain a `Tone` makes about colour. A table with nothing to make a sound with
/// plays none of them and nothing about the game is lost.
type Sound =
    | Tap
    | Chime
    | Fanfare

type ToPlayer =
    | Seated of seat: int * token: string
    | Screen of text: string
    | Told of text: string
    | TurnedAway of why: string
    | GotUp of said: string
    | Nudged
    | Rang of Sound

type Post = { To: string; Say: ToPlayer }
