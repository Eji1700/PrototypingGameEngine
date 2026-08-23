namespace TCModel.Warband

/// One blow, and everything a player wants to know about it afterwards: who swung, from where,
/// who took it, whether a warder stepped in front of it, and what it left them with. Kept as one
/// record rather than eight arguments because every reader of it wants a different four.
type Landed =
    {
        Side: int
        From: Hex
        Kind: Kind

        /// Whether it was shot rather than swung. What it changes is who it found, which has already
        /// happened by the time this is written - so this is here for the words alone.
        Shot: bool

        Onto: int
        At: Hex
        Took: Kind
        Guarded: bool
        Power: int
        Left: int
    }

type Happening =
    | Mustered of side: int * kind: Kind * hex: Hex
    | Joined
    | RoundOpened of round: int
    | Struck of Landed
    | Fell of side: int * hex: Hex * kind: Kind
    | Tended of side: int * from: Hex * at: Hex * kind: Kind * by: int * left: int
    | Untended of side: int * hex: Hex * kind: Kind
    | Idled of side: int * hex: Hex * kind: Kind
    | Started
    | Halted
    | GameEnded of Ending

/// The refusals that belong to a squad carry which one, because the other side is told only that
/// a muster of theirs was turned down - never what it was or where. Everything hidden in this game
/// is hidden in `Words.saidTo` and nowhere else, and this is what gives it enough to go on.
type Refusal =
    | HexTaken of side: int * hex: Hex * kind: Kind
    | SquadFull of side: int
    | TooAlike of side: int * kind: Kind
    | NotMustering
    | NoBattleYet
    | NoGivingUp

type Notice =
    | Happened of Happening
    | Refused of Refusal
