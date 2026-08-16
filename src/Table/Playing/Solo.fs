namespace TCModel.Table

open TCModel.Engine

/// How one person is reading the game: how much of the writing round the board comes with
/// it, and in what hand it is drawn.
///
/// Neither is part of the game. Both are how it is being read rather than how it is being
/// played, so they stay out of the model and out of the record - and a fresh deal is still
/// read the way the player was reading the last one.
[<NoComparison; NoEquality>]
type Reading<'Move, 'State, 'Notice> =
    { Margins: Margins
      View: View<'Move, 'State, 'Notice> }

/// What a line asked of the world, which a value cannot do for itself.
///
/// Writing a file is the only thing in the whole local game that reaches outside, so it is
/// the only thing here. Saying it rather than doing it is what keeps the rules below a
/// fold: a check can ask what a line was going to write without a disk being involved.
[<NoComparison; NoEquality>]
type Errand<'Move, 'State, 'Notice> =
    | Carrying
    /// Write this record out, under this stamp.
    ///
    /// The game is carried rather than read off the table afterwards, because the one time
    /// they differ is the one that matters: a restart writes the game that has just been
    /// cleared away, not the one that replaced it.
    ///
    /// `Announce` is whether the player asked for this and should be told where it went. A
    /// game that writes itself down on ending, or on being swept off the table, does so
    /// without saying anything.
    | Keeping of keep: Model<'Move, 'State, 'Notice> * stamp: string * announce: bool
    /// The same, and the player is going.
    ///
    /// Nothing to write where the game has not moved since this table took it up. Taking a
    /// record up and putting it straight back down is reading, and reading a game is not a
    /// reason to write over the file it came out of - which matters most for the records that
    /// are oldest, since those are the ones written in a form this would quietly restate.
    | Leaving of keep: Model<'Move, 'State, 'Notice> option * stamp: string

/// A game at one keyboard, with whoever is watching it.
///
/// This is `Lobby`'s counterpart and the shorter of the two, because almost everything a
/// table adds to the game is about there being more than one person at it. Here there is
/// one pair of hands: the screen belongs to whoever is to play and changes hands with the
/// turn, nobody can act out of turn because there is nobody else, and walking the game back
/// and forth is allowed because there is no hand being read over anybody's shoulder.
///
/// What it is *for* is to be the whole of the local game's rules as a fold, so that the
/// terminal and the browser can both play it without either of them keeping a second copy of
/// what a typed line means.
///
/// Watchers, plural, and the reading is per watcher. One is the usual case - a person at a
/// keyboard - but a game served to a browser can have the same hot seat open in two places
/// at once, and they are entitled to read it differently.
///
/// Some of the seats may be played by the program rather than by anybody in the room. That
/// changes nothing about the game and nothing about the screen: a machine's move goes
/// through the same `update` as everybody else's, lands in the record like everybody else's,
/// and is drawn to everybody watching like everybody else's. All this table adds is that
/// after a person has spoken, the machines answer before the prompt comes back.
///
/// The game it is playing is carried rather than known. That is the whole of what makes this
/// file one table rather than one game's table.
[<NoComparison; NoEquality>]
type Solo<'Move, 'State, 'Notice> =
    private
        {
            Game: Playable<'Move, 'State, 'Notice>
            Model: Model<'Move, 'State, 'Notice>
            Stamp: string
            /// How far the game had got when this table took it up, which is nought for a fresh
            /// deal and wherever it was left for a game taken up again. What it is for is to
            /// tell a sitting that added something from one that only looked.
            Opened: int
            Rivals: (PlayerId * Seated<'Move, 'State>) list
            Watchers: (string * Reading<'Move, 'State, 'Notice>) list
        }

module Solo =

    /// A game with nobody looking at it yet and nobody but people at it. The stamp names
    /// the file this game's record will be kept in; it is taken outside and handed in,
    /// because it comes from the clock and a table that read the clock for itself could not
    /// be folded twice to the same answer.
    let opened game stamp model =
        { Game = game
          Model = model
          Stamp = stamp
          Opened = Timeline.movesMade model.Timeline
          Rivals = []
          Watchers = [] }

    let model solo = solo.Model

    let game solo = solo.Game

    let stamp solo = solo.Stamp

    let private rules solo = solo.Game.Rules

    let private standing solo = Model.state solo.Model

    /// Whether this sitting has put anything into the game, as against having only read it
    /// back. A record is written out on the way past whether anybody asked or not, and this
    /// is what keeps that from meaning that reading one changes it.
    let private added solo =
        Timeline.movesMade solo.Model.Timeline <> solo.Opened

    let isOver solo = (rules solo).Over(standing solo)

    /// Whose turn it is. A seat rather than a player, because that is all a table needs to
    /// know and all any game agrees about.
    let private active solo = (rules solo).Active(standing solo)

    // --- the seats nobody is sitting in ------------------------------------------------

    /// Let the machines take their turns, which the engine does for every table there is.
    /// All this end has to do is hand them the game and take back the one they left.
    let private answering solo =
        let model, rivals =
            Machines.answering (rules solo) Playable.plays solo.Rivals solo.Model

        { solo with
            Model = model
            Rivals = rivals }

    /// The same rule read backwards, for walking the game about.
    ///
    /// A move taken back takes the machines' answers to it back with it, and a move made
    /// again brings them along again. Anything else would be no way of taking a move back at
    /// all: one `undo` and the machine would simply play it for you again before you had
    /// looked at the board.
    ///
    /// What stops it is the history rather than the position, for the reason the engine's own
    /// loop gives: a table that compared two states would need games to be comparable, and
    /// nothing about a game says it has to be.
    let rec private walking msg solo =
        if not (Machines.holds (rules solo) solo.Rivals solo.Model) then
            solo
        else
            let next =
                { solo with
                    Model = Update.update (rules solo) msg solo.Model }

            if Timeline.movesMade next.Model.Timeline = Timeline.movesMade solo.Model.Timeline then
                next
            else
                walking msg next

    /// Seat the machines, and let them play up to the first seat a person has to fill.
    ///
    /// Handed in already seated rather than named here, because which seat a machine plays
    /// and what its generator started from are facts about the game that was dealt, and this
    /// table is only where they sit.
    ///
    /// Nobody is watching yet, so there is nothing to draw and nothing comes back but the
    /// record - which is only ever asked for by a table where every seat is a machine's,
    /// there being no seat for the opening to stop at, so it plays the whole game here.
    let against rivals solo =
        let played = answering { solo with Rivals = rivals }

        played, (if isOver played && not (isOver solo) then Keeping(played.Model, played.Stamp, false) else Carrying)

    let private readingAt console solo =
        solo.Watchers |> List.tryFind (fst >> (=) console) |> Option.map snd

    let private withReading console reading solo =
        { solo with
            Watchers =
                solo.Watchers
                |> List.map (fun (other, was) -> if other = console then other, reading else other, was) }

    // --- what a console is shown ------------------------------------------------------

    /// The board as one watcher reads it.
    ///
    /// One keyboard, so the screen belongs to whoever is to play and the beholder changes
    /// hands with the turn. Over a network each console has a seat of its own and this is
    /// the line that differs.
    let private boardFor solo (reading: Reading<_, _, _>) =
        reading.View.Board reading.Margins (active solo) solo.Model

    let private screenFor solo (console, reading) =
        { To = console
          Say = Screen(boardFor solo reading) }

    /// The board again, with nothing having happened to ask for it.
    ///
    /// A prompt draws this at the top of every turn. A terminal cannot patch a screen in
    /// place the way a page can, so it redraws instead - which is why this is here and why
    /// the browser has no use for it.
    let board console solo =
        readingAt console solo |> Option.map (boardFor solo)

    /// Draw everybody watching. Anything that moves the game ends this way, because two
    /// people looking at one hot seat are looking at the same game.
    let private drawAll solo =
        solo.Watchers |> List.map (screenFor solo)

    /// Everybody watching but the one who just spoke, told the game has come round to them.
    ///
    /// The screen here belongs to whoever is to play, so anybody watching who did not type
    /// the line has had the turn handed to them while they were looking elsewhere - which is
    /// the whole of what a nudge means. At the ordinary table, one person at one keyboard,
    /// this list is empty and nothing rings: you cannot be interrupted by yourself.
    ///
    /// A game that is over comes round to nobody.
    let private nudging console solo =
        if isOver solo then
            []
        else
            solo.Watchers
            |> List.map fst
            |> List.filter ((<>) console)
            |> List.map (fun other -> { To = other; Say = Nudged })

    let private just console said = [ { To = console; Say = said } ]

    /// Something to tell one watcher, in the words their own view would put it in.
    ///
    /// Here because the table is the only thing that knows how anybody is reading. What has
    /// to be said after the fact - where a record went, say, which nothing in here can know
    /// - goes out through this rather than as bare text, because to a terminal bare text is
    /// a line and to a page it is markup that is not markup, and the page would quietly
    /// draw nothing at all.
    let saying console text solo =
        readingAt console solo
        |> Option.map (fun reading ->
            { To = console
              Say = Told(reading.View.Says text) })
        |> Option.toList

    // --- coming and going ---------------------------------------------------------------

    /// Who at this table is the machine, said to somebody as they sit down to watch - in the
    /// words their own view speaks, like everything else said after the fact. The sentence
    /// itself is `Playable`'s, because the networked table says the same one.
    let private roster (reading: Reading<_, _, _>) solo =
        Playable.roster solo.Game solo.Rivals
        |> Option.map (reading.View.Says >> Told)
        |> Option.toList

    /// Somebody starts watching, reading it the way they say. A console already watching is
    /// not seated again; it simply says how it would rather read, which is what a browser
    /// reloading its page amounts to.
    let watching console (reading: Reading<_, _, _>) solo =
        let solo =
            match readingAt console solo with
            | Some _ -> withReading console reading solo
            | None ->
                { solo with
                    Watchers = solo.Watchers @ [ (console, reading) ] }

        solo,
        [ screenFor solo (console, reading) ]
        @ (roster reading solo |> List.map (fun said -> { To = console; Say = said }))

    /// Somebody stops. Nothing about the game changes - there are no seats to keep warm
    /// here, and the person who was playing every side has simply put it down.
    let gone console solo =
        { solo with
            Watchers = solo.Watchers |> List.filter (fst >> (<>) console) },
        []

    // --- a typed line ---------------------------------------------------------------------

    /// A line typed at one console, and what everybody watching is shown as a result.
    ///
    /// `fresh` is a stamp for a game this line might deal; it is handed in rather than taken
    /// here for the same reason `opened`'s is. Unused unless the line restarts the game.
    let said fresh console (typed: string) solo =
        match readingAt console solo with
        | None -> solo, just console (TurnedAway "You are not watching this game."), Carrying
        | Some reading ->

        let beholder = active solo

        let told text =
            solo, just console (Told(reading.View.Says text)), Carrying

        /// Something about this console has changed rather than something about the game,
        /// so this one is drawn again and nobody else hears about it.
        let mine reading =
            let solo = withReading console reading solo
            solo, [ screenFor solo (console, reading) ], Carrying

        /// The game has moved, so everybody looking at it is drawn again - and everybody
        /// but the one who moved it is nudged, the board having reached them on its own.
        let moved solo errand =
            solo, drawAll solo @ nudging console solo, errand

        match Playable.read solo.Game typed with
        | Error problem -> told problem
        | Ok Nothing -> solo, [ screenFor solo (console, reading) ], Carrying
        | Ok Help -> solo, just console (Told reading.View.Rules), Carrying
        | Ok Recount -> solo, just console (Told(reading.View.History beholder solo.Model)), Carrying
        | Ok(Asking question) -> solo, just console (Told(reading.View.Answer beholder question solo.Model)), Carrying
        | Ok(Notes wanted) ->
            mine
                { reading with
                    Margins =
                        { reading.Margins with
                            Notes = wanted |> Option.defaultValue (not reading.Margins.Notes) } }
        | Ok(Listing wanted) ->
            mine
                { reading with
                    Margins =
                        { reading.Margins with
                            Commands = wanted |> Option.defaultValue (not reading.Margins.Commands) } }
        | Ok(Looking name) ->
            // In whatever colours this console is already reading in, and only among the
            // views it could show: a page cannot be drawn on a terminal.
            match Playable.byName reading.View.Shown reading.View.Palette solo.Game name with
            | Ok view -> mine { reading with View = view }
            | Error problem -> told problem
        | Ok Keep -> solo, [], Keeping(solo.Model, solo.Stamp, true)
        // Written down as it stands and left standing. A game still in play used to be
        // resigned on the way out, so that its record said how it ended rather than simply
        // stopping - which was the right answer while a record was a finished thing to read
        // back. It is not, now: a record is a game to take up again, and a game conceded on
        // the way out of the room is a game nobody can come back to. Conceding is `resign`,
        // which is a move a player makes on purpose and always was.
        | Ok Leave -> solo, drawAll solo, Leaving((if added solo then Some solo.Model else None), solo.Stamp)
        | Ok(Send(Restart _ as msg)) ->
            // The old game's record is closed and kept before the table is cleared, which
            // is why the errand carries that game rather than this one. The machines stay
            // where they were sitting - they are at the table, not in the game - and answer
            // the fresh deal the same way they would answer anything else.
            let closing = solo.Model

            moved
                (answering
                    { solo with
                        Model = Update.update (rules solo) msg solo.Model
                        Stamp = fresh
                        Opened = 0 })
                // Nothing to keep where the game being swept away is the one that was taken
                // up a moment ago and never played.
                (if added solo then Keeping(closing, solo.Stamp, false) else Carrying)
        | Ok(Send((Undo | Redo) as msg)) ->
            moved
                (walking
                    msg
                    { solo with
                        Model = Update.update (rules solo) msg solo.Model })
                Carrying
        | Ok(Send msg) ->
            let next =
                answering
                    { solo with
                        Model = Update.update (rules solo) msg solo.Model }

            // A game that has just ended writes itself down without being asked - whoever
            // ended it, which by now may well have been the machine.
            let errand =
                if isOver next && not (isOver solo) then Keeping(next.Model, solo.Stamp, false) else Carrying

            moved next errand
