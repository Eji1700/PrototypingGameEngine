namespace TCModel.Table

open System
open System.IO

/// What to call this program on a command line.
///
/// Everything this program prints for somebody to type - the seat you were given, the line
/// to read out to the room, the header on a record - is an instruction, and an instruction
/// that will not run is worse than none. There are two ways this game gets passed around
/// and they are called two different things: a repository somebody cloned, run with
/// `dotnet run --`, and one file somebody was sent, called by its own name.
///
/// Which is right is not a question about how this process was started. It is a question
/// about where whoever is reading is standing: `dotnet run --` works when there is a project
/// in the current directory to run, and nowhere else. So that is what is asked.
///
/// Read once. It cannot change while the process lives, and it is printed on every board a
/// networked table draws.
module Invoked =

    /// The name of the running program, without the folder it sits in or the `.exe`.
    let private ourName =
        match Environment.ProcessPath with
        | null -> "TCModel"
        | path -> Path.GetFileNameWithoutExtension path

    /// Whether a `dotnet run` from here would run *this* program.
    ///
    /// Any project at all used to be enough, and it stopped being enough the moment there was
    /// more than one project to be standing in. There are eight now - the engine, a game
    /// apiece, and the one that offers them all - so a clone's root holds a project that
    /// `dotnet run` would run and it is not the game whose executable somebody just started
    /// from there.
    /// `dotnet run -- play 5` printed by `Turncoats.exe` in that folder is an instruction that
    /// runs, and runs something else, which is worse than one that does not run at all.
    ///
    /// So the project has to be ours, and the two are named the same on purpose: every project
    /// here sets `AssemblyName` to its own file's name, which is what makes this one lookup the
    /// whole question.
    let private inOurProject () =
        try
            File.Exists(Path.Combine(Directory.GetCurrentDirectory(), $"{ourName}.fsproj"))
        with _ ->
            false

    let program = lazy (if inOurProject () then "dotnet run --" else ourName)

    // --- and whether the game's name goes after it ------------------------------------------
    //
    // A game is its own executable now, and that put a second question beside the first.
    // `Turncoats replay <file>` and `TCModel turncoats replay <file>` are both true lines, and
    // which of them is true here depends on which program is running rather than on anything
    // about the game - so it is answered once, in the same place and for the same reason.

    /// Whether this program has exactly one game in it.
    ///
    /// Told rather than asked, which is the one thing here that is. The name above is a fact
    /// about the world and can be looked up; how many games were compiled into this program is
    /// not something the process can see about itself, so whichever way in built the program
    /// says so, once, before it opens anything.
    let mutable private one = false

    /// Said by a game's own executable, and by nothing else. `Play.only` is the whole of that.
    let isTheOnlyGame () = one <- true

    /// What to type to open this program's game: the program, and the game's name after it
    /// where that is not already said.
    let opening (game: string) =
        if one then program.Value else $"{program.Value} {game}"

    /// And what to type to open a *different* game, where there is such a line at all.
    ///
    /// A program with a list of games in it can always name another of them. A game's own
    /// executable cannot play anything but itself, so there is no line for it to offer, and
    /// having none is how it says so - a program that guessed at what somebody else's
    /// executable is called would be printing an instruction that may not run, which is the
    /// one thing this module exists to stop.
    let another (game: string) =
        if one then None else Some $"{program.Value} {game}"
