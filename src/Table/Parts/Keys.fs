namespace Prototyping.Table

open System

module Keys =

    [<NoComparison; NoEquality>]
    type Pick =
        | Sends of string
        | Types of string
        | Opens of Screen

    and [<NoComparison; NoEquality>] Row =
        { Digit: char option

          Says: string
          Does: string
          Pick: Pick

          Turns: (int -> string) option }

    and [<NoComparison; NoEquality>] Screen =
        { Title: string
          Prose: string list
          Rows: Row list
          Note: string list

          Backs: string option }

    let sends digit says does line =
        { Digit = digit
          Says = says
          Does = does
          Pick = Sends line
          Turns = None }

    let opens digit says does screen =
        { Digit = digit
          Says = says
          Does = does
          Pick = Opens screen
          Turns = None }

    let turning turn row = { row with Turns = Some turn }

    let types digit says does text =
        { Digit = digit
          Says = says
          Does = does
          Pick = Types text
          Turns = None }

    // Rows are numbered from 1 as they are read, so the tenth is 0 and there is no eleventh.
    let nth n =
        if n > 9 then None else Some(char (int '0' + ((n + 1) % 10)))

    // Moving off either end comes round to the other. The doubled modulo is to bring a negative
    // remainder back round, which .NET's does not do.
    let moved by at (screen: Screen) =
        match List.length screen.Rows with
        | 0 -> 0
        | count -> ((at + by) % count + count) % count

    let byDigit digit (screen: Screen) =
        screen.Rows |> List.tryFind (fun row -> row.Digit = Some digit)

    [<Literal>]
    let private Marker = "->"

    let draw at (screen: Screen) =
        let column =
            match screen.Rows |> List.map (fun row -> String.length row.Says) with
            | [] -> 0
            | widths -> List.max widths

        let row index (row: Row) =
            let mark = if at = Some index then Marker else "  "
            let digit = row.Digit |> Option.map string |> Option.defaultValue " "

            (sprintf "  %s %s  %s  %s" mark digit (row.Says.PadRight column) row.Does).TrimEnd()

        let indented lines =
            match lines with
            | [] -> []
            | _ -> (lines |> List.map (fun line -> ("  " + (line: string)).TrimEnd())) @ [ "" ]

        String.concat
            Environment.NewLine
            ([ ""; $"=== {screen.Title} ==="; "" ]
             @ indented screen.Prose
             @ (screen.Rows |> List.mapi row)
             @ [ "" ]
             @ indented screen.Note)

    type Press =
        | Moved of by: int
        | Turned of by: int
        | Picked
        | Numbered of char
        | Typed of char

        /// Hand the keyboard to the prompt, with nothing typed into it yet.
        | Prompting

        | Rubbed
        | Backed
        | Sent
        | Ignored

    /// What a keypress means, which depends on whose the keyboard is.
    ///
    /// Steering, the letter keys are the letter keys: w, a, s and d walk the rows and a digit takes
    /// one. Typing, **every** printable key is a letter, those four included - which is the whole
    /// point of there being a mode at all. Reading the mode off "has anything been typed yet" is
    /// what this used to do, and it left every word beginning with one of those four impossible to
    /// type: the first press steered instead of starting the line, so there was never a line under
    /// way for the rest of it to belong to. `search` was untypeable at a game about searching.
    ///
    /// The space bar is what hands the keyboard over, since a line never usefully begins with one.
    /// Any other printable key that is not a steering key hands it over as well and is the first
    /// letter, so nothing about the old way of starting a line has been taken away.
    let pressed typing (key: ConsoleKeyInfo) =
        match key.Key with
        | ConsoleKey.Enter -> if typing then Sent else Picked
        | ConsoleKey.Backspace -> Rubbed
        | ConsoleKey.Escape -> Backed

        // The arrows are never ambiguous, so they steer whether or not a line is under way.
        | ConsoleKey.UpArrow -> Moved -1
        | ConsoleKey.DownArrow
        | ConsoleKey.Tab -> Moved 1
        | ConsoleKey.LeftArrow -> if typing then Ignored else Turned -1
        | ConsoleKey.RightArrow -> if typing then Ignored else Turned 1

        // Matched as a key as well as a character, because a space arrives as both: a terminal
        // sends ' ' with it and some send nothing at all.
        | ConsoleKey.Spacebar -> if typing then Typed ' ' else Prompting
        | _ ->
            match key.KeyChar with
            | letter when typing -> if Char.IsControl letter then Ignored else Typed letter
            | ' ' -> Prompting
            | 'w'
            | 'W' -> Moved -1
            | 's'
            | 'S' -> Moved 1
            | 'a'
            | 'A' -> Turned -1
            | 'd'
            | 'D' -> Turned 1
            | digit when Char.IsDigit digit -> Numbered digit
            | letter when Char.IsControl letter -> Ignored
            | letter -> Typed letter


    [<NoComparison; NoEquality>]
    type Standing =
        {
            Stack: (Screen * int) list
            Buffer: string

            /// Whose the keyboard is. Held rather than worked out from whether anything has been typed,
            /// so that a line may begin with one of the steering letters - and so that the prompt can
            /// be open and empty, which is what backspacing a line back to nothing leaves.
            Typing: bool
        }

    let standing screen at =
        { Stack = [ (screen, at) ]
          Buffer = ""
          Typing = false }

    let facing standing = List.head standing.Stack

    let started standing = List.last standing.Stack |> snd

    let typing standing = standing.Typing

    [<NoComparison; NoEquality>]
    type Answer =
        | Steering of Standing
        | Answered of string

    let answer press standing =
        let showing, at = facing standing
        let here () = List.tryItem at showing.Rows

        let taking (row: Row) =
            match row.Pick with
            | Sends line -> Answered line

            // A row that writes the beginning of a line and waits hands the keyboard over as well,
            // or the rest of what it asked for could not be typed.
            | Types text ->
                Steering
                    { standing with
                        Buffer = text
                        Typing = true }
            | Opens under ->
                Steering
                    { Stack = (under, 0) :: standing.Stack
                      Buffer = ""
                      Typing = false }

        // Backing out of the innermost screen; from the outermost there is nowhere to go, so it
        // answers with whatever line the screen said stands for leaving it, if any.
        let out () =
            match standing.Stack with
            | [ _ ] ->
                match showing.Backs with
                | Some line -> Answered line
                | None -> Steering standing
            | _ :: behind ->
                Steering
                    { Stack = behind
                      Buffer = ""
                      Typing = false }
            | [] -> Steering standing

        /// Out of the prompt and back to the rows, with whatever was half-typed thrown away.
        let dropped () =
            Steering
                { standing with
                    Buffer = ""
                    Typing = false }

        let rubbed () =
            Steering
                { standing with
                    Buffer = standing.Buffer.Substring(0, standing.Buffer.Length - 1) }

        match press with
        | Ignored -> Steering standing
        | Prompting -> Steering { standing with Typing = true }
        | Typed letter ->
            Steering
                { standing with
                    Buffer = standing.Buffer + string letter
                    Typing = true }
        | Sent -> Answered standing.Buffer

        // Escape leaves the prompt before it leaves the screen: somebody half-way through a line
        // who changed their mind wants the line gone, not the screen.
        | Backed -> if typing standing then dropped () else out ()
        | Rubbed ->
            if not (typing standing) then out ()
            elif standing.Buffer = "" then dropped ()
            else rubbed ()
        | Moved by ->
            Steering
                { standing with
                    Stack = (showing, moved by at showing) :: List.tail standing.Stack }
        | Numbered digit ->
            match byDigit digit showing with
            | Some row -> taking row
            | None -> Steering standing
        | Picked ->
            match here () with
            | Some row -> taking row
            | None -> Steering standing

        // Right on a row that has nothing to turn through takes it, so a screen of plain rows can
        // still be walked with one hand on the arrows.
        | Turned by ->
            match
                here ()
                |> Option.bind (fun row -> row.Turns |> Option.map (fun turn -> row, turn))
            with
            | Some(_, turn) -> Answered(turn by)
            | None when by > 0 ->
                match here () with
                | Some row -> taking row
                | None -> Steering standing
            | None -> out ()
