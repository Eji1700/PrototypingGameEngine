namespace Prototyping.Compile

open Prototyping.Engine

module Draft =

    let order = [ Seat.at 1; Seat.at 2; Seat.at 2; Seat.at 1; Seat.at 1; Seat.at 2 ]

    [<Literal>]
    let Picks = 6

    let picking made = order |> List.tryItem made

    let picksBy seat =
        order |> List.filter ((=) seat) |> List.length
