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

/// A line in the record of what has happened.
type Notice =
    | Happened of Event
    | Refused of Rejection
    | Misunderstood of string

/// The whole of what the game keeps: the session, and the story so far.
type Model =
    { Seed: uint64
      Session: Session
      /// Newest first.
      Log: Notice list }

module Session =

    let game session =
        match session with
        | InPlay play -> play.Game
        | Finished over -> over.Game

    let turn session =
        match session with
        | InPlay play -> play.Turn
        | Finished over -> over.Turn

module Model =

    /// Longest run of notices kept.
    [<Literal>]
    let LogDepth = 12

    let record notice model =
        { model with Log = notice :: model.Log |> List.truncate LogDepth }

    let recordAll notices model =
        notices |> List.fold (fun model notice -> record notice model) model

    let game model = Session.game model.Session

    let isOver model =
        match model.Session with
        | Finished _ -> true
        | InPlay _ -> false
