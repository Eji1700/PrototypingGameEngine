namespace TCModel.Diplomacy

type Terrain =
    | Inland
    | Coastal
    | Sea

type Centre =
    | NotACentre
    | Neutral
    | Home of Power

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

type ProvinceId = private ProvinceId of string

type Location = { At: ProvinceId; Coast: Coast option }

type Province =
    { Id: ProvinceId
      Name: string
      Terrain: Terrain
      Centre: Centre
      Region: Region }

module Atlas =


    let private declared =
        [ "cly", "Clyde", Coastal, NotACentre, TheIsles
          "edi", "Edinburgh", Coastal, Home England, TheIsles
          "lvp", "Liverpool", Coastal, Home England, TheIsles
          "yor", "Yorkshire", Coastal, NotACentre, TheIsles
          "wal", "Wales", Coastal, NotACentre, TheIsles
          "lon", "London", Coastal, Home England, TheIsles

          "por", "Portugal", Coastal, Neutral, Iberia
          "spa", "Spain", Coastal, Neutral, Iberia

          "bre", "Brest", Coastal, Home France, FranceAnd
          "pic", "Picardy", Coastal, NotACentre, FranceAnd
          "par", "Paris", Inland, Home France, FranceAnd
          "bur", "Burgundy", Inland, NotACentre, FranceAnd
          "gas", "Gascony", Coastal, NotACentre, FranceAnd
          "mar", "Marseilles", Coastal, Home France, FranceAnd

          "bel", "Belgium", Coastal, Neutral, TheLowCountries
          "hol", "Holland", Coastal, Neutral, TheLowCountries

          "ruh", "Ruhr", Inland, NotACentre, GermanyAnd
          "kie", "Kiel", Coastal, Home Germany, GermanyAnd
          "ber", "Berlin", Coastal, Home Germany, GermanyAnd
          "mun", "Munich", Inland, Home Germany, GermanyAnd
          "sil", "Silesia", Inland, NotACentre, GermanyAnd
          "pru", "Prussia", Coastal, NotACentre, GermanyAnd

          "den", "Denmark", Coastal, Neutral, Scandinavia
          "nwy", "Norway", Coastal, Neutral, Scandinavia
          "swe", "Sweden", Coastal, Neutral, Scandinavia
          "fin", "Finland", Coastal, NotACentre, Scandinavia

          "stp", "St Petersburg", Coastal, Home Russia, RussiaAnd
          "lvn", "Livonia", Coastal, NotACentre, RussiaAnd
          "mos", "Moscow", Inland, Home Russia, RussiaAnd
          "war", "Warsaw", Inland, Home Russia, RussiaAnd
          "ukr", "Ukraine", Inland, NotACentre, RussiaAnd
          "sev", "Sevastopol", Coastal, Home Russia, RussiaAnd

          "boh", "Bohemia", Inland, NotACentre, AustriaAnd
          "gal", "Galicia", Inland, NotACentre, AustriaAnd
          "tyr", "Tyrolia", Inland, NotACentre, AustriaAnd
          "vie", "Vienna", Inland, Home Austria, AustriaAnd
          "bud", "Budapest", Inland, Home Austria, AustriaAnd
          "tri", "Trieste", Coastal, Home Austria, AustriaAnd

          "pie", "Piedmont", Coastal, NotACentre, ItalyAnd
          "tus", "Tuscany", Coastal, NotACentre, ItalyAnd
          "ven", "Venice", Coastal, Home Italy, ItalyAnd
          "rom", "Rome", Coastal, Home Italy, ItalyAnd
          "apu", "Apulia", Coastal, NotACentre, ItalyAnd
          "nap", "Naples", Coastal, Home Italy, ItalyAnd

          "ser", "Serbia", Inland, Neutral, TheBalkans
          "alb", "Albania", Coastal, NotACentre, TheBalkans
          "gre", "Greece", Coastal, Neutral, TheBalkans
          "bul", "Bulgaria", Coastal, Neutral, TheBalkans
          "rum", "Rumania", Coastal, Neutral, TheBalkans

          "con", "Constantinople", Coastal, Home Turkey, TurkeyAnd
          "ank", "Ankara", Coastal, Home Turkey, TurkeyAnd
          "smy", "Smyrna", Coastal, Home Turkey, TurkeyAnd
          "arm", "Armenia", Coastal, NotACentre, TurkeyAnd
          "syr", "Syria", Coastal, NotACentre, TurkeyAnd

          "naf", "North Africa", Coastal, NotACentre, Africa
          "tun", "Tunis", Coastal, Neutral, Africa

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

    let private twoCoasted =
        [ "spa", [ North; South ]; "bul", [ East; South ]; "stp", [ North; South ] ]


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


    let private fleetBorders =
        [ "nao", [ "nwg"; "iri"; "mao"; "cly"; "lvp" ]
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


    let all =
        declared
        |> List.map (fun (code, name, terrain, centre, region) ->
            { Id = ProvinceId code
              Name = name
              Terrain = terrain
              Centre = centre
              Region = region })

    let code (ProvinceId text) = text

    let private lookup =
        all |> List.map (fun province -> code province.Id, province) |> Map.ofList

    let count = List.length all

    let byCode (text: string) =
        Map.tryFind (text.ToLowerInvariant()) lookup
        |> Option.map (fun province -> province.Id)

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

    let centres =
        all |> List.filter (fun p -> p.Centre <> NotACentre) |> List.map (fun p -> p.Id)

    let homesOf power =
        all |> List.filter (fun p -> p.Centre = Home power) |> List.map (fun p -> p.Id)


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

    let spotBy (text: string) =
        parseCoast text
        |> Option.bind (fun (province, coast) -> byWord province |> Option.map (fun id -> { At = id; Coast = coast }))

    let coastsOf id =
        twoCoasted
        |> List.tryFind (fun (c, _) -> c = code id)
        |> Option.map snd
        |> Option.defaultValue []

    let hasCoasts id = coastsOf id |> List.isEmpty |> not

    let standing kind id coast =
        match kind with
        | Army -> { At = id; Coast = None }
        | Fleet -> { At = id; Coast = coast }


    // Army borders are read as provinces and fleet borders as locations, because which coast a
    // fleet stands on changes where it can go next: `spa/nc` and `spa/sc` are one province and two
    // different sets of neighbours.
    let private edgesOf declaredBorders reading =
        declaredBorders
        |> List.choose (fun (from, into) -> reading from |> Option.map (fun key -> key, into |> List.choose reading))
        |> Map.ofList

    let private armyMap = edgesOf armyBorders byCode

    let private fleetMap = edgesOf fleetBorders locationOf

    let armyReach id =
        Map.tryFind id armyMap |> Option.defaultValue []

    let fleetReach location =
        Map.tryFind location fleetMap |> Option.defaultValue []

    let reach kind location =
        match kind with
        | Army -> armyReach location.At |> List.map (fun id -> { At = id; Coast = None })
        | Fleet -> fleetReach location

    let canGo kind location into =
        reach kind location |> List.exists (fun there -> there.At = into)

    let waysInto kind location into =
        reach kind location |> List.filter (fun there -> there.At = into)

    let walkable from into = armyReach from |> List.contains into


    // The map, drawn as rows of hexes. Each row starts at an offset counted in halves of a cell and
    // its cells sit two apart, so a row offset by an odd number nestles between the one above it. A
    // province named in several neighbouring cells is drawn as one shape covering all of them, and
    // "." is a hole. `problems` checks the shape this makes against the borders actually declared.
    let private places =
        [ 0, [ "nao"; "nwg"; "nwg"; "nwg"; "nwg"; "bar"; "bar" ]
          -3, [ "mao"; "nao"; "cly"; "nwg"; "nwg"; "edi"; "nwg"; "nwy"; "stp" ]
          -4,
          [ "mao"
            "iri"
            "nao"
            "cly"
            "cly"
            "cly"
            "edi"
            "nwg"
            "nwy"
            "stp"
            "stp"
            "stp"
            "stp" ]
          -5,
          [ "mao"
            "iri"
            "lvp"
            "cly"
            "lvp"
            "lvp"
            "edi"
            "nwg"
            "nwy"
            "nwy"
            "nwy"
            "nwy"
            "nwy"
            "stp" ]
          -4,
          [ "mao"
            "iri"
            "lvp"
            "lvp"
            "wal"
            "yor"
            "nth"
            "nth"
            "nth"
            "nth"
            "ska"
            "swe"
            "fin"
            "stp" ]
          -3,
          [ "mao"
            "iri"
            "iri"
            "wal"
            "lon"
            "nth"
            "nth"
            "nth"
            "den"
            "den"
            "swe"
            "bot"
            "stp"
            "stp"
            "stp" ]
          -2,
          [ "mao"
            "eng"
            "eng"
            "lon"
            "lon"
            "nth"
            "hel"
            "den"
            "bal"
            "bal"
            "bot"
            "bot"
            "bot"
            "lvn"
            "stp" ]
          -3,
          [ "mao"
            "eng"
            "eng"
            "eng"
            "eng"
            "nth"
            "hol"
            "kie"
            "bal"
            "bal"
            "bal"
            "bal"
            "lvn"
            "lvn"
            "stp" ]
          -4,
          [ "mao"
            "bre"
            "pic"
            "bel"
            "bel"
            "bel"
            "hol"
            "kie"
            "kie"
            "ber"
            "pru"
            "pru"
            "pru"
            "war"
            "mos" ]
          -5,
          [ "mao"
            "gas"
            "bre"
            "pic"
            "bel"
            "bel"
            "hol"
            "kie"
            "kie"
            "kie"
            "ber"
            "pru"
            "pru"
            "war"
            "war"
            "mos" ]
          -6,
          [ "mao"
            "mao"
            "gas"
            "par"
            "bur"
            "bur"
            "bel"
            "ruh"
            "ruh"
            "mun"
            "mun"
            "sil"
            "sil"
            "sil"
            "gal"
            "ukr"
            "sev" ]
          -5,
          [ "mao"
            "gas"
            "gas"
            "bur"
            "bur"
            "bur"
            "bur"
            "bur"
            "mun"
            "mun"
            "boh"
            "boh"
            "gal"
            "gal"
            "ukr"
            "sev"
            "sev" ]
          -6,
          [ "mao"
            "spa"
            "spa"
            "gas"
            "mar"
            "."
            "."
            "."
            "."
            "tyr"
            "boh"
            "vie"
            "vie"
            "bud"
            "rum"
            "sev"
            "bla"
            "sev"
            "sev" ]
          -5,
          [ "mao"
            "spa"
            "spa"
            "spa"
            "mar"
            "pie"
            "pie"
            "pie"
            "tyr"
            "tyr"
            "tyr"
            "tri"
            "tri"
            "ser"
            "rum"
            "bla"
            "bla"
            "bla"
            "arm" ]
          -4,
          [ "mao"
            "por"
            "por"
            "spa"
            "gol"
            "tus"
            "tus"
            "ven"
            "ven"
            "ven"
            "tri"
            "tri"
            "ser"
            "ser"
            "bul"
            "con"
            "ank"
            "arm" ]
          -3,
          [ "mao"
            "mao"
            "spa"
            "gol"
            "tus"
            "rom"
            "rom"
            "ven"
            "adr"
            "adr"
            "tri"
            "ser"
            "ser"
            "bul"
            "con"
            "ank"
            "arm" ]
          -2,
          [ "mao"
            "wes"
            "wes"
            "tys"
            "rom"
            "nap"
            "apu"
            "adr"
            "adr"
            "adr"
            "alb"
            "alb"
            "gre"
            "aeg"
            "smy"
            "smy"
            "syr" ]
          -1,
          [ "naf"
            "naf"
            "wes"
            "tys"
            "tys"
            "ion"
            "adr"
            "ion"
            "ion"
            "alb"
            "gre"
            "gre"
            "aeg"
            "smy"
            "syr"
            "syr" ]
          2,
          [ "naf"
            "tun"
            "tun"
            "ion"
            "ion"
            "ion"
            "ion"
            "ion"
            "ion"
            "ion"
            "ion"
            "eas"
            "eas" ] ]

    let private placedCells =
        places
        |> List.map (fun (start, cells) -> cells |> List.mapi (fun step code -> code, start + 2 * step))

    let private placedPlaces =
        placedCells |> List.map (List.filter (fun (code, _) -> code <> "."))

    let private asPair one other = min one other, max one other

    let private drawnBorders =
        Set.ofList
            [ for row in placedPlaces do
                  for one, here in row do
                      for other, there in row do
                          if one <> other && abs (here - there) = 2 then yield asPair one other

              for above, below in List.pairwise placedPlaces do
                  for one, here in above do
                      for other, there in below do
                          if one <> other && abs (here - there) = 1 then yield asPair one other ]

    let private hexesOf =
        placedCells
        |> List.mapi (fun row cells -> cells |> List.map (fun (code, here) -> code, (row, here)))
        |> List.collect id
        |> List.filter (fun (code, _) -> code <> ".")
        |> List.groupBy fst
        |> List.map (fun (code, cells) -> code, cells |> List.map snd)

    let private around (row, here) =
        [ row, here - 2
          row, here + 2
          row - 1, here - 1
          row - 1, here + 1
          row + 1, here - 1
          row + 1, here + 1 ]

    // Rows are shifted to whatever offset each was written with, so the whole map is slid east until
    // the furthest-west cell sits at nothing and no row has to be drawn at a negative column.
    let layout =
        let westmost = placedCells |> List.collect id |> List.map snd |> List.min

        placedCells
        |> List.map (fun row ->
            let start = row |> List.map snd |> List.min

            start - westmost, row |> List.map (fun (code, _) -> if code = "." then None else byCode code))


    let problems =
        let codes = declared |> List.map (fun (code, _, _, _, _) -> code)
        let known = Set.ofList codes

        let named = List.map (fun (code, _, _, _, _) -> code) >> Set.ofList

        let mentioned table =
            table
            |> List.collect (fun (from: string, into) -> from :: into)
            |> List.map (fun text -> (text.Split '/')[0])
            |> Set.ofList

        let pairs table =
            table
            |> List.collect (fun (from, into) -> into |> List.map (fun there -> from, there))

        let unmirrored table =
            let held = Set.ofList (pairs table)

            pairs table
            |> List.filter (fun (from, into) -> not (Set.contains (into, from) held))

        let terrainOfCode code =
            declared
            |> List.tryPick (fun (c, _, terrain, _, _) -> if c = code then Some terrain else None)

        let reachedFrom start borders =
            let rec walk seen =
                function
                | [] -> seen
                | here :: rest when Set.contains here seen -> walk seen rest
                | here :: rest ->
                    let next =
                        borders
                        |> List.tryFind (fst >> (=) here)
                        |> Option.map snd
                        |> Option.defaultValue []

                    walk (Set.add here seen) (next @ rest)

            walk Set.empty [ start ]

        let together =
            let flatten table =
                table
                |> List.map (fun (from: string, into) ->
                    (from.Split '/')[0], into |> List.map (fun (there: string) -> (there.Split '/')[0]))

            flatten armyBorders @ flatten fleetBorders
            |> List.groupBy fst
            |> List.map (fun (from, rows) -> from, rows |> List.collect snd |> List.distinct)

        [ if List.length codes <> List.length (List.distinct codes) then
              yield "the same province written down twice"

          if count <> 75 then yield $"{count} provinces, where the board has 75"

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

          for missing in Set.difference (mentioned armyBorders) known do
              yield $"an army border to '{missing}', which is not a province"

          for missing in Set.difference (mentioned fleetBorders) known do
              yield $"a fleet border to '{missing}', which is not a province"

          for from, into in unmirrored armyBorders do
              yield $"{from} borders {into} by land, and {into} does not border {from}"

          for from, into in unmirrored fleetBorders do
              yield $"{from} borders {into} by sea, and {into} does not border {from}"

          if armyBorders |> List.exists (fun (from, into) -> List.contains from into) then
              yield "a province bordering itself by land"

          if fleetBorders |> List.exists (fun (from, into) -> List.contains from into) then
              yield "a province bordering itself by sea"

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

          let reachedTogether = reachedFrom "vie" together

          for code in codes do
              if not (Set.contains code reachedTogether) then
                  yield $"{code} cannot be reached from Vienna by any piece at all"

          let laid = placedPlaces |> List.collect id |> List.map fst

          let touching =
              Set.ofList
                  [ for from, into in together do
                        for other in into do
                            if from <> other then yield asPair from other ]

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

          for above, below in List.pairwise placedCells do
              let parity row =
                  row |> List.map (snd >> abs >> (fun h -> h % 2)) |> List.distinct

              match parity above, parity below with
              | [ one ], [ other ] when one <> other -> ()
              | _ -> yield "two rows of the map do not sit half a cell apart" ]
