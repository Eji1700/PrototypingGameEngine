namespace TCModel.Table

open TCModel.Engine

/// A machine at a seat: how it plays, and the word for how well it plays.
///
/// The word is carried because a table has to be able to say who the machine is as somebody
/// sits down - a game where you cannot tell who you are playing has a secret in it that is
/// no part of the game. The `Machine` itself is a closure and could not be asked.
[<NoComparison; NoEquality>]
type Seated<'Move, 'State> =
    { Skill: string
      Plays: Machine<'Move, 'State> }

/// Everything a game has to say about itself to be read and played here.
///
/// `Rules` is the engine's seam and answers how a game is *played*. This is the other one,
/// and it answers how a game is *read*: what it is called, how many may sit down, what a
/// typed line means, how a move is written into a record, what it colours, who plays it when
/// nobody is there, and the ways it can be drawn.
///
/// The two are deliberately separate. A game could be folded, replayed and checked with only
/// the first of them - and every test that does not draw a board does exactly that.
///
/// Adding a game means filling this in once. Nothing above it is touched: the timeline, the
/// record, the seats, the tokens, the wire, the menu, the command line and all three ways of
/// drawing a board are already written and already generic.
[<NoComparison; NoEquality>]
type Playable<'Move, 'State, 'Notice> =
    {
        Rules: Rules<'Move, 'State, 'Notice>

        /// What it is called on a command line and in a record - one word, lower case.
        Name: string
        /// What it is called to a person, and a line about what it is.
        Title: string
        Blurb: string

        /// How many may sit down. The same two numbers turn away a table too big at the
        /// menu, on the command line, and in a record that asks for one.
        Fewest: int
        Most: int

        /// A typed line that was not one of the words every game knows. Only this game's own
        /// moves reach here - `Commands` has already taken `undo`, `save`, `view` and the
        /// rest - so a game reads what it invented and nothing else.
        Read: string -> Result<Command<'Move>, string>

        /// A move as the line a person would have typed for it. This is what a record is
        /// made of, which is why there is no second language in one: what `Read` takes,
        /// this writes.
        Write: Msg<'Move> -> string

        /// What a seat is called - "Player 1" at one game, "X" at another. Used wherever a
        /// seat is named outside a board: in a record, in the roster of who the machine is.
        Seat: PlayerId -> string

        /// What the game itself said. Only the game's own notices reach here - what the
        /// engine says about undo, redo and a line nobody could read is said once, below,
        /// in words that suit any game.
        Says: 'Notice -> string

        /// The same, as much of it as one seat may know. A game where everything is on the
        /// table answers this the same for everybody; one with something hidden does not.
        SeenBy: PlayerId -> 'Notice -> string

        /// What `resign` plays, for a game that can be walked away from mid-play.
        Resign: 'Move option

        /// What this game says is wrong with itself, before anybody sits down.
        ///
        /// A game built out of data - a map, a deck, a board laid out by hand - can be built
        /// wrong, and the only thing that can notice is the game: nothing above here knows
        /// what a well-formed one would look like. Empty at a game of nine squares; not
        /// empty at one whose map has a region bordering itself.
        Faults: string list

        /// What takes a colour, beyond the seat belonging to whoever is reading.
        Slots: Slot list

        /// The machines on offer, worst to best: the word a person types for each, and what
        /// it plays like. What one *is* is below.
        Skills: (string * string) list

        /// Seat the machines named - one entry per seat, in dealing order, naming the skill
        /// or nobody - each with a generator of its own drawn from the deal, so that the same
        /// table dealt twice plays the same twice.
        Seating: uint64 -> string option list -> 'State -> (PlayerId * Seated<'Move, 'State>) list

        /// What this game brings to a browser: a name for the tab, its own rules of drawing,
        /// and what the empty prompt suggests. The page itself is not the game's.
        Page: Shell

        /// Every way this game can be drawn, in the order they are offered.
        Views: Palette -> View<'Move, 'State, 'Notice> list
    }

module Playable =

    let seats game = game.Fewest, game.Most

    /// Every seat at a dealt game, in dealing order.
    ///
    /// The rules say how many there are and `Seat` says they count from one, which between
    /// them is the whole answer - so a table can list the seats of a game it knows nothing
    /// else about, which is what the networked one needs to hand them out.
    let seatsOf game state =
        [ for place in 1 .. game.Rules.Seats state -> Seat.at place ]

    /// One machine's turn, in the shape the engine asks for it: the seat keeps its name for
    /// the roster while the machine inside it moves on.
    ///
    /// Here rather than at either table, because both tables seat machines and the shape is
    /// the engine's rather than the table's.
    let plays state seated =
        Machines.playing state seated.Plays
        |> Option.map (fun (move, next) -> move, { seated with Plays = next })

    /// Who at a table is the machine, in one sentence, for saying to somebody as they sit
    /// down to it - and nothing at all at a table of nothing but people.
    ///
    /// Nothing on the board says so and nothing should: a machine's pieces look like
    /// anybody's, and they are. But a game where you cannot tell who you are playing has a
    /// secret in it that is no part of the game, so it is said once, plainly, to whoever
    /// arrives.
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

    /// Those a given reader can actually show. Everything that offers a choice asks for
    /// this rather than for all of them, so nobody is offered a screen they could not read.
    let offered shown palette game =
        game.Views palette |> List.filter (fun view -> view.Shown = shown)

    /// What there is to choose from. Which views there are does not depend on what colour
    /// they are drawn in, so this asks for the list in the standard ones and reads off the
    /// names.
    let namesFor shown game =
        offered shown (standard game) game
        |> List.map (fun view -> view.Name)
        |> String.concat ", "

    /// Find a view by name, among those the reader asking could show.
    ///
    /// The reader is part of the question rather than something checked afterwards: a
    /// console asking for `html` is not asking for something that exists elsewhere, it is
    /// asking for something it cannot read, and the answer that helps is the list of what
    /// it can.
    let byName shown palette game (name: string) =
        let wanted = name.ToLowerInvariant()

        match offered shown palette game |> List.tryFind (fun view -> view.Name = wanted) with
        | Some view -> Ok view
        | None -> Error $"'{name}' is not a way of showing the game here. There is {namesFor shown game}."

    /// The first way of drawing it that this reader can show - what somebody who has said
    /// nothing about how they read gets.
    let plainest shown palette game = offered shown palette game |> List.head

    /// The view a game opens in, as the settings were left: drawn the way that was kept, in
    /// the colours that were kept for this game, and whatever the game itself says wherever
    /// they say nothing. Anything the settings got wrong comes back beside it, to be said on
    /// the first screen somebody is shown.
    ///
    /// Here rather than at each way in, and that is the point of it: the menu and the command
    /// line both have to open a game the way it was left, and two answers to that question
    /// would be two defaults - one for people who type and one for people who pick.
    ///
    /// A view the settings name that this game does not have falls back to the plainest rather
    /// than refusing, which is the right way round for a default. The name may have been kept
    /// at a different game with different ways of drawing, and a game you cannot open because
    /// of how you once read another one is not a setting but a trap.
    let opening shown settings game =
        let palette, problems = Settings.palette game.Name game.Slots settings

        let view =
            match Settings.drawn game.Name settings with
            | Some name -> byName shown palette game name |> Result.defaultValue (plainest shown palette game)
            | None -> plainest shown palette game

        view, problems

    /// The same view, built again in different colours. A view is its endpoints, and those
    /// have the old palette baked into them, so changing colours means asking for the view
    /// afresh rather than putting a new palette on the one in hand.
    let recoloured palette game (view: View<_, _, _>) =
        game.Views palette
        |> List.tryFind (fun other -> other.Name = view.Name)
        |> Option.defaultValue (plainest view.Shown palette game)

    /// A whole typed line: the words every game knows first, and whatever is left over to
    /// the game itself. In that order, so no game can quietly redefine `quit`.
    let read game typed =
        match Commands.read (seats game) game.Resign typed with
        | Some answer -> answer
        | None -> game.Read typed

    /// Anything the table has been told, in words: this game's half through the game, the
    /// engine's half in the words the engine puts it in. Both halves are `Told.inWords`,
    /// which is where those sentences live so that a game's own renderer can reach them too.
    let told game = Told.inWords game.Says game.Write

    /// The same, as much of it as one seat may know. Only what the game said differs by who
    /// is reading: nothing the engine says is a secret from anybody.
    let toldSeenBy game seat =
        Told.inWords (game.SeenBy seat) game.Write
