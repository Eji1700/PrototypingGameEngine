namespace TCModel.Domain

/// Identifies a player. Private, so the only ids in circulation are the ones the
/// table below seated.
type PlayerId = private PlayerId of int

module PlayerId =
    let value (PlayerId n) = n

/// A player commands no faction of their own: the bag holds stones of any colour.
type Player =
    { Id: PlayerId
      Bag: Pile }

module Player =
    let isEmptyHanded player = Pile.isEmpty player.Bag

/// Two to five players in a fixed seating order, exactly one of them active. Holding
/// the active seat as a position rather than an id means there is always an active
/// player, and they are always sitting at this table.
type Table =
    private
        { Seats: Player list
          ActiveSeat: int }

type TableTooBig = TooFewPlayers of int | TooManyPlayers of int

module Table =

    [<Literal>]
    let MinPlayers = 2

    [<Literal>]
    let MaxPlayers = 5

    /// Seat one player per bag, in order, with the first to act.
    let trySeat bags =
        match List.length bags with
        | n when n < MinPlayers -> Error(TooFewPlayers n)
        | n when n > MaxPlayers -> Error(TooManyPlayers n)
        | _ ->
            Ok
                { Seats = bags |> List.mapi (fun index bag -> { Id = PlayerId(index + 1); Bag = bag })
                  ActiveSeat = 0 }

    let players table = table.Seats

    let count table = List.length table.Seats

    let active table = table.Seats[table.ActiveSeat]

    /// Pass the active seat to the next player round the table.
    let advance table =
        { table with ActiveSeat = (table.ActiveSeat + 1) % count table }

    /// Replace the active player, who is the only one an action can change.
    let withActive player table =
        { table with
            Seats = table.Seats |> List.mapi (fun seat other -> if seat = table.ActiveSeat then player else other) }

    /// The players in turn order, beginning with whoever acts next. A player's place
    /// in this list is how long they would wait for a turn.
    let fromNext table =
        let seats = count table

        [ for offset in 0 .. seats - 1 -> table.Seats[(table.ActiveSeat + 1 + offset) % seats] ]

    let allEmptyHanded table =
        table.Seats |> List.forall Player.isEmptyHanded
