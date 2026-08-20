#load "Harness.fsx"

open TCModel.Turncoats
open Harness

let private size =
    function
    | Open pile -> Pile.total pile
    | Closed n -> n

let private dealt = Setup.deal 3 7UL |> Result.toOption |> Option.get

let private beholder = Game.active dealt
let private view = Knowledge.seenBy beholder dealt

let private others =
    Game.players dealt |> List.filter (fun player -> player.Id <> beholder.Id)

let private bag playerId =
    view.Bags |> List.filter (fst >> (=) playerId) |> List.map snd


report "the beholder's own bag is laid open" [ Open beholder.Bag ] (bag beholder.Id)

report
    "every other bag shows its size and nothing else"
    (others |> List.map (fun player -> [ Closed(Pile.total player.Bag) ]))
    (others |> List.map (fun player -> bag player.Id))

report "the reserve shows its size and nothing else" (Closed(Pile.total dealt.Reserve)) view.Reserve


report
    "what is out of sight is the reserve and the other bags together"
    (Pile.toCounts (
        others
        |> List.fold (fun pile player -> Pile.merge player.Bag pile) dealt.Reserve
    ))
    (Pile.toCounts view.Unseen)

report
    "the sizes held back come to exactly what is out of sight"
    (Pile.total view.Unseen)
    (size view.Reserve + (others |> List.sumBy (fun player -> Pile.total player.Bag)))

report
    "every seat sees the map, its own bag and what is out of sight come to all 63"
    (Game.players dealt |> List.map (fun _ -> 63))
    (Game.players dealt
     |> List.map (fun player ->
         let seen = Knowledge.seenBy player dealt

         Pile.total (Position.total seen.Position)
         + Pile.total player.Bag
         + Pile.total seen.Unseen))


let private bare = Knowledge.laidBare beholder dealt

report "a game laid bare holds no bag back" (Game.players dealt |> List.map (fun player -> player.Id, Open player.Bag)) bare.Bags

report "a game laid bare opens the reserve too" (Open dealt.Reserve) bare.Reserve

finish ()
