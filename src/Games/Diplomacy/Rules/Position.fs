namespace TCModel.Diplomacy

/// One piece on the board: whose it is, what it is, and where it stands.
///
/// The location carries a coast where the province has two, so a fleet on the south coast of
/// Spain and a fleet on the north coast are different pieces in the same province - which is
/// exactly what they are, since neither can sail where the other can.
type Piece =
    { Power: Power
      Kind: Kind
      Where: Location }

/// The board between one adjudication and the next: what is standing where, and who owns
/// which supply centres.
///
/// Units are held by province rather than by location, and that is the rule about crowding
/// written into the shape of the thing: two pieces may never share a province, whatever
/// coasts they would be on, so there is no way to build a position with two fleets in Spain.
///
/// Ownership is separate from occupation and outlives it. A centre belongs to whoever last
/// held it at the end of an autumn, and goes on belonging to them while their army is off
/// somewhere else - which is the whole reason a power can be down to one unit and still be
/// building.
type Position =
    { Units: Map<ProvinceId, Piece>
      Owners: Map<ProvinceId, Power> }

module Piece =

    /// The three letters and the coast, as everybody writes them: `stp/sc`.
    let whereabouts (location: Location) =
        match location.Coast with
        | Some coast -> $"{Atlas.code location.At}/{Coast.code coast}"
        | None -> Atlas.code location.At

    /// A piece as an order names it: `A vie`, `F stp/sc`.
    let written piece =
        $"{Kind.letter piece.Kind} {whereabouts piece.Where}"

    /// Where this piece could be ordered to go, ignoring everything and everybody in the way.
    /// Whether it arrives is the adjudicator's business; this is only what may be *asked*.
    let reach piece = Atlas.reach piece.Kind piece.Where

module Position =

    // --- reading one ---------------------------------------------------------------------------

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

    /// A power with nothing on the board and nothing owed to it is out of the game. Checked
    /// this way round rather than kept as a flag, because it is a fact about the position and
    /// a flag would be a second place for it to be true.
    let isOut power position =
        List.isEmpty (unitsOf power position) && List.isEmpty (centresOf power position)

    let stillIn position =
        Power.all |> List.filter (fun power -> not (isOut power position))

    // --- changing one -------------------------------------------------------------------------

    let private place piece position =
        { position with
            Units = Map.add piece.Where.At piece position.Units }

    let private lift province position =
        { position with
            Units = Map.remove province position.Units }

    /// Take a piece off the board, wherever it was standing.
    let remove province position = lift province position

    /// Put one on, at a province nothing is standing in.
    let add piece position = place piece position

    /// Move a piece from one place to another. The province it left is emptied first, so a
    /// piece told to move to where it already is - which the rules never produce, but a
    /// machine writing orders might - does not vanish on the way.
    let march piece into position =
        position |> lift piece.Where.At |> place { piece with Where = into }

    /// Hand a centre to whoever is standing in it. Done once at the end of an autumn and at
    /// no other time, which is why it takes the whole board rather than one province: a centre
    /// nobody is standing in keeps the owner it had.
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

    // --- and the one it starts from --------------------------------------------------------------

    /// Spring 1901, which is the same board every time it is dealt.
    ///
    /// Written out rather than worked out from the home centres, because it is not derivable:
    /// Russia's fleet at St Petersburg is on the south coast and not the north, and Liverpool
    /// holds an army where every other island centre holds a fleet. Those two facts are the
    /// opening, and a rule that generated them would be a rule invented to fit.
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

    /// What is wrong with the opening, which is a thing only this game could notice: every
    /// piece on a square it may stand on, every piece in a home centre of its own power, and
    /// twenty-two of them.
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
