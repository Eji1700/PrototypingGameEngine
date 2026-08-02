namespace TCModel

/// Sequences validation steps, short-circuiting on the first objection.
type ResultBuilder() =
    member _.Return value = Ok value
    member _.ReturnFrom(outcome: Result<'T, 'E>) = outcome
    member _.Bind(outcome, f) = Result.bind f outcome
    member _.Zero() = Ok()
    member _.Delay(f: unit -> Result<'T, 'E>) = f ()

[<AutoOpen>]
module Validation =

    let result = ResultBuilder()

    /// Nothing to say when the condition holds; otherwise the objection to it.
    let require condition objection = if condition then Ok() else Error objection
