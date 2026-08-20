namespace TCModel.Common

type ResultBuilder() =
    member _.Return value = Ok value
    member _.ReturnFrom(outcome: Result<'T, 'E>) = outcome
    member _.Bind(outcome, f) = Result.bind f outcome
    member _.Zero() = Ok()
    member _.Delay(f: unit -> Result<'T, 'E>) = f ()

[<AutoOpen>]
module Validation =

    let result = ResultBuilder()

    let require condition objection =
        if condition then Ok() else Error objection
