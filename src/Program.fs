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
///
/// Only this program asks the question. A game's own executable has one game in it and the
/// first word of its line is already a launch, which is exactly the difference between the
/// two and the whole of it.
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
///
/// One is a case rather than a number in a sentence, and it is here because there is a game
/// with one seat now: "1 players" reads as a bug in the list, and it was.
let private seating (game: Play.Chosen) =
    match game.Fewest, game.Most with
    | 1, 1 -> "1 player"
    | fewest, most when fewest = most -> $"{fewest} players"
    | fewest, most -> $"{fewest} to {most} players"

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

/// Ask which game, play it, and ask again if the player walked back out of it. The list is
/// the front door here, so backing out of a game lands on it rather than in a shell.
///
/// Which is the one thing this program does that a game's own executable cannot: `Play.opened`
/// is the same refusal at both, and `FromMenu true` is the same menu with somewhere behind it.
let rec private choosing at =
    match asking at "" with
    | None, _ -> 0
    | Some game, at ->
        match Play.opened game (fun game -> game.FromMenu true) with
        | Some code -> code
        | None -> choosing at

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    // Nothing said at all: ask which game, and then let that game's own menu ask the rest.
    // With only one to choose from there is nothing to ask, and it would be rude to.
    | [] ->
        match Games.all with
        | [ only ] -> Play.alone only []
        | _ -> choosing 0
    // Arguments say what to open and go straight to it, so a game can still be started from a
    // script or a shortcut. Once the game is settled there is nothing left that a game's own
    // executable does not do identically, so `Play.alone` does it for both.
    | words -> picked words ||> Play.alone
