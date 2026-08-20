module TCModel.Games

let all =
    [ Play.chosen TCModel.Turncoats.Offer.ways TCModel.Turncoats.Offer.playable
      Play.chosen TCModel.TicTacToe.Offer.ways TCModel.TicTacToe.Offer.playable
      Play.chosen TCModel.Diplomacy.Offer.ways TCModel.Diplomacy.Offer.playable
      Play.chosen TCModel.Compile.Offer.ways TCModel.Compile.Offer.playable
      Play.chosen TCModel.Life.Offer.ways TCModel.Life.Offer.playable
      Play.chosen TCModel.Snake.Offer.ways TCModel.Snake.Offer.playable ]

let usually = List.head all

let byName (word: string) =
    let wanted = word.ToLowerInvariant()

    all
    |> List.tryFind (fun game -> game.Names |> List.contains wanted)
    |> Option.map (fun game -> game.As wanted)

let names = all |> List.map (fun game -> game.Name) |> String.concat ", "
