namespace TCModel.Table

open TCModel.Engine

type ToPlayer =
    | Seated of seat: int * token: string
    | Screen of text: string
    | Told of text: string
    | TurnedAway of why: string
    | GotUp of said: string
    | Nudged

type Post = { To: string; Say: ToPlayer }
