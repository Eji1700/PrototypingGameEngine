namespace TCModel.Warband

/// What a unit does from a rank, and the whole of what a kind of unit is.
///
/// The same five people in the same five hexes are a different squad for being turned round: a
/// rider in the front rank strikes three times and one in the back has nowhere to ride from. That
/// is the one idea this game is built on, so it lives in the type rather than in a table of
/// exceptions - a kind is not "an archer with a bonus at range", it is three answers to the same
/// question, one for each rank.
/// `reach` on the two that cross the ground is how many hexes of it a blow will carry. The two
/// lines stand a hex apart to begin with, so at the moment every reach here is enough and none of
/// them bites; wind the ground out and they start to, one kind at a time. Nothing here counts the
/// ranks *within* a formation towards the crossing yet - a rank is who you are, not how far away
/// you are - which is a thing to look at again when a unit gets stats of its own.
type Stance =
    /// Hand to hand: it falls on the foremost rank of the other squad that still has anybody up.
    | Strikes of power: int * times: int * reach: int

    /// Over the heads of the front rank: it ignores rank and finds whoever is nearest to falling.
    | Shoots of power: int * times: int * reach: int

    /// Back into a neighbour on your own side - the one missing the most, and only one it touches.
    /// Nothing about mending crosses the ground, so no reach comes into it.
    | Mends of power: int

    /// A rank this kind can do nothing from.
    | Idles

type Kind =
    | Footman
    | Spearman
    | Bowman
    | Rider
    | Mender
    | Warder

module Kinds =

    let all = [ Footman; Spearman; Bowman; Rider; Mender; Warder ]

    /// The word a player types and the record writes. Lower case, one word, and no two alike.
    let name =
        function
        | Footman -> "footman"
        | Spearman -> "spearman"
        | Bowman -> "bowman"
        | Rider -> "rider"
        | Mender -> "mender"
        | Warder -> "warder"

    let plural =
        function
        | Footman -> "footmen"
        | Spearman -> "spearmen"
        | Bowman -> "bowmen"
        | Rider -> "riders"
        | Mender -> "menders"
        | Warder -> "warders"

    /// Four letters, because that is what fits in a hex on a board of twenty of them.
    let code =
        function
        | Footman -> "Foot"
        | Spearman -> "Pike"
        | Bowman -> "Bow"
        | Rider -> "Ride"
        | Mender -> "Mend"
        | Warder -> "Ward"

    let vigour =
        function
        | Footman -> 10
        | Spearman -> 9
        | Bowman -> 7
        | Rider -> 12
        | Mender -> 6
        | Warder -> 14

    /// Who swings first. Nothing else turns on it, and it is what decides a round where two units
    /// would each have felled the other.
    let quick =
        function
        | Rider -> 5
        | Bowman -> 4
        | Footman -> 3
        | Spearman -> 3
        | Mender -> 2
        | Warder -> 1

    /// A warder stands in front of a blow aimed at a hex it touches. It does that from any rank -
    /// what the rank changes is how many hexes it is touching, and a corner touches half what the
    /// middle does.
    let guards =
        function
        | Warder -> true
        | _ -> false

    let stance rank kind =
        match kind, rank with
        | Footman, Front -> Strikes(3, 2, 1)
        | Footman, Middle -> Strikes(3, 1, 1)
        | Footman, Back -> Strikes(1, 1, 1)

        // A spear reaches past the rank in front of it, which is the one kind that would rather be
        // in the middle than at the front - and it reaches across a hex of ground as well, which is
        // the only melee that does.
        | Spearman, Front -> Strikes(5, 1, 2)
        | Spearman, Middle -> Strikes(3, 2, 2)
        | Spearman, Back -> Strikes(1, 1, 2)

        | Bowman, Front -> Strikes(1, 1, 1)
        | Bowman, Middle -> Shoots(2, 2, 4)
        | Bowman, Back -> Shoots(2, 3, 4)

        // The charge, and the reason there is a rank a kind can be wasted on: there is no room to
        // ride from behind two ranks of your own people. A charge from the front rank crosses a hex
        // of ground to arrive; one from the middle is only a horse in a crowd.
        | Rider, Front -> Strikes(3, 3, 2)
        | Rider, Middle -> Strikes(3, 1, 1)
        | Rider, Back -> Idles

        | Mender, Front -> Strikes(1, 1, 1)
        | Mender, Middle -> Mends 2
        | Mender, Back -> Mends 4

        | Warder, Front -> Strikes(2, 2, 1)
        | Warder, Middle -> Strikes(2, 1, 1)
        | Warder, Back -> Strikes(1, 1, 1)

    /// How many hexes of ground a stance will carry across. Nought for the two that never leave
    /// their own formation, which is not "no reach" so much as "the question does not arise".
    let reach =
        function
        | Strikes(_, _, reach)
        | Shoots(_, _, reach) -> reach
        | Mends _
        | Idles -> 0

    /// Whether a stance can do anything at all with that much ground between the lines.
    let carries engaged stance =
        match stance with
        | Strikes _
        | Shoots _ -> reach stance >= engaged
        | Mends _
        | Idles -> false

    /// The furthest this kind reaches from any rank - what a table of six of them can show in one
    /// narrow column. Which rank it reaches that far from is `stance`'s to say, and `why <kind>`
    /// reads it out rank by rank.
    let furthest kind =
        Formation.ranks |> List.map (fun rank -> reach (stance rank kind)) |> List.max

    let byName (word: string) =
        let wanted = word.ToLowerInvariant()

        all
        |> List.tryFind (fun kind -> name kind = wanted || (code kind).ToLowerInvariant() = wanted)

    let names = all |> List.map name |> String.concat ", "
