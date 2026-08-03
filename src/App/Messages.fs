namespace TCModel.App

open TCModel.Domain

/// The four things a player may ask to do on their turn.
type Action =
    | Recruit of color: StoneColor * into: RegionId
    | Battle of color: StoneColor * target: RegionId * driven: Casualties
    | March of color: StoneColor * from: RegionId * into: RegionId * count: int
    | Negotiate

/// Everything the game can be asked to do.
type Msg =
    | Act of Action
    /// Finish a negotiation by handing a stone back to the reserve.
    | Settle of handBack: StoneColor
    /// Abandon this game and deal a fresh one. Anything left unsaid is carried over
    /// from the game in progress.
    | Restart of players: int option * seed: uint64 option
    | Quit
