namespace TCModel.App

open TCModel.Domain

/// What the active player owes before their turn can end. A turn is either open to
/// any action, or waiting on a stone to go back to the reserve - never both.
type Phase =
    | AwaitingAction
    | AwaitingReturn of drawn: StoneColor

/// A game still being played.
type Play =
    { Game: Game
      Phase: Phase
      /// Turns spent negotiating, or skipped for want of stones, without a stone
      /// being played in between. The game ends when every player has done so in a row.
      Negotiations: int
      Turn: int }

/// A game that has finished. There is no phase and no turn to take.
type Over =
    { Game: Game
      Ending: Ending
      Turn: int }

/// A game is either in play or over, and the two offer different things to do,
/// so no code has to ask whether the game it holds is still running.
type Session =
    | InPlay of Play
    | Finished of Over

/// What the game said back. Everything a player is told passes through here, whether
/// it came from the rules, from walking the history, or from the shell failing to make
/// sense of a line.
type Notice =
    | Happened of Event
    | Refused of Rejection
    | TookBack of Msg
    | MadeAgain of Msg
    | NothingToTakeBack
    | NothingToMakeAgain
    /// A move was asked for after the game had finished.
    | GameIsOver
    | Misunderstood of string

module Session =

    let game session =
        match session with
        | InPlay play -> play.Game
        | Finished over -> over.Game

    let turn session =
        match session with
        | InPlay play -> play.Turn
        | Finished over -> over.Turn

    let isOver session =
        match session with
        | Finished _ -> true
        | InPlay _ -> false
