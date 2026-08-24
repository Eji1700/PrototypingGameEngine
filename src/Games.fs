module Prototyping.Games

let all =
    [ Play.chosen Prototyping.Turncoats.Offer.ways Prototyping.Turncoats.Offer.playable
      Play.chosen Prototyping.TicTacToe.Offer.ways Prototyping.TicTacToe.Offer.playable
      Play.chosen Prototyping.Diplomacy.Offer.ways Prototyping.Diplomacy.Offer.playable
      Play.chosen Prototyping.Compile.Offer.ways Prototyping.Compile.Offer.playable
      Play.chosen Prototyping.Life.Offer.ways Prototyping.Life.Offer.playable
      Play.chosen Prototyping.Snake.Offer.ways Prototyping.Snake.Offer.playable
      Play.chosen Prototyping.Cascade.Offer.ways Prototyping.Cascade.Offer.playable
      Play.chosen Prototyping.Warband.Offer.ways Prototyping.Warband.Offer.playable ]

let usually = List.head all

let byName (word: string) =
    let wanted = word.ToLowerInvariant()

    all
    |> List.tryFind (fun game -> game.Names |> List.contains wanted)
    |> Option.map (fun game -> game.As wanted)

let names = all |> List.map (fun game -> game.Name) |> String.concat ", "
