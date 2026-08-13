module TCModel.Games

/// The games there are, and the only file in the program that names more than one.
///
/// Each has already been sealed into a `Chosen`, so nothing here knows what a move is at
/// either of them - which is what lets them sit in one list. Adding a game is a line.
///
/// In the order they are offered. The first is what somebody who says nothing gets.
let all =
    [ Play.chosen TCModel.Turncoats.Offer.playable
      Play.chosen TCModel.TicTacToe.Offer.playable
      Play.chosen TCModel.Diplomacy.Offer.playable
      Play.chosen TCModel.Compile.Offer.playable ]

/// The one a line that named no game is about. There is always at least one game, so this
/// is a list that cannot be empty rather than an answer that might not be there.
let usually = List.head all

let byName (word: string) =
    let wanted = word.ToLowerInvariant()
    all |> List.tryFind (fun game -> game.Name = wanted)

let names = all |> List.map (fun game -> game.Name) |> String.concat ", "
