namespace TCModel.TicTacToe

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and the command line's argument types carry names this game already uses - `Open`.
open TCModel.TicTacToe

/// This game, filled into both seams. One value, and it is the only thing the rest of the
/// program is handed.
///
/// Worth reading beside `TCModel.Turncoats.Offer`, because the two are the whole argument for
/// the seams being where they are: one game has a map, hidden bags, a generator, four kinds
/// of move and three ways of being won; the other has nine squares. What they fill in is the
/// same record, and everything above it - the timeline, the record on disk, the seats and
/// their tokens, the menu, the colour screen, the command line, the wire - was written once.
module Offer =

    // --- the engine's seam -----------------------------------------------------------------

    /// Two, and exactly two. Which is itself worth having: how many may play used to be a
    /// number the menu and the command line each knew, and a game of two is what says whether
    /// they have really stopped knowing it.
    [<Literal>]
    let Seats = 2

    let private deal players _ =
        if players = Seats then
            Ok Session.dealt
        else
            Error $"{players} players? Noughts and crosses takes {Seats}."

    // --- what this game says is wrong with itself --------------------------------------------

    /// The board is worked out from `Squares.Side` rather than written down, so what could be
    /// wrong with it is arithmetic rather than a typo - but arithmetic goes wrong too, and
    /// this is where a game says so before anybody sits down to one.
    let private faults =
        let lines = Squares.lines

        [ if lines |> List.exists (fun line -> List.length line <> Squares.Side) then
              yield $"a winning line that is not {Squares.Side} squares long"

          if lines |> List.collect id |> List.exists (Squares.holds >> not) then
              yield "a winning line running off the board"

          if List.length lines <> 2 * Squares.Side + 2 then
              yield $"{List.length lines} winning lines, where a board of {Squares.Side} has {2 * Squares.Side + 2}"

          if List.distinct lines |> List.length <> List.length lines then
              yield "the same winning line written down twice" ]

    // --- the machines ---------------------------------------------------------------------

    /// One of this game's rivals as the engine takes a machine: a function from where the
    /// game stands to what it plays, carrying its own generator inside it.
    let rec machine rival =
        Choosing(fun session -> Rival.plays session rival |> Option.map (fun (move, next) -> move, machine next))

    let private skill name =
        match Rival.byName name with
        | Ok skill -> Some skill
        | Error _ -> None

    // --- how it is drawn ----------------------------------------------------------------------

    let private views palette =
        [ { Name = "plain"
            Describe = "plain text, and nothing this terminal has to understand"
            Shown = AtATerminal
            Palette = palette
            Board = Render.model
            History = Render.history
            // Nothing here is worked out, so there is nothing to ask about. The field is
            // filled rather than left, because a game that answered nothing at all would be
            // a game whose players learn that by being ignored.
            Answer = fun _ _ -> Render.nothingToExplain
            Rules = Render.help
            Says = id
            Waiting = Render.waiting }

          { Name = "rich"
            Describe = "walled squares and colour, for a terminal that can show them"
            Shown = AtATerminal
            Palette = palette
            Board = Rich.board palette
            History = Rich.history palette
            Answer = fun _ _ -> Rich.answer palette
            Rules = Rich.rules palette
            Says = Ink.paint palette
            Waiting = Rich.waiting palette }

          // This one takes no palette into its endpoints, and it is the only one that does
          // not. A page carries its colours in its own head - `Page.page` writes them there
          // as custom properties - and every fragment draws in those, so one board serves
          // however many people are reading it.
          { Name = "html"
            Describe = "a page with nine buttons on it, for a player reading in a browser"
            Shown = InABrowser
            Palette = palette
            Board = Html.board
            History = Html.history
            Answer = fun _ _ -> Html.answer
            Rules = Html.rules
            Says = Html.says
            Waiting = Html.waiting } ]

    // --- and the whole of it ------------------------------------------------------------------

    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = fun _ -> Seats
              // Nothing at this game is drawn, dealt or shuffled, so there is no next seed
              // to draw: every deal is the same deal. Saying so plainly is more honest than
              // reaching for a clock the rest of this program is careful not to touch.
              Reseed = fun _ -> 0UL }

          Name = "tictactoe"
          Title = "Noughts and crosses"
          Blurb = "Nine squares, three in a row, and nothing hidden."
          Fewest = Seats
          Most = Seats

          Read = Parse.line
          Write = Words.command
          Seat = Words.player
          Says = Words.said
          SeenBy = Words.saidTo

          Resign = Some Resign
          Faults = faults
          Slots = Ink.slots
          Skills = Rival.all |> List.map (fun skill -> skill.Name, skill.Describe)

          Seating =
            fun seed sitting _ ->
                Rival.seating seed (sitting |> List.map (Option.bind skill))
                |> List.map (fun (seat, rival) ->
                    seat,
                    { Skill = rival.Skill.Name
                      Plays = machine rival })

          Page = Html.shell
          Views = views }
