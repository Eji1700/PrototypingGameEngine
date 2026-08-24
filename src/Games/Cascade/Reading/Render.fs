namespace Prototyping.Cascade

open Prototyping.Engine
open Prototyping.Table
open Prototyping.Cascade

module Render =

    module Blocks =
        let board = "The board"
        let count = "The count"
        let onwards = "What next"
        let commands = "Commands"
        let log = "Log"

    module Notes =
        let board =
            "A letter and a number name a cell - 'f7' is column f, row 7. Say one to set it turning. 'why f7' says what it would reach when it lands."

        let rule =
            "A cell turns a quarter to the right. When it lands it reaches out along the two arms it now has, and every cell reaching back begins turning itself. Nothing may be touched while anything is still turning."

        let wear =
            $"A cell is drawn a step heavier for every {Session.PerStep} turns it has made, so the ground a cascade keeps coming back over shows."


    /// How many pictures a turning cell is drawn as across one beat: where it was, its corner half
    /// way round, and where it is going. A reader with no frame clock behind it is handed the first
    /// every time, which is right - it never sees the turn, only the board before and after.
    [<Literal>]
    let Pictures = 3

    let heading play =
        let where =
            match play.Left with
            | 0 -> "No touches left"
            | 1 -> "1 touch left"
            | left -> $"{left} touches left"

        if not (Session.atRest play) then
            $"{where} - {Words.cells (Set.count play.Turning)} turning"
        else
            match play.Run with
            | Some run when run.Halted ->
                $"{where} - the cascade from {Words.cell run.From} was stopped at {Words.turns run.Rotations}"
            | Some run -> $"{where} - the cascade from {Words.cell run.From} ran to {Words.turns run.Rotations}"
            | None -> $"{where} - nothing has been touched yet"


    /// Where in a lit shape the light has got to. It runs a cell a frame and is three cells wide,
    /// so a whole row takes about three beats end to end and a square blinks. Beats and frames are
    /// counted together, so the light travels on across a beat boundary rather than starting again.
    let private lighting margins play (lit: Lit) index =
        let head = (play.Wave - lit.Since) * Pictures + Margins.frame Pictures margins

        index <= head && head < index + 3

    /// The visual bell: a band of light four rows deep, crossing the board in about three beats.
    /// The row *labels* are marked as well as the cells coloured, so a reader with no colour at all
    /// still sees the band travel down the edge of the board.
    let private strikes margins play row =
        match Session.struck Pictures (Margins.frame Pictures margins) play with
        | Some head -> row - 1 <= head && head < row + 3
        | None -> false

    let private litAt margins play cell =
        play.Lit
        |> List.tryPick (fun lit ->
            Shape.cells lit.Shape
            |> List.tryFindIndex ((=) cell)
            |> Option.filter (lighting margins play lit)
            |> Option.map (fun index -> lit, index))


    /// One cell, as it is at this instant. The moods are for a page, which is sent the board once a
    /// beat and animates the turn out of the game's own stylesheet; the glyph is for a terminal,
    /// which is sent the board several times a beat and shown the turn a picture at a time. Both
    /// are drawn from the same state, so neither can show what the other does not.
    let private speck margins play cell =
        let standing = Session.standing cell play
        let wear = Session.wear cell play

        // A page can ring a cell without taking anything away from it, so it is told where the hand
        // is; a terminal is told on the edges of the board instead.
        let under = if cell = play.At then [ "at" ] else []

        let struck = if strikes margins play cell.Row then [ "struck" ] else []

        if Session.isTurning cell play then
            let glyph =
                match Margins.frame Pictures margins with
                | 0 -> Ink.elbow wear standing.Facing
                | 1 -> Ink.turning standing.Facing
                | _ -> Ink.elbow wear (Facing.turned standing.Facing)

            Speck.slot Ink.Turning glyph
            |> Speck.doing ([ "turning"; $"pace-{play.Speed}"; $"wear-{wear}" ] @ under)
        else

        let glyph = Ink.elbow wear standing.Facing

        match litAt margins play cell with
        | Some(_, index) ->
            Speck.slot Ink.Lit glyph
            |> Speck.doing ([ "lit"; $"lit-{index % Board.Width}"; $"wear-{wear}" ] @ under)
        | None ->

        // A cell that just landed is shown in the turning ink for as long as the first picture of
        // the beat lasts. A page inverts it outright, which a terminal cannot do; this is what a
        // terminal can do and mean the same thing.
        if Session.justLanded cell play then
            let flashing = Margins.frame Pictures margins = 0

            Speck.slot (if flashing then Ink.Turning else Ink.wornBy wear) glyph
            |> Speck.doing ([ "landed"; $"wear-{wear}" ] @ under)
        elif strikes margins play cell.Row then
            Speck.slot Ink.Lit glyph |> Speck.doing ([ $"wear-{wear}" ] @ struck @ under)
        else
            Speck.slot (Ink.wornBy wear) glyph |> Speck.doing ([ $"wear-{wear}" ] @ under)

    /// The board, with the hand marked on the edges rather than in the grid. A cell is one
    /// character wide and already says which way it faces and how worn it is, so a cursor in the
    /// grid could only be a glyph taken away from the cell under it. Marking the row down the side
    /// and the column across the top costs nothing and is legible in plain text.
    let private field margins play =
        let legend =
            Board.letters
            |> String.mapi (fun column letter ->
                if column + 1 = play.At.Column then System.Char.ToUpperInvariant letter else letter)

        Field(
            legend,
            [ for row in 1 .. Board.Height ->
                  sprintf
                      "%s%2d"
                      (if row = play.At.Row then ">"
                       elif strikes margins play row then "*"
                       else " ")
                      row,
                  [ for column in 1 .. Board.Width -> speck margins play { Row = row; Column = column } ] ]
        )


    /// The four things a board is counted by, this cascade and over all of them. Waves have no
    /// total, because a wave is how long a cascade took rather than anything it was worth - adding
    /// them up would be adding up seconds and calling it a score.
    let private count play =
        let mine reading = play.Run |> Option.map reading

        // Padded left because `Aligned` lines its columns up on the left, and counts read down want
        // units under units. Four is the width of the largest a cascade is allowed to reach.
        let shown =
            function
            | Some(number: int) -> (string number).PadLeft 4
            | None -> "   -"

        let column (title: string) (now: int option) (all: int option) =
            [ Scene.cell Tone.Quiet title
              Scene.cell Tone.Plainly (shown now)
              Scene.cell Tone.Plainly (shown all) ]

        Aligned
            [ [ Scene.cell Tone.Quiet ""
                Scene.cell Tone.Quiet "this"
                Scene.cell Tone.Quiet " all" ]
              column "Turns" (mine (fun run -> run.Rotations)) (Some play.Tally.Rotations)
              column "Rows, columns" (mine (fun run -> Session.lines run.Made)) (Some play.Tally.Lines)
              column "Squares" (mine (fun run -> Session.squares run.Made)) (Some play.Tally.Squares)
              column "Waves" (mine (fun run -> run.Waves)) None ]

    let private standing play =
        [ count play
          Scene.quietly "this cascade, and every cascade so far"
          Scene.says $"{Words.touches play.Tally.Touches} spent of {Session.Touches}."
          Scene.says $"{Session.quarter play.Speed}ms a quarter turn - notch {play.Speed} of {Session.Fastest}." ]

    let private onwards play =
        [ Scene.quietly "each of these is a line you could type"
          Does("press", "press", Tone.Plainly)
          Does("f7", "f7", Tone.Plainly)
          Does("why f7", "why f7", Tone.Plainly)
          Does("faster", "faster", Tone.Plainly)
          Does("slower", "slower", Tone.Plainly)
          Does("mute", "mute", Tone.Plainly)
          Does("log", "log", Tone.Plainly)
          Does("undo", "undo", Tone.Plainly)
          Does("restart", "restart", Tone.Plainly) ]


    let private verbs =
        [ "arrows, wasd", "move the hand about the board"
          "space, press", "set the cell the hand is on turning"
          "f7", "set that cell turning, wherever the hand is"
          "why f7", "what that cell would reach when it lands"
          "faster, slower", "how long a quarter turn takes"
          "speed 7", "go straight to that notch, from 1 to 9"
          "sound, mute", "whether this board is heard as well as read"
          "undo, redo", "walk the cascade back and forward, a wave at a time"
          "restart", "deal another board; 'restart 42' deals that one"
          "resign", "put the board down with your touches unspent"
          "history", "the record so far"
          "notes", "hide the writing that explains the board"
          "commands", "hide this box"
          "log", "hide what the game has been saying"
          "view <name>", "draw the board another way"
          "save", "write the record now"
          "help", "every command, at length"
          "quit", "leave; the record is written and can be replayed" ]

    let commands = Scene.verbs verbs

    let private wrapped text = Scene.paragraph 66 text

    let help =
        String.concat
            "\n"
            [ wrapped
                  $"A board of {Board.Width} by {Board.Height}, and every cell on it an elbow: two arms at a right angle, pointing up and right, right and down, down and left, or left and up. They are dealt at random and nothing is checked about them until you touch one."
              ""
              wrapped Notes.rule
              ""
              wrapped
                  $"A quarter turn takes {Session.quarter Session.Ordinary}ms at the notch a board is dealt on. Everything that is turning lands together, and only once it has all landed is the board read for what happens next - so a cascade goes in waves, and a wave is a beat of the clock."
              ""
              wrapped
                  "A cell that has already turned is not spared. A cascade may come back over its own ground, and the good ones do."
              ""
              wrapped
                  $"You are given {Session.Touches} touches. A row or a column counts when every one of its {Board.Width} cells has turned during a single cascade, and a square counts when all four of a two-by-two have; squares overlap and each is worth its own. That is the score, and the board is over when the touches are spent."
              ""
              wrapped Notes.wear
              ""
              wrapped
                  $"A cascade is held to {Session.MostRotations} turns over {Session.MostWaves} waves. Nothing ordinary comes near it - it is there because a board that never came to rest would be a board that could never be touched again."
              ""
              "COMMANDS"
              commands ]


    let wording = Told.inWords Words.said Words.command


    /// The log, cut down to its last few lines while the clock is running. A cascade says something
    /// every time a shape comes up, and a screen that grows a line a beat walks the board off the
    /// top of the terminal. Opening the notes or the commands puts the whole log back, on the
    /// grounds that a board with its margins open is one being read rather than watched.
    [<Literal>]
    let private Lately = 3

    let private lately (margins: Margins) lines =
        if margins.Notes || margins.Commands then
            lines
        else
            lines |> List.skip (max 0 (List.length lines - Lately))

    /// The board on the left and everything counted on the right. Side by side rather than stacked,
    /// because a board sixteen deep with three boxes under it is taller than a terminal - and a
    /// clock redrawing a screen taller than its window scrolls the board off the top of it.
    let board margins _ (model: Model<Move, Session, Notice>) =
        let play = Session.play (Model.state model)

        Stack
            [ Heading(heading play)
              Beside
                  [ Block(Blocks.board, [ field margins play; Scene.noted margins Notes.board ])
                    Stack
                        [ Block(Blocks.count, standing play @ [ Scene.noted margins Notes.rule ])
                          Block(Blocks.onwards, onwards play) ] ]
              Scene.listing margins Blocks.commands commands
              Scene.logged margins Blocks.log (lately margins (Scene.log wording model)) ]


    let history _ (model: Model<Move, Session, Notice>) =
        let entry (entry: Entry<Move, Notice>) =
            [ Scene.cell Tone.Quiet $"{entry.Ordinal}  touch {entry.Turn}"
              Scene.cell Tone.Plainly (Words.command entry.Asked)
              Scene.cell Tone.Plainly (entry.Told |> List.map wording |> String.concat " ") ]

        Journal.entries model.Journal
        |> List.map entry
        |> Scene.record (heading (Session.play (Model.state model)))


    let answer _ asked (model: Model<Move, Session, Notice>) =
        let play = Session.play (Model.state model)

        match Board.read asked with
        | Some cell when Board.holds cell ->
            let standing = Session.standing cell play
            let landing = Facing.turned standing.Facing

            let reaching =
                Facing.arms landing
                |> List.map (fun way -> way, Board.along way cell)
                |> List.map (fun (way, other) ->
                    if not (Board.holds other) then
                        way, other, false
                    else
                        way, other, Facing.reaches (Way.opposite way) (Session.facing other play))

            let said (way, other, matching) =
                let where = Words.way way

                if not (Board.holds other) then
                    $"Its arm {where} would point off the board."
                elif matching then
                    $"Its arm {where} would find {Words.cell other} reaching back, and set it turning."
                else
                    $"Its arm {where} would find {Words.cell other} facing away."

            Block(
                $"Cell {Words.cell cell}",
                [ Scene.says (
                      let far =
                          match standing.Turned with
                          | 0 -> "has not turned yet"
                          | 1 -> "has turned once"
                          | turned -> $"has turned {Words.turns turned}"

                      $"{Words.cell cell} is {Words.facing standing.Facing}, and {far}."
                  )
                  Scene.says $"A quarter to the right would leave it {Words.facing landing}."
                  yield! reaching |> List.map (said >> Scene.says)
                  Scene.quietly (
                      if Session.isTurning cell play then
                          "It is turning now, so this is where it is going rather than where it is."
                      elif Session.atRest play then
                          "Nothing is turning, so it may be touched."
                      else
                          "Something is still turning, so nothing may be touched yet."
                  ) ]
            )
        | Some cell -> Block(Blocks.board, [ Scene.says (Words.rejection (NoSuchCell cell)) ])
        | None -> Block(Blocks.board, [ Scene.says $"'{asked}' is not a cell. Ask about one by name - 'why f7'." ])

    let rules = Scene.rules help

    let waiting = Scene.waiting Words.seated


    /// What a page needs that no general reader could know: how to turn a cell that is turning. The
    /// board is sent once a beat and everything between two of them is done here. `--turn` follows
    /// the notch the game is running at, so winding the clock winds the animation with it; the
    /// cells carry the notch as a mood because the sheet is written before any notch is known.
    let private sheet =
        let paces =
            [ for notch in Session.Slowest .. Session.Fastest -> $".speck.pace-{notch} {{ --turn: {Session.quarter notch}ms; }}" ]

        let lights =
            [ for at in 0 .. Board.Width - 1 -> $".speck.lit-{at} {{ animation-delay: {at * 40}ms; }}" ]

        String.concat
            "\n"
            ([ ".field { font-size: 1.1rem; line-height: 1.15; }"
               ".field .speck { width: 1.15ch; transition: color 240ms linear; }"
               ""
               "/* Where the hand is resting: a ring round the cell rather than anything in it, so
   the glyph goes on saying which way it faces and how worn it is. */"
               ".speck.at { outline: 1px solid var(--yours); outline-offset: 1px; border-radius: 2px; }"
               ""
               "/* The visual bell: the band a page shows for what a terminal would ring for. */"
               ".speck.struck { animation: struck 320ms ease-out both; border-radius: 2px; }"
               "@keyframes struck {"
               "  0%   { background: var(--lit); color: var(--ground); }"
               "  100% { background: transparent; }"
               "}"
               ""
               ".speck.turning { animation: turning var(--turn, 500ms) linear both; }"
               "@keyframes turning { from { transform: rotate(0deg); } to { transform: rotate(90deg); } }"
               "" ]
             @ paces
             @ [ ""
                 ".speck.landed { animation: landed 260ms ease-out both; border-radius: 2px; }"
                 "@keyframes landed {"
                 "  0%   { background: var(--turning); color: var(--ground); }"
                 "  100% { background: transparent; }"
                 "}"
                 ""
                 ".speck.lit { animation: lit 420ms ease-out both; border-radius: 2px; }"
                 "@keyframes lit {"
                 "  0%, 100% { background: transparent; }"
                 "  45%      { background: var(--lit); color: var(--ground); }"
                 "}" ]
             @ lights
             @ [ ""
                 "@media (prefers-reduced-motion: reduce) {"
                 "  .speck.turning, .speck.landed, .speck.lit { animation: none; }"
                 "}" ])

    let shell =
        { Title = "Cascade"
          Sheet = sheet
          Placeholder = "a cell to set it turning - 'f7' - or 'help'"
          Keys =
            [ "ArrowUp", "up"
              "ArrowDown", "down"
              "ArrowLeft", "left"
              "ArrowRight", "right"
              "w", "up"
              "s", "down"
              "a", "left"
              "d", "right"
              " ", "press"
              "+", "faster"
              "-", "slower" ] }
