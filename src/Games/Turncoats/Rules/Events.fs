namespace TCModel.Turncoats

open TCModel.Engine

/// Why a game finished.
type Ending =
    /// Every player in a row negotiated, or was skipped for want of stones.
    | AllNegotiated
    /// The same, in the particular case where nobody holds a stone any more.
    | AllPlayedOut
    /// The players stopped playing.
    | Abandoned

/// Something that happened. The domain reports what it did in its own terms and
/// leaves the wording to whoever is showing it.
type Event =
    | Recruited of player: PlayerId * color: StoneColor * into: RegionId
    | Battled of player: PlayerId * color: StoneColor * target: RegionId * driven: Pile
    | Marched of player: PlayerId * color: StoneColor * from: RegionId * into: RegionId * count: int
    | Drew of player: PlayerId * color: StoneColor
    | HandedBack of player: PlayerId * color: StoneColor
    | TurnSkipped of player: PlayerId
    | GameEnded of Ending

/// Why an action was refused. Again in the domain's terms, not a player's language.
type Rejection =
    | NotInBag of player: PlayerId * color: StoneColor
    | DeadGround of RegionId
    | StandsApart of RegionId
    /// Nothing of the attacking colour in the region to fight for.
    | NothingToBattleWith of RegionId * StoneColor
    /// Nothing of any other colour in the region to drive out.
    | NothingToDriveOut of RegionId * StoneColor
    | BattleMustDriveOutSomething
    | CannotDriveOutOwnColour of StoneColor
    | MoreDrivenThanAllowed of RegionId * color: StoneColor * allowed: int
    /// More stones on offer than removals, across more than one colour, so the
    /// attacker has to say which they mean.
    | MustChooseCasualties of RegionId * available: Pile * allowed: int
    | NotStandingThere of RegionId * StoneColor
    | NothingToMarch of RegionId * StoneColor
    | NotEnoughToMarch of RegionId * color: StoneColor * held: int * wanted: int
    | MarchNeedsAStone
    | NotAdjacent of from: RegionId * into: RegionId
    | ReserveEmpty
    | EmptyHandedCannotNegotiate of PlayerId
    /// A draw from the reserve is outstanding and must be settled first.
    | MustSettleFirst of drawn: StoneColor
    | NothingToSettle
