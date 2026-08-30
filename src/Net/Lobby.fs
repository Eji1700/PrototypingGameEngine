namespace Prototyping.Net

open Prototyping.Engine
open Prototyping.Table

/// Who has a seat. `Taken` names the console that took it and whether it is still here: a seat
/// whose console has gone is held for it - nobody else may take it - and the token, or the same
/// console coming back, brings it back to it.
type Occupant =
    | Empty
    | Taken of token: string * console: string * here: bool
    | Played

[<NoComparison; NoEquality>]
type Seat<'Move, 'State, 'Notice> =
    { Player: PlayerId
      Occupant: Occupant
      Margins: Margins
      Hushed: bool
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
                  Hushed = false
                  View = plain }) }

    /// Opened with the machines the seating asked for at their seats, which is how every table but
    /// a check's is opened.
    let openedFor game model sitters =
        opened game model (game.Seating (Model.seed model) (Seating.machines sitters) (Model.state model))

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
            | Taken(_, console, true) -> Some(console, seat)
            | Taken(_, _, false)
            | Played
            | Empty -> None)

    /// The seat a console is at and the token it holds there, for a console that is here.
    let private seatAt console lobby =
        lobby.Seats
        |> List.tryPick (fun seat ->
            match seat.Occupant with
            | Taken(token, at, true) when at = console -> Some(seat, token)
            | Taken _
            | Played
            | Empty -> None)

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
        {
            Stage: Stage
            Places: int
            Machines: int
            Sat: int
            Reading: int
            /// Whether anything has been played at it - here, or before it was taken up off a record,
            /// which leaves a table nobody is at holding somebody's game rather than nothing.
            Begun: bool
            Sitters: string list
        }

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
                | Taken(_, _, true) -> true
                | Taken(_, _, false)
                | Played
                | Empty -> false)
          Begun = not (Journal.isEmpty lobby.Model.Journal)
          Sitters =
            lobby.Seats
            |> List.map (fun seat ->
                let who =
                    match seat.Occupant with
                    | Empty -> "waiting"
                    | Played -> "the machine"
                    | Taken(_, _, true) -> "here"
                    | Taken(_, _, false) -> "away"

                $"{lobby.Game.Seat seat.Player} ({who})") }


    let private waitingAt lobby seat =
        lobby.Seats
        |> List.map (fun other ->
            { Player = other.Player
              Expected = isEmpty other
              Away =
                match other.Occupant with
                | Taken(_, _, false) -> true
                | Taken(_, _, true)
                | Played
                | Empty -> false
              Yours = other.Player = seat.Player })
        |> seat.View.Waiting

    let private screenFor lobby (console, seat) =
        let text =
            if not (everyoneHere lobby) then waitingAt lobby seat
            elif stillSeated seat lobby then seat.View.Board seat.Margins seat.Player lobby.Model
            else seat.View.Says "Your seat is no longer at this table."

        { To = console
          Say = ToPlayer.Screen text }

    let private drawAll lobby =
        consoles lobby |> List.map (screenFor lobby)

    /// What the board is sounding, said to every console at the table. The same reading `Solo`
    /// takes, and for the same reasons: off where the game stands rather than out of what it said,
    /// so two players at different keyboards hear the same board on the same beat; and only for a
    /// move that happened, since a refused move would ring the last real move's sound again.
    let private sounding before lobby =
        if Timeline.movesMade lobby.Model.Timeline = Timeline.movesMade before.Model.Timeline then
            []
        else
            [ for sound in lobby.Game.Rings(standing lobby) do
                  for console, seat in consoles lobby do
                      if not seat.Hushed then
                          { To = console
                            Say = ToPlayer.Rang sound } ]

    let private nudging spoke lobby =
        if (rules lobby).Over(standing lobby) || not (everyoneHere lobby) then
            []
        else
            let active = (rules lobby).Active(standing lobby)

            consoles lobby
            |> List.filter (fun (console, seat) -> seat.Player = active && Some console <> spoke)
            |> List.map (fun (console, _) -> { To = console; Say = ToPlayer.Nudged })

    let private just console said = [ { To = console; Say = said } ]


    let beaten lobby =
        match lobby.Game.Pulse with
        | Some pulse when everyoneHere lobby && not ((rules lobby).Over(standing lobby)) ->
            let next =
                answering
                    { lobby with
                        Model = Update.update (rules lobby) (Make pulse.Beat) lobby.Model }

            if Journal.length next.Model.Journal = Journal.length lobby.Model.Journal then
                next, []
            else
                next, drawAll next @ sounding lobby next
        | Some _
        | None -> lobby, []


    let join console offered resuming (view: View<_, _, _>) lobby =
        let holding wanted =
            lobby.Seats
            |> List.tryPick (fun seat ->
                match seat.Occupant with
                | Taken(token, at, _) when wanted token at -> Some(seat, token)
                | Taken _
                | Played
                | Empty -> None)

        let sit seat token lobby =
            let begins = not (everyoneHere lobby)

            let lobby =
                lobby
                |> withSeat
                    { seat with
                        Occupant = Taken(token, console, true)
                        View = view }

            let lobby = if begins && everyoneHere lobby then answering lobby else lobby

            lobby,
            just console (ToPlayer.Seated(PlayerId.value seat.Player, token))
            @ drawAll lobby
            @ (Playable.roster lobby.Game lobby.Rivals
               |> Option.map (fun said ->
                   { To = console
                     Say = ToPlayer.Told(view.Says said) })
               |> Option.toList)
            @ (if begins then nudging (Some console) lobby else [])

        // A token brings a console back to the seat it left whatever it is called now, since a
        // terminal comes back under a new name. A console the table already knows is put back in
        // its own seat without one - which is what a page that reloads has: the same cookie, and
        // no idea it was ever away.
        match resuming with
        | Some token ->
            match holding (fun mine _ -> mine = token) with
            | Some(seat, token) -> sit seat token lobby
            | None -> lobby, just console (ToPlayer.TurnedAway "That is not a seat at this table.")
        | None ->
            match holding (fun _ at -> at = console) with
            | Some(seat, token) -> sit seat token lobby
            | None ->
                match lobby.Seats |> List.tryFind isEmpty with
                | Some seat -> sit seat offered lobby
                | None -> lobby, just console (ToPlayer.TurnedAway "Every seat at this table is taken.")

    let left console lobby =
        match seatAt console lobby with
        | None -> lobby, []
        | Some(seat, token) ->
            let lobby =
                lobby
                |> withSeat
                    { seat with
                        Occupant = Taken(token, console, false) }

            lobby, drawAll lobby


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
        | None -> lobby, just console (ToPlayer.TurnedAway "You are not sitting at this table.")
        | Some(seat, _) ->

        let told text =
            lobby, just console (ToPlayer.Told(seat.View.Says text))

        let redraw seat lobby =
            lobby, [ screenFor lobby (console, seat) ]

        let gotUp () =
            let lobby, posts = left console lobby

            lobby,
            posts
            @ just
                console
                (ToPlayer.GotUp(
                    seat.View.Says
                        "You are up from the table. Your seat is kept - nobody else may take it - and the line you were given when you sat down brings you back to it."
                ))

        let mine seat = lobby |> withSeat seat |> redraw seat

        // Getting up comes before everything else, since a console may leave a table that is still
        // filling or a seat that is no longer at it; at either of those nothing else is answered.
        match Playable.read lobby.Game typed with
        | Ok Leave -> gotUp ()
        | _ when not (everyoneHere lobby) -> redraw seat lobby
        | _ when not (stillSeated seat lobby) -> told "Your seat is no longer at this table."
        | Error problem -> told problem
        | Ok Nothing -> redraw seat lobby
        | Ok Help -> lobby, just console (ToPlayer.Told seat.View.Rules)
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
        | Ok(Logging wanted) ->
            mine
                { seat with
                    Margins =
                        { seat.Margins with
                            Logged = wanted |> Option.defaultValue (not seat.Margins.Logged) } }
        | Ok(Showing screen) ->
            mine
                { seat with
                    Margins = { seat.Margins with Showing = screen } }
        | Ok(Hushing wanted) ->
            mine
                { seat with
                    Hushed = wanted |> Option.defaultValue (not seat.Hushed) }
        | Ok(Looking name) ->
            match Playable.byName seat.View.Shown seat.View.Palette lobby.Game name with
            | Ok view -> mine { seat with View = view }
            | Error problem -> told problem
        | Ok(Asking question) -> lobby, just console (ToPlayer.Told(seat.View.Answer seat.Player question lobby.Model))
        | Ok Recount -> lobby, just console (ToPlayer.Told(seat.View.History seat.Player lobby.Model))
        | Ok Keep -> told "The table keeps the record itself, and writes it out after every move."
        | Ok(Send msg) ->
            match refused msg with
            | Some why -> told why
            | None ->

            // A game of turns takes a move from whoever is to play and from nobody else - and so does
            // a game on a clock while it is still taking turns, which is what `Pulse.Free` says.
            // Where the beat is what moves the game, any console may speak: steering a snake is
            // not taking a turn.
            let active = (rules lobby).Active(standing lobby)

            let free =
                lobby.Game.Pulse |> Option.exists (fun pulse -> pulse.Free(standing lobby))

            if not free && seat.Player <> active then
                told $"It is {lobby.Game.Seat active}'s turn."
            else
                let next =
                    answering
                        { lobby with
                            Model = Update.update (rules lobby) msg lobby.Model }

                next, drawAll next @ sounding lobby next @ nudging (Some console) next
