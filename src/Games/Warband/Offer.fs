namespace Prototyping.Warband

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table

/// The seam. Everything above this file is how the game is played and how it is read; everything
/// below it is a table, a menu, a command line, a record, a browser and a wire, and none of that
/// knows what game it is carrying.
module Offer =

    [<Literal>]
    let Seats = 2

    /// How long the table leaves between blows. A beat is a move, so this is the only thing in the
    /// game that knows about real time, and nothing that follows from it reaches the rules: a
    /// battle folded out by hand is the same battle, and the same record.
    let private every (_: Play) = System.TimeSpan.FromMilliseconds 600.0

    /// A key stands for a line the game already reads, so nothing can be pressed that could not
    /// have been typed.
    let private pressed (key: System.ConsoleKeyInfo) =
        match key.Key with
        | System.ConsoleKey.Spacebar
        | System.ConsoleKey.P -> Some "run"
        | System.ConsoleKey.OemPeriod -> Some "step"
        | _ -> None

    let private asked = Counting.several "player" "players"

    let private deal players _ =
        if players = Seats then
            Ok Session.dealt
        else
            Error $"{asked players}? A warband is two squads, so it takes {Seats}."


    /// What the board is sounding, read off where the game stands rather than out of what it said -
    /// which is what makes a battle taken up from a record sound like the one it was saved from. A
    /// blow is a tap because there are thirty of them; the muster rings for whoever is waited on.
    let private rings play =
        match play.Stage with
        | Mustering _ -> [ Ready ]
        | Fighting _ -> [ Tap ]
        | Ended _ -> [ Fanfare ]


    /// What the game checks about itself before it will open at all. Most of it is about the
    /// formation, since a hex grid that is wrong about what touches what is a game that looks
    /// right and plays wrong.
    let private faults =
        [ if List.length Formation.hexes <> 10 then
              yield $"{List.length Formation.hexes} hexes, where three ranks of 3, 4 and 3 make ten"

          if
              Formation.hexes
              |> List.exists (fun hex -> Formation.read (Formation.name hex) <> Some hex)
          then
              yield "a hex whose name does not read back as the hex it was drawn on"

          if
              Formation.hexes
              |> List.exists (fun hex -> Formation.touches hex |> List.contains hex)
          then
              yield "a hex that touches itself"

          if
              Formation.hexes
              |> List.exists (fun hex ->
                  Formation.touches hex
                  |> List.exists (fun other -> not (Formation.touches other |> List.contains hex)))
          then
              yield "a hex that touches one that does not touch it back"

          if
              Formation.hexes
              |> List.exists (fun hex -> Formation.touches hex |> List.exists (Formation.holds >> not))
          then
              yield "a hex touching one that is not on the formation"

          match Formation.hexes |> List.map (Formation.touches >> List.length) |> List.max with
          | most when most <> 6 -> yield $"a formation whose fullest hex touches {Words.hexes most}, where a hex has six sides"
          | _ -> ()

          if
              Formation.hexes
              |> List.exists (fun hex ->
                  hex.Rank = Front
                  && Formation.touches hex |> List.exists (fun other -> other.Rank = Back))
          then
              yield "a front rank touching the back one, with the middle rank between them"

          if Squad.Strong > List.length Formation.hexes then
              yield $"{Words.units Squad.Strong} to a squad on {Words.hexes (List.length Formation.hexes)}"

          if Squad.Alike * List.length Kinds.all < Squad.Strong then
              yield
                  $"{Words.units Squad.Strong} to muster, where {List.length Kinds.all} kinds {Squad.Alike} at a time cannot fill one"

          if Kinds.all |> List.exists (fun kind -> Kinds.vigour kind < 1) then
              yield "a kind of unit that is down before the battle starts"

          if Session.Closest < 1 || Session.Furthest < Session.Closest then
              yield $"ground running from {Session.Closest} to {Session.Furthest} hexes, which is no range at all"

          if not (Session.groundHolds Session.dealt.Engaged) then
              yield $"a deal standing the lines {Words.hexes Session.dealt.Engaged} apart, which is off the range they may take"

          // A kind that cannot reach across the ground a game is dealt at is a kind nobody would
          // ever muster, which is a hole in the roster rather than a choice in it.
          for kind in Kinds.all do
              if
                  not (
                      Formation.ranks
                      |> List.exists (fun rank -> Kinds.carries Session.Closest (Kinds.stance rank kind))
                  )
              then
                  yield $"a {Kinds.name kind} that reaches nothing from any rank, even with the lines touching"

          if
              List.distinct (Kinds.all |> List.map Kinds.name) |> List.length
              <> List.length Kinds.all
          then
              yield "two kinds of unit with one name between them"

          if
              Kinds.all
              |> List.exists (fun kind -> Kinds.byName (Kinds.name kind) <> Some kind)
          then
              yield "a kind whose name does not read back as itself"

          if
              Kinds.all
              |> List.forall (fun kind ->
                  Formation.ranks
                  |> List.forall (fun rank -> Kinds.stance rank kind = Kinds.stance Front kind))
          then
              yield "no kind that does anything different for the rank it stands in, which is the whole game"

          // The machine musters to a written-out plan rather than a search, so a plan with a hex
          // twice in it or six units in it is a machine that stops halfway through its muster.
          for plan in Rival.plans do
              if List.length plan <> Squad.Strong then
                  yield $"a machine's plan of {Words.units (List.length plan)}, where a squad takes {Words.units Squad.Strong}"

              if List.distinct (plan |> List.map snd) |> List.length <> List.length plan then
                  yield "a machine's plan that musters twice onto one hex"

              if plan |> List.map snd |> List.exists (Formation.holds >> not) then
                  yield "a machine's plan that musters onto a hex that is not there"

              for kind in Kinds.all do
                  if plan |> List.filter (fst >> (=) kind) |> List.length > Squad.Alike then
                      yield $"a machine's plan with more than {Words.units Squad.Alike} of one kind in it" ]


    let machine rival = Machines.choosing Rival.plays rival

    let private skill name = Rival.byName name |> Result.toOption


    let private scenes: Readers.Scenes<Move, Play, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          Width = 84 }


    let playable: Playable<Move, Play, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = Session.seats

              // No chance in it anywhere. The deal is two empty formations and the battle is
              // settled the moment the tenth unit is placed, so the seed is never drawn from.
              Reseed = fun _ -> 0UL }

          Name = "warband"
          Title = "Warband"
          Blurb =
            "Two squads of five on ten hexes apiece, mustered out of each other's sight - and then a battle neither of you plays."
          Fewest = Seats
          Most = Seats

          Read = Parse.line
          Write = Words.command
          Seat = Words.player
          Says = Words.said
          SeenBy = Words.saidTo

          Rings = rings

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

          Pulse =
            Some
                { Every = every
                  Beat = Beat

                  // A blow has landed or it has not, and there is nothing in between two of them
                  // worth drawing.
                  Frames = fun _ -> 0

                  Pressed = pressed }

          Page = Render.shell
          Views = Readers.views scenes }

    let ways = [ playable ]
