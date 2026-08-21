namespace TCModel.Table

open TCModel.Engine

/// What a table can be heard doing.
///
/// Named for what happened rather than for what they sound like, so a table is free to make them
/// however it can - the same bargain a `Tone` makes about colour. A table with nothing to make a
/// sound with plays none of them and nothing about the game is lost.
///
/// The first three are a scale of how much has happened: a `Tap` is small and there will be a
/// great many of them, a `Chime` came out well, a `Fanfare` came out well and rarely. The last
/// two are not on that scale at all - `Ready` is the table waiting on somebody again, and a
/// `Knell` is something ending or being stopped short.
type Sound =
    | Tap
    | Chime
    | Fanfare
    | Ready
    | Knell

module Sound =

    /// Whether a table with one bell should ring for it.
    ///
    /// This is a judgement about how *often* a sound comes rather than how much it is worth. A
    /// page can tell five sounds apart and makes all five; a terminal has one bell and would say
    /// the same thing with it every time, so it keeps it for the three that come rarely enough to
    /// be worth interrupting somebody with. A bell twice a second is a noise rather than a sound.
    let worthABell =
        function
        | Tap
        | Chime -> false
        | Fanfare
        | Ready
        | Knell -> true

    let all = [ Tap; Chime; Fanfare; Ready; Knell ]

    let word =
        function
        | Tap -> "tap"
        | Chime -> "chime"
        | Fanfare -> "fanfare"
        | Ready -> "ready"
        | Knell -> "knell"

    let byWord said =
        all |> List.tryFind (fun sound -> word sound = said)


type ToPlayer =
    | Seated of seat: int * token: string
    | Screen of text: string
    | Told of text: string
    | TurnedAway of why: string
    | GotUp of said: string
    | Nudged
    | Rang of Sound

type Post = { To: string; Say: ToPlayer }
