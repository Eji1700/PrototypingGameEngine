namespace TCModel.Compile

/// The twelve protocols a game is drafted from, and nothing about who holds one.
///
/// A protocol is the unit of everything at this game: it is what is drafted, it is what six
/// cards belong to, and it is what a player writes across a line. Which twelve there are is
/// the one piece of data this game is built out of, which is why `Faults` counts them before
/// anybody sits down.
type Protocol =
    | Fire
    | Water
    | Dark
    | Light
    | Metal
    | Gravity
    | Life
    | Death
    | Speed
    | Spirit
    | Psychic
    | Plague

module Protocol =

    /// How many each player drafts, and therefore how many lines there are: a player's
    /// protocols and the lines they sit on are the same count said twice, and `Faults` says
    /// so if they ever stop being.
    [<Literal>]
    let Each = 3

    /// All twelve, in the order they are offered at the draft. There are no duplicates, and
    /// a draft takes six of them, so half of these are left over in any one game.
    let all =
        [ Fire; Water; Dark; Light; Metal; Gravity; Life; Death; Speed; Spirit; Psychic; Plague ]

    let name =
        function
        | Fire -> "Fire"
        | Water -> "Water"
        | Dark -> "Dark"
        | Light -> "Light"
        | Metal -> "Metal"
        | Gravity -> "Gravity"
        | Life -> "Life"
        | Death -> "Death"
        | Speed -> "Speed"
        | Spirit -> "Spirit"
        | Psychic -> "Psychic"
        | Plague -> "Plague"

    /// The word a player types for it, which is its name in lower case. One function rather
    /// than a second table, so a protocol renamed is renamed everywhere at once.
    let key protocol = (name protocol).ToLowerInvariant()

    let byName (word: string) =
        let wanted = word.ToLowerInvariant()
        all |> List.tryFind (fun protocol -> key protocol = wanted)

    /// Every protocol there is, in the words a player would type - for the sentence a
    /// refusal ends with.
    let names = all |> List.map key |> String.concat ", "
