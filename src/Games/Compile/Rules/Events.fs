namespace TCModel.Compile

open TCModel.Engine

type Happening =
    | Drafted of PlayerId * Protocol
    | DraftEnded
    | Arranged of PlayerId * Protocol list
    | Revealed of (PlayerId * Protocol list) list
    | HandsDealt
    | Played of PlayerId * Placed * line: int
    | Refreshed of PlayerId * put: int * took: int
    | Compiled of PlayerId * Protocol * line: int
    | CompiledAgain of PlayerId * Protocol * line: int
    | Took of PlayerId * Card
    | TookNothing of PlayerId


    | Flipped of PlayerId * Placed * line: int
    | Deleted of PlayerId * Placed * line: int
    | Discarded of PlayerId * Card
    | Gave of PlayerId * Card
    | TookAtRandom of PlayerId * Card
    | PlayedFromDeck of PlayerId * Placed * line: int
    | Returned of PlayerId * Placed * line: int
    | Shifted of PlayerId * Placed * from: int * ``to``: int
    | Drew of PlayerId * int
    | Fizzled of PlayerId * Card
    | Asked of PlayerId * Card
    | OverTheLimit of PlayerId * over: int
    | Declined of PlayerId
    | StoppedCompiling of PlayerId
    | Showed of PlayerId * Card
    | ShowedHand of PlayerId * Card list


    | TookControl of PlayerId * from: PlayerId option
    | MustRearrange of PlayerId
    | Rearranged of PlayerId * Protocol list

    | GameEnded of Ending

type Refusal =
    | NotNow of Doing
    | AlreadyTaken of Protocol
    | NotDrafted of Protocol
    | NotThree of said: int
    | SaidTwice of Protocol
    | NotInHand of Card
    | NoSuchLine of said: int
    | NotFacingThere of Card * said: int * couldGo: int list
    | MustRefresh
    | Forbidden of Barred * line: int
    | AnswerFirst of Wanting
    | NotOnOffer of Wanting

type Notice =
    | Happened of Happening
    | Refused of Refusal
