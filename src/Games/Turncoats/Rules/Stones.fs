namespace Prototyping.Turncoats

open Prototyping.Common

type StoneColour =
    | Red
    | Blue
    | Green

module StoneColour =
    let all = [ Red; Blue; Green ]

type Pile = private Pile of Map<StoneColour, int>

module Pile =

    let empty = Pile Map.empty

    let count colour (Pile counts) =
        counts |> Map.tryFind colour |> Option.defaultValue 0

    let total (Pile counts) =
        counts |> Map.fold (fun sum _ n -> sum + n) 0

    let isEmpty pile = total pile = 0

    let private withCount colour n (Pile counts) =
        Pile(if n <= 0 then Map.remove colour counts else Map.add colour n counts)

    let add colour n pile =
        if n <= 0 then pile else withCount colour (count colour pile + n) pile

    let remove colour n pile =
        if n <= 0 then pile else withCount colour (count colour pile - n) pile

    let tryTake colour n pile =
        if count colour pile >= n then Some(remove colour n pile) else None

    let ofCounts pairs =
        pairs |> List.fold (fun pile (colour, n) -> add colour n pile) empty

    let ofColours colours =
        colours |> Seq.fold (fun pile colour -> add colour 1 pile) empty

    let merge (Pile counts) pile =
        counts |> Map.fold (fun pile colour n -> add colour n pile) pile

    let without (Pile counts) pile =
        counts |> Map.fold (fun pile colour n -> remove colour n pile) pile

    let toCounts pile =
        StoneColour.all
        |> List.choose (fun colour ->
            match count colour pile with
            | 0 -> None
            | n -> Some(colour, n))

    let toColours pile =
        toCounts pile |> List.collect (fun (colour, n) -> List.replicate n colour)

    let drawOne pile rng =
        match toColours pile with
        | [] -> None, rng
        | stones ->
            let colour, rng = Rng.pick stones rng
            Some(colour, remove colour 1 pile), rng

    let draw n pile rng =
        let rec loop remaining drawn source rng =
            if remaining <= 0 then
                (drawn, source), rng
            else
                match drawOne source rng with
                | None, rng -> (drawn, source), rng
                | Some(colour, source), rng -> loop (remaining - 1) (add colour 1 drawn) source rng

        loop n empty pile rng
