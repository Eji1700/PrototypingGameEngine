namespace TCModel

/// The three kinds of stone in the game.
type StoneColor =
    | Red
    | Blue
    | Black

module StoneColor =

    /// Canonical ordering, used for display and for indexing into piles.
    let all = [ Red; Blue; Black ]

    let name =
        function
        | Red -> "Red"
        | Blue -> "Blue"
        | Black -> "Black"

    let glyph =
        function
        | Red -> 'R'
        | Blue -> 'B'
        | Black -> 'K'

    let tryParse (text: string) =
        match text.Trim().ToLowerInvariant() with
        | "r"
        | "red" -> Some Red
        | "b"
        | "blue" -> Some Blue
        | "k"
        | "black" -> Some Black
        | _ -> None

/// An immutable multiset of stones. Counts are always positive; a colour that is
/// absent from the underlying map is simply not held.
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

    /// The colour of the stone sitting at `index` when the pile is laid out in
    /// canonical order. Used to turn a uniform integer into a uniform stone.
    let private colorAt index pile =
        let rec walk remaining colors =
            match colors with
            | [] -> failwith "Pile index out of range."
            | color :: rest ->
                let held = count color pile
                if remaining < held then color else walk (remaining - held) rest

        walk index StoneColor.all

    /// Draw a single stone uniformly at random, yielding it and the diminished pile.
    let drawOne pile : Rand<(StoneColor * Pile) option> =
        match total pile with
        | 0 -> Rand.retn None
        | size ->
            Rand.intBelow size
            |> Rand.map (fun index ->
                let color = colorAt index pile
                Some(color, remove color 1 pile))

    /// Draw up to `n` stones at random, yielding the drawn stones and what is left.
    let draw n pile : Rand<Pile * Pile> =
        let rec loop remaining (drawn, source) =
            if remaining <= 0 then
                Rand.retn (drawn, source)
            else
                drawOne source
                |> Rand.bind (function
                    | None -> Rand.retn (drawn, source)
                    | Some(color, source) -> loop (remaining - 1) (add color 1 drawn, source))

        loop n (empty, pile)

type RegionId = RegionId of int

/// What a region is, which decides how it is seeded and who may enter it.
type RegionKind =
    /// The heartland of a colour, seeded with that colour's stones.
    | Home of StoneColor
    /// Contested ground, seeded at random.
    | Wild
    /// Stands apart from the map: no region borders it, and it starts empty.
    | Special
    /// Ground no stone may ever enter.
    | Dead

module RegionKind =

    /// Whether stones may be placed here at all.
    let isOpen kind =
        match kind with
        | Dead -> false
        | Home _
        | Wild
        | Special -> true

    /// Whether the region stands outside the map graph, bordering nothing.
    /// The dead region is deliberately not isolated: adjacency still runs through it.
    let isIsolated kind =
        match kind with
        | Special -> true
        | Home _
        | Wild
        | Dead -> false

    let describe kind =
        match kind with
        | Home color -> $"{StoneColor.name color} home"
        | Wild -> "wild"
        | Special -> "special"
        | Dead -> "dead"

type Region =
    { Id: RegionId
      Name: string
      Kind: RegionKind
      Stones: Pile }

module Region =

    let isOpen region = RegionKind.isOpen region.Kind

    let isIsolated region = RegionKind.isIsolated region.Kind

    let describeKind region = RegionKind.describe region.Kind

    let addStone color region =
        { region with Stones = Pile.add color 1 region.Stones }

type PlayerId = PlayerId of int

/// A player commands no faction of their own: the bag holds stones of any colour.
type Player =
    { Id: PlayerId
      /// The stones this player has yet to play.
      Bag: Pile }

module Player =
    let name player =
        let (PlayerId n) = player.Id
        $"Player {n}"
