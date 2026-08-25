namespace Prototyping.TicTacToe

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table
open Prototyping.TicTacToe

module Offer =


    [<Literal>]
    let Seats = 2

    let private asked = Counting.several "player" "players"

    let private deal players _ =
        if players = Seats then
            Ok Session.dealt
        else
            Error $"{asked players}? Noughts and crosses takes {Seats}."


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


    let machine rival = Machines.choosing Rival.plays rival

    let private skill name = Rival.byName name |> Result.toOption


    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = fun _ _ _ -> Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          Width = 72 }


    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = fun _ -> Seats
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

          Rings = fun _ -> []

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

          Pulse = None


          // Nothing but a board on offer, so no section of the menu belongs to this game.
          Aside = None

          // Nothing to steer: this board is typed at, and every line it takes is one somebody wrote.
          Steering = fun _ _ _ _ -> None

          Page = Render.shell
          Views = Readers.views scenes }

    let ways = [ playable ]
