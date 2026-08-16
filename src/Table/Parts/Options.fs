namespace TCModel.Table

/// The settings: a short menu, and a page behind each row of it.
///
/// There are three pages because there are three kinds of question, and telling them apart is
/// most of what this file is for. **Video** is how a board reaches your eyes - the way it is
/// drawn and what colour everything is in. **Audio** is how it reaches your ears, which today
/// is one bell and a question about whether you want it. **Game** is whatever this particular
/// game lets you settle about itself, which for most games is nothing at all and for one is
/// which of its rules are in play.
///
/// The split is not decoration. Before it there was one screen with a view row and a colour
/// row on it, and every new kind of question would have gone on the end of the same list until
/// it was a list nobody could read. A menu with pages behind it has room, and - more to the
/// point - it has somewhere obvious to put the next thing, which is the whole of what "settled
/// in one place" is worth.
///
/// Pure, like the rest of this layer. It says what the screens read like and what a typed line
/// means, and leaves the reading and the writing to whoever is running them - keeping the
/// settings is a thing these screens *ask for* and never do, which is what lets the whole of
/// it be checked without a disk.
///
/// Nothing here paints anything. The screens are written in the board's own words - whatever
/// the game said its slots show - and go out through the view like every other screen, which
/// colours those words as it would on a board. So what a player is shown while choosing is
/// exactly what they will get once they have.
///
/// And nothing here has met a game. Which slots there are came from the game and travel in the
/// palette; the ways of drawing and the ways of playing come in as lists of names. So these
/// are the same screens at every game and the words on them are never the same twice.
module Options =

    /// Which page is being read, and the word that opens it.
    ///
    /// A type rather than three functions with three names, because the menu has to be able to
    /// hand one back without knowing what is behind it, and because the compiler should be the
    /// thing that notices when a fourth page is added and somewhere has not been taught to open
    /// it. `Audio` is first for the reason it is first on the screen: it is the shortest page,
    /// and a menu that opens with its longest entry reads like a list with an afterthought.
    type Page =
        | Audio
        | Video
        | Game

    module Page =

        let word page =
            match page with
            | Audio -> "audio"
            | Video -> "video"
            | Game -> "game"

    /// What a typed line at any of these screens came to.
    ///
    /// One type for all four screens rather than one apiece, and that is worth a sentence: the
    /// three ways out - keep it, ask again, go back - are the same three at every page, and
    /// three copies of them would be three places to fix the day a fourth is wanted. What only
    /// one page can say, only one page returns; nothing checks that, and nothing has to, because
    /// the page that would have to return it is the only one whose reader can build it.
    [<NoComparison; NoEquality>]
    type Step =
        /// From the menu: open that page.
        | Opening of Page
        /// Video: this colour, for that slot.
        | Changed of Palette
        /// Video: read it this way from here on. A name rather than a view, because which views
        /// there are is the game's answer and this screen has never met the game - so it is
        /// checked where the view is built, and refused there in the same words.
        | Drawn of string
        /// Audio: ring, or do not.
        | Ringing of bool
        /// Game: play it this way. A name, for the same reason `Drawn` carries one.
        | Playing of string
        /// Keep all of it for next time. What "all of it" is, is whatever the screens are
        /// showing when this is said, so there is nothing here to remember and nothing that
        /// can be kept which was not on a screen a moment before.
        | Keep
        /// Nothing was typed, so the screen simply asks again.
        | Same
        /// Back one: to the menu from a page, and to the game's menu from the menu.
        | Done

    /// The colours on offer, a handful to a line, because nineteen names in a row is not a
    /// list anybody reads. However many there are - a colours file may have made it more.
    let private offered =
        let rec inRows names =
            match names with
            | [] -> []
            | _ ->
                let row = names |> List.truncate 5
                (row |> String.concat ", ") :: inRows (names |> List.skip (List.length row))

        Palette.shades
        |> List.map (fun shade -> shade.Name)
        |> inRows
        |> List.mapi (fun i row -> sprintf "%-11s%s" (if i = 0 then "Colours:" else "") row)

    /// One thing walked one step along a list it is somewhere in, as the line that says so.
    ///
    /// Every row that turns - the view, the bell, each colour, the way of playing - is the same
    /// shape of question asked over and over, so it is answered once: where the thing standing
    /// now is in the list, one step on from there, and round the ends. Nothing is remembered
    /// between presses, because where it stands now is read off what the screen was built from.
    let private stepping said (all: string list) standing step =
        match List.length all with
        | 0 -> said standing
        | count ->
            let at =
                all |> List.tryFindIndex (fun name -> name = standing) |> Option.defaultValue 0

            said all[((at + step) % count + count) % count]

    /// The three ways out, which every page below reads the same way. Read before anything a
    /// page invented, so no page can quietly redefine `done`.
    let private wayOut words =
        match words with
        | [ "done" ]
        | [ "back" ]
        | [ "menu" ]
        | [ "q" ] -> Some(Ok Done)
        | [ "save" ]
        | [ "keep" ] -> Some(Ok Keep)
        | [] -> Some(Ok Same)
        | _ -> None

    let private lower (text: string) =
        Commands.words text |> List.map (fun word -> word.ToLowerInvariant())

    // --- the menu ------------------------------------------------------------------------------

    /// What each page is for, in one line, said here rather than on the page itself: a menu row
    /// that does not say what is behind it is a row you have to open to find out about.
    ///
    /// The Game row says how many choices this game is offering, because "nothing to settle" is
    /// worth knowing from the menu. Most games say nothing, and being told so on the row is
    /// better than being shown an empty page.
    let screen (settles: int) : Keys.Screen =
        let game =
            match settles with
            | 0 -> "this game has nothing of its own to settle"
            | 1 -> "one way this game can be played"
            | many -> $"{many} ways this game can be played"

        { Title = "Settings"
          Prose =
            [ "How the game is read rather than how it is played, so it is nobody's business but"
              "yours - it stays out of the record, and at a table over a network everyone reads in"
              "their own." ]
          Rows =
            [ Keys.sends (Keys.nth 0) "audio" "whether the table rings when your turn comes round" "audio"
              Keys.sends (Keys.nth 1) "video" "how the board is drawn, and what is drawn in what colour" "video"
              Keys.sends (Keys.nth 2) "game" game "game"
              Keys.sends (Some '0') "done" "back to the menu" "done" ]
          Note =
            [ ""
              "Nothing on any of these pages is kept unless you say 'save', which keeps all three"
              "at once - so a page you only looked at is a page that changed nothing." ]
          Backs = Some "done" }

    let choose (text: string) : Result<Step, string> =
        let words = lower text

        match wayOut words with
        | Some out -> out
        | None ->
            match words with
            | [ "audio" ] -> Ok(Opening Audio)
            | [ "video" ]
            // The page was called the settings screen when it was the only one, and a word
            // somebody has in their fingers is a word worth still taking.
            | [ "colours" ]
            | [ "colors" ] -> Ok(Opening Video)
            | [ "game" ]
            | [ "rules" ] -> Ok(Opening Game)
            | word :: _ -> Error $"I don't know how to '{word}'. Say 'audio', 'video', 'game', 'save', or 'done'."
            | [] -> Ok Same

    // --- audio ---------------------------------------------------------------------------------

    /// One row, and it is a real one rather than a place kept for later: the table already
    /// rings when the turn comes round and nobody asked for it, and until now there was no way
    /// to say you would rather it did not.
    ///
    /// There will be more here. What there will not be is a second place to put it.
    let audio (ringing: bool) : Keys.Screen =
        let said = if ringing then "on" else "off"

        { Title = "Settings - Audio"
          Prose =
            [ "The table rings when the turn comes round and nothing you did brought it round -"
              "so a game you are not watching can tell you it is waiting. It never rings for a"
              "move you made yourself." ]
          Rows =
            [ Keys.sends (Keys.nth 0) "bell" (sprintf "%-24s%s" "" said) $"bell {said}"
              |> Keys.turning (stepping (sprintf "bell %s") [ "on"; "off" ] said)
              Keys.sends (Keys.nth 1) "save" "keep all this, and open that way next time" "save"
              Keys.sends (Some '0') "done" "back to the settings" "done" ]
          Note =
            [ "Left and right turn it over, or say 'bell on' or 'bell off' outright."
              ""
              "This one is not a game's own. A bell is a fact about the room you are sitting in,"
              "so it is asked once and every game picks it up." ]
          Backs = Some "done" }

    let chooseAudio (text: string) : Result<Step, string> =
        let words = lower text

        match wayOut words with
        | Some out -> out
        | None ->
            match words with
            | [ "bell"; "on" ] -> Ok(Ringing true)
            | [ "bell"; "off" ] -> Ok(Ringing false)
            | [ "bell"; said ] -> Error $"'{said}' is not something a bell can be. Say 'bell on' or 'bell off'."
            | [ "bell" ] -> Error "Say 'bell on' or 'bell off'."
            | word :: _ -> Error $"I don't know how to '{word}'. Say 'bell on', 'bell off', 'save', or 'done'."
            | [] -> Ok Same

    // --- video ---------------------------------------------------------------------------------

    /// The page this whole file used to be.
    ///
    /// `views` is what this game can be drawn as and `drawn` is which of them is doing the
    /// drawing, both handed in - a screen that asked the game would be a screen that knew there
    /// was one.
    ///
    /// Left and right walk one row through what it has to choose from. There is nothing to
    /// remember between presses: what a slot is drawn in now is in the palette this was built
    /// from, so the step is read off that and the line says the whole of the change - and
    /// because the screen comes straight back in the new palette, the sample beside the name
    /// changes under the cursor as it is walked.
    let video (views: string list) (drawn: string) palette : Keys.Screen =
        let slots = Palette.slots palette
        let colours = Palette.shades |> List.map (fun shade -> shade.Name)

        let drawing =
            Keys.sends (Keys.nth 0) "drawn" (sprintf "%-24s%s" "" drawn) $"view {drawn}"
            |> Keys.turning (stepping (sprintf "view %s") views drawn)

        let standing at (slot: Slot) =
            let now = (Palette.inSlot slot palette).Name

            Keys.sends
                (Keys.nth at)
                slot.Key
                (sprintf "%-24s%-10s%s" slot.Shows now slot.Draws)
                (stepping (sprintf "%s %s" slot.Key) colours now 1)
            |> Keys.turning (stepping (sprintf "%s %s" slot.Key) colours now)

        let after = List.length slots + 1

        { Title = "Settings - Video"
          Prose =
            [ "How the board is drawn, and what is drawn in what colour. This is this game's own:"
              "a game of stones colours three factions and a game of nine squares colours two"
              "marks, and there is no one list of them to keep." ]
          Rows =
            [ drawing ]
            @ (slots |> List.mapi (fun at slot -> standing (at + 1) slot))
            @ [ Keys.sends (Keys.nth after) "reset" "put the colours all back" "reset"
                Keys.sends (Keys.nth (after + 1)) "save" "keep all this, and open that way next time" "save"
                Keys.sends (Some '0') "done" "back to the settings" "done" ]
          Note =
            [ "Left and right walk the one marked -> through what it can be, or say 'blue teal' to"
              "name a colour outright, or 'view rich' to name a way of drawing."
              "" ]
            @ [ sprintf "%-11s%s" "Drawn:" (String.concat ", " views) ]
            @ offered
            @ [ ""
                "Only the rich view draws in colour. Set them from either - plain carries them along"
                "until you ask for rich, and then they are what it draws in."
                ""
                "What is kept here is this game's own, except the way it is drawn, which every game"
                "picks up unless it was saved at one of them." ]
          Backs = Some "done" }

    /// A typed line as a step. A view has its own word, two words are a colour for something,
    /// and the rest are the ways out.
    ///
    /// `view` is read before the two-word case below it and has to be: 'view rich' is two
    /// words like any other, and read as a colour it would come back saying there is nothing
    /// called 'view' to colour - which is true, and no help at all to somebody who typed
    /// exactly what the screen told them to.
    let chooseVideo palette (text: string) : Result<Step, string> =
        let words = lower text

        match wayOut words with
        | Some out -> out
        | None ->
            match words with
            | [ "reset" ] -> Ok(Changed(Palette.reset palette))
            | [ "view"; name ] -> Ok(Drawn name)
            | [ "view" ] -> Error "Say 'view <name>', for one of the ways this game can be drawn."
            | [ slot; colour ] -> Palette.set slot colour palette |> Result.map Changed
            | word :: _ -> Error $"I don't know how to '{word}'. Say '<what> <colour>', 'view <name>', 'save', or 'done'."
            | [] -> Ok Same

    // --- game ----------------------------------------------------------------------------------

    /// The ways this game can be played, and which of them is being played now.
    ///
    /// `ways` is a name and a sentence apiece, handed in like everything else here: a screen
    /// that could ask the game which ways there are would be a screen that had met one.
    ///
    /// This is the one page whose answer is not merely about reading. A game played with an
    /// optional rule in it is a different game, and the record says so - each way has a name of
    /// its own and that name is what goes in the deal line - so a record still replays into
    /// exactly the game it came out of, whatever this page was last left saying. What is
    /// settled here is what a *new* game is dealt as, and nothing about an old one.
    let game (ways: (string * string) list) (playing: string) : Keys.Screen =
        let names = ways |> List.map fst

        let standing at (name, says) =
            let now = if name = playing then "in play" else ""

            Keys.sends (Keys.nth at) name (sprintf "%-10s%s" now says) $"plays {name}"

        { Title = "Settings - Game"
          Prose =
            match ways with
            | []
            | [ _ ] ->
                [ "This game is played one way, so there is nothing here to settle. A game with an"
                  "optional rule in it would offer it from this page." ]
            | _ ->
                [ "Which of this game's ways of being played a new game is dealt as. Each is a game"
                  "in its own right and says so in the record it writes, so a game already saved is"
                  "taken back up exactly as it was played whatever is settled here." ]
          Rows =
            (ways |> List.mapi (fun at way -> standing at way))
            @ [ Keys.sends (Keys.nth (List.length ways)) "save" "keep all this, and open that way next time" "save"
                Keys.sends (Some '0') "done" "back to the settings" "done" ]
          Note =
            match names with
            | []
            | [ _ ] -> []
            | _ ->
                [ "Say the name of one outright, or 'plays <name>'."
                  ""
                  sprintf "%-11s%s" "Played:" (String.concat ", " names) ]
          Backs = Some "done" }

    /// A name on its own is that way, because that is what the rows send; `plays <name>` says
    /// the same thing and is what the settings file holds, so the file and the screen go on
    /// taking the same line.
    let chooseGame (ways: (string * string) list) (text: string) : Result<Step, string> =
        let names = ways |> List.map fst
        let words = lower text
        let listed = String.concat ", " names

        let named name =
            if List.contains name names then
                Ok(Playing name)
            else
                Error $"'{name}' is not a way this game can be played. There is {listed}."

        match wayOut words with
        | Some out -> out
        | None ->
            match words with
            | [ "plays"; name ] -> named name
            | [ "plays" ] -> Error $"Say 'plays <name>', for one of {listed}."
            | [ name ] -> named name
            | word :: _ -> Error $"I don't know how to '{word}'. Say 'plays <name>', 'save', or 'done'."
            | [] -> Ok Same
