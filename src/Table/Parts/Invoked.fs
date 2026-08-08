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

    /// Whether a `dotnet run` from here would find something to run.
    let private inAProject () =
        try
            Directory.EnumerateFiles(Directory.GetCurrentDirectory(), "*.fsproj")
            |> Seq.isEmpty
            |> not
        with _ ->
            false

    let program = lazy (if inAProject () then "dotnet run --" else ourName)
