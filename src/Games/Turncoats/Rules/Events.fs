namespace Prototyping.Turncoats

open Prototyping.Engine

type Ending =
    | AllNegotiated
    | AllPlayedOut
    | Abandoned

type Event =
    | Recruited of player: PlayerId * color: StoneColor * into: RegionId
    | Battled of player: PlayerId * color: StoneColor * target: RegionId * driven: Pile
    | Marched of player: PlayerId * color: StoneColor * from: RegionId * into: RegionId * count: int
    | Drew of player: PlayerId * color: StoneColor
    | HandedBack of player: PlayerId * color: StoneColor
    | TurnSkipped of player: PlayerId
    | GameEnded of Ending

type Rejection =
    | NotInBag of player: PlayerId * color: StoneColor
    | DeadGround of RegionId
    | StandsApart of RegionId
    | NothingToBattleWith of RegionId * StoneColor
    | NothingToDriveOut of RegionId * StoneColor
    | BattleMustDriveOutSomething
    | CannotDriveOutOwnColour of StoneColor
    | MoreDrivenThanAllowed of RegionId * color: StoneColor * allowed: int
    | MustChooseCasualties of RegionId * available: Pile * allowed: int
    | NotStandingThere of RegionId * StoneColor
    | NothingToMarch of RegionId * StoneColor
    | NotEnoughToMarch of RegionId * color: StoneColor * held: int * wanted: int
    | MarchNeedsAStone
    | NotAdjacent of from: RegionId * into: RegionId
    | ReserveEmpty
    | EmptyHandedCannotNegotiate of PlayerId
    | MustSettleFirst of drawn: StoneColor
    | NothingToSettle
