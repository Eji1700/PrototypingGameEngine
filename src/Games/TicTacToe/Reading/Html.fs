namespace TCModel.TicTacToe

open Falco.Markup
open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and both `Falco.Markup` and the command line's argument types carry names this game
// already uses - `Open`.
open TCModel.TicTacToe

/// The board as a page, for a player reading in a browser.
///
/// The third way of showing the game and shown the same way as the other two: every endpoint
/// is handed the model and gives back text. The text happens to be HTML.
///
/// This view is where a board of nine squares finally gets what it always wanted, which is
/// nine buttons. And they are buttons on the program's own terms: every one of them posts
/// the words a player would have typed at the prompt, so a button cannot ask for something
/// the parser would not take - there is nothing else for it to send.
///
/// Nothing here is a page. The shell, the stream, the prompt, the colour form and the door
/// are `Page`'s and are the same at every game; what is here is nine squares and a list.
module Html =

    let private quiet = Page.quiet

    let private block = Page.block

    let private note = Page.note

    let private types = Page.types

    // --- the board --------------------------------------------------------------------------

    /// A square. Taken, it is the mark in it; free, it is a button that types its own number
    /// - which is also the number printed on it, so what a player clicks and what a player
    /// could have typed are the same thing said twice.
    let private cell board square =
        match Board.at square board with
        | Some mark ->
            Elem.div [ Attr.class' $"cell {Ink.key mark}" ] [ Elem.span [ Attr.class' "mark" ] [ Text.enc (Words.mark mark) ] ]
        | None -> Elem.div [ Attr.class' "cell free" ] [ types (string square) (string square) ]

    let private grid board =
        Squares.rows
        |> List.map (fun row -> Elem.div [ Attr.class' "row" ] (row |> List.map (cell board)))
        |> Elem.div [ Attr.class' "grid" ]

    // --- who is playing -----------------------------------------------------------------------

    let private players beholder session =
        let acting = Session.active session
        let board = Session.board session

        Mark.all
        |> List.map (fun mark ->
            let seat = Session.seatOf mark
            let yours = seat = beholder

            let marker = if seat = acting && not (Session.isOver session) then "->" else ""

            Elem.div
                [ Attr.class' (if yours then "player yours" else "player") ]
                [ Elem.span [ Attr.class' "marker" ] [ Text.raw marker ]
                  Elem.span [ Attr.class' $"who {Ink.key mark}" ] [ Text.enc (Words.seated yours seat) ]
                  quiet $"{Board.held mark board |> List.length} of 9" ])

    // --- the whole screen -----------------------------------------------------------------------

    let private lines (text: string) = Page.lines text

    let private log (model: Model<Move, Session, Notice>) =
        match model.Log with
        | [] -> [ quiet Render.nothingYet ]
        | notices ->
            notices
            |> List.rev
            |> List.map (fun notice -> Elem.div [ Attr.class' "said" ] [ Text.enc (Render.wording notice) ])

    let board notes beholder (model: Model<Move, Session, Notice>) =
        let session = Model.state model
        let noted text = if notes then [ note text ] else []

        Page.screen (
            [ Elem.h1 [] [ Text.enc (Render.heading beholder session) ] ]
            @ [ block Render.Blocks.board ([ grid (Session.board session) ] @ noted Render.Notes.board) ]
            @ [ block Render.Blocks.players (players beholder session @ noted Render.Notes.winning) ]
            @ [ block Render.Blocks.commands [ lines Render.commands ] ]
            @ [ block Render.Blocks.log (log model) ]
        )

    // --- the rest of what a player reads ---------------------------------------------------------

    let says = Page.says

    let history beholder (model: Model<Move, Session, Notice>) =
        let row (entry: Entry<Move, Notice>) =
            Elem.div
                [ Attr.class' "entry" ]
                [ quiet $"{entry.Ordinal}  turn {entry.Turn}"
                  Elem.span [ Attr.class' "asked" ] [ Text.enc $"{Words.player entry.Actor}: {Words.command entry.Asked}" ]
                  Elem.span [ Attr.class' "outcome" ] (entry.Told |> List.map (fun notice -> quiet (Render.wording notice))) ]

        match Journal.entries model.Journal with
        | [] -> Page.aside [ Elem.h2 [] [ Text.raw "The record" ]; quiet Render.nothingYet ]
        | entries ->
            Page.aside (
                [ Elem.h2 [] [ Text.raw "The record" ] ]
                @ (entries |> List.map row)
                @ [ quiet (Render.heading beholder (Model.state model)) ]
            )

    let rules = Page.aside [ Elem.h2 [] [ Text.raw "The rules" ]; lines Render.help ]

    let answer = Page.says Render.nothingToExplain

    let waiting (seats: Waiting list) =
        let standing (seat: Waiting) =
            Elem.div
                [ Attr.class' (if seat.Yours then "player yours" else "player") ]
                [ Elem.span [ Attr.class' "who" ] [ Text.enc (Words.seated seat.Yours seat.Player) ]
                  quiet (Render.Filling.standing seat) ]

        Page.screen
            [ Elem.h1 [] [ Text.enc Render.Filling.title ]
              block "The table" ((seats |> List.map standing) @ [ quiet (Render.Filling.stillToCome seats) ]) ]

    // --- what this game brings to a page -----------------------------------------------------------

    /// This game's own rules of drawing, and no more than that: the page itself - the
    /// chrome, the prompt, the door, the corner - is styled once at `Page`.
    ///
    /// Short, because there is one thing on this board that the page does not already know
    /// how to draw, and it is a square.
    let private sheet =
        """
.grid { display: inline-grid; gap: 4px; margin: .2rem 0; }
.grid .row { display: flex; gap: 4px; }
.cell {
  width: 4.5rem; height: 4.5rem; display: flex; align-items: center; justify-content: center;
  border: 1px solid var(--edge); border-radius: .4rem; background: var(--raised);
}
.cell .mark { font-size: 2.2rem; font-weight: 700; line-height: 1; }
.cell.x { border-color: var(--x); } .cell.x .mark { color: var(--x); }
.cell.o { border-color: var(--o); } .cell.o .mark { color: var(--o); }
.cell.free .types { width: 100%; height: 100%; border: none; background: transparent; font-size: 1.1rem; }
.cell.free:hover { border-color: var(--yours); }

.who.x { color: var(--x); } .who.o { color: var(--o); }
.player.yours .who { color: var(--yours); }
.entry .asked { min-width: 18ch; }
"""

    let shell =
        { Title = "Noughts and crosses"
          Sheet = sheet
          Placeholder = "click a square, or type its number - 1 to 9, or 'help'" }
