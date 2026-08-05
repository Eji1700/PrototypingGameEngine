namespace TCModel.Console

open TCModel.Domain
open TCModel.App

/// What has to be reading a view for it to be worth anything.
///
/// A board built out of Spectre's panels is escape codes to a browser, and a board built
/// out of HTML is angle brackets to a terminal. So this is not a preference: it says which
/// views a given reader may be offered at all, and it is what stops a console being handed
/// a screen it cannot show.
type Shown =
    | AtATerminal
    | InABrowser

/// How the game is shown, which a player chooses for themselves.
///
/// This is every screen a player ever reads. Nothing else in the program prints anything a
/// player would call part of the game, so a new way of showing it is written once, here,
/// and everything - one keyboard or five over a network - picks it up.
///
/// The endpoints take the model rather than somebody else's finished text, so a view is
/// free to lay a whole screen out differently rather than only colouring what it is
/// handed. `rich` does exactly that, building boards out of Spectre's panels, tables and
/// charts while `plain` writes blocks of text.
///
/// Adding to the game means adding an endpoint here and answering it in every view. That
/// is the trade: a wide seam, but one that cannot be half-implemented without the compiler
/// saying so.
///
/// A view carries functions, so two of them can only be told apart by name - which is what
/// `byName` is for.
[<NoComparison; NoEquality>]
type View =
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

        /// The whole board, drawn for one player. The flag is whether the writing that
        /// explains the board comes with it.
        Board: bool -> Player -> Model -> string

        /// The record of play so far, as the player reading it may know it.
        History: Player -> Model -> string

        /// The working behind who rules a region.
        Ruling: RegionId -> Model -> string

        /// The rules and the commands, at length.
        Rules: string

        /// One line the game has said, with no board to go with it.
        Says: string -> string

        /// A table still waiting for people to arrive, in seating order.
        Waiting: Waiting list -> string
    }

module View =

    /// The board as `Render` writes it: blocks of text, and nothing this terminal has to
    /// understand. It draws in no colour at all, and still carries the palette, so that a
    /// player who sets their colours here and then asks for `rich` gets the ones they set.
    let plain palette =
        { Name = "plain"
          Describe = "plain text, and nothing this terminal has to understand"
          Shown = AtATerminal
          Palette = palette
          Board = Render.model
          History = Render.history
          Ruling = Render.explainRule
          Rules = Render.help
          Says = id
          Waiting = Render.waiting }

    /// The same game built out of panels, tables and charts, in colour.
    let rich palette =
        { Name = "rich"
          Describe = "panels, charts and colour, for a terminal that can show them"
          Shown = AtATerminal
          Palette = palette
          Board = Rich.board palette
          History = Rich.history palette
          Ruling = Rich.ruling palette
          Rules = Rich.rules palette
          Says = Tint.paint palette
          Waiting = Rich.waiting palette }

    /// The same game as a page, for somebody reading in a browser.
    ///
    /// This one takes no palette into its endpoints, and it is the only one that does not.
    /// A page carries its colours in its own head - `Html.page` writes them there - and
    /// every fragment below draws in those rather than in colours of its own, so a board
    /// is built once however many people are reading it. The palette is still kept here,
    /// because that is what a player who changes their colours changes.
    let html palette =
        { Name = "html"
          Describe = "a page, for a player reading in a browser"
          Shown = InABrowser
          Palette = palette
          Board = Html.board
          History = Html.history
          Ruling = Html.ruling
          Rules = Html.rules
          Says = Html.says
          Waiting = Html.waiting }

    /// Every view there is, in the order they are offered. A new one is added here and
    /// nowhere else: the menu, the command line and the prompt all read this list rather
    /// than keeping their own.
    let all palette =
        [ plain palette; rich palette; html palette ]

    /// Those a given reader can actually show. Everything that offers a choice asks for
    /// this rather than for `all`, so nobody is offered a screen they could not read.
    let offered shown palette =
        all palette |> List.filter (fun view -> view.Shown = shown)

    /// What there is to choose from. Which views there are does not depend on what colour
    /// they are drawn in, so this asks for the list in the standard ones and reads off the
    /// names.
    let namesFor shown =
        offered shown Palette.standard
        |> List.map (fun view -> view.Name)
        |> String.concat ", "

    /// Find a view by name, among those the reader asking could show.
    ///
    /// The reader is part of the question rather than something checked afterwards: a
    /// console asking for `html` is not asking for something that exists elsewhere, it is
    /// asking for something it cannot read, and the answer that helps is the list of what
    /// it can.
    let byName shown palette (name: string) =
        let wanted = name.ToLowerInvariant()

        match offered shown palette |> List.tryFind (fun view -> view.Name = wanted) with
        | Some view -> Ok view
        | None -> Error $"'{name}' is not a way of showing the game here. There is {namesFor shown}."

    /// The same view, built again in different colours. A view is its endpoints, and those
    /// have the old palette baked into them, so changing colours means asking for the view
    /// afresh rather than putting a new palette on the one in hand.
    let recoloured palette (view: View) =
        all palette
        |> List.tryFind (fun other -> other.Name = view.Name)
        |> Option.defaultValue (plain palette)
