namespace TCModel.Diplomacy

open TCModel.Engine

/// One of the seven, and nothing about who is playing it.
///
/// Which seat holds which power is settled once and never drawn for: seat one is Austria and
/// seat seven is Turkey, at every game there will ever be. That is not a rule of Diplomacy -
/// a room usually draws for them - but it is what lets `Seat` answer with a name, and a
/// record that says "Austria: A Vie - Tri" is a record anybody who plays this can read.
type Power =
    | Austria
    | England
    | France
    | Germany
    | Italy
    | Russia
    | Turkey

/// Which kind of piece, and the whole of the difference is where it may stand: an army walks
/// the land, a fleet swims the sea and hugs the coast, and the forty-two provinces both can
/// reach are where every game of this is decided.
type Kind =
    | Army
    | Fleet

/// Which coastline of a province that has more than one.
///
/// Three on this map do - Spain and St Petersburg have a north and a south, Bulgaria an east
/// and a south - and they have them because their two coasts face different waters with no
/// way round. A fleet on the north coast of Spain cannot reach the Mediterranean without
/// sailing the long way, so where a fleet stands is a province *and* a coast wherever there
/// is a choice, and a province alone everywhere else.
type Coast =
    | North
    | South
    | East

module Power =

    /// In seating order, which is alphabetical - and alphabetical is what every set of rules
    /// for this game prints them in, so nobody has to learn a second order.
    let all = [ Austria; England; France; Germany; Italy; Russia; Turkey ]

    /// How many sit down: seven, and exactly seven. There is no variant here for a table of
    /// five; a seat nobody is in is given to the machine, which is what `--rival` is for.
    let Count = List.length all

    let seatOf power =
        Seat.at (1 + List.findIndex ((=) power) all)

    /// The power at a seat. Every id in circulation was minted by a table for a game of this
    /// size, so the lookup cannot miss - but it is answered as an option rather than by
    /// indexing, because "cannot miss" is a claim about the caller and this is the callee.
    let atSeat seat =
        all |> List.tryItem (PlayerId.value seat - 1)

    let name =
        function
        | Austria -> "Austria"
        | England -> "England"
        | France -> "France"
        | Germany -> "Germany"
        | Italy -> "Italy"
        | Russia -> "Russia"
        | Turkey -> "Turkey"

    /// The adjective, for the sentences that want one: "the Austrian army in Vienna".
    let adjective =
        function
        | Austria -> "Austrian"
        | England -> "English"
        | France -> "French"
        | Germany -> "German"
        | Italy -> "Italian"
        | Russia -> "Russian"
        | Turkey -> "Turkish"

    /// The letter a board draws a power as, and the word a player types for it. One character
    /// each and all seven distinct, which is what lets a whole map fit on a screen.
    let letter =
        function
        | Austria -> "A"
        | England -> "E"
        | France -> "F"
        | Germany -> "G"
        | Italy -> "I"
        | Russia -> "R"
        | Turkey -> "T"

    /// The word a person types for a power, in a colour or in a whisper. Lower case and
    /// short, because it is typed rather than read.
    let key power = (name power).ToLowerInvariant()

    /// A power by whatever a person typed: its name, its adjective, its letter, or the first
    /// three of any of them. Generous on purpose - `press aus`, `press austria` and
    /// `press a` are all obviously the same thing, and refusing one of them teaches nobody
    /// anything.
    let byName (word: string) =
        let wanted = word.ToLowerInvariant()

        all
        |> List.tryFind (fun power ->
            let full = key power

            wanted = full
            || wanted = (letter power).ToLowerInvariant()
            || wanted = (adjective power).ToLowerInvariant()
            || (String.length wanted >= 3 && full.StartsWith wanted))

    let names = all |> List.map name |> String.concat ", "

module Kind =

    let letter =
        function
        | Army -> "A"
        | Fleet -> "F"

    let name =
        function
        | Army -> "army"
        | Fleet -> "fleet"

    let byName (word: string) =
        match word.ToLowerInvariant() with
        | "a"
        | "army" -> Some Army
        | "f"
        | "fleet" -> Some Fleet
        | _ -> None

module Coast =

    let all = [ North; South; East ]

    /// The two letters written after a slash, which is how every set of rules for this game
    /// writes them: `spa/nc`, `bul/ec`.
    let code =
        function
        | North -> "nc"
        | South -> "sc"
        | East -> "ec"

    let name =
        function
        | North -> "north coast"
        | South -> "south coast"
        | East -> "east coast"

    let byCode (word: string) =
        match word.ToLowerInvariant().TrimStart '(' |> fun w -> w.TrimEnd ')' with
        | "nc"
        | "n"
        | "north" -> Some North
        | "sc"
        | "s"
        | "south" -> Some South
        | "ec"
        | "e"
        | "east" -> Some East
        | _ -> None
