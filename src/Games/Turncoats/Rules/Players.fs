namespace Prototyping.Turncoats

open Prototyping.Engine

type Player = { Id: PlayerId; Bag: Pile }

module Player =
    let isEmptyHanded player = Pile.isEmpty player.Bag

type Table =
    private
        { Seats: Player list
          ActiveSeat: int }

type Unseatable =
    | TooFewPlayers of int
    | TooManyPlayers of int

module Table =

    [<Literal>]
    let MinPlayers = 2

    [<Literal>]
    let MaxPlayers = 5

    let trySeat bags =
        match List.length bags with
        | n when n < MinPlayers -> Error(TooFewPlayers n)
        | n when n > MaxPlayers -> Error(TooManyPlayers n)
        | _ ->
            Ok
                { Seats = bags |> List.mapi (fun index bag -> { Id = Seat.at (index + 1); Bag = bag })
                  ActiveSeat = 0 }

    let players table = table.Seats

    let count table = List.length table.Seats

    let active table = table.Seats[table.ActiveSeat]

    let tryPlayer playerId table =
        table.Seats |> List.tryFind (fun player -> player.Id = playerId)

    let advance table =
        { table with
            ActiveSeat = (table.ActiveSeat + 1) % count table }

    let withActive player (table: Table) =
        { table with
            Seats =
                table.Seats
                |> List.mapi (fun seat other -> if seat = table.ActiveSeat then player else other) }

    let fromNext table =
        let seats = count table

        [ for offset in 0 .. seats - 1 -> table.Seats[(table.ActiveSeat + 1 + offset) % seats] ]

    let allEmptyHanded table =
        table.Seats |> List.forall Player.isEmptyHanded
