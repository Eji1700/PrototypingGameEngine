let mutable failures = 0

[<Literal>]
let private Room = 150

/// A value as the lines worth comparing: a string by its own lines, a list by its items, anything
/// else as itself. What makes a failure readable is the one line that differs - a board drawn
/// wrong is eighty lines of which one matters, and printing both boards in full buries it.
let private lines (value: obj) : string list =
    match value with
    | :? string as text -> text.Replace("\r\n", "\n").Split '\n' |> List.ofArray
    | :? System.Collections.IEnumerable as many -> [ for item in many -> sprintf "%A" item ]
    | _ -> (sprintf "%A" value).Replace("\r\n", "\n").Split '\n' |> List.ofArray

let private shortened (text: string) =
    let text = text.Replace("\r", "").Replace("\t", "    ")

    if text.Length <= Room then text else text.Substring(0, Room - 1) + "…"

let private firstDifference expected actual =
    let rec walk at expected actual =
        match expected, actual with
        | e :: expected, a :: actual when e = a -> walk (at + 1) expected actual
        | e :: _, a :: _ -> Some(at, e, a)
        | e :: _, [] -> Some(at, e, "(nothing - it ends here)")
        | [], a :: _ -> Some(at, "(nothing - it ends here)", a)
        | [], [] -> None

    walk 1 expected actual

let report name expected actual =
    if actual = expected then
        printfn "ok   %s" name
    else
        failures <- failures + 1

        let wanted, got = lines (box expected), lines (box actual)

        // One line against one line reads better whole than as a difference between them.
        if List.length wanted <= 1 && List.length got <= 1 then
            printfn "FAIL %s: expected %s, got %s" name (shortened (List.head wanted)) (shortened (List.head got))
        else
            printfn "FAIL %s" name
            printfn "     lines: %d expected, %d got" (List.length wanted) (List.length got)

            match firstDifference wanted got with
            | None -> printfn "     every line agrees, so the difference is in the order or the ends"
            | Some(at, wanted, got) ->
                printfn "     first apart at %d" at
                printfn "       expected  %s" (shortened wanted)
                printfn "       got       %s" (shortened got)

let finish () =
    printfn ""

    if failures = 0 then printfn "all checks passed"
    elif failures = 1 then printfn "1 check failed"
    else printfn "%d checks failed" failures

    exit failures
