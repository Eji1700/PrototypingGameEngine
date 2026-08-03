namespace TCModel.App

/// A move that was made, and where it left the game.
type Step = { Move: Msg; After: Session }

/// The whole life of one dealt game, with a finger on the present: the moves made so
/// far behind it, the moves taken back ahead of it.
///
/// Because a session is a value, every state the game has been in is still here to walk
/// back to, generator and all. Undo and redo only move the finger, so taking a
/// negotiation back and making it again draws the same stone: undoing is going back in
/// time, not rolling again.
type Timeline =
    private
        { Dealt: Session
          /// Newest first. The head's `After` is the present.
          Made: Step list
          /// Next to make again first.
          TakenBack: Step list }

module Timeline =

    let ofDeal session =
        { Dealt = session
          Made = []
          TakenBack = [] }

    let present timeline =
        match timeline.Made with
        | [] -> timeline.Dealt
        | step :: _ -> step.After

    /// Record a move. Anything taken back is now off the table: play has moved on down
    /// a different road, and there is no way forward but the new one.
    let advance move session timeline =
        { timeline with
            Made = { Move = move; After = session } :: timeline.Made
            TakenBack = [] }

    /// Step back one move, yielding the timeline and the move undone.
    let undo timeline =
        match timeline.Made with
        | [] -> None
        | step :: earlier ->
            Some(
                { timeline with
                    Made = earlier
                    TakenBack = step :: timeline.TakenBack },
                step.Move
            )

    /// Step forward one move, yielding the timeline and the move made again.
    let redo timeline =
        match timeline.TakenBack with
        | [] -> None
        | step :: later ->
            Some(
                { timeline with
                    Made = step :: timeline.Made
                    TakenBack = later },
                step.Move
            )

    let movesMade timeline = List.length timeline.Made

    let movesTakenBack timeline = List.length timeline.TakenBack

    /// The moves standing between the deal and the present, oldest first.
    let moves timeline =
        timeline.Made |> List.rev |> List.map (fun step -> step.Move)

    /// Every state the game has stood in on the way here, oldest first, the deal first
    /// of all.
    let states timeline =
        timeline.Dealt :: (timeline.Made |> List.rev |> List.map (fun step -> step.After))
