namespace TCModel.Diplomacy

type Piece =
    { Power: Power
      Kind: Kind
      Where: Location }

type Position =
    { Units: Map<ProvinceId, Piece>
      Owners: Map<ProvinceId, Power> }

module Piece =

    let whereabouts (location: Location) =
        match location.Coast with
        | Some coast -> $"{Atlas.code location.At}/{Coast.code coast}"
        | None -> Atlas.code location.At

    let written piece =
        $"{Kind.letter piece.Kind} {whereabouts piece.Where}"

    let reach piece = Atlas.reach piece.Kind piece.Where

module Position =


    let at province position = Map.tryFind province position.Units

    let occupied province position = at province position |> Option.isSome

    let unitsOf power position =
        position.Units
        |> Map.toList
        |> List.map snd
        |> List.filter (fun piece -> piece.Power = power)
        |> List.sortBy (fun piece -> Atlas.code piece.Where.At)

    let allUnits position =
        position.Units
        |> Map.toList
        |> List.map snd
        |> List.sortBy (fun piece -> Atlas.code piece.Where.At)

    let ownerOf province position = Map.tryFind province position.Owners

    let centresOf power position =
        position.Owners
        |> Map.toList
        |> List.filter (fun (_, owner) -> owner = power)
        |> List.map fst
        |> List.sortBy Atlas.code

    let counts power position =
        List.length (centresOf power position), List.length (unitsOf power position)

    let isOut power position =
        List.isEmpty (unitsOf power position) && List.isEmpty (centresOf power position)

    let stillIn position =
        Power.all |> List.filter (fun power -> not (isOut power position))


    let private place piece position =
        { position with
            Units = Map.add piece.Where.At piece position.Units }

    let private lift province position =
        { position with
            Units = Map.remove province position.Units }

    let remove province position = lift province position

    let add piece position = place piece position

    let march piece into position =
        position |> lift piece.Where.At |> place { piece with Where = into }

    let harvest position =
        let taken =
            position.Units
            |> Map.toList
            |> List.filter (fun (province, _) -> Atlas.isCentre province)
            |> List.map (fun (province, piece) -> province, piece.Power)

        { position with
            Owners =
                taken
                |> List.fold (fun owners (province, power) -> Map.add province power owners) position.Owners }


    let private opening =
        [ Austria, Army, "vie", None
          Austria, Army, "bud", None
          Austria, Fleet, "tri", None

          England, Fleet, "lon", None
          England, Fleet, "edi", None
          England, Army, "lvp", None

          France, Army, "par", None
          France, Army, "mar", None
          France, Fleet, "bre", None

          Germany, Army, "ber", None
          Germany, Army, "mun", None
          Germany, Fleet, "kie", None

          Italy, Army, "rom", None
          Italy, Army, "ven", None
          Italy, Fleet, "nap", None

          Russia, Army, "mos", None
          Russia, Army, "war", None
          Russia, Fleet, "sev", None
          Russia, Fleet, "stp", Some South

          Turkey, Army, "con", None
          Turkey, Army, "smy", None
          Turkey, Fleet, "ank", None ]

    let dealt =
        let pieces =
            opening
            |> List.choose (fun (power, kind, code, coast) ->
                Atlas.byCode code
                |> Option.map (fun province ->
                    province,
                    { Power = power
                      Kind = kind
                      Where = Atlas.standing kind province coast }))

        { Units = Map.ofList pieces
          Owners =
            Power.all
            |> List.collect (fun power -> Atlas.homesOf power |> List.map (fun home -> home, power))
            |> Map.ofList }

    let problems =
        let pieces = allUnits dealt

        [ if List.length pieces <> 22 then
              yield $"{List.length pieces} units at the opening, where there are 22"

          for piece in pieces do
              if Atlas.centreOf piece.Where.At <> Home piece.Power then
                  yield $"{Power.name piece.Power} opens with a unit at {Atlas.nameOf piece.Where.At}, which is not its own"

              match piece.Kind, Atlas.terrainOf piece.Where.At with
              | Fleet, Inland -> yield $"a fleet opens landlocked at {Atlas.nameOf piece.Where.At}"
              | Army, Sea -> yield $"an army opens at sea in {Atlas.nameOf piece.Where.At}"
              | _ -> ()

              if Atlas.hasCoasts piece.Where.At && piece.Kind = Fleet && piece.Where.Coast.IsNone then
                  yield $"a fleet opens at {Atlas.nameOf piece.Where.At} without saying which coast"

          for power in Power.all do
              let centres, units = counts power dealt

              if centres <> units then
                  yield $"{Power.name power} opens with {units} units and {centres} centres" ]
