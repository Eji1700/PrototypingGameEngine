namespace TCModel.Table

open TCModel.Engine

/// What has to be reading a view for it to be worth anything.
///
/// A board built out of Spectre's panels is escape codes to a browser, and a board built
/// out of HTML is angle brackets to a terminal. So this is not a preference: it says which
/// views a given reader may be offered at all, and it is what stops a console being handed
/// a screen it cannot show.
type Shown =
    | AtATerminal
    | InABrowser

/// How a game is shown, which a player chooses for themselves.
///
/// This is every screen a player ever reads. Nothing else in the program prints anything a
/// player would call part of the game, so a new way of showing one is written once, here,
/// and everything - one keyboard or five over a network - picks it up.
///
/// The endpoints take the model rather than somebody else's finished text, so a view is
/// free to lay a whole screen out differently rather than only colouring what it is handed.
/// A rich view does exactly that, building boards out of Spectre's panels, tables and charts
/// while a plain one writes blocks of text.
///
/// Generic in the game, and it is the seat that made it so: `Board` once took this game's
/// `Player` and `Ruling` took this game's `RegionId`, which is what kept every screen in the
/// program bound to one game. A seat is a `PlayerId` now, and anything else a game can be
/// asked about arrives at `Answer` as the words that were typed.
[<NoComparison; NoEquality>]
type View<'Move, 'State, 'Notice> =
    {
        Name: string
        Describe: string

        /// What has to be reading it. Nothing is offered to a reader that cannot show it,
        /// and nothing may be swapped for a view of the other kind.
        Shown: Shown

        /// The colours this one was built with. Every endpoint below has them already, so
        /// nothing needs to be told them twice; this is here so that a player who changes
        /// their colours can be handed the same view built again in the new ones, and so a
        /// console joining a table can say what it wants a board drawn in.
        Palette: Palette

        /// The whole board, drawn for one seat, with as much of the writing round it as the
        /// person reading has asked to keep.
        Board: Margins -> PlayerId -> Model<'Move, 'State, 'Notice> -> string

        /// The record of play so far, as the seat reading it may know it.
        History: PlayerId -> Model<'Move, 'State, 'Notice> -> string

        /// This game's own question, answered in the words it was asked in. A game with
        /// nothing to explain says so; nothing above here knows what there is to ask.
        Answer: string -> Model<'Move, 'State, 'Notice> -> string

        /// The rules and the commands, at length.
        Rules: string

        /// One line the game has said, with no board to go with it.
        Says: string -> string

        /// A table still waiting for people to arrive, in seating order.
        Waiting: Waiting list -> string
    }
