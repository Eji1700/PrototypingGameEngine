namespace TCModel.Diplomacy

open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// both `Spectre.Console` and the command line's argument types carry names this game already
// uses - `Open`.
open TCModel.Diplomacy

/// What this game colours.
///
/// Seven things, against the other games' four and two, and that is the whole argument for the
/// list being a list. The colour screen, the line that changes one, the form on the page and
/// both halves of sending a palette down a wire work off whatever is here, and not one of them
/// has an opinion about how many there should be.
module Ink =

    let key = Power.key

    /// The colours a board is drawn in unless somebody says otherwise. As close to the ones
    /// printed on the board everybody knows as nineteen terminal colours get: Austria red,
    /// England blue, France a paler blue, Germany the flat grey-white that stands in for black
    /// on a dark screen, Italy green, Turkey yellow. Russia's is the one that had to move -
    /// its counters are white, and white is what everything else on a terminal already is.
    let private standardFor =
        function
        | Austria -> Palette.crimson
        | England -> Palette.azure
        | France -> Palette.shades |> List.find (fun shade -> shade.Name = "sky")
        | Germany -> Palette.bone
        | Italy -> Palette.moss
        | Russia -> Palette.shades |> List.find (fun shade -> shade.Name = "violet")
        | Turkey -> Palette.gold

    let private slot power =
        { Key = key power
          Draws = $"{Power.name power}: its units, its centres and its name"
          Shows = $"{Power.letter power}  {Power.name power}"
          Standard = standardFor power }

    let slots = Power.all |> List.map slot

    let ink palette power = Palette.inkOf (key power) palette

    /// The quiet colour for everything that is nobody's: a centre no power holds, a province
    /// with nothing standing in it, a border. Not a slot - every game wants one of these and no
    /// two agree what it is for.
    let hidden _ = Palette.ink Palette.slate

    /// This board's alphabet, in the order the rules should win.
    ///
    /// One rule, and it is the powers' names. Colour is laid over text that has already been
    /// drawn, so what is here can wrap characters and cannot move one - which rules out the
    /// province codes, since `bal` is a sea and also three letters in the middle of "Balkans".
    let marking =
        let words =
            Power.all
            |> List.collect (fun power -> [ Power.name power; Power.adjective power ])
            |> String.concat "|"

        { Patterns = [ $@"(?<power>\b(?:{words})\b)" ]
          Paint =
            fun palette found ->
                match Power.byName found.Value with
                | Some power -> Tint.wrap (ink palette power) found.Value
                | None -> found.Value }
