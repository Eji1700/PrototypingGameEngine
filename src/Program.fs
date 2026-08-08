module TCModel.Program

open TCModel.Table

/// Which game a line is about, and what is left of it once that is settled.
///
/// The first word, if it is a game's name; everything after it is the launch, read by that
/// game. A line that names no game is about the one there has always been - which is what
/// keeps every command line anybody has written down still working, and what makes a second
/// game one word rather than a new spelling of an old one.
///
/// Here rather than in `Launch`, and it has to be: which game is being opened settles how
/// many may play, what the machines are called and what a move looks like, all of which
/// `Launch` needs before it can read a word.
let private picked argv =
    match argv with
    | word :: rest ->
        match Games.byName word with
        | Some game -> game, rest
        | None -> Games.usually, argv
    | [] -> Games.usually, argv

// --- the screen before there is a game -------------------------------------------------------
//
// Everything else a person is shown is drawn by a view, and a view belongs to a game. This
// one comes first, so it has none - and it is the only screen in the program written in plain
// text on purpose rather than by default.
//
// It is a screen like any other all the same: every row stands for a line somebody could have
// typed instead, and the line it stands for is the game's own name - which is exactly what
// the command line takes.

/// How many may sit down at one, said the way the seat list says it.
let private seating (game: Play.Chosen) =
    if game.Fewest = game.Most then
        $"{game.Fewest} players"
    else
        $"{game.Fewest} to {game.Most} players"

let private picking: Keys.Screen =
    { Title = "Which game?"
      Prose = [ "There is more than one." ]
      Rows =
        [ for index, game in List.indexed Games.all ->
              Keys.sends (Keys.nth index) game.Title $"{seating game} - {game.Blurb}" game.Name ]
      Note =
        [ "Or name one outright, here or on a command line, and everything after it is"
          $"read by that game:  dotnet run -- {Games.usually.Name} play 3" ]
      Backs = None }

/// The picker, which runs until it has settled on a game or the player has gone. Nothing is
/// behind it, so there is no way out of it but those two.
///
/// Where the highlight was left comes back with the answer, so that a player who opens a
/// game, changes their mind and walks back out finds the cursor where they left it rather
/// than at the top of the list.
let rec private asking at said =
    match Screens.asking id said picking at with
    // Nothing more coming, which is a line piped in that has run out.
    | None, at -> None, at
    | Some line, at ->

    match line.Trim().ToLowerInvariant() with
    | "" -> asking at ""
    | "quit"
    | "exit"
    | "q" -> None, at
    | word ->
        match Games.byName word with
        | Some game -> Some game, at
        | None -> asking at $"'{line.Trim()}' is not a game here. There is {Games.names}."

// --- and what opening one comes to -------------------------------------------------------------

/// Nothing is opened at a game that says it is broken. Asked here rather than at each way in,
/// because a map that does not hang together is not a thing to find out about halfway through
/// dealing.
/// Nothing is opened at a game that says it is broken. Asked here rather than at each way in,
/// because a map that does not hang together is not a thing to find out about halfway through
/// dealing.
///
/// `None` back means the player has not finished with this program - they walked out of the
/// game rather than out of the door - so it is a code or it is nothing.
let private opened (game: Play.Chosen) open' =
    match game.Faults with
    | _ :: _ as problems ->
        eprintfn $"{game.Title} does not hang together:"
        problems |> List.iter (eprintfn "  %s")
        Some 1
    | [] -> open' game

/// Ask which game, play it, and ask again if the player walked back out of it. The list is
/// the front door now, so backing out of a game lands here rather than in a shell.
let rec private choosing at =
    match asking at "" with
    | None, _ -> 0
    | Some game, at ->
        match opened game (fun game -> game.FromMenu true) with
        | Some code -> code
        | None -> choosing at

[<EntryPoint>]
let main argv =
    /// A game opened by name and then left to its own menu. Nothing is behind that menu
    /// when the game was named outright, so backing out of it is leaving.
    let alone game =
        opened game (fun game -> game.FromMenu false) |> Option.defaultValue 0

    match List.ofArray argv with
    // Nothing said at all: ask which game, and then let that game's own menu ask the rest.
    // With only one to choose from there is nothing to ask, and it would be rude to.
    | [] ->
        match Games.all with
        | [ only ] -> alone only
        | _ -> choosing 0
    | words ->
        // Arguments say what to open and go straight to it, so a game can still be started
        // from a script or a shortcut. A line that named a game and nothing else has said
        // which game and no more, so it gets that game's menu.
        match picked words with
        | game, [] -> alone game
        | game, launch ->
            opened game (fun game -> Some(game.FromCommandLine launch))
            |> Option.defaultValue 0
