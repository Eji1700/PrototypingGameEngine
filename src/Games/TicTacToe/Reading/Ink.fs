namespace TCModel.TicTacToe

open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace,
// and both `Spectre.Console` and the command line's argument types carry names this game
// already uses - `Open`.
open TCModel.TicTacToe

/// What this game colours.
///
/// Two things, against the other game's four, and that is the point of it being a list: the
/// colour screen, the line that changes one, the form on the page and the two halves of
/// sending a palette down a wire all work off whatever is here and none of them has an
/// opinion about how many there should be.
module Ink =

    let key =
        function
        | Cross -> "x"
        | Nought -> "o"

    let private slot mark standard =
        { Key = key mark
          Draws = $"the {(Words.named mark).ToLowerInvariant()}, and the line that wins with them"
          Shows = $"{Words.mark mark} {Words.mark mark} {Words.mark mark}"
          Standard = standard }

    let slots = [ slot Cross Palette.crimson; slot Nought Palette.azure ]

    /// A mark's colour as markup says it.
    let ink palette mark = Palette.inkOf (key mark) palette

    /// The quiet colour a free square and everything else that is not a mark is drawn in.
    /// Every game wants one of these and none of them agrees what it is for, so it is not a
    /// slot: it is the colour of whichever mark is not being talked about, which on this
    /// board is nobody's.
    let hidden _ = Palette.ink Palette.slate

    /// This board's alphabet, in the order the rules should win.
    ///
    /// Two rules, against the other game's seven, and neither of them is a board: colour is
    /// laid over text that has already been drawn, so what is here can wrap characters and
    /// cannot move one. The table's own - headings, `(you)`, the arrow - go first and are
    /// not written here.
    let marking =
        { Patterns =
            [ // The marks named in prose: "Crosses has three in a row".
              @"(?<named>\b(?:Crosses|Noughts)\b)"
              // And drawn on the board, or naming a seat: a lone X or O.
              @"(?<mark>\b[XO]\b)" ]
          Paint =
            fun palette found ->
                // Both groups start with the letter that says which mark it is - X and
                // Crosses, O and Noughts - so one test serves for either.
                let mark =
                    if found.Value.StartsWith "X" || found.Value.StartsWith "C" then Cross else Nought

                Tint.wrap (ink palette mark) found.Value }
