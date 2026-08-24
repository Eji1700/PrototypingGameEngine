namespace Prototyping.Warband

/// A squad: five units on ten hexes, and everything that has to be asked of one before a blow can
/// be aimed. Nothing here changes over the course of a battle except how much is left of somebody.
type Standing = { Kind: Kind; Left: int }

type Squad = Map<Hex, Standing>

module Squad =

    /// Five units on ten hexes, and at most two alike - so a squad is at least three kinds of
    /// thing, and where the other five hexes are left empty is a choice as much as where the
    /// units go.
    [<Literal>]
    let Strong = 5

    [<Literal>]
    let Alike = 2

    let empty: Squad = Map.empty

    let at hex (squad: Squad) = Map.tryFind hex squad

    let mustered (squad: Squad) = Map.count squad

    let full squad = mustered squad >= Strong

    let manyOf kind (squad: Squad) =
        squad |> Map.filter (fun _ unit -> unit.Kind = kind) |> Map.count

    let joined hex kind (squad: Squad) : Squad =
        Map.add
            hex
            { Kind = kind
              Left = Kinds.vigour kind }
            squad

    let standing (squad: Squad) =
        squad |> Map.toList |> List.filter (fun (_, unit) -> unit.Left > 0)

    let broken squad = List.isEmpty (standing squad)

    let left squad =
        standing squad |> List.sumBy (fun (_, unit) -> unit.Left)


    /// The rank nearest the other squad that still has somebody on their feet. A strike falls
    /// there and nowhere else, which is what puts the front rank in front: empty it and the
    /// blows walk back to the middle.
    let foremost squad =
        Formation.ranks
        |> List.tryFind (fun rank -> standing squad |> List.exists (fun (hex, _) -> hex.Rank = rank))

    /// Who a strike falls on: whoever in that rank is still holding the line, which is the one
    /// with the most left in them. The bow does the exact opposite, and that is the whole reason
    /// a squad wants both.
    let stoutest rank squad =
        standing squad
        |> List.filter (fun (hex, _) -> hex.Rank = rank)
        |> List.sortBy (fun (hex, unit) -> -unit.Left, hex.Step)
        |> List.tryHead

    /// Who a shot finds: whoever is nearest to falling, rank no object.
    let nearestFalling squad =
        standing squad
        |> List.sortBy (fun (hex, unit) -> unit.Left, Formation.depth hex.Rank, hex.Step)
        |> List.tryHead

    /// The warder that steps in front of a blow aimed at this hex: one still up on a hex it
    /// touches, the furthest forward of them.
    ///
    /// A blow steps aside once and no further. Nothing steps in front of a blow aimed at a warder,
    /// or two warders either side of one hex would hand it back and forth for ever.
    let warder hex squad =
        match at hex squad with
        | Some unit when Kinds.guards unit.Kind -> None
        | _ ->
            Formation.touches hex
            |> List.choose (fun other ->
                match at other squad with
                | Some unit when unit.Left > 0 && Kinds.guards unit.Kind -> Some(other, unit)
                | _ -> None)
            |> List.sortBy (fun (other, _) -> Formation.depth other.Rank, other.Step)
            |> List.tryHead

    /// Who a mender tends: of the hexes it is handed, whoever is missing the most. The fallen are
    /// not among them - nothing here brings anybody back up.
    let mostHurt hexes squad =
        hexes
        |> List.choose (fun hex -> at hex squad |> Option.map (fun unit -> hex, unit))
        |> List.filter (fun (_, unit) -> unit.Left > 0 && unit.Left < Kinds.vigour unit.Kind)
        |> List.sortBy (fun (hex, unit) -> unit.Left - Kinds.vigour unit.Kind, Formation.depth hex.Rank, hex.Step)
        |> List.tryHead


    let hurt hex power (squad: Squad) =
        match at hex squad with
        | None -> squad, 0
        | Some unit ->
            let left = max 0 (unit.Left - power)
            Map.add hex { unit with Left = left } squad, left

    /// Mending, and how much of it landed: a unit two short of whole takes two of a mending of
    /// four, and the log says two rather than four.
    let mend hex power (squad: Squad) =
        match at hex squad with
        | None -> squad, 0, 0
        | Some unit ->
            let left = min (Kinds.vigour unit.Kind) (unit.Left + power)
            Map.add hex { unit with Left = left } squad, left - unit.Left, left
