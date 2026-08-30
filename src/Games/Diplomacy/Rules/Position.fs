namespace Prototyping.Diplomacy

open Prototyping.Common

type Piece =
    { Power: Power
      Kind: Kind
      Where: Location }

type Position =
    { Units: Map<ProvinceId, Piece>
      Owners: Map<ProvinceId, Power> }

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


    let add piece position =
        { position with
            Units = Map.add piece.Where.At piece position.Units }

    let remove province position =
        { position with
            Units = Map.remove province position.Units }

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

    let private units = Counting.several "unit" "units"

    let private centres = Counting.several "centre" "centres"

    let problems =
        let pieces = allUnits dealt

        [ if List.length pieces <> 22 then
              yield $"{units (List.length pieces)} at the opening, where there are 22"

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
              let held, standing = counts power dealt

              if held <> standing then
                  yield $"{Power.name power} opens with {units standing} and {centres held}" ]
