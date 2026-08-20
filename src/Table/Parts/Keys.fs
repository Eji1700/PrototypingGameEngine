namespace TCModel.Table

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
        | Rubbed
        | Backed
        | Sent
        | Ignored

    /// What a keypress means. Once anything has been typed the letter keys are letters again, so
    /// w, a, s and d only steer a screen nobody is typing a line into.
    let pressed typing (key: ConsoleKeyInfo) =
        match key.Key with
        | ConsoleKey.Enter -> if typing then Sent else Picked
        | ConsoleKey.Backspace -> Rubbed
        | ConsoleKey.Escape -> Backed
        | ConsoleKey.UpArrow -> Moved -1
        | ConsoleKey.DownArrow
        | ConsoleKey.Tab -> Moved 1
        | ConsoleKey.LeftArrow -> if typing then Ignored else Turned -1
        | ConsoleKey.RightArrow -> if typing then Ignored else Turned 1
        | _ ->
            match key.KeyChar with
            | letter when typing -> if Char.IsControl letter then Ignored else Typed letter
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
        { Stack: (Screen * int) list
          Buffer: string }

    let standing screen at =
        { Stack = [ (screen, at) ]
          Buffer = "" }

    let facing standing = List.head standing.Stack

    let started standing = List.last standing.Stack |> snd

    let typing standing = standing.Buffer <> ""

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
            | Types text -> Steering { standing with Buffer = text }
            | Opens under ->
                Steering
                    { Stack = (under, 0) :: standing.Stack
                      Buffer = "" }

        // Backing out of the innermost screen; from the outermost there is nowhere to go, so it
        // answers with whatever line the screen said stands for leaving it, if any.
        let out () =
            match standing.Stack with
            | [ _ ] ->
                match showing.Backs with
                | Some line -> Answered line
                | None -> Steering standing
            | _ :: behind -> Steering { Stack = behind; Buffer = "" }
            | [] -> Steering standing

        let rubbed () =
            Steering
                { standing with
                    Buffer = standing.Buffer.Substring(0, standing.Buffer.Length - 1) }

        match press with
        | Ignored -> Steering standing
        | Typed letter ->
            Steering
                { standing with
                    Buffer = standing.Buffer + string letter }
        | Sent -> Answered standing.Buffer
        | Backed -> out ()
        | Rubbed -> if typing standing then rubbed () else out ()
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
