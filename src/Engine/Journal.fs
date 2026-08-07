namespace TCModel.Engine

/// One line of the record: who asked for what, on whose turn, and what came of it.
///
/// Moves the game refused are kept alongside those that carried. Asking is part of what
/// happened at the table, and a refusal can tell everyone something - that a bag was empty,
/// that a region held nothing of that colour - so it belongs in the record just as much as a
/// position changing.
type Entry<'Move, 'Notice> =
    {
        Ordinal: int
        Turn: int
        /// The seat to act when the move was asked for.
        Actor: PlayerId
        Asked: Msg<'Move>
        Told: Told<'Move, 'Notice> list
    }

/// The record of one dealt game, in the order things happened.
///
/// Nothing is ever taken out. Undoing a move adds a line saying so rather than erasing the
/// one before it, so the record is the game as it was actually played - doubling back and
/// all - and reading it from the top plays that same game again.
///
/// That last part is the engine's side of a bargain the game has to keep: moves are written
/// in the words the prompt takes, and dealing is settled by a count and a seed. Given those,
/// a record is a game, and every game is replayable without anybody writing a replayer.
type Journal<'Move, 'Notice> =
    private
        {
            Players: int
            Seed: uint64
            /// Newest first.
            Written: Entry<'Move, 'Notice> list
        }

module Journal =

    let ofDeal players seed =
        { Players = players
          Seed = seed
          Written = [] }

    let players journal = journal.Players

    let seed journal = journal.Seed

    let length journal = List.length journal.Written

    let isEmpty journal = List.isEmpty journal.Written

    /// Every entry, oldest first.
    let entries journal = List.rev journal.Written

    let write turn actor asked told journal =
        { journal with
            Written =
                { Ordinal = length journal + 1
                  Turn = turn
                  Actor = actor
                  Asked = asked
                  Told = told }
                :: journal.Written }

    /// Every move as it was asked for, oldest first: enough, with the deal, to play the whole
    /// game again and arrive at exactly the state it left off in.
    let moves journal =
        entries journal |> List.map (fun entry -> entry.Asked)
