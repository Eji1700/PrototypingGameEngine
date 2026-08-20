namespace TCModel.Table

open System
open System.IO

module Invoked =

    let private ourName =
        match Environment.ProcessPath with
        | null -> "TCModel"
        | path -> Path.GetFileNameWithoutExtension path

    let private inOurProject () =
        try
            File.Exists(Path.Combine(Directory.GetCurrentDirectory(), $"{ourName}.fsproj"))
        with _ ->
            false

    let program = lazy (if inOurProject () then "dotnet run --" else ourName)


    let mutable private one = false

    let isTheOnlyGame () = one <- true

    let opening (game: string) =
        if one then program.Value else $"{program.Value} {game}"

    let another (game: string) =
        if one then None else Some $"{program.Value} {game}"
