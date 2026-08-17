namespace TCModel.Life

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Life

/// This game, filled into both seams. One value, and it is the only thing the rest of the
/// program is handed.
///
/// Worth reading beside the others, because it is the one that argues with them. They are
/// games in the ordinary sense: two or more people, turns taken in order, somebody wins. This
/// one has a single seat, no opponent, no winning, and a position that changes because a rule
/// says so rather than because anybody chose it - and it fills in the same two records,
/// unchanged, and gets the timeline, the record on disk, the replay, the seats, the menu, the
/// command line, the wire and all three screens for nothing.
module Offer =

    // --- the engine's seam -----------------------------------------------------------------

    /// One, and exactly one. Every seam above here takes a count and this is the count they
    /// were never given before: a game of one is what says whether "how many may play" is
    /// really the game's own answer or two of somebody else's.
    [<Literal>]
    let Seats = 1

    let private deal players seed =
        if players = Seats then
            Ok(World.dealt seed)
        else
            Error $"{players} players? Life is played by nobody - there is one seat at it, for whoever is watching."

    // --- what this game says is wrong with itself --------------------------------------------

    /// The board is worked out from its two sides rather than written down, so what could be
    /// wrong with it is arithmetic - and arithmetic goes wrong too. Every line below is a way
    /// the board and the rule could disagree with each other before anybody sits down.
    let private faults =
        [ if Grid.Width < 3 || Grid.Height < 3 then
              yield $"a board {Grid.Width} by {Grid.Height}, small enough that joining the edges makes a cell its own neighbour"

          if Grid.Width > String.length Grid.letters then
              yield $"{Grid.Width} columns, where the letters they are named by run out at {String.length Grid.letters}"

          if List.length Grid.all <> Grid.Width * Grid.Height then
              yield $"{List.length Grid.all} squares on a board of {Grid.Width} by {Grid.Height}"

          if
              Grid.all
              |> List.exists (fun cell -> List.length (List.distinct (Grid.neighbours cell)) <> 8)
          then
              yield "a cell with something other than eight neighbours"

          if Grid.all |> List.exists (fun cell -> Grid.neighbours cell |> List.contains cell) then
              yield "a cell that is its own neighbour"

          if
              Grid.all
              |> List.exists (fun cell -> Grid.neighbours cell |> List.exists (Grid.holds >> not))
          then
              yield "a neighbour off the board, where joining the edges should have brought it back on"

          // The names are the moves. A cell whose name did not read back as itself would be a
          // cell a player could see and not type.
          if Grid.all |> List.exists (fun cell -> Grid.read (Grid.name cell) <> Some cell) then
              yield "a cell whose name does not read back as the cell it was drawn on"

          if World.Density < 1 || World.Density > 99 then
              yield $"a deal filling {World.Density} squares in a hundred, which is not a soup" ]

    // --- the machines ---------------------------------------------------------------------
    //
    // None, and the empty list is the honest answer rather than a gap. A machine here is a
    // thing that would sit in the one seat and type `step` for ever - and it would: a glider on
    // a board with joined edges neither dies nor settles, so the run between one prompt and the
    // next would never come back. The rule already plays this game. What the person at the
    // keyboard does is decide when to let it, which is not a thing to hand to the program.

    // --- how it is drawn ----------------------------------------------------------------------

    /// Every screen this game has, described once - and the three ways of reading one come
    /// back from `Readers` already written.
    let private scenes: Readers.Scenes<Move, World, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = fun _ asked model -> Render.answer asked model
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          // Wide enough for the board, the blocks under it and a line of the log. The board is
          // twenty-six characters and a row number, and a screen padded out past what is on it
          // is a screen with a lot of nothing down the middle.
          Width = 72 }

    // --- and the whole of it ------------------------------------------------------------------

    let playable: Playable<Move, World, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = World.active
              // The generation *is* the turn here, and saying so rather than counting moves is
              // what makes a record read the way the game does: two cells turned on and a run
              // of ten are three lines of the record, and the first two happened at the same
              // moment of the world.
              Turn = World.turn
              // Never, and the flat answer is the honest one. `Over` is what stops the engine
              // taking moves and stops a table waiting on anybody, and there is no position
              // here that should do either: a board the rule has run out on - settled, or
              // empty - is still a board to draw on, and drawing a glider on an empty grid and
              // letting it go is half of what this game is for. What the rule has nothing left
              // to do is something this game *says*, on the screen and in the log, which is a
              // different thing from the game being finished.
              Over = fun _ -> false
              Seats = fun _ -> Seats
              // Out of the game's own generator rather than off the clock, so a game restarted
              // twice from the same record restarts the same way twice.
              Reseed = World.reseed }

          Name = "life"
          Title = "Life"
          Blurb = "Conway's, on a board with its edges joined: a soup, a rule, and nobody to play against."
          Fewest = Seats
          Most = Seats

          Read = Parse.line
          Write = Words.command
          Seat = Words.player
          Says = Words.said
          SeenBy = Words.saidTo

          // There is nobody to resign to and nothing to give up. A game that answered `resign`
          // with an ending would be a game inventing an opponent for the sake of a verb.
          Resign = None
          Faults = faults
          Slots = Ink.slots
          Skills = []
          Seating = fun _ _ _ -> []

          Page = Render.shell
          Views = Readers.views scenes }

    /// Every way this game can be played, the plainest first. One, here.
    let ways = [ playable ]
