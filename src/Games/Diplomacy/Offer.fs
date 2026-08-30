namespace Prototyping.Diplomacy

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

module Offer =


    let Seats = Power.Count

    let private deal players _ =
        if players = Seats then
            Ok Session.dealt
        else
            Error
                $"{Commands.players players}? Diplomacy takes {Seats}, one for each power - give the seats nobody is in to the machine with --rival."


    let private faults = Atlas.problems @ Position.problems


    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          Width = 138 }


    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = fun _ -> Seats
              Reseed = fun _ -> 0UL }

          Name = "diplomacy"
          Title = "Diplomacy"
          Blurb = "Seven powers, thirty-four centres, no dice - and everybody writes at once."
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
