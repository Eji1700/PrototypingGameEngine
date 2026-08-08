namespace TCModel.Table

/// The colour screen: what is drawn in what, and how a person changes it.
///
/// Pure, like the rest of this layer. It says what the screen reads like and what a typed
/// line means, and leaves the reading and the writing to `Program`.
///
/// Nothing here paints anything. The screen is written in the board's own words - whatever
/// the game said its slots show - and goes out through the view like every other screen,
/// which colours those words as it would on a board. So what a player is shown while
/// choosing is exactly what they will get once they have.
///
/// It knows nothing about what is being played. Which slots there are came from the game and
/// travel in the palette, so this screen is the same screen at every game and the words on it
/// are never the same twice.
module Options =

    /// What a typed line at the colour screen came to.
    [<NoComparison; NoEquality>]
    type Step =
        | Changed of Palette
        /// Nothing was typed, so the screen simply asks again.
        | Same
        /// Back to the menu, in whatever colours have been settled on.
        | Done

    /// The colours on offer, a handful to a line, because nineteen names in a row is not a
    /// list anybody reads.
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

    /// The screen, in whatever colours have been settled on so far.
    ///
    /// Left and right walk one slot through the nineteen. There is nothing to remember
    /// between presses: what a slot is drawn in now is in the palette this was built from,
    /// so the step is read off that and the line says the whole of the change - and because
    /// the screen comes straight back in the new palette, the sample beside the name changes
    /// under the cursor as it is walked.
    let screen palette : Keys.Screen =
        let slots = Palette.slots palette

        let walking (slot: Slot) step =
            let at =
                Palette.shades
                |> List.tryFindIndex (fun shade -> shade.Name = (Palette.inSlot slot palette).Name)
                |> Option.defaultValue 0

            let count = List.length Palette.shades

            $"{slot.Key} {Palette.shades[((at + step) % count + count) % count].Name}"

        let standing at (slot: Slot) =
            Keys.sends
                (Keys.nth at)
                slot.Key
                (sprintf "%-24s%-10s%s" slot.Shows (Palette.inSlot slot palette).Name slot.Draws)
                (walking slot 1)
            |> Keys.turning (walking slot)

        { Title = "Colours"
          Prose =
            [ "Which colour is drawn for what. This is how the game is read rather than how it is"
              "played, so it is nobody's business but yours - it stays out of the record, and at a"
              "table over a network everyone reads in their own." ]
          Rows =
            (slots |> List.mapi standing)
            @ [ Keys.sends (Keys.nth (List.length slots)) "reset" "put them all back" "reset"
                Keys.sends (Some '0') "done" "back to the menu" "done" ]
          Note =
            [ "Left and right walk the one marked -> through the colours, or say 'blue teal' to"
              "name one outright."
              "" ]
            @ offered
            @ [ ""
                "Only the rich view draws in colour. Set them from either - plain carries them along"
                "until you ask for rich, and then they are what it draws in." ]
          Backs = Some "done" }

    /// A typed line as a step. Two words are a colour for something; the rest are the ways
    /// out.
    let choose palette (text: string) : Result<Step, string> =
        match Commands.words text |> List.map (fun word -> word.ToLowerInvariant()) with
        | [] -> Ok Same
        | [ "done" ]
        | [ "back" ]
        | [ "menu" ]
        | [ "q" ] -> Ok Done
        | [ "reset" ] -> Ok(Changed(Palette.reset palette))
        | [ slot; colour ] -> Palette.set slot colour palette |> Result.map Changed
        | word :: _ -> Error $"I don't know how to '{word}'. Say '<what> <colour>', or 'done'."
