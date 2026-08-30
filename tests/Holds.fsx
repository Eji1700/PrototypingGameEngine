// FsCheck, held to the checks: a property that fails is one failed check, with FsCheck's own
// account of the case that broke it printed under the line. Loaded by the suites that lean on a
// property rather than an example, so the wrapper is written once.
#r "nuget: FsCheck, 3.3.3"

#load "Checks.fsx"

open FsCheck

/// `cases` is how many the property is tried against, since a suite says how hard to lean on it.
let holds cases name property =
    let config = Config.QuickThrowOnFailure.WithMaxTest(cases).WithQuietOnSuccess(true)

    let failure =
        try
            Check.One(config, property)
            None
        with problem ->
            Some problem.Message

    match failure with
    | None -> Checks.report name true true
    | Some message ->
        Checks.report name true false

        message.Split '\n'
        |> Array.iter (fun line -> printfn "     %s" (line.TrimEnd()))
