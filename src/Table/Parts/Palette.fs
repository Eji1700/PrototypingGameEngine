namespace Prototyping.Table

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

/// The reader's own colour sits in `Shades` under its own key beside the game's slots, so that the
/// one colour every game shares is looked up, set and written down the same way as the rest.
[<NoComparison>]
type Palette =
    private
        { Shades: Map<string, Shade>
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

    /// The colours a file's lines leave standing, and what was wrong with any line that left
    /// nothing. A file that takes every colour away is a file that changed nothing, since a board
    /// has to have something to be drawn in.
    let fromText (text: string) =
        // A file written by another program can open with a byte order mark, which would
        // otherwise be the first letter of the first name.
        let shades, problems =
            text.TrimStart('\uFEFF').Split '\n' |> Array.fold folding (catalogue, [])

        if List.isEmpty shades then
            catalogue, problems @ [ "That leaves no colours at all, so the usual ones stand." ]
        else
            shades, problems

    let private files =
        [ "colours.txt"; "colors.txt" ]
        |> List.map (fun name -> Path.Combine(Directory.GetCurrentDirectory(), name))

    let private loaded =
        lazy
            (try
                match files |> List.tryFind File.Exists with
                | Some path ->
                    let shades, problems = fromText (File.ReadAllText path)
                    shades, problems |> List.map (fun said -> $"{Path.GetFileName path}: {said}")
                | None -> catalogue, []
             with problem ->
                 catalogue, [ $"The colours file could not be read: {problem.Message}" ])

    let shades = fst loaded.Value

    let complaints = snd loaded.Value

    [<Literal>]
    let private Yours = "yours"

    let private ownSlot =
        { Key = Yours
          Draws = "your own seat, and whose turn it is"
          Shows = "(you)   ->"
          Standard = gold }

    let standard slots =
        { Shades =
            slots @ [ ownSlot ]
            |> List.map (fun slot -> slot.Key, slot.Standard)
            |> Map.ofList
          Offered = slots }

    let slots palette = palette.Offered @ [ ownSlot ]

    let ink (shade: Shade) = shade.Color.ToMarkup()

    let paint (shade: Shade) = "#" + shade.Color.ToHex()

    let shadeOf key palette =
        palette.Shades |> Map.tryFind key |> Option.defaultValue slate

    let own palette = shadeOf Yours palette

    let inkOf key palette = ink (shadeOf key palette)

    let inSlot (slot: Slot) palette = shadeOf slot.Key palette

    let reset palette = standard palette.Offered

    let names = shades |> List.map (fun shade -> shade.Name) |> String.concat ", "

    let private keysOf palette =
        slots palette |> List.map (fun slot -> slot.Key) |> String.concat ", "

    let set (key: string) (colour: string) palette =
        match slots palette |> List.tryFind (fun candidate -> candidate.Key = key) with
        | None -> Error $"There is nothing called '{key}' to colour. There is {keysOf palette}."
        | Some slot ->
            match shades |> List.tryFind (fun shade -> shade.Name = colour) with
            | None -> Error $"'{colour}' is not a colour I have. There is {names}."
            | Some shade ->
                Ok
                    { palette with
                        Shades = Map.add slot.Key shade palette.Shades }

    let write palette =
        slots palette
        |> List.map (fun slot -> $"{slot.Key}={(inSlot slot palette).Name}")
        |> String.concat " "

    /// A palette as it comes off the wire - the query string a page's colour form sends, or a
    /// cookie an earlier version of this program wrote. A word not known here leaves that slot
    /// standard rather than turning the reader away, since nobody at that end is at a prompt to
    /// read a refusal; the same words typed at the Video page are refused in full.
    let read slots (text: string) =
        text.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.fold
            (fun palette pair ->
                match pair.Split '=' with
                | [| key; colour |] -> set key colour palette |> Result.defaultValue palette
                | _ -> palette)
            (standard slots)
