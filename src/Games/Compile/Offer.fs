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

    let private deal control players seed =
        if players = Session.Seats then
            Ok(Session.dealt control seed)
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

          // Every protocol has six cards, and every one of them skips exactly one of the seven
          // numbers - twelve skip the 6, and the three that carry one give up a number lower
          // down. A protocol that skipped none, or two, would deal a deck of the wrong size.
          for protocol in Protocol.all do
              if List.length (Card.inProtocol protocol) <> Card.PerProtocol then
                  yield
                      $"{Protocol.name protocol} with {List.length (Card.inProtocol protocol)} cards, where every protocol has {Card.PerProtocol}"

          if Lines.Count <> Protocol.Each then
              yield $"{Lines.Count} lines for {Protocol.Each} protocols, where each protocol wants a line of its own"

          if Deck.HandSize > Deck.Size then
              yield $"an opening hand of {Deck.HandSize} out of a deck of {Deck.Size}"

          // What a card is worth face down is the one number in this game that is not printed
          // on anything, and it is the balance of the whole thing: too high and there is never
          // a reason to play face up, too low and a hand of the wrong protocol is a dead hand.
          if Placed.FaceDownValue > List.max Card.values then
              yield
                  $"a card face down worth {Placed.FaceDownValue}, where the best card printed is {List.max Card.values} - nothing would ever be played face up"

          if Placed.FaceDownValue < List.min Card.values then
              yield $"a card face down worth {Placed.FaceDownValue}, which is less than the worst card printed"

          // And the other number nothing is printed with. Between them these two are the whole
          // balance of the game before a single card has any text on it, which is why they are
          // checked rather than trusted.
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

          // The optional rule's own two numbers. Checked whether or not it is being played,
          // because both games are built from this list and a fault in one is a fault in both.
          if Field.LanesForControl > Lines.Count then
              yield $"a control component taken by leading {Field.LanesForControl} of {Lines.Count} lanes, which nobody could do"

          if Field.LanesForControl < 1 then
              yield $"a control component taken by leading {Field.LanesForControl} lanes, which everybody has always done"

          // What the component costs is a *different* order, so there has to be one to move to.
          if List.length (Protocol.orders [ 1 .. Protocol.Each ]) < 2 then
              yield $"{Protocol.Each} protocols, which cannot be put in a different order than they are in"

          // --- and what is printed on the cards -------------------------------------------
          //
          // The reason card text is data rather than functions. Seventy-two cards typed in by
          // hand is exactly the "game built out of data that can be built wrong" the seam is
          // for, and only the game knows what well-formed looks like. A closure could not be
          // asked any of this.
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

              // A card with the same rule twice is a card somebody typed twice.
              if List.distinct rules |> List.length <> List.length rules then
                  yield $"{Card.name card} carries the same standing rule twice"

              // And the same rule in both boxes at once, which is a card that would go on
              // applying after being covered and is therefore a bottom box doing nothing.
              if text.Top |> List.exists (fun rule -> List.contains rule text.Bottom) then
                  yield $"{Card.name card} carries one rule in both its top and its bottom box" ]

    // --- the machines ------------------------------------------------------------------------

    /// One of this game's rivals as the engine takes a machine. What choosing *is* is
    /// `Rival.plays` and is this game's; tying it into a machine that carries its own
    /// generator between turns is the same knot at every game, and is the engine's.
    let machine rival = Machines.choosing Rival.plays rival

    let private skill name = Rival.byName name |> Result.toOption

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

    /// This game, with or without the control component - and it is one function rather than
    /// two records, because everything but three fields is the same.
    ///
    /// An optional rule ships as a second `Playable` rather than as a parameter, because
    /// `Rules.Deal` takes a player count and a seed and should keep taking exactly that: a
    /// game's options are not the engine's business, and a third argument would touch every
    /// table in the program. `Rules.fs` says why this works - *a game is a value here, and two
    /// of them can sit side by side in one process.*
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

    /// The game as it is usually played.
    let playable = offering NotInPlay

    /// And the same game with the optional rule in it, which is a component sitting between the
    /// players until somebody leads two lanes.
    let withControl = offering InTheMiddle

    /// Every way this game can be played, the plainest first - and the only game here with
    /// more than one.
    ///
    /// Two entries rather than two games, which is the change worth writing down. They were
    /// two entries in the list of games for a while, so the program with all four in it offered
    /// five and the picker asked twice about the same game. They are one game with two ways of
    /// being played now, and which way is asked at the Game page of this game's own settings.
    ///
    /// What has *not* changed is that each way keeps a name of its own. A game with the
    /// optional rule in it is a different game - different deal, different reckoning - and its
    /// record says so on the deal line, so a saved game is taken back up exactly as it was
    /// played whatever the settings were left saying. A setting that could quietly change what
    /// a record replayed into would not be a setting, it would be a bug with a screen in front
    /// of it.
    let ways = [ playable; withControl ]
