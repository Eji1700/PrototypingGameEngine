namespace TCModel.Table

/// The settings screen: how a board is drawn, what is drawn in what colour, and whether to
/// be handed the same answers again next time.
///
/// Pure, like the rest of this layer. It says what the screen reads like and what a typed
/// line means, and leaves the reading and the writing to `Program` - keeping the settings is
/// a thing this screen *asks for* and never does, which is what lets the whole of it be
/// checked without a disk.
///
/// Nothing here paints anything. The screen is written in the board's own words - whatever
/// the game said its slots show - and goes out through the view like every other screen,
/// which colours those words as it would on a board. So what a player is shown while
/// choosing is exactly what they will get once they have.
///
/// It knows nothing about what is being played. Which slots there are came from the game and
/// travel in the palette, and the ways of drawing come in as a list of names, so this screen
/// is the same screen at every game and the words on it are never the same twice.
module Options =

    /// What a typed line at the settings screen came to.
    [<NoComparison; NoEquality>]
    type Step =
        | Changed of Palette
        /// Read it this way from here on. A name rather than a view, because which views
        /// there are is the game's answer and this screen has never met the game - so it is
        /// checked where the view is built, and refused there in the same words.
        | Drawn of string
        /// Keep all of it for next time. What "all of it" is, is whatever the screen is
        /// showing when this is said, so there is nothing here to remember and nothing that
        /// can be kept which was not on the screen a moment before.
        | Keep
        /// Nothing was typed, so the screen simply asks again.
        | Same
        /// Back to the menu, in whatever has been settled on.
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
    /// The two rows that turn - the view and every colour - are the same shape of question
    /// asked twice, so they are answered once: where the thing standing now is in the list,
    /// one step on from there, and round the ends. Nothing is remembered between presses,
    /// because where it stands now is read off what the screen was built from.
    let private stepping said (all: string list) standing step =
        match List.length all with
        | 0 -> said standing
        | count ->
            let at =
                all |> List.tryFindIndex (fun name -> name = standing) |> Option.defaultValue 0

            said all[((at + step) % count + count) % count]

    /// The screen, in whatever has been settled on so far.
    ///
    /// `views` is what this game can be drawn as and `drawn` is which of them is doing the
    /// drawing, both handed in - a screen that asked the game would be a screen that knew
    /// there was one.
    ///
    /// Left and right walk one row through what it has to choose from. There is nothing to
    /// remember between presses: what a slot is drawn in now is in the palette this was built
    /// from, so the step is read off that and the line says the whole of the change - and
    /// because the screen comes straight back in the new palette, the sample beside the name
    /// changes under the cursor as it is walked.
    let screen (views: string list) (drawn: string) palette : Keys.Screen =
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

        { Title = "Settings"
          Prose =
            [ "How the board is drawn, and what is drawn in what colour. This is how the game is"
              "read rather than how it is played, so it is nobody's business but yours - it stays"
              "out of the record, and at a table over a network everyone reads in their own." ]
          Rows =
            [ drawing ]
            @ (slots |> List.mapi (fun at slot -> standing (at + 1) slot))
            @ [ Keys.sends (Keys.nth after) "reset" "put the colours all back" "reset"
                Keys.sends (Keys.nth (after + 1)) "save" "keep all this, and open that way next time" "save"
                Keys.sends (Some '0') "done" "back to the menu" "done" ]
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
                "Nothing here is kept unless you say 'save'. What is kept is this game's own, except"
                "the way it is drawn, which every game picks up unless it was saved at one of them." ]
          Backs = Some "done" }

    /// A typed line as a step. A view has its own word, two words are a colour for something,
    /// and the rest are the ways out.
    ///
    /// `view` is read before the two-word case below it and has to be: 'view rich' is two
    /// words like any other, and read as a colour it would come back saying there is nothing
    /// called 'view' to colour - which is true, and no help at all to somebody who typed
    /// exactly what the screen told them to.
    let choose palette (text: string) : Result<Step, string> =
        match Commands.words text |> List.map (fun word -> word.ToLowerInvariant()) with
        | [] -> Ok Same
        | [ "done" ]
        | [ "back" ]
        | [ "menu" ]
        | [ "q" ] -> Ok Done
        | [ "save" ]
        | [ "keep" ] -> Ok Keep
        | [ "reset" ] -> Ok(Changed(Palette.reset palette))
        | [ "view"; name ] -> Ok(Drawn name)
        | [ "view" ] -> Error "Say 'view <name>', for one of the ways this game can be drawn."
        | [ slot; colour ] -> Palette.set slot colour palette |> Result.map Changed
        | word :: _ -> Error $"I don't know how to '{word}'. Say '<what> <colour>', 'view <name>', 'save', or 'done'."
