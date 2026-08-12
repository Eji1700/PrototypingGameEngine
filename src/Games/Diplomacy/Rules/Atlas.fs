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

    // --- where they lie, so the board can be drawn as a board -------------------------------------

    /// Rows north to south. Within a row the provinces stand two half-columns apart; each row is
    /// offset an odd number of half-columns from the one above. So a province touches the two
    /// beside it and two in each of the rows above and below - six shared sides, drawn as a
    /// honeycomb, exactly the way the other game of maps here draws its twelve regions.
    ///
    /// `"."` is a cell with nothing in it, and there are four of them - all in one row, all in
    /// the same place. A gap used to be load-bearing here: it was what kept two provinces that do
    /// not border from being drawn side by side, and there were a good many. Once the map drew
    /// every border there was, most of them were keeping nothing apart and were filled in.
    ///
    /// What is left is not a hole. It is **Switzerland**, which is not a province of this game
    /// because nothing may ever enter it, and the five provinces round it - Marseilles, Burgundy,
    /// Munich, Tyrolia, Piedmont - are exactly the five that ring Switzerland on the printed
    /// board. `diplomacy.fsx` checks that, so a gap opened anywhere else is an accident and fails
    /// rather than passing as scenery.
    ///
    /// **A province takes as many hexes as it needs, and that is the whole trick.** One hex a
    /// province gives it six sides and no more, and provinces here have up to eleven neighbours -
    /// which capped the first version of this map at about half the borders drawn. A region three
    /// or four hexes across has sides to spare, so it can touch everything it really touches.
    ///
    /// **And it is the border table.** Turncoats' board is a patch of a triangular lattice, so
    /// every one of its twenty-three borders can be drawn and `problems` insists every one of them
    /// is - the picture *is* its border table. That was the thing this board was thought not to be
    /// able to manage, and for a long time what was demanded here was one direction only: a side
    /// drawn is a border, a border may go undrawn. It is both directions now. **Every side this
    /// picture draws between two provinces is a real border, and every one of the two hundred and
    /// six borders is drawn.** `problems` refuses a game either way round.
    ///
    /// A side between two hexes of the *same* province is not a border at all - it is the inside
    /// of a country - so `problems` passes over those and looks only at where two different names
    /// meet.
    ///
    /// Grown rather than drawn by hand: the regions were seeded a hex apiece at roughly the right
    /// places and then spread outwards, a hex at a time, always into the space that met a
    /// neighbour they had not met yet and never into one that would put them beside a province
    /// they do not border. That is why some of the shapes are odd - the Norwegian Sea wraps round
    /// the Barents, and the Ionian round the Eastern Mediterranean. Those are the shapes that make
    /// the adjacencies come out right, and the adjacencies are what a map of this is *for*.
    ///
    /// What the grower could not do is back out of a corner. It only ever added a hex where one
    /// was free and safe, so wherever the right answer was to take a hex away from somebody or to
    /// move a province across, it simply stopped - and stopped somewhere that broke no rule and
    /// made no sense. Those places have since been put right by hand, and each of them is a case
    /// of that same failing.
    ///
    /// The Mid-Atlantic was the worst-served province on the board: it runs the whole west coast
    /// of Europe and reached four provinces short. It now runs on down the western margin, round
    /// the foot of Portugal and along the top of North Africa at one end and up past Ireland to
    /// the North Atlantic at the other - which is where the Atlantic actually is - and it now
    /// reaches every one of the ten provinces it borders. The map costs nothing for it: the
    /// western margin was already open at every row it uses, so the board is not one column wider
    /// than it was.
    ///
    /// The Barents had been left in the middle of the top row with the Norwegian Sea on both
    /// sides of it, touching nothing else - a sea of no consequence at all, when the whole of what
    /// it is for is that Russia's fleet comes out of St Petersburg into it. It sits at the top
    /// right now, above Norway and St Petersburg with the Norwegian Sea to its west, and has all
    /// three of the borders it really has. That freed the column between Edinburgh and Norway,
    /// which is the Norwegian Sea in every atlas and is drawn as it now - so the Norwegian Sea
    /// runs down to meet the North Sea, which it had never managed to touch.
    ///
    /// The Eastern Mediterranean was the Barents over again: one hex in the middle of the Ionian,
    /// with Ionian on all six sides and none of the three provinces it exists to touch. It is at
    /// the east end of the bottom row now, under Smyrna and Syria with the Aegean beside it, and
    /// the Ionian - which is the biggest sea on the board and can afford it - runs along the rest.
    ///
    /// Burgundy and Munich were short of each other and of Ruhr, penned in by a row of Piedmont
    /// that was doing nothing: three of Piedmont's six hexes touched only Piedmont and empty
    /// space, and one of Tyrolia's was the same. Handing that row to Burgundy joins all three, and
    /// what is left of it is the gap between France and Italy, which is the Alps and belongs there.
    ///
    /// Italy took two cells and no more. Rome had nowhere to reach Venice or Apulia from, and
    /// Venice could get at neither Rome nor Tuscany. The cell between them went to Rome and
    /// Piedmont's southern tip went to Venice, and with that every province in Italy - Rome,
    /// Venice, Tuscany, Apulia, Naples, Piedmont - touches everything it borders.
    ///
    /// The last two were Armenia with Sevastopol and Moscow with St Petersburg, and they are the
    /// reason the promise above is now made in both directions. Sevastopol takes one more hex east
    /// of the Black Sea and Armenia one under it; St Petersburg runs down the far side of Livonia
    /// on three cells that had nothing in them. With those the map draws every border there is.
    let private places =
        [ 0, [ "nao"; "nwg"; "nwg"; "nwg"; "nwg"; "bar"; "bar" ]
          -3, [ "mao"; "nao"; "cly"; "nwg"; "nwg"; "edi"; "nwg"; "nwy"; "stp" ]
          -4, [ "mao"; "iri"; "nao"; "cly"; "cly"; "cly"; "edi"; "nwg"; "nwy"; "stp"; "stp"; "stp"; "stp" ]
          -5, [ "mao"; "iri"; "lvp"; "cly"; "lvp"; "lvp"; "edi"; "nwg"; "nwy"; "nwy"; "nwy"; "nwy"; "nwy"; "stp" ]
          -4, [ "mao"; "iri"; "lvp"; "lvp"; "wal"; "yor"; "nth"; "nth"; "nth"; "nth"; "ska"; "swe"; "fin"; "stp" ]
          -3, [ "mao"; "iri"; "iri"; "wal"; "lon"; "nth"; "nth"; "nth"; "den"; "den"; "swe"; "bot"; "stp"; "stp"; "stp" ]
          -2, [ "mao"; "eng"; "eng"; "lon"; "lon"; "nth"; "hel"; "den"; "bal"; "bal"; "bot"; "bot"; "bot"; "lvn"; "stp" ]
          -3, [ "mao"; "eng"; "eng"; "eng"; "eng"; "nth"; "hol"; "kie"; "bal"; "bal"; "bal"; "bal"; "lvn"; "lvn"; "stp" ]
          -4, [ "mao"; "bre"; "pic"; "bel"; "bel"; "bel"; "hol"; "kie"; "kie"; "ber"; "pru"; "pru"; "pru"; "war"; "mos" ]
          -5, [ "mao"; "gas"; "bre"; "pic"; "bel"; "bel"; "hol"; "kie"; "kie"; "kie"; "ber"; "pru"; "pru"; "war"; "war"; "mos" ]
          -6, [ "mao"; "mao"; "gas"; "par"; "bur"; "bur"; "bel"; "ruh"; "ruh"; "mun"; "mun"; "sil"; "sil"; "sil"; "gal"; "ukr"; "sev" ]
          -5, [ "mao"; "gas"; "gas"; "bur"; "bur"; "bur"; "bur"; "bur"; "mun"; "mun"; "boh"; "boh"; "gal"; "gal"; "ukr"; "sev"; "sev" ]
          -6, [ "mao"; "spa"; "spa"; "gas"; "mar"; "."; "."; "."; "."; "tyr"; "boh"; "vie"; "vie"; "bud"; "rum"; "sev"; "bla"; "sev"; "sev" ]
          -5, [ "mao"; "spa"; "spa"; "spa"; "mar"; "pie"; "pie"; "pie"; "tyr"; "tyr"; "tyr"; "tri"; "tri"; "ser"; "rum"; "bla"; "bla"; "bla"; "arm" ]
          -4, [ "mao"; "por"; "por"; "spa"; "gol"; "tus"; "tus"; "ven"; "ven"; "ven"; "tri"; "tri"; "ser"; "ser"; "bul"; "con"; "ank"; "arm" ]
          -3, [ "mao"; "mao"; "spa"; "gol"; "tus"; "rom"; "rom"; "ven"; "adr"; "adr"; "tri"; "ser"; "ser"; "bul"; "con"; "ank"; "arm" ]
          -2, [ "mao"; "wes"; "wes"; "tys"; "rom"; "nap"; "apu"; "adr"; "adr"; "adr"; "alb"; "alb"; "gre"; "aeg"; "smy"; "smy"; "syr" ]
          -1, [ "naf"; "naf"; "wes"; "tys"; "tys"; "ion"; "adr"; "ion"; "ion"; "alb"; "gre"; "gre"; "aeg"; "smy"; "syr"; "syr" ]
          2, [ "naf"; "tun"; "tun"; "ion"; "ion"; "ion"; "ion"; "ion"; "ion"; "ion"; "ion"; "eas"; "eas" ] ]

    /// Every cell with the half-column it stands in, gaps and all.
    let private placedCells =
        places
        |> List.map (fun (start, cells) -> cells |> List.mapi (fun step code -> code, start + 2 * step))

    /// The same with the gaps thrown away, which is what the borders are read off.
    let private placedPlaces =
        placedCells |> List.map (List.filter (fun (code, _) -> code <> "."))

    let private asPair one other = min one other, max one other

    /// The borders this layout draws: two *different* provinces two half-columns apart in one
    /// row, or one half-column apart in rows that touch. Every one of them had better be real.
    ///
    /// Two hexes of the same province share a side as well, and that side is not a border - it
    /// is the inside of a country. Those are passed over rather than checked, which is the one
    /// thing that lets a province be more than one hex.
    let private drawnBorders =
        Set.ofList
            [ for row in placedPlaces do
                  for one, here in row do
                      for other, there in row do
                          if one <> other && abs (here - there) = 2 then
                              yield asPair one other

              for above, below in List.pairwise placedPlaces do
                  for one, here in above do
                      for other, there in below do
                          if one <> other && abs (here - there) = 1 then yield asPair one other ]

    /// Which hexes each province stands on, for the checks that ask about a region's shape.
    let private hexesOf =
        placedCells
        |> List.mapi (fun row cells -> cells |> List.map (fun (code, here) -> code, (row, here)))
        |> List.collect id
        |> List.filter (fun (code, _) -> code <> ".")
        |> List.groupBy fst
        |> List.map (fun (code, cells) -> code, cells |> List.map snd)

    /// The six hexes around one: two beside it, and two in each of the rows above and below.
    let private around (row, here) =
        [ row, here - 2
          row, here + 2
          row - 1, here - 1
          row - 1, here + 1
          row + 1, here - 1
          row + 1, here + 1 ]

    /// The rows as a screen wants them: how far the row is shifted from the westmost cell on the
    /// board, in half-columns, and then its cells in order - a province, or nothing at all.
    let layout =
        let westmost = placedCells |> List.collect id |> List.map snd |> List.min

        placedCells
        |> List.map (fun row ->
            let start = row |> List.map snd |> List.min

            start - westmost,
            row |> List.map (fun (code, _) -> if code = "." then None else byCode code))

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
                  yield $"{code} cannot be reached from Vienna by any piece at all"

          // --- and whether the map as drawn is the board
          //
          // Both directions: every side the picture draws is a border, and every border is
          // drawn. Which is to say the picture *is* the border table, and a player who reads
          // the map has read the whole truth about where a piece may go.
          //
          // It was one direction only for a long time, and the argument for that was sound
          // while it lasted: a province here has up to eleven neighbours where a hexagon has six
          // sides, so no map giving each province one hex could ever have drawn them all, and
          // the honest thing was to promise the half that could be kept. What answered it was
          // letting a province take as many hexes as its borders need, and then going back over
          // the places the layout had settled for less. The second half of the promise is made
          // here rather than described in a comment, so an edit that quietly drops a border
          // fails before a game is dealt, in the words of the border it dropped.
          let laid = placedPlaces |> List.collect id |> List.map fst

          let touching =
            Set.ofList
                [ for from, into in together do
                      for other in into do
                          if from <> other then
                              yield asPair from other ]

          for one, other in drawnBorders do
              if not (Set.contains (asPair one other) touching) then
                  yield $"the map draws {one} touching {other}, and they do not border"

          for one, other in touching do
              if not (Set.contains (asPair one other) drawnBorders) then
                  yield $"{one} borders {other}, and the map draws them apart"

          for code in laid do
              if not (Set.contains code known) then
                  yield $"the map lays out '{code}', which is not a province"

          for code in codes do
              if not (List.contains code laid) then
                  yield $"{code} is on the board and nowhere on the map"

          // A province may hold as many hexes as it needs, and they have to be one region. Two
          // blobs a map apart both labelled Munich are two Munichs, whatever the tables say.
          for code, hexes in hexesOf do
              let held = Set.ofList hexes

              let rec walk seen edge =
                  match edge with
                  | [] -> seen
                  | hex :: rest when Set.contains hex seen -> walk seen rest
                  | hex :: rest -> walk (Set.add hex seen) ((around hex |> List.filter (fun n -> Set.contains n held)) @ rest)

              match hexes with
              | [] -> ()
              | first :: _ ->
                  if Set.count (walk Set.empty [ first ]) <> List.length hexes then
                      yield $"{code} is drawn on the map in more than one piece"

          // A row whose cells do not alternate parity with the row above cannot share a side
          // with it at all, which would be a map in two halves rather than one map.
          for above, below in List.pairwise placedCells do
              let parity row = row |> List.map (snd >> abs >> (fun h -> h % 2)) |> List.distinct

              match parity above, parity below with
              | [ one ], [ other ] when one <> other -> ()
              | _ -> yield "two rows of the map do not sit half a cell apart" ]
