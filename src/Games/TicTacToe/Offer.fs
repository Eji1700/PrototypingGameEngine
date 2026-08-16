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

    /// Every screen this game has, described once - and the three ways of reading one come
    /// back from `Readers` already written.
    ///
    /// This used to be forty lines and three files. What a board *is* has not changed; what
    /// has gone is saying it three times over and keeping the three in step by hand.
    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          // Nothing here is worked out, so there is nothing to ask about. The field is filled
          // rather than left, because a game that answered nothing at all would be a game
          // whose players learn that by being ignored.
          Answer = fun _ _ _ -> Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          // Wide enough for the board, the two seats beside it and a line of the log, and no
          // wider: this board is small, and a screen padded out to fill a terminal is a
          // screen with a lot of nothing in the middle of it.
          Width = 72 }

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

          Page = Render.shell
          Views = Readers.views scenes }

    /// Every way this game can be played, the plainest first.
    ///
    /// One, here. A game with an optional rule in it offers two and the Game page of the
    /// settings screen asks which - see [Compile](../Compile/Offer.fs). This is a list even
    /// where it holds one so that the door and the settings screen are the same at every game
    /// rather than nearly the same at most of them.
    let ways = [ playable ]
