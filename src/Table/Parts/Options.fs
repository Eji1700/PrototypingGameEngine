namespace Prototyping.Table

open Prototyping.Common

module Options =

    type Page =
        | Audio
        | Video
        | Game
        | Keyboard

    [<NoComparison; NoEquality>]
    type Step =
        | Opening of Page
        | Changed of Palette
        | Drawn of string
        | Ringing of bool
        | Playing of string
        | Rebound of Keymap
        | Keep
        | Same
        | Done

    // The columns a row is described in: what a setting shows, what it is set to, and the label at
    // the head of a list in a note. Padded to a width rather than tabbed, so rows line up under one
    // another whatever the words in them.
    [<Literal>]
    let private Shows = 24

    [<Literal>]
    let private Holds = 10

    [<Literal>]
    let private Labels = 11

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
        |> List.mapi (fun i row -> (if i = 0 then "Colours:" else "").PadRight Labels + row)

    // What turning a row sends when it turns through a list of names: the next one along, coming
    // round at either end. Taking the row sends the next one too, so Enter on a row that turns does
    // what turning it on does rather than sending back what is already there.
    let private stepping said (all: string list) standing step =
        match List.length all with
        | 0 -> said standing
        | count ->
            let at =
                all |> List.tryFindIndex (fun name -> name = standing) |> Option.defaultValue 0

            said all[Keys.wrapped count at step]

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
        let ways = Counting.several "way" "ways"

        let game =
            match settles with
            | 0 -> "this game has nothing of its own to settle"
            | many -> $"{ways many} this game can be played"

        { Title = "Settings"
          Prose =
            [ "How the game is read rather than how it is played, so it is nobody's business but"
              "yours - it stays out of the record, and at a table over a network everyone reads in"
              "their own." ]
          Rows =
            [ Keys.sends (Keys.nth 0) "audio" "whether the table rings when your turn comes round" "audio"
              Keys.sends (Keys.nth 1) "video" "how the board is drawn, and what is drawn in what colour" "video"
              Keys.sends (Keys.nth 2) "game" game "game"
              Keys.sends (Keys.nth 3) "keys" "which letters steer: move, turn, page, seal" "keys"
              Keys.sends (Some '0') "done" "back to the menu" "done" ]
          Note =
            [ "Nothing on any of these pages is kept unless you say 'save', which keeps them all"
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
            | [ "keys" ]
            | [ "keyboard" ] -> Ok(Opening Keyboard)
            | word :: _ -> Error $"I don't know how to '{word}'. Say 'audio', 'video', 'game', 'keys', 'save', or 'done'."
            | [] -> Ok Same


    let audio (ringing: bool) : Keys.Screen =
        let said = if ringing then "on" else "off"
        let turn = stepping (sprintf "bell %s") [ "on"; "off" ] said

        { Title = "Settings - Audio"
          Prose =
            [ "The table rings when the turn comes round and nothing you did brought it round -"
              "so a game you are not watching can tell you it is waiting. It never rings for a"
              "move you made yourself. A game whose board makes a sound of its own rings for"
              "that too, and mute at the table silences one board without touching this." ]
          Rows =
            [ Keys.sends (Keys.nth 0) "bell" ("".PadRight Shows + said) (turn 1)
              |> Keys.turning turn
              Keys.sends (Keys.nth 1) "save" "keep all this, and open that way next time" "save"
              Keys.sends (Some '0') "done" "back to the settings" "done" ]
          Note =
            [ "Turning the row turns it over, and so does taking it; or say 'bell on' or"
              "'bell off' outright."
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
            let turn = stepping (sprintf "view %s") views drawn

            Keys.sends (Keys.nth 0) "drawn" ("".PadRight Shows + drawn) (turn 1)
            |> Keys.turning turn

        let standing at (slot: Slot) =
            let now = (Palette.inSlot slot palette).Name
            let turn = stepping (sprintf "%s %s" slot.Key) colours now

            Keys.sends (Keys.nth at) slot.Key (slot.Shows.PadRight Shows + now.PadRight Holds + slot.Draws) (turn 1)
            |> Keys.turning turn

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
            [ "Turning the one marked -> walks it through what it can be, and so does taking it."
              "Or name one outright: 'blue teal' for a colour, 'view rich' for a way of drawing."
              "" ]
            @ [ "Drawn:".PadRight Labels + String.concat ", " views ]
            @ offered
            @ [ ""
                "Plain draws in no colour but sets them all the same: they are carried along, and"
                "rich draws in them the moment you ask for it, as a browser does."
                ""
                "What is kept here is this game's own, except the way it is drawn - every game picks"
                "that up unless one of them saved its own." ]
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

            Keys.sends (Keys.nth at) name (now.PadRight Holds + says) $"plays {name}"

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
                  "Played:".PadRight Labels + String.concat ", " names ]
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


    let keyboard (keymap: Keymap) : Keys.Screen =
        let row at key =
            let word = Keymap.word key
            let holding keymap = string (Keymap.letter key keymap)

            Keys.types (Keys.nth at) word ((Keymap.says key).PadRight Shows + holding keymap) $"key {word} "
            |> Keys.turning (fun by -> $"key {word} {holding (Keymap.next key by keymap)}")

        let after = List.length Keymap.all

        { Title = "Settings - Keys"
          Prose =
            [ "Which letter does what while you walk a list, at the menus and at a board. The space"
              "bar, Enter, Escape, Backspace and the numbers do the same everywhere and are not"
              "moved here, so a list with every letter moved somewhere strange can still be left by"
              "its number." ]
          Rows =
            (Keymap.all |> List.mapi row)
            @ [ Keys.sends (Keys.nth after) "reset" "put the letters all back" "reset"
                Keys.sends (Keys.nth (after + 1)) "save" "keep all this, and open that way next time" "save"
                Keys.sends (Some '0') "done" "back to the settings" "done" ]
          Note =
            [ "Turning the one marked -> walks it on to the next letter nothing else has. Taking it"
              "writes the start of the line and waits for the letter; or say it outright, 'key up j'."
              ""
              "This one is not a game's own. Which letter does what is a fact about the hands at the"
              "keyboard, so it is asked once and every game picks it up." ]
          Backs = Some "done" }

    let chooseKeyboard (keymap: Keymap) (text: string) : Result<Step, string> =
        let words = Commands.lowered text
        let movable = Keymap.all |> List.map Keymap.word |> String.concat ", "

        match wayOut words with
        | Some out -> out
        | None ->
            match words with
            | [ "reset" ] -> Ok(Rebound Keymap.standard)
            | [ "key"; word; letter ] ->
                match Keymap.ofWord word with
                | Some key -> Keymap.rebind key letter keymap |> Result.map Rebound
                | None -> Error $"'{word}' is not a key that can be moved. There is {movable}."
            | "key" :: _ -> Error "Say 'key <what> <letter>', as in 'key up j'."
            | word :: _ -> Error $"I don't know how to '{word}'. Say 'key <what> <letter>', 'reset', 'save', or 'done'."
            | [] -> Ok Same
