namespace Prototyping.Compile

open Prototyping.Engine

type Whose =
    | Yours
    | Theirs
    | Anyone

type Where =
    | ThisLine
    | AnyLine
    | OtherLines
    | ToOrFromHere

type Barred =
    | NoPlayHere
    | NoFaceDownHere
    | OnlyFaceDown

type Pick =
    | Whichever
    | Highest
    | Lowest

type Selector =
    { Whose: Whose
      Where: Where
      Showing: Face option
      Uncovered: bool
      Covered: bool
      Worth: int list
      NotThis: bool
      JustThis: bool
      WasChosen: bool
      Pick: Pick }

module Select =

    let any =
        { Whose = Anyone
          Where = AnyLine
          Showing = None
          Uncovered = false
          Covered = false
          Worth = []
          NotThis = false
          JustThis = false
          WasChosen = false
          Pick = Whichever }

    let yours selector = { selector with Whose = Yours }
    let theirs selector = { selector with Whose = Theirs }
    let here selector = { selector with Where = ThisLine }
    let elsewhere selector = { selector with Where = OtherLines }

    let faceDown selector =
        { selector with
            Showing = Some FaceDown }

    let faceUp selector = { selector with Showing = Some FaceUp }
    let uncovered selector = { selector with Uncovered = true }
    let covered selector = { selector with Covered = true }
    let worth values selector = { selector with Worth = values }
    let other selector = { selector with NotThis = true }
    let thisCard selector = { selector with JustThis = true }
    let thatCard selector = { selector with WasChosen = true }
    let highest selector = { selector with Pick = Highest }
    let lowest selector = { selector with Pick = Lowest }

type Count =
    | Just of int
    | WorthOfChosen
    | HowManyPlus of int
    | PerCards of int * Selector

type Command =
    | Draw of Count
    | Discard
    | Delete of Selector
    | Flip of Selector
    | Return of Selector
    | Shift of Selector * Where
    | RefreshHand
    | FromDeck of Face * Where
    | PlayFromHand of Face * Where
    | Give
    | TakeAtRandom
    | StopTheirCompile
    | Reveal
    | RevealTheirHand
    | Swap
    | Rearrange of Whose
    | TakeTheirTop
    | Show of Selector
    | OneOrMore of Command
    | UnderThis of Face
    | Times of Count * Command
    | Every of Command
    | InAChosenLine of Command
    | InEachOtherLine of Command
    | InEachLineHolding of Command
    | InAChosenLineOf of atLeast: int * Command
    | May of Command
    | IfYouDo of Command * Command list
    | IfCovering of Command list
    | Either of Command * Command
    | Opposing of Command

/// A rule that holds for as long as the card lies face up, rather than a thing done once.
type Ongoing =
    // What a line counts to.
    | FaceDownWorth of int
    | LinePlus of int
    | LinePlusPerFaceDown of int
    | TheirLineMinus of int

    // Where a card may be played, theirs and yours.
    | TheyCannotPlayHere
    | TheyCannotPlayFaceDownHere
    | TheyMustPlayFaceDown
    | YouMayPlayAnywhere

    // A phase of your own turn that does not happen.
    | SkipsCacheCheck

    // The middle box of every card in the line, either side's, says nothing.
    | Silence

type Trigger =
    | YouDraw
    | YouDelete
    | TheyDiscard
    | YouClearCache

type Text =
    { Top: Ongoing list
      After: (Trigger * Command list) list
      Shown: Command list
      Bottom: Ongoing list
      AtStart: Command list
      AtEnd: Command list

      WhenCovered: Command list
      WhenFlipped: Command list
      WhenCompiled: Command list }

type Source =
    { Owner: PlayerId
      Saying: Card
      Line: int }

type Target =
    | OnTable of PlayerId * line: int * Placed
    | InHand of PlayerId * Card

module Target =

    let card =
        function
        | OnTable(_, _, placed) -> placed.Card
        | InHand(_, card) -> card

    let owner =
        function
        | OnTable(seat, _, _) -> seat
        | InHand(seat, _) -> seat

type Wanting =
    | ACard of Command * Target list
    | AnOrder of whose: PlayerId * Protocol list list
    | ALine of moving: Target * to': int list
    | Whether of Command
    | ALineFor of Command * to': int list
    | OneOf of Command * Command

type Chosen =
    | TheCard of Card
    | TheLine of int
    | Yes
    | No
    | TheFirst
    | TheSecond

type Asker =
    | ACardSaying of Source
    | TheControlComponent
    | TheCacheCheck

type Question =
    { Chooser: PlayerId
      Because: Asker
      Wanting: Wanting }

/// Where a card was until it was placed, which is what the placing is announced as. A card out
/// of a line was announced as it set off, so that placing says nothing when it lands.
type Origin =
    | FromHand
    | OffTheDeck
    | FromLine of int

/// One piece of work on the pile. Most of these exist because something has to happen between
/// two commands rather than inside one: a card is only really placed once whatever it covers
/// has spoken, a `Gate` only opens once the command under it has run, and a turn only ends
/// once everything the last move set off has finished.
type Pending =
    | Run of Command * Source
    | Ask of Question
    | EndTurn
    | BeginTurn
    | Compiling of lines: int list
    | Refreshing
    | Repeating of Command * Source * tally: int
    | Trimming
    | Placing of PlayerId * Placed * line: int * from: Origin
    | Gate of Command list * Source
    | Turning of PlayerId * Placed * line: int
    | Escaping of lines: int list
    | Opening
    | Closing
