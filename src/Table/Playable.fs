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

        /// Whether, where the game stands, the clock frees every seat to speak. True where the
        /// beat is what moves the game and any console's line is a steer - Snake on the clock,
        /// Life, Cascade - and false while a clocked game is still taking turns, as Warband does
        /// through its muster. A table over a network used to hold the turn only at a game with
        /// no pulse, so a hosted Warband let either console muster into the other squad and
        /// resign for it. Where this says false the lobby takes a move from whoever is to play
        /// and from nobody else, as it does at a game of turns.
        Free: 'State -> bool
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

        /// What is wrong with the game as described, if anything: a board that does not add up, a
        /// slot named twice, a card with nothing printed on it. The table refuses to open a game
        /// that has any and says them, so a mistake in a description is met at the menu rather
        /// than at the first move to trip on it. Empty for a game that has looked itself over and
        /// found nothing.
        Faults: string list

        /// What the game draws in colour, each under a key the table can be asked to colour -
        /// 'blue teal' at the Video page - with a standard shade and the words the page says it by.
        Slots: Slot list

        /// The ways the machine can play this game, by name and in a sentence each, for the menu
        /// and the command line to offer. Empty for a game only people can play.
        Skills: (string * string) list

        /// The machines at the table. Given the seed the game was dealt from, which skill each seat
        /// was asked to be played by - `None` for a person - and the state as dealt, answers which
        /// seats a machine plays and what plays them. `Playable.seating` builds it from the parts a
        /// game with machines already has.
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
        /// The board as this table drew it is handed in, with the margins and the seat it was drawn
        /// for - the same three the board itself was drawn from, so the rows and the board are
        /// answers about one screen rather than two. `None` means what it always meant: draw the
        /// board and read a line.
        Steering: string -> Margins -> PlayerId -> Model<'Move, 'State, 'Notice> -> Keys.Screen option

        /// What a browser is sent besides the board: the page's title, the game's own stylesheet,
        /// the keys it binds, and the placeholder in the prompt.
        Page: Shell

        /// Every way this game can be drawn, in the palette given - at least one for a terminal
        /// and one for a browser, which `Conforms` holds every game to. `Readers.views` makes the
        /// three from a game's scenes.
        Views: Palette -> View<'Move, 'State, 'Notice> list
    }

module Playable =

    let seats game = game.Fewest, game.Most

    let seatsOf game state =
        [ for place in 1 .. game.Rules.Seats state -> Seat.at place ]

    let plays state seated =
        Machines.playing state seated.Plays
        |> Option.map (fun (move, next) -> move, { seated with Plays = next })

    /// A game's `Seating`, from what a game with machines already has: its skills by name, how it
    /// seats them, what each is called and what it plays. Six games used to write the dozen lines
    /// between those and the seam for themselves, identically.
    let seating
        (byName: string -> Result<'Skill, string>)
        (seated: uint64 -> 'Skill option list -> 'State -> (PlayerId * 'Rival) list)
        (nameOf: 'Rival -> string)
        (plays: 'State -> 'Rival -> ('Move * 'Rival) option)
        =
        fun seed (sitting: string option list) state ->
            seated seed (sitting |> List.map (Option.bind (byName >> Result.toOption))) state
            |> List.map (fun (seat, rival) ->
                seat,
                { Skill = nameOf rival
                  Plays = Machines.choosing plays rival })

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

    /// The first view of a kind, which is the plainest, since a game lists its plainest first.
    /// `List.head` is safe here where `byName` beside it answers with a `Result`: `Conforms` holds
    /// every game to a view of each kind, so a game with none never reaches a table.
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
