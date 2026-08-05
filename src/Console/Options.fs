namespace TCModel.Console

open System

/// The colour screen: what is drawn in what, and how a person changes it.
///
/// Pure, like the rest of the console layer. It says what the screen reads like and what a
/// typed line means, and leaves the reading and the writing to `Program`.
///
/// Nothing here paints anything. The screen is written in the board's own words - a stone's
/// letter, the arrow that marks whose turn it is, `(you)`, `dead` - and goes out through the
/// view like every other screen, which colours those words as it would on a board. So what
/// a player is shown while choosing is exactly what they will get once they have.
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
        |> List.mapi (fun i row -> sprintf "  %-11s%s" (if i = 0 then "Colours:" else "") row)

    let private standing palette (slot: Slot) =
        sprintf "    %-9s%-24s%-10s%s" slot.Says slot.Shows (slot.Of palette).Name slot.Draws

    let screen palette =
        String.concat
            Environment.NewLine
            ([ ""
               "=== Colours ==="
               ""
               "  Which colour is drawn for what. This is how the game is read rather than how it"
               "  is played, so it is nobody's business but yours - it stays out of the record, and"
               "  at a table over a network everyone reads in their own."
               "" ]
             @ (Palette.slots |> List.map (standing palette))
             @ [ ""
                 "    Say 'blue teal' to change one, 'reset' to put them all back, or 'done'."
                 "" ]
             @ offered
             @ [ ""
                 "  Only the rich view draws in colour. Set them from either - plain carries them"
                 "  along until you ask for rich, and then they are what it draws in."
                 "" ])

    /// A typed line as a step. Two words are a colour for something; the rest are the ways
    /// out.
    let choose palette (text: string) : Result<Step, string> =
        match Parse.words text |> List.map (fun word -> word.ToLowerInvariant()) with
        | [] -> Ok Same
        | [ "done" ]
        | [ "back" ]
        | [ "menu" ]
        | [ "q" ] -> Ok Done
        | [ "reset" ] -> Ok(Changed Palette.standard)
        | [ slot; colour ] -> Palette.set slot colour palette |> Result.map Changed
        | word :: _ -> Error $"I don't know how to '{word}'. Say '<what> <colour>', or 'done'."
