namespace TCModel.Table

module Options =

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

    [<NoComparison; NoEquality>]
    type Step =
        | Opening of Page
        | Changed of Palette
        | Drawn of string
        | Ringing of bool
        | Playing of string
        | Keep
        | Same
        | Done

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

    // What the left and right arrows send on a row that turns through a list of names: the next one
    // along, coming round at either end.
    let private stepping said (all: string list) standing step =
        match List.length all with
        | 0 -> said standing
        | count ->
            let at =
                all |> List.tryFindIndex (fun name -> name = standing) |> Option.defaultValue 0

            said all[((at + step) % count + count) % count]

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
        let words = Commands.lowered text

        match wayOut words with
        | Some out -> out
        | None ->
            match words with
            | [ "audio" ] -> Ok(Opening Audio)
            | [ "video" ]
            | [ "colours" ]
            | [ "colors" ] -> Ok(Opening Video)
            | [ "game" ]
            | [ "rules" ] -> Ok(Opening Game)
            | word :: _ -> Error $"I don't know how to '{word}'. Say 'audio', 'video', 'game', 'save', or 'done'."
            | [] -> Ok Same


    let audio (ringing: bool) : Keys.Screen =
        let said = if ringing then "on" else "off"

        { Title = "Settings - Audio"
          Prose =
            [ "The table rings when the turn comes round and nothing you did brought it round -"
              "so a game you are not watching can tell you it is waiting. It never rings for a"
              "move you made yourself. A game whose board makes a sound of its own rings for"
              "that too, and mute at the table silences one board without touching this." ]
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
        let words = Commands.lowered text

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

    let chooseVideo palette (text: string) : Result<Step, string> =
        let words = Commands.lowered text

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

    let chooseGame (ways: (string * string) list) (text: string) : Result<Step, string> =
        let names = ways |> List.map fst
        let words = Commands.lowered text
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
