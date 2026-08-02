/// The V of MVU: a pure projection from the model to console text.
module TCModel.View

open System
open System.Text

let private stones pile =
    if Pile.isEmpty pile then
        "-"
    else
        pile |> Pile.toColors |> List.map (StoneColor.glyph >> string) |> String.concat " "

let private tally pile =
    let counts =
        Pile.toCounts pile
        |> List.map (fun (color, n) -> $"{StoneColor.glyph color}x{n}")
        |> String.concat " "

    let counts = if counts = "" then "empty" else counts
    $"{counts} ({Pile.total pile})"

let private borders model (region: Region) =
    match Model.neighbours region.Id model |> Set.toList with
    | [] -> "-"
    | ids -> ids |> List.map (fun (RegionId n) -> string n) |> String.concat ","

let private regionLine model (region: Region) =
    let (RegionId n) = region.Id

    sprintf
        "  [%2d] %-18s %-11s %-15s %s"
        n
        region.Name
        (Region.describeKind region)
        (borders model region)
        (stones region.Stones)

let private playerLine active (player: Player) =
    let marker = if player.Id = active then "->" else "  "
    sprintf "  %s %-9s bag: %s" marker (Player.name player) (tally player.Bag)

let private section (sb: StringBuilder) (title: string) lines =
    sb.AppendLine(title) |> ignore
    lines |> List.iter (fun line -> sb.AppendLine(line: string) |> ignore)
    sb.AppendLine() |> ignore

/// Render the whole game as a block of text.
let render model =
    let sb = StringBuilder()
    let active = Model.activePlayer model

    let heading =
        match model.Status with
        | InProgress -> $"Turn {model.Turn} - {Player.name active} to play"
        | Over reason -> $"Game over after {model.Turn} turns - {reason}"

    sb.AppendLine().AppendLine($"=== {heading} ===").AppendLine() |> ignore

    sb.AppendLine(sprintf "  %-4s %-18s %-11s %-15s %s" "id" "region" "kind" "borders" "stones")
    |> ignore

    let byKind predicate =
        Model.regionsOfKind predicate model |> List.map (regionLine model)

    section sb "HOMELANDS" (byKind (function Home _ -> true | _ -> false))
    section sb "WILDS" (byKind (function Wild -> true | _ -> false))
    section sb "SPECIAL (standing alone)" (byKind (function Special -> true | _ -> false))
    section sb "DEAD" (byKind (function Dead -> true | _ -> false))
    section sb "PLAYERS" (model.Players |> List.map (playerLine model.Active))

    section
        sb
        "SUPPLY"
        [ $"  on the board: {tally (Model.stonesOnBoard model)}"
          $"  in reserve:   {tally model.Reserve}" ]

    section sb "LOG" (model.Log |> List.rev |> List.map (fun entry -> $"  {entry}"))

    sb.ToString()

let help =
    String.concat
        Environment.NewLine
        [ "Commands:"
          "  place <colour> <region>   put a stone from your bag into a region (alias: p)"
          "  pass                      end your turn without placing"
          "  restart [seed]            deal a fresh game to the same players"
          $"  players <n> [seed]        deal a fresh game to n players ({Setup.MinPlayers}-{Setup.MaxPlayers})"
          "  help                      show this list"
          "  quit                      leave the game"
          ""
          "Colours: r/red, b/blue, k/black. Regions are numbered by the board above." ]
