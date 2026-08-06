namespace TCModel.Console

open System

/// The screens a person picks from rather than types at, and what a key press means on one.
///
/// A row is not a second way of saying something. Every row stands for a line - the same
/// line somebody who knew the words would have typed - and choosing it hands that line to
/// the very reader the typing would have reached. So the arrows are a way of typing, there
/// is one grammar rather than two, and a row that came to stand for a line the reader does
/// not understand is something a check can catch rather than something a player finds.
///
/// Pure, like the rest of the console layer: it says what a screen reads like and what a
/// key comes to, and leaves the reading and the writing to `Program`.
module Keys =

    /// What choosing a row means.
    [<NoComparison; NoEquality>]
    type Pick =
        /// Hand this line over, as though it had been typed.
        | Sends of string
        /// Put this much in the line and wait. What is left is a file, an address - something
        /// no list could hold - so the row types the part it knows and the person finishes it.
        | Types of string
        /// A list of its own, which comes back here once it has been answered.
        | Opens of Screen

    /// One thing on offer.
    and [<NoComparison; NoEquality>] Row =
        {
            /// The key that picks it outright. Ten digits is all there is, so a long list
            /// runs out of them and the rest are reached with the arrows.
            Digit: char option

            Says: string
            Does: string
            Pick: Pick

            /// What left and right do to it, where there is anything to turn: the step is -1
            /// or 1, and what comes back is the line that makes that change. Nothing has to
            /// be remembered between presses, because the screen is built from what is
            /// already settled and the line says the whole of the change.
            Turns: (int -> string) option
        }

    and [<NoComparison; NoEquality>] Screen =
        {
            Title: string
            /// What the screen is asking, in whole lines - nothing here wraps anything.
            Prose: string list
            Rows: Row list
            /// What is worth saying under the list: the words that can be typed instead,
            /// and anything the rows cannot show.
            Note: string list

            /// The line that backing out of this screen stands for, where there is one. The
            /// front door has none, because there is nothing behind it.
            Backs: string option
        }

    /// A row that stands for a line outright.
    let sends digit says does line =
        { Digit = digit
          Says = says
          Does = does
          Pick = Sends line
          Turns = None }

    /// A row that opens a list of its own.
    let opens digit says does screen =
        { Digit = digit
          Says = says
          Does = does
          Pick = Opens screen
          Turns = None }

    /// The same row, with left and right walking it through a list of its own.
    let turning turn row = { row with Turns = Some turn }

    /// A row that writes the part of a line it knows and waits for the rest.
    let types digit says does text =
        { Digit = digit
          Says = says
          Does = does
          Pick = Types text
          Turns = None }

    /// The digit that picks the nth row, for a list short enough to have one each. Nought
    /// comes last, the way it does on a keyboard and on a telephone, and a list longer than
    /// ten simply runs out - the arrows still reach the rest.
    let nth n =
        if n > 9 then None else Some(char (int '0' + ((n + 1) % 10)))

    /// The highlight, moved and kept on the list. It runs round the ends, because a list
    /// this short is quicker to reach the bottom of by going up.
    let moved by at (screen: Screen) =
        match List.length screen.Rows with
        | 0 -> 0
        | count -> ((at + by) % count + count) % count

    let byDigit digit (screen: Screen) =
        screen.Rows |> List.tryFind (fun row -> row.Digit = Some digit)

    /// The mark on the row under the cursor.
    ///
    /// It is the same arrow that marks whose turn it is on a board, and it is that on
    /// purpose: `Tint` already draws that arrow in the reader's own colour, so the row
    /// being pointed at is picked out by the colour a player has already learnt means
    /// "this one, yours" - and no view had to be told about menus at all.
    [<Literal>]
    let private Marker = "->"

    /// The screen as text, with the highlight on one row - or on none, for a console reading
    /// a piped line, which has no cursor to move.
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

    /// What a key comes to.
    type Press =
        | Moved of by: int
        /// Left or right, which is either the row's own to answer or, where it has nothing
        /// to turn, a way in and a way back out.
        | Turned of by: int
        | Picked
        | Numbered of char
        | Typed of char
        | Rubbed
        | Backed
        /// Enter, with something typed to send.
        | Sent
        | Ignored

    /// A key as what it comes to. Whether a line has been started decides it: with one
    /// underway every letter belongs to it, because somebody spelling out an address is not
    /// steering. With nothing typed the letters steer - w and s up and down, a and d back
    /// and in - which is the one place the two readings meet, and the reason every screen
    /// says both are there.
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

    // --- where a person has got to ------------------------------------------------------

    /// Which list is open, where the cursor is on it, and what has been typed so far.
    ///
    /// The whole of it. What a press comes to is decided from this and nothing else, which
    /// is why the walking about can be checked without a keyboard - `Program` is left with
    /// drawing what this says and asking for the next key.
    [<NoComparison; NoEquality>]
    type Standing =
        {
            /// The list in hand, and the ones it was opened from, nearest first.
            Stack: (Screen * int) list
            Buffer: string
        }

    /// Somebody arriving at a screen, with the cursor where they last left it.
    let standing screen at =
        { Stack = [ (screen, at) ]
          Buffer = "" }

    /// The screen being looked at, and where the cursor is on it.
    let facing standing = List.head standing.Stack

    /// Where the cursor stands on the screen this was opened at, which is where it should
    /// still be when the same screen is built again and shown afresh.
    let started standing = List.last standing.Stack |> snd

    /// Whether a line is underway, which is what decides whether the letters steer.
    let typing standing = standing.Buffer <> ""

    /// What a press came to: somewhere else to stand, or a line to hand over.
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

        // Out of a list of its own is back to the one that opened it. Out of the screen
        // somebody arrived at is whatever that screen says leaving it means, which for the
        // front door is nothing at all - there is nowhere behind it to go.
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
        // Where the row has something to turn, that; where it has not, right is a way in and
        // left a way back out, so the four directions mean something on every screen.
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
