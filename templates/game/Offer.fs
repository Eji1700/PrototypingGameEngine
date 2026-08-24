namespace Prototyping.MyGame

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

/// The seam. Everything above this file is how the game is played and how it is read; everything
/// below it is a table, a menu, a command line, a record, a browser and a wire, and none of that
/// knows what game it is carrying. This is the only file either side ever sees.
module Offer =


    [<Literal>]
    let Fewest = 2

    [<Literal>]
    let Most = 4

    let private asked = Counting.several "player" "players"

    let private deal players _ =
        if players >= Fewest && players <= Most then
            Ok(Round.dealt players)
        else
            Error $"{asked players}? This one takes {Fewest} to {Most}."


    /// What the game checks about itself before it will open at all. A board that cannot be played
    /// is worth refusing at the door rather than halfway through somebody's game.
    let private faults =
        [ if Row.Most < 1 then
              yield $"a row nobody may take from: at most {Row.Most} at a time"

          if Row.Dealt <= Row.Most then
              yield $"a row of {Row.Dealt} with {Row.Most} takeable at once, so the first move could end it" ]


    let private scenes: Readers.Scenes<Move, Round, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = fun _ _ _ -> Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          Width = 72 }


    let playable: Playable<Move, Round, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = Round.active
              Turn = Round.turn
              Over = Round.isOver
              Seats = Round.seats

              // No chance in it anywhere, so the seed is never drawn from and a restart says so.
              Reseed = fun _ -> 0UL }

          Name = "mygame"
          Title = "MyGame"
          Blurb = "A row of tokens, taken one to three at a time, and whoever takes the last one wins."
          Fewest = Fewest
          Most = Most

          Read = Parse.line
          Write = Words.command
          Seat = Words.player
          Says = Words.said
          SeenBy = Words.saidTo

          // Nothing worth hearing yet. A game that wants a sound reads one off the state here,
          // rather than out of its notices, so a replayed table sounds like a played one.
          Rings = fun _ -> []

          Resign = Some Resign
          Faults = faults
          Slots = Ink.slots

          // No machines yet. To add one, write a `Rival.fs` under `Rules/` that picks a `Move` from
          // what a seat can see, and fill these two in - see `src/Games/TicTacToe/Rules/Rival.fs`.
          Skills = []
          Seating = fun _ _ _ -> []

          // No clock. A game whose board moves on its own fills this in instead - see
          // `src/Games/Life/Offer.fs`, where a beat is a move.
          Pulse = None

          Page = Render.shell
          Views = Readers.views scenes }

    let ways = [ playable ]
