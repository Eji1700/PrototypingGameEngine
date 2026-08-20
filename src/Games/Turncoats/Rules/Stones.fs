namespace TCModel.Turncoats

open TCModel.Common

type StoneColor =
    | Red
    | Blue
    | Green

module StoneColor =
    let all = [ Red; Blue; Green ]

type Pile = private Pile of Map<StoneColor, int>

module Pile =

    let empty = Pile Map.empty

    let count color (Pile counts) =
        counts |> Map.tryFind color |> Option.defaultValue 0

    let total (Pile counts) =
        counts |> Map.fold (fun sum _ n -> sum + n) 0

    let isEmpty pile = total pile = 0

    let private withCount color n (Pile counts) =
        Pile(if n <= 0 then Map.remove color counts else Map.add color n counts)

    let add color n pile =
        if n <= 0 then pile else withCount color (count color pile + n) pile

    let remove color n pile =
        if n <= 0 then pile else withCount color (count color pile - n) pile

    let tryTake color n pile =
        if count color pile >= n then Some(remove color n pile) else None

    let ofCounts pairs =
        pairs |> List.fold (fun pile (color, n) -> add color n pile) empty

    let ofColors colors =
        colors |> Seq.fold (fun pile color -> add color 1 pile) empty

    let merge (Pile counts) pile =
        counts |> Map.fold (fun pile color n -> add color n pile) pile

    let without (Pile counts) pile =
        counts |> Map.fold (fun pile color n -> remove color n pile) pile

    let toCounts pile =
        StoneColor.all
        |> List.choose (fun color ->
            match count color pile with
            | 0 -> None
            | n -> Some(color, n))

    let toColors pile =
        toCounts pile |> List.collect (fun (color, n) -> List.replicate n color)

    // A pile is counts rather than a bag of stones, so drawing one at random means treating those
    // counts as one run and walking to whichever colour the index falls in.
    let private colorAt index pile =
        let rec walk remaining colors =
            match colors with
            | [] -> failwith "Pile index out of range."
            | color :: rest ->
                let held = count color pile
                if remaining < held then color else walk (remaining - held) rest

        walk index StoneColor.all

    let drawOne pile rng =
        match total pile with
        | 0 -> None, rng
        | size ->
            let index, rng = Rng.intBelow size rng
            let color = colorAt index pile
            Some(color, remove color 1 pile), rng

    let draw n pile rng =
        let rec loop remaining drawn source rng =
            if remaining <= 0 then
                (drawn, source), rng
            else
                match drawOne source rng with
                | None, rng -> (drawn, source), rng
                | Some(color, source), rng -> loop (remaining - 1) (add color 1 drawn) source rng

        loop n empty pile rng
