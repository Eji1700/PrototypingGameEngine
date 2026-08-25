namespace Prototyping.Compile

open Prototyping.Common
open Prototyping.Engine
open Prototyping.Table
open Prototyping.Compile

module Offer =


    let private asked = Counting.several "player" "players"

    let private deal control players seed =
        if players = Session.Seats then
            Ok(Session.dealt control seed)
        else
            Error $"{asked players}? Compile takes {Session.Seats}, sitting opposite each other."


    let private faults =
        [ if List.distinct Protocol.all |> List.length <> List.length Protocol.all then
              yield "the same protocol listed twice"

          if List.length Protocol.all < Draft.Picks then
              yield $"{List.length Protocol.all} protocols, where a draft of {Draft.Picks} needs at least that many"

          if List.distinct Card.values |> List.length <> List.length Card.values then
              yield "the same number printed on two of a protocol's cards"

          for protocol in Protocol.all do
              if List.length (Card.inProtocol protocol) <> Card.PerProtocol then
                  yield
                      $"{Protocol.name protocol} with {List.length (Card.inProtocol protocol)} cards, where every protocol has {Card.PerProtocol}"

          if Lines.Count <> Protocol.Each then
              yield $"{Lines.Count} lines for {Protocol.Each} protocols, where each protocol wants a line of its own"

          if Deck.HandSize > Deck.Size then
              yield $"an opening hand of {Deck.HandSize} out of a deck of {Deck.Size}"

          if Placed.FaceDownValue > List.max Card.values then
              yield
                  $"a card face down worth {Placed.FaceDownValue}, where the best card printed is {List.max Card.values} - nothing would ever be played face up"

          if Placed.FaceDownValue < List.min Card.values then
              yield $"a card face down worth {Placed.FaceDownValue}, which is less than the worst card printed"

          if Stack.ToCompile <= List.max Card.values then
              yield $"a line compiled at {Stack.ToCompile}, which one card of {List.max Card.values} would reach on its own"

          for protocol in Protocol.all do
              let whole = Card.inProtocol protocol |> List.sumBy (fun card -> card.Value)

              if Stack.ToCompile > whole then
                  yield
                      $"a line compiled at {Stack.ToCompile}, which the whole of {Protocol.name protocol} at {whole} could not reach"

          if List.length Draft.order <> Protocol.Each * Session.Seats then
              yield
                  $"a draft of {List.length Draft.order} picks, where {Session.Seats} players taking {Protocol.Each} each makes {Protocol.Each * Session.Seats}"

          for seat in Session.seats do
              if Draft.picksBy seat <> Protocol.Each then
                  yield $"{Words.player seat} picking {Draft.picksBy seat} times in a draft where each takes {Protocol.Each}"

          if Draft.order |> List.exists (fun seat -> not (List.contains seat Session.seats)) then
              yield "a draft pick belonging to a seat nobody is in"

          if Field.LanesForControl > Lines.Count then
              yield $"a control component taken by leading {Field.LanesForControl} of {Lines.Count} lanes, which nobody could do"

          if Field.LanesForControl < 1 then
              yield $"a control component taken by leading {Field.LanesForControl} lanes, which everybody has always done"

          if List.length (Protocol.orders [ 1 .. Protocol.Each ]) < 2 then
              yield $"{Protocol.Each} protocols, which cannot be put in a different order than they are in"

          for card in Protocol.all |> List.collect Card.inProtocol do
              let text = Printed.on card

              let commands =
                  text.Shown
                  @ text.AtStart
                  @ text.AtEnd
                  @ text.WhenCovered
                  @ text.WhenFlipped
                  @ text.WhenCompiled
                  @ (text.After |> List.collect snd)

              let rules = text.Top @ text.Bottom

              let rec faulty =
                  function
                  | Draw(Just n) when n < 1 -> Some $"draws {n} cards"
                  | Opposing(Opposing _) -> Some "hands a command to the other player twice over"
                  | Opposing inner -> faulty inner
                  | _ -> None

              for wrong in commands |> List.choose faulty do
                  yield $"{Card.name card} {wrong}"

              if List.distinct rules |> List.length <> List.length rules then
                  yield $"{Card.name card} carries the same standing rule twice"

              if text.Top |> List.exists (fun rule -> List.contains rule text.Bottom) then
                  yield $"{Card.name card} carries one rule in both its top and its bottom box" ]


    let machine rival = Machines.choosing Rival.plays rival

    let private skill name = Rival.byName name |> Result.toOption


    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          Width = 96 }


    let private offering control : Playable<Move, Session, Notice> =
        { Rules =
            { Deal = deal control
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = fun _ -> Session.Seats
              Reseed = Session.reseed }

          Name = if control = NotInPlay then "compile" else "compile-control"
          Title = if control = NotInPlay then "Compile" else "Compile, with the control component"
          Blurb =
            if control = NotInPlay then
                "Draft three protocols, set them against three lines, and play across the table."
            else
                $"The same, with the optional rule: lead {Field.LanesForControl} lanes and take the component, then pay for it every time you compile or refresh."
          Fewest = Session.Seats
          Most = Session.Seats

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
          Steering = fun _ _ _ -> None

          Page = Render.shell
          Views = Readers.views scenes }

    let playable = offering NotInPlay

    let withControl = offering InTheMiddle

    let ways = [ playable; withControl ]
