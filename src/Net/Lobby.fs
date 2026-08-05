namespace TCModel.Net


open TCModel.Domain
open TCModel.App
open TCModel.Console

/// Who is at a seat.
///
/// A seat is empty until somebody takes it. Once taken it keeps its token for good, so a
/// console that drops off can come back to the same stones rather than the seat being
/// handed to a stranger - and a seat waiting for its player back is not an empty one.
type Occupant =
    | Empty
    | Taken of token: string * console: string option

/// One place at a networked table: the player it plays, who is in it, and how that person
/// is reading the board.
///
/// The notes and the view belong to the seat rather than to the game, because they are how
/// one player reads and not how the game is played. The board is drawn here at the table -
/// a view lays the whole screen out and so needs the game to do it - which means two people
/// at the same table can be sent two boards that look nothing alike, from one position.
[<NoComparison; NoEquality>]
type Seat =
    { Player: PlayerId
      Occupant: Occupant
      Notes: bool
      View: View }

/// A dealt game with consoles at it.
///
/// This is where the difference between one keyboard and several is settled, and it is only
/// ever three things: who may act, who may see, and what a player may ask for. The rules of
/// the game itself are untouched - `Update.update` decides a move here exactly as it does
/// at a kitchen table.
[<NoComparison; NoEquality>]
type Lobby =
    private
        { Model: Model
          Seats: Seat list }

module Lobby =

    /// A table for a game that has been dealt, with nobody at it yet. A seat nobody has
    /// taken has no view of its own, so it holds the plain one until somebody arrives and
    /// says how they would rather read.
    let opened model =
        { Model = model
          Seats =
            Game.players (Model.game model)
            |> List.map (fun player ->
                { Player = player.Id
                  Occupant = Empty
                  Notes = true
                  View = View.plain Palette.standard }) }

    let model lobby = lobby.Model

    let private game lobby = Model.game lobby.Model

    let private isEmpty seat = seat.Occupant = Empty

    /// Every seat with somebody actually at it now. A dropped player is still in their seat
    /// but has nothing to read it with.
    let private consoles lobby =
        lobby.Seats
        |> List.choose (fun seat ->
            match seat.Occupant with
            | Taken(_, Some console) -> Some(console, seat)
            | Taken(_, None)
            | Empty -> None)

    let private seatAt console lobby =
        consoles lobby |> List.tryFind (fst >> (=) console) |> Option.map snd

    // `Table` in the domain has seats of its own, so this one says which it means.
    let private withSeat seat (lobby: Lobby) =
        { lobby with
            Seats =
                lobby.Seats
                |> List.map (fun other -> if other.Player = seat.Player then seat else other) }

    /// Nobody plays until every seat is taken. A game dealt for four hands out four bags
    /// whether or not four people have arrived, so starting early would mean somebody
    /// playing a bag that is not theirs.
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
                | Empty -> false
              Yours = other.Player = seat.Player })
        |> seat.View.Waiting

    /// The board as one seat sees it, drawn the way that seat asked for it - or the lobby,
    /// if there is no game to look at yet.
    let private screenFor lobby (console, seat) =
        let text =
            if not (everyoneHere lobby) then
                waitingAt lobby seat
            else
                match Game.tryPlayer seat.Player (game lobby) with
                | Some player -> seat.View.Board seat.Notes player lobby.Model
                | None -> seat.View.Says "Your seat is no longer at this table."

        { To = console; Say = Screen text }

    /// Draw every console its own board. Anything that moves the game ends this way,
    /// because a change for one player is a change for all of them - and each of them has
    /// to be told in their own terms.
    let private drawAll lobby =
        consoles lobby |> List.map (screenFor lobby)

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
    let join console offered resuming (view: View) lobby =
        let byToken token =
            lobby.Seats
            |> List.tryFind (fun seat ->
                match seat.Occupant with
                | Taken(mine, _) -> mine = token
                | Empty -> false)

        let sit seat token lobby =
            let lobby =
                lobby
                |> withSeat
                    { seat with
                        Occupant = Taken(token, Some console)
                        View = view }

            lobby, just console (Seated(PlayerId.value seat.Player, token)) @ drawAll lobby

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
    /// stones for them.
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
                "Undo is not played over a network. With more than one player at the table a game only goes forward - and walking it back would show you a bag you are not meant to see."
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

        if not (everyoneHere lobby) then
            redraw seat lobby
        else

        match Game.tryPlayer seat.Player (game lobby) with
        | None -> told "Your seat is no longer at this table."
        | Some player ->

        /// Something about this seat has changed rather than something about the game, so
        /// this one console is drawn again and nobody else hears about it.
        let mine seat = lobby |> withSeat seat |> redraw seat

        match Parse.line typed with
        | Error problem -> told problem
        | Ok Parse.Nothing -> redraw seat lobby
        | Ok Parse.Help -> lobby, just console (Told seat.View.Rules)
        | Ok Parse.Leave -> left console lobby
        | Ok(Parse.Notes wanted) ->
            mine
                { seat with
                    Notes = wanted |> Option.defaultValue (not seat.Notes) }
        | Ok(Parse.Looking name) ->
            // In whatever colours this seat is already reading in: a player who set them
            // before sitting down does not lose them by changing how the board is laid out.
            // And only among the views this seat could read - a browser asking for `rich`
            // would be sent escape codes, and a console asking for `html` angle brackets.
            match View.byName seat.View.Shown seat.View.Palette name with
            | Ok view -> mine { seat with View = view }
            | Error problem -> told problem
        | Ok(Parse.Explain regionId) -> lobby, just console (Told(seat.View.Ruling regionId lobby.Model))
        | Ok Parse.Recount -> lobby, just console (Told(seat.View.History player lobby.Model))
        | Ok Parse.Keep -> told "The table keeps the record itself, and writes it out after every move."
        | Ok(Parse.Send msg) ->
            match refused msg with
            | Some why -> told why
            | None ->

            // Only the player whose turn it is may move. This is the one rule a single
            // keyboard never needed, because there was only ever one pair of hands.
            let active = Game.active (game lobby)

            if seat.Player <> active.Id then
                told $"It is {Words.player active.Id}'s turn."
            else
                let lobby =
                    { lobby with
                        Model = Update.update msg lobby.Model }

                lobby, drawAll lobby
