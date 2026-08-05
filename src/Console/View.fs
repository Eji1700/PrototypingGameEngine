namespace TCModel.Console

/// How the game is shown, which a player chooses for themselves.
///
/// The game writes one screen and writes it plainly: `Render` turns a model into text and
/// knows nothing about who is going to read it or on what. A view is the last thing that
/// text passes through before a person sees it.
///
/// Keeping the seam here - on the finished text rather than on the model - is what lets a
/// player at a networked table choose their own. The wire carries the plain board, and the
/// view runs at the console it is going to, so two people at the same game can read it
/// two different ways and the table need not know.
/// A view carries a function, so two of them can only be told apart by name - which is
/// what `View.byName` is for.
[<NoComparison; NoEquality>]
type View =
    { Name: string
      Describe: string
      Show: string -> string }

module View =

    /// The board as the game writes it, and nothing done to it.
    let plain =
        { Name = "plain"
          Describe = "plain text, and nothing this terminal has to understand"
          Show = id }

    /// The same board with colour laid over it.
    let rich =
        { Name = "rich"
          Describe = "colour, for a terminal that can show it"
          Show = Tint.paint }

    /// Every view on offer, in the order they are offered. A new one is added here and
    /// nowhere else: the menu and the command line both read this list rather than
    /// keeping their own.
    let all = [ plain; rich ]

    let names = all |> List.map (fun view -> view.Name) |> String.concat ", "

    let byName (name: string) =
        let wanted = name.ToLowerInvariant()

        match all |> List.tryFind (fun view -> view.Name = wanted) with
        | Some view -> Ok view
        | None -> Error $"'{name}' is not a way of showing the game. There is {names}."
