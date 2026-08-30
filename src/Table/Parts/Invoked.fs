namespace Prototyping.Table

open System
open System.IO

module Invoked =

    let private ourName =
        match Environment.ProcessPath with
        | null -> "Proto"
        | path -> Path.GetFileNameWithoutExtension path

    // Whether this is being run from its own source directory, told by `<ourName>.fsproj` being
    // there. That ties a project's file name to the assembly it builds: `Proto.fsproj` has to
    // produce `Proto`, and `Turncoats.fsproj` `Turncoats`, or a clone stops recognising itself and
    // starts telling people to type a name that is not on disk yet. Renaming a project means
    // renaming both, or setting `AssemblyName` to match.
    let private inOurProject () =
        try
            File.Exists(Path.Combine(Directory.GetCurrentDirectory(), $"{ourName}.fsproj"))
        with _ ->
            false

    // How to say "run this program again" in whatever way it was run in the first place: from its own
    // source directory that is `dotnet run --`, and anywhere else it is the built program's own name.
    // Every line the program prints for a person to type comes through here.
    let program = lazy (if inOurProject () then "dotnet run --" else ourName)


    let mutable private one = false

    let isTheOnlyGame () = one <- true

    let opening (game: string) =
        if one then program.Value else $"{program.Value} {game}"

    let another (game: string) =
        if one then None else Some $"{program.Value} {game}"
