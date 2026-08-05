namespace TCModel.Console

open TCModel.Domain
open TCModel.App

/// How one person is reading the game: whether the board comes with the writing that
/// explains it, and in what hand it is drawn.
///
/// Neither is part of the game. Both are how it is being read rather than how it is being
/// played, so they stay out of the model and out of the record - and a fresh deal is still
/// read the way the player was reading the last one.
[<NoComparison; NoEquality>]
type Reading = { Notes: bool; View: View }

/// What a line asked of the world, which a value cannot do for itself.
///
/// Writing a file is the only thing in the whole local game that reaches outside, so it is
/// the only thing here. Saying it rather than doing it is what keeps the rules below a
/// fold: a check can ask what a line was going to write without a disk being involved.
[<NoComparison; NoEquality>]
type Errand =
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
    | Keeping of keep: Model * stamp: string * announce: bool
    /// The same, and the player is going.
    | Leaving of keep: Model * stamp: string

/// A game at one keyboard, with whoever is watching it.
///
/// This is `Lobby`'s counterpart and the shorter of the two, because almost everything a
/// table adds to the game is about there being more than one person at it. Here there is
/// one pair of hands: the screen belongs to whoever is to play and changes hands with the
/// turn, nobody can act out of turn because there is nobody else, and walking the game back
/// and forth is allowed because there is no bag being read over anybody's shoulder.
///
/// What it is *for* is the same as `Lobby`'s: to be the whole of the local game's rules as
/// a fold, so that the terminal and the browser can both play it without either of them
/// keeping a second copy of what a typed line means.
///
/// Watchers, plural, and the reading is per watcher. One is the usual case - a person at a
/// keyboard - but a game served to a browser can have the same hot seat open in two places
/// at once, and they are entitled to read it differently.
[<NoComparison; NoEquality>]
type Solo =
    private
        { Model: Model
          Stamp: string
          Watchers: (string * Reading) list }

module Solo =

    /// A game with nobody looking at it yet. The stamp names the file this game's record
    /// will be kept in; it is taken outside and handed in, because it comes from the clock
    /// and a table that read the clock for itself could not be folded twice to the same
    /// answer.
    let opened stamp model =
        { Model = model
          Stamp = stamp
          Watchers = [] }

    let model solo = solo.Model

    let stamp solo = solo.Stamp

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
    let private boardFor solo (reading: Reading) =
        let beholder = Game.active (Model.game solo.Model)
        reading.View.Board reading.Notes beholder solo.Model

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

    /// Somebody starts watching, reading it the way they say. A console already watching is
    /// not seated again; it simply says how it would rather read, which is what a browser
    /// reloading its page amounts to.
    let watching console (reading: Reading) solo =
        let solo =
            match readingAt console solo with
            | Some _ -> withReading console reading solo
            | None ->
                { solo with
                    Watchers = solo.Watchers @ [ (console, reading) ] }

        solo, [ screenFor solo (console, reading) ]

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

        let beholder = Game.active (Model.game solo.Model)

        let told text =
            solo, just console (Told(reading.View.Says text)), Carrying

        /// Something about this console has changed rather than something about the game,
        /// so this one is drawn again and nobody else hears about it.
        let mine reading =
            let solo = withReading console reading solo
            solo, [ screenFor solo (console, reading) ], Carrying

        /// The game has moved, so everybody looking at it is drawn again.
        let moved solo errand = solo, drawAll solo, errand

        match Parse.line typed with
        | Error problem -> told problem
        | Ok Parse.Nothing -> solo, [ screenFor solo (console, reading) ], Carrying
        | Ok Parse.Help -> solo, just console (Told reading.View.Rules), Carrying
        | Ok Parse.Recount -> solo, just console (Told(reading.View.History beholder solo.Model)), Carrying
        | Ok(Parse.Explain regionId) -> solo, just console (Told(reading.View.Ruling regionId solo.Model)), Carrying
        | Ok(Parse.Notes wanted) ->
            mine
                { reading with
                    Notes = wanted |> Option.defaultValue (not reading.Notes) }
        | Ok(Parse.Looking name) ->
            // In whatever colours this console is already reading in, and only among the
            // views it could show: a page cannot be drawn on a terminal.
            match View.byName reading.View.Shown reading.View.Palette name with
            | Ok view -> mine { reading with View = view }
            | Error problem -> told problem
        | Ok Parse.Keep -> solo, [], Keeping(solo.Model, solo.Stamp, true)
        | Ok Parse.Leave ->
            // A game still in play is resigned first, so the record says how it ended
            // rather than simply stopping.
            let ended =
                if Model.isOver solo.Model then solo.Model else Update.update (Make Resign) solo.Model

            let solo = { solo with Model = ended }
            solo, drawAll solo, Leaving(ended, solo.Stamp)
        | Ok(Parse.Send(Restart _ as msg)) ->
            // The old game's record is closed and kept before the table is cleared, which
            // is why the errand carries that game rather than this one.
            let closing = solo.Model

            moved
                { solo with
                    Model = Update.update msg solo.Model
                    Stamp = fresh }
                (Keeping(closing, solo.Stamp, false))
        | Ok(Parse.Send msg) ->
            let next = Update.update msg solo.Model

            // A game that has just ended writes itself down without being asked.
            let errand =
                if Model.isOver next && not (Model.isOver solo.Model) then
                    Keeping(next, solo.Stamp, false)
                else
                    Carrying

            moved { solo with Model = next } errand
