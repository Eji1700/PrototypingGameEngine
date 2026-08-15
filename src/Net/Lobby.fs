namespace TCModel.Net

open TCModel.Engine
open TCModel.Table

/// Who is at a seat.
///
/// A seat is empty until somebody takes it. Once taken it keeps its token for good, so a
/// console that drops off can come back to the same pieces rather than the seat being
/// handed to a stranger - and a seat waiting for its player back is not an empty one.
///
/// A seat the program plays was never empty and cannot be sat at. It is what lets a person
/// open a table for themselves, a friend two rooms away, and one machine between them.
type Occupant =
    | Empty
    | Taken of token: string * console: string option
    | Played

/// One place at a networked table: the seat it plays, who is in it, and how that person is
/// reading the board.
///
/// The margins and the view belong to the seat rather than to the game, because they are how
/// one player reads and not how the game is played. The board is drawn here at the table -
/// a view lays the whole screen out and so needs the game to do it - which means two people
/// at the same table can be sent two boards that look nothing alike, from one position.
[<NoComparison; NoEquality>]
type Seat<'Move, 'State, 'Notice> =
    { Player: PlayerId
      Occupant: Occupant
      Margins: Margins
      View: View<'Move, 'State, 'Notice> }

/// A dealt game with consoles at it.
///
/// This is where the difference between one keyboard and several is settled, and it is only
/// ever three things: who may act, who may see, and what a player may ask for. The rules of
/// the game itself are untouched - a move is decided here exactly as it is at a kitchen
/// table, by the same `update`.
///
/// The game it is playing is carried rather than known, the same as at `Solo`. Nothing below
/// names one.
[<NoComparison; NoEquality>]
type Lobby<'Move, 'State, 'Notice> =
    private
        { Game: Playable<'Move, 'State, 'Notice>
          Model: Model<'Move, 'State, 'Notice>
          Rivals: (PlayerId * Seated<'Move, 'State>) list
          Seats: Seat<'Move, 'State, 'Notice> list }

module Lobby =

    /// A table for a game that has been dealt, with nobody at it yet. A seat nobody has
    /// taken has no view of its own, so it holds the plain one until somebody arrives and
    /// says how they would rather read.
    ///
    /// The machines are handed in already seated, the same way the one-keyboard table takes
    /// them: which seat a machine plays and what its generator started from are facts about
    /// the game that was dealt, and this table is only where they sit.
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

    /// Let the machines take their turns, which they do at this table for the same reason
    /// and by the same loop as at any other: the game has reached a seat that is theirs.
    let private answering lobby =
        let model, rivals =
            Machines.answering (rules lobby) Playable.plays lobby.Rivals lobby.Model

        { lobby with
            Model = model
            Rivals = rivals }

    let private isEmpty seat = seat.Occupant = Empty

    /// Whether a seat is still one of the game's. Nothing a networked table allows can take
    /// a seat off the board - how many are playing is settled when the table is opened - but
    /// a seat is looked up rather than assumed, because the answer is the game's to give.
    let private stillSeated seat lobby =
        Playable.seatsOf lobby.Game (standing lobby) |> List.contains seat.Player

    /// Every seat with somebody actually at it now. A dropped player is still in their seat
    /// but has nothing to read it with.
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

    // A game may well have seats of its own, so this one says which it means.
    let private withSeat seat (lobby: Lobby<_, _, _>) =
        { lobby with
            Seats =
                lobby.Seats
                |> List.map (fun other -> if other.Player = seat.Player then seat else other) }

    /// Nobody plays until every seat is taken. A game dealt for four hands out four bags
    /// whether or not four people have arrived, so starting early would mean somebody
    /// playing a hand that is not theirs.
    let private everyoneHere lobby =
        lobby.Seats |> List.forall (isEmpty >> not)

    // --- what a console is shown ------------------------------------------------------

    /// Who is here and who is not, as a list for a view to lay out. Nothing here decides
    /// how it reads - a table gathers the facts and the view draws them, the same as with
    /// a board.
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

    /// The board as one seat sees it, drawn the way that seat asked for it - or the lobby,
    /// if there is no game to look at yet.
    let private screenFor lobby (console, seat) =
        let text =
            if not (everyoneHere lobby) then waitingAt lobby seat
            elif stillSeated seat lobby then seat.View.Board seat.Margins seat.Player lobby.Model
            else seat.View.Says "Your seat is no longer at this table."

        { To = console; Say = Screen text }

    /// Draw every console its own board. Anything that moves the game ends this way,
    /// because a change for one player is a change for all of them - and each of them has
    /// to be told in their own terms.
    let private drawAll lobby =
        consoles lobby |> List.map (screenFor lobby)

    /// Whoever the game has just come round to, told so - unless they are the one who moved
    /// it, who is sitting there watching and needs no telling.
    ///
    /// This is the table a nudge was worth building for. Everybody here is at their own
    /// machine, waiting on somebody they cannot see, and the honest thing to do while waiting
    /// is something else. So the turn arriving has to be able to reach a player who is not
    /// looking - and it is the table, and only the table, that knows it has arrived.
    ///
    /// A game not yet begun has come round to nobody, and neither has one that is over.
    let private nudging spoke lobby =
        if (rules lobby).Over(standing lobby) || not (everyoneHere lobby) then
            []
        else
            let active = (rules lobby).Active(standing lobby)

            consoles lobby
            |> List.filter (fun (console, seat) -> seat.Player = active && Some console <> spoke)
            |> List.map (fun (console, _) -> { To = console; Say = Nudged })

    let private just console said = [ { To = console; Say = said } ]

    // --- taking a seat ----------------------------------------------------------------

    /// Take a seat, or come back to one already taken.
    ///
    /// A token is what tells the two apart: without one the next empty seat is handed out,
    /// and with one the seat that token claimed is given back, whatever machine the player
    /// has come back on. `offered` is the token a new seat would be given - minted outside,
    /// because a table that invented its own would not be a value any more.
    ///
    /// The view comes in with the player, because they said how they wanted to read before
    /// they had anything to read, and it comes in again on every return: a console that
    /// drops and comes back on something else may want a different one.
    let join console offered resuming (view: View<_, _, _>) lobby =
        let byToken token =
            lobby.Seats
            |> List.tryFind (fun seat ->
                match seat.Occupant with
                | Taken(mine, _) -> mine = token
                | Played
                | Empty -> false)

        let sit seat token lobby =
            // Whether this is the arrival the table has been waiting for. Taking the last
            // seat starts the game, and the player it starts with is owed a nudge as much as
            // by any move - they have been waiting on strangers with nothing to watch. A
            // console coming back to a seat it already held starts nothing, and nudges
            // nobody: the turn was where it was before, and whoever holds it knows.
            let begins = not (everyoneHere lobby)

            let lobby =
                lobby
                |> withSeat
                    { seat with
                        Occupant = Taken(token, Some console)
                        View = view }

            // The machines wait for the table to fill like everybody else, and then play up
            // to the first seat a person has to answer at. A machine that moved while people
            // were still arriving would be playing a game nobody had joined yet.
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

    /// A console has gone. The seat stays taken and keeps its token, so the player can come
    /// back to it; the game simply waits, because there is nobody else who may play their
    /// pieces for them.
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

    // --- what a player may ask for ----------------------------------------------------

    /// The three things a networked table refuses that one keyboard allows. Each of them is
    /// a move that reaches behind the game rather than playing it, and with more than one
    /// player at the table there is nobody with the standing to make it.
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

    /// A line typed at one console, and what the whole table is told as a result.
    let said console (typed: string) lobby =
        match seatAt console lobby with
        | None -> lobby, just console (TurnedAway "You are not sitting at this table.")
        | Some seat ->

        let told text =
            lobby, just console (Told(seat.View.Says text))

        let redraw seat lobby =
            lobby, [ screenFor lobby (console, seat) ]

        /// Getting up, which is the same thing to this table as dropping off: the seat is
        /// kept and the game waits, because there is nobody with the standing to play
        /// somebody else's pieces and no reason to think they are not coming back.
        ///
        /// What is not the same is that this one was meant, so it is said out loud. A player
        /// who put the game down and heard nothing has no way of telling that from a table
        /// that stopped answering - and a console at a terminal has nothing else to end on:
        /// it holds no opinion about what `quit` means, and should not be given one.
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

        // Before the two guards below rather than after them, because getting up is the one
        // thing a player may do at every table there is: one still filling up, one whose game
        // has finished, one whose seat the game no longer has. It was the only thing they
        // could not do at any of them - a line typed at a table still waiting was answered
        // with the waiting screen, whatever it said.
        match Playable.read lobby.Game typed with
        | Ok Leave -> gotUp ()
        | read ->

        if not (everyoneHere lobby) then
            redraw seat lobby
        elif not (stillSeated seat lobby) then
            told "Your seat is no longer at this table."
        else

        /// Something about this seat has changed rather than something about the game, so
        /// this one console is drawn again and nobody else hears about it.
        let mine seat = lobby |> withSeat seat |> redraw seat

        match read with
        | Error problem -> told problem
        | Ok Nothing -> redraw seat lobby
        | Ok Help -> lobby, just console (Told seat.View.Rules)
        // Answered above, and here so that the whole of what a line can be is still read in
        // one place - a case added to `Command` has to be answered by this match or it will
        // not compile, and that is worth more than the line it costs.
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
            // In whatever colours this seat is already reading in: a player who set them
            // before sitting down does not lose them by changing how the board is laid out.
            // And only among the views this seat could read - a browser asking for `rich`
            // would be sent escape codes, and a console asking for `html` angle brackets.
            match Playable.byName seat.View.Shown seat.View.Palette lobby.Game name with
            | Ok view -> mine { seat with View = view }
            | Error problem -> told problem
        | Ok(Asking question) -> lobby, just console (Told(seat.View.Answer question lobby.Model))
        | Ok Recount -> lobby, just console (Told(seat.View.History seat.Player lobby.Model))
        | Ok Keep -> told "The table keeps the record itself, and writes it out after every move."
        | Ok(Send msg) ->
            match refused msg with
            | Some why -> told why
            | None ->

            // Only the player whose turn it is may move. This is the one rule a single
            // keyboard never needed, because there was only ever one pair of hands.
            let active = (rules lobby).Active(standing lobby)

            if seat.Player <> active then
                told $"It is {lobby.Game.Seat active}'s turn."
            else
                // And whoever the machine is between this player and their next turn answers
                // before anybody is drawn, so one screen arrives holding the whole of what
                // happened rather than a board that is about to change again.
                let lobby =
                    answering
                        { lobby with
                            Model = Update.update (rules lobby) msg lobby.Model }

                lobby, drawAll lobby @ nudging (Some console) lobby
