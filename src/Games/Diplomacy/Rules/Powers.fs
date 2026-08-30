namespace Prototyping.Diplomacy

open Prototyping.Engine

type Power =
    | Austria
    | England
    | France
    | Germany
    | Italy
    | Russia
    | Turkey

type Kind =
    | Army
    | Fleet

type Coast =
    | North
    | South
    | East

module Power =

    let all = [ Austria; England; France; Germany; Italy; Russia; Turkey ]

    let Count = List.length all

    let seatOf power =
        Seat.at (1 + List.findIndex ((=) power) all)

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

    let adjective =
        function
        | Austria -> "Austrian"
        | England -> "English"
        | France -> "French"
        | Germany -> "German"
        | Italy -> "Italian"
        | Russia -> "Russian"
        | Turkey -> "Turkish"

    let letter =
        function
        | Austria -> "A"
        | England -> "E"
        | France -> "F"
        | Germany -> "G"
        | Italy -> "I"
        | Russia -> "R"
        | Turkey -> "T"

    let key power = (name power).ToLowerInvariant()

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
