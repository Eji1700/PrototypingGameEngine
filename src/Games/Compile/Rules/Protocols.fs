namespace TCModel.Compile

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

    [<Literal>]
    let Each = 3

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

    let key protocol = (name protocol).ToLowerInvariant()

    let byName (word: string) =
        let wanted = word.ToLowerInvariant()
        all |> List.tryFind (fun protocol -> key protocol = wanted)

    let names = all |> List.map key |> String.concat ", "

    let rec orders protocols =
        match protocols with
        | [] -> [ [] ]
        | _ ->
            protocols
            |> List.collect (fun one ->
                orders (protocols |> List.filter ((<>) one))
                |> List.map (fun rest -> one :: rest))
