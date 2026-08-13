namespace TCModel.Compile

open TCModel.Engine

/// Putting the game into English. The rules report what happened in their own terms;
/// everything a player actually reads is written here.
module Words =

    let player seat = $"Player {PlayerId.value seat}"

    /// A seat as one screen names it, with the reader's own marked. Every view does this, and
    /// the game is unreadable without it over a network, where the seat to play is very often
    /// not the seat reading.
    let seated yours seat =
        player seat + (if yours then " (you)" else "")

    let protocol = Protocol.name

    let card = Card.name

    let line (n: int) = $"line {n}"

    /// A run of protocols as an order is said: "Water, Dark, Fire".
    let order protocols =
        protocols |> List.map protocol |> String.concat ", "

    let ending =
        function
        | Abandoned who -> $"{player who} walked away"

    let event =
        function
        | Drafted(who, taken) -> $"{player who} drafts {protocol taken}."
        | DraftEnded -> $"The draft is over. Both players now set their {Protocol.Each} protocols against the {Lines.Count} lines."
        | Arranged(who, protocols) -> $"{player who} sets {order protocols} against lines 1 to {Lines.Count}."
        | HandsDealt -> $"Both decks are shuffled - {Deck.Size} cards each - and {Deck.HandSize} drawn."
        | Played(who, played, where) -> $"{player who} plays {card played} to {line where}."
        | GameEnded e -> $"The game is over: {ending e}."

    /// What the game is asking for, said as the thing to do about it. A refusal for the wrong
    /// stage ends in one of these, because being told what is wanted now is worth more than
    /// being told what is not.
    let asking =
        function
        | TheDraft -> "the draft is still going: say a protocol to take it"
        | TheProtocols -> $"the protocols are being set against the lines: say your {Protocol.Each} in the order you want them"
        | ThePlay -> "cards are being played: say a card and a line, like 'fire-3 2'"
        | Nothing -> "the game is over"

    let rejection =
        function
        | NotNow doing -> $"That is not what the game is asking for - {asking doing}."
        | AlreadyTaken taken -> $"{protocol taken} has already been drafted. There are no duplicates."
        | NotDrafted stranger -> $"{protocol stranger} is not one of yours. You can only set the protocols you drafted."
        | NotThree said -> $"An order is {Protocol.Each} protocols, one for each line - {said} is not."
        | SaidTwice twice -> $"{protocol twice} is in that order twice. Each of your {Protocol.Each} goes on a line of its own."
        | NotInHand wanted -> $"{card wanted} is not in your hand."
        | NoSuchLine said -> $"There is no {line said}. They are numbered 1 to {Lines.Count}."

    /// A message written the way a player types it. The record is kept in the same words the
    /// prompt takes, so a game can be read back and played again without a second language
    /// standing between the two.
    let command msg =
        match msg with
        | Make(Take taken) -> $"draft {Protocol.key taken}"
        | Make(Arrange protocols) ->
            let said = protocols |> List.map Protocol.key |> String.concat " "
            $"arrange {said}"
        | Make(Play(played, where)) -> $"play {Card.key played} {where}"
        | Make Resign -> "resign"
        | Undo -> "undo"
        | Redo -> "redo"
        | Restart(None, None) -> "restart"
        | Restart(None, Some seed) -> $"restart {seed}"
        | Restart(Some players, None) -> $"players {players}"
        | Restart(Some players, Some seed) -> $"players {players} {seed}"

    /// What this game itself said, and the whole of what it has to say for itself.
    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    /// The same, as much of it as one seat may know.
    ///
    /// Everything the game *says* is public here, and that is not the same as everything
    /// being public: what a player holds is hidden, but a hand is never in a notice - it is on
    /// a screen, and hiding it there is the board's business. Nothing announced is a secret,
    /// and the draft is announced on purpose: a protocol taken is taken from both of them.
    let saidTo _ notice = said notice
