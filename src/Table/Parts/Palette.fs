namespace TCModel.Table

open System
open System.IO
open Spectre.Console

[<NoComparison>]
type Shade = { Name: string; Color: Color }

[<NoComparison>]
type Slot =
    { Key: string
      Draws: string
      Shows: string
      Standard: Shade }

[<NoComparison>]
type Palette =
    private
        { Own: Shade
          Shades: Map<string, Shade>
          Offered: Slot list }

module Palette =

    let private shade name color = { Name = name; Color = color }

    let crimson = shade "crimson" Color.Red1
    let azure = shade "azure" Color.DodgerBlue1
    let moss = shade "moss" Color.Green3
    let gold = shade "gold" Color.Gold1
    let slate = shade "slate" Color.Grey37
    let bone = shade "bone" Color.Silver

    let catalogue =
        [ crimson
          shade "ember" Color.OrangeRed1
          shade "amber" Color.Orange1
          gold
          shade "lemon" Color.Yellow1
          shade "lime" Color.GreenYellow
          moss
          shade "grass" Color.Green1
          shade "jade" Color.SpringGreen1
          shade "teal" Color.Turquoise2
          shade "sky" Color.Cyan1
          azure
          shade "indigo" Color.SlateBlue1
          shade "violet" Color.MediumPurple1
          shade "plum" Color.Magenta1
          shade "rose" Color.HotPink
          shade "sand" Color.Wheat1
          bone
          slate ]

    let named wanted =
        catalogue
        |> List.tryFind (fun shade -> shade.Name = wanted)
        |> Option.defaultValue slate


    let private triple (word: string) =
        let digits = if word.StartsWith "#" then word.Substring 1 else word

        if digits.Length = 6 && digits |> Seq.forall Uri.IsHexDigit then
            let part at =
                Convert.ToByte(digits.Substring(at, 2), 16)

            Some(Color(part 0, part 2, part 4))
        else
            None

    let private sayable (name: string) =
        name <> "" && name |> Seq.forall Char.IsLetter

    // Words up to a '#' that is not a colour, so a line may carry a comment after it and '#b7410e'
    // is still read as what it is.
    let private saying (line: string) =
        line.Split([| ' '; '\t' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.toList
        |> List.takeWhile (fun word -> not (word.StartsWith "#") || (triple word).IsSome)

    let private folding (shades, problems) (line: string) =
        let words = saying line

        let wrong said = shades, problems @ [ said ]

        let put shade =
            if shades |> List.exists (fun (have: Shade) -> have.Name = shade.Name) then
                shades |> List.map (fun have -> if have.Name = shade.Name then shade else have)
            else
                shades @ [ shade ]

        match words with
        | [] -> shades, problems
        | [ "no"; name ] ->
            if shades |> List.exists (fun shade -> shade.Name = name) then
                shades |> List.filter (fun shade -> shade.Name <> name), problems
            else
                wrong $"'no {name}' drops a colour there is none of."
        | [ name; colour ] ->
            match sayable name, triple colour with
            | false, _ -> wrong $"'{name}' is not a name for a colour. Letters only, and no spaces."
            | true, None -> wrong $"'{colour}' is not a colour. Say it as six hex digits - '#b7410e'."
            | true, Some colour -> put { Name = name; Color = colour }, problems
        | said ->
            let read = String.concat " " said
            wrong $"'{read}' is not '<name> #rrggbb', or 'no <name>'."

    let private asked (text: string) =
        text.Split '\n' |> Array.fold folding (catalogue, [])

    let private files =
        [ "colours.txt"; "colors.txt" ]
        |> List.map (fun name -> Path.Combine(Directory.GetCurrentDirectory(), name))

    let source = List.head files

    let private loaded =
        lazy
            (try
                match files |> List.tryFind File.Exists with
                | Some path ->
                    let shades, problems = asked (File.ReadAllText path)

                    if List.isEmpty shades then
                        catalogue,
                        problems
                        @ [ $"{Path.GetFileName path} leaves no colours at all, so the usual ones stand." ]
                    else
                        shades, problems |> List.map (fun said -> $"{Path.GetFileName path}: {said}")
                | None -> catalogue, []
             with problem ->
                 catalogue, [ $"The colours file could not be read: {problem.Message}" ])

    let shades = fst loaded.Value

    let complaints = snd loaded.Value

    [<Literal>]
    let Yours = "yours"

    let private ownSlot =
        { Key = Yours
          Draws = "your own seat, and whose turn it is"
          Shows = "(you)   ->"
          Standard = gold }

    let standard slots =
        { Own = ownSlot.Standard
          Shades = slots |> List.map (fun slot -> slot.Key, slot.Standard) |> Map.ofList
          Offered = slots }

    let slots palette = palette.Offered @ [ ownSlot ]

    let ink (shade: Shade) = shade.Color.ToMarkup()

    let paint (shade: Shade) = "#" + shade.Color.ToHex()

    let own palette = palette.Own

    let shadeOf key palette =
        palette.Shades |> Map.tryFind key |> Option.defaultValue slate

    let inkOf key palette = ink (shadeOf key palette)

    let inSlot (slot: Slot) palette =
        if slot.Key = Yours then palette.Own else shadeOf slot.Key palette

    let reset palette = standard palette.Offered

    let names = shades |> List.map (fun shade -> shade.Name) |> String.concat ", "

    let private keysOf palette =
        slots palette |> List.map (fun slot -> slot.Key) |> String.concat ", "

    let private withShade key shade palette =
        if key = Yours then
            { palette with Own = shade }
        else
            { palette with
                Shades = palette.Shades |> Map.add key shade }

    let set (key: string) (colour: string) palette =
        match slots palette |> List.tryFind (fun candidate -> candidate.Key = key) with
        | None -> Error $"There is nothing called '{key}' to colour. There is {keysOf palette}."
        | Some slot ->
            match shades |> List.tryFind (fun shade -> shade.Name = colour) with
            | None -> Error $"'{colour}' is not a colour I have. There is {names}."
            | Some shade -> Ok(withShade slot.Key shade palette)


    let private nameOf key palette =
        if key = Yours then palette.Own.Name else (shadeOf key palette).Name

    let write palette =
        slots palette
        |> List.map (fun slot -> $"{slot.Key}={nameOf slot.Key palette}")
        |> String.concat " "

    let read slots (text: string) =
        text.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.fold
            (fun palette pair ->
                match pair.Split '=' with
                | [| key; colour |] -> set key colour palette |> Result.defaultValue palette
                | _ -> palette)
            (standard slots)
