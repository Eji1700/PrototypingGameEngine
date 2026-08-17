namespace TCModel.Snake

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Snake

/// This game, filled into both seams. One value, and it is the only thing the rest of the
/// program is handed.
///
/// The one here that takes any number of seats from one to four, and it is worth having beside
/// the others for that: every other game in this program is a fixed table or a range it was
/// built around, and this one is the same rules whether it is a person alone with a score or
/// four snakes going round each other. Nothing above the seam is told which of those it is.
module Offer =

    // --- the engine's seam -----------------------------------------------------------------

    let private deal pace players seed =
        if players >= Session.Fewest && players <= Session.Most then
            Ok(Session.dealt pace players seed)
        else
            Error $"{players} players? Snake takes {Session.Fewest} to {Session.Most}, a snake each."

    // --- and the clock ------------------------------------------------------------------------

    /// How long a table leaves between beats, which is the whole of what makes this the arcade
    /// game rather than a game of turns.
    ///
    /// It quickens as the longest snake on the board eats, because a game that ran at one speed
    /// for ever would be a game with nothing to fear in it - and it stops quickening at a tenth
    /// of a second, which is about as fast as a person can still be said to be steering.
    [<Literal>]
    let private Slowest = 320

    [<Literal>]
    let private Quickest = 110

    [<Literal>]
    let private PerPiece = 8

    let private every session =
        let eaten =
            Session.play session
            |> Session.snakes
            |> List.map (fun (_, snake) -> snake.Eaten)
            |> function
                | [] -> 0
                | all -> List.max all

        System.TimeSpan.FromMilliseconds(float (max Quickest (Slowest - PerPiece * eaten)))

    /// Which key turns which snake, as the line it stands for.
    ///
    /// Four hands at one keyboard, which is what arcade multiplayer has always been: the arrows
    /// are Snake A, `wasd` is B, `ijkl` is C and the number pad is D. Every one of them sends a
    /// line naming its snake, so nothing here can steer somebody else's and nothing here is a
    /// second language - `b north` is a line a player could have typed, and at a table of four
    /// somebody often does.
    let private pressed (key: System.ConsoleKeyInfo) =
        let turning seat way = Some $"{seat} {way}"

        match key.Key with
        | System.ConsoleKey.UpArrow -> turning "a" "north"
        | System.ConsoleKey.LeftArrow -> turning "a" "west"
        | System.ConsoleKey.DownArrow -> turning "a" "south"
        | System.ConsoleKey.RightArrow -> turning "a" "east"
        | System.ConsoleKey.W -> turning "b" "north"
        | System.ConsoleKey.A -> turning "b" "west"
        | System.ConsoleKey.S -> turning "b" "south"
        | System.ConsoleKey.D -> turning "b" "east"
        | System.ConsoleKey.I -> turning "c" "north"
        | System.ConsoleKey.J -> turning "c" "west"
        | System.ConsoleKey.K -> turning "c" "south"
        | System.ConsoleKey.L -> turning "c" "east"
        | System.ConsoleKey.NumPad8 -> turning "d" "north"
        | System.ConsoleKey.NumPad4 -> turning "d" "west"
        | System.ConsoleKey.NumPad5 -> turning "d" "south"
        | System.ConsoleKey.NumPad6 -> turning "d" "east"
        | _ -> None

    // --- what this game says is wrong with itself --------------------------------------------

    /// Where a snake starts is worked out from its seat and the size of the table rather than
    /// written down, so what could be wrong with it is arithmetic - and arithmetic goes wrong.
    /// Every line below is a way a table could be dealt that could not be played.
    let private faults =
        [ if Board.Width < 2 * Snake.Length + 2 || Board.Height < Session.Most then
              yield $"a board {Board.Width} by {Board.Height}, too small to lay {Session.Most} snakes of {Snake.Length} out on"

          // Asked of one pace only, because where the snakes start has nothing to do with what
          // moves them: the two ways are dealt the same board and differ from the first move on.
          for players in Session.Fewest .. Session.Most do
              let dealt = Session.play (Session.dealt Turns players 0UL)

              let bodies = Session.snakes dealt |> List.collect (fun (_, snake) -> snake.Body)

              if bodies |> List.exists (Board.holds >> not) then
                  yield $"a table of {players} dealt with a snake hanging off the board"

              if List.distinct bodies |> List.length <> List.length bodies then
                  yield $"a table of {players} dealt with two snakes on the same square"

              if
                  Session.snakes dealt
                  |> List.exists (fun (_, snake) -> Snake.length snake <> Snake.Length)
              then
                  yield $"a table of {players} dealt a snake that is not {Snake.Length} long"

              // A snake with nowhere to go on the first turn is a table that deals somebody a
              // loss. Every one of them has to have at least one way out that is not back.
              for seat, snake in Session.snakes dealt do
                  let open' =
                      Direction.all
                      |> List.filter (fun way -> way <> Direction.opposite snake.Facing)
                      |> List.filter (fun way ->
                          match Turn.ahead seat way dealt with
                          | Wall
                          | Into _ -> false
                          | Food
                          | Clear -> true)

                  if List.isEmpty open' then
                      yield $"a table of {players} where {Words.player seat} is dealt with nowhere to go"

              if dealt.Food |> Option.forall Board.holds |> not then
                  yield $"a table of {players} dealt with its food off the board" ]

    // --- the machines ---------------------------------------------------------------------

    /// One of this game's rivals as the engine takes a machine. What choosing *is* is
    /// `Rival.plays` and is this game's; tying it into a machine that carries its own
    /// generator between turns is the same knot at every game, and is the engine's.
    let machine rival = Machines.choosing Rival.plays rival

    let private skill name = Rival.byName name |> Result.toOption

    // --- how it is drawn ----------------------------------------------------------------------

    let private scenes pace : Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = Render.answer
          Rules = Render.rules pace
          Waiting = Render.waiting
          Marking = Ink.marking
          // Wide enough for the board and its walls, the seats beside it and a line of the
          // log, and no wider.
          Width = 72 }

    // --- and the whole of it ------------------------------------------------------------------

    /// One way of playing this game, at one pace.
    ///
    /// The two are the same game and differ in five answers: what a line means, what a beat is,
    /// whether there is a clock, who the machines are, and what it is called. Everything else -
    /// the board, the rules of a step, the record, the screens - is written once and asked for
    /// twice, which is what says the two really are one game rather than two that look alike.
    let private way pace =
        { Rules =
            { Deal = deal pace
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = Session.seats
              // Out of the game's own generator rather than off the clock, so a game restarted
              // twice from the same record restarts the same way twice. The generator has
              // moved with every piece of food eaten, so a restart after a long game is a
              // different board from one after a short one - which is exactly right: it is the
              // game that was played, drawn on.
              Reseed = Session.reseed }

          Name = (if pace = Clock then "snake" else "snake-turns")
          Title = "Snake"
          Blurb =
            match pace with
            | Clock -> "The arcade game: the snakes move on their own and quicken as they eat, and you only steer."
            | Turns -> "The same board, a step at a time: it waits for you, and four can play it round one keyboard."
          Fewest = Session.Fewest
          Most = Session.Most

          Read = (if pace = Clock then Parse.racing else Parse.turning)
          Write = Words.command
          Seat = Words.player
          Says = Words.said
          SeenBy = Words.saidTo

          Resign = Some Resign
          Faults = faults
          Slots = Ink.slots

          // No machine on a clock, and the empty list is the honest answer rather than a gap.
          // The engine plays a machine's turns between one person's move and their next, and at
          // a table where nobody's turn it is there is no such gap to play them in - a rival
          // here would have to be steered by the beat, which is a thing to build rather than a
          // thing to pretend. At a game of turns it is the same three as ever.
          Skills =
            match pace with
            | Clock -> []
            | Turns -> Rival.all |> List.map (fun skill -> skill.Name, skill.Describe)

          Seating =
            fun seed sitting state ->
                match pace with
                | Clock -> []
                | Turns ->
                    ignore state

                    Rival.seating seed (sitting |> List.map (Option.bind skill))
                    |> List.map (fun (seat, rival) ->
                        seat,
                        { Skill = rival.Skill.Name
                          Plays = machine rival })

          // And the clock itself, which is the whole of what makes one of these the arcade game
          // and the other a game of turns.
          Pulse =
            match pace with
            | Turns -> None
            | Clock ->
                Some
                    { Every = every
                      Beat = Beat
                      Pressed = pressed }

          Page = Render.shell pace
          Views = Readers.views (scenes pace) }

    let playable: Playable<Move, Session, Notice> = way Clock

    /// Every way this game can be played, the plainest first - which here is the one everybody
    /// means by the name. The other is the same board with the clock taken out of it, and is
    /// where the machines live.
    let ways = [ playable; way Turns ]
