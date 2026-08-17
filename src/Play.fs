module TCModel.Play

open System
open System.IO
open TCModel.Engine
open TCModel.Table
open TCModel.Net

let private clockSeed () = uint64 DateTime.UtcNow.Ticks

/// Names the file one game's record will be kept in: when it was dealt, and what it is.
/// Taken once when the game is dealt, so every save of that game writes over the same file.
///
/// A thunk rather than a name, because the clock is read at the moment a game is dealt and
/// not a moment earlier - a table that took the time when it was built would file a game
/// under the hour the program started. What goes into it is `Transcript`'s to say, this
/// being one half of a name whose other half takes it apart.
let private stamping (game: Playable<_, _, _>) =
    fun () -> Transcript.stamping game.Name DateTime.Now

/// The console watching its own game. There is only ever one of these at a keyboard, so
/// what it is called matters no more than that `Solo` and this agree on it.
[<Literal>]
let private Keyboard = "keyboard"

/// Do what a typed line asked of the world, and hand back whatever there is to say about it.
///
/// Writing a file is the whole of what that ever is. `Solo` decides *whether* - including
/// the awkward case where a restart has to write the game it just swept away rather than
/// the one in hand - and this does it, which is the only reason there is a `Program.fs`
/// between a keyboard and a fold.
///
/// Handed back rather than printed, for the reason the loop below gives: anything said before
/// the board is drawn is said off the top of the screen.
let private errand game sitters doing =
    let write model stamp announce =
        if announce then
            let path = Transcript.save game stamp sitters model.Journal
            [ $"Record saved to {Path.GetRelativePath(Directory.GetCurrentDirectory(), path)}" ]
        else
            if not (Journal.isEmpty model.Journal) then
                Transcript.save game stamp sitters model.Journal |> ignore

            []

    match doing with
    | Carrying -> []
    | Keeping(model, stamp, announce) -> write model stamp announce
    | Leaving(Some model, stamp) -> if Journal.isEmpty model.Journal then [] else write model stamp true
    | Leaving(None, _) -> []

/// The parts of what the table said that a terminal has to show for itself. A screen is not
/// one of them: the loop below draws the board afresh each turn, because a terminal cannot
/// patch one in place the way a page can.
///
/// Handed back rather than printed, and that is the whole of the fix for a thing that had been
/// wrong since there was a loop at all: a refusal, a page of help or a whole record was printed
/// where it happened, and then the board was drawn *under* it - so the longer the answer, the
/// further off the top of the screen it went, and `help` at a game with a board this tall
/// scrolled itself away entirely. It goes below the board now, where the eye already is because
/// the prompt is the next line down.
///
/// The bell is not one of those: it is rung here rather than carried, because a bell is not
/// something to read in order. At one keyboard it never rings anyway - the only console at the
/// table is the one that just typed something, and a table never nudges the player who spoke.
///
/// Whether it rings at all is handed in rather than read here, and that is deliberate: the
/// Audio page can turn it off in the middle of a sitting without saving, so what is true is
/// what the player last said and not what the file last held.
let private tell rings posts =
    for post in posts do
        if rings && post.Say = Nudged then printf "\a"

    posts
    |> List.choose (fun post ->
        match post.Say with
        | Told text
        | TurnedAway text
        // Never said at this table - there are no seats here to get up from, and putting the
        // game down ends the loop below rather than being reported to it. Shown anyway,
        // because a sentence addressed to the person at the keyboard is a sentence to show
        // them whatever came to say it.
        | GotUp text -> Some text
        | Nudged
        | Screen _
        | Seated _ -> None)

/// Fold what is typed here into the game. Once it is over the loop stays open, so the
/// record can be read and walked back through before the table is cleared.
///
/// What a line *means* is `Solo`'s, not this file's. All that is left here is a keyboard,
/// a screen and a file - which is why the local game can be checked without any of the
/// three, the same way the networked one can.
///
/// Which game it is, is the table's rather than this loop's: `Solo` carries it, and nothing
/// between here and the file being written has to be told.
let rec private loop rings sitters said solo =
    let show lines =
        for line in lines do
            printf "%s" (line + Environment.NewLine)

    Solo.board Keyboard solo |> Option.iter (printf "%s")
    // What the last line came to, under the board rather than over it. A refusal or a page of
    // help printed where it happened is a refusal the board then scrolls off the screen.
    show said
    printf "%s" (if Solo.isOver solo then "(over) > " else "> ")

    let heard line =
        let next, posts, doing = Solo.said (stamping (Solo.game solo) ()) Keyboard line solo
        let answered = tell rings posts @ errand (Solo.game solo) sitters doing

        match doing with
        // Nothing more will be drawn, so this is the last chance to say where the record went.
        | Leaving _ ->
            show answered
            Solo.model next
        | Carrying
        | Keeping _ -> loop rings sitters answered next

    match Console.ReadLine() with
    // Nothing more coming. The same as saying so, and answered the same way, so that a
    // game piped in from a file still ends up written down.
    | null -> heard "quit"
    | line -> heard line

/// The same keyboard, at a game that does not wait for it.
///
/// The loop above asks for a line and the game waits while it is typed. This one does not: it
/// draws, watches the clock, and plays the game's own beat whenever the time is up - so what a
/// player presses is steering rather than stepping, and a board left alone goes on without
/// them. Which is the whole of what "real time" means here, and it is *all* that is different:
/// the beat is an ordinary move, folded by the same `Solo`, written into the same record, and
/// replayed off it beat for beat with no clock involved at all.
///
/// Keys rather than lines, because a game running at three a second cannot ask for a newline.
/// The table's own three come first - Enter to type a whole line, space to hold the clock, Esc
/// to put it down - and everything else is the game's, as the line it stands for. So nothing
/// can be pressed here that could not have been typed, which is the same bargain a button on a
/// page makes.
///
/// Falls back to the ordinary loop the moment the game is over, and starts there instead where
/// there is nobody at the keyboard to press anything: a game piped in from a file plays its
/// beats by asking for them in words, and comes out the same game.
let rec private racing rings sitters said (pulse: Pulse<_, _>) solo =
    let show lines =
        for line in lines do
            printf "%s" (line + Environment.NewLine)

    /// What the table itself does with a press, which is the same at any game that has a
    /// clock: hold it, put it down, or say something longer than a key.
    let (|Typing|Holding|Leaving'|Steering|) (key: ConsoleKeyInfo) =
        match key.Key with
        | ConsoleKey.Enter -> Typing
        | ConsoleKey.Spacebar -> Holding
        | ConsoleKey.Escape -> Leaving'
        | _ -> Steering

    let rec spin holding said solo =
        Screens.cleared ()
        Solo.board Keyboard solo |> Option.iter (printf "%s")
        show said

        printfn
            "%s"
            (if holding then
                 "  (held) space to go on - Enter to type a line - Esc to put it down"
             else
                 "  space to hold - Enter to type a line - Esc to put it down")

        if Solo.isOver solo then
            // The clock stops with the game, and the ordinary prompt comes back - so a game
            // that has just ended can be read, walked back through and saved like any other.
            // Whatever the last beat had to say goes with it, which is where the line saying
            // the record was written lands.
            loop rings sitters said solo
        else

        /// A line, from wherever it came: a key that stands for one, or one typed at the
        /// prompt. Answered exactly as the ordinary loop answers it.
        let heard holding line solo =
            let next, posts, doing = Solo.said (stamping (Solo.game solo) ()) Keyboard line solo
            let answered = tell rings posts @ errand (Solo.game solo) sitters doing

            match doing with
            | Leaving _ ->
                show answered
                Solo.model next
            | Carrying
            | Keeping _ -> spin holding answered next

        // Held, the clock is simply not consulted - the game stands still and a press still
        // steers, which is what anybody stopping to think wants.
        let due =
            DateTime.UtcNow
            + (if holding then TimeSpan.FromDays 1.0 else pulse.Every(Model.state (Solo.model solo)))

        match Screens.awaiting due with
        | None ->
            let next, posts, doing = Solo.beaten solo
            spin holding (tell rings posts @ errand (Solo.game solo) sitters doing) next
        | Some key ->
            match key with
            | Leaving' -> heard holding "quit" solo
            | Holding -> spin (not holding) said solo
            | Typing ->
                printf "> "

                match Console.ReadLine() with
                | null -> heard holding "quit" solo
                | line -> heard holding line solo
            | Steering ->
                match pulse.Pressed key with
                | Some line -> heard holding line solo
                | None -> spin holding said solo

    if Screens.steering () then spin false said solo else loop rings sitters said solo

/// Deal a game. The rules are the only thing that can refuse, and they say why in words, so
/// a count that came from a person is answered in their own rather than swallowed - and this
/// no longer has to know what it is that refuses, which is a fair test of the seam.
let private dealt game players seed = Update.start game.Rules players seed

/// Take a saved game up again: played through move by move to where it was left, with the
/// same players back in the same seats, and `undo` able to walk back through every state it
/// passed on the way.
///
/// Three things come back, and all three are the difference between taking a game up and
/// merely looking at one. The model is the game. The seating is who was at it, so the
/// machines sit back down at the seats they were playing and at the strength they were
/// playing them - without that, taking a game up again hands you every seat at the table and
/// the game you resume is not the game you put down. And the stamp is the file it came out
/// of, so it goes on being written to rather than forked into a second record beside the
/// first.
/// The same, said out loud. `Transcript.takenUp` does the reading; this is the part that only
/// makes sense where there is somebody sitting at a screen to be told.
let private takeUp game path =
    /// Where to take it instead, for a program that has somewhere to send it. One with a list
    /// of games in it can name another of them; a game's own executable cannot open a record
    /// of anything but itself, and says which game it is rather than inventing a line that
    /// names an executable it has never seen.
    let elsewhere other =
        match Invoked.another other with
        | Some line -> $" Take it up with '{line} replay {path}'."
        | None -> ""

    Transcript.takenUp elsewhere game path
    |> Result.map (fun (model, sitters, stamp, moves) ->
        printfn "Took up %d move(s) from %s." moves path
        printfn "Take them back with 'undo', or read them with 'history'."
        model, sitters, stamp)

/// The machines this seating asks for, seated at the game that was dealt.
///
/// Which seat a machine plays and what its generator started from are facts about the deal,
/// so they are settled here, once, and both tables simply take them as they are.
let private machines game sitters model =
    game.Seating (Model.seed model) (Seating.machines sitters) (Model.state model)

/// Serve a game to a browser on this machine. Nothing comes back: like a hosted table it
/// runs until the process is stopped, because a page has no way of saying the game is over
/// and done with.
///
/// The game arrives already opened - dealt or taken up from a record - because which of the
/// two it was is settled once, further down, and nothing below that line has any business
/// asking again.
let private serveFor game palette reach (model, sitters, stamp) =
    let keep model stamp =
        if Journal.isEmpty model.Journal then
            None
        else
            let path = Transcript.save game stamp sitters model.Journal
            Some(Path.GetRelativePath(Directory.GetCurrentDirectory(), path))

    // Nobody is watching yet. A browser adds itself when it opens its stream, which is
    // the only moment anybody is really there - and the machines have already played up
    // to the first seat a person has to fill by the time it does.
    let solo, doing =
        Solo.opened game stamp model |> Solo.against (machines game sitters model)

    // Nobody at a keyboard here, so there is nowhere to put what it has to say - a browser is
    // told where its record went by the page rather than by this process's console.
    errand game sitters doing |> ignore
    Server.serve reach palette solo (stamping game) keep

/// Open a table for players at their own machines. Nothing comes back: the table waits
/// until whoever opened it stops the process, because no one player may close it on all
/// the others.
///
/// The seating goes in whole rather than as a count, because a table may have the machine
/// at some of its seats - those are played here and are never waited for. The reach goes in
/// whole for the same reason: how far the table can be reached and what it takes to sit down
/// at it are settled together or they contradict each other.
let private hostFor game view rings reach (model, sitters, stamp) =
    // A seat of the host's own is taken from here, by a console sitting down at this very
    // table over the same wire everybody else arrives on. Nothing about the table is
    // special-cased for it: it joins, it is handed a seat and a token, it is drawn a board
    // per turn, and if the process it is inside had been on another machine the table
    // could not tell the difference. Which is the point - a seat played by a shortcut
    // would be a second way of sitting down, and there is no room here for two.
    let mine, _ = Seating.awaited sitters

    let playing =
        if mine = 0 then
            None
        else
            Some(fun () ->
                // No table named: this is the host sitting down at the one table its own
                // process is holding, which is the case that has no name to give.
                match Client.join game (Reach.at reach "localhost") None (Reach.word reach) None rings view with
                // The console got up, and the table it got up from is this process. It
                // goes on standing - a player leaving their seat is not the same as
                // closing the room - so what is left is to say so, where before there
                // was a prompt that had stopped answering and no word about why.
                | 0 ->
                    printfn ""
                    printfn "  The table is still open to whoever else is at it. Ctrl+C closes it."
                    printfn ""
                // It never sat down, and said why on its way past. The table stands
                // regardless: a console that could not find a seat is no reason to
                // close one on everybody who did.
                | _ -> ())

    let keep model =
        if not (Journal.isEmpty model.Journal) then
            Transcript.save game stamp sitters model.Journal |> ignore

    Server.host game reach model sitters keep playing

/// Everything the settings screens are able to change, carried together.
///
/// Four things, and they travel as one because between the front door and the first board
/// every one of them can be changed from the same screen. Threaded one at a time they were
/// four parameters on every function down the menu, three of which any given one of those
/// functions did not care about - and the fifth would have been a fifth.
///
/// `Ways` is here rather than beside the game because the settings screen can change which
/// way is being played, and a screen that could hand back a way needs the list to choose from.
/// A game with one way carries a list of one, which is the ordinary case and costs nothing.
[<NoComparison; NoEquality>]
type private Settled<'Move, 'State, 'Notice> =
    {
        /// Every way this game can be played, the plainest first.
        Ways: Playable<'Move, 'State, 'Notice> list
        /// The one being played, which is always one of the above.
        Game: Playable<'Move, 'State, 'Notice>
        View: View<'Move, 'State, 'Notice>
        /// Whether the table rings when the turn comes round unasked.
        Rings: bool
    }

module private Settled =

    /// The ways a game offers, as the settings screen wants them: a name and a sentence each.
    let ways settled =
        settled.Ways |> List.map (fun way -> way.Name, way.Blurb)

    /// Play it this way instead. The view comes with it, because a view is built out of a
    /// game and the one in hand belongs to the way being left - drawn afresh from the new one,
    /// in the colours that were already settled on, and falling back to that game's plainest
    /// where it has no view by that name.
    let playing name settled =
        match settled.Ways |> List.tryFind (fun way -> way.Name = name) with
        | None -> settled
        | Some game ->
            { settled with
                Game = game
                View = Playable.recoloured settled.View.Palette game settled.View }

/// What the menu settled on: a game to play at this keyboard, or a way of playing that
/// runs to its own end and only has an exit code to give back.
[<NoComparison; NoEquality>]
type private Opening<'Move, 'State, 'Notice> =
    /// A game to play here: everything that was settled about it, who is at it, and the file
    /// its record belongs in - which is a fresh one for a fresh deal and the old one for a
    /// game being taken up again.
    ///
    /// The whole of what was settled rather than the view alone, because the settings screen
    /// can change which way the game is played and whether the table rings, and both of those
    /// have to survive the walk from the menu to the first board.
    | Play of Settled<'Move, 'State, 'Notice> * Model<'Move, 'State, 'Notice> * Sitter list * stamp: string
    | Done of code: int
    /// The player walked back out of this game's front door. Not the same as leaving: there
    /// is a list of games behind it, and that is where they meant to be.
    | Back

/// The settings, which run until the player is done with them and give back what they settled
/// on - which may be a different way of playing and a different view from the ones they came
/// in with, and is in whatever colours they chose either way.
///
/// They are shown through that view, so the sample colours on them are drawn by the very thing
/// that will be drawing the board - and asking for another way of drawing redraws the screen
/// itself that way, which is a fair look at what is being chosen.
///
/// Keeping the settings happens here and not in the screens, for the same reason writing a
/// record happens here and not in `Solo`: a screen says what it wants and this is the only
/// part of the program allowed to touch a disk. They are read afresh at the moment they are
/// written rather than held from startup, so that keeping this game's colours does not put
/// back a stale copy of every other game's.
///
/// All three pages are kept by one `save`, from whichever page it was said on. Which is the
/// only honest reading of the word: nothing on any page is a draft, they are all simply what
/// is settled at this moment, and a save that kept the page you happened to be standing on
/// would be a save that quietly dropped the answer you gave two screens ago.
let rec private settling settled at said =
    match Screens.asking settled.View.Says said (Options.screen (List.length settled.Ways)) at with
    | None, _ -> settled
    | Some line, at ->
        match Options.choose line with
        | Error problem -> settling settled at problem
        | Ok Options.Done -> settled
        | Ok(Options.Opening Options.Audio) -> settling (listening settled 0 "") at ""
        | Ok(Options.Opening Options.Video) -> settling (watching settled 0 "") at ""
        | Ok(Options.Opening Options.Game) -> settling (playing settled 0 "") at ""
        | Ok step ->
            match wayOut settled step (fun settled said -> settling settled at said) with
            | Some again -> again
            // Nothing else can reach here: every other step is one only a page's own reader
            // can build, and the menu's reader is not one of them.
            | None -> settling settled at ""

/// Whether the table rings. One row, and the only page whose answer is not a game's own.
and private listening settled at said =
    let view = settled.View

    match Screens.asking view.Says said (Options.audio settled.Rings) at with
    | None, _ -> settled
    | Some line, at ->
        match Options.chooseAudio line with
        | Error problem -> listening settled at problem
        | Ok Options.Done -> settled
        | Ok(Options.Ringing on) -> listening { settled with Rings = on } at ""
        | Ok step ->
            match wayOut settled step (fun settled said -> listening settled at said) with
            | Some again -> again
            | None -> listening settled at ""

/// How the board is drawn, and in what colours - the page this screen used to be all of.
and private watching settled at said =
    let view = settled.View

    let views =
        Playable.offered AtATerminal view.Palette settled.Game
        |> List.map (fun view -> view.Name)

    match Screens.asking view.Says said (Options.video views view.Name view.Palette) at with
    | None, _ -> settled
    | Some line, at ->
        match Options.chooseVideo view.Palette line with
        | Error problem -> watching settled at problem
        | Ok Options.Done -> settled
        | Ok(Options.Changed palette) ->
            watching
                { settled with
                    View = Playable.recoloured palette settled.Game view }
                at
                ""
        | Ok(Options.Drawn name) ->
            // In the colours already settled on, and only among the views a terminal can
            // show - the same two conditions the game itself puts on `view`.
            match Playable.byName AtATerminal view.Palette settled.Game name with
            | Ok chosen -> watching { settled with View = chosen } at ""
            | Error problem -> watching settled at problem
        | Ok step ->
            match wayOut settled step (fun settled said -> watching settled at said) with
            | Some again -> again
            | None -> watching settled at ""

/// And which way this game is played, for a game that offers more than one.
and private playing settled at said =
    let view = settled.View

    match Screens.asking view.Says said (Options.game (Settled.ways settled) settled.Game.Name) at with
    | None, _ -> settled
    | Some line, at ->
        match Options.chooseGame (Settled.ways settled) line with
        | Error problem -> playing settled at problem
        | Ok Options.Done -> settled
        | Ok(Options.Playing name) -> playing (Settled.playing name settled) at ""
        | Ok step ->
            match wayOut settled step (fun settled said -> playing settled at said) with
            | Some again -> again
            | None -> playing settled at ""

/// The two steps every one of the four screens answers the same way, answered once for all of
/// them: ask again, or keep the lot.
///
/// `None` back means this was not one of them, and whichever screen asked is the only thing
/// that could have known what else it might be.
///
/// One `save` writes all three pages, from whichever page it was said on - which is the only
/// honest reading of the word. Nothing on any page is a draft; they are all simply what is
/// settled at this moment, and a save that kept only the page somebody happened to be standing
/// on would be a save that quietly dropped the answer they gave two screens ago. The file is
/// read afresh here rather than held from startup, so keeping this game's answers does not put
/// back a stale copy of every other game's.
and private wayOut settled step onwards =
    match step with
    | Options.Same -> Some(onwards settled "")
    | Options.Keep ->
        let settings, _ = Settings.load ()

        let kept =
            settings
            |> Settings.keeping settled.Game.Name settled.View.Name settled.View.Palette
            |> Settings.ringing settled.Rings
            // Under the name of the game that offers the ways rather than of the way being
            // played, so that this answer is found again by a game which has not yet been told
            // which way it is - that being exactly the question this line answers.
            |> Settings.playing (List.head settled.Ways).Name settled.Game.Name

        match Settings.save kept with
        | Ok path -> Some(onwards settled $"Kept in {path}. Every game opened from here on opens like this.")
        | Error problem -> Some(onwards settled problem)
    | _ -> None

/// What the menu settled on, once it is a game rather than another screen.
///
/// Shared by the front door and the seat list, because a seating dealt from one is dealt
/// exactly as it is from the other - and because a way of opening a game added to only one
/// of the two would be a way of opening a game a player could not find.
let private starting settled choice =
    let game, view = settled.Game, settled.View

    let clocked seed =
        seed |> Option.defaultValue (clockSeed ())

    /// A table from a seating the menu settled on: dealt, and filed under a fresh name.
    let dealing sitters seed =
        dealt game (List.length sitters) (clocked seed)
        |> Result.map (fun model -> model, sitters, stamping game ())

    match choice with
    | Menu.Deal(sitters, seed) ->
        dealing sitters seed
        |> Result.map (fun (model, sitters, stamp) -> Play(settled, model, sitters, stamp))
    // In whatever colours the player settled on here, which is the same promise the
    // command line's --colour keeps.
    //
    // A line that said nothing about how far the table reaches is answered the way the
    // command line answers the same silence: a word at the door, made up here. Everything the
    // seat list and the screen behind it send says the whole of it, so this is only reached
    // by somebody typing the short way round.
    | Menu.Serve(sitters, seed, reach) ->
        dealing sitters seed
        |> Result.map (fun table -> Done(serveFor game view.Palette (reach |> Option.defaultWith Reach.fresh) table))
    | Menu.Host(sitters, seed, reach) ->
        dealing sitters seed
        |> Result.map (fun table -> Done(hostFor game view settled.Rings (reach |> Option.defaultWith Reach.fresh) table))
    | Menu.Join(address, code) -> Ok(Done(Client.join game address None code None settled.Rings view))
    | Menu.Replay path ->
        takeUp game path
        |> Result.map (fun (model, sitters, stamp) -> Play(settled, model, sitters, stamp |> Option.defaultWith (stamping game)))
    // The rest are screens rather than games. The front door answers every one of them
    // itself, so what is left here is somebody at the seat list asking for one of them from
    // there - which is a fair thing to type and wants an answer rather than a shrug.
    | Menu.Sitting _
    | Menu.Reaching _
    | Menu.Continuing
    | Menu.Rules
    | Menu.Looking _
    | Menu.Options
    | Menu.Leave
    | Menu.Backing
    | Menu.Waiting -> Error "That is settled at the menu. Say 'back' to go there, or name the seats."

/// The two screens a game is settled on, which run until there is one to open or are backed
/// out of: who is in each seat, and how far the table those seats are at will reach.
///
/// One loop each and one word between them, because every line either of them sends says the
/// whole of both - a seating and a reach - so neither has anything to remember and either can
/// hand the other everything it needs. The word is the one thing here that had to be made up
/// rather than read, so it is made up once, out here, and passed along: a screen that invented
/// one as it drew would show a different word every time it was drawn.
let rec private sitting settled word sitters reach at said =
    match Screens.asking settled.View.Says said (Menu.seats settled.Game sitters reach) at with
    | None, _ -> Some(Done 0)
    | Some line, at -> answering settled word sitters reach at line sitting

and private reaching settled word sitters reach at said =
    match Screens.asking settled.View.Says said (Menu.reaches word sitters reach) at with
    | None, _ -> Some(Done 0)
    | Some line, at -> answering settled word sitters reach at line reaching

/// What either of them does with a line, which is the same thing: the seats and the reach
/// come back out of it, and where they are shown next is the line's own answer.
and private answering settled word sitters reach at line asked =
    let again sitters reach said =
        asked settled word sitters reach at said

    match Menu.choose settled.Game settled.View.Palette line with
    | Ok(Menu.Sitting(sitters, asked)) -> sitting settled word sitters (asked |> Option.defaultValue reach) at ""
    | Ok(Menu.Reaching(sitters, reach)) -> reaching settled word sitters reach at ""
    | Ok Menu.Waiting -> again sitters reach ""
    | Ok Menu.Backing -> None
    // Going is going, from wherever it is said.
    | Ok Menu.Leave -> Some(Done 0)
    | Ok chosen ->
        match starting settled chosen with
        | Ok opening -> Some opening
        | Error problem -> again sitters reach problem
    | Error problem -> again sitters reach problem

/// The list of saved games, which runs until one is taken up or backed out of.
///
/// The folder is read afresh every time round rather than once on the way in, and that is
/// what makes the list honest: a game put down in another window a moment ago is on it, and
/// a record deleted from underneath is not. It costs a directory listing per key press at the
/// one screen in the program where nobody is in a hurry.
///
/// Which records are worth showing is settled here, because it is a question about a person
/// rather than about a folder: somebody standing at this game's front door means this game's
/// saved games. A record whose name does not say which game it is, is shown too - it may well
/// be this one, those being the records written before a name said, and it is marked on the
/// screen as the open question it is.
let rec private taking settled at said =
    let game = settled.Game

    let records =
        Transcript.saved ()
        |> List.filter (fun record -> record.Game = Some game.Name || record.Game = None)

    match Screens.asking settled.View.Says said (Menu.continuing game records) at with
    | None, _ -> Some(Done 0)
    | Some line, at ->
        match Menu.choose game settled.View.Palette line with
        | Ok Menu.Waiting
        // Asked for from here, which is where it already is. Drawn again rather than refused,
        // because the list is read afresh each time round and asking for it is a fair way to
        // say "look again".
        | Ok Menu.Continuing -> taking settled at ""
        | Ok Menu.Backing -> None
        | Ok Menu.Leave -> Some(Done 0)
        | Ok chosen ->
            match starting settled chosen with
            | Ok opening -> Some opening
            | Error problem -> taking settled at problem
        | Error problem -> taking settled at problem

/// The start menu, which runs until it has settled on a game. Everything it offers either
/// opens one or comes back round to here, so there is no way out of it but the two, and no
/// way to be at the prompt with nothing to play.
/// The settings carry a way of playing as well as a way of reading, so the menu is written
/// against the whole of what was settled rather than against a game and a view. Which is what
/// lets somebody open the settings, change which rules are in play, and be handed back a menu
/// for *that* game - the alternative being a screen that made a change nothing acted on until
/// the program was started again.
let rec private welcome settled behind at said =
    let game, view = settled.Game, settled.View

    // The menu is shown in the view it is offering, so 'view rich' shows what rich looks
    // like before a whole game is committed to it.
    match Screens.asking view.Says said (Menu.screen game view behind) at with
    | None, _ -> Done 0
    | Some line, at ->

    /// Say what went wrong and ask again, with the cursor where it was left. The menu is the
    /// only place to be when there is no game yet, so nothing here leaves except by being
    /// asked to.
    let retry problem = welcome settled behind at problem

    let again () = welcome settled behind at ""

    match Menu.choose game view.Palette line with
    // There is nothing behind the front door, so backing out of it is asking again.
    | Ok Menu.Waiting -> again ()
    // Backing out of the front door lands wherever it was opened from, which is the list of
    // games if there is one and here if there is not.
    | Ok Menu.Backing -> if behind then Back else again ()
    | Ok Menu.Leave -> Done 0
    | Ok Menu.Rules ->
        printfn "%s" view.Rules
        Screens.held ()
        again ()
    | Ok(Menu.Looking chosen) -> welcome { settled with View = chosen } behind at ""
    | Ok Menu.Options -> welcome (settling settled 0 "") behind at ""
    | Ok Menu.Continuing ->
        match taking settled 0 "" with
        | Some opening -> opening
        | None -> again ()
    | Ok(Menu.Sitting(sitters, asked)) ->
        // One word for this way through the menu, made up here because a screen cannot make
        // one up: it is what the door starts out holding, and what walking the door shut
        // again puts back, so that a player who opens it to look and changes their mind is
        // not handed a different table than the one they were reading a moment ago.
        let word = Reach.minted ()

        match sitting settled word sitters (asked |> Option.defaultValue (Reach.locked word)) 0 "" with
        | Some opening -> opening
        | None -> again ()
    | Ok chosen ->
        match starting settled chosen with
        | Ok opening -> opening
        | Error problem -> retry problem
    | Error problem -> retry problem

/// Sit down at a game and play it here. The board the player is looking at when they
/// arrive is drawn by the same code that draws every one after it, because sitting down is
/// simply the first thing that happens at the table.
let private play settled sitters stamp model =
    let game = settled.Game

    // The machines take their seats before anybody sits down to watch, so that a table where
    // the first move is theirs has already had it made by the time the first board is drawn.
    // A game taken up again seats them from what its record said, so they come back to the
    // seats they were playing rather than to whatever the line that opened it happened to say.
    let seated, doing =
        Solo.opened game stamp model |> Solo.against (machines game sitters model)

    let kept = errand game sitters doing

    let solo, posts =
        seated
        |> Solo.watching
            Keyboard
            { Margins = Margins.all
              View = settled.View }

    let said = kept @ tell settled.Rings posts

    // Under the first board, the same as everything after it - which for the one line said here,
    // who the machine is, is the only place it would ever be read.
    //
    // Which of the two loops is the game's own answer and nothing else's: a game that waits is
    // asked for a line, and one that does not is drawn on a clock and steered with keys.
    match game.Pulse with
    | Some pulse -> racing settled.Rings sitters said pulse solo |> ignore
    | None -> loop settled.Rings sitters said solo |> ignore

    0

/// Act on what a command line asked for.
///
/// Reading a command line stops at a `Launch`, and this is what one is handed to. So there
/// is one place that knows what opening a game actually involves, and adding a way in means
/// adding a case rather than another road through `main`.
let private opening settled launch =
    let game, view = settled.Game, settled.View

    let clocked seed =
        seed |> Option.defaultValue (clockSeed ())

    /// The table a way in opens, settled once for all three of them: a game, who is at it,
    /// and the file its record belongs in.
    ///
    /// `others` is who the people at this table are - somebody at this keyboard, or somebody
    /// arriving from their own machine - and that is a fact about the way in rather than
    /// about the game. Which is what lets a game put down at one keyboard be taken up as a
    /// table for four: the machines come back out of the record, because which seat one
    /// played and how well it played are written down nowhere else, and everybody else is
    /// whoever this way in means.
    ///
    /// The command line names the machines rather than the seats - `--rival hard` is the seat
    /// after yours - so what it asks for is a seating said shorter, spelt out here.
    let table others start =
        match start with
        | Start.Dealt(players, seed, rivals) ->
            dealt game players (clocked seed)
            |> Result.map (fun model -> model, Seating.after players rivals |> Seating.resuming others, stamping game ())
        | Start.Saved path ->
            takeUp game path
            |> Result.map (fun (model, sitters, stamp) ->
                model, Seating.resuming others sitters, stamp |> Option.defaultWith (stamping game))

    let onward how outcome =
        match outcome with
        | Ok table -> how table
        | Error problem ->
            eprintfn "%s" problem
            1

    match launch with
    | Launch.Play start ->
        table Here start
        |> onward (fun (model, sitters, stamp) ->
            printfn "%s" view.Rules
            play settled sitters stamp model)
    | Launch.Serve(start, reach) -> table Here start |> onward (serveFor game view.Palette reach)
    | Launch.Host(start, reach) -> table Elsewhere start |> onward (hostFor game view settled.Rings reach)
    | Launch.Join(address, token, code, table) -> Client.join game address token code table settled.Rings view
    // A house deals its own tables, so there is no game to open here and nothing to hand it.
    // Which way each table is played is settled when somebody opens one, not once for the
    // whole house - that being the point of a house rather than four of them.
    | Launch.House(reach, filling) ->
        let hosting =
            Net.Hosting.of' settled.Ways clockSeed (fun game -> Transcript.stamping game.Name DateTime.Now)

        Server.house hosting reach filling

// --- one game, with its types sealed off behind it --------------------------------------
//
// Everything above this line is generic in the game and could not be otherwise: it is what
// opening a game *involves*, and none of it is any game in particular.
//
// What is below is the one place that has to stop being generic. Two games have different
// moves, different states and different notices, so `Playable<Move, Session, Notice>` and
// `Playable<Mark, Board, Said>` are different types and no list holds both. The standard way
// out is the one taken here: an interface with no type parameters at all, implemented by
// closing over a game - which is exactly what a game is *for* at this point, since by now
// everything anybody wants of one comes back as a number, a string or a screen.
//
// So the type parameters stop here rather than reaching `Games.fs` or `main`. Which is the
// whole of what makes a list of games possible.

/// A game as the program picks one: something to name, something to say about it, and two
/// ways of opening it.
[<NoComparison; NoEquality>]
type Chosen =
    /// One word, lower case - what it is called on a command line.
    abstract Name: string
    /// And what it is called to a person, with a line about what it is.
    abstract Title: string
    abstract Blurb: string

    /// How many may sit down, for a picker to say before anybody commits to one.
    abstract Fewest: int
    abstract Most: int

    /// Every name this game answers to - one per way it can be played, the plainest first.
    ///
    /// One for nearly every game. A game with an optional rule in it has two, because a game
    /// played with that rule is a different game and writes a different record, so it has a
    /// name of its own rather than a flag somebody has to remember they set.
    abstract Names: string list

    /// The same game as the way with that name, for a command line that named one outright.
    /// A name that is none of this game's leaves it as it was, which is the same answer the
    /// settings file gets for a way that has since been renamed.
    abstract As: string -> Chosen

    /// What the game says is wrong with itself, before anybody sits down.
    abstract Faults: string list

    /// Open whatever a command line asked for. The words are the launch alone - whichever
    /// of them named the game has been taken off already.
    abstract FromCommandLine: string seq -> int

    /// Or ask, at the menu, and play whatever it settles on.
    ///
    /// `behind` is whether there is anything to go back to - a list of games, if this
    /// program has more than one. `None` back means the player walked out of this game's
    /// front door rather than leaving, and expects to be asked which game again.
    abstract FromMenu: behind: bool -> int option

/// A game, as one of those.
///
/// Everything it answers is either read straight off the game or is one of the two functions
/// above with the game already given to it. There is nothing else to it, and there should not
/// be: a `Chosen` that could do anything a `Playable` could not would be a second seam.
///
/// `ways` is every way this game can be played and `game` is the one this `Chosen` is. They
/// are two arguments rather than one because the second is not always the first of the first:
/// a command line that named a way outright opens that way, and the settings file may name
/// another, and a `Chosen` is built the same way whichever of those settled it.
let rec private making (ways: Playable<'Move, 'State, 'Notice> list) (game: Playable<'Move, 'State, 'Notice>) (asked: bool) =
    /// Which way to open, and the whole of the difference `asked` makes.
    ///
    /// Somebody who typed `compile-control` has said which way and is not asking; the settings
    /// answer for somebody who did not. Without that, a word on a command line would be
    /// silently overruled by a file - which is the wrong way round for every other thing this
    /// program is told, and would make `compile` mean whichever game was last played.
    let settling settings =
        if asked then
            game
        else
            match Settings.plays (List.head ways).Name settings with
            | Some name -> ways |> List.tryFind (fun way -> way.Name = name) |> Option.defaultValue game
            | None -> game

    { new Chosen with
        member _.Name = game.Name
        member _.Title = game.Title
        member _.Blurb = game.Blurb
        member _.Fewest = game.Fewest
        member _.Most = game.Most
        member _.Names = ways |> List.map (fun way -> way.Name)

        member this.As name =
            ways
            |> List.tryFind (fun way -> way.Name = name)
            |> Option.map (fun way -> making ways way true)
            |> Option.defaultValue this

        member _.Faults = game.Faults

        member _.FromCommandLine argv =
            // A command line says nothing about the bell or the way of playing, so what it
            // does not say is what was last settled - the same promise the menu keeps, kept at
            // the other way in.
            let settings, _ = Settings.load ()
            let opened = settling settings
            let view, _ = Playable.opening AtATerminal settings opened

            let settled =
                { Ways = ways
                  Game = opened
                  View = view
                  Rings = Settings.bell settings }

            Launch.run opened (fun view launch -> opening { settled with View = view } launch) argv

        member _.FromMenu behind =
            // The way this game was last left, which for somebody who has never said
            // otherwise is the plainest way it can be drawn at a terminal. The menu offers
            // the rest for this sitting, and the settings screen is where saying so sticks.
            let settings, unread = Settings.load ()
            let opened = settling settings
            let plain, stale = Playable.opening AtATerminal settings opened

            // What was wrong with the two files that answer for how a game is read, said on
            // the first screen there is to say it on rather than off the top of one. Both are
            // files a person writes by hand, so a line of either that quietly did nothing is
            // a line they would otherwise spend an evening on.
            let said = String.concat Environment.NewLine (Palette.complaints @ unread @ stale)

            let settled =
                { Ways = ways
                  Game = opened
                  View = plain
                  Rings = Settings.bell settings }

            match welcome settled behind 0 said with
            | Play(settled, model, sitters, stamp) -> Some(play settled sitters stamp model)
            | Done code -> Some code
            | Back -> None }

/// A game, as one of those: every way it can be played, and the plainest of them as the one
/// nobody has yet said otherwise about.
let chosen ways game = making ways game false

// --- and the whole of what a program made of one game is -------------------------------
//
// A game is its own executable now, and what `main` does in one is the same three things
// every time: refuse a game that says it is broken, read the line if there was one, and ask
// at the menu if there was not. Written here rather than four times over, because a game's
// own `Program.fs` should be the one line that says which game it is and nothing else.
//
// The program that offers a *list* of games is built out of the same two pieces, which is
// the point of their being pieces: it settles which game a line is about and then hands it
// to `alone` exactly as a single-game executable would.

/// Nothing is opened at a game that says it is broken. Asked here rather than at each way
/// in, because a map that does not hang together is not a thing to find out about halfway
/// through dealing.
///
/// `None` back means whoever is playing has not finished with this program - they walked out
/// of the game rather than out of the door - so it is a code or it is nothing.
let opened (game: Chosen) open' =
    match game.Faults with
    | _ :: _ as problems ->
        eprintfn $"{game.Title} does not hang together:"
        problems |> List.iter (eprintfn "  %s")
        Some 1
    | [] -> open' game

/// One game, with nothing behind it: a line opens what it asks for, and no line at all opens
/// the menu. Backing out of that menu is leaving, because there is nowhere else to be.
///
/// The words are the launch alone - whichever of them named the game, if any did, has been
/// taken off already.
let alone (game: Chosen) words =
    let opening =
        match words with
        | [] -> fun (game: Chosen) -> game.FromMenu false
        | launch -> fun (game: Chosen) -> Some(game.FromCommandLine launch)

    opened game opening |> Option.defaultValue 0

/// A whole program with this one game in it - what `main` is at a game's own executable.
///
/// Every game's `Program.fs` is this line, and it is worth their all being the same line:
/// there is nothing a game's way in should get to decide that a game does not already say
/// for itself, and a game whose door could would be a third seam.
///
/// The one thing said here that `alone` does not say is said to `Invoked`, and it is said
/// first: every line this program prints for somebody to type names this program, and from
/// here on none of them should name the game as well.
///
/// `ways` is every way the game can be played, the plainest first. Nearly every game has one
/// and passes a list of one; a game with an optional rule in it passes both, and which of them
/// a new game is dealt as is settled at the Game page of its own settings screen.
let only (ways: Playable<'Move, 'State, 'Notice> list) argv =
    Invoked.isTheOnlyGame ()
    alone (chosen ways (List.head ways)) (List.ofArray argv)
