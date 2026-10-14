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

    // Rows are numbered from 1 as they are read, and there is no tenth: 0 is the way out of every
    // screen, so a row that was handed it would answer the key meant for leaving. A screen with
    // more rows than digits walks the rest by moving the mark.
    let nth n =
        if n > 8 then None else Some(char (int '0' + n + 1))

    /// Moving off either end of a list comes round to the other. The doubled modulo is to bring a
    /// negative remainder back round, which .NET's does not do.
    let wrapped count at by = ((at + by) % count + count) % count

    let private moved by at (screen: Screen) =
        match List.length screen.Rows with
        | 0 -> 0
        | count -> wrapped count at by

    let private byDigit digit (screen: Screen) =
        screen.Rows |> List.tryFind (fun row -> row.Digit = Some digit)

    [<Literal>]
    let private Marker = "->"

    /// The gap between one column of rows and the next; the fewest rows worth making a column out
    /// of, because three columns of two is a shape rather than a list; and the most columns there
    /// is any point in, since a list read left to right as well as down stops being read at all.
    [<Literal>]
    let private Gutter = 3

    [<Literal>]
    let private Fewest = 4

    [<Literal>]
    let private Most = 3

    /// How many rows go side by side.
    ///
    /// One, unless every row on the screen is a bare name. A row with something to say about it is a
    /// line of prose and belongs on a line of its own; a screen of nothing but names is a list of
    /// names, and a list of names is read in columns. A screen with a dozen short rows on it,
    /// drawn one to a line, is a dozen lines of a window that had something else to show.
    let private columns room (screen: Screen) wide =
        if screen.Rows |> List.exists (fun row -> row.Does <> "") then
            1
        else
            let fitting = max 1 ((room - 2) / (wide + Gutter))
            let worth = max 1 (List.length screen.Rows / Fewest)
            List.min [ fitting; worth; Most ]

    /// A screen, with the row at `at` marked. `room` is how wide it may be drawn.
    ///
    /// The rows go down a column and then down the next, never across, so that walking off the foot
    /// of one column arrives at the head of the following one - which is what up and down do, and
    /// there is nothing that walks sideways between them.
    let draw room at (screen: Screen) =
        let column =
            match screen.Rows |> List.map (fun row -> String.length row.Says) with
            | [] -> 0
            | widths -> List.max widths

        let row index (row: Row) =
            let mark = if at = Some index then Marker else "  "
            let digit = row.Digit |> Option.map string |> Option.defaultValue " "

            (sprintf "  %s %s  %s  %s" mark digit (row.Says.PadRight column) row.Does).TrimEnd()

        // What one row takes with nothing said beside it: two spaces, the mark, a space, the digit,
        // two more, and the name padded out.
        let wide = column + 7

        let laid =
            match columns room screen wide with
            | 1 -> screen.Rows |> List.mapi row
            | across ->
                let deep = (List.length screen.Rows + across - 1) / across

                [ for down in 0 .. deep - 1 ->
                      [ for over in 0 .. across - 1 do
                            let index = over * deep + down

                            match List.tryItem index screen.Rows with
                            | Some here -> yield (row index here).PadRight wide
                            | None -> () ]
                      |> String.concat (String.replicate Gutter " ")
                      |> fun line -> line.TrimEnd() ]

        let indented lines =
            match lines with
            | [] -> []
            | _ -> (lines |> List.map (fun line -> ("  " + (line: string)).TrimEnd())) @ [ "" ]

        String.concat
            Environment.NewLine
            ([ ""; $"=== {screen.Title} ==="; "" ]
             @ indented screen.Prose
             @ laid
             @ [ "" ]
             @ indented screen.Note)

    /// A screen as it is steered: the outermost one, and what turning the page and sealing the turn
    /// send. Those two are facts about the whole board rather than about any one list on it, which
    /// is why they sit here and not on `Screen` - every menu has neither - and why the page turns and
    /// the turn seals from however deep somebody has walked.
    [<NoComparison; NoEquality>]
    type Steered =
        { Screen: Screen
          Pages: (int -> string) option
          Seals: string option }

    let steered screen =
        { Screen = screen
          Pages = None
          Seals = None }

    /// Whose the keyboard is. Held rather than worked out from whether anything has been typed, so
    /// that a line may begin with one of the steering letters, and so that the prompt can be open
    /// and empty, which is what backspacing a line back to nothing leaves.
    type Prompt =
        | Shut
        | Open of line: string

        /// Sealing was asked for and is asked about before it is sent: a sealed turn cannot be
        /// walked back the way a mark can.
        | Offering of seal: string

    [<NoComparison; NoEquality>]
    type Standing =
        { Stack: (Screen * int) list
          Board: Steered
          Prompt: Prompt }

    /// Where somebody is standing, as the way down to it: which row of the first screen, then which
    /// row of the screen that one opens, and so on.
    ///
    /// A place is kept this way rather than as the screens themselves because a game builds its
    /// screens afresh every time it is asked for them - the ones somebody walked down are stale by
    /// the moment they are wanted again. A path is walked down whatever was handed back this time,
    /// and stops early where the screens no longer go that deep.
    let rec private descend (screen: Screen) path =
        let here at = [ (screen, at) ]

        match path with
        | [] -> here 0
        | at :: below ->
            let at = if at >= 0 && at < List.length screen.Rows then at else 0

            match List.tryItem at screen.Rows, below with
            | Some row, (_ :: _) ->
                match row.Pick with
                | Opens under -> descend under below @ here at
                | Sends _
                | Types _ -> here at
            | Some _, [] -> here at
            | None, _ -> here 0

    let standing (board: Steered) path =
        { Stack = descend board.Screen path
          Board = board
          Prompt = Shut }

    let facing standing = List.head standing.Stack

    /// The way back to where somebody is, outermost first - what `standing` is handed to put them
    /// back there. Answering a line used to give back only the row of the *first* screen, so taking
    /// anything on a list two deep dropped whoever took it back out to the top of the first one:
    /// a list of things to tick was a list you were thrown off every time you ticked one.
    let path standing =
        standing.Stack |> List.rev |> List.map snd

    /// How far to scroll a board taller than the window. In pages rather than lines, because how
    /// many lines a page is depends on the window and the list under the board, and only the screen
    /// drawing them knows either.
    type Scroll =
        | Windowful of by: int
        | Head
        | Foot

    /// Scrolling is an answer of its own rather than a field on `Standing`, because it is what is
    /// on the window and not where somebody is: a standing is handed back to put somebody back on
    /// the next board, and the next board may be another page, to be read from its head.
    [<NoComparison; NoEquality>]
    type Answer =
        | Steering of Standing
        | Scrolling of Standing * Scroll
        | Answered of string

    type private Press =
        | Moved of by: int
        | Turned of by: int
        | Paged of by: int
        | Scrolled of Scroll
        | Offered
        | Picked
        | Numbered of char
        | Prompting
        | Backed
        | Ignored

    /// What a key means while the prompt is shut.
    ///
    /// Only the letters in the keymap steer, and nothing else does - not the arrows, not Tab, not a
    /// letter the keymap does not hold. A letter that is not a steering one used to start a line,
    /// but which letters steer is now the player's to choose, so the same key would start a line at
    /// one keyboard and move the mark at another. The space bar is the one way into the prompt, and
    /// is the same at every keyboard. So are the page keys, Home and End, which scroll a board as two
    /// of the letters do: the keymap holds letters, and these are not.
    let private pressed keymap (key: ConsoleKeyInfo) =
        match key.Key with
        | ConsoleKey.Enter -> Picked
        | ConsoleKey.Backspace
        | ConsoleKey.Escape -> Backed
        | ConsoleKey.PageDown -> Scrolled(Windowful 1)
        | ConsoleKey.PageUp -> Scrolled(Windowful -1)
        | ConsoleKey.Home -> Scrolled Head
        | ConsoleKey.End -> Scrolled Foot

        // Matched as a key as well as a character, because a space arrives as both: a terminal
        // sends ' ' with it and some send nothing at all.
        | ConsoleKey.Spacebar -> Prompting
        | _ ->
            match key.KeyChar with
            | ' ' -> Prompting
            | digit when Char.IsDigit digit -> Numbered digit
            | letter ->
                match Keymap.keyOf letter keymap with
                | Some Up -> Moved -1
                | Some Down -> Moved 1
                | Some Left -> Turned -1
                | Some Right -> Turned 1
                | Some Previous -> Paged -1
                | Some Next -> Paged 1
                | Some Seal -> Offered
                | Some Above -> Scrolled(Windowful -1)
                | Some Below -> Scrolled(Windowful 1)
                | None -> Ignored

    /// What a key means while a line is being typed. **Every** printable key is a letter, the
    /// steering ones included - which is the whole point of there being a mode at all: `search` was
    /// once untypeable at a game about searching, because its first letter steered instead.
    let private typing line (key: ConsoleKeyInfo) standing =
        let opened line =
            Steering { standing with Prompt = Open line }

        let shut () =
            Steering { standing with Prompt = Shut }

        match key.Key with
        | ConsoleKey.Enter -> Answered line

        // Escape leaves the prompt before it leaves the screen: somebody half-way through a line
        // who changed their mind wants the line gone, not the screen.
        | ConsoleKey.Escape -> shut ()
        | ConsoleKey.Backspace when line = "" -> shut ()
        | ConsoleKey.Backspace -> opened (line.Substring(0, line.Length - 1))
        | ConsoleKey.Spacebar -> opened (line + " ")
        | _ when Char.IsControl key.KeyChar -> Steering standing
        | _ -> opened (line + string key.KeyChar)

    let answer keymap (key: ConsoleKeyInfo) standing =
        let showing, at = facing standing
        let here () = List.tryItem at showing.Rows

        let taking (row: Row) =
            match row.Pick with
            | Sends line -> Answered line

            // A row that writes the beginning of a line and waits hands the keyboard over as well,
            // or the rest of what it asked for could not be typed.
            | Types text -> Steering { standing with Prompt = Open text }
            | Opens under ->
                Steering
                    { standing with
                        Stack = (under, 0) :: standing.Stack }

        // Backing out of the innermost screen; from the outermost there is nowhere to go, so it
        // answers with whatever line the screen said stands for leaving it, if any.
        let out () =
            match standing.Stack with
            | [ _ ] ->
                match showing.Backs with
                | Some line -> Answered line
                | None -> Steering standing
            | _ :: behind -> Steering { standing with Stack = behind }
            | [] -> Steering standing

        match standing.Prompt with
        | Open line -> typing line key standing

        // Anything but the seal or Enter is a no, and is used up saying it: a key pressed to get
        // out of the question is not also a key that does something else.
        | Offering seal ->
            if key.Key = ConsoleKey.Enter || Keymap.keyOf key.KeyChar keymap = Some Seal then
                Answered seal
            else
                Steering { standing with Prompt = Shut }
        | Shut ->
            match pressed keymap key with
            | Ignored -> Steering standing
            | Prompting -> Steering { standing with Prompt = Open "" }
            | Backed -> out ()
            | Scrolled scroll -> Scrolling(standing, scroll)
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
            | Paged by ->
                match standing.Board.Pages with
                | Some turn -> Answered(turn by)
                | None -> Steering standing
            | Offered ->
                match standing.Board.Seals with
                | Some seal -> Steering { standing with Prompt = Offering seal }
                | None -> Steering standing

            // Turning on a row that has nothing to turn through takes it, and turning back backs
            // out, so a screen of plain rows can be walked into and out of with one hand.
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

    /// The lines under the list: what the keys do here, then the line a line is typed on. The last
    /// is always the typed one, and is the only one that must never be painted - what somebody types
    /// is not markup.
    ///
    /// The hint is built from the keymap and names only what does something on this screen, so it
    /// is right for whoever moved which letters, and it is the one place the program names a key: a
    /// note that said "left and right" would be wrong for every player who moved them.
    ///
    /// `cut` is whether the board above was cut to the window, which only the screen drawing it
    /// knows. The keys that scroll it are named only then: on a board that fits they do nothing.
    let prompt keymap cut standing =
        let showing, _ = facing standing
        let letter key = string (Keymap.letter key keymap)

        let pair first second does =
            $"{letter first} {letter second} {does}"

        match standing.Prompt with
        | Open line -> [ $"> {line}_" ]
        | Offering _ -> [ $"  Ready? {letter Seal} or Enter seals it, anything else does not."; "> " ]
        | Shut ->
            let hint =
                [ pair Up Down "move"
                  if showing.Rows |> List.exists (fun row -> row.Turns.IsSome) then
                      pair Left Right "turn"
                  if standing.Board.Pages.IsSome then pair Previous Next "page"
                  if standing.Board.Seals.IsSome then $"{letter Seal} ready"
                  if cut then pair Above Below "scroll"
                  "space to type" ]

            [ "  " + String.concat "   " hint; "> " ]
