namespace Prototyping.Table

open System
open Prototyping.Engine

[<NoComparison; NoEquality>]
type Pulse<'Move, 'State> =
    {
        Every: 'State -> TimeSpan

        Beat: 'Move

        /// How many times to draw the board between one beat and the next. A beat is a move; a
        /// frame is not. Nothing a frame draws reaches the rules, the timeline or the record, and
        /// all that differs between two frames is `Margins.Phase` - so a frame cannot change a
        /// game, only how far through a change it is caught. Nought for a game with nothing moving
        /// between its beats, which is every game of turns and Snake as well.
        Frames: 'State -> int

        Pressed: ConsoleKeyInfo -> string option
    }

module Pulse =

    /// The next moment worth waking for: the next frame, or the beat itself if none is left before
    /// it. Frames are laid evenly across the beat rather than counted off one after another, so
    /// what is asked for is the next boundary after *now* - a frame that arrived late does not push
    /// the ones behind it late as well.
    let waking frames (since: DateTime) (due: DateTime) (now: DateTime) =
        if frames <= 1 || due <= since then
            due
        else
            let step = (due - since) / float frames
            min due (since + step * (floor ((now - since) / step) + 1.0))

    /// How far a drawing made now is between one beat and the next: nought at the beat, up towards
    /// but never reaching one - which is what keeps the last frame of a beat the last picture of
    /// this turn rather than the first of the next.
    let phase (since: DateTime) (due: DateTime) (now: DateTime) =
        if due <= since then 0.0 else (now - since) / (due - since) |> max 0.0 |> min 0.999

[<NoComparison; NoEquality>]
type Seated<'Move, 'State> =
    { Skill: string
      Plays: Machine<'Move, 'State> }

/// A section of the main menu that belongs to the game rather than to the table.
///
/// Everything else a game offers happens at a board, between players, inside a game. This is the
/// one thing that does not: a bench a player works at with nobody else connected and nothing dealt,
/// whose results are theirs to keep between games. The table draws the screen and carries the
/// lines; **whatever the section remembers, the game remembers** - which is why there is no state
/// in this type. A `Playable` carrying a type parameter for it would put one on every game that has
/// no bench at all, which is seven of the eight.
///
/// `Screen` is a function rather than a value because the screen is drawn again after every line,
/// and a bench that could not show what the last line did to it would be a bench nobody could work
/// at.
[<NoComparison; NoEquality>]
type Aside =
    {
        /// What a player types to open it, and what the menu row sends. One lower-case word, and
        /// not one the menu already answers to.
        Word: string

        Says: string
        Does: string

        Screen: unit -> Keys.Screen

        /// A line typed at the bench. `Ok` says what to print under the screen, which may be
        /// nothing; `Error` is a complaint, in words. The lines the menu itself answers to - back,
        /// quit and the rest - are read before this is reached.
        Read: string -> Result<string, string>
    }

[<NoComparison; NoEquality>]
type Playable<'Move, 'State, 'Notice> =
    {
        Rules: Rules<'Move, 'State, 'Notice>

        Name: string
        Title: string
        Blurb: string

        Fewest: int
        Most: int

        Read: string -> Result<Command<'Move>, string>

        Write: Msg<'Move> -> string

        Seat: PlayerId -> string

        Says: 'Notice -> string

        SeenBy: PlayerId -> 'Notice -> string

        /// What the board is sounding, from where it stands. Read off the state after a move rather
        /// than out of the notices, which is what makes a replayed table sound like a played one
        /// and lets a game say it once for every table rather than once per endpoint.
        Rings: 'State -> Sound list

        Resign: 'Move option

        Faults: string list

        Slots: Slot list

        Skills: (string * string) list

        Seating: uint64 -> string option list -> 'State -> (PlayerId * Seated<'Move, 'State>) list

        Pulse: Pulse<'Move, 'State> option

        /// A section of the main menu the game owns, if it wants one. `None` for a game whose whole
        /// offer is a board.
        Aside: Aside option

        /// Rows a player may steer at the board, where this game has any for where it stands.
        ///
        /// A row stands for a line the game already reads - exactly as `Pulse.Pressed` does for a
        /// game on a clock - so nothing can be picked that could not have been typed, and a game
        /// driven by the arrow keys writes the same record as one driven by hand. Enter with nothing
        /// typed takes the marked row; Enter with a line underway sends the line, so the prompt is
        /// never taken away from anybody.
        ///
        /// The board as this table drew it is handed in, with the seat it was drawn for, so a game
        /// may put it above the rows, replace it, or ignore it - the table has already chosen the
        /// view and the margins, and this does not second-guess either. `None` means what it always
        /// meant: draw the board and read a line.
        Steering: string -> PlayerId -> Model<'Move, 'State, 'Notice> -> Keys.Screen option

        Page: Shell

        Views: Palette -> View<'Move, 'State, 'Notice> list
    }

module Playable =

    let seats game = game.Fewest, game.Most

    let seatsOf game state =
        [ for place in 1 .. game.Rules.Seats state -> Seat.at place ]

    let plays state seated =
        Machines.playing state seated.Plays
        |> Option.map (fun (move, next) -> move, { seated with Plays = next })

    let roster game rivals =
        match rivals with
        | [] -> None
        | rivals ->
            rivals
            |> List.map (fun (seat, seated) -> $"{game.Seat seat} ({seated.Skill})")
            |> String.concat ", "
            |> sprintf "Played by the machine: %s."
            |> Some

    let standard game = Palette.standard game.Slots

    let offered shown palette game =
        game.Views palette |> List.filter (fun view -> view.Shown = shown)

    let namesFor shown game =
        offered shown (standard game) game
        |> List.map (fun view -> view.Name)
        |> String.concat ", "

    let byName shown palette game (name: string) =
        let wanted = name.ToLowerInvariant()

        match offered shown palette game |> List.tryFind (fun view -> view.Name = wanted) with
        | Some view -> Ok view
        | None -> Error $"'{name}' is not a way of showing the game here. There is {namesFor shown game}."

    let plainest shown palette game = offered shown palette game |> List.head

    let opening shown settings game =
        let palette, problems = Settings.palette game.Name game.Slots settings

        let view =
            match Settings.drawn game.Name settings with
            | Some name ->
                byName shown palette game name
                |> Result.defaultValue (plainest shown palette game)
            | None -> plainest shown palette game

        view, problems

    let recoloured palette game (view: View<_, _, _>) =
        game.Views palette
        |> List.tryFind (fun other -> other.Name = view.Name)
        |> Option.defaultValue (plainest view.Shown palette game)

    let read game typed =
        match Commands.read (seats game) game.Resign typed with
        | Some answer -> answer
        | None -> game.Read typed

    let told game = Told.inWords game.Says game.Write

    let toldSeenBy game seat =
        Told.inWords (game.SeenBy seat) game.Write
