namespace TCModel.Table

open System
open TCModel.Engine

[<NoComparison; NoEquality>]
type Pulse<'Move, 'State> =
    {
        Every: 'State -> TimeSpan

        Beat: 'Move

        /// How many times to draw the board between one beat and the next.
        ///
        /// A beat is a move; a frame is not. Nothing a frame draws reaches the rules, the timeline
        /// or the record, and the only thing that differs between one frame and the next is
        /// `Margins.Phase` - so a frame cannot change a game, only how far through a change it is
        /// caught. Nought is a game with nothing moving between its beats, which is every game of
        /// turns and Snake as well: a snake is on one square or the next and never between them.
        Frames: 'State -> int

        Pressed: ConsoleKeyInfo -> string option
    }

module Pulse =

    /// The next moment worth waking for: the next frame, or the beat itself if there is no frame
    /// left before it.
    ///
    /// Frames are laid evenly across the beat rather than counted off one after another, so a
    /// frame that arrived late does not push the ones behind it late as well - what is asked for
    /// is the next boundary after *now*, whichever one that is. A game that asks for no frames is
    /// only ever woken by the beat, which is the loop every clocked game had before there were
    /// any frames to ask for.
    let waking frames (since: DateTime) (due: DateTime) (now: DateTime) =
        if frames <= 1 || due <= since then
            due
        else
            let step = (due - since) / float frames
            min due (since + step * (floor ((now - since) / step) + 1.0))

    /// How far a drawing made now is between one beat and the next: nought at the beat, and up
    /// towards - but never reaching - one. Never reaching it is what keeps the last frame of a
    /// beat the last picture of the turn rather than the first picture of the next one.
    let phase (since: DateTime) (due: DateTime) (now: DateTime) =
        if due <= since then 0.0 else (now - since) / (due - since) |> max 0.0 |> min 0.999

[<NoComparison; NoEquality>]
type Seated<'Move, 'State> =
    { Skill: string
      Plays: Machine<'Move, 'State> }

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

        /// What the board is sounding, from where it stands.
        ///
        /// Read off the state after a move rather than out of the notices, which is what makes it
        /// the same at a replayed table as at a played one, and lets a game say it once for every
        /// table there is rather than once per endpoint. Empty at a game nobody needs to hear.
        Rings: 'State -> Sound list

        Resign: 'Move option

        Faults: string list

        Slots: Slot list

        Skills: (string * string) list

        Seating: uint64 -> string option list -> 'State -> (PlayerId * Seated<'Move, 'State>) list

        Pulse: Pulse<'Move, 'State> option

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
