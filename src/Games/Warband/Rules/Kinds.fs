namespace TCModel.Warband

/// What a unit does from a rank, and the whole of what a kind of unit is.
///
/// The same five people in the same five hexes are a different squad for being turned round: a
/// rider in the front rank strikes three times and one in the back has nowhere to ride from. That
/// is the one idea this game is built on, so it lives in the type rather than in a table of
/// exceptions - a kind is not "an archer with a bonus at range", it is three answers to the same
/// question, one for each rank.
type Stance =
    /// Hand to hand: it falls on the foremost rank of the other squad that still has anybody up.
    | Strikes of power: int * times: int

    /// Over the heads of the front rank: it ignores rank and finds whoever is nearest to falling.
    | Shoots of power: int * times: int

    /// Back into a neighbour on your own side - the one missing the most, and only one it touches.
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
        | Footman, Front -> Strikes(3, 2)
        | Footman, Middle -> Strikes(3, 1)
        | Footman, Back -> Strikes(1, 1)

        // A spear reaches past the rank in front of it, which is the one kind that would rather be
        // in the middle than at the front.
        | Spearman, Front -> Strikes(5, 1)
        | Spearman, Middle -> Strikes(3, 2)
        | Spearman, Back -> Strikes(1, 1)

        | Bowman, Front -> Strikes(1, 1)
        | Bowman, Middle -> Shoots(2, 2)
        | Bowman, Back -> Shoots(2, 3)

        // The charge, and the reason there is a rank a kind can be wasted on: there is no room to
        // ride from behind two ranks of your own people.
        | Rider, Front -> Strikes(3, 3)
        | Rider, Middle -> Strikes(3, 1)
        | Rider, Back -> Idles

        | Mender, Front -> Strikes(1, 1)
        | Mender, Middle -> Mends 2
        | Mender, Back -> Mends 4

        | Warder, Front -> Strikes(2, 2)
        | Warder, Middle -> Strikes(2, 1)
        | Warder, Back -> Strikes(1, 1)

    let byName (word: string) =
        let wanted = word.ToLowerInvariant()

        all
        |> List.tryFind (fun kind -> name kind = wanted || (code kind).ToLowerInvariant() = wanted)

    let names = all |> List.map name |> String.concat ", "
