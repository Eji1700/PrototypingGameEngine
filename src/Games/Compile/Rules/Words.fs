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

        let best =
            match selector.Pick with
            | Whichever -> ""
            | Highest -> " highest-value"
            | Lowest -> " lowest-value"

        let way =
            match selector.Showing with
            | Some FaceUp -> " face-up"
            | Some FaceDown -> " face-down"
            | None -> ""

        let lying =
            match selector.Uncovered, selector.Covered with
            | true, _ -> " uncovered"
            | _, true -> " covered"
            | _ -> ""

        let notThis = if selector.NotThis then " other" else ""

        let worth =
            match selector.Worth with
            | [] -> ""
            | [ only ] -> $" worth {only}"
            | many ->
                let said = many |> List.map string
                " worth " + String.concat ", " (List.truncate (List.length said - 1) said) + " or " + List.last said

        let where =
            match selector.Where with
            | ThisLine -> " in this line"
            | OtherLines -> " in another line"
            | AnyLine
            | ToOrFromHere -> ""

        // "This card" and "that card" are whole phrases and take none of the rest of it: a card
        // that said "flip your uncovered this card" would be reading the record out rather than
        // reading the card.
        if selector.JustThis then "this card"
        elif selector.WasChosen then "that card"
        else $"{whose}{best}{notThis}{lying}{way} card{worth}{where}"

    let rec printing =
        function
        | Draw(Just 1) -> "draw a card"
        | Draw(Just n) -> $"draw {n} cards"
        | Draw WorthOfChosen -> "draw cards equal to that card's value"
        | Draw(HowManyPlus n) -> $"draw that many cards plus {n}"
        | Draw(PerCards(each, _)) -> $"draw a card for every {each} cards in this line"
        | Discard -> "discard a card"
        | Delete selector -> $"delete {pointing selector}"
        | Flip selector -> $"flip {pointing selector}"
        | Return selector -> $"return {pointing selector} to hand"
        | Shift(selector, ThisLine) -> $"shift {pointing selector} to this line"
        | Shift(selector, ToOrFromHere) -> $"shift {pointing selector} either to or from this line"
        | Shift(selector, _) -> $"shift {pointing selector} to another line"
        | Refreshing' -> "refresh"
        | Give -> "give a card from your hand to your opponent"
        | TakeAtRandom -> "take a card at random from your opponent's hand"
        | StopTheirCompile -> "your opponent cannot compile next turn"
        | Reveal -> "reveal a card from your hand"
        | RevealTheirHand -> "your opponent reveals their hand"
        | Swap -> "swap the positions of two of your protocols"
        | Rearrange Theirs -> "rearrange their protocols"
        | Rearrange _ -> "rearrange your protocols"
        | TakeTheirTop -> "draw the top card of your opponent's deck"
        | Show selector -> $"reveal {pointing selector}"
        | OneOrMore inner -> (printing inner) + ", one or more times"
        | UnderThis FaceDown -> "play the top card of your deck face down under this card"
        | UnderThis FaceUp -> "play the top card of your deck face up under this card"
        | Times(Just n, inner) -> $"{printing inner}, {n} times over"
        | Times(PerCards(each, _), inner) -> $"{printing inner}, once for every {each} cards in this line"
        | Times(_, inner) -> $"{printing inner}, once for each"
        | FromDeck(face, where) ->
            let way =
                match face with
                | FaceUp -> "face up"
                | FaceDown -> "face down"

            let into =
                match where with
                | ThisLine
                | ToOrFromHere -> " in this line"
                | OtherLines -> " in another line"
                | AnyLine -> ""

            $"play the top card of your deck {way}{into}"
        | PlayFromHand(face, where) ->
            let way =
                match face with
                | FaceUp -> "face up"
                | FaceDown -> "face down"

            let into =
                match where with
                | ThisLine
                | ToOrFromHere -> " in this line"
                | OtherLines -> " in another line"
                | AnyLine -> ""

            $"play a card from your hand {way}{into}"
        | May inner -> $"you may {printing inner}"
        // "delete any card" becomes "delete every card", which is the one place the words for a
        // command are not simply the words for what is inside it.
        | InAChosenLine inner -> (printing inner) + ", in a line of your choosing"
        | InEachOtherLine inner -> (printing inner) + ", in each other line"
        | InEachLineHolding inner -> (printing inner) + ", in each line where you have a card"
        | InAChosenLineOf(atLeast, inner) ->
            (printing inner) + $", in another line of your choosing with {atLeast} or more cards"
        | Every inner -> (printing inner).Replace(" any ", " every ").Replace(" your ", " every ").Replace(" their ", " every ")
        | IfYouDo(first, rest) ->
            let after = rest |> List.map printing |> String.concat ", then "
            $"{printing first}. If you do, {after}"
        | IfCovering rest -> "if this card is covering a card, " + (rest |> List.map printing |> String.concat ", then ")
        | Either(first, second) -> $"either {printing first} or {printing second}"
        | Opposing inner -> $"your opponent: {printing inner}"

    let ongoing =
        function
        | FaceDownWorth n -> $"every face-down card in this stack is worth {n}"
        | LinePlus n -> $"your total in this line is increased by {n}"
        | LinePlusPerFaceDown n -> $"your total in this line is increased by {n} for each face-down card in it"
        | TheirLineMinus n -> $"their total in this line is reduced by {n}"
        | TheyCannotPlayHere -> "your opponent cannot play cards in this line"
        | TheyCannotPlayFaceDownHere -> "your opponent cannot play cards face down in this line"
        | TheyMustPlayFaceDown -> "your opponent can only play cards face down"
        | YouMayPlayAnywhere -> "you can play cards in any line"
        | SkipsCacheCheck -> "you skip your check cache phase"
        | Silence -> "the middle commands of cards in this line do nothing"

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

    /// A card as it is printed: the top box, the middle box and the bottom box, in that order,
    /// each of them possibly empty.
    ///
    /// **Which box a rule is in is the rule that matters most at this game.** A card played over
    /// another one covers its middle and its bottom and leaves its top showing, so the same
    /// sentence is a rule that survives being built on or one that does not, depending on
    /// nothing but where it is printed. That is why they are handed back separately rather than
    /// run together: a board that draws all three, empty ones and all, says which half of a card
    /// a cover has taken away by leaving a box a player can see is empty.
    ///
    /// The grouping is the one the **rules** make rather than the one a printed card's
    /// typography makes, and the two part company in exactly one place: all four triggers go on
    /// listening from under another card, so they are top-box things here however they read.
    /// `Ruling.saying` splits the standing rules the same way and `Resolving` splits the
    /// triggers the same way, so *the top box is exactly what a cover cannot silence* - which is
    /// a sentence a check can hold this to, and typography is not.
    let boxes card =
        let text = Printed.on card

        let listening =
            function
            | YouDraw -> "After you draw cards"
            | YouDelete -> "After you delete cards"
            | TheyDiscard -> "After your opponent discards cards"
            | YouClearCache -> "After you clear cache"

        let top =
            [ text.Top |> List.map ongoing |> sentence
              for trigger, commands in text.After do
                  commands
                  |> List.map printing
                  |> sentence
                  |> Option.map (fun said -> $"{listening trigger}: " + said)
              text.WhenFlipped
              |> List.map printing
              |> sentence
              |> Option.map (fun said -> "When this card would be flipped, first: " + said)
              text.WhenCompiled
              |> List.map printing
              |> sentence
              |> Option.map (fun said -> "When this card would be deleted by compiling, first: " + said) ]

        let middle = [ text.Shown |> List.map printing |> sentence ]

        let bottom =
            [ text.Bottom |> List.map ongoing |> sentence
              text.AtStart
              |> List.map printing
              |> sentence
              |> Option.map (fun said -> "At the start of your turn: " + said)
              text.AtEnd
              |> List.map printing
              |> sentence
              |> Option.map (fun said -> "At the end of your turn: " + said)
              text.WhenCovered
              |> List.map printing
              |> sentence
              |> Option.map (fun said -> "When this card would be covered, first: " + said) ]

        List.choose id top, List.choose id middle, List.choose id bottom

    /// The whole of a card, run together - for reading one at length rather than for drawing it.
    let printed card =
        let top, middle, bottom = boxes card
        top @ middle @ bottom

    /// The same three boxes, with as much taken out of each as this card has been silenced.
    ///
    /// **Face down it says nothing from any of them**, whatever is printed on it - so a board
    /// that printed the text of a face-down card would be handing over the one thing at this
    /// game that is worth something precisely because nobody can read it. Face up and
    /// **covered**, its middle and bottom are empty and its top is untouched. Uncovered, it says
    /// the lot.
    ///
    /// Which is not a screen's opinion about what to print. It is the same three cases the rules
    /// themselves make, off the same `Text` that makes them.
    let saying uncovered placed =
        if not (Placed.isFaceUp placed) then
            [], [], []
        else
            let top, middle, bottom = boxes placed.Card
            if uncovered then top, middle, bottom else top, [], []

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
        | Gave(who, given) -> $"{player who} gives {card given} away."
        | TookAtRandom(who, taken) -> $"{player who} takes {card taken} at random from the other hand."
        | PlayedFromDeck(who, played, where) ->
            $"{player who} plays {card played.Card} off the top of their own deck to {line where}."
        | Returned(who, back, where) -> $"{card back.Card} goes back to {player who}'s hand from {line where}."
        | Shifted(who, moved, from, into) -> $"{player who} shifts {card moved.Card} from {line from} to {line into}."
        | Drew(who, count) -> if count = 1 then $"{player who} draws a card." else $"{player who} draws {count}."
        | Fizzled(_, saying) -> $"{card saying} finds nothing to do."
        | Asked(who, saying) -> $"{card saying} asks {player who} to choose."
        | OverTheLimit(who, over) ->
            let many = if over = 1 then "a card" else $"{over} cards"
            $"{player who} is holding more than {Deck.HandSize}, so the check cache phase asks them to put {many} down."
        | Declined who -> $"{player who} says no, so nothing waiting on that happens."
        | StoppedCompiling who -> $"{player who} cannot compile when their turn next begins."
        | Showed(who, shown) -> $"{player who} shows {card shown} and puts it back."
        | ShowedHand(who, hand) ->
            let said = hand |> List.map Card.name |> String.concat ", "
            $"{player who} shows their hand: {said}."
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
        | AnOrder(_, offered) ->
            let said =
                offered
                |> List.map (fun each -> each |> List.map Protocol.key |> String.concat " ")
                |> String.concat ", or "

            $"say your {Protocol.Each} in a different order - {said}"
        | ALine(moving, offered) -> $"say where {card (Target.card moving)} goes - {lines offered}"
        | Whether inner -> $"say yes or no - {printing inner}"
        | ALineFor(_, offered) -> $"say which line - {lines offered}"
        | OneOf(first, second) -> $"say first or second - {printing first}, or {printing second}"

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
        | Forbidden(why, where) ->
            let said =
                match why with
                | NoPlayHere -> "a card of theirs says you cannot play there at all"
                | NoFaceDownHere -> "a card of theirs says you cannot play face down there"
                | OnlyFaceDown -> "a card of theirs says you can only play face down"

            $"You cannot play to {line where}: {said}."
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
        | Make(Choose TheFirst) -> "first"
        | Make(Choose TheSecond) -> "second"
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
