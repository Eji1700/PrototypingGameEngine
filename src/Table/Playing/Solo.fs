namespace TCModel.Table

open TCModel.Engine

[<NoComparison; NoEquality>]
type Reading<'Move, 'State, 'Notice> =
    { Margins: Margins
      View: View<'Move, 'State, 'Notice> }

[<NoComparison; NoEquality>]
type Errand<'Move, 'State, 'Notice> =
    | Carrying
    | Keeping of keep: Model<'Move, 'State, 'Notice> * stamp: string * announce: bool
    | Leaving of keep: Model<'Move, 'State, 'Notice> option * stamp: string

[<NoComparison; NoEquality>]
type Solo<'Move, 'State, 'Notice> =
    private
        { Game: Playable<'Move, 'State, 'Notice>
          Model: Model<'Move, 'State, 'Notice>
          Stamp: string
          Opened: int
          Rivals: (PlayerId * Seated<'Move, 'State>) list
          Watchers: (string * Reading<'Move, 'State, 'Notice>) list }

module Solo =

    let opened game stamp model =
        { Game = game
          Model = model
          Stamp = stamp
          Opened = Timeline.movesMade model.Timeline
          Rivals = []
          Watchers = [] }

    let model solo = solo.Model

    let game solo = solo.Game

    let stamp solo = solo.Stamp

    let private rules solo = solo.Game.Rules

    let private standing solo = Model.state solo.Model

    let private added solo =
        Timeline.movesMade solo.Model.Timeline <> solo.Opened

    let isOver solo = (rules solo).Over(standing solo)

    let private active solo = (rules solo).Active(standing solo)


    let private answering solo =
        let model, rivals =
            Machines.answering (rules solo) Playable.plays solo.Rivals solo.Model

        { solo with
            Model = model
            Rivals = rivals }

    /// Undo and redo walk past the machine's moves as well as your own. One step back would otherwise
    /// only take back what the machine last played, and hand the turn straight back to it.
    let rec private walking msg solo =
        if not (Machines.holds (rules solo) solo.Rivals solo.Model) then
            solo
        else
            let next =
                { solo with
                    Model = Update.update (rules solo) msg solo.Model }

            if Timeline.movesMade next.Model.Timeline = Timeline.movesMade solo.Model.Timeline then
                next
            else
                walking msg next

    let against rivals solo =
        let played = answering { solo with Rivals = rivals }

        played, (if isOver played && not (isOver solo) then Keeping(played.Model, played.Stamp, false) else Carrying)

    let private readingAt console solo =
        solo.Watchers |> List.tryFind (fst >> (=) console) |> Option.map snd

    let private withReading console reading solo =
        { solo with
            Watchers =
                solo.Watchers
                |> List.map (fun (other, was) -> if other = console then other, reading else other, was) }


    let private boardFor solo (reading: Reading<_, _, _>) =
        reading.View.Board reading.Margins (active solo) solo.Model

    let private screenFor solo (console, reading) =
        { To = console
          Say = Screen(boardFor solo reading) }

    let board console solo =
        readingAt console solo |> Option.map (boardFor solo)

    let margins console solo =
        readingAt console solo |> Option.map (fun reading -> reading.Margins)

    let reading console margins solo =
        match readingAt console solo with
        | Some reading -> withReading console { reading with Margins = margins } solo
        | None -> solo

    let private drawAll solo =
        solo.Watchers |> List.map (screenFor solo)

    /// What the board is sounding, said to everybody watching it. A sound goes *beside* a screen
    /// rather than instead of one, and it is read off where the game stands rather than out of
    /// what it said - so a board taken up from a record sounds the same as the one it was saved
    /// from, and a table with no way to make a noise drops it without any of this knowing.
    let private sounding solo =
        [ for sound in solo.Game.Rings(standing solo) do
              for console, _ in solo.Watchers -> { To = console; Say = Rang sound } ]

    let private nudging console solo =
        if isOver solo then
            []
        else
            solo.Watchers
            |> List.map fst
            |> List.filter ((<>) console)
            |> List.map (fun other -> { To = other; Say = Nudged })

    let private just console said = [ { To = console; Say = said } ]

    let saying console text solo =
        readingAt console solo
        |> Option.map (fun reading ->
            { To = console
              Say = Told(reading.View.Says text) })
        |> Option.toList


    let private roster (reading: Reading<_, _, _>) solo =
        Playable.roster solo.Game solo.Rivals
        |> Option.map (reading.View.Says >> Told)
        |> Option.toList

    let watching console (reading: Reading<_, _, _>) solo =
        let solo =
            match readingAt console solo with
            | Some _ -> withReading console reading solo
            | None ->
                { solo with
                    Watchers = solo.Watchers @ [ (console, reading) ] }

        solo,
        [ screenFor solo (console, reading) ]
        @ (roster reading solo |> List.map (fun said -> { To = console; Say = said }))

    let gone console solo =
        { solo with
            Watchers = solo.Watchers |> List.filter (fst >> (<>) console) },
        []


    /// One beat of the clock.
    ///
    /// A beat that found nothing to do leaves no trace - `Update` does not write down a move the
    /// game neither took nor spoke about - and so nothing is drawn for it either. That is what
    /// lets a game beat while it is at rest without sending a board down every wire in the house
    /// twice a second, and it is why a game whose board only moves in bursts may keep one clock
    /// rather than starting and stopping one.
    let beaten solo =
        match solo.Game.Pulse with
        | Some pulse when not (isOver solo) && not (List.isEmpty solo.Watchers) ->
            let next =
                answering
                    { solo with
                        Model = Update.update (rules solo) (Make pulse.Beat) solo.Model }

            if Journal.length next.Model.Journal = Journal.length solo.Model.Journal then
                next, [], Carrying
            else
                next, drawAll next @ sounding next, (if isOver next then Keeping(next.Model, next.Stamp, false) else Carrying)
        | Some _
        | None -> solo, [], Carrying


    let said fresh console (typed: string) solo =
        match readingAt console solo with
        | None -> solo, just console (TurnedAway "You are not watching this game."), Carrying
        | Some reading ->

        let beholder = active solo

        let told text =
            solo, just console (Told(reading.View.Says text)), Carrying

        let mine reading =
            let solo = withReading console reading solo
            solo, [ screenFor solo (console, reading) ], Carrying

        let moved solo errand =
            solo, drawAll solo @ sounding solo @ nudging console solo, errand

        match Playable.read solo.Game typed with
        | Error problem -> told problem
        | Ok Nothing -> solo, [ screenFor solo (console, reading) ], Carrying
        | Ok Help -> solo, just console (Told reading.View.Rules), Carrying
        | Ok Recount -> solo, just console (Told(reading.View.History beholder solo.Model)), Carrying
        | Ok(Asking question) -> solo, just console (Told(reading.View.Answer beholder question solo.Model)), Carrying
        | Ok(Notes wanted) ->
            mine
                { reading with
                    Margins =
                        { reading.Margins with
                            Notes = wanted |> Option.defaultValue (not reading.Margins.Notes) } }
        | Ok(Listing wanted) ->
            mine
                { reading with
                    Margins =
                        { reading.Margins with
                            Commands = wanted |> Option.defaultValue (not reading.Margins.Commands) } }
        | Ok(Looking name) ->
            match Playable.byName reading.View.Shown reading.View.Palette solo.Game name with
            | Ok view -> mine { reading with View = view }
            | Error problem -> told problem
        | Ok Keep -> solo, [], Keeping(solo.Model, solo.Stamp, true)
        | Ok Leave -> solo, drawAll solo, Leaving((if added solo then Some solo.Model else None), solo.Stamp)
        | Ok(Send(Restart _ as msg)) ->
            let closing = solo.Model

            moved
                (answering
                    { solo with
                        Model = Update.update (rules solo) msg solo.Model
                        Stamp = fresh
                        Opened = 0 })
                (if added solo then Keeping(closing, solo.Stamp, false) else Carrying)
        | Ok(Send((Undo | Redo) as msg)) ->
            moved
                (walking
                    msg
                    { solo with
                        Model = Update.update (rules solo) msg solo.Model })
                Carrying
        | Ok(Send msg) ->
            let next =
                answering
                    { solo with
                        Model = Update.update (rules solo) msg solo.Model }

            let errand =
                if isOver next && not (isOver solo) then Keeping(next.Model, solo.Stamp, false) else Carrying

            moved next errand
