namespace TCModel.Turncoats

open TCModel.Common
open TCModel.Engine
open TCModel.Table

module Offer =


    let private refused =
        function
        | TooFewPlayers n -> $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."
        | TooManyPlayers n -> $"{n} players? The game takes {Table.MinPlayers} to {Table.MaxPlayers}."

    let private at seat model =
        Game.tryPlayer seat (Playing.game model)
        |> Option.defaultValue (Game.active (Playing.game model))


    let machine rival = Machines.choosing Rival.taking rival

    let private skill name = Rival.byName name |> Result.toOption


    let private answering says ruling _ question model =
        match Parse.asked question with
        | Ok regionId -> ruling regionId model
        | Error problem -> says problem

    let private views palette =
        [ { Name = "plain"
            Describe = "plain text, and nothing this terminal has to understand"
            Shown = AtATerminal
            Palette = palette
            Board = fun margins seat model -> Render.model margins (at seat model) model
            History = fun seat model -> Render.history (at seat model) model
            Answer = answering id Render.explainRule
            Rules = Render.help
            Says = id
            Waiting = Render.waiting }

          { Name = "rich"
            Describe = "panels, charts and colour, for a terminal that can show them"
            Shown = AtATerminal
            Palette = palette
            Board = fun margins seat model -> Rich.board palette margins (at seat model) model
            History = fun seat model -> Rich.history palette (at seat model) model
            Answer = answering (Ink.paint palette) (Rich.ruling palette)
            Rules = Rich.rules palette
            Says = Ink.paint palette
            Waiting = Rich.waiting palette }

          { Name = "html"
            Describe = "a page, for a player reading in a browser"
            Shown = InABrowser
            Palette = palette
            Board = fun margins seat model -> Html.board margins (at seat model) model
            History = fun seat model -> Html.history (at seat model) model
            Answer = answering Html.says Html.ruling
            Rules = Html.rules
            Says = Html.says
            Waiting = Html.waiting } ]


    let playable: Playable<Move, Session, Notice> =
        { Rules =
            { Deal = fun players seed -> Setup.deal players seed |> Result.map Playing.opening |> Result.mapError refused
              Play = Turn.asked
              Active = fun session -> (Game.active (Session.game session)).Id
              Turn = Session.turn
              Over = Session.isOver
              Seats = fun session -> Game.playerCount (Session.game session)
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

          Pulse = None


          Page = Html.shell
          Views = views }

    let ways = [ playable ]
