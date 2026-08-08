namespace TCModel.Table

open System
open Spectre.Console

/// A colour, and the word a person says for it.
///
/// The word is the point of the pair. Spectre knows 256 colours and calls them things like
/// `mediumpurple1`; nobody sitting down to a game wants to type that, so every colour on
/// offer here has a short name of its own and the machine's name for it never has to be
/// said out loud.
///
/// Two shades can be told apart but not put in order: Spectre's colour is a triple of
/// brightnesses, and there is no answer to which of two colours comes first.
[<NoComparison>]
type Shade = { Name: string; Color: Color }

/// One thing that takes a colour: the word a person types for it, what it is for, and a
/// scrap drawn in it so the list of choices is also a look at them.
///
/// The game says what its own are. A game of stones colours three factions and what is held
/// back from a reader; a game of noughts and crosses colours two marks and nothing else -
/// and neither of them has to be known about here for the screen that offers them, the line
/// that changes one, or the two halves of sending a palette down a wire to go on working.
[<NoComparison>]
type Slot =
    { Key: string
      Draws: string
      Shows: string
      Standard: Shade }

/// Which colour is drawn for what.
///
/// It is not part of the game. A palette travels with the view rather than with the model -
/// the same position drawn twice in two palettes is the same position - so it stays out of
/// the record, and two people at one networked table can read one board in colours that
/// have nothing to do with each other.
///
/// `Yours` is the one colour that is not the game's. Every game there could be has a seat
/// belonging to whoever is reading and a turn that is somebody's, and both are marked the
/// same way whatever is being played, so that one is kept here rather than asked for. The
/// rest is whatever the game said it colours, held by the words a person types for them.
[<NoComparison>]
type Palette =
    private
        { Own: Shade
          Shades: Map<string, Shade>
          Offered: Slot list }

module Palette =

    let private shade name color = { Name = name; Color = color }

    // Eight-bit colours rather than the sixteen named ones, and each a good deal brighter
    // than the word for it: a stone drawn in the flat version of its own colour is hard to
    // pick out of a map, and on a dark screen a dark blue is barely there at all.
    let crimson = shade "crimson" Color.Red1
    let azure = shade "azure" Color.DodgerBlue1
    let moss = shade "moss" Color.Green3
    let gold = shade "gold" Color.Gold1
    let slate = shade "slate" Color.Grey37
    let bone = shade "bone" Color.Silver

    /// Every colour on offer, in the order they are offered - warm through cold, and the
    /// two quiet ones last. Enough to tell a handful of sides apart on any screen, and few
    /// enough to read out in a line or two.
    let shades =
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

    /// The word every game uses for its own seat. Said here rather than by each game,
    /// because the screen that offers the colours has to list it alongside theirs and
    /// neither of them should have to agree with the other about how it is spelt.
    [<Literal>]
    let Yours = "yours"

    let private ownSlot =
        { Key = Yours
          Draws = "your own seat, and whose turn it is"
          Shows = "(you)   ->"
          Standard = gold }

    /// The colours a game is drawn in unless somebody says otherwise: whatever each of its
    /// slots said it starts out as.
    let standard slots =
        { Own = ownSlot.Standard
          Shades = slots |> List.map (fun slot -> slot.Key, slot.Standard) |> Map.ofList
          Offered = slots }

    /// Every slot the screen offers: the game's own, and last the one that is not the
    /// game's. Last because it is the odd one out - a player reading down the list meets
    /// what they came for first, and `yours` is the same row at every game there will ever
    /// be, so it belongs at the bottom rather than in among them.
    let slots palette = palette.Offered @ [ ownSlot ]

    /// A shade as markup says it - in Spectre's word for the colour rather than a game's,
    /// which Spectre has never heard of.
    ///
    /// Not as a hex triple, though that would also be understood: several of these have
    /// the same triple as one of the sixteen colours a terminal lets its owner re-theme,
    /// and a red the reader has quietly turned brown is not the red that was chosen here.
    let ink (shade: Shade) = shade.Color.ToMarkup()

    /// The same shade as a browser says it, which is the hex triple after all.
    ///
    /// The reason `ink` above gives for avoiding hex is a reason about terminals: sixteen
    /// of their colours belong to whoever owns the terminal and may have been re-themed.
    /// A browser has no such sixteen, so there is nothing to defer to and the triple is
    /// exact - which is the whole of why this is a second function rather than the first
    /// one used twice.
    let paint (shade: Shade) = "#" + shade.Color.ToHex()

    /// The reader's own seat, and the arrow marking whoever is to play.
    let own palette = palette.Own

    /// What the game said it draws this in. A key nothing was ever set for comes back in
    /// the quiet colour rather than throwing: a palette is decoration, and a game that has
    /// asked for a slot it never declared should be readable while somebody fixes it.
    let shadeOf key palette =
        palette.Shades |> Map.tryFind key |> Option.defaultValue slate

    let inkOf key palette = ink (shadeOf key palette)

    /// What one slot is drawn in now, whichever side of the line it falls.
    let inSlot (slot: Slot) palette =
        if slot.Key = Yours then palette.Own else shadeOf slot.Key palette

    /// All of them back as they started, for the same game. A palette carries the slots it
    /// was built from, so putting them back needs nothing from outside.
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

    /// Change one colour, in the words a person types. Both halves are answered in their
    /// own terms, because a player who misremembers a colour is told which there are
    /// rather than simply refused.
    let set (key: string) (colour: string) palette =
        match slots palette |> List.tryFind (fun candidate -> candidate.Key = key) with
        | None -> Error $"There is nothing called '{key}' to colour. There is {keysOf palette}."
        | Some slot ->
            match shades |> List.tryFind (fun shade -> shade.Name = colour) with
            | None -> Error $"'{colour}' is not a colour I have. There is {names}."
            | Some shade -> Ok(withShade slot.Key shade palette)

    // --- down a wire ---------------------------------------------------------------------
    //
    // A board is drawn at the table, so a player joining one has to say what colours to draw
    // it in. It goes as the words a person would have typed - the same words, read by the
    // same function - so there is no second spelling of a palette to keep in step with this
    // one.

    let private nameOf key palette =
        if key = Yours then palette.Own.Name else (shadeOf key palette).Name

    let write palette =
        slots palette
        |> List.map (fun slot -> $"{slot.Key}={nameOf slot.Key palette}")
        |> String.concat " "

    /// Read one back, keeping the standard colour for anything the words do not name.
    /// Nothing here can fail: a palette is only how a board is drawn, and a word this table
    /// does not know is worth passing over rather than turning a player away for.
    let read slots (text: string) =
        text.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.fold
            (fun palette pair ->
                match pair.Split '=' with
                | [| key; colour |] -> set key colour palette |> Result.defaultValue palette
                | _ -> palette)
            (standard slots)
