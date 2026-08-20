namespace TCModel.Table

open TCModel.Engine

type Margins = { Notes: bool; Commands: bool }

module Margins =

    let all = { Notes = true; Commands = true }

    let none = { Notes = false; Commands = false }

[<RequireQualifiedAccess>]
type Tone =
    | Plainly
    | Quiet
    | Yours
    | Slot of key: string

type Span = { Text: string; Tone: Tone }

type Line = Span list

type Course = { Shift: int; Cells: Scene list }

and Scene =
    | Blank

    | Heading of string

    | Say of Line

    | Note of string

    | Written of string

    | Block of title: string * Scene list

    | Stack of Scene list

    | Beside of Scene list

    | Aligned of Line list list

    | Walled of across: int * Course list

    | Tile of title: string option * tone: Tone * body: Scene list

    | Patch of shape: string * tone: Tone * body: Scene list

    | Big of Span

    | Does of caption: string * line: string * tone: Tone

module Span =

    let plainly text = { Text = text; Tone = Tone.Plainly }

    let quiet text = { Text = text; Tone = Tone.Quiet }

    let yours text = { Text = text; Tone = Tone.Yours }

    let slot key text = { Text = text; Tone = Tone.Slot key }

    let toned tone text = { Text = text; Tone = tone }

module Scene =

    let says text = Say [ Span.plainly text ]

    let quietly text = Say [ Span.quiet text ]

    let noted (margins: Margins) text =
        if margins.Notes then Note text else Blank

    let listing (margins: Margins) title text =
        if margins.Commands then Block(title, [ Written text ]) else Blank

    let offering (margins: Margins) title body =
        if margins.Commands then Block(title, body) else Blank

    let plainText (line: Line) =
        line |> List.map (fun span -> span.Text) |> String.concat ""

    let cell tone text : Line = [ Span.toned tone text ]

    let squared cells = { Shift = 0; Cells = cells }

    let wrap room (text: string) =
        let put (lines, line) word =
            if line = "" then lines, word
            elif String.length line + 1 + String.length word <= room then lines, line + " " + word
            else lines @ [ line ], word

        let lines, last = text.Split ' ' |> Array.fold put ([], "")
        lines @ [ last ]

    let paragraph room text = wrap room text |> String.concat "\n"


    let runs (glyphs: (string * Tone) seq) : Line =
        glyphs
        |> Seq.fold
            (fun spans (glyph, tone) ->
                match spans with
                | (span: Span) :: rest when span.Tone = tone -> { span with Text = span.Text + glyph } :: rest
                | _ -> { Text = glyph; Tone = tone } :: spans)
            []
        |> List.rev


    [<Literal>]
    let NothingYet = "nothing yet"

    let log told (model: Model<'Move, 'State, 'Notice>) =
        match model.Log with
        | [] -> [ quietly NothingYet ]
        | notices -> notices |> List.rev |> List.map (told >> says)

    let record heading rows =
        match rows with
        | [] -> Block("The record", [ quietly NothingYet ])
        | rows -> Block("The record", [ Aligned rows; quietly heading ])

    let rules help = Block("The rules", [ Written help ])

    let verbs (listed: (string * string) list) =
        let across = listed |> List.map (fst >> String.length) |> List.fold max 0

        listed
        |> List.map (fun (verb: string, says) -> "  " + verb.PadRight across + "  " + says)
        |> String.concat "\n"


    module Filling =
        let title = "Waiting for the table to fill"

        let standing (seat: Waiting) =
            if seat.Expected then "still to arrive"
            elif seat.Away then "here, but their console has gone"
            else "here"

        let stillToCome (seats: Waiting list) =
            match seats |> List.filter (fun seat -> seat.Expected) |> List.length with
            | 0 -> "Everybody is here."
            | 1 -> "1 more to come. The game begins once every seat is taken."
            | more -> $"{more} more to come. The game begins once every seat is taken."

    let waiting seated (seats: Waiting list) =
        let standing (seat: Waiting) =
            [ (if seat.Yours then Span.yours else Span.plainly) (seated seat.Yours seat.Player) ]
            :: [ [ Span.quiet (Filling.standing seat) ] ]

        Stack
            [ Heading Filling.title
              Block("The table", [ Aligned(seats |> List.map standing); quietly (Filling.stillToCome seats) ]) ]
