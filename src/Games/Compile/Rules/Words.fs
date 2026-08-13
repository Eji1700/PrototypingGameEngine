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

    /// A run of lines as a sentence's tail: "line 2", "line 1 or line 3", "no line at all".
    let lines =
        function
        | [] -> "no line at all"
        | [ only ] -> line only
        | many ->
            let said = many |> List.map line
            String.concat ", " (List.truncate (List.length said - 1) said) + " or " + List.last said

    /// A card as it lies, to somebody who may see what it is: face down, what it is worth is
    /// two whatever is printed on it, and saying both is the whole of what a player needs.
    let placed card =
        match card.Face with
        | FaceUp -> Card.name card.Card
        | FaceDown -> $"[{Placed.FaceDownValue}] {Card.name card.Card}"

    /// And to somebody who may not. A face-down card is a two and nothing else to the player
    /// across the table - which is the only thing on this board that is worth something exactly
    /// because nobody can read it.
    let faceless card =
        match card.Face with
        | FaceUp -> Card.name card.Card
        | FaceDown -> $"[{Placed.FaceDownValue}]"

    /// What a board says where a protocol would be, before both orders are turned over.
    ///
    /// Its own word rather than "face down", which the log says about the same thing in prose:
    /// a screen that keeps something back should say so in one place, so that what is kept
    /// back can be *checked* by looking for it. A test that searched for "face down" would
    /// find the sentence explaining the rule and pass whether or not the board held its tongue.
    let hidden = "hidden"

    /// A run of protocols as an order is said: "Water, Dark, Fire".
    let order protocols =
        protocols |> List.map protocol |> String.concat ", "

    let ending =
        function
        | Won who -> $"{player who} has compiled all {Protocol.Each} of their protocols"
        | Abandoned who -> $"{player who} walked away"

    // --- what is printed on a card ------------------------------------------------------------
    //
    // Generated from what the card *does*, rather than written out beside it. That is the whole
    // argument for card text being data: a card cannot say one thing and do another, seventy-two
    // of them cannot drift one at a time, and all three views get the wording for nothing.

    let private pointing selector =
        let whose =
            match selector.Whose with
            | Yours -> "your"
            | Theirs -> "their"
            | Anyone -> "any"

        let way =
            match selector.Showing with
            | Some FaceUp -> " face-up"
            | Some FaceDown -> " face-down"
            | None -> ""

        let covered = if selector.Uncovered then " uncovered" else ""

        let where =
            match selector.Where with
            | ThisLine -> " in this line"
            | AnyLine -> ""

        $"{whose}{covered}{way} card{where}"

    let rec printing =
        function
        | Draw 1 -> "draw a card"
        | Draw n -> $"draw {n} cards"
        | Discard -> "discard a card"
        | Delete selector -> $"delete {pointing selector}"
        | Flip selector -> $"flip {pointing selector}"
        | Return selector -> $"return {pointing selector} to hand"
        | Shift selector -> $"shift {pointing selector} to another line"
        | Rehome selector -> $"give {pointing selector} back to whoever drafted it"
        | Refreshing' -> "refresh"
        | May inner -> $"you may {printing inner}"
        | IfYouDo(first, rest) ->
            let after = rest |> List.map printing |> String.concat ", then "
            $"{printing first}. If you do, {after}"
        | Opposing inner -> $"your opponent: {printing inner}"

    let ongoing =
        function
        | CountsAs n -> $"this card counts as {n}"
        | Unbreakable -> "this card cannot be deleted"

    /// The commands run together into one printed line. Each command is written in the lower case
    /// it reads in the middle of a sentence, so the capital goes on here rather than in ten
    /// places that would have to agree about it.
    let private capital (text: string) =
        if text = "" then
            text
        else
            string (System.Char.ToUpperInvariant text[0]) + text.Substring 1

    let private sentence said =
        match said with
        | [] -> None
        | said -> Some(said |> List.map (fun one -> capital one + ".") |> String.concat " ")

    /// A card as it is printed, box by box, top to bottom - and only the boxes it uses.
    ///
    /// Which box a line is in is worth saying, because it is the difference between a rule that
    /// survives being built on and one that does not. So the bottom two are marked and the top
    /// one is not, which is the way round that leaves the common case unadorned.
    let printed card =
        let text = Printed.on card

        [ text.Top |> List.map ongoing |> sentence
          text.Shown |> List.map printing |> sentence
          text.Bottom
          |> List.map ongoing
          |> sentence
          |> Option.map (fun said -> "While uncovered: " + said)
          text.AtEnd
          |> List.map printing
          |> sentence
          |> Option.map (fun said -> "At the end of your turn, while uncovered: " + said)
          text.WhenCovered
          |> List.map printing
          |> sentence
          |> Option.map (fun said -> "When this card would be covered, first: " + said) ]
        |> List.choose id

    let event =
        function
        | Drafted(who, taken) -> $"{player who} drafts {protocol taken}."
        | DraftEnded ->
            $"The draft is over. Both players now set their {Protocol.Each} protocols against the {Lines.Count} lines, face down."
        | Arranged(who, protocols) -> $"{player who} sets {order protocols} against lines 1 to {Lines.Count}."
        | Revealed both ->
            let said =
                both
                |> List.map (fun (who, protocols) -> $"{player who} has {order protocols}")
                |> String.concat "; "

            $"Both are turned over at once - {said}."
        | HandsDealt -> $"Both decks are shuffled - {Deck.Size} cards each - and {Deck.HandSize} drawn."
        | Played(who, played, where) ->
            match played.Face with
            | FaceUp -> $"{player who} plays {card played.Card} to {line where}."
            | FaceDown -> $"{player who} plays {card played.Card} face down to {line where}, for {Placed.FaceDownValue}."
        | Refreshed(who, put, took) ->
            match put, took with
            | 0, took -> $"{player who} refreshes on an empty hand and draws {took}."
            | put, took -> $"{player who} refreshes: {put} put down, {took} drawn."
        | Compiled(who, compiling, where) ->
            $"{player who} compiles {protocol compiling} on {line where}. Everything in that line goes."
        | CompiledAgain(who, compiling, where) ->
            $"{player who} compiles {protocol compiling} on {line where} - already compiled, so the line goes and a card comes off the other deck."
        | Took(who, taken) -> $"{player who} takes {card taken} from the top of the other deck."
        | Flipped(who, turned, where) ->
            let way =
                match turned.Face with
                | FaceUp -> "face up"
                | FaceDown -> $"face down, for {Placed.FaceDownValue}"

            $"{player who}'s {card turned.Card} on {line where} is turned {way}."
        | Deleted(who, gone, where) -> $"{card gone.Card} is deleted from {player who}'s {line where}."
        | Discarded(who, gone) -> $"{player who} discards {card gone}."
        | Returned(who, back, where) -> $"{card back.Card} goes back to {player who}'s hand from {line where}."
        | WentHome(home, back) -> $"{card back} goes back to {player home}, whose protocol it is."
        | Shifted(who, moved, from, into) -> $"{player who} shifts {card moved.Card} from {line from} to {line into}."
        | Drew(who, count) -> if count = 1 then $"{player who} draws a card." else $"{player who} draws {count}."
        | Fizzled(_, saying) -> $"{card saying} finds nothing to do."
        | Asked(who, saying) -> $"{card saying} asks {player who} to choose."
        | Declined who -> $"{player who} says no, so nothing waiting on that happens."
        | TookControl(who, from) ->
            match from with
            | Some was -> $"{player who} leads {Field.LanesForControl} lanes and takes the control component from {player was}."
            | None -> $"{player who} leads {Field.LanesForControl} lanes and takes the control component."
        | MustRearrange who ->
            $"{player who} holds the control component, so their protocols have to move before anything else - and into a different order."
        | Rearranged(who, protocols) -> $"{player who} sets theirs to {order protocols}."
        | TookNothing who -> $"{player who} takes nothing - the other player has no cards left to take."
        | GameEnded e -> $"The game is over: {ending e}."

    /// What the game is asking for, said as the thing to do about it. A refusal for the wrong
    /// stage ends in one of these, because being told what is wanted now is worth more than
    /// being told what is not.
    let asking =
        function
        | TheDraft -> "the draft is still going: say a protocol to take it"
        | TheProtocols -> $"the protocols are being set against the lines: say your {Protocol.Each} in the order you want them"
        | ThePlay -> "cards are being played: say a card and a line, like 'fire-3 2'"
        | AChoice -> "a card is waiting on somebody to choose"
        | Nothing -> "the game is over"

    /// A run of cards, for a question offering them.
    let choices cards =
        cards |> List.map Card.name |> String.concat ", "

    /// What a question is asking for, in the words that would answer it.
    let wanting =
        function
        | ACard(_, targets) -> $"say one of: {choices (targets |> List.map Target.card)}"
        | AnOrder offered ->
            let said =
                offered
                |> List.map (fun each -> each |> List.map Protocol.key |> String.concat " ")
                |> String.concat ", or "

            $"say your {Protocol.Each} in a different order - {said}"
        | ALine(moving, offered) -> $"say where {card (Target.card moving)} goes - {lines offered}"
        | Whether inner -> $"say 'yes' or 'no' - {printing inner}"

    let rejection =
        function
        | NotNow doing -> $"That is not what the game is asking for - {asking doing}."
        | AlreadyTaken taken -> $"{protocol taken} has already been drafted. There are no duplicates."
        | NotDrafted stranger -> $"{protocol stranger} is not one of yours. You can only set the protocols you drafted."
        | NotThree said -> $"An order is {Protocol.Each} protocols, one for each line - {said} is not."
        | SaidTwice twice -> $"{protocol twice} is in that order twice. Each of your {Protocol.Each} goes on a line of its own."
        | NotInHand wanted -> $"{card wanted} is not in your hand."
        | NoSuchLine said -> $"There is no {line said}. They are numbered 1 to {Lines.Count}."
        | NotFacingThere(wanted, said, couldGo) ->
            let where =
                match couldGo with
                | [] -> $"{protocol wanted.Protocol} is on no line, so it can only go face down."
                | couldGo -> $"{protocol wanted.Protocol} is on {lines couldGo} - or play it face down anywhere, for {Placed.FaceDownValue}."

            $"{card wanted} cannot go face up on {line said}. {where}"
        | AnswerFirst asked ->
            $"The game is waiting on an answer, and nothing else can happen until it comes: {wanting asked}."
        | NotOnOffer asked -> $"That is not one of the things being offered: {wanting asked}."
        | MustRefresh ->
            $"Your hand is empty, so refreshing is the only thing left to do this turn - say 'refresh', and {Deck.HandSize} come up."

    /// A message written the way a player types it. The record is kept in the same words the
    /// prompt takes, so a game can be read back and played again without a second language
    /// standing between the two.
    let command msg =
        match msg with
        | Make(Take taken) -> $"draft {Protocol.key taken}"
        | Make(Arrange protocols) ->
            let said = protocols |> List.map Protocol.key |> String.concat " "
            $"arrange {said}"
        | Make(Play(played, where, FaceUp)) -> $"play {Card.key played} {where}"
        | Make(Play(played, where, FaceDown)) -> $"play {Card.key played} {where} down"
        | Make Refresh -> "refresh"
        | Make(Choose(TheCard chosen)) -> $"choose {Card.key chosen}"
        | Make(Choose(TheLine line)) -> $"choose line {line}"
        | Make(Choose Yes) -> "yes"
        | Make(Choose No) -> "no"
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
    /// One thing at this game is said and kept back, and it is here: which protocol somebody
    /// put against which line, before both of them have. They are laid out face down and
    /// turned over together, so a table where the seats come round one at a time plays the
    /// same game as two people laying them out at once - and it has to, because a card may be
    /// played face up against *either* protocol on a line, which makes an order seen early
    /// worth a great deal.
    ///
    /// That the reader is told there *was* one is deliberate. Anybody at a real table can see
    /// that the other player has finished laying theirs out; what they cannot see is which way
    /// round. The draft above it is announced on purpose - a protocol taken is taken from both
    /// of them - and a hand is never in a notice at all: it is on a screen, and hiding it
    /// there is the board's business.
    let saidTo seat notice =
        match notice with
        | Happened(Arranged(who, _)) when who <> seat ->
            $"{player who} sets their {Protocol.Each} protocols against the lines, face down."
        // A card played face down is a two to the player across the table and nothing else. The
        // one who played it knows what it is - they had it in their hand a moment ago - and
        // pretending otherwise would be the game keeping a secret from the person who owns it.
        | Happened(Played(who, played, where)) when who <> seat && not (Placed.isFaceUp played) ->
            $"{player who} plays a card face down to {line where}, for {Placed.FaceDownValue}."
        // A card off the top of a shuffled deck is a card nobody had seen. The player it went
        // to has seen it now; the player it came from knows only that they are one lighter,
        // which is exactly what they would know at a table.
        | Happened(Took(who, _)) when who <> seat -> $"{player who} takes a card from the top of your deck."
        | notice -> said notice
