namespace Prototyping.TicTacToe

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

module Offer =


    [<Literal>]
    let Seats = 2

    let private deal players _ =
        if players = Seats then
            Ok Session.dealt
        else
            Error $"{Commands.players players}? Noughts and crosses takes {Seats}."


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

          Seating = Playable.seating Rival.byName Rival.seating (fun rival -> rival.Skill.Name) Rival.plays

          Pulse = None


          Aside = None

          Steering = fun _ _ _ _ -> None

          Page = Render.shell
          Views = Readers.views scenes }

    let ways = [ playable ]
