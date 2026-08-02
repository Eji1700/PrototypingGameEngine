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
        "  [%2d] %-18s %-11s %-15s %-8s %s"
        n
        region.Name
        (Region.describeKind region)
        (borders model region)
        (Model.ruleOver region model |> Ruling.describe)
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
        match model.Status, model.Pending with
        | Over reason, _ -> $"Game over after {model.Turn} turns - {reason}"
        | InProgress, Some(AwaitingReturn drawn) ->
            $"Turn {model.Turn} - {Player.name active} drew a {StoneColor.name drawn} stone and may hand one back"
        | InProgress, None -> $"Turn {model.Turn} - {Player.name active} to play"

    sb.AppendLine().AppendLine($"=== {heading} ===").AppendLine() |> ignore

    sb.AppendLine(sprintf "  %-4s %-18s %-11s %-15s %-8s %s" "id" "region" "kind" "borders" "ruler" "stones")
    |> ignore

    let byKind predicate =
        Model.regionsOfKind predicate model |> List.map (regionLine model)

    section sb "HOMELANDS" (byKind (function Home _ -> true | _ -> false))
    section sb "WILDS" (byKind (function Wild -> true | _ -> false))
    section sb "SPECIAL (standing alone)" (byKind (function Special -> true | _ -> false))
    section sb "DEAD" (byKind (function Dead -> true | _ -> false))
    let players = model.Players |> List.map (playerLine model.Active)

    let run =
        $"  negotiations in a row: {model.Negotiations} of {Model.playerCount model} - the game ends on the last"

    section sb "PLAYERS" (players @ [ run ])

    let countLand predicate =
        Model.landRulings model |> List.filter (snd >> predicate) |> List.length

    let ruled =
        Model.standings model
        |> List.map (fun (color, n) -> $"{StoneColor.name color} {n}")
        |> String.concat "   "

    let tied = countLand (function Ruling.Contested _ -> true | _ -> false)
    let unclaimed = countLand (function Ruling.Unclaimed -> true | _ -> false)

    section
        sb
        "LAND RULED"
        [ $"  {ruled}   tied {tied}   unclaimed {unclaimed}"
          "  (the Flag and the Axe are manoeuvres, not land, and do not count here)" ]

    section
        sb
        "SUPPLY"
        [ $"  on the board: {tally (Model.stonesOnBoard model)}"
          $"  in reserve:   {tally model.Reserve}" ]

    match model.Status with
    | InProgress -> ()
    | Over _ -> section sb "RESULT" (Outcome.explain model)

    section sb "LOG" (model.Log |> List.rev |> List.map (fun entry -> $"  {entry}"))

    sb.ToString()

/// Show the working behind who rules a region.
let explainRule regionId model =
    match Model.tryRegion regionId model with
    | None ->
        let (RegionId n) = regionId
        $"There is no region {n}."
    | Some region ->
        String.concat Environment.NewLine ($"{region.Name} holds {Pile.describe region.Stones}." :: Model.explainRule region model)

let help =
    String.concat
        Environment.NewLine
        [ "Each turn, take one of the four actions:"
          "  recruit <colour> <region>              place a stone from your bag on the map (alias: r)"
          "  battle <colour> <region> [colours...]  place a stone in the Axe, then drive that many"
          "                                         stones of other colours out of the region (alias: b)"
          "  march <colour> <from> <to> [count]     place a stone in the Flag, then move matching"
          "                                         stones into a bordering region (alias: m)"
          "  negotiate                              draw a stone from the reserve (alias: n)"
          "    then: return <colour> | keep         hand a stone back, or keep the draw"
          ""
          "The game ends once every player has negotiated in a row. An empty-handed"
          "player has their turn skipped, and that counts as a negotiation."
          ""
          "Other commands:"
          "  rule <region>             show the working behind who rules a region"
          "  restart [seed]            deal a fresh game to the same players"
          $"  players <n> [seed]        deal a fresh game to n players ({Setup.MinPlayers}-{Setup.MaxPlayers})"
          "  help                      show this list"
          "  quit                      leave the game"
          ""
          "Colours: r/red, b/blue, k/black. Regions are numbered by the board above."
          "Battle and march cannot target the dead region, the Flag or the Axe." ]
