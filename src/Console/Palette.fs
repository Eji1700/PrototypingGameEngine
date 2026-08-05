namespace TCModel.Console

open System
open Spectre.Console
open TCModel.Domain

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

/// Which colour is drawn for what.
///
/// Five things want one: the three factions, the reader's own seat, and everything being
/// held back from them. Nothing else in the program picks a colour for itself, so this is
/// the whole of what a player can change.
///
/// It is not part of the game. A palette travels with the view rather than with the model -
/// the same position drawn twice in two palettes is the same position - so it stays out of
/// the record, and two people at one networked table can read one board in colours that
/// have nothing to do with each other.
[<NoComparison>]
type Palette =
    { Red: Shade
      Blue: Shade
      Green: Shade
      Yours: Shade
      Hidden: Shade }

/// One thing that takes a colour: the word for it, what it is for, and how to read it out
/// of a palette or put it back in.
///
/// The five live in a list rather than being written out at each place that needs them,
/// because there are four such places - the screen that offers them, the line that changes
/// one, and the two halves of sending a palette down a wire - and a slot that only three of
/// them knew about would be a slot a player could not use.
[<NoComparison; NoEquality>]
type Slot =
    { Says: string
      Draws: string
      /// A scrap of board written in that colour, so the list of choices is also a look at
      /// them. The words are the board's own, so what is shown here is what will be seen.
      Shows: string
      Of: Palette -> Shade
      Set: Shade -> Palette -> Palette }

module Palette =

    let private shade name color = { Name = name; Color = color }

    // The five that are used unless somebody says otherwise. Eight-bit colours rather than
    // the sixteen named ones, and each a good deal brighter than the word for it: a stone
    // drawn in the flat version of its own colour is hard to pick out of a map, and on a
    // dark screen a dark blue is barely there at all.
    let private crimson = shade "crimson" Color.Red1
    let private azure = shade "azure" Color.DodgerBlue1
    let private moss = shade "moss" Color.Green3
    let private gold = shade "gold" Color.Gold1
    let private slate = shade "slate" Color.Grey37

    /// Every colour on offer, in the order they are offered - warm through cold, and the
    /// two quiet ones last. Enough to tell three factions apart on any screen, and few
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
          shade "bone" Color.Silver
          slate ]

    /// The colours the game has always been drawn in, and what it goes back to.
    let standard =
        { Red = crimson
          Blue = azure
          Green = moss
          Yours = gold
          Hidden = slate }

    /// A shade as markup says it - in Spectre's word for the colour rather than this
    /// game's, which Spectre has never heard of.
    ///
    /// Not as a hex triple, though that would also be understood: several of these have
    /// the same triple as one of the sixteen colours a terminal lets its owner re-theme,
    /// and a red the reader has quietly turned brown is not the red that was chosen here.
    let ink (shade: Shade) = shade.Color.ToMarkup()

    let forColor color palette =
        match color with
        | Red -> palette.Red
        | Blue -> palette.Blue
        | Green -> palette.Green

    let private withColor color shade palette =
        match color with
        | Red -> { palette with Red = shade }
        | Blue -> { palette with Blue = shade }
        | Green -> { palette with Green = shade }

    /// A faction's slot, said and shown the way the board says and shows it: whatever the
    /// factions come to be called, this is what a player will be typing.
    let private faction color =
        { Says = (Words.color color).ToLowerInvariant()
          Draws = $"{Words.color color} stones, and the regions {Words.color color} rules"
          Shows = $"{Words.color color}   {Words.glyph color} {Words.glyph color}   >{Words.glyph color}"
          Of = forColor color
          Set = withColor color }

    let slots =
        (StoneColor.all |> List.map faction)
        @ [ { Says = "yours"
              Draws = "your own seat, and whose turn it is"
              Shows = "(you)   ->"
              Of = fun palette -> palette.Yours
              Set = fun shade palette -> { palette with Yours = shade } }
            { Says = "hidden"
              Draws = "what is held back from you, and ground nobody may enter"
              Shows = "dead"
              Of = fun palette -> palette.Hidden
              Set = fun shade palette -> { palette with Hidden = shade } } ]

    let names =
        shades |> List.map (fun shade -> shade.Name) |> String.concat ", "

    let private slotNames =
        slots |> List.map (fun slot -> slot.Says) |> String.concat ", "

    /// Change one colour, in the words a person types. Both halves are answered in their
    /// own terms, because a player who misremembers a colour is told which there are
    /// rather than simply refused.
    let set (slot: string) (colour: string) palette =
        match slots |> List.tryFind (fun candidate -> candidate.Says = slot) with
        | None -> Error $"There is nothing called '{slot}' to colour. There is {slotNames}."
        | Some slot ->
            match shades |> List.tryFind (fun shade -> shade.Name = colour) with
            | None -> Error $"'{colour}' is not a colour I have. There is {names}."
            | Some shade -> Ok(slot.Set shade palette)

    // --- down a wire ---------------------------------------------------------------------
    //
    // A board is drawn at the table, so a player joining one has to say what colours to draw
    // it in. It goes as the words a person would have typed - the same words, read by the
    // same function - so there is no second spelling of a palette to keep in step with this
    // one.

    let write palette =
        slots
        |> List.map (fun slot -> $"{slot.Says}={(slot.Of palette).Name}")
        |> String.concat " "

    /// Read one back, keeping the standard colour for anything the words do not name.
    /// Nothing here can fail: a palette is only how a board is drawn, and a word this table
    /// does not know is worth passing over rather than turning a player away for.
    let read (text: string) =
        text.Split([| ' ' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.fold
            (fun palette pair ->
                match pair.Split '=' with
                | [| slot; colour |] -> set slot colour palette |> Result.defaultValue palette
                | _ -> palette)
            standard
