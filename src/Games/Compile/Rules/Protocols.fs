namespace TCModel.Compile

/// The twelve protocols a game is drafted from, and nothing about who holds one.
///
/// A protocol is the unit of everything at this game: it is what is drafted, it is what six
/// cards belong to, and it is what a player writes across a line. Which twelve there are is
/// the one piece of data this game is built out of, which is why `Faults` counts them before
/// anybody sits down.
type Protocol =
    | Apathy
    | Darkness
    | Death
    | Fire
    | Gravity
    | Hate
    | Life
    | Light
    | Love
    | Metal
    | Plague
    | Psychic
    | Speed
    | Spirit
    | Water


module Protocol =

    /// How many each player drafts, and therefore how many lines there are: a player's
    /// protocols and the lines they sit on are the same count said twice, and `Faults` says
    /// so if they ever stop being.
    [<Literal>]
    let Each = 3

    /// All fifteen, in the order they are offered at the draft - which is alphabetical, because
    /// nothing else about them has an order and a list somebody has to search should be in the
    /// one order everybody already knows.
    ///
    /// There are no duplicates, and a draft takes six, so nine of these are left over in any one
    /// game. That the pool is two and a half times what a game uses is what makes the draft a
    /// real decision rather than a formality.
    let all =
        [ Apathy
          Darkness
          Death
          Fire
          Gravity
          Hate
          Life
          Light
          Love
          Metal
          Plague
          Psychic
          Speed
          Spirit
          Water ]

    let name =
        function
        | Apathy -> "Apathy"
        | Darkness -> "Darkness"
        | Death -> "Death"
        | Fire -> "Fire"
        | Gravity -> "Gravity"
        | Hate -> "Hate"
        | Life -> "Life"
        | Light -> "Light"
        | Love -> "Love"
        | Metal -> "Metal"
        | Plague -> "Plague"
        | Psychic -> "Psychic"
        | Speed -> "Speed"
        | Spirit -> "Spirit"
        | Water -> "Water"

    /// The word a player types for it, which is its name in lower case. One function rather
    /// than a second table, so a protocol renamed is renamed everywhere at once.
    let key protocol = (name protocol).ToLowerInvariant()

    let byName (word: string) =
        let wanted = word.ToLowerInvariant()
        all |> List.tryFind (fun protocol -> key protocol = wanted)

    /// Every protocol there is, in the words a player would type - for the sentence a
    /// refusal ends with.
    let names = all |> List.map key |> String.concat ", "

    /// Every order a run of protocols could be put in. Six, for three of them, which is few
    /// enough to offer whole rather than asking somebody to think of one - and it is the same
    /// six whether a player is laying them out at the start or being made to move them by the
    /// control component.
    let rec orders protocols =
        match protocols with
        | [] -> [ [] ]
        | _ ->
            protocols
            |> List.collect (fun one -> orders (protocols |> List.filter ((<>) one)) |> List.map (fun rest -> one :: rest))
