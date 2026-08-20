namespace TCModel.Net

open TCModel.Engine
open TCModel.Table

/// Who has a seat. `Taken` with no console is somebody who is sitting there but whose connection has
/// gone - the seat is held for them and nobody else may take it, and the token is what brings them
/// back to it.
type Occupant =
    | Empty
    | Taken of token: string * console: string option
    | Played

[<NoComparison; NoEquality>]
type Seat<'Move, 'State, 'Notice> =
    { Player: PlayerId
      Occupant: Occupant
      Margins: Margins
      View: View<'Move, 'State, 'Notice> }

[<NoComparison; NoEquality>]
type Lobby<'Move, 'State, 'Notice> =
    private
        { Game: Playable<'Move, 'State, 'Notice>
          Model: Model<'Move, 'State, 'Notice>
          Rivals: (PlayerId * Seated<'Move, 'State>) list
          Seats: Seat<'Move, 'State, 'Notice> list }

module Lobby =

    let opened game model rivals =
        let plain = Playable.plainest AtATerminal (Playable.standard game) game

        { Game = game
          Model = model
          Rivals = rivals
          Seats =
            Playable.seatsOf game (Model.state model)
            |> List.map (fun player ->
                { Player = player
                  Occupant = (if rivals |> List.exists (fst >> (=) player) then Played else Empty)
                  Margins = Margins.all
                  View = plain }) }

    let model lobby = lobby.Model

    let game lobby = lobby.Game

    let private rules lobby = lobby.Game.Rules

    let private standing lobby = Model.state lobby.Model

    let private answering lobby =
        let model, rivals =
            Machines.answering (rules lobby) Playable.plays lobby.Rivals lobby.Model

        { lobby with
            Model = model
            Rivals = rivals }

    let private isEmpty seat = seat.Occupant = Empty

    let private stillSeated seat lobby =
        Playable.seatsOf lobby.Game (standing lobby) |> List.contains seat.Player

    let private consoles lobby =
        lobby.Seats
        |> List.choose (fun seat ->
            match seat.Occupant with
            | Taken(_, Some console) -> Some(console, seat)
            | Taken(_, None)
            | Played
            | Empty -> None)

    let private seatAt console lobby =
        consoles lobby |> List.tryFind (fst >> (=) console) |> Option.map snd

    let private withSeat seat (lobby: Lobby<_, _, _>) =
        { lobby with
            Seats =
                lobby.Seats
                |> List.map (fun other -> if other.Player = seat.Player then seat else other) }

    let private everyoneHere lobby =
        lobby.Seats |> List.forall (isEmpty >> not)


    type Stage =
        | Filling
        | Underway
        | Finished

    type Standing =
        { Stage: Stage
          Places: int
          Machines: int
          Sat: int
          Reading: int
          Sitters: string list }

    let described lobby : Standing =
        let counted wanted =
            lobby.Seats |> List.filter (fun seat -> wanted seat.Occupant) |> List.length

        { Stage =
            if (rules lobby).Over(standing lobby) then Finished
            elif everyoneHere lobby then Underway
            else Filling
          Places = List.length lobby.Seats
          Machines = counted (fun occupant -> occupant = Played)
          Sat =
            counted (fun occupant ->
                match occupant with
                | Taken _ -> true
                | Played
                | Empty -> false)
          Reading =
            counted (fun occupant ->
                match occupant with
                | Taken(_, Some _) -> true
                | Taken(_, None)
                | Played
                | Empty -> false)
          Sitters =
            lobby.Seats
            |> List.map (fun seat ->
                let who =
                    match seat.Occupant with
                    | Empty -> "waiting"
                    | Played -> "the machine"
                    | Taken(_, Some _) -> "here"
                    | Taken(_, None) -> "away"

                $"{lobby.Game.Seat seat.Player} ({who})") }


    let private waitingAt lobby seat =
        lobby.Seats
        |> List.map (fun other ->
            { Player = other.Player
              Expected = isEmpty other
              Away =
                match other.Occupant with
                | Taken(_, None) -> true
                | Taken(_, Some _)
                | Played
                | Empty -> false
              Yours = other.Player = seat.Player })
        |> seat.View.Waiting

    let private screenFor lobby (console, seat) =
        let text =
            if not (everyoneHere lobby) then waitingAt lobby seat
            elif stillSeated seat lobby then seat.View.Board seat.Margins seat.Player lobby.Model
            else seat.View.Says "Your seat is no longer at this table."

        { To = console; Say = Screen text }

    let private drawAll lobby =
        consoles lobby |> List.map (screenFor lobby)

    let private nudging spoke lobby =
        if (rules lobby).Over(standing lobby) || not (everyoneHere lobby) then
            []
        else
            let active = (rules lobby).Active(standing lobby)

            consoles lobby
            |> List.filter (fun (console, seat) -> seat.Player = active && Some console <> spoke)
            |> List.map (fun (console, _) -> { To = console; Say = Nudged })

    let private just console said = [ { To = console; Say = said } ]


    let beaten lobby =
        match lobby.Game.Pulse with
        | Some pulse when everyoneHere lobby && not ((rules lobby).Over(standing lobby)) ->
            let next =
                answering
                    { lobby with
                        Model = Update.update (rules lobby) (Make pulse.Beat) lobby.Model }

            next, drawAll next
        | Some _
        | None -> lobby, []


    let join console offered resuming (view: View<_, _, _>) lobby =
        let byToken token =
            lobby.Seats
            |> List.tryFind (fun seat ->
                match seat.Occupant with
                | Taken(mine, _) -> mine = token
                | Played
                | Empty -> false)

        let sit seat token lobby =
            let begins = not (everyoneHere lobby)

            let lobby =
                lobby
                |> withSeat
                    { seat with
                        Occupant = Taken(token, Some console)
                        View = view }

            let lobby = if begins && everyoneHere lobby then answering lobby else lobby

            lobby,
            just console (Seated(PlayerId.value seat.Player, token))
            @ drawAll lobby
            @ (Playable.roster lobby.Game lobby.Rivals
               |> Option.map (fun said ->
                   { To = console
                     Say = Told(view.Says said) })
               |> Option.toList)
            @ (if begins then nudging (Some console) lobby else [])

        match resuming with
        | Some token ->
            match byToken token with
            | Some seat -> sit seat token lobby
            | None -> lobby, just console (TurnedAway "That is not a seat at this table.")
        | None ->
            match lobby.Seats |> List.tryFind isEmpty with
            | Some seat -> sit seat offered lobby
            | None -> lobby, just console (TurnedAway "Every seat at this table is taken.")

    let left console lobby =
        match seatAt console lobby with
        | None -> lobby, []
        | Some seat ->
            let lobby =
                match seat.Occupant with
                | Taken(token, _) ->
                    lobby
                    |> withSeat
                        { seat with
                            Occupant = Taken(token, None) }
                | Played
                | Empty -> lobby

            lobby, drawAll lobby


    /// What a networked table will not do, and why. Undo is the interesting one: walking a game back
    /// in front of several players would show somebody a position they were meant to have seen only
    /// their own side of.
    let private refused =
        function
        | Undo
        | Redo ->
            Some
                "Undo is not played over a network. With more than one player at the table a game only goes forward - and walking it back would show you a hand you are not meant to see."
        | Restart(Some _, _) ->
            Some "How many are playing is settled when the table is opened, not once people are sitting at it."
        | Restart(None, _) -> Some "A networked table plays the one game it was dealt. Open another to play again."
        | Make _ -> None

    let said console (typed: string) lobby =
        match seatAt console lobby with
        | None -> lobby, just console (TurnedAway "You are not sitting at this table.")
        | Some seat ->

        let told text =
            lobby, just console (Told(seat.View.Says text))

        let redraw seat lobby =
            lobby, [ screenFor lobby (console, seat) ]

        let gotUp () =
            let lobby, posts = left console lobby

            lobby,
            posts
            @ just
                console
                (GotUp(
                    seat.View.Says
                        "You are up from the table. Your seat is kept - nobody else may take it - and the line you were given when you sat down brings you back to it."
                ))

        match Playable.read lobby.Game typed with
        | Ok Leave -> gotUp ()
        | read ->

        if not (everyoneHere lobby) then
            redraw seat lobby
        elif not (stillSeated seat lobby) then
            told "Your seat is no longer at this table."
        else

        let mine seat = lobby |> withSeat seat |> redraw seat

        match read with
        | Error problem -> told problem
        | Ok Nothing -> redraw seat lobby
        | Ok Help -> lobby, just console (Told seat.View.Rules)
        | Ok Leave -> gotUp ()
        | Ok(Notes wanted) ->
            mine
                { seat with
                    Margins =
                        { seat.Margins with
                            Notes = wanted |> Option.defaultValue (not seat.Margins.Notes) } }
        | Ok(Listing wanted) ->
            mine
                { seat with
                    Margins =
                        { seat.Margins with
                            Commands = wanted |> Option.defaultValue (not seat.Margins.Commands) } }
        | Ok(Looking name) ->
            match Playable.byName seat.View.Shown seat.View.Palette lobby.Game name with
            | Ok view -> mine { seat with View = view }
            | Error problem -> told problem
        | Ok(Asking question) -> lobby, just console (Told(seat.View.Answer seat.Player question lobby.Model))
        | Ok Recount -> lobby, just console (Told(seat.View.History seat.Player lobby.Model))
        | Ok Keep -> told "The table keeps the record itself, and writes it out after every move."
        | Ok(Send msg) ->
            match refused msg with
            | Some why -> told why
            | None ->

            // A game on a clock takes what anybody says whenever they say it - steering a snake is not
            // taking a turn. A game that goes by turns only takes it from whoever is to play.
            let active = (rules lobby).Active(standing lobby)

            if lobby.Game.Pulse.IsNone && seat.Player <> active then
                told $"It is {lobby.Game.Seat active}'s turn."
            else
                let lobby =
                    answering
                        { lobby with
                            Model = Update.update (rules lobby) msg lobby.Model }

                lobby, drawAll lobby @ nudging (Some console) lobby
