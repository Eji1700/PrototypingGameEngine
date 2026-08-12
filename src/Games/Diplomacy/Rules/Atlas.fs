namespace TCModel.Diplomacy

/// What a province is made of underfoot, which is the whole of what decides who may stand
/// there: an army on land, a fleet at sea, and either of them on a coast.
type Terrain =
    | Inland
    | Coastal
    | Sea

/// Whether a province is worth holding at the end of a year.
///
/// Thirty-four of the seventy-five are, and twenty-two of those thirty-four begin the game
/// belonging to somebody. A power builds at its own home centres and nowhere else, which is
/// why "home of" is part of what a province *is* rather than something the position keeps:
/// Berlin is German whoever has an army sitting in it.
type Centre =
    | NotACentre
    | Neutral
    | Home of Power

/// Which part of the map, for a screen that has to show seventy-five provinces without
/// becoming a wall.
///
/// Not a rule - nothing in the game asks which region a province is in - but it is how
/// everybody who plays this talks about the board, and a board grouped the way it is talked
/// about is a board that can be read.
type Region =
    | TheIsles
    | Iberia
    | TheLowCountries
    | FranceAnd
    | GermanyAnd
    | Scandinavia
    | RussiaAnd
    | AustriaAnd
    | ItalyAnd
    | TheBalkans
    | TurkeyAnd
    | Africa
    | Waters

/// One province, by the three letters everybody who plays this writes it as.
///
/// Private, so the only ids in circulation are ones the table below minted. A line typed at
/// the prompt becomes one through `Atlas.byCode` or does not become one at all, which is what
/// stops a misspelt province reaching the rules as anything but a refusal.
type ProvinceId = private ProvinceId of string

/// Where a piece stands: a province, and which of its coasts where the province has more than
/// one. `None` for the coast everywhere else, and that is not a missing answer - it is the
/// answer, because a province with one coastline has nothing to choose between.
type Location = { At: ProvinceId; Coast: Coast option }

type Province =
    { Id: ProvinceId
      Name: string
      Terrain: Terrain
      Centre: Centre
      Region: Region }

/// The board: seventy-five provinces, and the two graphs over them.
///
/// Two graphs, because there are two kinds of piece and they do not travel the same map. An
/// army walks between land provinces; a fleet sails between waters and along coasts, and on
/// the three provinces with two coastlines it is the coast rather than the province that has
/// neighbours. Neither graph is derivable from the other and both are declared.
///
/// **Declared in full at both ends rather than once and mirrored.** Turncoats names a border
/// from one end and symmetrises it, which is right for twenty-three edges written by hand.
/// This map has more than three hundred, and the thing that goes wrong at that size is a typo
/// - so both ends are written out and `problems` checks they agree. A mirrored table cannot
/// disagree with itself and so cannot catch anything; a doubled one can, and does.
module Atlas =

    // --- the provinces ------------------------------------------------------------------------

    /// Code, name, terrain, what it is worth, and where it is on the board. The one place the
    /// board is written down.
    let private declared =
        [ // The British Isles
          "cly", "Clyde", Coastal, NotACentre, TheIsles
          "edi", "Edinburgh", Coastal, Home England, TheIsles
          "lvp", "Liverpool", Coastal, Home England, TheIsles
          "yor", "Yorkshire", Coastal, NotACentre, TheIsles
          "wal", "Wales", Coastal, NotACentre, TheIsles
          "lon", "London", Coastal, Home England, TheIsles

          // Iberia
          "por", "Portugal", Coastal, Neutral, Iberia
          "spa", "Spain", Coastal, Neutral, Iberia

          // France
          "bre", "Brest", Coastal, Home France, FranceAnd
          "pic", "Picardy", Coastal, NotACentre, FranceAnd
          "par", "Paris", Inland, Home France, FranceAnd
          "bur", "Burgundy", Inland, NotACentre, FranceAnd
          "gas", "Gascony", Coastal, NotACentre, FranceAnd
          "mar", "Marseilles", Coastal, Home France, FranceAnd

          // The Low Countries
          "bel", "Belgium", Coastal, Neutral, TheLowCountries
          "hol", "Holland", Coastal, Neutral, TheLowCountries

          // Germany
          "ruh", "Ruhr", Inland, NotACentre, GermanyAnd
          "kie", "Kiel", Coastal, Home Germany, GermanyAnd
          "ber", "Berlin", Coastal, Home Germany, GermanyAnd
          "mun", "Munich", Inland, Home Germany, GermanyAnd
          "sil", "Silesia", Inland, NotACentre, GermanyAnd
          "pru", "Prussia", Coastal, NotACentre, GermanyAnd

          // Scandinavia
          "den", "Denmark", Coastal, Neutral, Scandinavia
          "nwy", "Norway", Coastal, Neutral, Scandinavia
          "swe", "Sweden", Coastal, Neutral, Scandinavia
          "fin", "Finland", Coastal, NotACentre, Scandinavia

          // Russia
          "stp", "St Petersburg", Coastal, Home Russia, RussiaAnd
          "lvn", "Livonia", Coastal, NotACentre, RussiaAnd
          "mos", "Moscow", Inland, Home Russia, RussiaAnd
          "war", "Warsaw", Inland, Home Russia, RussiaAnd
          "ukr", "Ukraine", Inland, NotACentre, RussiaAnd
          "sev", "Sevastopol", Coastal, Home Russia, RussiaAnd

          // Austria and its marches
          "boh", "Bohemia", Inland, NotACentre, AustriaAnd
          "gal", "Galicia", Inland, NotACentre, AustriaAnd
          "tyr", "Tyrolia", Inland, NotACentre, AustriaAnd
          "vie", "Vienna", Inland, Home Austria, AustriaAnd
          "bud", "Budapest", Inland, Home Austria, AustriaAnd
          "tri", "Trieste", Coastal, Home Austria, AustriaAnd

          // Italy
          "pie", "Piedmont", Coastal, NotACentre, ItalyAnd
          "tus", "Tuscany", Coastal, NotACentre, ItalyAnd
          "ven", "Venice", Coastal, Home Italy, ItalyAnd
          "rom", "Rome", Coastal, Home Italy, ItalyAnd
          "apu", "Apulia", Coastal, NotACentre, ItalyAnd
          "nap", "Naples", Coastal, Home Italy, ItalyAnd

          // The Balkans
          "ser", "Serbia", Inland, Neutral, TheBalkans
          "alb", "Albania", Coastal, NotACentre, TheBalkans
          "gre", "Greece", Coastal, Neutral, TheBalkans
          "bul", "Bulgaria", Coastal, Neutral, TheBalkans
          "rum", "Rumania", Coastal, Neutral, TheBalkans

          // Turkey
          "con", "Constantinople", Coastal, Home Turkey, TurkeyAnd
          "ank", "Ankara", Coastal, Home Turkey, TurkeyAnd
          "smy", "Smyrna", Coastal, Home Turkey, TurkeyAnd
          "arm", "Armenia", Coastal, NotACentre, TurkeyAnd
          "syr", "Syria", Coastal, NotACentre, TurkeyAnd

          // Africa
          "naf", "North Africa", Coastal, NotACentre, Africa
          "tun", "Tunis", Coastal, Neutral, Africa

          // And the waters
          "nao", "North Atlantic Ocean", Sea, NotACentre, Waters
          "nwg", "Norwegian Sea", Sea, NotACentre, Waters
          "bar", "Barents Sea", Sea, NotACentre, Waters
          "iri", "Irish Sea", Sea, NotACentre, Waters
          "nth", "North Sea", Sea, NotACentre, Waters
          "ska", "Skagerrak", Sea, NotACentre, Waters
          "hel", "Helgoland Bight", Sea, NotACentre, Waters
          "bal", "Baltic Sea", Sea, NotACentre, Waters
          "bot", "Gulf of Bothnia", Sea, NotACentre, Waters
          "eng", "English Channel", Sea, NotACentre, Waters
          "mao", "Mid-Atlantic Ocean", Sea, NotACentre, Waters
          "wes", "Western Mediterranean", Sea, NotACentre, Waters
          "gol", "Gulf of Lyon", Sea, NotACentre, Waters
          "tys", "Tyrrhenian Sea", Sea, NotACentre, Waters
          "ion", "Ionian Sea", Sea, NotACentre, Waters
          "adr", "Adriatic Sea", Sea, NotACentre, Waters
          "aeg", "Aegean Sea", Sea, NotACentre, Waters
          "eas", "Eastern Mediterranean", Sea, NotACentre, Waters
          "bla", "Black Sea", Sea, NotACentre, Waters ]

    /// The three provinces whose two coastlines face different waters, and which coasts they
    /// are. Written out rather than read off the fleet table, so that the two can be checked
    /// against each other: a coast that appears in one and not the other is a mistake, and
    /// deriving either from the other is how it would go unnoticed.
    let private twoCoasted =
        [ "spa", [ North; South ]
          "bul", [ East; South ]
          "stp", [ North; South ] ]

    // --- where an army may walk -----------------------------------------------------------------

    /// Land to land, both ends written out. No sea appears here at all, and `problems` says so
    /// if one ever does.
    ///
    /// Two provinces are missing from the rest of it on purpose: an army in Tunis may walk to
    /// North Africa and nowhere else, so the land graph is in two pieces and Africa is the
    /// small one. That is the map, not an omission, which is why the check below asks the two
    /// graphs *together* to be connected and never asks it of this one alone.
    let private armyBorders =
        [ "cly", [ "edi"; "lvp" ]
          "edi", [ "cly"; "lvp"; "yor" ]
          "lvp", [ "cly"; "edi"; "yor"; "wal" ]
          "yor", [ "edi"; "lvp"; "wal"; "lon" ]
          "wal", [ "lvp"; "yor"; "lon" ]
          "lon", [ "yor"; "wal" ]

          "por", [ "spa" ]
          "spa", [ "por"; "gas"; "mar" ]

          "bre", [ "pic"; "par"; "gas" ]
          "pic", [ "bre"; "par"; "bur"; "bel" ]
          "par", [ "bre"; "pic"; "bur"; "gas" ]
          "bur", [ "par"; "pic"; "bel"; "ruh"; "mun"; "mar"; "gas" ]
          "gas", [ "bre"; "par"; "bur"; "mar"; "spa" ]
          "mar", [ "spa"; "gas"; "bur"; "pie" ]

          "bel", [ "hol"; "ruh"; "bur"; "pic" ]
          "hol", [ "bel"; "ruh"; "kie" ]

          "ruh", [ "bel"; "hol"; "kie"; "mun"; "bur" ]
          "kie", [ "hol"; "den"; "ber"; "mun"; "ruh" ]
          "ber", [ "kie"; "pru"; "sil"; "mun" ]
          "mun", [ "ruh"; "kie"; "ber"; "sil"; "boh"; "tyr"; "bur" ]
          "sil", [ "ber"; "pru"; "war"; "gal"; "boh"; "mun" ]
          "pru", [ "ber"; "sil"; "war"; "lvn" ]

          "den", [ "kie"; "swe" ]
          "nwy", [ "swe"; "fin"; "stp" ]
          "swe", [ "nwy"; "fin"; "den" ]
          "fin", [ "swe"; "nwy"; "stp" ]

          "stp", [ "fin"; "nwy"; "mos"; "lvn" ]
          "lvn", [ "pru"; "war"; "mos"; "stp" ]
          "mos", [ "stp"; "lvn"; "war"; "ukr"; "sev" ]
          "war", [ "sil"; "pru"; "lvn"; "mos"; "ukr"; "gal" ]
          "ukr", [ "war"; "mos"; "sev"; "rum"; "gal" ]
          "sev", [ "ukr"; "mos"; "arm"; "rum" ]

          "boh", [ "mun"; "sil"; "gal"; "vie"; "tyr" ]
          "gal", [ "war"; "ukr"; "rum"; "bud"; "vie"; "boh"; "sil" ]
          "tyr", [ "mun"; "boh"; "vie"; "tri"; "ven"; "pie" ]
          "vie", [ "tyr"; "boh"; "gal"; "bud"; "tri" ]
          "bud", [ "vie"; "gal"; "rum"; "ser"; "tri" ]
          "tri", [ "ven"; "tyr"; "vie"; "bud"; "ser"; "alb" ]

          "pie", [ "mar"; "tus"; "ven"; "tyr" ]
          "tus", [ "rom"; "ven"; "pie" ]
          "ven", [ "pie"; "tus"; "rom"; "apu"; "tri"; "tyr" ]
          "rom", [ "nap"; "apu"; "ven"; "tus" ]
          "apu", [ "nap"; "rom"; "ven" ]
          "nap", [ "rom"; "apu" ]

          "ser", [ "tri"; "bud"; "rum"; "bul"; "gre"; "alb" ]
          "alb", [ "tri"; "ser"; "gre" ]
          "gre", [ "alb"; "ser"; "bul" ]
          "bul", [ "rum"; "ser"; "gre"; "con" ]
          "rum", [ "bul"; "ser"; "bud"; "gal"; "ukr"; "sev" ]

          "con", [ "bul"; "ank"; "smy" ]
          "ank", [ "con"; "smy"; "arm" ]
          "smy", [ "con"; "ank"; "arm"; "syr" ]
          "arm", [ "ank"; "smy"; "syr"; "sev" ]
          "syr", [ "smy"; "arm" ]

          "naf", [ "tun" ]
          "tun", [ "naf" ] ]

    // --- and where a fleet may sail --------------------------------------------------------------

    /// Waters to waters, waters to coast, and coast to coast where two coastlines actually
    /// meet. Both ends written out, and a coast named after a slash where the province has
    /// two of them.
    ///
    /// Coast to coast is the half people forget. A fleet may sail from Rome to Naples because
    /// their shores run into one another, and may not sail from Rome to Venice though the two
    /// provinces border - the land between them is a land border and a fleet is not walking
    /// it. So this is not the army table with the seas added; it is a different map.
    let private fleetBorders =
        [ // The oceans and seas
          "nao", [ "nwg"; "iri"; "mao"; "cly"; "lvp" ]
          "nwg", [ "nao"; "bar"; "nth"; "nwy"; "cly"; "edi" ]
          "bar", [ "nwg"; "nwy"; "stp/nc" ]
          "iri", [ "nao"; "eng"; "mao"; "lvp"; "wal" ]
          "nth", [ "nwg"; "ska"; "hel"; "eng"; "edi"; "yor"; "lon"; "bel"; "hol"; "den"; "nwy" ]
          "ska", [ "nth"; "nwy"; "swe"; "den" ]
          "hel", [ "nth"; "den"; "kie"; "hol" ]
          "bal", [ "bot"; "swe"; "den"; "kie"; "ber"; "pru"; "lvn" ]
          "bot", [ "bal"; "swe"; "fin"; "stp/sc"; "lvn" ]
          "eng", [ "iri"; "nth"; "mao"; "wal"; "lon"; "bel"; "pic"; "bre" ]
          "mao", [ "nao"; "iri"; "eng"; "wes"; "bre"; "gas"; "spa/nc"; "spa/sc"; "por"; "naf" ]
          "wes", [ "mao"; "gol"; "tys"; "spa/sc"; "naf"; "tun" ]
          "gol", [ "wes"; "tys"; "spa/sc"; "mar"; "pie"; "tus" ]
          "tys", [ "wes"; "gol"; "ion"; "tus"; "rom"; "nap"; "tun" ]
          "ion", [ "tys"; "adr"; "aeg"; "eas"; "tun"; "nap"; "apu"; "gre"; "alb" ]
          "adr", [ "ion"; "alb"; "tri"; "ven"; "apu" ]
          "aeg", [ "eas"; "ion"; "gre"; "bul/sc"; "con"; "smy" ]
          "eas", [ "ion"; "aeg"; "smy"; "syr" ]
          "bla", [ "bul/ec"; "rum"; "sev"; "arm"; "ank"; "con" ]

          // And the shores
          "cly", [ "nao"; "nwg"; "edi"; "lvp" ]
          "edi", [ "nth"; "nwg"; "cly"; "yor" ]
          "lvp", [ "nao"; "iri"; "cly"; "wal" ]
          "yor", [ "nth"; "edi"; "lon" ]
          "wal", [ "eng"; "iri"; "lon"; "lvp" ]
          "lon", [ "nth"; "eng"; "yor"; "wal" ]

          "por", [ "mao"; "spa/nc"; "spa/sc" ]
          "spa/nc", [ "mao"; "por"; "gas" ]
          "spa/sc", [ "mao"; "wes"; "gol"; "por"; "mar" ]

          "bre", [ "eng"; "mao"; "pic"; "gas" ]
          "pic", [ "eng"; "bel"; "bre" ]
          "gas", [ "mao"; "bre"; "spa/nc" ]
          "mar", [ "gol"; "spa/sc"; "pie" ]

          "bel", [ "nth"; "eng"; "hol"; "pic" ]
          "hol", [ "nth"; "hel"; "bel"; "kie" ]

          "kie", [ "bal"; "hel"; "den"; "ber"; "hol" ]
          "ber", [ "bal"; "kie"; "pru" ]
          "pru", [ "bal"; "ber"; "lvn" ]

          "den", [ "nth"; "ska"; "bal"; "hel"; "kie"; "swe" ]
          "nwy", [ "nwg"; "nth"; "bar"; "ska"; "swe"; "stp/nc" ]
          "swe", [ "bal"; "bot"; "ska"; "den"; "fin"; "nwy" ]
          "fin", [ "bot"; "swe"; "stp/sc" ]

          "stp/nc", [ "bar"; "nwy" ]
          "stp/sc", [ "bot"; "fin"; "lvn" ]
          "lvn", [ "bal"; "bot"; "pru"; "stp/sc" ]
          "sev", [ "bla"; "rum"; "arm" ]

          "tri", [ "adr"; "alb"; "ven" ]

          "pie", [ "gol"; "mar"; "tus" ]
          "tus", [ "tys"; "gol"; "rom"; "pie" ]
          "ven", [ "adr"; "tri"; "apu" ]
          "rom", [ "tys"; "nap"; "tus" ]
          "apu", [ "adr"; "ion"; "ven"; "nap" ]
          "nap", [ "tys"; "ion"; "rom"; "apu" ]

          "alb", [ "adr"; "ion"; "tri"; "gre" ]
          "gre", [ "aeg"; "ion"; "alb"; "bul/sc" ]
          "bul/ec", [ "bla"; "rum"; "con" ]
          "bul/sc", [ "aeg"; "gre"; "con" ]
          "rum", [ "bla"; "bul/ec"; "sev" ]

          "con", [ "bla"; "aeg"; "bul/ec"; "bul/sc"; "ank"; "smy" ]
          "ank", [ "bla"; "con"; "arm" ]
          "smy", [ "aeg"; "eas"; "con"; "syr" ]
          "arm", [ "bla"; "ank"; "sev" ]
          "syr", [ "eas"; "smy" ]

          "naf", [ "mao"; "wes"; "tun" ]
          "tun", [ "tys"; "ion"; "wes"; "naf" ] ]

    // --- the board built from all that ------------------------------------------------------------

    let all =
        declared
        |> List.map (fun (code, name, terrain, centre, region) ->
            { Id = ProvinceId code
              Name = name
              Terrain = terrain
              Centre = centre
              Region = region })

    /// The three letters, back out of an id. The only place the wrapper is taken off.
    let code (ProvinceId text) = text

    let private lookup =
        all |> List.map (fun province -> code province.Id, province) |> Map.ofList

    let count = List.length all

    /// A province by the three letters, if there is one. The only door in: a line typed at the
    /// prompt becomes a `ProvinceId` here or stays a string.
    let byCode (text: string) =
        Map.tryFind (text.ToLowerInvariant()) lookup |> Option.map (fun province -> province.Id)

    /// Everything known about one. Every id was minted above, so this is a total lookup on the
    /// ids that exist - written with a fallback rather than an option because every caller has
    /// an id in hand and none of them has anything useful to say about not finding it.
    let about id =
        Map.tryFind (code id) lookup
        |> Option.defaultValue
            { Id = id
              Name = code id
              Terrain = Inland
              Centre = NotACentre
              Region = Waters }

    let nameOf id = (about id).Name

    let terrainOf id = (about id).Terrain

    let centreOf id = (about id).Centre

    let regionOf id = (about id).Region

    let isCentre id = centreOf id <> NotACentre

    let isSea id = terrainOf id = Sea

    let isLand id = terrainOf id <> Sea

    /// Every supply centre, in the order the board lists them.
    let centres = all |> List.filter (fun p -> p.Centre <> NotACentre) |> List.map (fun p -> p.Id)

    /// The home centres of one power - the only places it may ever build.
    let homesOf power =
        all |> List.filter (fun p -> p.Centre = Home power) |> List.map (fun p -> p.Id)

    // --- reading a location --------------------------------------------------------------------

    /// A province by whatever a person typed: the three letters, or its name with the spaces
    /// taken out. `stp` and `stpetersburg` are the same place, and a player who types the
    /// second has not made a mistake worth correcting.
    let byWord (text: string) =
        let wanted = text.ToLowerInvariant()

        match byCode wanted with
        | Some id -> Some id
        | None ->
            all
            |> List.tryFind (fun province ->
                let name = province.Name.ToLowerInvariant()
                name = wanted || name.Replace(" ", "") = wanted)
            |> Option.map (fun province -> province.Id)

    let private parseCoast (text: string) =
        match text.Split '/' with
        | [| province |] -> Some(province, None)
        | [| province; coast |] -> Coast.byCode coast |> Option.map (fun c -> province, Some c)
        | _ -> None

    let private locationOf (text: string) =
        parseCoast text
        |> Option.bind (fun (province, coast) -> byCode province |> Option.map (fun id -> { At = id; Coast = coast }))

    /// A whereabouts by whatever a person typed: `stp/sc`, `spa/nc`, `vienna`, `tri`. The other
    /// door in, and the one an order comes through.
    let spotBy (text: string) =
        parseCoast text
        |> Option.bind (fun (province, coast) -> byWord province |> Option.map (fun id -> { At = id; Coast = coast }))

    /// The coasts a province has to choose between, which is two for three of them and none
    /// for everybody else.
    let coastsOf id =
        twoCoasted
        |> List.tryFind (fun (c, _) -> c = code id)
        |> Option.map snd
        |> Option.defaultValue []

    let hasCoasts id = coastsOf id |> List.isEmpty |> not

    /// Where a piece of that kind stands in a province, as a location. An army is never on a
    /// coast even when the province has two, because an army standing in Spain is standing in
    /// Spain; only a fleet has to say which water it is floating in.
    let standing kind id coast =
        match kind with
        | Army -> { At = id; Coast = None }
        | Fleet -> { At = id; Coast = coast }

    // --- and the two graphs -----------------------------------------------------------------------

    let private edgesOf declaredBorders reading =
        declaredBorders
        |> List.choose (fun (from, into) -> reading from |> Option.map (fun key -> key, into |> List.choose reading))
        |> Map.ofList

    let private armyMap = edgesOf armyBorders byCode

    let private fleetMap = edgesOf fleetBorders locationOf

    /// Where an army in that province may walk.
    let armyReach id = Map.tryFind id armyMap |> Option.defaultValue []

    /// Where a fleet standing there may sail. Asked of a location rather than a province,
    /// because on the three with two coasts that is the whole question.
    let fleetReach location = Map.tryFind location fleetMap |> Option.defaultValue []

    /// Every place a piece of that kind, standing there, could be ordered to go.
    let reach kind location =
        match kind with
        | Army -> armyReach location.At |> List.map (fun id -> { At = id; Coast = None })
        | Fleet -> fleetReach location

    /// Whether a piece of that kind standing there may move to that province at all - the
    /// question every order has to answer, asked without caring which coast it would land on.
    let canGo kind location into =
        reach kind location |> List.exists (fun there -> there.At = into)

    /// The ways in, for a fleet told to go somewhere with more than one shore: which coasts of
    /// the destination it could actually reach from where it is.
    let waysInto kind location into =
        reach kind location |> List.filter (fun there -> there.At = into)

    /// Whether an army could walk between two provinces, ignoring who is in the way. Used by
    /// the machine to work out how far a centre is, and by nothing in the rules.
    let walkable from into = armyReach from |> List.contains into

    // --- what could be wrong with all of it ------------------------------------------------------

    /// What this game says is wrong with its own board, before anybody sits down to one.
    ///
    /// A map of seventy-five provinces and three hundred-odd borders, typed out by hand, will
    /// have a mistake in it, and every one of these checks is a mistake that was actually
    /// made while writing the tables above. The point of declaring both ends of every border
    /// is that this list can catch them: a table mirrored from one end agrees with itself
    /// however wrong it is.
    let problems =
        let codes = declared |> List.map (fun (code, _, _, _, _) -> code)
        let known = Set.ofList codes

        let named = List.map (fun (code, _, _, _, _) -> code) >> Set.ofList

        /// Every code a border table mentions, coast and all stripped off.
        let mentioned table =
            table
            |> List.collect (fun (from: string, into) -> from :: into)
            |> List.map (fun text -> (text.Split '/')[0])
            |> Set.ofList

        /// The two ends of every border in a table, as plain text, so that one can be looked
        /// for in the other.
        let pairs table =
            table |> List.collect (fun (from, into) -> into |> List.map (fun there -> from, there))

        let unmirrored table =
            let held = Set.ofList (pairs table)
            pairs table |> List.filter (fun (from, into) -> not (Set.contains (into, from) held))

        let terrainOfCode code =
            declared
            |> List.tryPick (fun (c, _, terrain, _, _) -> if c = code then Some terrain else None)

        let reachedFrom start borders =
            let rec walk seen =
                function
                | [] -> seen
                | here :: rest when Set.contains here seen -> walk seen rest
                | here :: rest ->
                    let next = borders |> List.tryFind (fst >> (=) here) |> Option.map snd |> Option.defaultValue []
                    walk (Set.add here seen) (next @ rest)

            walk Set.empty [ start ]

        /// Both graphs at once, with the coasts flattened away - which is the only sense in
        /// which this map is one map. Neither half is connected on its own: an army cannot
        /// leave Africa and a fleet cannot enter Moscow.
        let together =
            let flatten table =
                table
                |> List.map (fun (from: string, into) ->
                    (from.Split '/')[0], into |> List.map (fun (there: string) -> (there.Split '/')[0]))

            flatten armyBorders @ flatten fleetBorders
            |> List.groupBy fst
            |> List.map (fun (from, rows) -> from, rows |> List.collect snd |> List.distinct)

        [ // --- the provinces themselves
          if List.length codes <> List.length (List.distinct codes) then
              yield "the same province written down twice"

          if count <> 75 then
              yield $"{count} provinces, where the board has 75"

          if List.length centres <> 34 then
              yield $"{List.length centres} supply centres, where the board has 34"

          match all |> List.filter (fun p -> p.Terrain = Sea) |> List.length with
          | 19 -> ()
          | seas -> yield $"{seas} seas, where the board has 19"

          for power in Power.all do
              let homes = homesOf power |> List.length
              let due = if power = Russia then 4 else 3

              if homes <> due then
                  yield $"{Power.name power} starts with {homes} home centres rather than {due}"

          if all |> List.exists (fun p -> p.Terrain = Sea && p.Centre <> NotACentre) then
              yield "a supply centre out at sea"

          // --- the codes the border tables name
          for missing in Set.difference (mentioned armyBorders) known do
              yield $"an army border to '{missing}', which is not a province"

          for missing in Set.difference (mentioned fleetBorders) known do
              yield $"a fleet border to '{missing}', which is not a province"

          // --- and whether they agree with each other
          for from, into in unmirrored armyBorders do
              yield $"{from} borders {into} by land, and {into} does not border {from}"

          for from, into in unmirrored fleetBorders do
              yield $"{from} borders {into} by sea, and {into} does not border {from}"

          if armyBorders |> List.exists (fun (from, into) -> List.contains from into) then
              yield "a province bordering itself by land"

          if fleetBorders |> List.exists (fun (from, into) -> List.contains from into) then
              yield "a province bordering itself by sea"

          // --- terrain against the graph a piece of that kind travels
          for code in mentioned armyBorders do
              if terrainOfCode code = Some Sea then
                  yield $"an army border at {code}, which is open sea"

          for code in mentioned fleetBorders do
              if terrainOfCode code = Some Inland then
                  yield $"a fleet border at {code}, which is landlocked"

          for code, _, terrain, _, _ in declared do
              let onFoot = armyBorders |> List.exists (fst >> (=) code)

              let afloat =
                  fleetBorders
                  |> List.exists (fun (from: string, _) -> (from.Split '/')[0] = code)

              match terrain with
              | Inland when not onFoot -> yield $"{code} is landlocked and borders nothing at all"
              | Inland when afloat -> yield $"{code} is landlocked and has a fleet border"
              | Coastal when not (onFoot && afloat) -> yield $"{code} is a coast with only half a border list"
              | Sea when onFoot -> yield $"{code} is open sea and has an army border"
              | Sea when not afloat -> yield $"{code} is open sea and borders nothing at all"
              | _ -> ()

          // --- the three with two coastlines
          for province, coasts in twoCoasted do
              if not (Set.contains province known) then
                  yield $"'{province}' has two coasts and is not a province"

              let declaredCoasts =
                  fleetBorders
                  |> List.choose (fun (from: string, _) ->
                      match from.Split '/' with
                      | [| p; c |] when p = province -> Coast.byCode c
                      | _ -> None)
                  |> List.distinct
                  |> List.sort

              if declaredCoasts <> List.sort coasts then
                  yield $"{province} has two coasts and the fleet table names {List.length declaredCoasts}"

              if fleetBorders |> List.exists (fun (from, _) -> from = province) then
                  yield $"{province} has two coasts and a fleet border that names neither"

          for from, _ in fleetBorders do
              match (from: string).Split '/' with
              | [| _ |] -> ()
              | [| p; _ |] when twoCoasted |> List.exists (fst >> (=) p) -> ()
              | _ -> yield $"'{from}' names a coast of a province that has only one"

          // --- and whether the whole thing hangs together
          let reachedTogether = reachedFrom "vie" together

          for code in codes do
              if not (Set.contains code reachedTogether) then
                  yield $"{code} cannot be reached from Vienna by any piece at all" ]
