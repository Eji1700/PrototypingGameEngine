namespace TCModel.Warband

/// Where the five of a squad stand: ten hexes in three ranks, three across the front, four across
/// the middle and three at the back.
///
/// The ranks are offset by half a hex, which is the whole difference between this and a three by
/// three square. On squares every cell has the same four orthogonal neighbours and a rank is three
/// wide wherever you stand in it. Here the middle rank is four wide and its two inner hexes touch
/// six others, while a corner of the front rank touches three - so a warder in the middle shields
/// twice what one in a corner does, and a mender at m2 can reach both ranks at once. Nothing else
/// in this game knows about hexes; everything else asks this module what touches what.
type Rank =
    | Front
    | Middle
    | Back

type Hex = { Rank: Rank; Step: int }

module Formation =

    /// The three ranks, front first, which is also the order anything picked "furthest forward" is
    /// picked in.
    let ranks = [ Front; Middle; Back ]

    let wide =
        function
        | Middle -> 4
        | Front
        | Back -> 3

    let hexes =
        [ for rank in ranks do
              for step in 1 .. wide rank -> { Rank = rank; Step = step } ]

    let holds hex =
        hex.Step >= 1 && hex.Step <= wide hex.Rank

    let letter =
        function
        | Front -> "f"
        | Middle -> "m"
        | Back -> "b"

    /// How far back a rank is, front first. What orders the ranks wherever one hex has to be picked
    /// over another - which unit a strike falls on, which warder steps in.
    let depth =
        function
        | Front -> 0
        | Middle -> 1
        | Back -> 2

    let name hex = $"{letter hex.Rank}{hex.Step}"

    let read (word: string) =
        match List.ofSeq (word.ToLowerInvariant()) with
        | [ letter; digit ] when System.Char.IsAsciiDigit digit ->
            (match letter with
             | 'f' -> Some Front
             | 'm' -> Some Middle
             | 'b' -> Some Back
             | _ -> None)
            |> Option.map (fun rank ->
                { Rank = rank
                  Step = int digit - int '0' })
            |> Option.filter holds
        | _ -> None


    /// Where a hex sits across the formation, counted in halves so the offset ranks come out whole
    /// numbers: the middle rank on the even ones and the two short ranks between them. Two hexes
    /// in one rank are two apart; two in touching ranks are one apart when they meet at a side.
    let private across hex =
        match hex.Rank with
        | Middle -> 2 * hex.Step - 2
        | Front
        | Back -> 2 * hex.Step - 1

    let private apart one other = abs (across one - across other)

    let private neighbouring one other =
        match one.Rank, other.Rank with
        | Front, Back
        | Back, Front -> false
        | one', other' when one' = other' -> apart one other = 2
        | _ -> apart one other = 1

    /// The hexes a hex meets at a side. Three at a corner, four along an edge, six in the middle -
    /// and the front and back ranks never touch, since the middle rank is between them.
    let touches hex = hexes |> List.filter (neighbouring hex)
