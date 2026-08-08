namespace TCModel.Turncoats

open TCModel.Common

/// The three kinds of stone, and so the three factions.
type StoneColor =
    | Red
    | Blue
    | Green

module StoneColor =
    /// Canonical ordering, used wherever colours are listed or indexed.
    let all = [ Red; Blue; Green ]

/// An immutable multiset of stones. Counts are always positive: a colour that is
/// absent is simply not held, so a pile can never carry a negative or zero count.
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

    /// Remove `n` stones, or None when the pile does not hold that many.
    let tryTake color n pile =
        if count color pile >= n then Some(remove color n pile) else None

    let ofCounts pairs =
        pairs |> List.fold (fun pile (color, n) -> add color n pile) empty

    let ofColors colors =
        colors |> Seq.fold (fun pile color -> add color 1 pile) empty

    /// Everything in both piles.
    let merge (Pile counts) pile =
        counts |> Map.fold (fun pile color n -> add color n pile) pile

    /// What is left of a pile once everything in the first is taken out of it.
    let without (Pile counts) pile =
        counts |> Map.fold (fun pile color n -> remove color n pile) pile

    /// Counts in canonical colour order, omitting colours that are absent.
    let toCounts pile =
        StoneColor.all
        |> List.choose (fun color ->
            match count color pile with
            | 0 -> None
            | n -> Some(color, n))

    /// The individual stones, in canonical colour order.
    let toColors pile =
        toCounts pile |> List.collect (fun (color, n) -> List.replicate n color)

    /// The colour of the stone at `index` when the pile is laid out in canonical
    /// order. Turns a uniform integer into a uniformly drawn stone.
    let private colorAt index pile =
        let rec walk remaining colors =
            match colors with
            | [] -> failwith "Pile index out of range."
            | color :: rest ->
                let held = count color pile
                if remaining < held then color else walk (remaining - held) rest

        walk index StoneColor.all

    /// Draw a single stone uniformly at random, yielding it, the diminished pile,
    /// and the generator to carry on with.
    let drawOne pile rng =
        match total pile with
        | 0 -> None, rng
        | size ->
            let index, rng = Rng.intBelow size rng
            let color = colorAt index pile
            Some(color, remove color 1 pile), rng

    /// Draw up to `n` stones at random, yielding the drawn stones and what is left.
    let draw n pile rng =
        let rec loop remaining drawn source rng =
            if remaining <= 0 then
                (drawn, source), rng
            else
                match drawOne source rng with
                | None, rng -> (drawn, source), rng
                | Some(color, source), rng -> loop (remaining - 1) (add color 1 drawn) source rng

        loop n empty pile rng
