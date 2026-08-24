namespace Prototyping.Compile

open Prototyping.Engine

module Words =

    let player seat = $"Player {PlayerId.value seat}"

    let seated yours seat =
        player seat + (if yours then " (you)" else "")

    let protocol = Protocol.name

    let card = Card.name

    let line (n: int) = $"line {n}"

    /// One of these lines, for a list somebody is choosing from.
    let lines =
        function
        | [] -> "no line at all"
        | [ only ] -> line only
        | many ->
            let said = many |> List.map line

            String.concat ", " (List.truncate (List.length said - 1) said)
            + " or "
            + List.last said

    /// All of these lines, for a list of what is happening to every one of them.
    let everyLine =
        function
        | [] -> "no line at all"
        | [ only ] -> line only
        | many ->
            let said = many |> List.map line

            String.concat ", " (List.truncate (List.length said - 1) said)
            + " and "
            + List.last said

    let placed card =
        match card.Face with
        | FaceUp -> Card.name card.Card
        | FaceDown -> $"[{Placed.FaceDownValue}] {Card.name card.Card}"

    let faceless card =
        match card.Face with
        | FaceUp -> Card.name card.Card
        | FaceDown -> $"[{Placed.FaceDownValue}]"

    let hidden = "hidden"

    let order protocols =
        protocols |> List.map protocol |> String.concat ", "

    let ending =
        function
        | Won who -> $"{player who} has compiled all {Protocol.Each} of their protocols"
        | Abandoned who -> $"{player who} walked away"


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

                " worth "
                + String.concat ", " (List.truncate (List.length said - 1) said)
                + " or "
                + List.last said

        let where =
            match selector.Where with
            | ThisLine -> " in this line"
            | OtherLines -> " in another line"
            | AnyLine
            | ToOrFromHere -> ""

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
        | InAChosenLine inner -> (printing inner) + ", in a line of your choosing"
        | InEachOtherLine inner -> (printing inner) + ", in each other line"
        | InEachLineHolding inner -> (printing inner) + ", in each line where you have a card"
        | InAChosenLineOf(atLeast, inner) ->
            (printing inner)
            + $", in another line of your choosing with {atLeast} or more cards"
        | Every inner -> (printing inner).Replace(" any ", " every ").Replace(" your ", " every ").Replace(" their ", " every ")
        | IfYouDo(first, rest) ->
            let after = rest |> List.map printing |> String.concat ", then "
            $"{printing first}. If you do, {after}"
        | IfCovering rest ->
            "if this card is covering a card, "
            + (rest |> List.map printing |> String.concat ", then ")
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

    let private capital (text: string) =
        if text = "" then text else string (System.Char.ToUpperInvariant text[0]) + text.Substring 1

    let private sentence said =
        match said with
        | [] -> None
        | said -> Some(said |> List.map (fun one -> capital one + ".") |> String.concat " ")

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

    let printed card =
        let top, middle, bottom = boxes card
        top @ middle @ bottom

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

    let asking =
        function
        | TheDraft -> "the draft is still going: say a protocol to take it"
        | TheProtocols -> $"the protocols are being set against the lines: say your {Protocol.Each} in the order you want them"
        | ThePlay -> "cards are being played: say a card and a line, like 'fire-3 2'"
        | AChoice -> "a card is waiting on somebody to choose"
        | Nothing -> "the game is over"

    let choices cards =
        cards |> List.map Card.name |> String.concat ", "

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

    let wantingSeenBy seat asked =
        let hidden =
            function
            | InHand(whose, _) -> whose <> seat
            | OnTable _ -> false

        match asked with
        | ACard(_, targets) when targets |> List.exists hidden ->
            let held = targets |> List.filter hidden |> List.length
            let rest = targets |> List.filter (hidden >> not) |> List.map Target.card

            match rest with
            | [] -> $"they say which of the {held} they are holding"
            | rest -> $"they say one of {choices rest}, or one of the {held} they are holding"
        | asked -> wanting asked

    let waiting seat step =
        match step with
        | Run(command, source) -> $"{card source.Saying}: {printing command}."
        | Repeating(command, source, _) -> $"{card source.Saying}: {printing command}, again if you want it."
        | Gate(commands, source) ->
            let rest = commands |> List.map printing |> String.concat ", then "
            $"{card source.Saying}: if that did anything, {rest}."
        | Ask question ->
            let who = if question.Chooser = seat then "you" else player question.Chooser
            $"waiting on {who} - {wantingSeenBy seat question.Wanting}."
        | Placing(who, placed, where, from) ->
            let coming =
                match from with
                | Some was -> $"from {line was}"
                | None -> "from hand"

            $"{player who}'s {card placed.Card} lands on {line where}, {coming}."
        | Turning(who, placed, where) -> $"{player who}'s {card placed.Card} on {line where} is turned over."
        | Escaping wiped -> $"anything in {everyLine wiped} that can get out of a compile does it now."
        | Compiling [ only ] -> $"{line only} compiles."
        | Compiling wiped -> $"{everyLine wiped} compile."
        | Refreshing -> "the hand goes down and a new one comes up."
        | Trimming -> $"any hand over {Deck.HandSize} comes back down to it."
        | Opening -> "the start commands of everything face up and uncovered."
        | Closing -> "the end commands of everything face up and uncovered."
        | BeginTurn -> "the turn begins: the control component, and whatever compiles."
        | EndTurn -> "the turn is handed on."

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
                | couldGo ->
                    $"{protocol wanted.Protocol} is on {lines couldGo} - or play it face down anywhere, for {Placed.FaceDownValue}."

            $"{card wanted} cannot go face up on {line said}. {where}"
        | AnswerFirst asked -> $"The game is waiting on an answer, and nothing else can happen until it comes: {wanting asked}."
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

    let command =
        Msg.written (function
            | Take taken -> $"draft {Protocol.key taken}"
            | Arrange protocols ->
                let said = protocols |> List.map Protocol.key |> String.concat " "
                $"arrange {said}"
            | Play(played, where, FaceUp) -> $"play {Card.key played} {where}"
            | Play(played, where, FaceDown) -> $"play {Card.key played} {where} down"
            | Refresh -> "refresh"
            | Choose(TheCard chosen) -> $"choose {Card.key chosen}"
            | Choose(TheLine line) -> $"choose line {line}"
            | Choose Yes -> "yes"
            | Choose No -> "no"
            | Choose TheFirst -> "first"
            | Choose TheSecond -> "second"
            | Resign -> "resign")

    let said =
        function
        | Happened e -> event e
        | Refused r -> rejection r

    let saidTo seat notice =
        match notice with
        | Happened(Arranged(who, _)) when who <> seat ->
            $"{player who} sets their {Protocol.Each} protocols against the lines, face down."
        | Happened(Played(who, played, where)) when who <> seat && not (Placed.isFaceUp played) ->
            $"{player who} plays a card face down to {line where}, for {Placed.FaceDownValue}."
        | Happened(Took(who, _)) when who <> seat -> $"{player who} takes a card from the top of your deck."
        | Refused(AnswerFirst asked) ->
            $"The game is waiting on an answer, and nothing else can happen until it comes: {wantingSeenBy seat asked}."
        | Refused(NotOnOffer asked) -> $"That is not one of the things being offered: {wantingSeenBy seat asked}."
        | notice -> said notice
