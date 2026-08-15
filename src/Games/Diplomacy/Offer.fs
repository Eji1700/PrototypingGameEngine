namespace TCModel.Diplomacy

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses - `Open`.
open TCModel.Diplomacy

/// This game, filled into both seams. One value, and it is the only thing the rest of the
/// program is handed.
///
/// Worth reading beside the other two, because the three of them together are the argument for
/// the seams being where they are. One has a map, hidden bags and a generator; one has nine
/// squares and nothing hidden at all; and this one has no chance in it whatsoever, seven seats,
/// three kinds of phase, and every player writing at the same time in secret. What they fill in
/// is the same record, and the timeline, the record on disk, the seats and their tokens, the
/// menu, the colour screen, the command line, the wire and the browser were all written once.
module Offer =

    // --- the engine's seam ------------------------------------------------------------------------

    /// Seven, and exactly seven. There is no variant here for a table of five, and there should
    /// not be: the standard map has seven home countries and the balance of the whole thing is
    /// built on all of them being played. A seat with nobody in it is given to the machine.
    let Seats = Power.Count

    let private deal players _ =
        if players = Seats then
            Ok Session.dealt
        else
            Error
                $"{players} players? Diplomacy takes {Seats}, one for each power - give the seats nobody is in to the machine with --rival."

    // --- what this game says is wrong with itself ----------------------------------------------------

    /// The board is three hundred-odd borders and an opening position typed out by hand, which
    /// is exactly the kind of thing this field exists for. Nothing above here could know what a
    /// well-formed map of Europe looks like; the game can, and does, before anybody sits down.
    let private faults = Atlas.problems @ Position.problems

    // --- the machines ----------------------------------------------------------------------------------

    let rec machine rival =
        Choosing(fun session -> Rival.plays session rival |> Option.map (fun (move, next) -> move, machine next))

    let private skill name =
        match Rival.byName name with
        | Ok skill -> Some skill
        | Error _ -> None

    // --- how it is drawn ---------------------------------------------------------------------------------

    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = fun _ asked model -> Render.answer asked model
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          // Wide, and it has to be: the map is seventeen hexes across at its widest, and an
          // order reads `bud s vie - tri` beside what became of it. The game of nine squares
          // asked for seventy-two and would look silly in this.
          Width = 138 }

    // --- and the whole of it -------------------------------------------------------------------------------

    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = fun _ -> Seats
              // Not one thing at this game is drawn, dealt or shuffled - it is the only one of
              // the three here with no chance in it at all, and the seed does nothing but tell
              // the machines how to break their ties. Saying so plainly is more honest than
              // reaching for a clock the rest of this program is careful not to touch.
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
