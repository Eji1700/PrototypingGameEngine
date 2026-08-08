namespace TCModel.Turncoats

open TCModel.Common
open TCModel.Engine
open TCModel.Table

/// This game, filled into both seams: `Rules` for how it is played, `Playable` for how it is
/// read. One value, and it is the only thing the rest of the program is handed.
///
/// Everything above here - the timeline, the record on disk, the seats and their tokens, the
/// machine loop, the menu, the seat list, the colour screen, the command line, the wire and
/// the browser - is already written and already generic. What follows is the whole of what a
/// game has to answer to get all of it.
module Offer =

    // --- the engine's seam, and this game's own reading of a seat --------------------------

    let private refused =
        function
        | TooFewPlayers n -> $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
        | TooManyPlayers n -> $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."

    /// The player at a seat, for the views - which draw a hand and a bag and so want the
    /// player rather than the seat. A seat that is not at this table cannot happen: the table
    /// only ever asks about seats the game dealt.
    let private at seat model =
        Game.tryPlayer seat (Playing.game model)
        |> Option.defaultValue (Game.active (Playing.game model))

    // --- the machines ---------------------------------------------------------------------

    /// One of this game's rivals as the engine takes a machine: a function from where the
    /// game stands to what it plays, carrying its own generator inside it.
    ///
    /// Public because a check may want to seat a skill that is not one of the three on offer
    /// - `Seating` below goes by the names a person can type, and a machine written to lose
    /// on purpose has no name to type.
    let rec machine rival =
        Choosing(fun session ->
            match session with
            | InPlay play -> Rival.plays play rival |> Option.map (fun (move, next) -> move, machine next)
            | Finished _ -> None)

    let private skill name =
        match Rival.byName name with
        | Ok skill -> Some skill
        | Error _ -> None

    // --- how it is drawn -------------------------------------------------------------------

    /// A `rule` question, answered by whichever view was asked. The words arrived as they
    /// were typed, so the region is read back here - and a region the board has not got is
    /// said in the same voice the view says everything else in.
    let private answering says ruling question model =
        match Parse.asked question with
        | Ok regionId -> ruling regionId model
        | Error problem -> says problem

    let private views palette =
        [ { Name = "plain"
            Describe = "plain text, and nothing this terminal has to understand"
            Shown = AtATerminal
            Palette = palette
            Board = fun notes seat model -> Render.model notes (at seat model) model
            History = fun seat model -> Render.history (at seat model) model
            Answer = answering id Render.explainRule
            Rules = Render.help
            Says = id
            Waiting = Render.waiting }

          { Name = "rich"
            Describe = "panels, charts and colour, for a terminal that can show them"
            Shown = AtATerminal
            Palette = palette
            Board = fun notes seat model -> Rich.board palette notes (at seat model) model
            History = fun seat model -> Rich.history palette (at seat model) model
            Answer = answering (Ink.paint palette) (Rich.ruling palette)
            Rules = Rich.rules palette
            Says = Ink.paint palette
            Waiting = Rich.waiting palette }

          // This one takes no palette into its endpoints, and it is the only one that does
          // not. A page carries its colours in its own head - `Html.page` writes them there -
          // and every fragment draws in those rather than in colours of its own, so a board is
          // built once however many people are reading it.
          { Name = "html"
            Describe = "a page, for a player reading in a browser"
            Shown = InABrowser
            Palette = palette
            Board = fun notes seat model -> Html.board notes (at seat model) model
            History = fun seat model -> Html.history (at seat model) model
            Answer = answering Html.says Html.ruling
            Rules = Html.rules
            Says = Html.says
            Waiting = Html.waiting } ]

    // --- and the whole of it ----------------------------------------------------------------

    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = fun players seed -> Setup.deal players seed |> Result.map Playing.opening |> Result.mapError refused
              Play = Turn.asked
              Active = fun session -> (Game.active (Session.game session)).Id
              Turn = Session.turn
              Over = Session.isOver
              Seats = fun session -> Game.playerCount (Session.game session)
              // Out of the game's own generator rather than off the clock, so a game restarted
              // twice from the same record restarts the same way twice.
              Reseed = fun session -> Rng.next (Session.game session).Rng |> fst }

          Name = "turncoats"
          Title = "Turncoats"
          Blurb = "Stones on a map, and a seat each."
          Fewest = Table.MinPlayers
          Most = Table.MaxPlayers

          Read = Parse.line
          Write = Words.command
          Seat = Words.player
          Says = Words.said
          SeenBy = Words.saidTo

          Resign = Some Resign
          Faults = Board.problems
          Slots = Ink.slots
          Skills = Rival.all |> List.map (fun skill -> skill.Name, skill.Describe)

          Seating =
            fun seed sitting session ->
                Rival.seating seed (sitting |> List.map (Option.bind skill)) (Session.game session)
                |> List.map (fun (seat, rival) ->
                    seat,
                    { Skill = rival.Skill.Name
                      Plays = machine rival })

          Page = Html.shell
          Views = views }
