namespace Prototyping.Turncoats

open Prototyping.Table

module Offer =


    // The table only asks about seats that are at the game. Were it to ask about one that is not,
    // a stranger's view is the honest answer: no bag of their own, and every other one closed.
    let private at seat model =
        Game.tryPlayer seat (Playing.game model)
        |> Option.defaultValue ({ Id = seat; Bag = Pile.empty }: Player)


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
        { Rules = Playing.rules

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

          Rings = fun _ -> []

          Resign = Some Resign
          Faults = Board.problems
          Slots = Ink.slots
          Skills = Rival.all |> List.map (fun skill -> skill.Name, skill.Describe)

          Seating = Playable.seating Rival.byName Rival.seating (fun rival -> rival.Skill.Name) Rival.taking

          Pulse = None


          Aside = None

          Steering = fun _ _ _ _ -> None

          Page = Html.shell
          Views = views }

    let ways = [ playable ]
