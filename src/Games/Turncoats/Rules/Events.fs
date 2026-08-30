namespace Prototyping.Turncoats

open Prototyping.Engine

type Ending =
    | AllNegotiated
    | AllPlayedOut
    | Abandoned

type Event =
    | Recruited of player: PlayerId * colour: StoneColour * into: RegionId
    | Battled of player: PlayerId * colour: StoneColour * target: RegionId * driven: Pile
    | Marched of player: PlayerId * colour: StoneColour * from: RegionId * into: RegionId * count: int
    | Drew of player: PlayerId * colour: StoneColour
    | HandedBack of player: PlayerId * colour: StoneColour
    | TurnSkipped of player: PlayerId
    | GameEnded of Ending

type Rejection =
    | NotInBag of player: PlayerId * colour: StoneColour
    | DeadGround of RegionId
    | StandsApart of RegionId
    | NothingToBattleWith of RegionId * StoneColour
    | NothingToDriveOut of RegionId * StoneColour
    | BattleMustDriveOutSomething
    | CannotDriveOutOwnColour of StoneColour
    | MoreDrivenThanAllowed of RegionId * colour: StoneColour * allowed: int
    | MustChooseCasualties of RegionId * available: Pile * allowed: int
    | NotStandingThere of RegionId * StoneColour
    | NothingToMarch of RegionId * StoneColour
    | NotEnoughToMarch of RegionId * colour: StoneColour * held: int * wanted: int
    | MarchNeedsAStone
    | NotAdjacent of from: RegionId * into: RegionId
    | ReserveEmpty
    | EmptyHandedCannotNegotiate of PlayerId
    | MustSettleFirst of drawn: StoneColour
    | NothingToSettle
