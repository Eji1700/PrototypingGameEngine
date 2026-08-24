namespace Prototyping.Table

open Prototyping.Engine

/// How much of a screen to draw, and how far through a beat it is being drawn. The three switches
/// are the boxes round the game itself; none is part of the game, so none reaches the model or the
/// record, and at a table over a network each person has their own.
///
/// `Phase` is the one thing here that is not a player's choice: it runs from 0 at a beat towards 1
/// just before the next, and is how a board drawn between two beats knows how far a moving piece
/// has got. A game with no frame clock is only ever drawn at 0.
type Margins =
    { Notes: bool
      Commands: bool

      // `Logged` rather than `Log`: a `Model` has a `Log`, and F# resolves a field on an
      // un-annotated value by name alone, so the clash would silently retype `model.Log`.
      Logged: bool

      Phase: float }

module Margins =

    let all =
        { Notes = true
          Commands = true
          Logged = true
          Phase = 0.0 }

    let none =
        { Notes = false
          Commands = false
          Logged = false
          Phase = 0.0 }

    let through phase margins = { margins with Phase = phase }

    /// Which of `count` frames a phase falls in - what a terminal picking one of a few pictures
    /// wants, rather than the fraction itself.
    let frame count margins =
        if count <= 1 then 0 else margins.Phase * float count |> int |> max 0 |> min (count - 1)

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

    // A board of many small cells rather than a few big ones. `Walled` walls every cell, which is
    // right for nine squares and unreadable at two hundred and fifty-six; a field is a glyph a
    // cell, in rows with a label down the side and a legend across the top.
    | Field of legend: string * rows: (string * Speck list) list

/// One cell of a field. `Mood` is what the cell is *doing* rather than what it is - turning,
/// landing, lit - as bare words: a page turns them into classes it animates from the game's own
/// stylesheet, and a terminal, which cannot be part-way through anything, ignores them.
and Speck =
    { Glyph: string
      Tone: Tone
      Mood: string list }

module Span =

    let plainly text = { Text = text; Tone = Tone.Plainly }

    let quiet text = { Text = text; Tone = Tone.Quiet }

    let yours text = { Text = text; Tone = Tone.Yours }

    let slot key text = { Text = text; Tone = Tone.Slot key }

    let toned tone text = { Text = text; Tone = tone }

module Speck =

    let plainly glyph =
        { Glyph = glyph
          Tone = Tone.Plainly
          Mood = [] }

    let quiet glyph =
        { Glyph = glyph
          Tone = Tone.Quiet
          Mood = [] }

    let toned tone glyph =
        { Glyph = glyph
          Tone = tone
          Mood = [] }

    let slot key glyph = toned (Tone.Slot key) glyph

    let doing mood speck = { speck with Mood = speck.Mood @ mood }

module Scene =

    let says text = Say [ Span.plainly text ]

    let quietly text = Say [ Span.quiet text ]

    let noted (margins: Margins) text =
        if margins.Notes then Note text else Blank

    let listing (margins: Margins) title text =
        if margins.Commands then Block(title, [ Written text ]) else Blank

    /// What the game has been saying, if the reader still wants to see it - a dozen lines of what
    /// led to a board, under the board somebody is trying to read, is a dozen lines in the way.
    let logged (margins: Margins) title body =
        if margins.Logged then Block(title, body) else Blank

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
