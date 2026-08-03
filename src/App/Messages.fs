namespace TCModel.App

open TCModel.Domain

/// Something a player does with the game itself, on their turn. Every move is asked
/// for by the player to act, and either the rules allow it or they do not.
type Move =
    | Recruit of color: StoneColor * into: RegionId
    | Battle of color: StoneColor * target: RegionId * driven: Casualties
    | March of color: StoneColor * from: RegionId * into: RegionId * count: int
    | Negotiate
    /// Finish a negotiation by handing a stone back to the reserve.
    | Settle of handBack: StoneColor
    /// Stop playing. The game ends where it stands.
    | Resign

/// Everything the game can be asked to do. A move changes the position; the rest walk
/// the game's own history, or leave it behind for a fresh deal.
type Msg =
    | Make of Move
    /// Take the last move back, whoever made it.
    | Undo
    /// Make again the move that was last taken back.
    | Redo
    /// Abandon this game and deal a fresh one. Anything left unsaid is carried over
    /// from the game in progress.
    | Restart of players: int option * seed: uint64 option
