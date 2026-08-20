namespace TCModel.Compile

open TCModel.Engine

module Lines =

    [<Literal>]
    let Count = Protocol.Each

    let all = [ 1..Count ]

    let holds line = line >= 1 && line <= Count

type Side =
    { Drafted: Protocol list
      Order: Protocol list
      Deck: Card list
      Discard: Card list
      Hand: Card list
      Stacks: Map<int, Placed list>
      Compiled: Set<Protocol> }

module Ruling =

    let saying uncovered placed =
        if Placed.isFaceUp placed then Printed.ongoing uncovered placed.Card else []

    let private live = saying

    let silences uncovered placed =
        live uncovered placed |> List.contains Silence

module Stack =

    [<Literal>]
    let ToCompile = 10

    let uncovered cards = List.tryHead cards

    let value cards = cards |> List.sumBy Placed.value

module Side =

    let empty =
        { Drafted = []
          Order = []
          Deck = []
          Discard = []
          Hand = []
          Stacks = Map.empty
          Compiled = Set.empty }

    let stack line side =
        side.Stacks |> Map.tryFind line |> Option.defaultValue []

    let protocolOn line side = side.Order |> List.tryItem (line - 1)

    let lineOf protocol side =
        side.Order |> List.tryFindIndex ((=) protocol) |> Option.map ((+) 1)

    let holds card side = side.Hand |> List.contains card

    let drafted protocol side =
        { side with
            Drafted = side.Drafted @ [ protocol ] }

    let arranged order side = { side with Order = order }

    let private restocked side rng =
        if List.isEmpty side.Deck && not (List.isEmpty side.Discard) then
            let deck, rng = Deck.shuffled side.Discard rng
            { side with Deck = deck; Discard = [] }, rng
        else
            side, rng

    let rec drawing count side rng =
        if count <= 0 then
            side, rng
        else
            match side.Deck with
            | top :: rest ->
                drawing
                    (count - 1)
                    { side with
                        Deck = rest
                        Hand = side.Hand @ [ top ] }
                    rng
            | [] ->
                let restocked, rng = restocked side rng

                if List.isEmpty restocked.Deck then restocked, rng else drawing count restocked rng

    let refreshed side rng =
        { side with
            Hand = []
            Discard = side.Hand @ side.Discard }
        |> fun side -> drawing Deck.HandSize side rng

    let played placed line side =
        { side with
            Hand = side.Hand |> List.filter ((<>) placed.Card)
            Stacks = side.Stacks |> Map.add line (placed :: stack line side) }

    let rulesOn line side =
        stack line side
        |> List.mapi (fun depth placed -> Ruling.saying (depth = 0) placed)
        |> List.concat

    let swept line side =
        { side with
            Discard = (stack line side |> List.map (fun placed -> placed.Card)) @ side.Discard
            Stacks = side.Stacks |> Map.add line [] }

    let compiled protocol side =
        { side with
            Compiled = Set.add protocol side.Compiled }

    let hasCompiled protocol side = Set.contains protocol side.Compiled

    let hasCompiledAll side =
        List.length side.Order = Protocol.Each
        && side.Order |> List.forall (fun protocol -> hasCompiled protocol side)

    let drawnFrom side rng =
        let side, rng = restocked side rng

        match side.Deck with
        | [] -> None, side, rng
        | top :: rest -> Some top, { side with Deck = rest }, rng

    let took card side =
        { side with
            Hand = side.Hand @ [ card ] }

type Field = private Field of Map<PlayerId, Side>

module Field =

    let ofSeats seats =
        seats |> List.map (fun seat -> seat, Side.empty) |> Map.ofList |> Field

    let side seat (Field sides) =
        sides |> Map.tryFind seat |> Option.defaultValue Side.empty

    let withSide seat replacement (Field sides) = Field(Map.add seat replacement sides)

    let update seat change field =
        withSide seat (change (side seat field)) field

    let seats (Field sides) = sides |> Map.toList |> List.map fst


    let protocolsOn line field =
        seats field |> List.choose (fun seat -> Side.protocolOn line (side seat field))

    let private mine seat line field = Side.rulesOn line (side seat field)

    let private across seat line field =
        seats field
        |> List.filter ((<>) seat)
        |> List.collect (fun other -> Side.rulesOn line (side other field))

    let skipsCache seat field =
        Lines.all
        |> List.exists (fun each -> mine seat each field |> List.contains SkipsCacheCheck)

    let allows seat card line field =
        protocolsOn line field |> List.contains card.Protocol
        || Lines.all
           |> List.exists (fun each -> mine seat each field |> List.contains YouMayPlayAnywhere)

    let facingLines seat card field =
        Lines.all |> List.filter (fun line -> allows seat card line field)

    let barred seat line face field =
        let theirs = across seat line field

        let anywhere =
            seats field
            |> List.filter ((<>) seat)
            |> List.collect (fun other -> Lines.all |> List.collect (fun each -> Side.rulesOn each (side other field)))

        if List.contains TheyCannotPlayHere theirs then Some NoPlayHere
        elif face = FaceDown && List.contains TheyCannotPlayFaceDownHere theirs then Some NoFaceDownHere
        elif face = FaceUp && List.contains TheyMustPlayFaceDown anywhere then Some OnlyFaceDown
        else None

    let homeOf card field =
        seats field
        |> List.tryFind (fun seat -> List.contains card.Protocol (side seat field).Drafted)


    let valueOn seat line field =
        let mine = Side.stack line (side seat field)
        let ours = Side.rulesOn line (side seat field)

        let across =
            seats field
            |> List.filter ((<>) seat)
            |> List.collect (fun other -> Side.rulesOn line (side other field))

        let faceDown =
            ours
            |> List.tryPick (function
                | FaceDownWorth n -> Some n
                | _ -> None)

        let counted =
            mine
            |> List.sumBy (fun placed ->
                match placed.Face, faceDown with
                | FaceDown, Some n -> n
                | _ -> Placed.value placed)

        let faceDownCards = mine |> List.filter (Placed.isFaceUp >> not) |> List.length

        let added =
            ours
            |> List.sumBy (function
                | LinePlus n -> n
                | LinePlusPerFaceDown n -> n * faceDownCards
                | _ -> 0)

        let taken =
            across
            |> List.sumBy (function
                | TheirLineMinus n -> n
                | _ -> 0)

        max 0 (counted + added - taken)


    let opposing seat line field =
        match seats field |> List.filter ((<>) seat) with
        | [] -> 0
        | others -> others |> List.map (fun other -> valueOn other line field) |> List.max

    let won seat line field =
        let mine = valueOn seat line field
        mine >= Stack.ToCompile && mine > opposing seat line field

    let winning seat field =
        Lines.all |> List.filter (fun line -> won seat line field)


    [<Literal>]
    let LanesForControl = 2

    let leads seat line field =
        valueOn seat line field > opposing seat line field

    let leading seat field =
        Lines.all |> List.filter (fun line -> leads seat line field) |> List.length

    let silenced line field =
        seats field
        |> List.exists (fun seat ->
            Side.stack line (side seat field)
            |> List.mapi (fun depth placed -> Ruling.silences (depth = 0) placed)
            |> List.exists id)
