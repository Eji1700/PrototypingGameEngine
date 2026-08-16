namespace TCModel.Compile

open TCModel.Engine

/// The draft: six picks out of twelve protocols, 1-2-2-1.
///
/// One pick to the first player, two to the second, two back to the first, one back to the
/// second. Six picks, three each, and the reason for the shape is that going first is worth
/// something and this is what pays for it: the player who chose first chooses again only
/// after the other has had two.
///
/// Written out as the seats in the order they choose rather than as a rule to be worked out,
/// because it is six entries and a list of six is a thing `Faults` can count. Nothing here
/// knows what a protocol is - which of them is left is the game's business, and whose turn it
/// is to take one is this file's.
module Draft =

    /// The seats, in the order they pick.
    let order = [ Seat.at 1; Seat.at 2; Seat.at 2; Seat.at 1; Seat.at 1; Seat.at 2 ]

    /// How many picks a whole draft is.
    let Picks = List.length order

    /// Whose pick the next one is, given how many have been made. Nobody's, once they are
    /// all made.
    let picking made = order |> List.tryItem made

    /// How many of them a seat makes, for a game checking its own draft adds up.
    let picksBy seat =
        order |> List.filter ((=) seat) |> List.length
