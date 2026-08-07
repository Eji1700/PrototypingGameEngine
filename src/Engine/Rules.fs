namespace TCModel.Engine

/// A game, as everything above it needs to know one.
///
/// This is the seam. Below it is whatever a game is made of - stones on a map, cards in a
/// hand, a position and a rulebook; above it are the things every turn-based game wants and
/// none of them should be writing again: a history to walk, a record that replays, seats at
/// a table, a machine to play one of them, a screen per player, a wire between them.
///
/// A record of functions rather than an interface, because that is what the rest of this
/// program does with a seam - a view is one of these too - and because a game is a *value*
/// here. Two of them can sit side by side in one process.
///
/// Three type parameters and no more: what a move is, where the game stands, and what the
/// game has to say. Everything else the engine needs, it asks for below.
[<NoComparison; NoEquality>]
type Rules<'Move, 'State, 'Notice> =
    {
        /// Deal a fresh game for this many, from this seed. How many may play is the game's
        /// own business, so a count it will not take comes back as words a person can read.
        Deal: int -> uint64 -> Result<'State, string>

        /// Ask for a move.
        ///
        /// Total on purpose: a refusal is something the game *says*, not something that
        /// breaks it. `None` for the state means the position did not move - the engine
        /// leaves the history where it was - and the notices say why, which is as true of a
        /// move that carried as of one that did not.
        ///
        /// This is what lets the whole of the rest be a fold. Nothing above this line has to
        /// handle a game that failed, because a game cannot fail; it can only answer.
        Play: 'Move -> 'State -> 'State option * 'Notice list

        /// Whose turn it is. Asked constantly - by the record, by the tables, by the seat a
        /// machine plays - so it is asked of the game rather than guessed at.
        Active: 'State -> PlayerId

        /// Which turn the game is on, for the record to say when something happened.
        Turn: 'State -> int

        /// Whether the game has finished. The engine refuses moves after this and the tables
        /// stop waiting on anybody, and neither of them looks any further into it than that.
        Over: 'State -> bool

        /// How many are playing, for a restart that was not told otherwise.
        Seats: 'State -> int

        /// And a seed for that next game, drawn out of this one rather than off the clock -
        /// so a game restarted from a record restarts the same way twice.
        Reseed: 'State -> uint64
    }
