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

    let private deal players seed =
        if players >= Session.Fewest && players <= Session.Most then
            Ok(Session.dealt players seed)
        else
            Error $"{players} players? Snake takes {Session.Fewest} to {Session.Most}, a snake each."

    // --- what this game says is wrong with itself --------------------------------------------

    /// Where a snake starts is worked out from its seat and the size of the table rather than
    /// written down, so what could be wrong with it is arithmetic - and arithmetic goes wrong.
    /// Every line below is a way a table could be dealt that could not be played.
    let private faults =
        [ if Board.Width < 2 * Snake.Length + 2 || Board.Height < Session.Most then
              yield $"a board {Board.Width} by {Board.Height}, too small to lay {Session.Most} snakes of {Snake.Length} out on"

          for players in Session.Fewest .. Session.Most do
              let dealt = Session.play (Session.dealt players 0UL)

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

    /// One of this game's rivals as the engine takes a machine: a function from where the game
    /// stands to what it plays, carrying its own generator inside it.
    let rec machine rival =
        Choosing(fun session -> Rival.plays session rival |> Option.map (fun (move, next) -> move, machine next))

    let private skill name =
        match Rival.byName name with
        | Ok skill -> Some skill
        | Error _ -> None

    // --- how it is drawn ----------------------------------------------------------------------

    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          // Wide enough for the board and its walls, the seats beside it and a line of the
          // log, and no wider.
          Width = 72 }

    // --- and the whole of it ------------------------------------------------------------------

    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = deal
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

          Name = "snake"
          Title = "Snake"
          Blurb = "One square a turn, eat the star, and do not run into anything. Alone, or up to four at once."
          Fewest = Session.Fewest
          Most = Session.Most

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

    /// Every way this game can be played, the plainest first. One, here.
    let ways = [ playable ]
