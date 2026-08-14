namespace TCModel.Compile

open TCModel.Engine

/// Everything this game has to say, in its own terms.
///
/// Its own file, and not beside the move that causes it, because two things now say things: a
/// move made by a player, and the pile working its way down after one. `Resolving` is below this
/// and `Turn` is below that, and neither could emit a notice the other had defined.
type Happening =
    | Drafted of PlayerId * Protocol
    /// All six taken; the protocols are settled and the lines are not.
    | DraftEnded
    /// One player has laid theirs out, face down. What they said is theirs until both have
    /// said it - `Words.saidTo` is where that is kept.
    | Arranged of PlayerId * Protocol list
    /// Both are in, so both are turned over at once. The one public statement of who put what
    /// where, and the reason the hidden half above is safe to hide.
    | Revealed of (PlayerId * Protocol list) list
    /// Both decks built, shuffled and drawn from.
    | HandsDealt
    | Played of PlayerId * Placed * line: int
    /// A hand put down and another taken up. Counts only: what went and what came are that
    /// player's business, and both are on their own screen.
    | Refreshed of PlayerId * put: int * took: int
    /// A line won, and the protocol facing it turned over. Both players' cards in that line go.
    | Compiled of PlayerId * Protocol * line: int
    /// The same, on a protocol that was compiled once already: no nearer winning, the line
    /// wiped just the same, and the top card of the other deck comes across.
    | CompiledAgain of PlayerId * Protocol * line: int
    | Took of PlayerId * Card
    /// A compile that could take nothing, because the other player had no cards left anywhere.
    | TookNothing of PlayerId

    // --- what a card's text does -------------------------------------------------------------

    /// A card turned over where it lies, and which way it landed.
    | Flipped of PlayerId * Placed * line: int
    /// A card taken off the table by an effect rather than by a compile.
    | Deleted of PlayerId * Placed * line: int
    | Discarded of PlayerId * Card
    /// A card out of one hand and into the other, chosen by the one giving it.
    | Gave of PlayerId * Card
    /// And one out of a hand at random, chosen by nobody - which is why what it was is a secret
    /// from the player it came from and not from the player it went to.
    | TookAtRandom of PlayerId * Card
    /// A card off the top of a deck and straight onto the table, seen by neither player before
    /// it landed.
    | PlayedFromDeck of PlayerId * Placed * line: int
    /// A card off the table and back into the hand of whoever it was sitting in front of.
    | Returned of PlayerId * Placed * line: int
    /// A card back to the player whose protocol it belongs to, which is not always the player it
    /// was taken from.
    /// A card moved to another line, on the same side of the table.
    | Shifted of PlayerId * Placed * from: int * ``to``: int
    | Drew of PlayerId * int
    /// A command that reached the table and found nothing to point at. Not an error - it is the
    /// ordinary case, and it is why "delete a card, draw a card" still draws when there was
    /// nothing to delete.
    | Fizzled of PlayerId * Card
    /// The game asking somebody to choose, so that a log read afterwards says what was asked as
    /// well as what was answered.
    | Asked of PlayerId * Card
    /// Somebody said no to something a card offered. Worth saying out loud: whatever the card
    /// had waiting behind an "if you do" is now not going to happen, and the record should show
    /// that it was a choice rather than a failure.
    | Declined of PlayerId
    /// Somebody stopped from compiling when their turn next begins - the one thing in this game
    /// that is remembered rather than read off the board.
    | StoppedCompiling of PlayerId
    /// A card shown to both players and put back where it was. **Public on purpose** - a reveal
    /// that only one seat could read would not be a reveal - and it is the one thing in this game
    /// whose whole effect is that it was said out loud.
    | Showed of PlayerId * Card
    | ShowedHand of PlayerId * Card list

    // --- the control component ---------------------------------------------------------------

    /// Somebody leads two lanes, so the component is theirs - out of the middle, or off the
    /// other player.
    | TookControl of PlayerId * from: PlayerId option
    /// Holding the component and about to compile or refresh, so the protocols have to move -
    /// and it is not a choice about whether, only about where.
    | MustRearrange of PlayerId
    | Rearranged of PlayerId * Protocol list

    | GameEnded of Ending

type Refusal =
    /// A move for a stage the game is not in, carrying the stage it is in - because what
    /// helps a player who drafted at the wrong moment is being told what the game is asking
    /// for now, and only the game knows that.
    | NotNow of Doing
    | AlreadyTaken of Protocol
    | NotDrafted of Protocol
    | NotThree of said: int
    | SaidTwice of Protocol
    | NotInHand of Card
    | NoSuchLine of said: int
    /// Face up, on a line where that card's protocol is not. Carries where it *could* have gone,
    /// because "no" on its own is the least useful thing a game can say - and only the game is
    /// in a position to work it out.
    | NotFacingThere of Card * said: int * couldGo: int list
    /// An empty hand is not a turn to be skipped: refreshing is the action, and it is the only
    /// one left.
    | MustRefresh
    /// A play something on the table forbids, and which line it was aimed at.
    | Forbidden of Barred * line: int
    /// A move made while the game is waiting on somebody. Carries what is being asked, because
    /// answering it is the only thing that will move the game on.
    | AnswerFirst of Wanting
    /// An answer that is not one of the things being offered.
    | NotOnOffer of Wanting

/// What this game has to say, and the whole of it. Nothing about undo and nothing about a
/// line nobody could read: those are the engine's, and are said once, above, in words that
/// suit any game.
type Notice =
    | Happened of Happening
    | Refused of Refusal
