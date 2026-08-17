namespace TCModel.Life

open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// both `Spectre.Console` and the command line's argument types carry names this game already
// uses.
open TCModel.Life

/// What this game colours, and what it is drawn with.
///
/// One slot, which is the fewest a game can have and still have any: there is one kind of
/// thing on this board, and it is either there or it is not. An empty square is not a second
/// thing to colour - it is the quiet colour every reader already has for whatever is not
/// being talked about.
module Ink =

    [<Literal>]
    let Key = "live"

    /// The two characters a board is drawn out of. ASCII on purpose: the plainest way of
    /// reading this game is the one with nothing in it a terminal has to understand, and a
    /// board is the last place to spend that.
    [<Literal>]
    let Living = "#"

    [<Literal>]
    let Empty = "."

    let slots =
        [ { Key = Key
            Draws = "the living cells, and the cells named in what the game says"
            Shows = String.replicate 3 Living
            Standard = Palette.moss } ]

    let ink palette = Palette.inkOf Key palette

    /// This board's alphabet, which is one rule.
    ///
    /// Nothing here is the board: colour is laid over text that has already been drawn, and
    /// the board is drawn as spans that carry their own colour and never come through here. So
    /// what is left is prose - a line of the log, a page of rules - and the one thing in this
    /// game's prose worth picking out is a cell's name, which is a letter and a number and
    /// looks like nothing else.
    let marking =
        { Patterns = [ @"(?<cell>\b[a-z][0-9]{1,2}\b)" ]
          Paint = fun palette found -> Tint.wrap (ink palette) found.Value }
