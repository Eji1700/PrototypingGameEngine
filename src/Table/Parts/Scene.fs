namespace TCModel.Table

/// What a player wants in the margins: the writing that explains the board, and the box that
/// lists what can be typed.
///
/// Neither is the game. Both are scaffolding for somebody who has not played before, and both
/// are dead space to somebody who has - which is the whole argument for their being here at
/// all rather than settled once by whoever wrote the screen. They are how a game is *read*, so
/// they stay out of the model and out of the record, and each person at a table has their own.
///
/// One record rather than two flags threaded side by side, because two booleans in a row is a
/// thing that gets passed the wrong way round exactly once and then never again looks wrong.
type Margins = { Notes: bool; Commands: bool }

module Margins =

    /// Everything, which is what somebody who has said nothing about it gets. A player who has
    /// not asked to be told less is a player who has not read it yet.
    let all = { Notes = true; Commands = true }

    /// The board and nothing round it.
    let none = { Notes = false; Commands = false }

/// What a screen is made of, before anybody has decided what it looks like.
///
/// A `View` is every screen a player reads, and it takes the model rather than somebody
/// else's finished text - which is what lets `plain` write blocks of text where `rich` builds
/// panels and `html` builds a page. The cost of that freedom is that a game with three views
/// answers every endpoint three times, and the three can quietly come to disagree: the same
/// border was a "side" in one and a "wall" in another, one view drew a block the others left
/// out, and nothing failed.
///
/// This is the middle ground. A game describes a screen once, in pieces that say what a thing
/// *is* - a block, a grid of cells, a row of columns, a note, a control that types a line -
/// and the three readers in `Readers` turn that one description into text, into Spectre's
/// widgets, and into elements. A game that wants a screen laid out in a way no reader here
/// would think of still writes its own view; the seam is untouched and `Turncoats` uses it,
/// because a honeycomb drawn by counting characters into columns is exactly that.
///
/// Nothing in here is a decision about the game. A `Scene` says what the parts are and what
/// they are drawn in; where they go on a screen is each reader's own business, and how big
/// they are is the terminal's or the browser's.
[<RequireQualifiedAccess>]
type Tone =
    /// The game's ordinary prose, in whatever the game colours prose in. A reader that
    /// paints prose paints this; one that does not leaves it alone.
    | Plainly
    /// Said once and easily passed over: a count beside a name, a free square's number, the
    /// writing under a board.
    | Quiet
    /// The reader's own seat, and whoever is to play. The one colour that is not the game's,
    /// which is why it is here rather than a slot.
    | Yours
    /// Whatever the game said it colours, by the word a person types for it. The key is a
    /// `Slot.Key`, so what a scene asks for and what the colour screen offers are the same
    /// list.
    | Slot of key: string

/// A run of text with one tone. The smallest thing a scene is built out of.
type Span = { Text: string; Tone: Tone }

/// A run of spans on one line.
type Line = Span list

/// One row of a grid: the cells, and how far the row is shifted.
///
/// The shift is in half-cells and it is the whole reason a grid is not a table. A map laid
/// out on a triangular lattice is a grid of hexes where each row sits half a cell across from
/// the one above, and that offset is what makes a region touch six others rather than four -
/// so it is a fact about the board rather than a flourish, and it is described here so that
/// no reader can drop it without saying so.
type Course = { Shift: int; Cells: Scene list }

and Scene =
    /// Nothing at all, so that a part of a screen can be left out without the thing building
    /// it having to change shape.
    | Blank

    /// The line every screen opens with: whose turn it is, or how the game ended.
    | Heading of string

    /// One line of the game talking.
    | Say of Line

    /// Writing that is there to be read once and gone the moment the notes are turned off.
    /// Whether it is here at all is the game's decision, not a reader's - `Scene.noted` is
    /// how that decision is written down.
    | Note of string

    /// Text that has already been laid out and must not be laid out again: a table of
    /// commands, a page of rules. Readers keep it exactly as it is and only colour it.
    | Written of string

    /// A named part of the screen. Every reader draws the name; how it draws it - shouted,
    /// written into the top wall of a panel, given a heading - is its own.
    | Block of title: string * Scene list

    /// One after another, down the screen.
    | Stack of Scene list

    /// Side by side where there is room for it, and stacked where there is not. A hint
    /// rather than an instruction: a reader with no way to put two things beside each other
    /// stacks them and nothing is lost but the width.
    | Beside of Scene list

    /// Rows of cells with the columns lined up, and no walls. Two seats and what each holds;
    /// a record, three columns wide.
    | Aligned of Line list list

    /// Cells laid out in rows, with walls between them.
    ///
    /// `across` is how much room one cell *wants*, in characters, and it is a hint rather
    /// than a measurement - a reader honours it if it can afford to. One laying a screen out
    /// by counting characters cannot and gives a cell what is in it; a page has its own sheet
    /// and its own arithmetic and would rather be told nothing.
    | Walled of across: int * Course list

    /// One cell with a wall round it: what it is called if it is called anything, what the
    /// wall is drawn in, and what is inside.
    | Tile of title: string option * tone: Tone * body: Scene list

    /// One cell of a grid that is a piece of something larger, and the name of the thing it
    /// is a piece of.
    ///
    /// A tile is a cell and its own walls; a patch is part of a shape. Cells carrying the
    /// same `shape` that touch are one region, so the wall between them is the inside of a
    /// country rather than a border and is not drawn at all - which is what lets a map lay a
    /// province out over as many cells as its borders need and still look like one province.
    /// The tone is the shape's, so a region owned by somebody is outlined in their colour.
    ///
    /// Here rather than left to each reader for the same reason a row's shift is: which cells
    /// make one region is a fact about the board, and a reader is not in a position to guess
    /// it. Two provinces held by the same power are two provinces and the border between them
    /// is real - the tone cannot say what the shape says.
    | Patch of shape: string * tone: Tone * body: Scene list

    /// One glyph, drawn large. A mark in a square is a thing to see across a room rather
    /// than a letter in a sentence, and every reader has its own way of saying so.
    | Big of Span

    /// A control that types a line for the player.
    ///
    /// This is the page's whole bargain with the parser, said in the description rather than
    /// in the page: a control carries the line it would type, so a reader that draws buttons
    /// has nothing else it *could* send. A reader with no buttons draws the caption, which is
    /// what a player at a terminal would have typed anyway.
    | Does of caption: string * line: string * tone: Tone

module Span =

    let plainly text = { Text = text; Tone = Tone.Plainly }

    let quiet text = { Text = text; Tone = Tone.Quiet }

    let yours text = { Text = text; Tone = Tone.Yours }

    let slot key text = { Text = text; Tone = Tone.Slot key }

    let toned tone text = { Text = text; Tone = tone }

module Scene =

    /// A whole line in one tone, which is most of them.
    let says text = Say [ Span.plainly text ]

    let quietly text = Say [ Span.quiet text ]

    /// A note, or nothing, which is the only thing the notes flag ever decides. Written here
    /// so that a game says `noted margins text` once per note instead of every reader asking.
    let noted (margins: Margins) text = if margins.Notes then Note text else Blank

    /// The block that lists what a player may type, or nothing at all.
    ///
    /// Here rather than in each game for the same reason `noted` is: three games build this
    /// block out of the same two pieces - a title and a table of verbs already laid out - and
    /// a fourth turns it off in three places of its own. What a game decides is what is *in*
    /// it; whether it is on the screen is the reader's, and is decided once.
    let listing (margins: Margins) title text =
        if margins.Commands then Block(title, [ Written text ]) else Blank

    /// A line's words, with the tones dropped. For a reader that draws no colour, and for
    /// working out how wide a thing is.
    let plainText (line: Line) =
        line |> List.map (fun span -> span.Text) |> String.concat ""

    /// A cell of one span, which is what most cells are.
    let cell tone text : Line = [ Span.toned tone text ]

    /// A grid row that sits square under the one above. The common case, and the one a game
    /// with an ordinary board wants.
    let squared cells = { Shift = 0; Cells = cells }

    /// A paragraph as lines of at most `room` characters, breaking no word.
    ///
    /// Here rather than left to whatever is showing it, because a reader that lays a note
    /// out inside a panel has to know where the breaks are before it can draw the walls -
    /// and a note broken to the width of the screen and then again to the width of the box
    /// holding it comes out as a column of scraps.
    let wrap room (text: string) =
        let put (lines, line) word =
            if line = "" then lines, word
            elif String.length line + 1 + String.length word <= room then lines, line + " " + word
            else lines @ [ line ], word

        let lines, last = text.Split ' ' |> Array.fold put ([], "")
        lines @ [ last ]

    // --- the one screen with no game behind it -------------------------------------------
    //
    // A table still filling up is drawn from a list of who has arrived and nothing else, so
    // there is no game in it to differ about - and both games had written it out anyway, in
    // words that had already begun to drift apart. It is described once here, and a game that
    // wants it says so in one line.

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

    /// The table filling up, given a game's own way of naming a seat.
    let waiting seated (seats: Waiting list) =
        let standing (seat: Waiting) =
            [ (if seat.Yours then Span.yours else Span.plainly) (seated seat.Yours seat.Player) ]
            :: [ [ Span.quiet (Filling.standing seat) ] ]

        Stack
            [ Heading Filling.title
              Block("The table", [ Aligned(seats |> List.map standing); quietly (Filling.stillToCome seats) ]) ]
