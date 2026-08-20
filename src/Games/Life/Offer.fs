namespace TCModel.Life

open TCModel.Engine
open TCModel.Table
open TCModel.Life

module Offer =


    [<Literal>]
    let Seats = 1

    let private deal players seed =
        if players = Seats then
            Ok(World.dealt seed)
        else
            Error $"{players} players? Life is played by nobody - there is one seat at it, for whoever is watching."


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

          if Grid.all |> List.exists (fun cell -> Grid.read (Grid.name cell) <> Some cell) then
              yield "a cell whose name does not read back as the cell it was drawn on"

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

          Resign = None
          Faults = faults
          Slots = Ink.slots
          Skills = []
          Seating = fun _ _ _ -> []

          Pulse = None


          Page = Render.shell
          Views = Readers.views scenes }

    let ways = [ playable ]
