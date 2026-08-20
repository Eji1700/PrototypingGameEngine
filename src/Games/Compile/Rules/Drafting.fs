namespace TCModel.Compile

open TCModel.Engine

module Draft =

    let order = [ Seat.at 1; Seat.at 2; Seat.at 2; Seat.at 1; Seat.at 1; Seat.at 2 ]

    let Picks = List.length order

    let picking made = order |> List.tryItem made

    let picksBy seat =
        order |> List.filter ((=) seat) |> List.length
