namespace Prototyping.Life

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

module Offer =


    [<Literal>]
    let Seats = 1

    /// How long until the next beat. The notch is what a player winds; a board that is stopped
    /// or has run out is beaten just the same and answers with nothing, which costs a step of the
    /// rule and no line anywhere.
    [<Literal>]
    let private Slackest = 560

    [<Literal>]
    let private PerNotch = 50

    let private every world =
        System.TimeSpan.FromMilliseconds(float (Slackest - PerNotch * world.Speed))

    /// A key stands for a line this game already reads, so nothing can be pressed that could not
    /// have been typed. `p` is the one that matters: it starts and stops the rule.
    let private pressed (key: System.ConsoleKeyInfo) =
        match key.Key with
        | System.ConsoleKey.P -> Some "run"
        | System.ConsoleKey.OemPeriod -> Some "step"
        | System.ConsoleKey.C -> Some "clear"
        | _ -> Commands.winding key

    let private deal players seed =
        if players = Seats then
            Ok(World.dealt seed)
        else
            Error $"{Commands.players players}? Life is played by nobody - there is one seat at it, for whoever is watching."


    let private faults =
        [ if Torus.Width < 3 || Torus.Height < 3 then
              yield $"a board {Torus.Width} by {Torus.Height}, small enough that joining the edges makes a cell its own neighbour"

          yield! Grid.faults Torus.grid

          if
              Torus.all
              |> List.exists (fun cell -> List.length (List.distinct (Torus.neighbours cell)) <> 8)
          then
              yield "a cell with something other than eight neighbours"

          if
              Torus.all
              |> List.exists (fun cell -> Torus.neighbours cell |> List.contains cell)
          then
              yield "a cell that is its own neighbour"

          if
              Torus.all
              |> List.exists (fun cell -> Torus.neighbours cell |> List.exists (Torus.holds >> not))
          then
              yield "a neighbour off the board, where joining the edges should have brought it back on"

          if World.Density < 1 || World.Density > 99 then
              yield $"a deal filling {World.Density} squares in a hundred, which is not a soup" ]


    let private scenes: Readers.Scenes<Move, World, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = fun _ asked model -> Render.answer asked model
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          Width = 72 }


    let playable: Playable<Move, World, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = World.active
              Turn = World.turn
              Over = fun _ -> false
              Seats = fun _ -> Seats
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

          Rings = fun _ -> []

          Resign = None
          Faults = faults
          Slots = Ink.slots
          Skills = []
          Seating = fun _ _ _ -> []

          Pulse =
            Some
                { Every = every
                  Beat = Beat

                  // A generation is on the board or it is not, and there is nothing in between
                  // two of them to draw.
                  Frames = fun _ -> 0

                  Pressed = pressed

                  // Every line typed at this board is a steer, so nobody is ever waited for.
                  Free = fun _ -> true }

          Aside = None

          Steering = fun _ _ _ _ -> None

          Page = Render.shell
          Views = Readers.views scenes }

    let ways = [ playable ]
