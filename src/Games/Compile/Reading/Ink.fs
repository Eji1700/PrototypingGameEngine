namespace TCModel.Compile

open TCModel.Engine
open TCModel.Table
// Last, so this game's own names win: an explicit open outranks the enclosing namespace, and
// both `Spectre.Console` and the command line's argument types carry names this game already
// uses.
open TCModel.Compile

/// What this game colours.
///
/// The two players, and not the twelve protocols. A protocol is drafted rather than owned to
/// begin with, and the thing a reader needs to tell apart on a line is whose half of it they
/// are looking at - which is one question with two answers, across the table from each other.
module Ink =

    let key seat = $"p{PlayerId.value seat}"

    let private slot seat standard =
        { Key = key seat
          Draws = $"{Words.player seat}'s protocols, stacks and cards"
          Shows = Words.player seat
          Standard = standard }

    let slots = [ slot (Seat.at 1) Palette.crimson; slot (Seat.at 2) Palette.azure ]

    /// A seat's colour as markup says it.
    let ink palette seat = Palette.inkOf (key seat) palette

    /// This game's alphabet, in the order the rules should win.
    ///
    /// One rule, and it is a seat rather than a card: the log is full of sentences that open
    /// with who did the thing, and colouring that is the whole of what makes a run of them
    /// readable at a glance. Card and protocol names are left alone on purpose - a line
    /// saying "Player 1 drafts Fire" would be two colours arguing about which fact matters.
    let marking =
        { Patterns = [ @"(?<seat>\bPlayer [12]\b)" ]
          Paint =
            fun palette found ->
                let seat = if found.Value.EndsWith "1" then Seat.at 1 else Seat.at 2
                Tint.wrap (ink palette seat) found.Value }
