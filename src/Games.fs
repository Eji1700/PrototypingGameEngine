module TCModel.Games

/// The games there are, and the only file in the program that names more than one.
///
/// Each has already been sealed into a `Chosen`, so nothing here knows what a move is at
/// either of them - which is what lets them sit in one list. Adding a game is a line.
///
/// In the order they are offered. The first is what somebody who says nothing gets.
///
/// Six entries and six games. It was five entries and four games until the settings screen grew
/// a page for a game's own choices: Compile was in this list twice, once plain and once with its
/// optional rule, so the picker asked which game and then asked the same question again in
/// different words. A game offers its ways itself now, and this list is a list of games again.
///
/// The last two are the ones that argue with the rest. Life is not a game of turns at all - one
/// seat, no opponent, nothing to win - and Snake does not wait for anybody: its board moves on a
/// clock and what a player presses only steers it. Both sit in this list exactly like the
/// others, which is the whole of what their being here proves.
let all =
    [ Play.chosen TCModel.Turncoats.Offer.ways TCModel.Turncoats.Offer.playable
      Play.chosen TCModel.TicTacToe.Offer.ways TCModel.TicTacToe.Offer.playable
      Play.chosen TCModel.Diplomacy.Offer.ways TCModel.Diplomacy.Offer.playable
      Play.chosen TCModel.Compile.Offer.ways TCModel.Compile.Offer.playable
      Play.chosen TCModel.Life.Offer.ways TCModel.Life.Offer.playable
      Play.chosen TCModel.Snake.Offer.ways TCModel.Snake.Offer.playable ]

/// The one a line that named no game is about. There is always at least one game, so this
/// is a list that cannot be empty rather than an answer that might not be there.
let usually = List.head all

/// The game a word names, opened as the way that word names.
///
/// A game answers to one name for every way it can be played, so `compile-control` still opens
/// a game of Compile with its optional rule in it - and does so however the settings were last
/// left, because a line that names a way outright has said which and is not asking. Which is
/// what keeps every command line anybody wrote down before that game had a settings page still
/// meaning exactly what it meant then.
let byName (word: string) =
    let wanted = word.ToLowerInvariant()

    all
    |> List.tryFind (fun game -> game.Names |> List.contains wanted)
    |> Option.map (fun game -> game.As wanted)

/// What there is to choose from, said to somebody who named none of it. The games rather than
/// every way of playing them: a picker offering `compile` and `compile-control` as though they
/// were two games is the list this file used to be.
let names = all |> List.map (fun game -> game.Name) |> String.concat ", "
