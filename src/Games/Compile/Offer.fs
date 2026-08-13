namespace TCModel.Compile

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// the command line's argument types carry names this game already uses.
open TCModel.Compile

/// This game, filled into both seams. One value, and it is the only thing the rest of the
/// program is handed.
module Offer =

    // --- the engine's seam ---------------------------------------------------------------------

    let private deal players seed =
        if players = Session.Seats then
            Ok(Session.dealt seed)
        else
            Error $"{players} players? Compile takes {Session.Seats}, sitting opposite each other."

    // --- what this game says is wrong with itself ------------------------------------------------

    /// A game built out of data can be built wrong, and nothing above here knows what
    /// well-formed would look like. What this game is built out of is twelve protocols, six
    /// cards apiece and a draft order, and every line below is one way those three could
    /// disagree with each other.
    let private faults =
        [ if List.distinct Protocol.all |> List.length <> List.length Protocol.all then
              yield "the same protocol listed twice"

          if List.length Protocol.all < Draft.Picks then
              yield $"{List.length Protocol.all} protocols, where a draft of {Draft.Picks} needs at least that many"

          if List.distinct Card.values |> List.length <> List.length Card.values then
              yield "the same number printed on two of a protocol's cards"

          if Lines.Count <> Protocol.Each then
              yield $"{Lines.Count} lines for {Protocol.Each} protocols, where each protocol wants a line of its own"

          if Deck.HandSize > Deck.Size then
              yield $"an opening hand of {Deck.HandSize} out of a deck of {Deck.Size}"

          if List.length Draft.order <> Protocol.Each * Session.Seats then
              yield
                  $"a draft of {List.length Draft.order} picks, where {Session.Seats} players taking {Protocol.Each} each makes {Protocol.Each * Session.Seats}"

          for seat in Session.seats do
              if Draft.picksBy seat <> Protocol.Each then
                  yield $"{Words.player seat} picking {Draft.picksBy seat} times in a draft where each takes {Protocol.Each}"

          if Draft.order |> List.exists (fun seat -> not (List.contains seat Session.seats)) then
              yield "a draft pick belonging to a seat nobody is in" ]

    // --- the machines ------------------------------------------------------------------------

    /// One of this game's rivals as the engine takes a machine: a function from where the game
    /// stands to what it plays, carrying its own generator inside it.
    let rec machine rival =
        Choosing(fun session -> Rival.plays session rival |> Option.map (fun (move, next) -> move, machine next))

    let private skill name =
        match Rival.byName name with
        | Ok skill -> Some skill
        | Error _ -> None

    // --- how it is drawn -----------------------------------------------------------------------

    let private scenes: Readers.Scenes<Move, Session, Notice> =
        { Board = Render.board
          History = Render.history
          Answer = Render.answer
          Rules = Render.rules
          Waiting = Render.waiting
          Marking = Ink.marking
          // Wide enough for three lines of stacked card names side by side, and the two seats
          // beside whatever the stage is asking for.
          Width = 96 }

    // --- and the whole of it -------------------------------------------------------------------

    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = deal
              Play = Turn.asked
              Active = Session.active
              Turn = Session.turn
              Over = Session.isOver
              Seats = fun _ -> Session.Seats
              Reseed = Session.reseed }

          Name = "compile"
          Title = "Compile"
          Blurb = "Draft three protocols, set them against three lines, and play across the table."
          Fewest = Session.Seats
          Most = Session.Seats

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
