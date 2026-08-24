namespace Prototyping.Snake

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Snake

module Offer =


    let private asked = Counting.several "player" "players"

    let private deal pace players seed =
        if players >= Session.Fewest && players <= Session.Most then
            Ok(Session.dealt pace players seed)
        else
            Error $"{asked players}? Snake takes {Session.Fewest} to {Session.Most}, a snake each."


    [<Literal>]
    let private Slackest = 420

    [<Literal>]
    let private PerNotch = 40

    [<Literal>]
    let private PerPiece = 8

    [<Literal>]
    let private Quickest = 50

    /// How long until the next beat. The speed notch is what a player sets, and the longest snake on
    /// the board quickens it further, so a game that has been going a while tightens on its own.
    let private every session =
        let play = Session.play session

        let eaten =
            Session.snakes play
            |> List.map (fun (_, snake) -> snake.Eaten)
            |> function
                | [] -> 0
                | all -> List.max all

        System.TimeSpan.FromMilliseconds(float (max Quickest (Slackest - PerNotch * play.Speed - PerPiece * eaten)))

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
        | System.ConsoleKey.OemPlus
        | System.ConsoleKey.Add -> Some "faster"
        | System.ConsoleKey.OemMinus
        | System.ConsoleKey.Subtract -> Some "slower"
        | _ -> None


    let private faults =
        [ if Board.Width < 2 * Snake.Length + 2 || Board.Height < Session.Most then
              yield $"a board {Board.Width} by {Board.Height}, too small to lay {Session.Most} snakes of {Snake.Length} out on"

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


    let machine rival = Machines.choosing Rival.plays rival

    let private skill name = Rival.byName name |> Result.toOption


    let private scenes pace : Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = Render.answer
          Rules = Render.rules pace
          Waiting = Render.waiting
          Marking = Ink.marking
          Width = 72 }


    let private way pace =
        { Rules =
            { Deal = deal pace
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = Session.seats
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

          Rings = fun _ -> []

          Resign = Some Resign
          Faults = faults
          Slots = Ink.slots

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

          Pulse =
            match pace with
            | Turns -> None
            | Clock ->
                Some
                    { Every = every
                      Beat = Beat

                      // A snake is on one square or the next and never between them, so there is
                      // nothing for a frame to catch.
                      Frames = fun _ -> 0

                      Pressed = pressed }

          Page = Render.shell pace
          Views = Readers.views (scenes pace) }

    let playable: Playable<Move, Session, Notice> = way Clock

    let ways = [ playable; way Turns ]
