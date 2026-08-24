namespace Prototyping.Diplomacy

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Diplomacy

module Offer =


    let Seats = Power.Count

    let private asked = Counting.several "player" "players"

    let private deal players _ =
        if players = Seats then
            Ok Session.dealt
        else
            Error
                $"{asked players}? Diplomacy takes {Seats}, one for each power - give the seats nobody is in to the machine with --rival."


    let private faults = Atlas.problems @ Position.problems


    let machine rival = Machines.choosing Rival.plays rival

    let private skill name = Rival.byName name |> Result.toOption


    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = fun _ asked model -> Render.answer asked model
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

          Seating =
            fun seed sitting _ ->
                Rival.seating seed (sitting |> List.map (Option.bind skill))
                |> List.map (fun (seat, rival) ->
                    seat,
                    { Skill = rival.Skill.Name
                      Plays = machine rival })

          Pulse = None


          Page = Render.shell
          Views = Readers.views scenes }

    let ways = [ playable ]
