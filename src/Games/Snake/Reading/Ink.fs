namespace TCModel.Snake

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// both `Spectre.Console` and the command line's argument types carry names this game already
// uses.
open TCModel.Snake

/// What this game colours, and what it is drawn with.
///
/// Five slots: one per seat, and the food. Four of them whatever the table's size, because
/// what a game colours is a fact about the game rather than about the deal - the colour screen
/// is read before anybody has said how many are playing, and a player who likes their snake
/// green should not have to sit down twice to say so.
module Ink =

    /// The alphabet a board is drawn in. A snake is its letter: its head in capitals and the
    /// rest in small, so a board tells you which way every snake is pointing without colour,
    /// at a terminal that has none.
    ///
    /// A snake that has stopped is drawn as wreckage instead, in the quiet colour - because
    /// what it is now is an obstacle, and the one thing anybody needs to know about it is that
    /// it is in the way.
    [<Literal>]
    let Wreck = "#"

    [<Literal>]
    let Food = "*"

    [<Literal>]
    let Empty = "."

    let key seat = string (Words.letter seat)

    let head seat =
        string (System.Char.ToUpperInvariant(Words.letter seat))

    let body seat = string (Words.letter seat)

    let private slot place standard =
        let seat = Seat.at place

        { Key = key seat
          Draws = $"{Words.player seat}, head and all"
          Shows = $"{body seat}{body seat}{head seat}"
          Standard = standard }

    let food =
        { Key = "food"
          Draws = "the food, wherever it has landed"
          Shows = Food + Food + Food
          Standard = Palette.gold }

    let slots =
        [ slot 1 Palette.moss
          slot 2 Palette.crimson
          slot 3 Palette.azure
          slot 4 (Palette.named "violet")
          food ]

    /// This board's alphabet in prose, which is one rule.
    ///
    /// Nothing here is the board: the board is drawn as spans that carry their own colour and
    /// never come through here. What is left is prose - a line of the log, a page of rules -
    /// and the one thing in this game's prose worth picking out is which snake is being talked
    /// about.
    let marking =
        { Patterns = [ @"(?<snake>\bSnake [A-Z]\b)" ]
          Paint =
            fun palette found ->
                let letter = System.Char.ToLowerInvariant(found.Value[found.Value.Length - 1])
                Tint.wrap (Palette.inkOf (string letter) palette) found.Value }
