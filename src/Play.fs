module TCModel.Play

open System
open System.IO
open TCModel.Engine
open TCModel.Table
open TCModel.Net

let private clockSeed () = uint64 DateTime.UtcNow.Ticks

/// Names the file one game's record will be kept in. Taken once when the game is dealt,
/// so every save of that game writes over the same file.
let private stampNow () =
    DateTime.Now.ToString "yyyy-MM-dd-HHmmss"

/// The console watching its own game. There is only ever one of these at a keyboard, so
/// what it is called matters no more than that `Solo` and this agree on it.
[<Literal>]
let private Keyboard = "keyboard"

/// Do what a typed line asked of the world.
///
/// Writing a file is the whole of what that ever is. `Solo` decides *whether* - including
/// the awkward case where a restart has to write the game it just swept away rather than
/// the one in hand - and this does it, which is the only reason there is a `Program.fs`
/// between a keyboard and a fold.
let private errand game doing =
    let write model stamp announce =
        if announce then
            let path = Transcript.save game stamp model.Journal
            printfn "Record saved to %s" (Path.GetRelativePath(Directory.GetCurrentDirectory(), path))
        elif not (Journal.isEmpty model.Journal) then
            Transcript.save game stamp model.Journal |> ignore

    match doing with
    | Carrying -> ()
    | Keeping(model, stamp, announce) -> write model stamp announce
    | Leaving(model, stamp) -> if not (Journal.isEmpty model.Journal) then write model stamp true

/// Say the parts of what the table said that a terminal has to print for itself. A screen is
/// not one of them: the loop below draws the board afresh each turn, because a terminal
/// cannot patch one in place the way a page can.
///
/// The bell is answered the same way a joined table answers it, and for the same reason -
/// but at one keyboard it never rings, because the only console at the table is the one that
/// just typed something and a table never nudges the player who spoke.
let private tell posts =
    for post in posts do
        match post.Say with
        | Told text
        | TurnedAway text
        // Never said at this table - there are no seats here to get up from, and putting
        // the game down ends the loop below rather than being reported to it. Printed
        // anyway, because a sentence addressed to the person at the keyboard is a sentence
        // to show them whatever came to say it.
        | GotUp text -> printf "%s" (text + Environment.NewLine)
        | Nudged -> printf "\a"
        | Screen _
        | Seated _ -> ()

/// Fold what is typed here into the game. Once it is over the loop stays open, so the
/// record can be read and walked back through before the table is cleared.
///
/// What a line *means* is `Solo`'s, not this file's. All that is left here is a keyboard,
/// a screen and a file - which is why the local game can be checked without any of the
/// three, the same way the networked one can.
///
/// Which game it is, is the table's rather than this loop's: `Solo` carries it, and nothing
/// between here and the file being written has to be told.
let rec private loop solo =
    Solo.board Keyboard solo |> Option.iter (printf "%s")
    printf "%s" (if Solo.isOver solo then "(over) > " else "> ")

    let heard line =
        let next, posts, doing = Solo.said (stampNow ()) Keyboard line solo

        tell posts
        errand (Solo.game solo) doing

        match doing with
        | Leaving _ -> Solo.model next
        | Carrying
        | Keeping _ -> loop next

    match Console.ReadLine() with
    // Nothing more coming. The same as saying so, and answered the same way, so that a
    // game piped in from a file still ends up written down.
    | null -> heard "quit"
    | line -> heard line

/// Deal a game. The rules are the only thing that can refuse, and they say why in words, so
/// a count that came from a person is answered in their own rather than swallowed - and this
/// no longer has to know what it is that refuses, which is a fair test of the seam.
let private dealt game players seed = Update.start game.Rules players seed

/// Play a saved game again and stop where it left off, from where `undo` walks back
/// through every state it passed on the way.
let private replayFrom game path =
    if not (File.Exists path) then
        Error $"There is no record at '{path}'."
    else
        Transcript.read game (File.ReadAllText path)
        |> Result.bind (fun reading ->
            Update.replay game.Rules reading.Players reading.Seed reading.Moves
            |> Result.mapError (fun _ -> $"'{path}' asks for a number of players the game does not take.")
            |> Result.map (fun model ->
                printfn "Replayed %d move(s) from %s." (List.length reading.Moves) path
                printfn "Take them back with 'undo', or read them with 'history'."
                model))

/// The machines this seating asks for, seated at the game that was dealt.
///
/// Which seat a machine plays and what its generator started from are facts about the deal,
/// so they are settled here, once, and both tables simply take them as they are.
let private machines game sitters model =
    game.Seating (Model.seed model) (Seating.machines sitters) (Model.state model)

/// Deal a game and serve it to a browser on this machine. Nothing comes back: like a
/// hosted table it runs until the process is stopped, because a page has no way of saying
/// the game is over and done with.
let private serveFor game palette sitters seed reach =
    match dealt game (List.length sitters) seed with
    | Error problem ->
        eprintfn "%s" problem
        1
    | Ok model ->
        let keep model stamp =
            if Journal.isEmpty model.Journal then
                None
            else
                let path = Transcript.save game stamp model.Journal
                Some(Path.GetRelativePath(Directory.GetCurrentDirectory(), path))

        // Nobody is watching yet. A browser adds itself when it opens its stream, which is
        // the only moment anybody is really there - and the machines have already played up
        // to the first seat a person has to fill by the time it does.
        let solo, doing =
            Solo.opened game (stampNow ()) model
            |> Solo.against (machines game sitters model)

        errand game doing
        Server.serve reach palette solo stampNow keep

/// Open a table for players at their own machines. Nothing comes back: the table waits
/// until whoever opened it stops the process, because no one player may close it on all
/// the others.
///
/// The seating goes in whole rather than as a count, because a table may have the machine
/// at some of its seats - those are played here and are never waited for. The reach goes in
/// whole for the same reason: how far the table can be reached and what it takes to sit down
/// at it are settled together or they contradict each other.
let private hostFor game view sitters seed reach =
    match dealt game (List.length sitters) seed with
    | Error problem ->
        eprintfn "%s" problem
        1
    | Ok model ->
        let stamp = stampNow ()

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
                    match Client.join game (Reach.at reach "localhost") None (Reach.word reach) view with
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
                Transcript.save game stamp model.Journal |> ignore

        Server.host game reach model sitters keep playing

/// What the menu settled on: a game to play at this keyboard, or a way of playing that
/// runs to its own end and only has an exit code to give back.
[<NoComparison; NoEquality>]
type private Opening<'Move, 'State, 'Notice> =
    | Play of Model<'Move, 'State, 'Notice> * View<'Move, 'State, 'Notice> * Sitter list
    | Done of code: int
    /// The player walked back out of this game's front door. Not the same as leaving: there
    /// is a list of games behind it, and that is where they meant to be.
    | Back

/// The colour screen, which runs until the player is done with it and gives back the view
/// they came in reading - the same one, in whatever colours they settled on.
///
/// It is shown through that view, so the sample colours on it are drawn by the very thing
/// that will be drawing the board.
let rec private colouring game (view: View<_, _, _>) at said =
    match Screens.asking view.Says said (Options.screen view.Palette) at with
    | None, _ -> view
    | Some line, at ->
        match Options.choose view.Palette line with
        | Ok Options.Done -> view
        | Ok Options.Same -> colouring game view at ""
        | Ok(Options.Changed palette) -> colouring game (Playable.recoloured palette game view) at ""
        | Error problem -> colouring game view at problem

/// What the menu settled on, once it is a game rather than another screen.
///
/// Shared by the front door and the seat list, because a seating dealt from one is dealt
/// exactly as it is from the other - and because a way of opening a game added to only one
/// of the two would be a way of opening a game a player could not find.
let private starting game (view: View<_, _, _>) choice =
    let clocked seed =
        seed |> Option.defaultValue (clockSeed ())

    match choice with
    | Menu.Deal(sitters, seed) ->
        dealt game (List.length sitters) (clocked seed)
        |> Result.map (fun model -> Play(model, view, sitters))
    // In whatever colours the player settled on here, which is the same promise the
    // command line's --colour keeps.
    //
    // A line that said nothing about how far the table reaches is answered the way the
    // command line answers the same silence: a word at the door, made up here. Everything the
    // seat list and the screen behind it send says the whole of it, so this is only reached
    // by somebody typing the short way round.
    | Menu.Serve(sitters, seed, reach) ->
        Ok(Done(serveFor game view.Palette sitters (clocked seed) (reach |> Option.defaultWith Reach.fresh)))
    | Menu.Host(sitters, seed, reach) ->
        Ok(Done(hostFor game view sitters (clocked seed) (reach |> Option.defaultWith Reach.fresh)))
    | Menu.Join(address, code) -> Ok(Done(Client.join game address None code view))
    | Menu.Replay path -> replayFrom game path |> Result.map (fun model -> Play(model, view, []))
    // The rest are screens rather than games. The front door answers every one of them
    // itself, so what is left here is somebody at the seat list asking for one of them from
    // there - which is a fair thing to type and wants an answer rather than a shrug.
    | Menu.Sitting _
    | Menu.Reaching _
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
let rec private sitting game (view: View<_, _, _>) word sitters reach at said =
    match Screens.asking view.Says said (Menu.seats game sitters reach) at with
    | None, _ -> Some(Done 0)
    | Some line, at -> answering game view word sitters reach at line sitting

and private reaching game (view: View<_, _, _>) word sitters reach at said =
    match Screens.asking view.Says said (Menu.reaches word sitters reach) at with
    | None, _ -> Some(Done 0)
    | Some line, at -> answering game view word sitters reach at line reaching

/// What either of them does with a line, which is the same thing: the seats and the reach
/// come back out of it, and where they are shown next is the line's own answer.
and private answering game view word sitters reach at line asked =
    let again sitters reach said =
        asked game view word sitters reach at said

    match Menu.choose game view.Palette line with
    | Ok(Menu.Sitting(sitters, asked)) -> sitting game view word sitters (asked |> Option.defaultValue reach) at ""
    | Ok(Menu.Reaching(sitters, reach)) -> reaching game view word sitters reach at ""
    | Ok Menu.Waiting -> again sitters reach ""
    | Ok Menu.Backing -> None
    // Going is going, from wherever it is said.
    | Ok Menu.Leave -> Some(Done 0)
    | Ok chosen ->
        match starting game view chosen with
        | Ok opening -> Some opening
        | Error problem -> again sitters reach problem
    | Error problem -> again sitters reach problem

/// The start menu, which runs until it has settled on a game. Everything it offers either
/// opens one or comes back round to here, so there is no way out of it but the two, and no
/// way to be at the prompt with nothing to play.
let rec private welcome game (view: View<_, _, _>) behind at said =
    // The menu is shown in the view it is offering, so 'view rich' shows what rich looks
    // like before a whole game is committed to it.
    match Screens.asking view.Says said (Menu.screen game view behind) at with
    | None, _ -> Done 0
    | Some line, at ->

    /// Say what went wrong and ask again, with the cursor where it was left. The menu is the
    /// only place to be when there is no game yet, so nothing here leaves except by being
    /// asked to.
    let retry problem = welcome game view behind at problem

    match Menu.choose game view.Palette line with
    // There is nothing behind the front door, so backing out of it is asking again.
    | Ok Menu.Waiting -> welcome game view behind at ""
    // Backing out of the front door lands wherever it was opened from, which is the list of
    // games if there is one and here if there is not.
    | Ok Menu.Backing -> if behind then Back else welcome game view behind at ""
    | Ok Menu.Leave -> Done 0
    | Ok Menu.Rules ->
        printfn "%s" view.Rules
        Screens.held ()
        welcome game view behind at ""
    | Ok(Menu.Looking chosen) -> welcome game chosen behind at ""
    | Ok Menu.Options -> welcome game (colouring game view 0 "") behind at ""
    | Ok(Menu.Sitting(sitters, asked)) ->
        // One word for this way through the menu, made up here because a screen cannot make
        // one up: it is what the door starts out holding, and what walking the door shut
        // again puts back, so that a player who opens it to look and changes their mind is
        // not handed a different table than the one they were reading a moment ago.
        let word = Reach.minted ()

        match sitting game view word sitters (asked |> Option.defaultValue (Reach.locked word)) 0 "" with
        | Some opening -> opening
        | None -> welcome game view behind at ""
    | Ok chosen ->
        match starting game view chosen with
        | Ok opening -> opening
        | Error problem -> retry problem
    | Error problem -> retry problem

/// Sit down at a game and play it here. The board the player is looking at when they
/// arrive is drawn by the same code that draws every one after it, because sitting down is
/// simply the first thing that happens at the table.
let private play game view sitters model =
    // The machines take their seats before anybody sits down to watch, so that a table where
    // the first move is theirs has already had it made by the time the first board is drawn.
    let seated, doing =
        Solo.opened game (stampNow ()) model
        |> Solo.against (machines game sitters model)

    errand game doing

    let solo, posts = seated |> Solo.watching Keyboard { Notes = true; View = view }

    tell posts
    loop solo |> ignore
    0

/// Act on what a command line asked for.
///
/// Reading a command line stops at a `Launch`, and this is what one is handed to. So there
/// is one place that knows what opening a game actually involves, and adding a way in means
/// adding a case rather than another road through `main`.
let private opening game (view: View<_, _, _>) launch =
    let orElse sitters outcome =
        match outcome with
        | Ok model ->
            printfn "%s" view.Rules
            play game view sitters model
        | Error problem ->
            eprintfn "%s" problem
            1

    let clocked seed =
        seed |> Option.defaultValue (clockSeed ())

    // The command line names the machines rather than the seats - `--rival hard` is the seat
    // after yours - so what it asks for is a seating said shorter, and it is spelt out into
    // one here. There is one kind of table below this line, and it is the seating.
    match launch with
    | Launch.Deal(players, seed, rivals) -> orElse (Seating.after players rivals) (dealt game players (clocked seed))
    | Launch.Serve(players, seed, rivals, reach) -> serveFor game view.Palette (Seating.after players rivals) (clocked seed) reach
    | Launch.Host(players, seed, reach) -> hostFor game view (Seating.hosting players) (clocked seed) reach
    | Launch.Join(address, token, code) -> Client.join game address token code view
    | Launch.Replay path -> orElse [] (replayFrom game path)

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
let chosen (game: Playable<'Move, 'State, 'Notice>) =
    { new Chosen with
        member _.Name = game.Name
        member _.Title = game.Title
        member _.Blurb = game.Blurb
        member _.Fewest = game.Fewest
        member _.Most = game.Most
        member _.Faults = game.Faults
        member _.FromCommandLine argv = Launch.run game (opening game) argv

        member _.FromMenu behind =
            // The plainest way this game can be drawn at a terminal, which is what somebody
            // who has said nothing about how they read gets. The menu offers the rest.
            let plain = Playable.plainest AtATerminal (Playable.standard game) game

            match welcome game plain behind 0 "" with
            | Play(model, view, sitters) -> Some(play game view sitters model)
            | Done code -> Some code
            | Back -> None }
