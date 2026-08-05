namespace TCModel.Console

open TCModel.Domain
open TCModel.App

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
    { Name: string
      Describe: string

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
      Waiting: Waiting list -> string }

module View =

    /// The board as `Render` writes it: blocks of text, and nothing this terminal has to
    /// understand.
    let plain =
        { Name = "plain"
          Describe = "plain text, and nothing this terminal has to understand"
          Board = Render.model
          History = Render.history
          Ruling = Render.explainRule
          Rules = Render.help
          Says = id
          Waiting = Render.waiting }

    /// The same game built out of panels, tables and charts, in colour.
    let rich =
        { Name = "rich"
          Describe = "panels, charts and colour, for a terminal that can show them"
          Board = Rich.board
          History = Rich.history
          Ruling = Rich.ruling
          Rules = Rich.rules
          Says = Tint.paint
          Waiting = Rich.waiting }

    /// Every view on offer, in the order they are offered. A new one is added here and
    /// nowhere else: the menu, the command line and the prompt all read this list rather
    /// than keeping their own.
    let all = [ plain; rich ]

    let names = all |> List.map (fun view -> view.Name) |> String.concat ", "

    let byName (name: string) =
        let wanted = name.ToLowerInvariant()

        match all |> List.tryFind (fun view -> view.Name = wanted) with
        | Some view -> Ok view
        | None -> Error $"'{name}' is not a way of showing the game. There is {names}."
